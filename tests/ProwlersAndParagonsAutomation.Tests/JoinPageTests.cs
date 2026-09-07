using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <c>web/wwwroot/join.html</c> — the handout a GM sends a player who has no account yet.
///
/// <para><b>It is a static file rather than a Blazor route</b>, which is what these tests are
/// about: nothing compiles it, no component owns it, and every discipline the front end holds
/// itself to reaches it only if something goes looking. The colour and typeface scans in
/// <see cref="WebPresentationTests"/> now enumerate every stylesheet under <c>wwwroot</c> and
/// name this page directly; what is left is the wiring, and the wiring is where a static page
/// fails silently — a stylesheet that 404s leaves a readable page in the browser's own default
/// styles, which looks like a design decision rather than a fault.</para>
/// </summary>
public sealed class JoinPageTests
{
    private static string WwwRoot => Path.Combine(RulesFixture.RepoRoot, "web", "wwwroot");

    private static string JoinHtml => File.ReadAllText(Path.Combine(WwwRoot, "join.html"));
    private static string JoinCss => File.ReadAllText(Path.Combine(WwwRoot, "css", "join.css"));
    private static string ThemeCss => File.ReadAllText(Path.Combine(WwwRoot, "css", "theme.css"));

    /// <summary>
    /// A scanning regex. See <see cref="ScanRegex"/> for why these are linear-time rather than
    /// backtracking under a five-second cap.
    /// </summary>
    private static Regex Rx(string pattern, RegexOptions options = RegexOptions.None) =>
        ScanRegex.Build(pattern, options);

    private static string WithoutComments(string css) =>
        Rx(@"/\*.*?\*/", RegexOptions.Singleline).Replace(css, " ");

    /// <summary>
    /// The page asks for the palette and its own rules, and for nothing off this origin.
    ///
    /// <para><b>The second half is the Content-Security-Policy's, restated where it can be
    /// read.</b> <c>default-src 'self'</c> blocks an off-origin stylesheet or font at runtime,
    /// so a hosted webfont added here would not degrade — it would simply not arrive, on the
    /// deployed site only, with every test green locally. The self-hosted faces in
    /// <c>wwwroot/fonts</c> are the reason nothing needs to be fetched.</para>
    /// </summary>
    [Fact]
    public void TheHandoutAsksForThePaletteAndNothingOffThisOrigin()
    {
        var html = JoinHtml;

        Assert.Contains("href=\"/css/theme.css\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/css/join.css\"", html, StringComparison.Ordinal);

        var offOrigin = Rx(@"(?:href|src)\s*=\s*""(?:https?:)?//", RegexOptions.IgnoreCase);

        Assert.False(offOrigin.IsMatch(html),
            "join.html fetches something off this origin. default-src 'self' blocks it on the "
            + "deployed site, so it would fail there and nowhere else.");
    }

    /// <summary>
    /// The document element carries a palette mode.
    ///
    /// <para><b>Without it the page matches no palette block at all.</b> Every set in theme.css
    /// past the bare light one is written against <c>[data-mode="hero"]</c> or
    /// <c>[data-mode="villain"]</c> — including both dark ones — so a page that stamps nothing
    /// gets hero-light in a browser set to dark, which is a bright white page for somebody who
    /// asked for the opposite. index.html stamps it for the same reason and says so.</para>
    /// </summary>
    [Fact]
    public void TheHandoutStampsAPaletteModeSoTheDarkSetsApply()
    {
        var html = Rx("<!--.*?-->", RegexOptions.Singleline).Replace(JoinHtml, " ");
        var root = Rx(@"<html\b[^>]*>").Match(html);

        Assert.True(root.Success, "join.html has no <html> element.");
        Assert.Matches(@"data-mode\s*=\s*""(hero|villain)""", root.Value);
    }

    /// <summary>
    /// Every token the handout's stylesheet asks for is one theme.css actually defines.
    ///
    /// <para><b>A misspelt custom property is the quietest failure in CSS.</b>
    /// <c>var(--panel-sunken)</c> is not an error, does not warn, and leaves the declaration
    /// with no value at all — so a card loses its ground and the page still renders, which is
    /// indistinguishable from a design that wanted no ground. It is also how this page would
    /// rot without being touched: theme.css renames a token, every component is updated because
    /// the compiler-adjacent scans in <see cref="WebPresentationTests"/> read app.css, and a
    /// static file nobody is looking at keeps asking for the old name.</para>
    /// </summary>
    [Fact]
    public void EveryTokenTheHandoutAsksForIsDefinedInThemeCss()
    {
        var asked = Rx(@"var\(\s*(--[a-z0-9-]+)")
            .Matches(WithoutComments(JoinCss))
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        // A stylesheet that asks for nothing would pass the loop below without running it, and
        // this page is written entirely in tokens — so the count is a floor, not a formality.
        Assert.True(asked.Count > 20,
            $"join.css asks for only {asked.Count} tokens, so it is naming values of its own.");

        var theme = WithoutComments(ThemeCss);

        foreach (var token in asked)
        {
            Assert.True(Rx($@"(?<![\w-]){Regex.Escape(token)}\s*:").IsMatch(theme),
                $"join.css asks for {token}, which theme.css does not define. A custom property "
                + "that resolves to nothing leaves the declaration empty and the page still "
                + "draws, so nothing else will report this.");
        }
    }
}
