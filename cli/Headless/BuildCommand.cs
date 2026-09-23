using System.Text.Json;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Cli.Export;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Cli.Headless;

/// <summary>
/// <c>build --from character.json</c>: costs and validates a character without asking
/// anybody anything, writes the two exports, and says what is wrong in a shape a caller can
/// act on.
///
/// <para><b>What it is for.</b> A model can propose a character; only the engine may decide
/// what a character costs and whether it is legal. This command is that seam, and the
/// ordering is the whole value of it — invert it and you have a random number generator with
/// good prose. Nothing here computes a Hero Point; it asks
/// <see cref="CostCalculator"/> and <see cref="CharacterValidator"/> and reports the
/// answer.</para>
///
/// <para><b>It reports, it never repairs.</b> An over-budget character comes back with every
/// issue and a non-zero exit code, and the caller decides what to give up. Clamping a rank
/// or dropping a Power would be the tool making a design decision about somebody's
/// character, and would hide from the player that their concept did not fit.</para>
///
/// <para>The input is the <see cref="CharacterSheet"/> shape — the inputs of a character.
/// <b>Not the JSON export</b>, which is a report: derived stats, costs and findings are all
/// answers, and reading one back would mean rebuilding a character from its own
/// conclusions.</para>
///
/// <para><b>A campaign is a roster, so <c>--from</c> repeats and <c>--from-dir</c> takes a
/// directory.</b> Statting twenty-eight NPCs meant twenty-eight process starts and a shell
/// loop written four separate times in one session. One character still reports exactly as it
/// always did — callers pin that shape — and more than one reports as one document holding
/// each character's own report plus the questions that are about all of them.</para>
/// </summary>
public sealed class BuildCommand
{
    /// <summary>The character is legal. Warnings may still have been reported.</summary>
    public const int Ok = 0;

    /// <summary>The character breaks a rule. Every issue is in the report.</summary>
    public const int CharacterIllegal = 1;

    /// <summary>The input could not be read, or the arguments made no sense.</summary>
    public const int InputUnusable = 2;

    /// <summary>The name of the subcommand, as the first argument.</summary>
    public const string Verb = "build";

    private readonly RulesRepository _rules;
    private readonly CostCalculator _costs;
    private readonly DerivedStatsCalculator _derived;
    private readonly CharacterValidator _validator;

    public BuildCommand(
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived,
        CharacterValidator validator)
    {
        _rules     = rules;
        _costs     = costs;
        _derived   = derived;
        _validator = validator;
    }

    public static string Usage =>
        $"""
         Usage: dotnet run -- {Verb} --from <file> [options]
                dotnet run -- {Verb} --from-dir <dir> [options]

         Costs and validates a character — or a whole roster — against the rulebook and
         writes the exports. Asks nothing and reads no terminal, so it can be driven by
         another program.

           --from <file>       The character to build, as JSON in the character-sheet shape.
                               Repeatable: pass it once per character to check a roster in
                               one process. Use - to read one from standard input, which may
                               be named at most once.
           --from-dir <dir>    Every *.json directly in <dir>, in name order. Not recursive.
                               Can be combined with --from.
           --out <dir>         Where to write the exports. Defaults to output/.
           --no-export         Cost and validate only; write no files.
           --overwrite         Name each export after its character alone, with no timestamp,
                               replacing any file of that name. Without it every run adds a
                               pair of files rather than replacing one, which is right for a
                               single export and wrong for a roster re-checked after an edit.
           --traits-above <n>  Add to the roster section every Ability, Talent and Power whose
                               effective rank is above n, and the Trait a Power's baseline is
                               derived from, so a reader can see whether the Power justifies
                               the rank.
           --trait-cap <n>     Build every character in this run to a house Trait Cap of n
                               rather than the tier's, overriding the field on the file. A
                               campaign may cap tighter than any tier does. It MOVES Resolve,
                               which is measured from the cap, and the report carries both the
                               cap in force and the tier's under tier_trait_cap.
           --help              This text.

         Run it as `dotnet run --no-build -- {Verb} ...` whenever anything else may be
         building this working tree — several `dotnet run` commands at once collide on the
         compiler, and --no-build skips the build and runs the last one instead. That is what
         makes several agents driving one checkout safe.

         Exit codes:
           {Ok}  the character is legal — warnings may still be reported
           {CharacterIllegal}  the character breaks a rule; every issue is in the report
           {InputUnusable}  the input could not be read, or the arguments made no sense

         Standard output carries one JSON report for each of those three exits. Anything
         said about the run itself goes to standard error, so a caller can parse stdout
         whole without filtering it.

         One character reports exactly as it always has. More than one is still exactly one
         JSON document: `characters` holds each character's own report, in the order they
         were given and each with its own exit_code and the `source` it was read from;
         `roster` answers the questions that are about all of them; `exit_code` is the
         highest of theirs ({InputUnusable} beats {CharacterIllegal} beats {Ok}); and `ok` is
         true only when every character is legal. A file that cannot be read is one exit-{InputUnusable}
         report inside `characters`, not the end of the run.
         """;

