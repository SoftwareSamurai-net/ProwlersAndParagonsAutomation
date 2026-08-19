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

        Assert.True(theme.ScriptIsMissing);
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
