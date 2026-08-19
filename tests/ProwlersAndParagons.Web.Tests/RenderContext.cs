using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// A bUnit context wired the way <c>web/Program.cs</c> wires the real app: the same
/// services, built from the <b>real</b> <c>data/rules/*.json</c>.
///
/// <para>Using the shipped rules rather than a stub is the same decision the engine suite
/// made, and for the same reason — a component that renders correctly against invented data
/// and wrongly against the rulebook has been tested for nothing. It also means these tests
/// exercise the exact path the browser takes, minus the HTTP fetch.</para>
/// </summary>
public sealed class RenderContext : BunitContext
{
    public CharacterSession Session { get; }

    /// <param name="recordingsProblem">
    /// Set to render the app as it is when the recordings could not be fetched — an empty
    /// library carrying the reason, which is what <c>web/Program.cs</c> registers when the
    /// fetch or the parse fails. There is no way to reach that state through the UI, and it is
    /// the state in which the replay pages have to say something true rather than guess.
    /// </param>
    public RenderContext(string? recordingsProblem = null)
    {
        var rules     = RulesRepository.FromBasePath(RepoRoot());
        var costs     = new CostCalculator(rules);
        var derived   = new DerivedStatsCalculator(rules);
        var validator = new CharacterValidator(rules, costs, derived);

        Services.AddSingleton(rules);
        Services.AddSingleton(costs);
        Services.AddSingleton(derived);
        Services.AddSingleton(validator);
        Services.AddSingleton(new ProConApplicability(rules));
        Services.AddSingleton(new SourceGrouping(rules));

        // The recorded conversations, read from the real data/transcripts for the same reason
        // the rules are read from the real data/rules: a replay that renders correctly against
        // an invented transcript and wrongly against the shipped ones has been tested for
        // nothing. This is the fetch in Program.cs, minus the HTTP.
        Services.AddSingleton(recordingsProblem is null
            ? new ReplayLibrary(TranscriptLibrary.ReadAll(
                TranscriptLibrary.FileNames.ToDictionary(
                    name => name,
                    name => File.ReadAllText(Path.Combine(RepoRoot(), "data", "transcripts", name)),
                    StringComparer.Ordinal)))
            : new ReplayLibrary([], recordingsProblem));
        Services.AddScoped<CharacterSession>();

        // Resolves bUnit's own IJSRuntime, so a component that persists can be rendered and
        // the interop it asks for can be read back off JSInterop.Invocations.
        Services.AddScoped<IIdentitySource, LocalIdentity>();
        Services.AddScoped<ICharacterStore, CharacterStore>();

        // Every call into motion.js, with its failures swallowed. Registered here so a render
        // test exercises the same guarded path the app does rather than a bare IJSRuntime.
        Services.AddScoped<Motion>();

        // The command palette: what it offers, and the guarded calls into palette.js. Both are
        // registered for every render rather than only for the palette's own tests, because
        // the Powers section asks for a requested Power on each pass and the step above it
        // reads the same service — a component that could not resolve them would throw out of
        // renders that have nothing to do with the palette.
        Services.AddScoped<Commands>();
        Services.AddScoped<Shortcuts>();
        Services.AddScoped<Theme>();

        // The mode switch and the sample loader both call into JS. Loose mode records the
        // calls and answers nothing, which is right here: what those calls do to the
        // document is the browser's business, not a component's.
        JSInterop.Mode = JSRuntimeMode.Loose;

        Session = Services.GetRequiredService<CharacterSession>();

        // Program.cs subscribes this, and it has to be here too or the tests cannot see the
        // half of persistence that matters: a save fires on every change, so anything that
        // clears storage is racing a write nobody awaits. Without the subscription a test
        // asserting on that ordering asserts on nothing.
        var store = Services.GetRequiredService<ICharacterStore>();
        Session.Changed += () => _ = store.SaveAsync(Session.Sheet, Session.Mode);
    }

    /// <summary>Loads a sample so a rendered sheet has something in every section.</summary>
    public RenderContext With(SheetMode mode)
    {
        Session.LoadSample(mode);
        return this;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (no .sln found above {AppContext.BaseDirectory}).");
    }
}
