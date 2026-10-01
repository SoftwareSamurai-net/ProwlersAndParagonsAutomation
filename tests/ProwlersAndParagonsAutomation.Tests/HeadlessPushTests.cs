using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Cli.Headless;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The headless <c>push</c> command, end to end, against a database that records what it was
/// asked and answers what the test scripted.
///
/// <para><b>Every test that expects nothing to be written proves it by counting statements</b>,
/// not by reading a sentence: a refusal that still ran the upsert would print the same report.
/// And the contract tests at the end read the server's and the browser's own source, because
/// the SQL and the envelope this command writes are transcriptions of theirs — a copy nobody
/// checks against its original is the drift this repository keeps finding.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class HeadlessPushTests : IDisposable
{
    private readonly RulesFixture _f;
    private readonly string _scratch;

    public HeadlessPushTests(RulesFixture f)
    {
        _f = f;
        _scratch = Path.Combine(Path.GetTempPath(), "pp-push-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // ── Databases ─────────────────────────────────────────────────────────

    /// <summary>
    /// A database that must never be asked. The positive control for every refusal that is
    /// supposed to happen before the first query — an illegal character, bad arguments.
    /// </summary>
    internal sealed class NoDatabase : ICharacterDatabase
    {
        public IReadOnlyList<IReadOnlyList<JsonObject>> Execute(string sql) =>
            throw new InvalidOperationException($"The database was asked, and this test says it must not be:\n{sql}");
    }

    /// <summary>Records every statement and answers the lookup and the upsert as scripted.</summary>
    private sealed class Scripted : ICharacterDatabase
    {
        public List<string> Sql { get; } = [];

        public JsonObject? User { get; init; } = Gm();
        public List<JsonObject> Existing { get; init; } = [];
        public bool CampaignFound { get; init; } = true;
        public bool UpsertLands { get; init; } = true;

        public IReadOnlyList<IReadOnlyList<JsonObject>> Execute(string sql)
        {
            Sql.Add(sql);

            if (sql.StartsWith("INSERT", StringComparison.Ordinal))
                return UpsertLands ? [[new JsonObject { ["id"] = "c_returned" }]] : [[]];

            if (sql == PushCommand.ListUsersSql())
                return [[Gm(), Gm()]];   // --list-users: two rows

            var statements = new List<IReadOnlyList<JsonObject>>
            {
                User is null ? [] : [User],
                Existing,
            };
            if (sql.Contains("FROM campaigns", StringComparison.Ordinal))
                statements.Add(CampaignFound ? [new JsonObject { ["id"] = "g_x", ["label"] = "Game" }] : []);

            return statements;
        }
    }

    private static JsonObject Gm() => new()
    {
        ["id"] = "u_gm",
        ["email"] = "gm@example.net",
        ["display_name"] = "GM",
        ["character_limit"] = 5,
        ["character_count"] = 2,
    };

    private const string MintedId = "c_AAAAAAAAAAAAAAAAAAAAAA";
    private const string ExistingId = "c_BBBBBBBBBBBBBBBBBBBBBB";
    private const string AGame = "g_EQVHwU_Bl0VZdFrEssjk0A";

    // ── Running it ────────────────────────────────────────────────────────

    private sealed record Run(int ExitCode, string StdOut, string StdErr)
    {
        public JsonNode Report => JsonNode.Parse(StdOut)
            ?? throw new InvalidOperationException($"The command wrote no JSON report:\n{StdOut}");

        public JsonArray Issues => Report["issues"]!.AsArray();

        public JsonNode? Issue(string code) => Issues.FirstOrDefault(i => (string?)i!["code"] == code);
    }

    private Run Invoke(ICharacterDatabase db, params string[] args) =>
        Invoke(db, () => MintedId, args);

    private Run Invoke(ICharacterDatabase db, Func<string> newId, params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var build = new BuildCommand(_f.Rules, _f.Costs, _f.Derived, _f.Validator);
        var exit = new PushCommand(build, _f.Costs, db, now: () => 1_700_000_000_000, newId: newId)
            .Run(args, stdout, stderr, new StringReader(""));

        return new Run(exit, stdout.ToString(), stderr.ToString());
    }

    /// <summary>
    /// The engine's sample Villain, built in the Villain palette. <c>SampleCharacters.Villain()</c>
    /// leaves <c>IsVillain</c> false — it is a sheet for the printed preview, and the palette is
    /// the browser's — so the flag the envelope's <c>Mode</c> and the row's <c>kind</c> are read
    /// from is set here, deliberately, as the browser would have set it.
    /// </summary>
    private static CharacterSheet AVillain()
    {
        var villain = SampleCharacters.Villain();
        villain.IsVillain = true;
        return villain;
    }

    private string CharacterFile(CharacterSheet sheet)
    {
        var path = Path.Combine(_scratch, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, CharacterSheetJson.Write(sheet));
        return path;
    }

    private static readonly Regex Writes = new(@"\b(INSERT|UPDATE|DELETE|DROP|ALTER)\b", RegexOptions.None, TimeSpan.FromSeconds(5));

    // ── The push ──────────────────────────────────────────────────────────

    [Fact]
    public void ADryRunLooksEverythingUpAndWritesNothing()
    {
        var villain = AVillain();
        var db = new Scripted();

        var run = Invoke(db, "--from", CharacterFile(villain), "--user", "GM@Example.net", "--dry-run");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Equal("dry-run", (string?)run.Report["action"]);
        Assert.Equal("create", (string?)run.Report["would"]);

        // One round trip, and it reads only.
        var lookup = Assert.Single(db.Sql);
        Assert.DoesNotMatch(Writes, lookup);
        Assert.Contains("'gm@example.net'", lookup, StringComparison.Ordinal);   // lower-cased, as the server normalises it

        Assert.Equal("u_gm", (string?)run.Report["user"]!["id"]);
        Assert.Equal(MintedId, (string?)run.Report["character"]!["id"]);
        Assert.Equal("villain", (string?)run.Report["character"]!["kind"]);
        Assert.Equal(villain.SelectedTierId, (string?)run.Report["character"]!["tier_id"]);
        Assert.Equal(_f.Costs.TotalCost(villain), (int)run.Report["character"]!["spent"]!);
        Assert.Equal(1_700_000_000_000, (long)run.Report["character"]!["updated_at"]!);

        // The engine's own report travels inside, so the caller has one document to read.
        Assert.True((bool)run.Report["build"]!["ok"]!);
        Assert.Equal(_f.Costs.TotalCost(villain), (int)run.Report["build"]!["hero_points"]!["spent"]!);

        // And the statement that would have run is shown, whole.
        var sql = (string?)run.Report["sql"];
        Assert.NotNull(sql);
        Assert.StartsWith("INSERT INTO characters (", sql, StringComparison.Ordinal);
        Assert.Contains($"'{MintedId}'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void APushWritesTheRowTheBrowserWouldHave()
    {
        var villain = AVillain();
        var db = new Scripted();

        var run = Invoke(db, "--from", CharacterFile(villain), "--user", "u_gm");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Equal("created", (string?)run.Report["action"]);
        Assert.Equal(2, db.Sql.Count);

        var upsert = db.Sql[1];
        Assert.StartsWith($"INSERT INTO characters ({string.Join(", ", PushCommand.Columns)}) SELECT ", upsert, StringComparison.Ordinal);
        Assert.Contains("'u_gm', " + $"'{MintedId}', ", upsert, StringComparison.Ordinal);
        Assert.Contains($"'{PushCommand.LabelFor(villain)}'", upsert, StringComparison.Ordinal);

        // The payload literal in the statement is the browser's envelope, escaped and whole.
        Assert.Contains(PushCommand.Literal(PushCommand.Envelope(villain)), upsert, StringComparison.Ordinal);
        Assert.Contains("RETURNING id", upsert, StringComparison.Ordinal);
    }

    private static readonly string[] EnvelopeKeys = ["Version", "Mode", "Sheet"];

    [Fact]
    public void TheEnvelopeRoundTripsThroughTheEngineAtTheSameCost()
    {
        var villain = AVillain();
        var envelope = JsonNode.Parse(PushCommand.Envelope(villain))!.AsObject();

        // Key order is the browser's record's: Version, Mode, Sheet.
        Assert.Equal(EnvelopeKeys, envelope.Select(p => p.Key).ToArray());
        Assert.Equal(PushCommand.EnvelopeVersion, (int)envelope["Version"]!);
        Assert.Equal(PushCommand.VillainMode, (int)envelope["Mode"]!);

        var back = CharacterSheetJson.Read(envelope["Sheet"]!.ToJsonString(), strict: true);
        Assert.NotNull(back);
        Assert.True(back.IsVillain);
        Assert.Equal(_f.Costs.TotalCost(villain), _f.Costs.TotalCost(back));
        Assert.True(_f.Validator.Validate(back).IsValid);

        var hero = SampleCharacters.Hero();
        Assert.Equal(PushCommand.HeroMode, (int)JsonNode.Parse(PushCommand.Envelope(hero))!["Mode"]!);
    }

    [Fact]
    public void ARerunReplacesTheRowItFindsByLabelAndMintsNothing()
    {
        var villain = AVillain();
        var db = new Scripted { Existing = [new JsonObject { ["id"] = ExistingId, ["label"] = villain.Name }] };

        var run = Invoke(db, () => throw new InvalidOperationException("an id was minted for an update"),
            "--from", CharacterFile(villain), "--user", "u_gm");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Equal("updated", (string?)run.Report["action"]);
        Assert.Equal(ExistingId, (string?)run.Report["character"]!["id"]);
        Assert.Contains($"'{ExistingId}'", db.Sql[1], StringComparison.Ordinal);

        // The lookup asked by label, with the label the row is written under.
        Assert.Contains($"label = '{PushCommand.LabelFor(villain)}'", db.Sql[0], StringComparison.Ordinal);
    }

    [Fact]
    public void TwoRowsWithTheSameLabelAreRefusedNotGuessed()
    {
        var villain = AVillain();
        var db = new Scripted
        {
            Existing = [new JsonObject { ["id"] = ExistingId }, new JsonObject { ["id"] = MintedId }],
        };

        var run = Invoke(db, "--from", CharacterFile(villain), "--user", "u_gm");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        var issue = run.Issue("AMBIGUOUS_CHARACTER");
        Assert.NotNull(issue);
        Assert.Equal(new[] { ExistingId, MintedId }, issue["options"]!.AsArray().Select(o => (string?)o).ToArray());
        Assert.Single(db.Sql);   // the lookup, and nothing after it
    }

    [Fact]
    public void NamingTheIdSkipsTheLabelLookupAndReplacesThatRow()
    {
        var villain = AVillain();
        var db = new Scripted { Existing = [new JsonObject { ["id"] = ExistingId }] };

        var run = Invoke(db, "--from", CharacterFile(villain), "--user", "u_gm", "--id", ExistingId);

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Equal("updated", (string?)run.Report["action"]);
        Assert.Contains($"id = '{ExistingId}'", db.Sql[0], StringComparison.Ordinal);
        Assert.DoesNotContain("label = ", db.Sql[0], StringComparison.Ordinal);
    }

    [Fact]
    public void AnIllegalCharacterIsRefusedBeforeTheDatabaseIsAsked()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Flaws.Add(new SelectedFlaw(_f.Rules.Flaws[0].Id));
        foreach (var ability in _f.Rules.Abilities) sheet.AbilityRanks[ability.Id] = 12;
        foreach (var talent in _f.Rules.Talents) sheet.TalentRanks[talent.Id] = 12;

        var run = Invoke(new NoDatabase(), "--from", CharacterFile(sheet), "--user", "u_gm");

        Assert.Equal(BuildCommand.CharacterIllegal, run.ExitCode);
        Assert.Equal("refused", (string?)run.Report["action"]);
        Assert.False((bool)run.Report["ok"]!);
        Assert.NotNull(run.Issue("HP_BUDGET_EXCEEDED"));
        Assert.False((bool)run.Report["build"]!["ok"]!);
    }

    [Fact]
    public void AnUnreadableFileIsRefusedBeforeTheDatabaseIsAsked()
    {
        var path = Path.Combine(_scratch, "not-a-character.json");
        File.WriteAllText(path, "{\"AbilityRnaks\": {}}");

        var run = Invoke(new NoDatabase(), "--from", path, "--user", "u_gm");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("INPUT_UNREADABLE"));
    }

    [Fact]
    public void AnUnknownAccountIsRefusedAndNothingIsWritten()
    {
        var db = new Scripted { User = null };

        var run = Invoke(db, "--from", CharacterFile(AVillain()), "--user", "nobody@example.net");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("UNKNOWN_USER"));
        Assert.Single(db.Sql);
    }

    [Fact]
    public void AFullAccountIsRefusedTheWayTheServerRefusesIt()
    {
        var db = new Scripted { UpsertLands = false };

        var run = Invoke(db, "--from", CharacterFile(AVillain()), "--user", "u_gm");

        Assert.Equal(PushCommand.DatabaseRefused, run.ExitCode);
        Assert.Equal("refused", (string?)run.Report["action"]);
        var issue = run.Issue("ACCOUNT_FULL");
        Assert.NotNull(issue);
        Assert.Equal(5, (int)issue["limit"]!);
        Assert.Equal(2, db.Sql.Count);
    }

    [Fact]
    public void ADatabaseThatCannotBeReachedIsExitThreeWithAReport()
    {
        var run = Invoke(new Unreachable(), "--from", CharacterFile(AVillain()), "--user", "u_gm");

        Assert.Equal(PushCommand.DatabaseRefused, run.ExitCode);
        Assert.NotNull(run.Issue("DATABASE_UNREACHABLE"));
        Assert.Contains("wrangler is not here", run.StdErr, StringComparison.Ordinal);
    }

    private sealed class Unreachable : ICharacterDatabase
    {
        public IReadOnlyList<IReadOnlyList<JsonObject>> Execute(string sql) =>
            throw new DatabaseException("wrangler is not here");
    }

    [Fact]
    public void ApostrophesAreDoubledForSqliteAndNothingElseIsEscaped()
    {
        Assert.Equal("'O''Brien''s'", PushCommand.Literal("O'Brien's"));
        Assert.Equal("'a\\b\"c'", PushCommand.Literal("a\\b\"c"));
        Assert.Equal("NULL", PushCommand.Literal(null));

        var villain = AVillain();
        villain.Name = "O'Brien's Hood";
        villain.Quote = "You're no longer the ones who decide.";
        var db = new Scripted();

        var run = Invoke(db, "--from", CharacterFile(villain), "--user", "u_gm");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);

        // The label is prose and carries the doubled quote; the payload is JSON, and
        // System.Text.Json has already written its apostrophes as \u0027, so there is nothing
        // in it for the doubling to touch — and nothing in it a bare quote could break.
        Assert.Contains("'O''Brien''s Hood'", db.Sql[1], StringComparison.Ordinal);
        Assert.Contains("'O''Brien''s Hood'", db.Sql[0], StringComparison.Ordinal);
        Assert.Contains("You\\u0027re no longer", db.Sql[1], StringComparison.Ordinal);
        Assert.DoesNotContain("You're", db.Sql[1], StringComparison.Ordinal);

        // Positive control on the escaping itself: the statement as a whole is balanced, so
        // every quote inside a literal has been doubled.
        var quotes = db.Sql[1].Count(c => c == '\'');
        Assert.True(quotes % 2 == 0, $"the upsert has an odd number of quotes ({quotes}); a literal is unterminated");
    }

    [Fact]
    public void TheCampaignFlagOverridesTheFileAndIsWrittenIntoThePayloadToo()
    {
        var villain = AVillain();
        villain.CampaignId = "g_0000000000000000000000";
        var db = new Scripted();

        var run = Invoke(db, "--from", CharacterFile(villain), "--user", "u_gm", "--campaign", AGame);

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Equal(AGame, (string?)run.Report["character"]!["campaign_id"]);
        Assert.Empty(run.Issues);

        villain.CampaignId = AGame;
        Assert.Contains(PushCommand.Literal(PushCommand.Envelope(villain)), db.Sql[1], StringComparison.Ordinal);
        Assert.Contains($"FROM campaigns WHERE user_id = (SELECT id FROM users WHERE id = 'u_gm' OR email = 'u_gm') AND id = '{AGame}'", db.Sql[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ACampaignTheAccountDoesNotHaveIsAWarningNotARefusal()
    {
        var villain = AVillain();
        villain.CampaignId = AGame;
        var db = new Scripted { CampaignFound = false };

        var run = Invoke(db, "--from", CharacterFile(villain), "--user", "u_gm");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Equal("created", (string?)run.Report["action"]);
        var issue = run.Issue("UNKNOWN_CAMPAIGN");
        Assert.NotNull(issue);
        Assert.Equal("warning", (string?)issue["severity"]);
        Assert.Equal(AGame, (string?)issue["value"]);
    }

    [Theory]
    [InlineData("--campaign", "nope")]
    [InlineData("--id", "legacy")]
    [InlineData("--id", "c_tooshort")]
    public void AnIdOfTheWrongShapeIsRefusedAsAnArgument(string flag, string value)
    {
        var run = Invoke(new NoDatabase(), "--from", CharacterFile(AVillain()), "--user", "u_gm", flag, value);

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("BAD_ARGUMENTS"));
        Assert.Contains(value, run.Issue("BAD_ARGUMENTS")!["message"]!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ACampaignIdOfTheWrongShapeOnTheFileIsRefusedBeforeTheEngineOrTheDatabase()
    {
        var villain = AVillain();
        villain.CampaignId = "not-a-campaign";

        var run = Invoke(new NoDatabase(), "--from", CharacterFile(villain), "--user", "u_gm");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("ROW_REFUSED"));
    }

    [Fact]
    public void MissingArgumentsAreReportedAndTheDatabaseIsNotAsked()
    {
        Assert.NotNull(Invoke(new NoDatabase(), "--user", "u_gm").Issue("BAD_ARGUMENTS"));
        Assert.NotNull(Invoke(new NoDatabase(), "--from", "x.json").Issue("BAD_ARGUMENTS"));
        Assert.NotNull(Invoke(new NoDatabase(), "--from", "x.json", "--user", "u_gm", "--bogus").Issue("BAD_ARGUMENTS"));
    }

    [Fact]
    public void ListUsersPrintsEveryAccountAndReadsNothingElse()
    {
        var db = new Scripted();

        var run = Invoke(db, "--list-users");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        var sql = Assert.Single(db.Sql);
        Assert.DoesNotMatch(Writes, sql);
        Assert.Equal(PushCommand.ListUsersSql(), sql);

        var users = run.Report["users"]!.AsArray();
        Assert.Equal(2, users.Count);
        Assert.Equal("u_gm", (string?)users[0]!["id"]);
        Assert.Equal("gm@example.net", (string?)users[0]!["email"]);
        Assert.Equal(2, (int)users[0]!["character_count"]!);
    }

    private static readonly string[] EveryFlag = ["--from", "--user", "--campaign", "--id", "--dry-run", "--list-users"];

    [Fact]
    public void HelpIsExitZeroAndNamesEveryFlag()
    {
        var run = Invoke(new NoDatabase(), "--help");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        foreach (var flag in EveryFlag)
            Assert.Contains(flag, run.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void AMintedIdHasTheServersShape()
    {
        var pattern = new Regex(PushCommand.CharacterIdPattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        for (var i = 0; i < 20; i++) Assert.Matches(pattern, PushCommand.NewId());
        Assert.NotEqual(PushCommand.NewId(), PushCommand.NewId());
    }

    // ── Wrangler's answer ─────────────────────────────────────────────────

    [Fact]
    public void WranglerOutputIsReadOneStatementAtATime()
    {
        const string answer = """
            [
              { "results": [ { "id": "u_1", "character_count": 30 } ], "success": true, "meta": { "changes": 0 } },
              { "results": [], "success": true, "meta": { "changes": 0 } }
            ]
            """;

        var parsed = WranglerDatabase.Parse(answer);

        Assert.Equal(2, parsed.Count);
        Assert.Equal("u_1", (string?)Assert.Single(parsed[0])["id"]);
        Assert.Equal(30, (int)parsed[0][0]["character_count"]!);
        Assert.Empty(parsed[1]);

        // A banner ahead of the array is skipped, and the array is still read whole.
        var withPreamble = WranglerDatabase.Parse("├ Checking something\n│ 🌀 Uploading complete.\n" + answer);
        Assert.Equal(2, withPreamble.Count);
        Assert.Equal("u_1", (string?)withPreamble[0][0]["id"]);

        Assert.Throws<DatabaseException>(() => WranglerDatabase.Parse("""[ { "results": [], "success": false } ]"""));
        Assert.Throws<DatabaseException>(() => WranglerDatabase.Parse("npm notice: not json"));
        Assert.Throws<DatabaseException>(() => WranglerDatabase.Parse("""{ "results": [] }"""));
    }

    // ── Contracts: the copies are held to their originals ─────────────────

    /// <summary>
    /// The upsert is <c>putCharacter</c> from <c>worker/db.js</c>, transcribed. Both column
    /// lists are read out of the two sources and compared, with a bound on each as the
    /// positive control — an extraction that stopped matching would compare two empty lists.
    /// </summary>
    [Fact]
    public void TheUpsertIsTheServersOwnStatement()
    {
        var dbJs = File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "worker", "db.js"));
        var body = Regex.Match(dbJs, @"export async function putCharacter\([\s\S]*?\.first\(\);",
            RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(body.Success, "putCharacter was not found in worker/db.js");

        // The statement is a concatenation of single-quoted fragments; join them into one text.
        var serverSql = string.Concat(Regex.Matches(body.Value, @"'([^']*)'", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups[1].Value));
        serverSql = Regex.Replace(serverSql, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(5));

        var serverColumns = Regex.Match(serverSql, @"INSERT INTO characters \(([^)]*)\)", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Groups[1].Value.Split(',').Select(c => c.Trim()).ToArray();
        Assert.True(serverColumns.Length >= 11, $"only {serverColumns.Length} columns read out of putCharacter");
        Assert.Equal(serverColumns, PushCommand.Columns);

        var villain = AVillain();
        var run = Invoke(new Scripted(), "--from", CharacterFile(villain), "--user", "u_gm", "--dry-run");
        var ourSql = (string)run.Report["sql"]!;

        static string[] Assigned(string sql) =>
            [.. Regex.Matches(sql, @"(\w+) = excluded\.(\w+)", RegexOptions.None, TimeSpan.FromSeconds(5))
                .Select(m => { Assert.Equal(m.Groups[1].Value, m.Groups[2].Value); return m.Groups[1].Value; })];

        var serverSet = Assigned(serverSql);
        Assert.True(serverSet.Length >= 9, $"only {serverSet.Length} assignments read out of putCharacter");
        Assert.Equal(serverSet, Assigned(ourSql));

        foreach (var clause in UpsertClauses)
        {
            Assert.Contains(clause, serverSql, StringComparison.Ordinal);
            Assert.Contains(clause, ourSql, StringComparison.Ordinal);
        }

        // **The one clause the copy deliberately leaves out**: the server refuses a save naming a
        // character its account handed to a campaign as a nemesis, so a browser tab cannot write
        // the Villain back. `push` never reads or writes `campaign_members` (see
        // mcp-and-headless.md), matches by label on the target account — where a handed-over row
        // no longer is — and so can only reach such an id when an operator names it with `--id`.
        // Asserted both ways, so the server's guard cannot be dropped and the copy cannot quietly
        // start reading approval state.
        const string HandedOver = "AND handed_over_at IS NOT NULL";
        Assert.Contains(HandedOver, serverSql, StringComparison.Ordinal);
        Assert.DoesNotContain("campaign_members", ourSql, StringComparison.Ordinal);
    }

    private static readonly string[] UpsertClauses =
    [
        "ON CONFLICT (user_id, id) DO UPDATE SET",
        "EXISTS (SELECT 1 FROM characters WHERE user_id = ",
        "OR (SELECT COUNT(*) FROM characters WHERE user_id = ",
        "< (SELECT character_limit FROM users WHERE id = ",
        "RETURNING id",
    ];

    /// <summary>The shapes and bounds are the ones <c>worker/characters.js</c> refuses with a 400.</summary>
    [Fact]
    public void TheIdShapesAndBoundsAreTheServersOwn()
    {
        var js = File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "worker", "characters.js"));

        static string JsConst(string source, string name)
        {
            var m = Regex.Match(source, name + @"\s*=\s*(/(?<re>[^/]+)/|'(?<str>[^']*)'|(?<num>[0-9_]+));",
                RegexOptions.None, TimeSpan.FromSeconds(5));
            Assert.True(m.Success, $"{name} was not found in worker/characters.js");
            return m.Groups["re"].Success ? m.Groups["re"].Value
                 : m.Groups["str"].Success ? m.Groups["str"].Value
                 : m.Groups["num"].Value.Replace("_", "", StringComparison.Ordinal);
        }

        Assert.Equal(PushCommand.CharacterIdPattern, JsConst(js, "ID_PATTERN"));
        Assert.Equal(PushCommand.CampaignIdPattern, JsConst(js, "CAMPAIGN_ID_PATTERN"));
        Assert.Equal(JsConst(js, "MAX_LABEL_LENGTH"), PushCommand.MaxLabelLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(JsConst(js, "MAX_INDEX_FIELD_LENGTH"), PushCommand.MaxIndexFieldLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(JsConst(js, "MAX_SPENT"), PushCommand.MaxSpent.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(PushCommand.DefaultLabel, JsConst(js, "DEFAULT_LABEL"));
    }

    /// <summary>
    /// The envelope is <c>StoredCharacter</c>'s in <c>web/</c>: its version, its record's field
    /// order, and the ordinals of <c>SheetMode</c>. A wrong version here is a character the
    /// browser silently discards, which is the worst kind of wrong.
    /// </summary>
    [Fact]
    public void TheEnvelopeIsTheBrowsersOwn()
    {
        var stored = File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "web", "Services", "StoredCharacter.cs"));
        var session = File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "web", "Services", "CharacterSession.cs"));
        var saved = File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "web", "Services", "SavedCharacters.cs"));

        var version = Regex.Match(stored, @"const int CurrentVersion = (\d+);", RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(version.Success, "CurrentVersion was not found in StoredCharacter.cs");
        Assert.Equal(PushCommand.EnvelopeVersion, int.Parse(version.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));

        var record = Regex.Match(stored, @"record Saved\(int (\w+), SheetMode (\w+), CharacterSheet\? (\w+)\)", RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(record.Success, "the Saved record was not found in StoredCharacter.cs, or its shape moved");
        var keys = JsonNode.Parse(PushCommand.Envelope(SampleCharacters.Hero()))!.AsObject().Select(p => p.Key).ToArray();
        Assert.Equal(new[] { record.Groups[1].Value, record.Groups[2].Value, record.Groups[3].Value }, keys);

        var modes = Regex.Match(session, @"enum SheetMode \{ (\w+), (\w+) \}", RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(modes.Success, "SheetMode was not found in CharacterSession.cs, or gained a member");
        Assert.Equal("Hero", modes.Groups[1 + PushCommand.HeroMode].Value);
        Assert.Equal("Villain", modes.Groups[1 + PushCommand.VillainMode].Value);

        Assert.Contains($"\"{PushCommand.DefaultLabel}\"", saved, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDatabaseNameIsTheOneInTheToml()
    {
        var toml = File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "d1", "wrangler.toml"));
        var name = Regex.Match(toml, @"database_name\s*=\s*""([^""]+)""", RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(name.Success);
        Assert.Equal(WranglerDatabase.DatabaseName, name.Groups[1].Value);
    }
}