    /// <summary>
    /// Runs the command. The writers and the reader are parameters rather than
    /// <see cref="Console"/> so the tests can drive the whole surface — this is the part of
    /// the CLI that is testable at all, and closing that gap is half of why it exists.
    /// </summary>
    public int Run(
        IReadOnlyList<string> args,
        string projectRoot,
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
            return Report(stdout, InputUnusable, "BAD_ARGUMENTS", argumentError);
        }

        if (options.Help)
        {
            stdout.WriteLine(Usage);
            return Ok;
        }

        var inputs = options.From
            .Select(path => ReadCharacter(path, stdin, out var sheet, out var readError)
                ? new Input(path, HouseCapped(sheet, options), null)
                : new Input(path, null, readError))
            .ToList();

        var shared  = SharedExportNames(inputs, options);
        var reports = inputs
            .Select(input => input.Sheet is null
                ? (Report: ErrorReport(InputUnusable, "INPUT_UNREADABLE", input.Error!),
                   ExitCode: InputUnusable)
                : JudgeOne(input.Sheet, options, projectRoot, stderr,
                           shared.GetValueOrDefault(CharacterSheetRenderer.BaseFileName(input.Sheet))))
            .ToList();

        // <b>One character reports exactly as it always did.</b> Callers pin this document,
        // and a roster wrapper around a single character would break every one of them for
        // nothing — there is no cross-sheet question to ask about one sheet.
        if (reports.Count == 1)
        {
            stdout.WriteLine(reports[0].Report.ToJsonString(Formatting));
            return reports[0].ExitCode;
        }

        var exitCode = reports.Max(r => r.ExitCode);

        for (var i = 0; i < reports.Count; i++)
            reports[i].Report["source"] = inputs[i].Path;

        var roster = new JsonObject
        {
            // The exit code is the worst news in the run, so a caller looping over a roster
            // learns from one number that something needs attention — and each character
            // keeps its own, so they learn which.
            ["ok"]         = reports.TrueForAll(r => r.ExitCode == Ok),
            ["exit_code"]  = exitCode,
            ["characters"] = new JsonArray([.. reports.Select(r => (JsonNode)r.Report)]),
            ["roster"]     = Roster(inputs, options)
        };

