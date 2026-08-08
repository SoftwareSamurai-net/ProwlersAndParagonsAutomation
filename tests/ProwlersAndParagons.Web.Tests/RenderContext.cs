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

    public RenderContext()
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
        Services.AddScoped<CharacterSession>();

        // Resolves bUnit's own IJSRuntime, so a component that persists can be rendered and
        // the interop it asks for can be read back off JSInterop.Invocations.
        Services.AddScoped<CharacterStore>();

        // The mode switch and the sample loader both call into JS. Loose mode records the
        // calls and answers nothing, which is right here: what those calls do to the
        // document is the browser's business, not a component's.
        JSInterop.Mode = JSRuntimeMode.Loose;

        Session = Services.GetRequiredService<CharacterSession>();
        Store = Services.GetRequiredService<CharacterStore>();

        // Program.cs subscribes this, and it has to be here too or the tests cannot see the
        // half of persistence that matters: a save fires on every change, so anything that
        // clears storage is racing a write nobody awaits. Without the subscription a test
        // asserting on that ordering asserts on nothing.
        Session.Changed += () => _ = Store.SaveAsync(Session.Sheet, Session.Mode);
    }

    public CharacterStore Store { get; }

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
