using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Cli.Headless;

/// <summary>
/// <c>push --from character.json --user &lt;id or email&gt;</c>: costs and validates a character
/// exactly as <see cref="BuildCommand"/> does, refuses it if it breaks a rule, and otherwise
/// writes it into the site's database as a character owned by that account.
///
/// <para><b>The engine judges first and the database is never asked about an illegal
/// character.</b> This is <c>build</c> with a destination: the same strict reader, the same
/// validator, the same report — carried inside this one under <c>build</c> — and nothing here
/// computes a Hero Point. An illegal sheet is exit 1 with every finding, and no query is run.</para>
///
/// <para><b>The row is the browser's row.</b> What lands in <c>characters</c> is byte for byte
/// what <c>ApiCharacterStore.SaveAsync</c> would have sent from a browser: the payload is the
/// storage envelope <c>{"Version":1,"Mode":…,"Sheet":…}</c> the app reads back, the label is the
/// sheet's own name, and the index columns — <c>kind</c>, <c>tier_id</c>, <c>spent</c>,
/// <c>campaign_id</c>, <c>variant_of</c>, <c>variant_kind</c> — are the ones the browser
/// duplicates out of the payload so a list can draw a row without opening one. The write is the
/// server's own upsert from <c>worker/db.js</c>, transcribed: it refuses at the account's cap
/// exactly as a PUT does, and replaces an id the account already holds however full it is.
/// Tests hold the transcription to the original.</para>
///
/// <para><b>Re-running it updates rather than duplicates.</b> A character is found by its label
/// on that account; one match is replaced in place, none is created under a fresh id, and more
/// than one is refused with the ids to choose from, because guessing which of two "Hood"s to
/// overwrite is a decision about somebody's characters. <c>--id</c> names one outright.</para>
///
/// <para><b>It never touches approval.</b> <c>campaign_members</c> — the clones a player sent and
/// a GM approved — is not written here and not read here. A pushed character is exactly what a
/// character the account built itself is: a row in <c>characters</c>, and nothing more.</para>
/// </summary>
public sealed partial class PushCommand
{
    /// <summary>The name of the subcommand, as the first argument.</summary>
    public const string Verb = "push";

    /// <summary>The database could not be reached, or refused the write — at the cap, most likely.</summary>
    public const int DatabaseRefused = 3;

    /// <summary>
    /// The storage envelope's version, which is <c>StoredCharacter.CurrentVersion</c> in
    /// <c>web/</c>. A mismatch is discarded in silence by the browser, so a wrong number here
    /// writes a character nobody can open. Pinned to the browser's source by a test.
    /// </summary>
    public const int EnvelopeVersion = 1;

    /// <summary>The envelope's <c>Mode</c> for a Hero — <c>SheetMode.Hero</c>'s ordinal.</summary>
    public const int HeroMode = 0;

    /// <summary>The envelope's <c>Mode</c> for a Villain — <c>SheetMode.Villain</c>'s ordinal.</summary>
    public const int VillainMode = 1;

    /// <summary>What the server lists a nameless character under, spelled as <c>worker/characters.js</c> spells it.</summary>
    public const string DefaultLabel = "Unnamed character";

    /// <summary>The server's bound on a label; anything longer is refused there with a 400.</summary>
    public const int MaxLabelLength = 80;

    /// <summary>The server's bound on <c>kind</c>, <c>tier_id</c> and <c>variant_kind</c>.</summary>
    public const int MaxIndexFieldLength = 40;

    /// <summary>The server's bound on <c>spent</c>.</summary>
    public const int MaxSpent = 1_000_000;

    /// <summary>
    /// The columns of <c>characters</c> this writes, in the order the server's own
    /// <c>putCharacter</c> names them. Public so the test that compares the two can ask this
    /// program rather than parse its SQL.
    /// </summary>
    public static IReadOnlyList<string> Columns { get; } =
        ["user_id", "id", "label", "payload", "campaign_id", "kind", "tier_id", "spent", "variant_of", "variant_kind", "updated_at"];

    private readonly BuildCommand _build;
    private readonly CostCalculator _costs;
    private readonly ICharacterDatabase _database;
    private readonly Func<long> _now;
    private readonly Func<string> _newId;

