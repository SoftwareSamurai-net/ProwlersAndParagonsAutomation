using System.Text.Json;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Cli.Export;
using ProwlersAndParagonsAutomation.Engine;

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

         Costs and validates a character against the rulebook and writes its two exports.
         Asks nothing and reads no terminal, so it can be driven by another program.

           --from <file>   The character to build, as JSON in the character-sheet shape.
                           Use - to read it from standard input.
           --out <dir>     Where to write the exports. Defaults to output/.
           --no-export     Cost and validate only; write no files.
           --help          This text.

         Exit codes:
           {Ok}  the character is legal — warnings may still be reported
           {CharacterIllegal}  the character breaks a rule; every issue is in the report
           {InputUnusable}  the input could not be read, or the arguments made no sense

         Standard output carries one JSON report for each of those three exits. Anything
         said about the run itself goes to standard error, so a caller can parse stdout
         whole without filtering it.
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

        if (ReadCharacter(options.From, stdin, out var sheet, out var readError) is false)
            return Report(stdout, InputUnusable, "INPUT_UNREADABLE", readError);

        return Judge(sheet, options, projectRoot, stdout, stderr);
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
    private int Judge(
        CharacterSheet sheet,
        Options options,
        string projectRoot,
        TextWriter stdout,
        TextWriter stderr)
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
            return Report(stdout, InputUnusable, "CHARACTER_UNUSABLE",
                "This character could not be checked against the rules at all. Some part of "
                + "it is not something a character can hold — a null where an id belongs, "
                + "most likely.");
        }

        var tier  = sheet.SelectedTierId is null ? null : _rules.GetTier(sheet.SelectedTierId);
        var spent = Answer(() => _costs.TotalCost(sheet));

        var exports = options.WriteExports
            ? WriteExports(sheet, validation, options.OutputDirectory, projectRoot, stderr)
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
            ["trait_cap"] = tier?.TraitCapRank,
            ["derived"]   = new JsonObject
            {
                ["edge"]    = Answer(() => _derived.CalculateEdge(sheet)),
                ["health"]  = Answer(() => _derived.CalculateHealth(sheet)),
                ["resolve"] = Answer(() => _derived.CalculateResolve(sheet))
            },
            ["issues"]  = Issues(validation),
            ["exports"] = exports
        };

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

        stdout.WriteLine(report.ToJsonString(Formatting));
        return validation.IsValid ? Ok : CharacterIllegal;
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
        TextWriter stderr)
    {
        try
        {
            var (txt, json) = new CharacterSheetExporter()
                .Export(sheet, _rules, _costs, _derived, validation, projectRoot, outputDirectory);

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

    private static bool ReadCharacter(
        string? from,
        TextReader stdin,
        out CharacterSheet sheet,
        out string error)
    {
        sheet = new CharacterSheet();
        error = "";

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
        catch (JsonException e)
        {
            // The exception's own message names the C# type it failed to build — "could not be
            // converted to ProwlersAndParagonsAutomation.Engine.SelectedPower" — which is this
            // program talking about itself to somebody holding a rulebook. The path and the
            // position are the useful half and are kept; the type name is not.
            var where = e.Path is null ? "" : $" at {e.Path}";
            var line  = e.LineNumber is null ? "" : $", line {e.LineNumber + 1}";

            error = $"The character file is not valid JSON in the character-sheet shape{where}{line}.";
            return false;
        }
    }

    // ── Arguments ─────────────────────────────────────────────────────────

    private sealed record Options(string? From, string? OutputDirectory, bool WriteExports, bool Help);

    /// <summary>
    /// Hand-rolled rather than a parser package, because the whole surface is four flags and
    /// this project takes no dependency it does not need. Unknown flags are refused rather
    /// than ignored: a caller that misspells <c>--out</c> should not silently get output/.
    /// </summary>
    private static bool ParseArguments(IReadOnlyList<string> args, out Options options, out string error)
    {
        string? from = null, outputDirectory = null;
        var writeExports = true;
        var help = false;
        error = "";

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

                case "--from" or "--out":
                {
                    if (i + 1 >= args.Count)
                    {
                        error = $"{args[i]} needs a value after it.";
                        options = new(null, null, true, false);
                        return false;
                    }

                    if (args[i] == "--from") from = args[i + 1];
                    else outputDirectory = args[i + 1];
                    i++;
                    break;
                }

                default:
                    error = $"'{args[i]}' is not an option this command has.";
                    options = new(null, null, true, false);
                    return false;
            }
        }

        options = new(from, outputDirectory, writeExports, help);

        if (help || from is not null) return true;

        error = "No character was given. Pass --from <file>, or --from - to read one from standard input.";
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
        var report = new JsonObject
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

        stdout.WriteLine(report.ToJsonString(Formatting));
        return exitCode;
    }
}