        stdout.WriteLine(roster.ToJsonString(Formatting));
        return exitCode;
    }

    /// <summary>One input path, and either the character it held or why it did not.</summary>
    private sealed record Input(string Path, CharacterSheet? Sheet, string? Error);

    /// <summary>
    /// The verdict on one sheet, with no exports — what <see cref="PushCommand"/> asks before it
    /// will write anything. The same guarded judging as a <c>build</c> run, so a character that
    /// is refused at the door of the database is refused with exactly the report <c>build</c>
    /// would have given, and a caller can fix it against one document rather than two.
    /// </summary>
    internal (JsonObject Report, int ExitCode) Judge(CharacterSheet sheet, TextWriter stderr) =>
        JudgeOne(sheet, NoOptions with { WriteExports = false }, "", stderr, null);

    /// <summary>
    /// <c>--trait-cap</c> applied, which is <b>every character in the run</b> and <b>over the
    /// field on the file</b>.
    ///
    /// <para>Both halves are deliberate. A house cap is a fact about the table rather than about
    /// one character, so a roster checked against a campaign's rule is checked whole — the flag
    /// exists because the alternative is editing twenty-eight files. And a flag a file could
    /// silently win against would answer a question nobody asked: the caller who typed
    /// <c>--trait-cap 6</c> wants to know what these characters look like at 6d, including the
    /// one that thinks it is built to 8d.</para>
    ///
    /// <para>It is not written back. The exports carry the cap in force, the report says which it
    /// was, and the character's own file is left exactly as it was found — this command reports
    /// and never repairs, and rewriting somebody's sheet from a command-line flag is the largest
    /// repair it could make.</para>
    /// </summary>
    private static CharacterSheet? HouseCapped(CharacterSheet? sheet, Options options)
    {
        if (sheet is not null && options.TraitCap is { } cap) sheet.TraitCapRank = cap;

        return sheet;
    }

    /// <summary>
    /// The export base names more than one character in this run would write to, each mapped
    /// to the files those characters came from.
    ///
    /// <para>Only under <c>--overwrite</c>: the timestamped default already separates them,
    /// and the numbered fallback in <c>CharacterSheetExporter</c> separates the rest. With a
    /// stable name there is nothing between two characters called <em>Cael Hughes — Emergence</em>
    /// and <em>Cael Hughes — Realised</em>, whose safe names are identical, but the order they
    /// happened to be read in.</para>
    ///
    /// <para>Compared ignoring case, because the filesystem underneath may. That reports a
    /// collision on Linux that would not have happened there; a warning nobody needed is
    /// cheaper than a sheet silently written over.</para>
    /// </summary>
    private static Dictionary<string, IReadOnlyList<string>> SharedExportNames(
        IReadOnlyList<Input> inputs, Options options)
    {
        if (!options.Overwrite || !options.WriteExports)
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        return inputs
            .Where(i => i.Sheet is not null)
            .GroupBy(i => CharacterSheetRenderer.BaseFileName(i.Sheet!), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToDictionary(g => g.Key,
                          IReadOnlyList<string> (g) => [.. g.Select(i => i.Path)],
                          StringComparer.OrdinalIgnoreCase);
    }

    // ── Judging ───────────────────────────────────────────────────────────

    /// <summary>
    /// Asks the engine everything and writes down what it said.
    ///
    /// <para>Every call is guarded, because a character submitted by a caller can be one the
    /// engine cannot answer for at all — a variable-cost Power with no variant chosen has no
    /// cost, and asking for one throws rather than guessing. That is a finding, not a crash:
    /// the validator reports it, and the figures it makes unanswerable are left null.</para>
    /// </summary>
    private (JsonObject Report, int ExitCode) JudgeOne(
        CharacterSheet sheet,
        Options options,
        string projectRoot,
        TextWriter stderr,
        IReadOnlyList<string>? exportNameSharedWith)
    {
        // The validator is the one call whose failure leaves nothing to report — without it
        // there are no findings, so there is no report to put them in. It was the only engine
        // call here that was not guarded, and a comment two lines up used to claim otherwise;
        // six shapes of hand-written character took the whole run down through it, with an
        // exit code outside the three and nothing on standard output at all.
        ValidationResult validation;
        try
        {
            validation = _validator.Validate(sheet);
        }
        catch (Exception e) when (IsUnanswerable(e))
        {
            stderr.WriteLine(e.Message);

            // <b>It does not guess the cause any more.</b> It used to say "a null where an id
            // belongs, most likely" — and a reviewer duplicated an id in one of the rules files,
            // which throws the same kind of exception from a lookup, and got a character blamed
            // for a fault in this program's own data. A caller told that would edit its own file
            // for ever. The exception's own words go to standard error, where somebody
            // debugging will look and a repair loop will not.
            return (ErrorReport(InputUnusable, "CHARACTER_UNUSABLE",
                "This character could not be checked against the rules at all. The reason is on "
                + "standard error; it may be the character, and it may be a fault in this "
                + "program or in its copy of the rules."), InputUnusable);
        }

        var tier  = sheet.SelectedTierId is null ? null : _rules.GetTier(sheet.SelectedTierId);
        var spent = Answer(() => _costs.TotalCost(sheet));

        var exports = options.WriteExports
            ? WriteExports(sheet, validation, options.OutputDirectory, projectRoot,
                           options.Overwrite, stderr)
            : null;

        var report = new JsonObject
        {
            ["ok"]        = validation.IsValid,
            ["exit_code"] = validation.IsValid ? Ok : CharacterIllegal,
            ["character"] = new JsonObject
            {
                ["name"]    = string.IsNullOrWhiteSpace(sheet.Name) ? null : sheet.Name,
                ["tier"]    = sheet.SelectedTierId,
                ["package"] = sheet.SelectedPackageId
            },
            ["hero_points"] = new JsonObject
            {
                ["spent"]     = spent,
                ["budget"]    = tier?.HeroPoints,
                ["remaining"] = spent is null || tier is null ? null : tier.HeroPoints - spent
            },
            // <b>The cap in force, and the tier's beside it.</b> They are the same figure until
            // a table tightens one, and then a caller reading only the first cannot tell whether
            // a Resolve of 4 is a specialist or a house rule. Resolve is measured from
            // `trait_cap`, never from `tier_trait_cap`.
            ["trait_cap"]      = DerivedStatsCalculator.EffectiveTraitCap(sheet, tier),
            ["tier_trait_cap"] = tier?.TraitCapRank,
            // <b>The table's price for Immortality, where the character carries one.</b> Null is
            // the book's, which the entry itself prints — the same shape as the cap above, and
            // here for the same reason: a spend that includes 9 Hero Points for a Power the
            // rulebook prices at 3 is unreadable without the figure that made it so.
            //
            // <b>There is no flag for it and there must not be one.</b> `--trait-cap` overrides
            // every character in a run because a house cap is a fact about a table and a caller
            // asking "what do these look like at 6d" wants exactly that. A price is different:
            // it is Hero Points, so an override would silently re-cost every sheet in the run
            // against a game none of them is in. The sheet is the source.
            ["immortality_cost"] = sheet.ImmortalityCost,
            ["derived"]   = new JsonObject
            {
                ["edge"]    = Answer(() => _derived.CalculateEdge(sheet)),
                ["health"]  = Answer(() => _derived.CalculateHealth(sheet)),
                ["resolve"] = Answer(() => _derived.CalculateResolve(sheet))
            },
            ["issues"]  = Issues(validation),
            ["exports"] = exports
        };

        // <b>Two characters cannot quietly share one pair of files.</b> --overwrite is asked
        // for so a roster replaces its own sheets; two characters whose safe names are equal
        // would have one replace the other's, and the report would name paths holding
        // somebody else. Reported on every character that shares the name, so it cannot
        // matter which of them a caller happened to look at.
        if (exportNameSharedWith is { Count: > 1 })
        {
            report["issues"]!.AsArray().Add(new JsonObject
            {
                ["severity"] = "warning",
                ["code"]     = "EXPORT_NAME_COLLISION",
                ["message"]  = $"{exportNameSharedWith.Count} characters in this run export "
                             + $"under the name '{CharacterSheetRenderer.BaseFileName(sheet)}', "
                             + "and --overwrite gives them all the same pair of files. Only the "
                             + "last one written is on disk. Rename one, or drop --overwrite.",
                ["value"]    = CharacterSheetRenderer.BaseFileName(sheet),
                ["options"]  = new JsonArray([.. exportNameSharedWith.Select(p => JsonValue.Create(p))])
            });
        }

        // <b>A figure the engine could not supply, with nothing to fix, is a fault here.</b>
        // The skill tells a caller that a null figure means "fix the errors and it appears" — so
        // a null with no errors beside it is an instruction to repair a character that is
        // already legal, which is a loop with no way out. This says whose fault it is and exits
        // non-zero, rather than letting a caller spin.
        if (spent is null && validation.IsValid)
        {
            report["ok"]        = false;
            report["exit_code"] = CharacterIllegal;
            report["issues"]!.AsArray().Add(new JsonObject
            {
                ["severity"] = "error",
                ["code"]     = "ENGINE_COULD_NOT_ANSWER",
                ["message"]  = "This character broke no rule, and the Hero Point total still "
                             + "could not be worked out. That is a fault in this program rather "
                             + "than in the character; the reason is on standard error."
            });

            return (report, CharacterIllegal);
        }

        // A caller that asked for files and got none has to be able to see that in the report.
        // It was a line on stderr and an `exports: null` a caller had no reason to check —
        // exit 0 and no sheet, which reads as success.
        if (options.WriteExports && exports is null)
        {
            report["issues"]!.AsArray().Add(new JsonObject
            {
                ["severity"] = "warning",
                ["code"]     = "EXPORTS_NOT_WRITTEN",
                ["message"]  = "The character was checked, but its sheets could not be "
                             + "written. The reason is on standard error."
            });
        }

        return (report, validation.IsValid ? Ok : CharacterIllegal);
    }

    /// <summary>
    /// Writes the two exports, or null with a line on stderr if they cannot be produced.
    ///
    /// <para>Both are rendered even for an illegal character: seeing what was built is how a
    /// caller works out what to give up. A character the engine cannot price has no sheet to
    /// render at all, and that is the one case that comes back null.</para>
    /// </summary>
    private JsonObject? WriteExports(
        CharacterSheet sheet,
        ValidationResult validation,
        string? outputDirectory,
        string projectRoot,
        bool overwrite,
        TextWriter stderr)
    {
        try
        {
            var (txt, json) = new CharacterSheetExporter()
                .Export(sheet, _rules, _costs, _derived, validation, projectRoot, outputDirectory,
                        overwrite);

            // Absolute, always. With no --out the paths came back absolute and with a relative
            // --out they came back relative, so a caller that resolved them from anywhere but
            // the process's own working directory found nothing half the time.
            return new JsonObject
            {
                ["text"] = Path.GetFullPath(txt),
                ["json"] = Path.GetFullPath(json)
            };
        }
        catch (Exception e) when (IsUnanswerable(e)
                                  || e is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"The exports were not written: {e.Message}");
            return null;
        }
    }

    // ── The roster ────────────────────────────────────────────────────────

    /// <summary>
    /// The questions that are about the whole roster rather than about one sheet.
    ///
    /// <para><b>Nothing here adds up a Hero Point.</b> Every figure is one the engine already
    /// answers — <see cref="CostCalculator"/>'s per-category totals and per-Perk price,
    /// <see cref="DerivedStatsCalculator"/>'s effective rank — and what this method contributes
    /// is grouping and counting. A count of characters and a count of units are arithmetic
    /// about the roster; a sum of Hero Points would be arithmetic about the rules, and the
    /// engine is the only thing allowed to do that.</para>
    ///
    /// <para>Characters that could not be read are absent from every section: there is no
    /// sheet to ask about. Their reports are in <c>characters</c> with exit
    /// <see cref="InputUnusable"/>, which is where a caller finds out.</para>
    /// </summary>
    private JsonObject Roster(IReadOnlyList<Input> inputs, Options options)
    {
        var read = inputs.Where(i => i.Sheet is not null).ToList();

        var roster = new JsonObject
        {
            ["character_count"] = inputs.Count,
            ["read_count"]      = read.Count
        };

        // Absent rather than empty when nobody asked. An empty list reads as "no Trait is
        // above the rank", which is an answer to a question that was never put.
        if (options.TraitsAbove is { } rank)
            roster["traits_above"] = TraitsAbove(read, rank);

        roster["spending"]    = new JsonArray([.. read.Select(Spending)]);
        roster["perks_by_id"] = PerksById(read);

        return roster;
    }

    /// <summary>
    /// Every Ability, Talent and Power on each sheet whose effective rank is above
    /// <paramref name="rank"/>, with what a Power's baseline is derived from beside it.
    ///
    /// <para><b>The baseline is the half that answers the question.</b> "Which Traits exceed
    /// 6d, and does a Power justify it" is two questions: a 9d Power bought outright and a 9d
    /// Power sitting on a 9d Ability are the same number and different characters. So the
    /// purchased ranks, the baseline rank and the Trait ids it is read from all travel with
    /// the row.</para>
    ///
    /// <para><b>Nothing is filtered by opinion.</b> A Power that does not affect Resolve is still
    /// listed — it is excluded from the Resolve arithmetic, not from being a high rank on
    /// somebody's sheet — and the flag is reported so a reader can tell. That flag is
    /// <see cref="DerivedStatsCalculator.ResolveAffectedBySelection"/> and not the entry's
    /// <c>AffectsResolve</c>: Ch.5 p.83's Expertise carve-out means two characters can hold the
    /// same Power at the same rank and get different answers, so the row about a purchase has to
    /// carry the purchase's answer or it contradicts the Resolve figure beside it.</para>
    /// </summary>
    private JsonArray TraitsAbove(IReadOnlyList<Input> read, int rank)
    {
        var characters = new JsonArray();

        foreach (var input in read)
        {
            var sheet  = input.Sheet!;
            var traits = new JsonArray();

            foreach (var ability in _rules.Abilities.Where(a => sheet.GetAbilityRank(a.Id) > rank))
                traits.Add(new JsonObject
                {
                    ["kind"] = "ability",
                    ["id"]   = ability.Id,
                    ["rank"] = sheet.GetAbilityRank(ability.Id)
                });

            foreach (var talent in _rules.Talents.Where(t => sheet.GetTalentRank(t.Id) > rank))
                traits.Add(new JsonObject
                {
                    ["kind"] = "talent",
                    ["id"]   = talent.Id,
                    ["rank"] = sheet.GetTalentRank(talent.Id)
                });

            foreach (var selection in sheet.SelectedPowers)
            {
                // Guarded like every other engine call here: an id the caller invented throws
                // rather than guessing, and it is already reported as UNKNOWN_POWER.
                var effective = Answer(() => _derived.GetEffectiveRank(selection, sheet));
                if (effective is null || effective <= rank) continue;

                var power = _rules.GetPower(selection.PowerId);
                var row = new JsonObject
                {
                    ["kind"]            = "power",
                    ["id"]              = selection.PowerId,
                    ["rank"]            = effective,
                    ["purchased_ranks"] = selection.PurchasedRanks,
                    ["baseline_rank"]   = power is null
                        ? null
                        : Answer(() => _derived.GetBaselineRank(power, sheet, selection))
                };

                if (power?.Prerequisite is { } prerequisite)
                {
                    row["baseline_relationship"] = prerequisite.Relationship;
                    row["baseline_traits"] = new JsonArray(
                        [.. DerivedStatsCalculator.BaselineTraitIds(power, selection)
                            .Select(id => JsonValue.Create(id))]);
                }

                // Asked of the selection, not of the entry: an Expertise nominated to a combat
                // skill counts where one nominated to Science does not (Ch.5 p.83), and this row
                // is about a purchase on somebody's sheet rather than about the Power in general.
                if (power is not null)
                    row["affects_resolve"] = _derived.ResolveAffectedBySelection(selection);

                traits.Add(row);
            }

            characters.Add(new JsonObject
            {
                ["source"] = input.Path,
                ["name"]   = string.IsNullOrWhiteSpace(sheet.Name) ? null : sheet.Name,
                ["traits"] = traits
            });
        }

        return characters;
    }

    /// <summary>
    /// What each sheet spent, by the engine's own categories, and every Perk on it with its
    /// Units and its price.
    ///
    /// <para>The Perks are itemised because "this sheet is padded with Contacts because
    /// Contacts is the cheapest dial" is invisible in a category total — five sheets were
    /// padded with invented contact categories and the totals looked ordinary.</para>
    /// </summary>
    private JsonObject Spending(Input input)
    {
        var sheet = input.Sheet!;

        return new JsonObject
        {
            ["source"] = input.Path,
            ["name"]   = string.IsNullOrWhiteSpace(sheet.Name) ? null : sheet.Name,
            ["totals"] = new JsonObject
            {
                ["package"]   = Answer(() => _costs.PackageCost(sheet)),
                ["abilities"] = Answer(() => _costs.AbilityCost(sheet)),
                ["talents"]   = Answer(() => _costs.TalentCost(sheet)),
                ["powers"]    = Answer(() => _costs.TotalPowersCost(sheet)),
                ["perks"]     = Answer(() => _costs.TotalPerksCost(sheet)),
                ["gear"]      = Answer(() => _costs.TotalGearCost(sheet)),

                // The Perks on every vehicle, headquarters and shared campaign object — Hero
                // Points, and the only figure about them that is. Vehicle Points and Base Points
                // are a second currency and a Gadget's pool runs the other way, so neither belongs
                // in a block whose parts have to add up to the line below.
                ["assets"]    = Answer(() => _costs.TotalAssetPerkCost(sheet)),

                ["total"]     = Answer(() => _costs.TotalCost(sheet))
            },
            ["perks"] = new JsonArray([.. sheet.Perks.Select(perk => (JsonNode)new JsonObject
            {
                ["id"]    = perk.PerkId,
                ["units"] = perk.Units,
                ["cost"]  = Answer(() => _costs.PerkCost(perk))
            })])
        };
    }

    /// <summary>
    /// Each Perk id anybody on the roster holds, how many of them hold it, and the Units
    /// across all of them.
    ///
    /// <para>Two counts and no price. A Hero Point figure across the roster would be this
    /// program doing the engine's arithmetic; the per-character prices are in
    /// <see cref="Spending"/>, each one asked of <see cref="CostCalculator"/>.</para>
    /// </summary>
    private static JsonArray PerksById(IReadOnlyList<Input> read) =>
        new([.. read
            .SelectMany(i => i.Sheet!.Perks)
            .GroupBy(p => p.PerkId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (JsonNode)new JsonObject
            {
                ["id"] = g.Key,
                ["characters"] = read.Count(i =>
                    i.Sheet!.Perks.Any(p => string.Equals(p.PerkId, g.Key, StringComparison.Ordinal))),
                // Guarded like a cost: Sum is checked, and a caller can write a Units big
                // enough to overflow it — which is already a finding on that character
                // rather than a reason the roster has no answer at all.
                ["units"] = Answer(() => g.Sum(p => p.Units))
            })]);

    private static JsonArray Issues(ValidationResult validation)
    {
        var array = new JsonArray();

        foreach (var issue in validation.Issues)
        {
            var node = new JsonObject
            {
                ["severity"] = issue.Severity == ValidationSeverity.Error ? "error" : "warning",
                ["code"]     = issue.Code,
                ["message"]  = issue.Message
            };

            // Absent rather than null: an issue that has nothing to locate is not an issue
            // with an empty subject, and a reader should not have to tell those apart.
            if (issue.SubjectKind != ValidationSubject.None)
                node["subject_kind"] = SubjectKindName(issue.SubjectKind);
            if (issue.SubjectId is not null) node["subject_id"] = issue.SubjectId;
            if (issue.OwnerId is not null) node["owner_id"] = issue.OwnerId;
            if (issue.Value is not null) node["value"] = issue.Value;
            if (issue.Limit is not null) node["limit"] = issue.Limit;
            if (issue.Options.Count > 0) node["options"] = new JsonArray([.. issue.Options.Select(o => JsonValue.Create(o))]);

            array.Add(node);
        }

        return array;
    }

    // ── Input ─────────────────────────────────────────────────────────────

    /// <summary>
    /// One character out of a file or standard input, strictly. Internal because
    /// <see cref="PushCommand"/> reads its input through this same door — the device-name
    /// refusal, the strict reader and the wording of every refusal are one copy.
    /// </summary>
    internal static bool ReadCharacter(
        string? from,
        TextReader stdin,
        out CharacterSheet sheet,
        out string error)
    {
        sheet = new CharacterSheet();
        error = "";

        // <b>A Windows device name is not a file, and reading one never returns.</b>
        // File.ReadAllText("CON") opens the console and blocks on a read with no end — no
        // output, no exit code, forever, which is worse than any crash. COM1 and CONIN$ do the
        // same. Refused by name before anything opens it.
        if (from != "-" && IsADeviceName(from))
        {
            error = $"'{from}' is the name of a device rather than a file, and reading it would "
                  + "never finish.";
            return false;
        }

        string text;
        try
        {
            text = from == "-" ? stdin.ReadToEnd() : File.ReadAllText(from!);
        }
        // ArgumentException covers `--from ""`, which File.ReadAllText refuses before it ever
        // touches a disk. It threw where every other unreadable input reported.
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
                                    or NotSupportedException or ArgumentException)
        {
            error = $"The character file could not be read: {e.Message}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            error = from == "-"
                ? "No character arrived on standard input."
                : $"The character file '{from}' is empty.";
            return false;
        }

        try
        {
            // A JSON null — the literal text "null" — parses to a null sheet rather than
            // throwing, and would otherwise arrive as a legal empty character.
            // Strict, because this file was written by whoever is calling. A field name they
            // misspelled would otherwise be ignored, and the character would arrive missing
            // whatever it held — cheaper, legal, and wrong in a way nothing would report.
            if (CharacterSheetJson.Read(text, strict: true) is not { } read)
            {
                error = "The character file holds no character.";
                return false;
            }

            sheet = read;
            return true;
        }
        // InvalidOperationException as well as JsonException, because the deserializer throws
        // the former — not the latter — when asked to put a null into a get-only collection.
        // `"AbilityRanks": null` is well-formed JSON that any hand-written character might
        // carry, and all ten of the top-level collections did it: no report, empty standard
        // output, and the CLR's own unhandled-exception exit code.
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            // The exception's own message names the C# type it failed to build — "could not be
            // converted to ProwlersAndParagonsAutomation.Engine.SelectedPower" — which is this
            // program talking about itself to somebody holding a rulebook. The path and the
            // position are the useful half and are kept; the type name is not.
            // Only a JsonException knows where it was; the other kind does not, and says so by
            // saying nothing rather than by guessing a position.
            var where = (e as JsonException)?.Path is { } path ? $" at {path}" : "";
            var line  = (e as JsonException)?.LineNumber is { } n ? $", line {n + 1}" : "";

            error = $"The character file is not valid JSON in the character-sheet shape{where}{line}.";
            return false;
        }
    }

    /// <summary>
    /// The DOS device names Windows still reserves, which are legal-looking paths in every
    /// directory at once. <c>NUL</c> and <c>PRN</c> happen to fail politely; <c>CON</c>,
    /// <c>COM1</c> and <c>CONIN$</c> hang, so the whole set is refused rather than the three
    /// that were caught misbehaving.
    ///
    /// <para>Matched on the file name without its extension, because <c>CON.json</c> is the
    /// console too. Ordinal-ignore-case, not the current culture: this is a rule about bytes
    /// Windows reserves, not about words.</para>
    /// </summary>
    private static bool IsADeviceName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string name;
        try { name = Path.GetFileNameWithoutExtension(path); }
        catch (ArgumentException) { return false; }   // reported by the read instead

        return DeviceNames.Contains(name);
    }

    private static readonly HashSet<string> DeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    // ── Arguments ─────────────────────────────────────────────────────────

    private sealed record Options(
        IReadOnlyList<string> From,
        string? OutputDirectory,
        bool WriteExports,
        bool Overwrite,
        int? TraitsAbove,
        int? TraitCap,
        bool Help);

    private static readonly Options NoOptions = new([], null, true, false, null, null, false);

    /// <summary>
    /// Hand-rolled rather than a parser package, because the whole surface is seven flags and
    /// this project takes no dependency it does not need. Unknown flags are refused rather
    /// than ignored: a caller that misspells <c>--out</c> should not silently get output/.
    ///
    /// <para><c>--from</c> accumulates and <c>--from-dir</c> expands, so what leaves here is
    /// one ordered list of inputs and the rest of the command never learns which flag named
    /// which file.</para>
    /// </summary>
    private static bool ParseArguments(IReadOnlyList<string> args, out Options options, out string error)
    {
        var from = new List<string>();
        string? outputDirectory = null;
        var writeExports = true;
        var overwrite = false;
        int? traitsAbove = null;
        int? traitCap = null;
        var help = false;
        error = "";
        options = NoOptions;

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    help = true;
                    break;

                case "--no-export":
                    writeExports = false;
                    break;

                case "--overwrite":
                    overwrite = true;
                    break;

                case "--from" or "--out" or "--from-dir" or "--traits-above" or "--trait-cap":
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
                        case "--from":
                            from.Add(value);
                            break;

                        case "--out":
                            outputDirectory = value;
                            break;

                        case "--from-dir":
                            if (CharactersIn(value, out var found, out error) is false) return false;
                            from.AddRange(found);
                            break;

                        // <b>A rank that is not a number is an argument fault; a number that makes
                        // no sense is not.</b> A cap of 0d, or one above the tier's, is reported by
                        // the validator as a finding on the character — the same finding it gets
                        // when the file carries it — so the flag and the field cannot disagree
                        // about what a nonsensical cap means.
                        default:
                            if (!int.TryParse(value, System.Globalization.NumberStyles.AllowLeadingSign,
                                              System.Globalization.CultureInfo.InvariantCulture, out var rank))
                            {
                                error = $"{args[i - 1]} needs a rank, and '{value}' is not a whole number.";
                                return false;
                            }

                            if (args[i - 1] == "--trait-cap") traitCap = rank; else traitsAbove = rank;
                            break;
                    }

                    break;
                }

                default:
                    error = $"'{args[i]}' is not an option this command has.";
                    return false;
            }
        }

        options = new(from, outputDirectory, writeExports, overwrite, traitsAbove, traitCap, help);

        if (help) return true;

        // <b>Standard input can only be read once.</b> The second --from - reads a stream the
        // first one drained, so the second character would be reported as an empty file — an
        // answer about a character nobody submitted. Refused rather than half-answered.
        if (from.Count(f => f == "-") > 1)
        {
            error = "--from - names standard input, which can only be read once. "
                  + "Pass the other characters as files.";
            return false;
        }

        if (from.Count > 0) return true;

        error = "No character was given. Pass --from <file>, --from-dir <dir> for every "
              + "character in a directory, or --from - to read one from standard input.";
        return false;
    }

    /// <summary>
    /// Every <c>*.json</c> directly in a directory, in name order.
    ///
    /// <para>Not recursive, and that is a decision rather than an omission: a roster is a
    /// directory of sheets, and walking into subdirectories would sweep up an <c>output/</c>
    /// of previous exports — which are reports, and are exactly what this command refuses to
    /// read back as characters.</para>
    ///
    /// <para>Ordered by name so two runs over one directory report in the same order and a
    /// diff of two roster reports is readable. Filtered on the extension rather than trusting
    /// the search pattern, which on Windows also matches longer extensions.</para>
    /// </summary>
    private static bool CharactersIn(string directory, out IReadOnlyList<string> found, out string error)
    {
        found = [];
        error = "";

        if (!Directory.Exists(directory))
        {
            error = $"--from-dir '{directory}' is not a directory this program can see.";
            return false;
        }

        try
        {
            found = [.. Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                .Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            error = $"--from-dir '{directory}' could not be listed: {e.Message}";
            return false;
        }

        if (found.Count > 0) return true;

        error = $"--from-dir '{directory}' holds no .json files.";
        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions Formatting = new() { WriteIndented = true };

    /// <summary>
    /// A figure the engine can answer, or null where it cannot. The exceptions listed are
    /// what a half-finished or hand-written character produces —
    /// <see cref="InvalidOperationException"/> for a selection with no cost yet, and the rest
    /// for ids and numbers a caller invented. All of them are already reported as issues by
    /// the validator, so swallowing them here loses nothing and keeps one bad figure from
    /// costing the caller the whole report.
    /// </summary>
    private static int? Answer(Func<int> figure)
    {
        try { return figure(); }
        catch (Exception e) when (IsUnanswerable(e)) { return null; }
    }

    private static bool IsUnanswerable(Exception e) =>
        e is InvalidOperationException or KeyNotFoundException or ArgumentException
          or NullReferenceException or FormatException or OverflowException;

    /// <summary>
    /// A subject kind as it goes over the wire: <c>GearFeature</c> to <c>gear_feature</c>, so
    /// the report reads like the rest of this project's JSON rather than like its C#.
    ///
    /// <para>Public because the skill documents these names, and the test that checks the
    /// document has to ask this method rather than convert the enum itself. It did convert it
    /// itself, and so agreed with a broken copy: dropping the underscore shipped
    /// <c>gearfeature</c> on the wire with the document still saying <c>gear_feature</c>, and
    /// both tests stayed green.</para>
    /// </summary>
    public static string SubjectKindName(ValidationSubject kind) =>
        string.Concat(kind.ToString().Select((c, i) =>
            char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    /// <summary>
    /// A report for an argument the program refused, for <see cref="CommandLine"/> to use on
    /// the arguments it handles itself. Standard output holds one JSON document whether the
    /// refusal happened here or a layer up.
    /// </summary>
    public static int ReportArgumentError(TextWriter stdout, string message) =>
        Report(stdout, InputUnusable, "BAD_ARGUMENTS", message);

    /// <summary>A report for a run that never reached the character.</summary>
    private static int Report(TextWriter stdout, int exitCode, string code, string message)
    {
        stdout.WriteLine(ErrorReport(exitCode, code, message).ToJsonString(Formatting));
        return exitCode;
    }

    /// <summary>
    /// The same document, unwritten. A roster needs one of these per character that could
    /// not be read — <b>an unreadable file is one character's exit-2 report, never the end
    /// of the run</b>, because a caller checking twenty-eight sheets should not lose the
    /// other twenty-seven answers to a typo in one file name.
    /// </summary>
    private static JsonObject ErrorReport(int exitCode, string code, string message) =>
        new()
        {
            ["ok"]        = false,
            ["exit_code"] = exitCode,
            ["issues"]    = new JsonArray(new JsonObject
            {
                ["severity"] = "error",
                ["code"]     = code,
                ["message"]  = message
            })
        };
}