    /// <param name="build">The judge. This command adds a destination to it and nothing else.</param>
    /// <param name="costs">For the one figure the row carries that the report does not name as such — <c>spent</c>.</param>
    /// <param name="database">Where the row goes. <see cref="WranglerDatabase"/> in <c>Program.cs</c>; a fake in tests.</param>
    /// <param name="now">Unix milliseconds for <c>updated_at</c>. Injected so a test can pin the row.</param>
    /// <param name="newId">Mints a character id when no existing row matched. Injected so a test can tell a create from an update.</param>
    public PushCommand(
        BuildCommand build,
        CostCalculator costs,
        ICharacterDatabase database,
        Func<long>? now = null,
        Func<string>? newId = null)
    {
        _build = build;
        _costs = costs;
        _database = database;
        _now = now ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _newId = newId ?? NewId;
    }

    public static string Usage =>
        $"""
         Usage: dotnet run -- {Verb} --from <file> --user <id or email> [options]
                dotnet run -- {Verb} --list-users

         Costs and validates a character exactly as `{BuildCommand.Verb}` does, refuses it if it
         breaks a rule, and otherwise writes it into the site's database as a character owned
         by that account — the same row the browser would have saved.

           --from <file>       The character to push, as JSON in the character-sheet shape.
                               Use - to read it from standard input.
           --user <who>        Whose character it becomes: an account id (u_…) or the address
                               the account signed in with.
           --campaign <id>     Put the character in this campaign (g_…), overriding the
                               CampaignId on the file. The campaign is looked up on that account
                               and reported if it is not there; it is not refused, because the
                               browser reports the same state rather than refusing it.
           --id <id>           Replace this character (c_…) rather than finding one by label.
           --dry-run           Look everything up, report exactly what would be written, and
                               write nothing.
           --list-users        Print every account with its id, address and character count,
                               and stop. Reads nothing else.
           --help              This text.

         Re-running it replaces rather than duplicates: a character is matched by its label on
         that account. One match is updated in place; none is created; more than one is refused
         with the ids to pick from, for --id.

         It reaches the database the way the pull script does — `npx wrangler d1 execute
         --remote` — so `npx wrangler login` has to have been run on this machine once.

         Exit codes:
           {BuildCommand.Ok}  written (or, with --dry-run, would be) — warnings may still be reported
           {BuildCommand.CharacterIllegal}  the character breaks a rule; nothing was written and every issue is in the report
           {BuildCommand.InputUnusable}  the input could not be read, the arguments made no sense, or the
              account or character could not be resolved
           {DatabaseRefused}  the database could not be reached or refused the write (the account is at its cap)

         Standard output carries one JSON report whatever the exit. The engine's own report for
         the character is inside it under `build`. Anything about the run itself goes to
         standard error.
         """;

    public int Run(
        IReadOnlyList<string> args,
        TextWriter stdout,
        TextWriter stderr,
        TextReader stdin)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(stdin);

        if (ParseArguments(args, out var options, out var argumentError) is false)
        {
            stderr.WriteLine(argumentError);
            stderr.WriteLine();
            stderr.WriteLine(Usage);
            return Report(stdout, BuildCommand.InputUnusable, "BAD_ARGUMENTS", argumentError);
        }

        if (options.Help)
        {
            stdout.WriteLine(Usage);
            return BuildCommand.Ok;
        }

