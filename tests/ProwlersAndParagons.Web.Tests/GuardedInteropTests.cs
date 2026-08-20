using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The three services that call into this app's own scripts, and swallow it when the script is
/// not there.
///
/// <para><b>Every one of these calls is reached from a render</b>, so an unguarded one throws out
/// of <c>OnAfterRenderAsync</c> on every page: a missing decoration would take the app with it.
/// <see cref="Motion"/> established the bargain and <see cref="Shortcuts"/> and <see cref="Theme"/>
/// make it too.</para>
///
/// <para><b>These exist because Qodana noticed nothing read <c>ScriptIsMissing</c>.</b> Both of the
/// newer classes carried a doc comment saying it was "read by tests" and no test read either — so
/// the swallow was asserted by two comments and one older class, and a `catch` that had quietly
/// stopped catching would have looked identical. The flag is the observable half of the bargain;
/// if it is unread, the bargain is unchecked.</para>
/// </summary>
public sealed class GuardedInteropTests
{
    /// <summary>A runtime with none of this app's scripts loaded, as a 404 leaves the browser.</summary>
    private sealed class NoScripts : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new JSException($"Could not find '{identifier}'.");

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new JSException($"Could not find '{identifier}'.");
    }

    [Fact]
    public async Task MotionSwallowsAMissingScript()
    {
        var motion = new Motion(new NoScripts());

        await motion.Begin();
        await motion.End();
        await motion.Count(default, 1, 2);
        await motion.Land(default, announce: true);

        Assert.True(motion.ScriptIsMissing);
    }

    [Fact]
    public async Task ShortcutsSwallowsAMissingScript()
    {
        var keys = new Shortcuts(new NoScripts());

        await keys.Listen(new object());
        await keys.Enter(default);
        await keys.Leave();

        Assert.True(keys.ScriptIsMissing);
    }

    [Fact]
    public async Task ThemeSwallowsAMissingScript()
    {
        var theme = new Theme(new NoScripts());

        await theme.Apply(SheetMode.Villain);
        await theme.ReadChoice();
        await theme.Choose(ThemeChoice.Dark);

        Assert.True(theme.ScriptIsMissing);
    }

    /// <summary>
    /// <b>A click on the light/dark control still moves the control, even when the script that
    /// would apply it is not there.</b>
    ///
    /// <para>The swallow above says the app does not fall over. This says the button does not lie
    /// about what was pressed: <c>Choose</c> records the choice before it calls out, so a reader
    /// on a broken deployment sees the state they clicked rather than one that springs back under
    /// their finger. Recorded separately from <c>ScriptIsMissing</c> because the two are
    /// independent — a version that assigned after the call would satisfy the swallow and fail
    /// this.</para>
    /// </summary>
    [Fact]
    public async Task AChoiceIsRecordedEvenWhenItCannotBeApplied()
    {
        var theme = new Theme(new NoScripts());

        await theme.Choose(ThemeChoice.Dark);

        Assert.Equal(ThemeChoice.Dark, theme.Choice);
        Assert.True(theme.ScriptIsMissing);
    }

    /// <summary>
    /// <b>An unreadable stored value leaves the reader on their system's setting.</b>
    ///
    /// <para>A key edited by hand, or written by an older build, must not be a third palette or
    /// a guess. The only honest answer is the default — which is also the state of somebody who
    /// has never chosen, and which follows the system as it changes.</para>
    /// </summary>
    [Theory]
    [InlineData(null, ThemeChoice.System)]
    [InlineData("system", ThemeChoice.System)]
    [InlineData("", ThemeChoice.System)]
    [InlineData("DARK", ThemeChoice.System)]
    [InlineData("midnight", ThemeChoice.System)]
    [InlineData("light", ThemeChoice.Light)]
    [InlineData("dark", ThemeChoice.Dark)]
    public async Task AStoredChoiceIsReadOrFallsBackToTheSystem(string? stored, ThemeChoice expected)
    {
        await using var ctx = new RenderContext();
        ctx.JSInterop.Setup<string?>("ppTheme.current").SetResult(stored);

        var theme = new Theme(ctx.Services.GetRequiredService<IJSRuntime>());
        await theme.ReadChoice();

        Assert.Equal(expected, theme.Choice);
        Assert.False(theme.ScriptIsMissing);
    }

    /// <summary>
    /// The wire name is the word the stylesheet and the script both use, in both directions.
    ///
    /// <para><b>Three spellings of one state is how the default gets a palette nobody designed.</b>
    /// <c>data-theme</c> is <em>absent</em> for the system state — the dark blocks are written
    /// <c>:not([data-theme="light"])</c>, so an attribute reading "system" matches neither path.
    /// The script is what removes it; what this pins is that C# asks for the same three words
    /// the script branches on.</para>
    /// </summary>
    [Theory]
    [InlineData(ThemeChoice.System, "system")]
    [InlineData(ThemeChoice.Light, "light")]
    [InlineData(ThemeChoice.Dark, "dark")]
    public async Task TheChoiceGoesOutUnderTheNameTheScriptBranchesOn(ThemeChoice choice, string wire)
    {
        await using var ctx = new RenderContext();

        var theme = new Theme(ctx.Services.GetRequiredService<IJSRuntime>());
        await theme.Choose(choice);

        Assert.Contains(ctx.JSInterop.Invocations,
            i => i.Identifier == "ppTheme.set" && i.Arguments.Contains(wire));

        // And the script really does branch on all three of them, rather than on two and a
        // fall-through that happens to agree today.
        // Async with the test's own token, because the two analyzers want different things here:
        // Qodana asks for the async overload and xUnit1051 asks any call taking a token to take
        // this one. Both are satisfied; neither is suppressed. Qualified, because bUnit declares a
        // TestContext of its own and `using Bunit` is at the top of this file.
        var script = await File.ReadAllTextAsync(
            Path.Combine(RepoRoot(), "web", "wwwroot", "js", "theme.js"),
            Xunit.TestContext.Current.CancellationToken);

        Assert.Contains($"\"{wire}\"", script, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    /// <summary>
    /// The positive control, and without it the three tests above pass on a class that has
    /// stopped calling anything at all.
    ///
    /// <para>A <c>try</c> around a call that was deleted swallows nothing and reports nothing, and
    /// <c>ScriptIsMissing</c> would stay false — which is what these assert against. So: a working
    /// runtime leaves the flag alone <b>and</b> the call is recorded as having been made.</para>
    /// </summary>
    [Fact]
    public async Task AWorkingRuntimeIsNotReportedAsMissing()
    {
        await using var ctx = new RenderContext();
        var js = ctx.Services.GetRequiredService<IJSRuntime>();

        var theme = new Theme(js);
        await theme.Apply(SheetMode.Hero);

        Assert.False(theme.ScriptIsMissing);
        Assert.Contains(ctx.JSInterop.Invocations, i => i.Identifier == "ppSetMode");
    }
}