        try
        {
            return options.ListUsers ? ListUsers(stdout) : Push(options, stdout, stderr, stdin);
        }
        catch (DatabaseException e)
        {
            stderr.WriteLine(e.Message);
            return Report(stdout, DatabaseRefused, "DATABASE_UNREACHABLE",
                "The database could not be reached or refused the request. The reason is on standard error.");
        }
    }

    // ── The push ──────────────────────────────────────────────────────────

    private int Push(Options options, TextWriter stdout, TextWriter stderr, TextReader stdin)
    {
        if (!BuildCommand.ReadCharacter(options.From, stdin, out var sheet, out var readError))
            return Report(stdout, BuildCommand.InputUnusable, "INPUT_UNREADABLE", readError);

        // The flag wins over the file, as --trait-cap does in build: the caller who typed it
        // wants this character in that game. Unlike --trait-cap it IS written — into the payload
        // as well as the column, because the browser reads the campaign off the sheet and a
        // column disagreeing with its own payload is a list that lies.
        if (options.Campaign is { } campaign) sheet.CampaignId = campaign;

        if (RowShapeError(sheet) is { } shapeError)
            return Report(stdout, BuildCommand.InputUnusable, "ROW_REFUSED", shapeError);

        // ── The engine decides. Nothing below runs for an illegal character. ──
        var (build, verdict) = _build.Judge(sheet, stderr);

        if (verdict != BuildCommand.Ok)
        {
            var refused = new JsonObject
            {
                ["ok"]        = false,
                ["exit_code"] = verdict,
                ["action"]    = "refused",
                ["build"]     = build,
                ["issues"]    = build["issues"]?.DeepClone() ?? new JsonArray(),
            };
            stdout.WriteLine(refused.ToJsonString(Formatting));
            return verdict;
        }

        var label   = LabelFor(sheet);
        var payload = Envelope(sheet);
        var spent   = Spent(sheet);

        // ── Who, and which row: one round trip, all reads. ──
        var lookup = _database.Execute(LookupSql(options.User, label, options.Id, sheet.CampaignId));

        IReadOnlyList<JsonObject> users = lookup.Count > 0 ? lookup[0] : [];
        if (users.Count != 1)
        {
            return Report(stdout, BuildCommand.InputUnusable, "UNKNOWN_USER",
                users.Count == 0
                    ? $"No account has the id or address '{options.User}'. Run --list-users to see them."
                    : $"'{options.User}' matched {users.Count} accounts, which should not be possible; nothing was written.");
        }

        var user = users[0];
        var userId = (string)user["id"]!;

        IReadOnlyList<JsonObject> existing = lookup.Count > 1 ? lookup[1] : [];
        string id;
        string would;

        if (options.Id is { } named)
        {
            id = named;
            would = existing.Count > 0 ? "update" : "create";
        }
        else if (existing.Count == 0)
        {
            id = _newId();
            would = "create";
        }
        else if (existing.Count == 1)
        {
            id = (string)existing[0]["id"]!;
            would = "update";
        }
        else
        {
            var ids = existing.Select(r => (string)r["id"]!).ToList();
            var report = ErrorReport(BuildCommand.InputUnusable, "AMBIGUOUS_CHARACTER",
                $"This account holds {existing.Count} characters labelled '{label}'. Pass --id with the one to replace.");
            report["issues"]![0]!["options"] = new JsonArray([.. ids.Select(i => JsonValue.Create(i))]);
            stdout.WriteLine(report.ToJsonString(Formatting));
            return BuildCommand.InputUnusable;
        }

        var issues = new JsonArray();

        if (sheet.CampaignId is { } wanted)
        {
            IReadOnlyList<JsonObject> campaigns = lookup.Count > 2 ? lookup[2] : [];
            if (campaigns.Count == 0)
            {
                // A warning and not a refusal: the server never validates this reference either,
                // and the browser reports a character naming a campaign that is not there.
                issues.Add(new JsonObject
                {
                    ["severity"] = "warning",
                    ["code"]     = "UNKNOWN_CAMPAIGN",
                    ["message"]  = $"This account has no campaign '{wanted}'. The character will be written naming it anyway, which the browser reports rather than refuses.",
                    ["value"]    = wanted,
                });
            }
        }

        var row = new Row(
            userId, id, label, payload, sheet.CampaignId,
            sheet.IsVillain ? "villain" : "hero", sheet.SelectedTierId, spent,
            sheet.Variant?.OfCharacterId, sheet.Variant?.Kind, _now());

        var sql = UpsertSql(row);

        var result = new JsonObject
        {
            ["ok"]        = true,
            ["exit_code"] = BuildCommand.Ok,
            ["action"]    = options.DryRun ? "dry-run" : would + "d",
            ["would"]     = options.DryRun ? would : null,
            ["user"]      = Account(user),
            ["character"] = new JsonObject
            {
                ["id"]            = row.Id,
                ["label"]         = row.Label,
                ["kind"]          = row.Kind,
                ["tier_id"]       = row.TierId,
                ["spent"]         = row.Spent,
                ["campaign_id"]   = row.CampaignId,
                ["variant_of"]    = row.VariantOf,
                ["variant_kind"]  = row.VariantKind,
                ["updated_at"]    = row.UpdatedAt,
                ["payload_bytes"] = System.Text.Encoding.UTF8.GetByteCount(row.Payload),
            },
            ["build"]     = build,
            ["issues"]    = issues,
        };

        if (options.DryRun)
        {
            result["sql"] = sql;
            stdout.WriteLine(result.ToJsonString(Formatting));
            return BuildCommand.Ok;
        }

        var written = _database.Execute(sql);

        // No row back is the server's own "409: this account already holds N characters" — the
        // upsert's WHERE let nothing through. See putCharacter in worker/db.js.
        if (written.Count == 0 || written[0].Count == 0)
        {
            result["ok"]        = false;
            result["exit_code"] = DatabaseRefused;
            result["action"]    = "refused";
            issues.Add(new JsonObject
            {
                ["severity"] = "error",
                ["code"]     = "ACCOUNT_FULL",
                ["message"]  = $"This account already holds {user["character_count"]} characters, which is its cap of {user["character_limit"]}. Nothing was written.",
                ["value"]    = user["character_count"]?.DeepClone(),
                ["limit"]    = user["character_limit"]?.DeepClone(),
            });
            stdout.WriteLine(result.ToJsonString(Formatting));
            return DatabaseRefused;
        }

        stdout.WriteLine(result.ToJsonString(Formatting));
        return BuildCommand.Ok;
    }

    private int ListUsers(TextWriter stdout)
    {
        var rows = _database.Execute(ListUsersSql());
        IReadOnlyList<JsonObject> users = rows.Count > 0 ? rows[0] : [];

        var report = new JsonObject
        {
            ["ok"]        = true,
            ["exit_code"] = BuildCommand.Ok,
            ["users"]     = new JsonArray([.. users.Select(r => (JsonNode)Account(r))]),
        };
        stdout.WriteLine(report.ToJsonString(Formatting));
        return BuildCommand.Ok;
    }

    private static JsonObject Account(JsonObject row) => new()
    {
        ["id"]              = row["id"]?.DeepClone(),
        ["email"]           = row["email"]?.DeepClone(),
        ["display_name"]    = row["display_name"]?.DeepClone(),
        ["character_limit"] = row["character_limit"]?.DeepClone(),
        ["character_count"] = row["character_count"]?.DeepClone(),
    };

    // ── The row, as the browser would have sent it ────────────────────────

    /// <summary>One row of <c>characters</c>, in column order.</summary>
    private sealed record Row(
        string UserId, string Id, string Label, string Payload, string? CampaignId,
        string Kind, string? TierId, int? Spent, string? VariantOf, string? VariantKind, long UpdatedAt);

    /// <summary>
    /// The name a character is listed under: the sheet's own, trimmed, or the server's default.
    /// The same spelling as <c>SavedCharacters.LabelFor</c> in <c>web/</c>.
    /// </summary>
    public static string LabelFor(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return string.IsNullOrWhiteSpace(sheet.Name) ? DefaultLabel : sheet.Name.Trim();
    }

    /// <summary>
    /// The storage envelope, <c>{"Version":1,"Mode":…,"Sheet":…}</c> — what
    /// <c>StoredCharacter.Write</c> in <c>web/</c> produces, with the sheet written by the same
    /// <see cref="CharacterSheetJson"/> the browser uses, so the two cannot disagree about a field.
    /// </summary>
    public static string Envelope(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var envelope = new JsonObject
        {
            ["Version"] = EnvelopeVersion,
            ["Mode"]    = sheet.IsVillain ? VillainMode : HeroMode,
            ["Sheet"]   = JsonNode.Parse(CharacterSheetJson.Write(sheet)),
        };

        return envelope.ToJsonString();
    }

    /// <summary>
    /// The engine's price, or null where it declines to give one — the browser sends null for
    /// exactly that case too, and a legal character has already been priced by the time this is
    /// asked, so null here is the engine's answer rather than a gap.
    /// </summary>
    private int? Spent(CharacterSheet sheet)
    {
        try { return _costs.TotalCost(sheet); }
        catch (InvalidOperationException) { return null; }
    }

    /// <summary>
    /// What the server would refuse with a 400 before storing: an id of the wrong shape, a
    /// label or index field longer than its column is asked to hold. Checked here because the
    /// SQL goes in under the server, not through it.
    /// </summary>
    private static string? RowShapeError(CharacterSheet sheet)
    {
        if (sheet.CampaignId is { } g && !CampaignId().IsMatch(g))
            return $"'{g}' is not a campaign id the server uses (g_ followed by 22 URL-safe characters).";

        if (sheet.Variant?.OfCharacterId is { } v && !CharacterId().IsMatch(v))
            return $"Variant.OfCharacterId '{v}' is not a character id the server uses (c_ followed by 22 URL-safe characters).";

        if (LabelFor(sheet).Length > MaxLabelLength)
            return $"The name is {LabelFor(sheet).Length} characters and the server lists at most {MaxLabelLength}.";

        if (sheet.SelectedTierId is { Length: > MaxIndexFieldLength })
            return $"The tier id is longer than the {MaxIndexFieldLength} characters the server stores.";

        if (sheet.Variant?.Kind is { Length: > MaxIndexFieldLength })
            return $"Variant.Kind is longer than the {MaxIndexFieldLength} characters the server stores.";

        return null;
    }

    /// <summary><c>c_</c> plus 22 URL-safe characters: 16 random bytes, base64url, no padding — the browser's own mint.</summary>
    public static string NewId()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var text = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return $"c_{text}";
    }

    /// <summary>The character id shape <c>worker/characters.js</c> accepts. Pinned to it by a test.</summary>
    public const string CharacterIdPattern = "^c_[A-Za-z0-9_-]{22}$";

    /// <summary>The campaign id shape <c>worker/characters.js</c> accepts. Pinned to it by a test.</summary>
    public const string CampaignIdPattern = "^g_[A-Za-z0-9_-]{22}$";

    [GeneratedRegex(CharacterIdPattern)]
    private static partial Regex CharacterId();

    [GeneratedRegex(CampaignIdPattern)]
    private static partial Regex CampaignId();

    // ── SQL ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The user, the rows already labelled this way (or the one named by <c>--id</c>), and the
    /// campaign — three statements, one round trip, nothing written. The account is resolved
    /// inside each statement rather than in this program, so the three cannot disagree about
    /// who was asked about.
    /// </summary>
    internal static string LookupSql(string who, string label, string? id, string? campaignId)
    {
        var key = who.Trim();
        var account = $"(SELECT id FROM users WHERE id = {Literal(key)} OR email = {Literal(key.ToLowerInvariant())})";

        var sql =
            $"SELECT u.id, u.email, u.display_name, u.character_limit, "
            + "(SELECT COUNT(*) FROM characters c WHERE c.user_id = u.id) AS character_count "
            + $"FROM users u WHERE u.id = {account};\n"
            + "SELECT id, label, updated_at FROM characters "
            + $"WHERE user_id = {account} AND "
            + (id is null ? $"label = {Literal(label)}" : $"id = {Literal(id)}")
            + ";\n";

        if (campaignId is not null)
            sql += $"SELECT id, label FROM campaigns WHERE user_id = {account} AND id = {Literal(campaignId)};\n";

        return sql;
    }

    public static string ListUsersSql() =>
        "SELECT u.id, u.email, u.display_name, u.character_limit, "
        + "(SELECT COUNT(*) FROM characters c WHERE c.user_id = u.id) AS character_count "
        + "FROM users u ORDER BY u.email;\n";

    /// <summary>
    /// <c>putCharacter</c> from <c>worker/db.js</c>, with literals where it binds parameters.
    /// One statement, for the reason that one is: the cap check is inside the write's own
    /// <c>WHERE</c>, an id the account already holds is let through however full it is, and
    /// <c>RETURNING id</c> is how the caller learns whether anything landed.
    /// </summary>
    private static string UpsertSql(Row row)
    {
        var values = string.Join(", ", new[]
        {
            Literal(row.UserId), Literal(row.Id), Literal(row.Label), Literal(row.Payload),
            Literal(row.CampaignId), Literal(row.Kind), Literal(row.TierId), Literal(row.Spent),
            Literal(row.VariantOf), Literal(row.VariantKind), Literal(row.UpdatedAt),
        });

        return
            $"INSERT INTO characters ({string.Join(", ", Columns)}) "
            + $"SELECT {values} "
            + $"WHERE EXISTS (SELECT 1 FROM characters WHERE user_id = {Literal(row.UserId)} AND id = {Literal(row.Id)}) "
            + $"OR (SELECT COUNT(*) FROM characters WHERE user_id = {Literal(row.UserId)}) "
            + $"< (SELECT character_limit FROM users WHERE id = {Literal(row.UserId)}) "
            + "ON CONFLICT (user_id, id) DO UPDATE SET "
            + "label = excluded.label, payload = excluded.payload, "
            + "campaign_id = excluded.campaign_id, kind = excluded.kind, "
            + "tier_id = excluded.tier_id, spent = excluded.spent, "
            + "variant_of = excluded.variant_of, variant_kind = excluded.variant_kind, "
            + "updated_at = excluded.updated_at "
            + "RETURNING id;\n";
    }

    /// <summary>
    /// A SQL string literal. The only escape SQLite has is the doubled quote, and a payload's
    /// prose is full of the single one. Backslashes mean nothing to it and are left alone.
    /// </summary>
    public static string Literal(string? value) =>
        value is null ? "NULL" : "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string Literal(long? value) =>
        value is null ? "NULL" : value.Value.ToString(CultureInfo.InvariantCulture);

    // ── Arguments ─────────────────────────────────────────────────────────

    private sealed record Options(
        string From, string User, string? Campaign, string? Id, bool DryRun, bool ListUsers, bool Help);

    private static readonly Options NoOptions = new("", "", null, null, false, false, false);

    private static bool ParseArguments(IReadOnlyList<string> args, out Options options, out string error)
    {
        string? from = null, user = null, campaign = null, id = null;
        bool dryRun = false, listUsers = false, help = false;
        error = "";
        options = NoOptions;

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h": help = true; break;
                case "--dry-run": dryRun = true; break;
                case "--list-users": listUsers = true; break;

                case "--from" or "--user" or "--campaign" or "--id":
                {
                    if (i + 1 >= args.Count)
                    {
                        error = $"{args[i]} needs a value after it.";
                        return false;
                    }

                    var value = args[i + 1];
                    i++;

                    switch (args[i - 1])
                    {
                        case "--from": from = value; break;
                        case "--user": user = value; break;
                        case "--campaign": campaign = value; break;
                        default: id = value; break;
                    }

                    break;
                }

                default:
                    error = $"'{args[i]}' is not an option this command has.";
                    return false;
            }
        }

        if (help)
        {
            options = NoOptions with { Help = true };
            return true;
        }

        if (listUsers)
        {
            options = NoOptions with { ListUsers = true };
            return true;
        }

        if (from is null)
        {
            error = "No character was given. Pass --from <file>, or --from - to read one from standard input.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(user))
        {
            error = "No account was given. Pass --user <id or email>; --list-users prints them.";
            return false;
        }

        if (campaign is not null && !CampaignId().IsMatch(campaign))
        {
            error = $"--campaign '{campaign}' is not a campaign id the server uses (g_ followed by 22 URL-safe characters).";
            return false;
        }

        if (id is not null && !CharacterId().IsMatch(id))
        {
            error = $"--id '{id}' is not a character id the server uses (c_ followed by 22 URL-safe characters).";
            return false;
        }

        options = new(from, user, campaign, id, dryRun, false, false);
        return true;
    }

    // ── Reporting ─────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions Formatting = new() { WriteIndented = true };

    private static int Report(TextWriter stdout, int exitCode, string code, string message)
    {
        stdout.WriteLine(ErrorReport(exitCode, code, message).ToJsonString(Formatting));
        return exitCode;
    }

    private static JsonObject ErrorReport(int exitCode, string code, string message) =>
        new()
        {
            ["ok"]        = false,
            ["exit_code"] = exitCode,
            ["action"]    = "refused",
            ["issues"]    = new JsonArray(new JsonObject
            {
                ["severity"] = "error",
                ["code"]     = code,
                ["message"]  = message,
            }),
        };
}
