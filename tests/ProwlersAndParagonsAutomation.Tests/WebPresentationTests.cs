using System.Globalization;
using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The browser front end's presentation rules, asserted against its own source.
///
/// <para>These are not unit tests of behaviour. They are the disciplines the front end is
/// built on, each of which has already been broken once and none of which any compiler can
/// see: no component names a colour; nothing on screen names an internal type or a build
/// command, while the rulebook references stay; each repeated class has one owner; and the
/// printed sheet has a page, a light palette and break control.</para>
///
/// <para><b>The first version of this file was mostly theatre, and knowing how is the point
/// of the comments below.</b> An adversarial pass applied thirteen violations at once —
/// including white ink on white paper, every page-break rule flipped to `auto`, the whole
/// working UI un-hidden, a second `@media print` block undoing the first, and the "Armor8d"
/// bug reinstated — and all of it passed. The rewrite that followed is why the helpers here
/// look paranoid: substring checks against a whole file are almost always satisfied by
/// something other than the thing being tested.</para>
///
/// <para>One gap is structural and stated rather than papered over: everything here reads
/// source. Nothing renders a component, so no test in this file can catch a bug in rendered
/// output — "Armor8d" itself would walk straight past. Closing that needs bUnit; see
/// PROGRESS.md.</para>
/// </summary>
public sealed class WebPresentationTests
{
    private static string WebRoot => Path.Combine(RulesFixture.RepoRoot, "web");

    private static IReadOnlyList<string> RazorFiles => SourceFiles(WebRoot, "*.razor");

    private static string AppCss => File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "css", "app.css"));
    private static string ThemeCss => File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "css", "theme.css"));

    /// <summary>
    /// The page the app boots into.
    ///
    /// <para><b>It is the other file in the payload that can carry CSS, and the colour and
    /// typeface rules did not read it.</b> A <c>&lt;style&gt;</c> block dropped in here setting
    /// everything in Comic Sans applied — the CSP carries <c>style-src 'unsafe-inline'</c> for
    /// the budget bar's live width, so an inline block is not blocked at runtime either — and
    /// every presentation test stayed green. It also holds the boot screen, whose markup no
    /// component owns.</para>
    /// </summary>
    private static string IndexHtml => File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "index.html"));

    /// <summary>
    /// The app's own scripts — the third route into the payload, and the one that bypasses CSS
    /// entirely.
    ///
    /// <para><b>A CSSOM write is not subject to the Content-Security-Policy and no stylesheet
    /// scan can see it.</b> Three lines added to the existing <c>ppSetMode</c> —
    /// <c>documentElement.style.setProperty("--font-body", …)</c> and the same for
    /// <c>--font-display</c> and <c>--heading</c> — override both typefaces and a text colour
    /// at runtime with every presentation test green. That file already touches
    /// <c>documentElement</c> to set the palette mode, so it is the plausible place for it to
    /// happen rather than a contrived one.</para>
    /// </summary>
    private static IEnumerable<(string Name, string Text)> Scripts =>
        SourceFiles(Path.Combine(WebRoot, "wwwroot"), "*.js")
            .Select(f => (Path.GetFileName(f), File.ReadAllText(f)));

    private static List<string> SourceFiles(string root, string pattern) =>
        Directory.GetFiles(root, pattern, SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static Regex Rx(string pattern, RegexOptions options = RegexOptions.None) =>
        new(pattern, options, TimeSpan.FromSeconds(5));

    /// <summary>The projects whose type names must never reach the page.</summary>
    private static readonly string[] ProjectsWithTypes = ["engine", "sheets"];

    /// <summary>
    /// Every way to make an element invisible while leaving its class and its text in place.
    ///
    /// <para><b>Shared, because the second copy of it was weaker than the first.</b> Banning
    /// <c>display: none</c> alone leaves <c>visibility: hidden</c>, <c>color: transparent</c> and
    /// <c>font-size: 0</c> — several ways to one result, all green. And they are matched as whole
    /// declarations: <c>font-size:0</c> is a prefix of a rule's own <c>font-size:0.72rem</c> and
    /// <c>opacity:0</c> of <c>opacity:0.8</c>, so a naive spelling fires on the thing it is
    /// protecting, and the obvious fix for that is to weaken the ban.</para>
    ///
    /// <para>A guard added later for the empty state wrote its own four-entry version without the
    /// prefix guards. Two lists is one list going stale, so there is one.</para>
    /// </summary>
    /// <remarks>
    /// <c>color</c> is anchored with <c>(?&lt;![\w-])</c> because it is a suffix of
    /// <c>border-left-color</c>, <c>border-bottom-color</c> and the rest — so an entirely legitimate
    /// transparent border on a guarded class was reported as hiding the element. A ban that fires
    /// on something innocent is worse than no ban, because the natural fix is to weaken it.
    /// </remarks>
    private static readonly string[] EverySpellingOfHidden =
    [
        @"display:none", @"visibility:hidden", @"visibility:collapse",
        @"(?<![\w-])color:transparent", @"font-size:0(?![.\d])", @"opacity:0(?![.\d])",
        @"content-visibility:hidden"
    ];

    // ── No component names a colour ─────────────────────────────────────────────

    /// <summary>
    /// theme.css is the one file allowed to name a colour. Everything else asks it for one
    /// by token, which is what keeps the Hero/Villain switch a single attribute on the
    /// document element and lets print restate the whole palette without touching a
    /// component.
    ///
    /// <para>All three spellings are checked in <b>both</b> app.css and the components. An
    /// earlier version checked hex everywhere but channel functions only in app.css, and a
    /// <c>&lt;style&gt;</c> block dropped into a component walked through carrying
    /// <c>rgb()</c> and <c>hsl()</c>.</para>
    /// </summary>
    [Theory]
    [InlineData("app.css")]
    [InlineData("razor")]
    [InlineData("index.html")]
    public void NoComponentNamesAColour(string what)
    {
        var hex = Rx(@"#[0-9A-Fa-f]{3,8}\b");
        var keyword = Rx(@":\s*(red|blue|green|white|black|grey|gray|yellow|orange|purple)\b", RegexOptions.IgnoreCase);
        var channels = Rx(@"\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch)\s*\(", RegexOptions.IgnoreCase);

        List<(string, string)> sources = what switch
        {
            "app.css" => [("app.css", Scannable(AppCss, css: true))],
            "index.html" => [("index.html", Scannable(Scannable(IndexHtml, css: true), css: false))],
            _ => RazorFiles.Select(f => (Path.GetFileName(f), Scannable(File.ReadAllText(f), css: false))).ToList()
        };

        foreach (var (name, text) in sources)
        {
            Assert.False(hex.IsMatch(text), $"{name} names a colour by hex value.");
            Assert.False(keyword.IsMatch(text), $"{name} names a colour keyword.");
            Assert.False(channels.IsMatch(text),
                $"{name} builds a colour from raw channel values. Add a token to theme.css instead.");
        }
    }

    /// <summary>
    /// What the colour scan has to ignore, and why each exclusion is safe.
    ///
    /// <list type="bullet">
    ///   <item>Comments. A comment saying "never write rgb() here" failed the test that
    ///     comment exists to explain.</item>
    ///   <item>Numeric HTML entities. <c>&amp;#8212;</c> — an em dash — is four hex-looking
    ///     digits behind a hash, and reads as a colour to the regex.</item>
    ///   <item><c>color-mix()</c>, the one channel function this codebase uses. It mixes
    ///     tokens; its arguments are <c>var(…)</c> or <c>transparent</c>, never a literal,
    ///     and any literal inside one is still caught because only the function name is
    ///     masked.</item>
    /// </list>
    /// </summary>
    private static string Scannable(string text, bool css)
    {
        var stripped = css
            ? Rx(@"/\*.*?\*/", RegexOptions.Singleline).Replace(text, " ")
            : Rx(@"@\*.*?\*@", RegexOptions.Singleline).Replace(text, " ");

        stripped = Rx("&#x?[0-9A-Fa-f]+;").Replace(stripped, " ");
        return Rx(@"\bcolor-mix\s*\(", RegexOptions.IgnoreCase).Replace(stripped, "MIX(");
    }

    /// <summary>
    /// The same rule from the other side. A component carrying its own
    /// <c>&lt;style&gt;</c> element can style anything without going near app.css, which is
    /// where every rule above stops applying.
    /// </summary>
    [Fact]
    public void NoComponentCarriesItsOwnStylesheet() =>
        Assert.All(RazorFiles, f =>
            Assert.DoesNotContain("<style", File.ReadAllText(f), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Paper is white and ink is dark enough to read. That is the whole rule, and it is the
    /// one a Villain sheet used to break: on the screen palette it printed its near-black
    /// surface edge to edge — unreadable, and it empties a cartridge per character.
    ///
    /// <para>It is <b>not</b> a rule that everything prints grey. The published sheet is in
    /// full colour and the two modes keep their own headings, rules and heading bars; what
    /// they may not do is darken the paper or lighten the ink.</para>
    ///
    /// <para>The <b>values</b> are checked, not the presence of the token. Asserting only
    /// that <c>--ink</c> is declared lets it be declared white, and white ink on white paper
    /// prints blank pages that look like a printer fault rather than a bug.</para>
    /// </summary>
    [Theory]
    [InlineData("hero")]
    [InlineData("villain")]
    public void PrintKeepsThePaperWhiteAndTheInkReadable(string mode)
    {
        var print = OnlyPrintBlockOf(ThemeCss);

        Assert.Contains($":root[data-mode=\"{mode}\"]", print, StringComparison.Ordinal);

        // Resolved the way the cascade resolves it: the shared block first, then the mode's.
        var palette = PrintPalette(print, mode);

        // Paper, and anything that sits behind body text.
        foreach (var token in new[] { "--surface", "--panel", "--panel-sunk", "--primary", "--accent-soft" })
            Assert.True(Luminance(palette[token]) > 0.85,
                $"print {token} darkens the paper in {mode} mode ({palette[token]}).");

        // Ink. 0.45 is about a 4.5:1 contrast floor against white, which is what the small
        // print on this sheet needs.
        foreach (var token in new[] { "--ink", "--heading", "--rule", "--accent", "--on-primary", "--muted", "--danger" })
            Assert.True(Luminance(palette[token]) < 0.45,
                $"print {token} is too pale to read on white in {mode} mode ({palette[token]}).");
    }

    /// <summary>
    /// Every token the two screen palettes declare has to be resolved by the print block for
    /// each mode, whether from the shared rule or the mode's own. A token left out keeps its
    /// screen value through the cascade, which is exactly how the near-black page happened.
    /// </summary>
    [Theory]
    [InlineData("hero")]
    [InlineData("villain")]
    public void PrintRestatesEveryTokenTheScreenPalettesDeclare(string mode)
    {
        // The two mode palettes only. The shape and motion tokens in the plain `:root` block
        // are not colours and print has no reason to restate a transition duration.
        var screen = ThemeCss[..ThemeCss.IndexOf("@media print", StringComparison.Ordinal)];

        var declared = Rx(@"data-mode=""(hero|villain)""\s*\]?\s*\{([^}]*)\}", RegexOptions.Singleline)
            .Matches(screen)
            .SelectMany(m => Rx(@"(--[a-z-]+)\s*:").Matches(m.Groups[2].Value).Select(d => d.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(declared);

        var palette = PrintPalette(OnlyPrintBlockOf(ThemeCss), mode);

        foreach (var token in declared)
            Assert.True(palette.ContainsKey(token),
                $"print does not restate {token} for {mode}, so its screen value survives.");
    }

    /// <summary>
    /// The type tokens are restated for paper too.
    ///
    /// <para><b>The theory above cannot see them and would not notice them going.</b> It
    /// derives its list from the two <c>data-mode</c> blocks, and these three live on a bare
    /// <c>:root</c> because they are not per-mode — so a screen tracking of 0.06em, which reads
    /// as deliberate at 1.7rem, would silently open an 8pt heading bar on paper into loose
    /// letters. They are restated today; nothing held them there.</para>
    ///
    /// <para><c>--font-display</c> and <c>--font-body</c> are deliberately <b>not</b> in this
    /// list: they have no per-mode variant to survive, and paper wants the same two faces the
    /// screen does.</para>
    /// </summary>
    [Theory]
    [InlineData("--display-track")]
    [InlineData("--display-leading")]
    [InlineData("--label-track")]
    public void PrintRestatesTheTypeTokensToo(string token)
    {
        var print = OnlyPrintBlockOf(ThemeCss);
        var screen = ThemeCss[..ThemeCss.IndexOf("@media print", StringComparison.Ordinal)];

        Assert.Contains($"{token}:", Normalise(screen), StringComparison.Ordinal);
        Assert.Contains($"{token}:", Normalise(print), StringComparison.Ordinal);
    }

    /// <summary>
    /// The print block's tokens as the cascade resolves them for one mode: the unqualified
    /// rule, then the mode's own on top.
    /// </summary>
    private static Dictionary<string, string> PrintPalette(string print, string mode)
    {
        var palette = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match rule in Rx(@"([^{}]+)\{([^{}]*)\}").Matches(print))
        {
            var selectors = rule.Groups[1].Value;
            var appliesToMode = selectors.Contains($"data-mode=\"{mode}\"", StringComparison.Ordinal)
                                || !selectors.Contains("data-mode", StringComparison.Ordinal);

            if (!appliesToMode) continue;

            foreach (Match declaration in Rx(@"(--[a-z-]+)\s*:\s*([^;]+);").Matches(rule.Groups[2].Value))
                palette[declaration.Groups[1].Value] = declaration.Groups[2].Value.Trim();
        }

        return palette;
    }

    /// <summary>
    /// Durations are tokens so one media query can switch the whole app to no motion for
    /// anyone whose system asks for it.
    ///
    /// <para>The first version of this test anchored the number to the colon, which meant it
    /// could not see the shorthand — and every transition in app.css is written
    /// <c>transition: color 180ms ease</c>, so it matched none of them and would have missed
    /// a hard-coded duration in all eleven.</para>
    /// </summary>
    [Fact]
    public void MotionIsTokenisedAndCanBeTurnedOffWholesale()
    {
        Assert.Contains("prefers-reduced-motion: reduce", ThemeCss, StringComparison.Ordinal);

        var body = Rx(@"/\*.*?\*/", RegexOptions.Singleline).Replace(AppCss, " ");
        var duration = Rx(@"\b\d[\d.]*m?s\b").Match(body);

        Assert.False(duration.Success,
            $"app.css states the duration '{duration.Value}' outright. Use --step / --swap / "
            + "--enter so prefers-reduced-motion can switch it off.");
    }

    // ── No rule names a raw length ──────────────────────────────────────────────

    /// <summary>
    /// The screen half of the stylesheet asks the spacing and type scales for every padding,
    /// margin, gap and font size. It may not name one itself.
    ///
    /// <para>This is the same rule as colour, typeface, radius and duration, for the same
    /// reason, and it was the last of the five missing. Before the scales existed the screen
    /// half spent <b>twenty-seven</b> distinct lengths on padding, margin and gap and
    /// <b>twenty</b> on font size, ten of the latter crowded between 0.68rem and 0.9rem where
    /// no reader can tell one from the next. That is not a design, it is a history of
    /// individual decisions — and nothing could have told you so, because every one of them
    /// was locally reasonable.</para>
    ///
    /// <para><b>Both spellings are refused, which is the point.</b> Checking only <c>rem</c>
    /// leaves <c>padding: 13px</c> as an open door, and px is what somebody reaching for a
    /// value rather than a rung would most naturally write.</para>
    ///
    /// <para><b>The print block is deliberately out of scope.</b> It is mm and pt — a
    /// different medium with its own scale, where 2.5mm is a measurement of paper and not a
    /// rung on a screen rhythm. Its sizes have their own tests further down this file
    /// (<c>NothingOnPaperIsSetBelowSevenPoint</c> and the break rules).</para>
    ///
    /// <para><c>index.html</c> is scanned too. It is the other file in the payload that can
    /// carry CSS, and both the colour and typeface rules once missed it. <b>Its expected reach
    /// is zero and that is stated rather than hidden</b>: it carries no CSS at all today, so
    /// this half of the theory is a prohibition on a file that could gain some — the CSP allows
    /// an inline <c>&lt;style&gt;</c> block there — and not a check on any rule that exists.
    /// Only the app.css half proves the scan can see anything.</para>
    /// </summary>
    [Theory]
    [InlineData("app.css", 100)]
    [InlineData("index.html", 0)]
    public void NoScreenRuleNamesARawSpacingOrTypeLength(string what, int leastExpected)
    {
        var css = what == "app.css" ? ScreenHalfOfAppCss : Scannable(IndexHtml, css: true);
        var scanned = 0;

        foreach (var (selector, declarations) in RulesOf(css))
        {
            foreach (Match declaration in Rx(@"(?<prop>(?:padding|margin|gap|font-size)[a-z-]*)\s*:\s*(?<value>[^;}]+)")
                         .Matches(declarations))
            {
                var property = declaration.Groups["prop"].Value;
                var value = declaration.Groups["value"].Value.Trim();
                scanned++;

                // Normalised on both sides. Comparing a normalised declaration against a
                // hand-written exemption matched nothing, so every exemption was dead and the
                // rule fired on all three of them — which is the failure mode this whole test
                // is about, arriving first inside the test itself.
                if (ExemptLengths.Any(e => e.Selector == selector
                                           && Normalise(e.Declaration) == Normalise($"{property}:{value}")))
                    continue;   // and the reason is checked by EveryExemptedLengthStillExists

                // Every var(…) reference removed, then anything still carrying a digit and a
                // unit is a literal. `0`, `auto` and a bare ratio survive this and should.
                var residue = Rx(@"var\(\s*--[a-z0-9-]+\s*\)").Replace(value, " ");
                var literal = Rx(ANumberWithAUnit).Match(residue);

                Assert.False(literal.Success,
                    $"{what}: `{selector} {{ {property}: {value} }}` names the length "
                    + $"'{literal.Value}' outright. Use a --space-* or --text-* token from "
                    + "theme.css, or add it to ExemptLengths with a reason.");
            }
        }

        // A scan that matched nothing passes every assertion in it. That has happened in this
        // file before — thirteen of one guard's twenty-five selectors matched no rendered page
        // at all — so the instrument reports its own reach.
        Assert.True(scanned >= leastExpected,
            $"{what}: only {scanned} spacing or type declarations were found, expected at least "
            + $"{leastExpected}. The scan is not reading the stylesheet it is supposed to read.");
    }

    /// <summary>
    /// A number carrying any unit at all. <b>Not a list of units.</b>
    ///
    /// <para>This has now been the wrong shape twice, and the second time was worse because it
    /// was written knowing better. The first version listed <c>px|rem|em|ch|vh|vw|%</c> and
    /// <c>margin-top: 9pt</c> walked through. The second listed thirty-odd units, said in its own
    /// doc comment that "an allow-list of units is the wrong shape for a ban" — and then shipped a
    /// longer allow-list, which <c>margin-top: 9dvmin; padding: 3svb 2lvi; font-size: 4PX</c>
    /// walked through four times over: <c>dvi dvb dvmin dvmax svi svb svmin svmax lvi lvb lvmin
    /// lvmax</c> were all missing, and the match was case-sensitive so every capitalised spelling
    /// of every unit escaped as well.</para>
    ///
    /// <para><b>Inverted, so there is nothing to leave out.</b> In these four properties a digit
    /// followed by letters or a percent sign is a length, whatever the letters are — CSS has no
    /// other meaning for that shape here, and a unit invented by a future specification is caught
    /// on the day it ships rather than on the day somebody remembers it. Bare <c>0</c> and
    /// keywords like <c>auto</c> and <c>inherit</c> carry no digit and are unaffected.</para>
    /// </summary>
    /// <remarks>
    /// No whitespace between the number and the unit, because CSS allows none — and permitting it
    /// made <c>margin: 0 auto</c> read as the length "0 auto", which is a false positive on a
    /// perfectly ordinary centring rule.
    /// </remarks>
    private const string ANumberWithAUnit = @"\d*\.?\d+(?:[A-Za-z]+|%)";

    /// <summary>
    /// theme.css is the only file that may <b>declare</b> a custom property.
    ///
    /// <para><b>This is the door two separate mutations walked through, and neither needed a
    /// token name the scales use.</b> A custom property declared in app.css is not one of the four
    /// properties the raw-length scan reads, and every <c>var()</c> reference is stripped before
    /// the scan looks for a literal — so <c>--table-inset: 1.2rem</c> beside
    /// <c>width: calc(100% - var(--table-inset))</c> restored the table misalignment byte for
    /// byte, at 3.20px on all fifteen tables, with the suite green; and
    /// <c>--pad-lg: 4rem</c> with <c>padding: var(--pad-lg)</c> re-padded an element with a raw
    /// length under a name no rule about the scales could ever match.</para>
    ///
    /// <para>Narrowing the earlier check to <c>--space-*</c> and <c>--text-*</c> was the mistake:
    /// it defended the *names* of the scales rather than the property that makes a scale
    /// meaningful, which is that there is one place lengths are decided. <b>The general rule is
    /// the enforceable one</b>, it costs nothing — app.css declares no custom property today —
    /// and it is the same rule already holding for colour and for typefaces.</para>
    /// </summary>
    [Theory]
    [InlineData("app.css")]
    [InlineData("index.html")]
    public void OnlyTheThemeDeclaresACustomProperty(string what)
    {
        var css = WithoutCssComments(what == "app.css" ? AppCss : IndexHtml);

        // A declaration, not a reference: `--x:` at the start of a declaration rather than
        // inside `var(--x)`. The lookbehind is what tells the two apart.
        var declaration = Rx(@"(?<!var\(\s*)(--[A-Za-z0-9-]+)\s*:");
        var found = declaration.Match(css);

        Assert.False(found.Success,
            $"{what} declares the custom property {found.Groups[1].Value}. theme.css is the only "
            + "file that may — a token declared here is a length, colour or duration decided "
            + "outside the one file that is supposed to decide them, and no scan of padding, "
            + "margin, gap or font-size can see it.");

        // The instrument reports its reach: theme.css must declare plenty, or the regex is wrong
        // and this test is passing because it matches nothing anywhere.
        Assert.True(declaration.Count(WithoutCssComments(ThemeCss)) > 20,
            "The declaration pattern finds almost nothing in theme.css, so it is not capable of "
            + "finding one in " + what);
    }

    /// <summary>
    /// The three literals the rule above allows, each with the selector it belongs to.
    ///
    /// <para>All three are <b>optical</b> rather than rhythmic, which is the distinction that
    /// makes them exemptions rather than holes: a spacing token says how far apart two things
    /// sit, and these three adjust one thing against itself. The pair of 1px paddings are the
    /// gap between a word and the border-bottom standing in for its underline — a hairline, and
    /// on the scale it would be 2px, which is a visibly detached underline. The negative em
    /// pulls back the letter-spacing added after the final character of a Hero Point cost so
    /// the column of them lines up on the right edge; it is in em because it is a fraction of
    /// the tracking, and a spacing token in rem cannot express that.</para>
    ///
    /// <para>Each is asserted to still exist, by the test below. A stale exemption is a hole
    /// somebody can walk through later, so the list is not allowed to outlive its subjects.</para>
    /// </summary>
    /// <remarks>
    /// <c>AlsoRequires</c> is the exemption's own <em>reason</em>, asserted. The two 1px paddings
    /// are justified entirely by the <c>border-bottom</c> they sit against — that is what makes
    /// them an optical hairline rather than spacing — and a guard that checked only that the
    /// declaration still existed passed after the border was replaced with
    /// <c>text-decoration: underline</c>, leaving 1px of dead padding and a stated reason that
    /// was no longer true. The doc below calls a stale exemption "worse than a missing one"; that
    /// is the stale case that matters, and checking the string alone could not see it.
    /// </remarks>
    /// <remarks>
    /// <c>AlsoRequires</c> is a property <b>name</b> with no colon — it is looked up through
    /// <see cref="EffectiveValue"/>, which supplies the colon itself. Written as
    /// <c>"border-bottom:"</c> it produced a pattern demanding two colons, matched nothing, and
    /// reported every exemption's reason as missing.
    /// </remarks>
    private static readonly (string Selector, string Declaration, string AlsoRequires, string RequiredOn)[] ExemptLengths =
    [
        (".budget-toggle", "padding:0 0 1px", "border-bottom", ".budget-toggle"),
        (".banner-link", "padding-bottom:1px", "border-bottom", ".banner-link"),
        // The standard clip-to-nothing pattern for text that is read out and never drawn. The
        // -1px is part of the recipe rather than spacing anybody chose: the element is a 1px box
        // pulled back over itself so it occupies no layout at all, and rounding it to a spacing
        // rung would give it size. `clip-path` is the precondition — with the clip gone this is
        // just an element positioned 1px off, which scrolls into view and is a different bug.
        (".sr-only", "margin:-1px", "clip-path", ".sr-only"),
        // The negative em pulls back the letter-spacing added after the final character, so the
        // thing it depends on is the letter-spacing, not a border — and it comes from the base
        // `.hp` rule, which is why the selector it is required on is not the one it is exempt on.
        (".power-entry .head .hp", "margin-right:-0.08em", "letter-spacing", ".hp"),
    ];

    /// <summary>
    /// Every exemption above still names a rule that exists and still carries that declaration.
    ///
    /// <para><b>A guard that enumerates subjects has to refuse a subject it never found.</b>
    /// That lesson is recorded twice in this project already: an uppercased-text guard shipped
    /// with 13 of its 25 selectors matching nothing, and a font check filtered out the very
    /// face it existed to correlate. An exemption whose selector has been renamed away is
    /// worse than a missing one — it silently permits that declaration on any selector, because
    /// the tuple can never match again and nothing says so.</para>
    /// </summary>
    [Fact]
    public void EveryExemptedLengthStillExists()
    {
        var rules = RulesOf(ScreenHalfOfAppCss);

        foreach (var (selector, declaration, alsoRequires, requiredOn) in ExemptLengths)
        {
            var rule = rules.Where(r => r.Selector == selector).ToList();

            Assert.True(rule.Count > 0,
                $"ExemptLengths names `{selector}`, which no screen rule in app.css declares. "
                + "Remove the exemption or fix the selector — a tuple that can never match "
                + "permits its declaration everywhere.");

            Assert.True(rule.Any(r => Normalise(r.Declarations).Contains(Normalise(declaration), StringComparison.Ordinal)),
                $"`{selector}` no longer carries `{declaration}`. The exemption is stale.");

            // And the thing the exemption's reason rests on is still **in effect**. Without this
            // the declaration survives as dead decoration while the justification for exempting
            // it has gone — the stale case that actually costs something.
            //
            // Read as a **value**, on the **named** selector. Two routes reached the same end
            // state past weaker versions of this: `border-bottom: none` instead of deleting the
            // line, which a check for the property name accepted; and — once the value was read —
            // `.sheet .budget-toggle { border-bottom: … }`, a selector matching nothing in this
            // app, supplying the reason while the real rule lost it. A source-reading test cannot
            // know which selectors match real elements, so the exemption names the one that has to
            // carry its reason. For `.hp` that is the base rule rather than the exempt selector,
            // because the letter-spacing is inherited.
            var effective = EffectiveValue(ScreenHalfOfAppCss, requiredOn, alsoRequires, exact: true);

            Assert.True(effective is not null,
                $"`{requiredOn}` does not declare `{alsoRequires}`, which is the whole reason "
                + $"`{declaration}` on `{selector}` is exempt. The literal is now unjustified "
                + "rather than exempt.");

            Assert.True(effective is not ("none" or "normal" or "0" or "unset" or "initial"),
                $"`{alsoRequires}` resolves to `{effective}` on `{requiredOn}`, which is the same "
                + $"as not having it — so `{declaration}` is dead decoration and its stated reason "
                + "is false. Remove both, or remove the exemption.");
        }
    }

    /// <summary>
    /// The spacing and type scales, rung by rung, <b>with their values</b>.
    ///
    /// <para><b>The literals are duplicated here on purpose, and the duplication is the whole
    /// point.</b> The first version of this guard asserted that each token <em>name</em> was
    /// declared in theme.css and referenced by app.css, and never read a value — so
    /// <c>--space-4: 4rem</c> passed, re-padding most boxes in the app, the narrow-viewport
    /// shell and the strip's bleed from 12px to 64px with the whole suite green. Nothing else
    /// in the suite pinned any <c>--space-*</c> at all.</para>
    ///
    /// <para>This is the pattern <c>PROGRESS.md</c> records for the server instructions and the
    /// baseline note: where the value <em>is</em> the deliverable, the value is the assertion,
    /// and what the duplicated literal buys is that changing it has to be deliberate and visible
    /// in a diff. A design scale is exactly that kind of subject — the point of it is that the
    /// rungs stop moving.</para>
    /// </summary>
    private static readonly (string Name, double Rem)[] SpaceScale =
    [
        ("--space-0", 0.125), ("--space-1", 0.25), ("--space-2", 0.375), ("--space-3", 0.5),
        ("--space-4", 0.75),  ("--space-5", 1.0),  ("--space-6", 1.5),   ("--space-7", 2.0),
        ("--space-8", 3.0),
    ];

    private static readonly (string Name, double Rem)[] TextScale =
    [
        ("--text-xs", 0.72), ("--text-sm", 0.82), ("--text-base", 0.97), ("--text-lg", 1.15),
        ("--text-xl", 1.4),  ("--text-2xl", 1.7), ("--text-3xl", 2.15),
    ];

    /// <summary>
    /// Every rung of a scale is declared in theme.css at the value recorded above, is asked for
    /// by app.css, and is the only rung of that scale in existence.
    ///
    /// <para><b>Four things, and the first version had only two of them.</b> A token declared
    /// and used by nothing is dead weight that reads as a decision — this app shipped a
    /// <c>.label-line</c> class applied to nothing for exactly that reason, and it was found by
    /// looking at a rendered page rather than by any test. A token used and not declared is a
    /// rule that silently does nothing, because an unresolvable <c>var()</c> invalidates the
    /// whole declaration: the browser drops it and no error appears anywhere. The value is
    /// checked because a name-only check pins nothing. And the rungs must be strictly
    /// increasing, because a scale whose steps are not ordered is a list.</para>
    ///
    /// <para><b>The set is read from every stylesheet in the payload, not from theme.css.</b>
    /// The range check's own comment said it stopped "one <c>--space-9</c> at a time, each
    /// locally reasonable" — and it computed the declared set from theme.css alone, so a rung
    /// declared inside a <c>:root</c> block in <em>app.css</em> joined the scale invisibly and
    /// the raw-length scan waved through every <c>var()</c> that used it. That is precisely the
    /// accumulation this rule exists to prevent, entering by the one door nobody watched.</para>
    /// </summary>
    [Theory]
    [InlineData("space")]
    [InlineData("text")]
    public void EveryRungOfAScaleIsDeclaredOnceAtItsRecordedValue(string which)
    {
        var scale = which == "space" ? SpaceScale : TextScale;
        var prefix = which == "space" ? "--space-" : "--text-";

        var app = WithoutCssComments(AppCss);

        foreach (var (name, rem) in scale)
        {
            // **Every declaration of the rung, not one of them.** `Assert.Contains` on the
            // expected literal was satisfied by the original while a duplicate lower in the same
            // `:root` block won the cascade — a fix-audit put `--space-4: 4rem` under `--space-8`
            // and the workhorse rung sat at 64px with the full suite green. So a rung is required
            // to be declared exactly once, and that once at its recorded value. Declaring it
            // twice is refused outright even if both copies agree: a scale with two homes for one
            // rung is a scale that will disagree with itself later.
            var declarations = DeclarationsOf(name);

            Assert.True(declarations.Count == 1,
                $"{name} is declared {declarations.Count} times in theme.css "
                + $"({string.Join(", ", declarations)}). A rung has one home.");

            Assert.Equal($"{rem.ToString(CultureInfo.InvariantCulture)}rem", declarations[0]);
            Assert.Contains($"var({name})", app, StringComparison.Ordinal);
        }

        // Strictly increasing, in the order recorded. A scale is an ordering before it is a set
        // of values, and two rungs that cross make every "a step down from" comment a lie.
        var values = scale.Select(s => s.Rem).ToList();
        Assert.Equal(values.Order(), values);
        Assert.Equal(values.Distinct().Count(), values.Count);

        // No rung of this scale declared anywhere but theme.css, and none there beyond the set
        // above. Both stylesheets and index.html are read: theme.css is the only file allowed
        // to declare one, so a :root block elsewhere is the interesting case.
        foreach (var (where, css) in new[] { ("app.css", AppCss), ("index.html", IndexHtml) })
            Assert.DoesNotMatch(Rx($@"{Regex.Escape(prefix)}[a-z0-9-]+\s*:"), WithoutCssComments(css));

        var declared = Rx($@"({Regex.Escape(prefix)}[a-z0-9-]+)\s*:")
            .Matches(WithoutCssComments(ThemeCss))
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(scale.Select(s => s.Name).Order(StringComparer.Ordinal), declared);
    }

    /// <summary>
    /// The three elevation steps are declared for both palettes, all three are used, and no two
    /// of them are the same shadow.
    ///
    /// <para>Three tokens holding one value is the state this scale replaced, spelled three ways
    /// — and a name-only check cannot tell the difference. The <em>values</em> differ per palette,
    /// which is why they are declared with the colours; what is asserted here is that within a
    /// palette the three steps are three steps.</para>
    /// </summary>
    [Theory]
    [InlineData("hero")]
    [InlineData("villain")]
    public void TheThreeElevationStepsAreThreeDifferentShadows(string mode)
    {
        var screen = ThemeCss[..ThemeCss.IndexOf("@media print", StringComparison.Ordinal)];

        // The mode's own palette block. Hero is the unqualified :root as well as [data-mode=hero].
        var block = Rx($@"data-mode=""{mode}""\s*\]\s*\{{([^}}]*)\}}", RegexOptions.Singleline)
            .Match(WithoutCssComments(screen));

        Assert.True(block.Success, $"theme.css has no {mode} palette block.");

        var shadows = Rx(@"(--shadow-[123])\s*:\s*([^;]+);")
            .Matches(block.Groups[1].Value)
            .ToDictionary(m => m.Groups[1].Value, m => Normalise(m.Groups[2].Value), StringComparer.Ordinal);

        Assert.Equal(3, shadows.Count);
        Assert.Equal(3, shadows.Values.Distinct(StringComparer.Ordinal).Count());

        foreach (var name in new[] { "--shadow-1", "--shadow-2", "--shadow-3" })
            Assert.Contains($"var({name})", WithoutCssComments(AppCss), StringComparison.Ordinal);
    }

    /// <summary>
    /// Elevation is a hierarchy, and <c>--shadow-3</c> is claimed by one element.
    ///
    /// <para>The sticky budget strip is the only thing in the app that moves independently of
    /// the document, and so the only thing that should read as floating over it. One
    /// <c>--shadow</c> used to carry the banner, every panel, the sheet, the tier cards and the
    /// strip — five heights spelled one way, which is the same as no height at all. Spreading
    /// the top step back across the page would undo the distinction without changing a single
    /// value, which is why this is asserted by count rather than by the token existing.</para>
    /// </summary>
    [Fact]
    public void OnlyTheStickyStripReadsAsFloating()
    {
        var floating = RulesOf(ScreenHalfOfAppCss)
            .Where(r => r.Declarations.Contains("var(--shadow-3)", StringComparison.Ordinal))
            .Select(r => r.Selector)
            .ToList();

        Assert.Equal([".budget"], floating);

        // The strip is sticky, which is the property that earns it the top step. Asserted here
        // rather than left implied: if it stops being sticky it stops being the exception.
        //
        // Every rule targeting `.budget`, not one of them — there are two, the base rule and
        // the narrow-viewport rule that re-states the bleed, and `Single` threw on the pair.
        //
        // **What actually applies, which took two goes to get right.** `Contains("position:
        // sticky")` was satisfied by a declaration overridden one line below it, so
        // `position: static` un-stuck the strip while it kept the top elevation step. Reading the
        // last declaration fixed that and left the other half: filtering on
        // `Selector == ".budget"` misses `.budget, .breakdown { position: static }` added later,
        // because a comma list is not that string — and the comment written with the first fix
        // claimed it read *every* rule targeting `.budget`, which it did not.
        Assert.Equal("sticky", EffectiveValue(ScreenHalfOfAppCss, ".budget", "position"));
    }

    /// <summary>
    /// No <c>calc()</c> on the screen side names a raw length. If a value is derived from a
    /// spacing rung, it derives from the <b>token</b>.
    ///
    /// <para><b>This is the class of bug the four-property scan let through, one property name
    /// outside its own scope.</b> <c>.sheet-section &gt; table</c> read
    /// <c>width: calc(100% - 1.2rem)</c> and was a matched pair with the 0.6rem inset on its
    /// siblings. Phase 0 moved the inset onto the scale and left the width behind, so a table's
    /// right edge fell 3.2px short of every other child of the box and of the heading bar above
    /// it — 8.00px of inset on the left against 11.20px on the right, on five boxes a sheet, in
    /// both palettes. Two reviewers found it independently by measuring a rendered sheet; no test
    /// could, because <c>width</c> is not padding, margin, gap or font-size.</para>
    ///
    /// <para>Banning the literal inside the <c>calc()</c> is the general fix, and it is a better
    /// rule than adding <c>width</c> to the property list would have been: what makes the bug
    /// possible is not the property, it is a number that has to agree with a token and has no
    /// way of doing so. <c>calc(100% - 2 * var(--space-3))</c> cannot fall out of step.</para>
    ///
    /// <para>Percentages and unitless factors are fine — <c>100%</c> is the container and
    /// <c>-1</c> and <c>2</c> are multipliers, not lengths.</para>
    /// </summary>
    [Fact]
    public void NoScreenCalcNamesARawLength()
    {
        var calls = Rx(@"calc\(([^()]*(?:\([^()]*\)[^()]*)*)\)").Matches(ScreenHalfOfAppCss);

        Assert.NotEmpty(calls);

        var absolute = Rx(ANumberWithAUnit);

        foreach (Match call in calls)
        {
            // The percentage is a share of the container, not a length somebody chose.
            var body = Rx(@"\d*\.?\d+%").Replace(call.Groups[1].Value, " ");
            var literal = absolute.Match(body);

            Assert.False(literal.Success,
                $"`calc({call.Groups[1].Value})` names the length '{literal.Value}'. If it is "
                + "derived from a spacing rung, derive it from the token: a literal that agrees "
                + "with a token today has no way of still agreeing tomorrow.");
        }
    }

    /// <summary>
    /// The chrome bands centre their contents on the same column as the shell, and reserve the
    /// same padding around it, at every breakpoint — so the step labels, the spend figure and
    /// the page heading share a left edge.
    ///
    /// <para><b>This replaces a guard on a mechanism that no longer exists, which is the better
    /// outcome of the two.</b> The step list and the budget strip used to live inside the shell
    /// and escape its padding with a negative margin; that bleed had to cancel the shell's
    /// padding exactly, and when the narrow-viewport query cut the padding and left the pull
    /// alone the band ran 8px past the page on both sides — scrollWidth 368 against clientWidth
    /// 360 at a 375px viewport. The bands are siblings of <c>main</c> now and are already the
    /// width of the window, so there is nothing to escape and no negative margin anywhere in the
    /// file. <b>An invariant is better deleted than guarded when the thing it constrains can be
    /// removed.</b></para>
    ///
    /// <para>What remains is a real requirement in the direction that cannot overflow: three
    /// bands agreeing on one column. It is the agreement that is asserted rather than any value,
    /// so the column may be re-padded to any rung as long as all three follow — and the media
    /// queries are discovered rather than listed, so a third breakpoint is covered the day it is
    /// added rather than the day somebody remembers it. That is the half the old bleed rules got
    /// wrong.</para>
    /// </summary>
    [Fact]
    public void TheChromeBandsShareTheShellsColumn()
    {
        var css = ScreenHalfOfAppCss;

        // The shell, and the inner column of every band outside it — **including the banner's**,
        // which the first version of this left out while the layout's own comment offered the
        // banner as the example the others follow. It was the only band not on the column.
        string[] columns = [".shell", ".banner-inner", ".steps-list", ".budget-strip", ".breakdown"];

        // The band elements themselves. A band is a full-width fill and may not pad its own
        // contents: padding here shifts the column inside it and nothing else, so
        // `.budget { padding: … var(--space-6) … }` moved the spend figure 24px off the heading
        // and stopped the rail running edge to edge, with the column checks all still passing.
        string[] bands = [".banner", ".steps", ".budget"];

        var scopes = new List<(string Where, string Css)> { ("the base rules", TopLevelOf(css)) };

        // **Every media query touching any of the columns, not just the ones that mention
        // `.shell`.** Gating on `.shell` meant a breakpoint re-padding only the chrome bands was
        // not a scope at all, so between two breakpoints the step labels sat 8px in while the
        // heading sat 24px in — the exact "stop sharing a left edge" failure this test is for,
        // arriving in a query the scan declined to look at.
        foreach (var (condition, body) in MediaQueriesOf(css))
            if (columns.Any(c => body.Contains(c, StringComparison.Ordinal)))
                scopes.Add(($"@media {condition}", body));

        Assert.True(scopes.Count >= 2,
            "Only one scope touching a column was found. The narrow-viewport query pads all five, "
            + "so the query scan is not reading the stylesheet.");

        // Every column is capped on the same token, and **centred**, and neither was fully
        // checked. `max-width` was read in the base rules only, so a new breakpoint widening
        // `.shell` alone passed while the doc claimed queries were discovered rather than listed;
        // and centring was not read at all, so `.budget-strip { margin: 0 }` put the spend figure
        // flush against the window edge while the labels above and the heading below stayed on the
        // column — the exact failure this test's summary names.
        foreach (var column in columns)
        {
            Assert.Equal("var(--column)", EffectiveValue(css, column, "max-width"));

            var margin = EffectiveValue(css, column, "margin");
            Assert.True(margin is not null && margin.Contains("auto", StringComparison.Ordinal),
                $"{column} is capped on --column but not centred (margin: {margin ?? "unset"}), so "
                + "it sits at the left edge of a band that runs the whole window.");
        }

        // And no band pads its own contents, **by any spelling**. Asked through
        // `HorizontalPaddingTokenOf` this passed for `padding-inline: var(--space-6)`, because that
        // reader returned null for "no padding" and for "a spelling I do not read" alike — so the
        // question is put to the one that only asks whether the property is set at all.
        foreach (var band in bands)
            Assert.False(SetsAnyHorizontalPadding(css, band),
                $"{band} sets horizontal padding. A band is a full-width fill; the padding belongs "
                + "to the column inside it, or the two stop agreeing and the rail stops running "
                + "edge to edge.");

        // And every band reserves the same padding around it, in each scope that sets any of
        // them. A scope that re-pads the shell and forgets a band is the failure the old bleed
        // rules had, arriving from the other side.
        foreach (var (where, scope) in scopes)
        {
            var reserved = columns
                .Select(c => (Column: c, Padding: HorizontalPaddingTokenOf(scope, c)))
                .Where(p => p.Padding is not null)
                .ToList();

            if (reserved.Count == 0) continue;

            Assert.True(reserved.Count == columns.Length,
                $"{where} pads {string.Join(", ", reserved.Select(r => r.Column))} but not "
                + $"{string.Join(", ", columns.Except(reserved.Select(r => r.Column)))}. All "
                + $"{columns.Length} columns narrow together or they stop sharing a left edge.");

            Assert.True(reserved.Select(r => r.Padding).Distinct(StringComparer.Ordinal).Count() == 1,
                $"{where} reserves different padding per band: "
                + string.Join(", ", reserved.Select(r => $"{r.Column} {r.Padding}")));
        }
    }

    /// <summary>
    /// Every <c>@media</c> block, as its condition and its body, found by <b>matching braces</b>
    /// rather than by pattern.
    ///
    /// <para><b>The regex this replaces assumed a formatted block</b> — it ended at the first
    /// <c>\n}</c> — so a query written on one line was not found at all, and its contents were
    /// swallowed into whichever block *did* end that way. A fix-audit's single-line breakpoint duly
    /// evaded the scan, and the guard's own doc claims the queries are discovered. CSS formatting is
    /// not a property a guard may depend on.</para>
    /// </summary>
    private static List<(string Condition, string Body)> MediaQueriesOf(string css)
    {
        var found = new List<(string, string)>();

        foreach (Match at in Rx(@"@media\s*([^{]+)\{").Matches(css))
        {
            var open = at.Index + at.Length - 1;
            var depth = 0;

            for (var i = open; i < css.Length; i++)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}' && --depth == 0)
                {
                    found.Add((at.Groups[1].Value.Trim(), css[(open + 1)..i]));
                    break;
                }
            }
        }

        return found;
    }

    /// <summary>The stylesheet with every <c>@media</c> block's contents removed.</summary>
    private static string TopLevelOf(string css)
    {
        var stripped = css;

        // Longest first, so removing an outer block cannot invalidate an inner one's offsets.
        foreach (var (_, body) in MediaQueriesOf(css).OrderByDescending(q => q.Body.Length))
            stripped = stripped.Replace(body, " ", StringComparison.Ordinal);

        return stripped;
    }

    /// <summary>
    /// The single <c>--space-*</c> token a selector's horizontal padding resolves to, or null if
    /// it sets none or sets the two sides differently. Reads both the shorthand and the
    /// longhands, and reads every rule for the selector rather than the first — the narrow
    /// query writes longhands and the base rule writes a shorthand.
    /// </summary>
    private static string? HorizontalPaddingTokenOf(string css, string selector)
    {
        string? left = null, right = null;

        // **Comma lists split.** This filtered on the whole selector string being equal, which
        // is the same weakness a fix-audit found in the sticky-strip guard — and it bit
        // immediately: the narrow-viewport rule pads the three chrome columns in one grouped
        // rule, so `.steps-list, .budget-strip, .breakdown` matched none of them and the padding
        // read as absent. Exact per-selector match, not a suffix: this asks "does this rule pad
        // this band", and a descendant selector padding something else is not an answer.
        foreach (var declarations in RulesOf(css)
                     .Where(r => r.Selector.Split(',').Select(Normalise).Contains(Normalise(selector), StringComparer.Ordinal))
                     .Select(r => r.Declarations))
        {
            if (Rx(@"padding:\s*([^;}]+)").Match(declarations) is { Success: true } shorthand)
            {
                // The same four-side reader the bleed uses, rather than a second splitter that
                // happens to work on the shorthands in the file today. This one split on plain
                // whitespace, which is correct only while no padding contains a `calc()`.
                var sides = MarginSides(shorthand.Groups[1].Value);
                if (sides is not null)
                {
                    right = TokenIn(sides[1]);
                    left = TokenIn(sides[3]);
                }
            }

            // `padding-inline` sets the same two sides under a name sharing no prefix with them,
            // and this reader knew only the physical pair — so `padding-inline: var(--space-6)` on
            // a band read as "no padding at all" and walked past the guard. Read as one of the
            // spellings rather than refused, because it is the modern way to write exactly this
            // and the file may reasonably use it.
            if (Rx(@"padding-inline:\s*([^;}]+)").Match(declarations) is { Success: true } inline)
            {
                var sides = MarginSides(inline.Groups[1].Value);
                if (sides is not null) { right = TokenIn(sides[1]); left = TokenIn(sides[3]); }
            }

            if (Rx(@"padding-left:\s*([^;}]+)").Match(declarations) is { Success: true } l) left = TokenIn(l.Groups[1].Value);
            if (Rx(@"padding-right:\s*([^;}]+)").Match(declarations) is { Success: true } r) right = TokenIn(r.Groups[1].Value);
            if (Rx(@"padding-inline-start:\s*([^;}]+)").Match(declarations) is { Success: true } s) left = TokenIn(s.Groups[1].Value);
            if (Rx(@"padding-inline-end:\s*([^;}]+)").Match(declarations) is { Success: true } e) right = TokenIn(e.Groups[1].Value);
        }

        return left is not null && left == right ? left : null;
    }

    /// <summary>
    /// Every spelling that sets horizontal padding, so a caller asking "does this element pad its
    /// contents" can be sure a null means none rather than a name the reader above skipped.
    /// </summary>
    private static readonly string[] HorizontalPaddingSpellings =
    [
        "padding", "padding-left", "padding-right",
        "padding-inline", "padding-inline-start", "padding-inline-end",
    ];

    /// <summary>
    /// Whether a selector sets horizontal padding by <b>any</b> spelling — the question
    /// <see cref="HorizontalPaddingTokenOf"/> cannot answer, because it returns null both for "no
    /// padding" and for "padding this reader could not resolve to a rung".
    /// </summary>
    private static bool SetsAnyHorizontalPadding(string css, string selector) =>
        RulesFor(css, selector, exact: true)
            .Any(r => HorizontalPaddingSpellings.Any(p =>
                Rx($@"(?<![\w-]){Regex.Escape(p)}\s*:").IsMatch(r)));

    /// <summary>
    /// A <c>margin</c> shorthand as its four sides, top-right-bottom-left, or null if it cannot
    /// be read. Splitting on whitespace does not work: <c>calc(-1 * var(--space-6))</c> contains
    /// three spaces and would arrive as four parts, which is how the caller above came to be
    /// unable to tell one edge from another.
    /// </summary>
    private static string[]? MarginSides(string value)
    {
        var parts = new List<string>();
        var depth = 0;
        var current = new System.Text.StringBuilder();

        foreach (var c in value.Trim())
        {
            if (c == '(') depth++;
            if (c == ')') depth--;

            if (char.IsWhiteSpace(c) && depth == 0)
            {
                if (current.Length > 0) { parts.Add(current.ToString()); current.Clear(); }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0) parts.Add(current.ToString());

        return parts.Count switch
        {
            1 => [parts[0], parts[0], parts[0], parts[0]],
            2 => [parts[0], parts[1], parts[0], parts[1]],
            3 => [parts[0], parts[1], parts[2], parts[1]],
            4 => [.. parts],
            _ => null,
        };
    }

    /// <summary>
    /// The rung a single box side is set to, or null — and <b>anchored</b>, so the side has to be
    /// exactly that token and nothing else.
    ///
    /// <para><b>This took the first token it found anywhere inside the side, which is the same
    /// first-match weakness the four-side parser was written to remove, surviving one level
    /// down.</b> A fix-audit got through with <c>calc(var(--space-6) * 3)</c> — 72px of padding
    /// reported as matching a 24px bleed. Anchoring means anything more complicated than a bare
    /// token returns null and the caller reports "sets no horizontal padding token", which fails.
    /// That is the right direction: this exists to compare two rungs, and a side doing arithmetic
    /// is not a rung.</para>
    ///
    /// <para>Its negative twin, <c>NegativeTokenIn</c>, went with the bleed it read — the chrome
    /// bands are siblings of <c>main</c> now and there is no negative margin left in the
    /// stylesheet for it to parse. A test helper kept past its subject is the same dead weight as
    /// a CSS class applied to nothing.</para>
    /// </summary>
    private static string? TokenIn(string value) =>
        Rx(@"^var\(\s*(--space-[a-z0-9-]+)\s*\)$").Match(Normalise(value)) is { Success: true } m
            ? m.Groups[1].Value
            : null;

    /// <summary>
    /// The chrome always ends in a visible edge, on the one band that always renders.
    ///
    /// <para><b>This is the most severe defect of the phase, and it was fixed without a guard
    /// until a mutation put it straight back.</b> <c>.steps</c> lost its bottom rule on the
    /// argument that the budget strip directly beneath carries the edge for both — and the strip
    /// is absent on three whole classes of screen: <b>every page in Villain mode</b>, since Ch.9
    /// gives Villains no budget and the component renders nothing at all; <b>the tier page before
    /// a tier is chosen</b>, which is the first screen a new visitor sees; and <b>every
    /// <c>/replay</c> route</b>, where the layout hides it deliberately. On all three the step
    /// chips sat on the page ground with nothing under them.</para>
    ///
    /// <para>So the requirement is on the band that is always there. The three conditions are
    /// asserted as well as the edge, because they are the *reason* for it: if the strip ever
    /// became unconditional the argument for a rule here would change, and a guard whose premise
    /// has quietly gone is worse than none.</para>
    /// </summary>
    [Fact]
    public void TheChromeAlwaysEndsInAVisibleEdge()
    {
        // `exact: true`, because the question is "does the step band still declare this" rather
        // than "what applies to a step band somewhere". Without it a `.sheet .steps` rule —
        // matching no element in this app, since no step nav renders inside a sheet — supplied the
        // value while the real rule had none. That is verbatim the defeat recorded on the `exact`
        // parameter itself from an earlier audit, reached again by a guard that did not pass it.
        var edge = EffectiveValue(ScreenHalfOfAppCss, ".steps", "border-bottom", exact: true);

        Assert.True(edge is not null,
            "The step band sets no bottom edge. The budget strip below it does not render in "
            + "Villain mode, before a tier is chosen, or on a replay route — so on those screens "
            + "nothing closes the chrome and the step chips sit on the page ground.");

        AssertIsAVisibleEdge(".steps", "border-bottom", edge!);

        // The premise. Each of these is what makes the strip conditional; together they are why
        // the edge cannot be left to it.
        var strip = File.ReadAllText(Path.Combine(WebRoot, "Components", "HpBudgetBar.razor"));
        var layout = File.ReadAllText(Path.Combine(WebRoot, "Layout", "MainLayout.razor"));

        Assert.Contains("Session.ShowBudget", strip, StringComparison.Ordinal);
        Assert.Contains("SelectedTierId is not null", strip, StringComparison.Ordinal);
        Assert.Contains("!ShowingARecording", layout, StringComparison.Ordinal);
    }

    /// <summary>
    /// Three treatments that were <b>fixed and left unguarded</b>, so each of their defects reverted
    /// green when a fix-audit deleted the one line that repairs it.
    ///
    /// <para>Each is a single declaration whose whole substance is in the stylesheet, so no
    /// rendering test can see any of them — the class stays on the element and the markup is
    /// identical either way. Together they are the same lesson three more times: a repair without a
    /// guard is a repair that lasts until somebody tidies the file.</para>
    ///
    /// <list type="bullet">
    ///   <item><c>.options</c>'s bottom rule marks where a 22rem scroller <em>clips</em> 141 Powers.
    ///     Without it a row cut through the middle of its own stat line reads as a rendering fault
    ///     against the panel's white, with only the scrollbar thumb saying otherwise.</item>
    ///   <item><c>.grid &gt; .field</c>'s reset. A field carries its own bottom margin and a grid row
    ///     already supplies one, so without it the Sources editor's rows sit 32px apart and its
    ///     columns 16px.</item>
    ///   <item>The untouched ring on the tab strip. Deleting the rule leaves the class on the
    ///     element, every assertion in <c>EmptyStateTests</c> reading <c>ClassList</c>, and no
    ///     marking on the page — the <c>.hp</c> trap yet again, on the sibling of the feature the
    ///     change had just closed it for.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void TheTreatmentsThatOnlyExistInTheStylesheetAreStillThere()
    {
        var css = ScreenHalfOfAppCss;

        // The scroller is clipped, and says so.
        var clip = EffectiveValue(css, ".options", "border-bottom", exact: true);
        Assert.True(clip is not null,
            "`.options` has no bottom rule, so nothing marks where the list is clipped and a "
            + "half-row against the panel's own ground reads as a rendering fault.");
        AssertIsAVisibleEdge(".options", "border-bottom", clip!);

        // A grid cell does not add to the gap the grid already supplies.
        Assert.Equal("0", EffectiveValue(css, ".grid > .field", "margin-bottom", exact: true));

        // And the untouched marker is drawn.
        var ring = RulesFor(css, ".tab-count.untouched", exact: false);
        Assert.NotEmpty(ring);
        Assert.Contains(ring, r => Rx(@"border\s*:").IsMatch(r));
        Assert.All(ring, r =>
            Assert.All(EverySpellingOfHidden, way =>
                Assert.False(Rx(way).IsMatch(Normalise(r)),
                    $"A rule targeting .tab-count.untouched hides it with '{way}'.")));
    }

    /// <summary>
    /// An empty state is marked out as guidance, and the marking is entirely typographic — so it
    /// is entirely in the stylesheet, where no rendering test can see it.
    ///
    /// <para><b>Deleting the whole <c>.empty-state</c> rule left every one of the eight
    /// <c>EmptyStateTests</c> green, and both class-ownership tests too.</b> The class is still on
    /// the element and every one of those assertions reads markup, so the leading edge and the
    /// padding — the entire mechanism that stops the sentence reading as the first row of the list
    /// it stands in for — could go without a word. That is the <c>.hp</c> trap this file already
    /// records verbatim, reproduced by the change that cites it as a lesson.</para>
    ///
    /// <para>Checked over every rule targeting the class rather than the base one, because a more
    /// specific rule further down wins the cascade — the same reason the <c>.hp</c> guard does it.</para>
    /// </summary>
    [Fact]
    public void AnEmptyStateIsMarkedOutAsGuidance()
    {
        var rules = RulesTargeting(".empty-state");

        Assert.NotEmpty(rules);

        var edge = EffectiveValue(ScreenHalfOfAppCss, ".empty-state", "border-left", exact: true);

        Assert.True(edge is not null,
            "The empty state has no leading edge, so it reads as the first row of the list it is "
            + "standing in for rather than as guidance about it.");

        AssertIsAVisibleEdge(".empty-state", "border-left", edge!);

        // And set in from that edge, or the rule sits against the text.
        Assert.True(Rx(@"padding[^;}]*:[^;}]*var\(--space-").IsMatch(string.Join(';', rules)),
            "The empty state sets no padding, so its leading edge touches the sentence.");

        // Never hidden. Everything above is satisfied by an element that is present and invisible,
        // and every rendering test passes on one too — a hidden element keeps its class and its
        // text. The shared list, not a fresh one: the first version written here had four entries
        // and none of the prefix guards, so it would have fired on `opacity: 0.8`.
        Assert.All(rules, rule =>
            Assert.All(EverySpellingOfHidden, way =>
                Assert.False(Rx(way).IsMatch(Normalise(rule)),
                    $"A rule targeting .empty-state hides it with '{way}': {Normalise(rule)}")));
    }

    /// <summary>
    /// The smallest type rung is exactly the size <c>--muted</c> was measured against.
    ///
    /// <para>theme.css holds <c>--muted</c> to a 4.5:1 contrast floor rather than 3:1, and says
    /// why: it carries the explanatory prose at this size. That measurement is a statement
    /// about a number, and rounding the bottom rung of the type scale down to fit a ratio would
    /// invalidate it silently — the colour would still pass its own test, at a size nobody
    /// checked. <b>The floor is the anchor the scale is built from, not a value the scale
    /// happens to produce.</b></para>
    /// </summary>
    [Fact]
    public void TheSmallestTypeRungIsTheSizeTheMutedInkWasMeasuredAt()
    {
        var xs = Rx(@"--text-xs:\s*([0-9.]+)rem").Match(WithoutCssComments(ThemeCss));

        Assert.True(xs.Success, "theme.css does not declare --text-xs in rem.");
        Assert.Equal(0.72, double.Parse(xs.Groups[1].Value, CultureInfo.InvariantCulture), 3);
    }

    /// <summary>
    /// Every value theme.css declares for one custom property, in source order.
    ///
    /// <para>The <b>list</b> rather than the value, because "how many times is this declared" is
    /// the question a <c>Contains</c> cannot answer, and a second declaration lower down is how
    /// three guards in this file were defeated at once. Only the screen half is read: the print
    /// block deliberately restates most of the palette, and a restatement there is the mechanism
    /// rather than a duplicate.</para>
    /// </summary>
    private static List<string> DeclarationsOf(string token)
    {
        var screen = WithoutCssComments(ThemeCss);
        screen = screen[..screen.IndexOf("@media print", StringComparison.Ordinal)];

        return [.. Rx($@"(?<![\w-]){Regex.Escape(token)}\s*:\s*([^;}}]+)")
            .Matches(screen)
            .Select(m => Normalise(m.Groups[1].Value))];
    }

    /// <summary>
    /// The type scale as theme.css declares it: token name to size in rem.
    ///
    /// <para><b>A duplicate throws rather than resolving.</b> That began as an accident of
    /// <c>ToDictionary</c> — and it was the accident protecting the type scale from the shadowing
    /// mutation that got through against the spacing scale, so it is now deliberate and says so.
    /// A token with two declarations has no single answer here, and quietly taking either one is
    /// how a guard reads a value that the browser does not use.</para>
    /// </summary>
    private static Dictionary<string, double> TypeScale
    {
        get
        {
            var found = Rx(@"(--text-[a-z0-9-]+):\s*([0-9.]+)rem")
                .Matches(WithoutCssComments(ThemeCss))
                .Select(m => (Name: m.Groups[1].Value, Rem: double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)))
                .ToList();

            var twice = found.GroupBy(f => f.Name, StringComparer.Ordinal).Where(g => g.Count() > 1).ToList();

            Assert.True(twice.Count == 0,
                $"theme.css declares {string.Join(", ", twice.Select(g => g.Key))} more than once. "
                + "Whichever this helper picked would be a value the cascade might not use.");

            return found.ToDictionary(f => f.Name, f => f.Rem, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Every font size a declaration block sets, in rem, <b>with the type scale resolved</b>.
    ///
    /// <para>The three typographic guards in this file — the Hero Point cost, the rank word and
    /// the trait Source line — assert that a size falls in a band, because the relationship
    /// between two sizes is the whole substance of each rule and <c>Contains("font-size:")</c>
    /// is satisfied by a cost set three times the size of the rank beside it. Each read the
    /// number out of the declaration with a regex, which stopped working the moment the sizes
    /// became tokens.</para>
    ///
    /// <para><b>Resolving is the fix, and loosening the band would have been the bug.</b> The
    /// obvious repair — accept a <c>var()</c> and skip the range check — turns three measured
    /// assertions into three assertions that a property is present, which is the exact weakness
    /// every one of their doc comments records being hardened against. Resolved, they are
    /// stronger than before: they now also catch a scale rung moved to a wrong value, which no
    /// literal read could see.</para>
    ///
    /// <para>An unresolvable token <b>throws</b> rather than being skipped. A <c>var()</c>
    /// naming a token that does not exist makes the whole declaration invalid, so the browser
    /// drops it and the element falls back to inherited size — a real failure that looks like
    /// nothing. Sizes in <c>pt</c> are skipped by design: they belong to the print block, which
    /// has its own floor test, and this reads rem.</para>
    /// </summary>
    private static List<double> FontSizesInRem(string declarations)
    {
        var sizes = new List<double>();

        foreach (Match declaration in Rx(@"font-size:\s*([^;}]+)").Matches(declarations))
        {
            var value = declaration.Groups[1].Value.Trim();

            if (Rx(@"var\(\s*(--[a-z0-9-]+)\s*\)").Match(value) is { Success: true } token)
            {
                var name = token.Groups[1].Value;

                if (!TypeScale.TryGetValue(name, out var rem))
                    throw new InvalidOperationException(
                        $"`font-size: {value}` asks for {name}, which theme.css does not declare "
                        + "as a rem size. An unresolvable var() invalidates the whole "
                        + "declaration and the browser drops it.");

                sizes.Add(rem);
                continue;
            }

            if (Rx(@"^([0-9.]+)rem$").Match(value) is { Success: true } literal)
            {
                sizes.Add(double.Parse(literal.Groups[1].Value, CultureInfo.InvariantCulture));
                continue;
            }

            // Paper, or a keyword like `inherit`. Anything else carrying a number is a unit
            // this helper does not understand, and silently skipping it would make the band
            // assertions above vacuous.
            if (!value.EndsWith("pt", StringComparison.Ordinal) && Rx(@"\d").IsMatch(value))
                throw new InvalidOperationException(
                    $"`font-size: {value}` is a length this helper cannot resolve, so no band "
                    + "assertion can be made about it. Use a --text-* token.");
        }

        return sizes;
    }

    /// <summary>
    /// The stylesheet up to its print block, with the <c>@page</c> box removed. Comments stripped.
    ///
    /// <para><b><c>@page</c> is paper and it is not inside <c>@media print</c>.</b> It sits just
    /// above it, so cutting at the first <c>@media print</c> left <c>@page { margin: 14mm 13mm }</c>
    /// in the region this file calls "the screen half" — and the raw-length guard's own doc claim
    /// that the print rules are out of scope was false for it. It passed only because <c>mm</c>
    /// was missing from the unit list, so closing that gap would have turned a legitimate print
    /// declaration red. Excluded by name, which is the honest fix: the page box is a paper rule
    /// wherever it is written.
    ///
    /// <para><b>The first version of this note cited <c>ThePageIsA4WithMargins</c> as the
    /// compensating check and that was false when written.</b> That test read the *first*
    /// <c>@page</c> while this strips *every* one of them, so a second page box after the A4
    /// block — A5 landscape, margin 0 — won the cascade and was seen by nothing. It reads all of
    /// them and requires exactly one now, which is what makes stripping them here safe. A
    /// pseudo-page such as <c>@page :first</c> is stripped too, and counted there.</para>
    /// </summary>
    /// <summary>
    /// The value of <paramref name="property"/> that actually applies to <paramref name="selector"/>
    /// — the <b>last</b> one declared, across <b>every</b> rule that targets it — or null.
    ///
    /// <para><b>Three separate guards in this file were defeated by one root cause, and the fix
    /// for it was already here twice.</b> A fix-audit reached the same bad end state three
    /// different ways, each time by declaring the thing <em>again</em> rather than by editing the
    /// declaration the guard was reading:</para>
    ///
    /// <list type="bullet">
    ///   <item>a duplicate <c>--space-4: 4rem</c> lower in the same <c>:root</c> block — the
    ///     pinned-value check is a <c>Contains</c>, the shadowed original still satisfied it, and
    ///     the workhorse rung sat at 64px with the whole suite green;</item>
    ///   <item><c>.budget, .breakdown { position: static }</c> added later in the file — the strip
    ///     un-stuck while keeping the top elevation step, because the guard filtered on
    ///     <c>Selector == ".budget"</c> and a comma list is not that string, while its own new
    ///     comment claimed it read every rule targeting <c>.budget</c>;</item>
    ///   <item><c>border-bottom: none</c> instead of deleting the line — the exemption's
    ///     precondition asked whether the property <em>name</em> appeared, which is the same
    ///     "the string is still there" weakness it was written to replace.</item>
    /// </list>
    ///
    /// <para>So the instrument is the cascade rather than a substring: comma lists split,
    /// suffix-matched the way <see cref="RulesTargeting"/> already does, and the last declaration
    /// wins. It does not resolve specificity, and that is deliberately the safe direction — every
    /// one of those three mutations was a later override, which is what this does model.</para>
    /// </summary>
    /// <param name="exact">
    /// When true, only a rule whose selector <b>is</b> <paramref name="selector"/> counts, rather
    /// than any selector ending in it.
    ///
    /// <para>Suffix matching is right for asking "what applies to this element", and wrong for
    /// asking "does this rule still say this". A fix-audit used the difference: with the real
    /// <c>.budget-toggle</c> rule stripped of its <c>border-bottom</c>, adding
    /// <c>.sheet .budget-toggle { border-bottom: … }</c> — a selector that matches nothing in this
    /// app, since no budget strip renders inside a sheet — supplied the precondition and the guard
    /// passed. A source-reading test cannot tell which selectors match real elements, so the
    /// exemption records which selector has to carry its reason instead of accepting any that
    /// mentions it.</para>
    /// </param>
    private static string? EffectiveValue(string css, string selector, string property, bool exact = false)
    {
        var rules = RulesFor(css, selector, exact);

        // **Anything that could change this property but is spelled differently makes the answer
        // unavailable, rather than silently not counting.** This read one spelling per property, and
        // a fix-audit defeated four separate guards through the gap in exactly the same way:
        // `border-bottom-color: transparent` beat a `border-bottom` check, `border-left-width: 0`
        // beat a `border-left` check, `margin-left: 0` beat a `margin` check, and
        // `padding-inline: …` beat a padding check. Every one of them left the guard reading the
        // shorthand it knew and reporting the value it wanted.
        //
        // Modelling the cascade across longhands and logical properties properly is a bigger job
        // than any of these guards needs, so this fails instead — an unreadable answer is a red
        // test, and a red test is the safe direction. If a stylesheet ever legitimately needs one
        // of these spellings, the guard for it gets written then rather than guessed at now.
        // Every declaration that decides this property, in source order — the property itself and
        // any spelling that can override it.
        var deciding = rules
            .SelectMany(r => Rx(@"(?<![\w-])([a-z-]+)\s*:\s*([^;}]+)").Matches(r)
                                 .Select(m => (Property: m.Groups[1].Value, Value: Normalise(m.Groups[2].Value))))
            .Where(d => d.Property == property || CouldOverride(property, d.Property))
            .ToList();

        if (deciding.Count == 0) return null;

        // **Order is what makes this correct rather than merely strict.** `.budget-toggle` writes
        // `border: none` and then `border-bottom: …`, which the cascade resolves the way the author
        // meant — so a check that simply refused any related spelling would fail on correct CSS.
        // What is not readable is a *later* spelling this helper does not model, which is exactly
        // how a fix-audit beat four guards: `border-bottom-color: transparent` after a
        // `border-bottom`, `border-left-width: 0` after a `border-left`, `margin-left: 0` after a
        // `margin`, `padding-inline` instead of the pair. Each left the guard reading a declaration
        // the cascade no longer decides.
        var last = deciding[^1];

        Assert.True(last.Property == property,
            $"A rule targeting `{selector}` sets `{last.Property}: {last.Value}` after the last "
            + $"`{property}`, so it — not `{property}` — is what the cascade resolves. This reader "
            + $"does not model that spelling. Write the intent as `{property}`, or teach "
            + "CouldOverride and this helper to read the other one.");

        return last.Value;
    }

    /// <summary>
    /// A border shorthand actually draws something: a non-zero width, a style that is not
    /// <c>none</c>, and an ink that is not transparent.
    ///
    /// <para><b>Checking for the absence of the word <c>none</c> is not enough, and a fix-audit got
    /// through both edge guards on that.</b> <c>border-left: 0 solid var(--rule)</c> contains no
    /// <c>none</c>, contains the right token, and draws nothing at all — so the empty state lost
    /// its leading edge and the step band could have lost the edge that closes the chrome, with
    /// both guards green. "It mentions a colour" is not "it is visible".</para>
    /// </summary>
    private static void AssertIsAVisibleEdge(string selector, string property, string value)
    {
        Assert.DoesNotContain("none", value, StringComparison.Ordinal);
        Assert.DoesNotContain("transparent", value, StringComparison.Ordinal);

        // A zero width, in any unit or none. `0`, `0px` and `0rem` all draw nothing.
        Assert.False(Rx($@"(?<![.\d])0({CssZeroUnits})?(?![.\d])").IsMatch(value),
            $"`{selector} {{ {property}: {value} }}` has a zero width, so it draws nothing — the "
            + "value names an ink and an edge that is not there.");

        // And it is the hairline token rather than a length chosen here, which is the same rule the
        // rest of the file lives by.
        Assert.Contains("var(--rule", value, StringComparison.Ordinal);
    }

    /// <summary>Units a zero width might carry. A bare <c>0</c> is matched by the optional group.</summary>
    private const string CssZeroUnits = "px|rem|em|pt|mm|cm|in|ex|ch";

    /// <summary>The declaration blocks of every rule targeting a selector, in source order.</summary>
    private static List<string> RulesFor(string css, string selector, bool exact)
    {
        var wanted = Normalise(selector);

        return [.. RulesOf(css)
            .Where(r => r.Selector.Split(',').Select(Normalise)
                         .Any(s => s == wanted || (!exact && s.EndsWith(wanted, StringComparison.Ordinal))))
            .Select(r => r.Declarations)];
    }

    /// <summary>
    /// Whether <paramref name="other"/> can change what <paramref name="property"/> resolves to —
    /// a longhand of it, or its logical-property equivalent.
    ///
    /// <para>The logical family is the half that is easy to forget, and a fix-audit used it twice:
    /// <c>padding-inline</c> and <c>margin-inline</c> set the same two sides as the
    /// <c>-left</c>/<c>-right</c> pair every reader here was written against, under names that share
    /// no prefix with them.</para>
    /// </summary>
    private static bool CouldOverride(string property, string other)
    {
        // A longhand: `border-bottom` is changed by `border-bottom-color`, `padding` by
        // `padding-left`. Also the other way round, since a shorthand resets a longhand.
        if (other.StartsWith($"{property}-", StringComparison.Ordinal)) return true;
        if (property.StartsWith($"{other}-", StringComparison.Ordinal)) return true;

        var logical = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["margin"] = ["margin-inline", "margin-block"],
            ["margin-left"] = ["margin-inline", "margin-inline-start", "margin-inline-end"],
            ["margin-right"] = ["margin-inline", "margin-inline-start", "margin-inline-end"],
            ["padding"] = ["padding-inline", "padding-block"],
            ["padding-left"] = ["padding-inline", "padding-inline-start", "padding-inline-end"],
            ["padding-right"] = ["padding-inline", "padding-inline-start", "padding-inline-end"],
            ["border-bottom"] = ["border-block-end", "border-block", "border"],
            ["border-left"] = ["border-inline-start", "border-inline", "border"],
            ["max-width"] = ["max-inline-size", "inline-size", "width"],
        };

        return logical.TryGetValue(property, out var aliases)
               && aliases.Any(a => a == other || other.StartsWith($"{a}-", StringComparison.Ordinal));
    }

    private static string ScreenHalfOfAppCss
    {
        get
        {
            var css = WithoutCssComments(AppCss);
            var print = css.IndexOf("@media print", StringComparison.Ordinal);
            Assert.True(print > 0, "app.css has no @media print block, so the screen half cannot be bounded.");

            var screen = css[..print];

            // Assert it was there before removing it: an @page block that has moved or been
            // renamed would otherwise be silently un-excluded, which is the stale-exemption
            // shape this file has been caught by twice.
            Assert.Contains("@page", screen, StringComparison.Ordinal);
            return Rx(@"@page\b[^{]*\{[^{}]*\}").Replace(screen, " ");
        }
    }

    /// <summary>
    /// Every <c>selector { declarations }</c> pair in a stylesheet, flattened — a rule inside an
    /// <c>@media</c> query is returned as itself, which is what makes the mobile query's own
    /// paddings visible to the scan above. Comments must already be stripped.
    /// </summary>
    private static List<(string Selector, string Declarations)> RulesOf(string css) =>
        [.. Rx(@"([^{}]+)\{([^{}]*)\}")
            .Matches(css)
            .Select(m => (Selector: Rx(@"\s+").Replace(m.Groups[1].Value.Trim(), " "),
                          Declarations: m.Groups[2].Value))];

    // ── No component names a typeface ───────────────────────────────────────────

    /// <summary>
    /// The same rule as colour, radius and duration, for the same reason: theme.css is the one
    /// file allowed to name a face, and everything else asks it for one by token. That is what
    /// makes changing the house style an edit in one place — and it is what will make dropping
    /// in two self-hosted files a change to two token values rather than to forty rules.
    ///
    /// <para><b>Both spellings are checked.</b> <c>font-family</c> is the obvious one;
    /// <c>font</c> is the shorthand, and it carries a family too. <c>font: inherit</c> is all
    /// over app.css and is not naming anything — what a shorthand may not do is carry a quoted
    /// family or a generic family keyword.</para>
    /// </summary>
    [Theory]
    [InlineData("app.css")]
    [InlineData("razor")]
    [InlineData("index.html")]
    public void NoComponentNamesATypeface(string what)
    {
        List<(string, string)> sources = what switch
        {
            "app.css" => [("app.css", WithoutCssComments(AppCss))],
            "index.html" => [("index.html", WithoutCssComments(IndexHtml))],
            _ => RazorFiles.Select(f => (Path.GetFileName(f), Scannable(File.ReadAllText(f), css: false))).ToList()
        };

        var family = Rx(@"font-family\s*:\s*([^;}]+)");
        var shorthand = Rx(@"(?<![\w-])font\s*:\s*([^;}]+)");
        var token = Rx(@"^var\(--font-[a-z-]+\)$");
        var generic = Rx(@"\b(serif|sans-serif|monospace|cursive|fantasy|system-ui|ui-[a-z-]+)\b",
            RegexOptions.IgnoreCase);

        foreach (var (name, text) in sources)
        {
            foreach (Match declaration in family.Matches(text))
            {
                var value = declaration.Groups[1].Value.Trim();
                Assert.True(token.IsMatch(value),
                    $"{name} sets font-family to '{value}'. Ask theme.css for --font-display or "
                    + "--font-body instead.");
            }

            foreach (Match declaration in shorthand.Matches(text))
            {
                var value = declaration.Groups[1].Value.Trim();
                Assert.True(
                    !value.Contains('"') && !value.Contains('\'') && !generic.IsMatch(value),
                    $"{name} names a typeface in a font shorthand: '{value}'.");
            }
        }
    }

    /// <summary>
    /// No script sets a theme token either.
    ///
    /// <para>The colour and typeface rules read stylesheets, and a stylesheet is not the only
    /// way a token gets a value: <c>documentElement.style.setProperty("--heading", …)</c> wins
    /// over every rule in theme.css, is invisible to both scans, and is not something the
    /// Content-Security-Policy has any opinion about. The one thing the app's scripts may do to
    /// the document element is set <c>data-mode</c>, which is the palette switch.</para>
    /// </summary>
    [Fact]
    public void NoScriptSetsAThemeTokenOrNamesAFace()
    {
        var token = Rx(@"setProperty\s*\(\s*[""']--", RegexOptions.IgnoreCase);
        var face = Rx(@"font-family|--font-", RegexOptions.IgnoreCase);
        var colour = Rx(@"#[0-9A-Fa-f]{3,8}\b|\b(rgba?|hsla?|oklch)\s*\(", RegexOptions.IgnoreCase);

        Assert.NotEmpty(Scripts);

        foreach (var (name, text) in Scripts)
        {
            var body = Rx(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline).Replace(text, " ");

            Assert.False(token.IsMatch(body),
                $"{name} sets a custom property directly, which beats every rule in theme.css "
                + "and no stylesheet scan can see. Set data-mode and let the cascade do it.");
            Assert.False(face.IsMatch(body), $"{name} names a typeface.");
            Assert.False(colour.IsMatch(body), $"{name} names a colour.");
        }
    }

    /// <summary>
    /// Two faces, and they have to be two. A condensed display face for every heading, label
    /// and figure, and a separate face for running prose — which is the single biggest visual
    /// difference between this app and the one it was measured against, where levels were
    /// separated by size and weight in one face and read as a document rather than a page.
    ///
    /// <para><b>Both halves are asserted, and the first without the second is theatre.</b> Two
    /// tokens declared to the same stack satisfy any check that they exist and are used, and
    /// leave the app looking exactly as it did; and a display token nothing asks for is a
    /// token, not a typeface.</para>
    /// </summary>
    [Fact]
    public void TheTwoFacesAreTokensAndDoDifferentJobs()
    {
        var theme = WithoutCssComments(ThemeCss);

        var body = Rx(@"--font-body\s*:\s*([^;]+);").Match(theme);
        var display = Rx(@"--font-display\s*:\s*([^;]+);").Match(theme);

        Assert.True(body.Success, "theme.css declares no --font-body.");
        Assert.True(display.Success, "theme.css declares no --font-display.");
        Assert.NotEqual(Normalise(body.Groups[1].Value), Normalise(display.Groups[1].Value));

        var css = WithoutCssComments(AppCss);
        Assert.Contains("var(--font-display)", css, StringComparison.Ordinal);
        Assert.Contains("var(--font-body)", css, StringComparison.Ordinal);

        // The headings are the job the display face exists for, and they are set together in
        // one rule. If that rule stops asking for the face, every heading in the app silently
        // goes back to the body stack while both tokens are still declared and still used.
        var headings = Rx(@"h1,\s*h2,\s*h3,\s*h4\s*\{([^{}]*)\}").Match(css);
        Assert.True(headings.Success, "app.css no longer sets h1–h4 together.");
        Assert.Contains("font-family:var(--font-display)", Normalise(headings.Groups[1].Value),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Every self-hosted face resolves to a file that is actually there, and carries the
    /// licence that lets it be redistributed.
    ///
    /// <para><b>A missing font file fails silently and looks like a design decision.</b> The
    /// stacks name system fallbacks after each self-hosted family — deliberately, so a failed
    /// load still leaves a readable page — which means renaming a file, or dropping it from
    /// the publish, degrades the whole app to the system stack with every other test in this
    /// file green. Nothing but the bytes on disk can catch that.</para>
    ///
    /// <para><b>The licence is asserted beside the font because shipping it is a condition of
    /// the SIL Open Font License, not a courtesy.</b> These files are redistributed by every
    /// deploy and by every fork of this repository.</para>
    /// </summary>
    [Fact]
    public void EverySelfHostedFaceIsPresentAndCarriesItsLicence()
    {
        var fonts = Path.Combine(WebRoot, "wwwroot", "fonts");

        Assert.NotEmpty(FontFaces());

        foreach (var (family, file) in FontFaces())
        {
            var path = Path.Combine(fonts, file);

            Assert.True(File.Exists(path),
                $"theme.css asks for {file}, which is not in wwwroot/fonts. The page falls back "
                + "to the system stack and nothing else notices.");

            // A file that exists and is empty loads as a broken font, which fails the same way.
            Assert.True(new FileInfo(path).Length > 1024, $"{file} is empty.");
        }

        // One licence per family, not one licence in the folder: two families ship here and a
        // single OFL.txt would cover whichever of them somebody assumed.
        foreach (var family in FontFaces().Select(f => f.Family).Distinct(StringComparer.Ordinal))
        {
            var licence = Path.Combine(fonts, $"{family}-OFL.txt");

            Assert.True(File.Exists(licence),
                $"{family} ships without the Open Font License text that permits redistributing it.");

            // **The text, not the file.** This is the one guard here whose green carries a legal
            // claim — these files are redistributed by every deploy and every fork — and a
            // 23-byte stub saying "Oswald is a nice font." satisfied `File.Exists`. The reserved
            // font name is asserted too, so a licence cannot be copied from the other family.
            var text = File.ReadAllText(licence);

            Assert.Contains("SIL OPEN FONT LICENSE", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Copyright", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(family, text.Replace(" ", "", StringComparison.Ordinal),
                StringComparison.OrdinalIgnoreCase);

            // **The operative parts, not the header.** All three assertions above appear in the
            // first nine lines of an OFL file, so `head -9` — 383 bytes, with the permission
            // grant, all five conditions and the warranty disclaimer deleted — satisfied them.
            // What makes this file a licence rather than a title page is what follows.
            Assert.Contains("PERMISSION IS HEREBY GRANTED", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WITHOUT WARRANTY", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CONDITIONS", text, StringComparison.OrdinalIgnoreCase);

            Assert.True(new FileInfo(licence).Length > 3000,
                $"{family}'s licence is {new FileInfo(licence).Length} bytes; the OFL is ~4.5 KB. "
                + "A truncated licence is not the licence.");
        }
    }

    /// <summary>
    /// A family may only be served the file that carries it.
    ///
    /// <para><b>Nothing else correlates the two, and without this the redesign's headline item
    /// silently reverts.</b> Pointing Oswald's <c>src</c> at the Public Sans file leaves every
    /// heading, label, figure and section bar rendering in the body face — with the two tokens
    /// still declared, still different, still both asked for, and every file still present and
    /// licensed. Four font guards stay green while the app looks exactly as it did before the
    /// slice. Swapping the two <c>src</c> lines is the same hole and sets the prose in a
    /// condensed display face.</para>
    /// </summary>
    [Fact]
    public void EachFamilyIsServedItsOwnFile()
    {
        var faces = FontFaces();

        Assert.NotEmpty(faces);

        foreach (var (family, file) in faces)
        {
            // The name, which catches pointing one family's rule at the other's file...
            Assert.StartsWith(family, file, StringComparison.OrdinalIgnoreCase);

            // ...and the bytes, which catch the same regression done by overwriting the file
            // instead of by renaming it. A name check correlates a string; nothing in it reads
            // the font. A TrueType `name` table stores its family in UTF-16BE, so the family
            // appears in the file with a null byte between every character — a cheap proxy for
            // parsing the table, and enough to tell Oswald's bytes from Public Sans'.
            var utf16 = string.Concat(family.Select(c => $"\0{c}"));
            var bytes = File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "fonts", file),
                System.Text.Encoding.Latin1);

            Assert.True(bytes.Contains(utf16, StringComparison.Ordinal),
                $"{file} does not name itself {family} internally, so the file serving this "
                + "family is some other font under its name.");
        }
    }

    /// <summary>
    /// Every <c>@font-face</c> in theme.css as (family with spaces stripped, file name). The
    /// family is stripped because a file name cannot carry a space: "Public Sans" ships as
    /// <c>PublicSans-Variable.ttf</c>.
    ///
    /// <para><b>A face whose <c>src</c> this cannot read is a failure, not a face to skip.</b>
    /// An earlier version filtered those out — so writing <c>url("/fonts/PublicSans-Variable.ttf")</c>
    /// with an absolute path dropped the Oswald face from this list *and* from the licence
    /// guard, while <c>&lt;base href="/"&gt;</c> made the path perfectly live. The app served
    /// Public Sans for every heading with both guards green. The filter meant to make this
    /// robust widened the hole it was closing.</para>
    /// </summary>
    private static List<(string Family, string File)> FontFaces()
    {
        var faces = new List<(string, string)>();

        foreach (Match face in Rx(@"@font-face\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline)
                     .Matches(WithoutCssComments(ThemeCss)))
        {
            var body = face.Groups["body"].Value;

            var family = Rx(@"font-family:\s*""([^""]+)""").Match(body);
            Assert.True(family.Success, $"An @font-face names no family: {Normalise(body)}");

            var url = Rx(@"url\(""([^""]+)""\)").Match(body);
            Assert.True(url.Success, $"An @font-face has no url(): {Normalise(body)}");

            // Relative to the stylesheet, and only from the one folder that ships fonts. An
            // absolute path or a second location is how a face escapes every check below.
            var reference = Rx(@"^\.\./fonts/(?<file>[^/]+)$").Match(url.Groups[1].Value);
            Assert.True(reference.Success,
                $"An @font-face loads from '{url.Groups[1].Value}'. Fonts are served from "
                + "../fonts/ so that every one of them is checked; a path this test cannot read "
                + "is a face that silently escapes it.");

            faces.Add((
                family.Groups[1].Value.Replace(" ", "", StringComparison.Ordinal),
                reference.Groups["file"].Value));
        }

        return faces;
    }

    /// <summary>
    /// The self-hosted family leads each stack. A file that is downloaded, served and then
    /// listed behind the system face is paid for on every visit and never seen.
    /// </summary>
    [Theory]
    [InlineData("--font-display")]
    [InlineData("--font-body")]
    public void TheSelfHostedFaceIsTheFirstOneAskedFor(string token)
    {
        var theme = WithoutCssComments(ThemeCss);

        var declared = Rx($@"{token}\s*:\s*([^;]+);").Match(theme);
        Assert.True(declared.Success, $"theme.css declares no {token}.");

        var first = Normalise(declared.Groups[1].Value).Split(',')[0].Trim('"');

        var hosted = Rx(@"@font-face\s*\{[^}]*?font-family:\s*""([^""]+)""", RegexOptions.Singleline)
            .Matches(theme)
            .Select(m => Normalise(m.Groups[1].Value))
            .ToList();

        Assert.Contains(first, hosted, StringComparer.Ordinal);
    }

    /// <summary>
    /// The rulebook's word for a rank is set behind the rank it glosses, not level with it.
    /// The number is the fact a player rolls; the word is Ch.2's name for it, and in the same
    /// size and ink the two compete. Nothing in the markup can carry this — the element is
    /// there either way — which is why it is asserted against the rule.
    /// </summary>
    [Fact]
    public void TheRankWordIsSetBehindTheRankItGlosses()
    {
        var rule = Rx(@"(?<![\w.-])\.rank-word\s*\{([^{}]*)\}").Match(WithoutCssComments(AppCss));

        Assert.True(rule.Success,
            "app.css has no .rank-word rule, so the rulebook's word is set like the rank.");

        var declarations = Normalise(rule.Groups[1].Value);

        Assert.Contains("color:var(--muted)", declarations, StringComparison.Ordinal);
        Assert.Contains("text-transform:uppercase", declarations, StringComparison.Ordinal);
        Assert.Contains("font-family:var(--font-display)", declarations, StringComparison.Ordinal);

        // A step behind the rank beside it. The band, not the presence of a size:
        // `Contains("font-size:")` is satisfied by 2rem, which would put the gloss in front of
        // the figure. The scale is resolved, so a rung moved to a wrong value fails here too.
        var sizes = FontSizesInRem(declarations);
        Assert.NotEmpty(sizes);
        Assert.All(sizes, rem => Assert.InRange(rem, 0.6, 0.9));

        // **And strictly smaller than the rank it glosses — which is `.stepper .value`, and the
        // first version of this named the wrong element.** It compared against `.trait-table td`,
        // the rank on the printed sheet; but `.rank-word` is emitted by `RankRow.razor` and
        // `.trait-table` by `SheetView.razor`, so **the two never appear on the same surface** and
        // the stated claim was about a pair that cannot be seen together. The comparison passed
        // only by being conservative: 0.82 is tighter than the 1.15 it should have been read
        // against, so the failure it was written for was still reachable from the other side —
        // shrinking `.stepper .value` to `--text-xs` put the gloss exactly level with the figure
        // it sits behind, on the only surface where they co-occur, with the guard green.
        //
        // Before Phase 0 the two were unrelated literals in two rules and this could not be
        // expressed at all. Both are rungs of one resolvable scale now, so it is asserted.
        var rank = RulesOf(ScreenHalfOfAppCss)
            .Where(r => r.Selector is ".stepper .value")
            .SelectMany(r => FontSizesInRem(Normalise(r.Declarations)))
            .ToList();

        Assert.NotEmpty(rank);
        Assert.True(sizes.Max() < rank.Min(),
            $"The rank word is set at {sizes.Max()}rem and the rank it glosses — the stepper's own "
            + $"value, which is the figure it renders beside — at {rank.Min()}rem. The word is a "
            + "gloss on the number and has to read as a step behind it, not level with it.");

        // The two really do render together, which is what makes the comparison meaningful. This
        // is the assertion whose absence let the guard name an element on another page.
        var rankRow = File.ReadAllText(Path.Combine(WebRoot, "Components", "RankRow.razor"));
        Assert.Contains("rank-word", rankRow, StringComparison.Ordinal);
        Assert.Contains("stepper", rankRow, StringComparison.Ordinal);

        // **And it is shown at all.** Everything above is satisfied by an element that is
        // present and hidden — a hidden element still has its class and still has its text, so
        // every rendering test in the bUnit project passes while the rulebook's word simply
        // stops appearing. Checked over every rule that targets the class, not the base one,
        // since a more specific rule further down wins the cascade.
        //
        // **Every spelling of hidden, not one.** Banning `display: none` alone left
        // `visibility: hidden`, `color: transparent` and `font-size: 0` — three ways to the
        // identical result, all green.
        // Matched as whole declarations, not as substrings: `font-size:0` is a prefix of the
        // rule's own `font-size:0.72rem`, and `opacity:0` of `opacity:0.8`. A ban that fires on
        // the thing it is protecting is worse than no ban, because the fix is to weaken it.
        Assert.All(RulesTargeting(".rank-word"), rule =>
            Assert.All(EverySpellingOfHidden, way =>
                Assert.False(Rx(way).IsMatch(Normalise(rule)),
                    $"A rule targeting .rank-word hides it with '{way}': {Normalise(rule)}")));
    }

    // ── The UI is written for players ───────────────────────────────────────────

    /// <summary>
    /// Setting something in monospace on a page for players is, every time so far, an
    /// internal name. Matched on the tag rather than on <c>&lt;code&gt;</c> exactly, because
    /// an attribute on it changes nothing about what the reader sees.
    /// </summary>
    [Fact]
    public void NoPageSetsAnythingInMonospaceForThePlayer()
    {
        var monospace = Rx(@"<(code|kbd|samp|pre)\b", RegexOptions.IgnoreCase);

        Assert.All(RazorFiles, f =>
            Assert.False(monospace.IsMatch(VisibleMarkup(File.ReadAllText(f))),
                $"{Path.GetFileName(f)} sets text in monospace on the page."));
    }

    /// <summary>
    /// The general form of the rule, rather than a denylist of the four offenders that
    /// prompted it: no type this project declares may appear in the prose a player reads.
    ///
    /// <para>Only compound PascalCase names are considered — <c>CharacterSheetRenderer</c>,
    /// not a hypothetical <c>Tier</c> — because a single capitalised word is also English,
    /// and the app has every right to write "Power" or "Source" on the page.</para>
    /// </summary>
    [Fact]
    public void NoPageNamesATypeThisProjectDeclares()
    {
        var declared = ProjectsWithTypes
            .SelectMany(dir => SourceFiles(Path.Combine(RulesFixture.RepoRoot, dir), "*.cs"))
            .SelectMany(f => Rx(@"\b(?:class|record|interface|enum|struct)\s+([A-Z]\w+)")
                .Matches(File.ReadAllText(f))
                .Select(m => m.Groups[1].Value))
            .Where(n => Rx("[A-Z][a-z]+[A-Z]").IsMatch(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(declared);   // a broken scan must not pass by finding nothing

        foreach (var file in RazorFiles)
        {
            var prose = VisibleText(File.ReadAllText(file));

            foreach (var type in declared)
            {
                Assert.False(
                    Rx($@"\b{Regex.Escape(type)}\b").IsMatch(prose),
                    $"{Path.GetFileName(file)} prints the type name '{type}' at the player. "
                    + "Keep it in a @* *@ comment or the @code block.");
            }
        }
    }

    /// <summary>
    /// Phrases that could only ever be prose. A player has no use for how the app is built
    /// or what it is built with.
    /// </summary>
    [Theory]
    [InlineData("terminal wizard")]
    [InlineData("dotnet")]
    [InlineData("byte-for-byte")]
    [InlineData("sheets layer")]
    [InlineData("the same compiled code")]
    [InlineData("shared engine")]
    [InlineData("blazor")]
    [InlineData("webassembly")]
    [InlineData("command line")]
    public void NoPageExplainsItselfToADeveloper(string phrase) =>
        Assert.All(RazorFiles, f =>
            Assert.False(
                VisibleText(File.ReadAllText(f)).Contains(phrase, StringComparison.OrdinalIgnoreCase),
                $"{Path.GetFileName(f)} says \"{phrase}\" to the player. Keep it in a comment."));

    /// <summary>
    /// The other half of the same rule, and the reason it is not simply "delete the jargon":
    /// the rulebook references are the app's best feature.
    ///
    /// <para>Asserted on the prose a player reads, not on the raw file. Read raw, this test
    /// passed while <c>Ch.6</c> survived <b>only</b> in a Razor comment — the label on the
    /// gear-features picker had lost its page reference and nothing noticed.</para>
    /// </summary>
    [Theory]
    [InlineData("Ch.6")]
    [InlineData("Ch.9")]
    [InlineData("Trait Cap")]
    [InlineData("Hero Point")]
    public void RulebookReferencesAreStillOnThePage(string reference)
    {
        var prose = string.Concat(RazorFiles.Select(f => VisibleText(File.ReadAllText(f))));

        Assert.Contains(reference, prose, StringComparison.Ordinal);
    }

    // ── Repeated markup stays behind its component ──────────────────────────────

    private static readonly (string Class, string Owner)[] OwnedClasses =
    [
        ("panel", "Panel.razor"),
        ("panel-head", "Panel.razor"),
        ("field", "Field.razor"),
        ("sheet-section", "SheetSection.razor"),
        ("stat-block", "StatBlock.razor"),
        ("stat-blocks", "StatBlockRow.razor"),
        ("chosen", "ChosenList.razor"),
        ("options", "OptionList.razor"),
        ("option", "OptionRow.razor"),

        // What a list says when it holds nothing. Owned for the usual reason and one of its
        // own: six copies of this treatment is six chances for an empty state to go back to
        // being a full stop, and the guard that refuses that needs one element to find.
        ("empty-state", "EmptyState.razor"),

        // The blank ruled lines. SheetView hand-wrote three of these — two in the masthead
        // and one inside a MarkupString that also hand-rolled its own HtmlEncode, which is a
        // raw-HTML sink in the file whose whole argument is that markup lives in components.
        ("ruled", "RuledLines.razor"),
        ("rule-line", "RuledLines.razor"),

        // A recorded turn and the engine's answer about the character in it. They are owned
        // for the reason the rest are, plus one of this surface's own: the attribution above
        // a turn is half of what keeps a recording from reading as a live conversation, so
        // it may not be something a second page can write without it.
        ("replay-turn", "ReplayTurn.razor"),
        ("replay-who", "ReplayTurn.razor"),
        ("replay-figures", "ReplayVerdict.razor")
    ];

    public static TheoryData<string, string> Owned()
    {
        var data = new TheoryData<string, string>();
        foreach (var (cssClass, owner) in OwnedClasses) data.Add(cssClass, owner);
        return data;
    }

    /// <summary>
    /// Each of these was hand-written between five and twenty-two times before it had a
    /// component. The print stylesheet is what made that expensive: ruled boxes and
    /// break-inside rules had to reach every one of them at once.
    ///
    /// <para>The class is matched as a member of the attribute's value, not as its prefix.
    /// Anchored at <c>class="</c>, the check waved through <c>class="wrapper panel"</c> —
    /// a hand-rolled panel with a word in front of it.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Owned))]
    public void OnlyOneComponentWritesEachRepeatedClass(string cssClass, string owner)
    {
        var attribute = Rx("""class\s*=\s*(?<q>["'])(?<v>[^"']*)\k<q>""");

        foreach (var file in RazorFiles.Where(f => Path.GetFileName(f) != owner))
        {
            var offending = attribute.Matches(File.ReadAllText(file))
                .Select(m => m.Groups["v"].Value)
                .FirstOrDefault(v => v.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Contains(cssClass, StringComparer.Ordinal));

            Assert.True(offending is null,
                $"{Path.GetFileName(file)} writes class=\"{offending}\" itself, which includes "
                + $"\"{cssClass}\". Use <{Path.GetFileNameWithoutExtension(owner)}>.");
        }
    }

    /// <summary>
    /// The other half, without which the exemption above is decorative: an owner that stops
    /// writing its class would satisfy the test by writing nothing at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(Owned))]
    public void EachOwnerActuallyWritesTheClassItOwns(string cssClass, string owner)
    {
        var source = File.ReadAllText(Path.Combine(WebRoot, "Components", owner));

        Assert.Contains($"\"{cssClass}", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// The budget bar's fill width is a live number and has to arrive as an inline style —
    /// it is the reason the Content-Security-Policy carries <c>style-src 'unsafe-inline'</c>.
    /// It is the only one, and that is worth keeping true.
    /// </summary>
    [Fact]
    public void TheOnlyInlineStyleIsTheBudgetBarsLiveWidth()
    {
        // Either quote: Razor accepts both, and checking only the double-quoted form lets
        // style='…' through, which is the same concession spelled differently.
        var inline = Rx("""(?<![-\w])style\s*=\s*["'@]""", RegexOptions.IgnoreCase);

        var offenders = RazorFiles
            .Where(f => inline.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["HpBudgetBar.razor"], offenders);
    }

    // ── The printed sheet ───────────────────────────────────────────────────────

    /// <summary>
    /// One print block, so that reading it is reading all of it. A second
    /// <c>@media print</c> appended to the file undid every rule below and nothing noticed,
    /// because the helper stopped at the first.
    /// </summary>
    [Fact]
    public void ThereIsExactlyOnePrintBlockInEachStylesheet()
    {
        Assert.Single(Rx(@"@media\s+print\b").Matches(WithoutCssComments(AppCss)));
        Assert.Single(Rx(@"@media\s+print\b").Matches(WithoutCssComments(ThemeCss)));
    }

    /// <summary>The paper, stated in an actual <c>@page</c> rule rather than anywhere at all.</summary>
    [Fact]
    public void ThePageIsA4WithMargins()
    {
        // **Exactly one page box, and every one of them read.** This used `Match`, i.e. the first
        // only — and the raw-length guard had meanwhile started stripping *every* `@page` block
        // out of the region it scans, citing this test as the compensating check. It was not one:
        // a second `@page { size: A5 landscape; margin: 0 }` placed after the A4 block wins the
        // cascade, prints the sheet A5 landscape with no margins, and was seen by nothing at all.
        //
        // `ThereIsExactlyOnePrintBlockInEachStylesheet` exists in this file for precisely this
        // failure one at-rule over; the page box had no equivalent. A pseudo-page — `@page :first`
        // — counts as another one here rather than being tolerated, because it can restate the
        // size and the margin just as completely.
        var pages = Rx(@"@page\b([^{]*)\{([^}]*)\}").Matches(WithoutCssComments(AppCss));

        Assert.True(pages.Count > 0, "app.css has no @page rule, so print uses whatever the browser guesses.");
        Assert.True(pages.Count == 1,
            $"app.css has {pages.Count} @page rules ({string.Join(" / ", pages.Select(p => $"@page{p.Groups[1].Value.Trim()}"))}). "
            + "A later one overrides the size and the margins, and the length guard strips them all "
            + "from its own scan — so only the first is checked anywhere. Keep one.");

        var body = pages[0].Groups[2].Value;
        Assert.Contains("size:A4", Normalise(body), StringComparison.Ordinal);
        Assert.Matches(@"margin:\s*[\d.]+mm", body);
    }

    /// <summary>
    /// Nothing a reader follows down the page may be cut in half. Asserted <b>inside its own
    /// rule</b>: a free-floating search for "break-inside: avoid" anywhere in the print block
    /// was satisfied by an unrelated rule while every one of these was flipped to
    /// <c>auto</c>.
    /// </summary>
    [Theory]
    [InlineData(".power-entry")]
    [InlineData(".stat-block")]
    [InlineData(".stat-table tr")]
    [InlineData(".chosen > li")]
    [InlineData(".sheet-section")]
    public void NoUnitOfTheSheetMayBeSplitAcrossAPage(string selector)
    {
        var rule = PrintRuleFor(selector);

        Assert.True(rule is not null, $"The print block has no rule for {selector}.");
        Assert.Contains("break-inside:avoid", Normalise(rule!), StringComparison.Ordinal);
    }

    /// <summary>
    /// The two boxes that can be taller than a page opt back out, and they have to.
    ///
    /// <para><c>break-inside: avoid</c> is a request Chrome honours by moving the whole box to
    /// the next page first. On a box that cannot fit on any page that means the previous page
    /// is abandoned — a fifteen-Power character left two thirds of page one white. The Powers
    /// group is one such box; the <c>fill</c> boxes are the other, because they stretch to the
    /// height of the tallest column and the Powers column is unbounded.</para>
    /// </summary>
    [Theory]
    [InlineData(".sheet-section.powers")]
    [InlineData(".sheet-section.fill")]
    public void TheBoxesThatCanExceedAPageAreAllowedToBreak(string selector)
    {
        var rule = PrintRuleFor(selector);

        Assert.True(rule is not null, $"The print block does not let {selector} break.");
        Assert.Contains("break-inside:auto", Normalise(rule!), StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing on the printed sheet is set below 7pt. Under that, small caps at a letter-space
    /// stop being something a person reads and become a texture — and everything the sheet
    /// carries, it carries because somebody at a table needs it.
    ///
    /// <para>This is measured rather than eyeballed because the one place it went wrong was a
    /// deliberate choice: Hero Point costs were set at 6.8pt to push them behind the ranks,
    /// which the case and the ink already do.</para>
    ///
    /// <para><b>Every size on paper is read, not only the ones already in points.</b> The
    /// pattern was <c>font-size:\s*([\d.]+)pt</c>, which does not match a size in any other
    /// unit — so setting the two densest blocks on the sheet, the stat tables and the Power
    /// stat lines, to <c>0.3rem</c> and <c>4px</c> left this green, because the sizes it did
    /// find were all still above the floor and <c>Assert.NotEmpty</c> was satisfied by them.
    /// A print size in <c>rem</c> is also wrong on its own terms: it is relative to a root
    /// size the print block resets, so it says nothing about what comes out of the printer.
    /// </para>
    /// </summary>
    [Fact]
    public void NothingOnPaperIsSetBelowSevenPoint()
    {
        var sizes = Rx(@"font-size:\s*([^;}]+)")
            .Matches(OnlyPrintBlockOf(AppCss))
            .Select(m => (Text: Normalise(m.Value), Value: m.Groups[1].Value.Trim()))
            .ToList();

        Assert.NotEmpty(sizes);

        Assert.All(sizes, s =>
        {
            var points = Rx(@"^([\d.]+)pt$").Match(s.Value);

            Assert.True(points.Success,
                $"{s.Text} is not set in points, so nothing here can say how big it prints.");

            Assert.True(
                double.Parse(points.Groups[1].Value, CultureInfo.InvariantCulture) >= 7,
                $"{s.Text} is too small to read on paper.");
        });

        // **And no `font` shorthand**, which sets a size without ever writing `font-size` —
        // `font: 400 4pt/1.1 inherit` on the stat tables puts the densest block of the sheet at
        // 4pt and every check above still passes. Refused outright rather than parsed: the
        // shorthand also silently resets four other properties, so a print block that sets
        // sizes one property at a time is the right rule anyway.
        Assert.DoesNotMatch(Rx(@"(?<![\w-])font:"), Normalise(OnlyPrintBlockOf(AppCss)));
    }

    /// <summary>
    /// The heading bars are the sheet's one run of colour on paper, and a browser drops print
    /// backgrounds unless a page asks for them. Without the ask, every bar prints white and
    /// the sheet comes out looking half-styled — which is the failure the rule's own comment
    /// describes, and which nothing asserted: <c>exact</c> could be changed to <c>economy</c>
    /// with the suite green.
    ///
    /// <para>Both spellings, because the unprefixed property is not what Chrome reads.</para>
    /// </summary>
    [Fact]
    public void TheHeadingBarsAskToBePrintedRatherThanDroppedAsBackgrounds()
    {
        var rule = PrintRuleFor(".sheet-section > h3");

        Assert.True(rule is not null, "The print block does not style the heading bars at all.");

        var declarations = Normalise(rule!);

        // The tint is what there is to print. Asserted here rather than left implied: a rule
        // that asks for its background to be printed and then has none is a no-op that reads
        // like a guarantee.
        Assert.Contains("background:var(--accent-soft)", declarations, StringComparison.Ordinal);
        Assert.Contains("print-color-adjust:exact", declarations, StringComparison.Ordinal);
        Assert.Contains("-webkit-print-color-adjust:exact", declarations, StringComparison.Ordinal);

        // **And nothing later in the block may take it back.** Reading one rule by its exact
        // selector is defeated by a second, more specific one — `.sheet .sheet-section h3 {
        // background: none; print-color-adjust: economy }` prints every bar white and this
        // test never sees it, because `PrintRuleFor` matches the selector string it was given.
        // So: no value of the property other than `exact`, anywhere on paper.
        var print = OnlyPrintBlockOf(AppCss);

        Assert.All(
            Rx(@"print-color-adjust:\s*([^;}]+)").Matches(print).Select(m => m.Groups[1].Value.Trim()),
            value => Assert.Equal("exact", value));

        // And no heading rule on paper may drop the tint it is asking to have printed.
        Assert.All(
            Rx(@"([^{}]+)\{([^{}]*)\}").Matches(print)
                .Where(r => r.Groups[1].Value.Contains("h3", StringComparison.Ordinal))
                .Select(r => Normalise(r.Groups[2].Value)),
            r => Assert.DoesNotMatch(Rx(@"background(-color)?:(none|transparent|#fff|white)"), r));
    }

    /// <summary>
    /// Nothing on paper scales the sheet as a whole. The 7pt floor reads declared sizes, and a
    /// declared size is only what comes out of the printer if nothing shrinks the page under
    /// it — <c>zoom: 0.55</c> on the printed sheet leaves every declaration at or above 7pt
    /// while the 7.5pt stat lines arrive at about 4pt.
    ///
    /// <para>"Make it fit on one page" is the most likely reason anybody edits this block, and
    /// the sheet being one page is a stated goal, so this is a change somebody would make in
    /// good faith. The one-page property is held by the <c>fill</c> boxes absorbing the
    /// difference, not by shrinking the type.</para>
    /// </summary>
    [Fact]
    public void ThePrintedSheetIsNotScaledDownUnderItsOwnTypeSizes()
    {
        var print = Normalise(OnlyPrintBlockOf(AppCss));

        Assert.DoesNotContain("zoom:", print, StringComparison.Ordinal);
        Assert.DoesNotMatch(Rx(@"transform:[^;}]*scale\("), print);
    }

    /// <summary>
    /// A Hero Point cost is set apart from a rank, and the separation is entirely typographic
    /// — so it is entirely in the stylesheet, where no test that reads rendered markup can
    /// see it. Emptying this rule leaves costs in the same size, weight and ink as the numbers
    /// a player rolls, and the sheet goes back to reading as a receipt.
    ///
    /// <para><b>The values, not the properties.</b> <c>Contains("font-size:")</c> is satisfied
    /// by <c>2.4rem</c> at weight 800 — a cost set three times the size of the rank it sits
    /// beside, which is the opposite of what this rule is for and exactly the weakness the
    /// neighbouring <see cref="ATraitSourceLineIsSetApartFromThePowersBelowIt"/> was hardened
    /// against and this one was not.</para>
    /// </summary>
    /// <para><b>Every rule that targets <c>.hp</c>, not the first one found.</b> Reading only
    /// the first is defeated without touching it: a more specific rule six lines below —
    /// <c>.power-entry .head .hp</c>, which is already in the file — wins the cascade and was
    /// never read, so every Hero Point cost on the Powers stack could go to 2.4rem at weight
    /// 800 in heading ink with the suite green. <c>CLAUDE.md</c> records this trap twice, on
    /// <c>.ruled</c> and on gear alignment; it applies to a test as much as to a stylesheet.
    /// </para>
    /// </summary>
    [Fact]
    public void AHeroPointCostIsSetApartFromTheNumbersAPlayerRolls()
    {
        var rules = RulesTargeting(".hp");

        Assert.NotEmpty(rules);

        // The base rule carries the intent; the rest may not undo it.
        var declarations = Normalise(
            Rx(@"(?<![\w.-])\.hp\s*\{([^{}]*)\}").Match(WithoutCssComments(AppCss)) is { Success: true } m
                ? m.Groups[1].Value
                : throw new InvalidOperationException(
                    "app.css has no .hp rule, so costs are set like everything else."));

        Assert.Contains("letter-spacing:", declarations, StringComparison.Ordinal);
        Assert.Contains("color:var(--muted)", declarations, StringComparison.Ordinal);
        Assert.Contains("text-transform:uppercase", declarations, StringComparison.Ordinal);

        var weight = Rx(@"font-weight:(\d+)").Match(declarations);
        Assert.True(weight.Success, "The .hp rule sets no font weight.");

        Assert.NotEmpty(FontSizesInRem(declarations));

        // Every declaration of each property in every one of those rules, not the first found.
        // Two `font-size` declarations in one block is the same shadowing trick at a smaller
        // scale, and the later one wins.
        static IEnumerable<string> Values(string rule, string pattern) =>
            Rx(pattern).Matches(rule).Select(m => m.Groups[1].Value);

        Assert.All(rules, rule =>
        {
            // A step behind the body size, not a shout. Set in small caps, so it reads smaller
            // than its figure — the lower bound is what stops that becoming a texture. A size
            // in points belongs to the print block and is held by the 7pt floor instead.
            Assert.All(FontSizesInRem(rule), rem => Assert.InRange(rem, 0.6, 0.95));

            // Never emphasised. A cost is bookkeeping; bolding it puts it in front of the rank,
            // whatever the size and the ink are doing.
            Assert.All(Values(rule, @"font-weight:(\d+)"),
                v => Assert.InRange(int.Parse(v, CultureInfo.InvariantCulture), 100, 500));

            // And never brought back into the body ink or out of small caps, which are the
            // other two halves of the separation.
            Assert.All(Values(rule, @"(?<!-)color:([^;]+)"), v => Assert.Equal("var(--muted)", v));
            Assert.All(Values(rule, @"text-transform:([^;]+)"), v => Assert.Equal("uppercase", v));
        });
    }

    /// <summary>
    /// The <c>Abilities (…)</c> line that opens a Source group is set apart from the Powers
    /// under it. It is not a Power — no rank, no cost — and in the same face it reads as the
    /// first entry in the list, which is a misreading the markup cannot prevent: the class is
    /// on the element either way, so every rendering test passes with this rule emptied.
    ///
    /// <para>The print size is asserted too. On screen it is set in <c>rem</c>, which the
    /// print block overrides for everything else on the sheet — leaving this one behind would
    /// print it visibly larger than the Power names beside it.</para>
    /// </summary>
    [Fact]
    public void ATraitSourceLineIsSetApartFromThePowersBelowIt()
    {
        var css  = WithoutCssComments(AppCss);
        var rule = Rx(@"(?<![\w.-])\.power-entry\.trait-sources\s*\{([^{}]*)\}").Match(css);

        Assert.True(rule.Success,
            "app.css does not set the trait Source line apart, so it reads as the first Power.");

        var declarations = Normalise(rule.Groups[1].Value);
        Assert.Contains("font-style:italic", declarations, StringComparison.Ordinal);

        // The value, not just the property. `Contains("font-size:")` passed at 3rem — three
        // times the body size — which is the same weakness this test's print half already
        // had. Set apart means a step down from the entries it sits above, not a shout.
        var screen = FontSizesInRem(declarations);
        Assert.NotEmpty(screen);
        Assert.All(screen, rem => Assert.InRange(rem, 0.7, 1.0));

        var printed = PrintRuleFor(".power-entry.trait-sources");
        Assert.True(printed is not null,
            "The print block leaves the trait Source line at its screen size.");

        // The value, not merely the unit. `Contains("pt")` passed at 30pt, while this test's
        // own comment claimed it stopped the line printing larger than the entries around it.
        // The band it belongs to runs from the stat lines (7.5pt) to the body size (10.5pt).
        var size = Rx(@"font-size:\s*([0-9.]+)pt").Match(Normalise(printed!));
        Assert.True(size.Success, "The print rule sets no font size in points.");

        // Strictly under the 10.5pt body size the Power names take, which an inclusive upper
        // bound of 10.5 did not enforce: the line has to read as a step below them.
        var points = double.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.InRange(points, 7.5, 9.5);
    }

    /// <summary>A heading that strands at the foot of a page belongs to nothing.</summary>
    [Fact]
    public void ASectionHeadingNeverStrandsAtTheFootOfAPage()
    {
        var rule = PrintRuleFor(".sheet-section > h3");

        Assert.True(rule is not null, "The print block does not constrain where a section heading may break.");
        Assert.Contains("break-after:avoid", Normalise(rule!), StringComparison.Ordinal);
    }

    /// <summary>
    /// Working UI is not sheet content. Asserted with the declaration, not just the selector:
    /// the rule was flipped to <c>display: block</c> and the test still passed, because it
    /// only ever checked that the word ".banner" appeared.
    /// </summary>
    [Theory]
    [InlineData(".banner")]
    [InlineData(".steps")]
    [InlineData(".nav-buttons")]
    [InlineData(".budget")]
    [InlineData(".tabs")]
    [InlineData(".mode-switch")]
    [InlineData(".banner-link")]
    [InlineData(".replay")]
    [InlineData(".no-print")]
    // A filter box is a control and the count beside it is a fact about a screen.
    [InlineData(".options-filter")]
    // The rule a figure came out of. The sheet does not ask for one, but printing any other
    // page should not put three lines of small print under each of four boxes either.
    [InlineData(".stat-block .formula")]
    [InlineData("h1")]
    public void ThePrintedSheetLeavesOutTheToolAroundIt(string selector)
    {
        var rule = PrintRuleFor(selector);

        Assert.True(rule is not null, $"Nothing in the print block hides {selector}.");
        Assert.Contains("display:none", Normalise(rule!), StringComparison.Ordinal);

        // **And the thing being hidden still exists to be hidden.** A class renamed in the
        // components leaves this rule hiding nothing at all — it passes here, and passes every
        // rendering test, while the element it was written for prints. So the class has to be
        // written by some component or styled for the screen somewhere; a print rule for a
        // selector nothing produces is a rule that has quietly stopped working.
        var name = selector.Split(' ').Last().TrimStart('.');

        if (!selector.StartsWith('.') && !selector.Contains(" .", StringComparison.Ordinal)) return;

        var written = RazorFiles.Any(f => File.ReadAllText(f).Contains(name, StringComparison.Ordinal))
                      || WithoutCssComments(AppCss)
                          .Contains($".{name}", StringComparison.Ordinal);

        Assert.True(written,
            $"The print block hides '{selector}', but nothing writes \"{name}\" any more. The "
            + "rule is hiding an element that no longer exists under that name.");
    }

    /// <summary>
    /// A running footer positioned with <c>position: fixed</c> looks correct and is not:
    /// Chrome's print output renders it once, at the top of page two, over the content. What
    /// carries the character's name across every page is the document title, which the
    /// browser prints in its own header — so the review page's title leads with the name.
    /// </summary>
    [Fact]
    public void TheNameTravelsByDocumentTitleAndNotByAFixedFooter()
    {
        Assert.DoesNotContain("position:fixed", Normalise(OnlyPrintBlockOf(AppCss)), StringComparison.Ordinal);

        var review = File.ReadAllText(Path.Combine(WebRoot, "Pages", "Review.razor"));
        var title = Rx("<PageTitle>(.*?)</PageTitle>", RegexOptions.Singleline).Match(review);

        Assert.True(title.Success, "Review.razor has no <PageTitle>.");

        // Leads with, not merely contains: a browser's print header is one line and gets
        // truncated, so whatever comes first is what identifies the page.
        var text = title.Groups[1].Value;
        var name = text.IndexOf("Session.Sheet.Name", StringComparison.Ordinal);
        var app = text.IndexOf("Prowlers", StringComparison.Ordinal);

        Assert.True(name >= 0, "Review.razor's title does not carry the character's name.");
        Assert.True(app < 0 || name < app,
            "Review.razor's title puts the app name first, so a truncated print header "
            + "identifies the tool rather than the character.");
    }

    /// <summary>
    /// The browser builds its replay library through the loader that promises a failed fetch
    /// leaves the app running, rather than open-coding the fetch again.
    ///
    /// <para>This is the source half of a guarantee whose behaviour is tested in
    /// <c>ProwlersAndParagons.Web.Tests.ReplayLoadingTests</c>. Both are needed and neither is
    /// enough: a guarded loader nobody calls guarantees nothing, and it lived here as a
    /// <c>try</c>/<c>catch</c> in top-level statements that no test could reach — deleting the
    /// <c>try</c> was green, and one 404 then took the character generator to a blank page.
    /// </para>
    /// </summary>
    [Fact]
    public void TheBrowserBuildsItsReplayLibraryThroughTheGuardedLoader()
    {
        var program = File.ReadAllText(Path.Combine(WebRoot, "Program.cs"));

        Assert.Contains("ReplayLibrary.LoadAsync(http)", program, StringComparison.Ordinal);

        // And nowhere else builds one. Constructing it here is how the guard gets bypassed
        // without anything looking wrong.
        Assert.DoesNotContain("new ReplayLibrary(", program, StringComparison.Ordinal);

        // **Nor may it do the fetching itself.** Calling the guarded loader is not the same as
        // being guarded: an adversarial pass fetched every transcript here, in a bare loop,
        // and handed the loader a delegate that only read the resulting dictionary. The
        // throwing call was back outside the try, one 404 took the app to a blank page, and
        // both this test and every behavioural test stayed green. So the path lives on
        // ReplayLibrary and this file may not name it.
        Assert.DoesNotContain("transcripts", program, StringComparison.Ordinal);
    }

    /// <summary>
    /// The address the app asks for and the folder the build stages the recordings into are
    /// the same, and they are written down in two files that nothing else connects.
    ///
    /// <para>The behavioural half — that the loader really requests that path — is
    /// <c>ReplayLoadingTests.TheLibraryAsksForEachRecordingWhereTheBuildPutsIt</c>. This is the
    /// other end: that the path it agrees on is where the csproj puts the files. Either alone
    /// is satisfied by a consistent, wrong answer.</para>
    /// </summary>
    [Fact]
    public void TheRecordingsAreAskedForFromTheFolderTheBuildStagesThemInto()
    {
        var served = Rx(@"ServedFrom\s*=\s*""([^""]+)""")
            .Match(File.ReadAllText(Path.Combine(WebRoot, "Services", "ReplayLibrary.cs")));

        Assert.True(served.Success, "ReplayLibrary does not say where the recordings are served from.");

        // The folders the build actually stages into, read whole. `Contains` is wrong here and
        // was: `wwwroot\data\transcript` is a substring of `wwwroot\data\transcripts`, so
        // dropping the "s" — the exact one-character mistake this test exists to catch — passed.
        var staged = Rx(@"DestinationFolder=""[^""]*\\wwwroot\\([^""]+)""")
            .Matches(File.ReadAllText(Path.Combine(WebRoot, "ProwlersAndParagons.Web.csproj")))
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Contains(served.Groups[1].Value.Replace('/', '\\'), staged, StringComparer.Ordinal);
    }

    /// <summary>Widows and orphans, so a paragraph never leaves one line behind.</summary>
    [Fact]
    public void ProseDoesNotLeaveASingleLineBehind()
    {
        var normalised = Normalise(OnlyPrintBlockOf(AppCss));

        Assert.Contains("orphans:3", normalised, StringComparison.Ordinal);
        Assert.Contains("widows:3", normalised, StringComparison.Ordinal);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A .razor file with the parts a player never sees removed: Razor comments, and the
    /// <c>@code</c> block, where engineering detail belongs and is wanted.
    ///
    /// <para>The block is matched as a line that opens a brace, and the leading whitespace is
    /// tolerated. Searching for a bare <c>"\n@code"</c> meant an indented block stripped
    /// nothing — and every C# comment in it was then read as text on the page.</para>
    /// </summary>
    private static string VisibleMarkup(string razor)
    {
        var withoutComments = Rx(@"@\*.*?\*@", RegexOptions.Singleline).Replace(razor, " ");
        var block = Rx(@"^[ \t]*@code\s*\{", RegexOptions.Multiline).Match(withoutComments);

        return block.Success ? withoutComments[..block.Index] : withoutComments;
    }

    /// <summary>
    /// The prose a player actually reads: visible markup with the tags removed (which takes
    /// every attribute with them) and Razor expressions removed. What is left is text.
    ///
    /// <para>This is why a type name can be tested for at all. <c>@PowerFormatter.StatLine(p)</c>
    /// is an expression and legitimate; the same characters sitting in a paragraph are not,
    /// and only one of the two survives to here.</para>
    /// </summary>
    private static string VisibleText(string razor)
    {
        var text = VisibleMarkup(razor);

        text = Rx("<[^>]*>").Replace(text, " ");              // tags, and so all attributes
        text = Rx(@"@\([^()]*(\([^()]*\))?[^()]*\)").Replace(text, " ");   // @( … )
        text = Rx(@"@\w+(\.\w+)*(\([^()]*\))?").Replace(text, " ");        // @Foo.Bar(…), @if, @foreach

        return text;
    }

    private static string WithoutCssComments(string css) =>
        Rx(@"/\*.*?\*/", RegexOptions.Singleline).Replace(css, " ");

    /// <summary>Whitespace removed, so `position:fixed` and `position: fixed` are one string.</summary>
    private static string Normalise(string css) => Rx(@"\s+").Replace(css, "");

    /// <summary>Every spelling of "start moving something" this app can use.</summary>
    private static readonly string[] AnimationCalls = ["startViewTransition(", ".animate("];

    /// <summary>
    /// <b>Every scripted animation asks whether movement is wanted — each one, not the file.</b>
    ///
    /// <para>The duration tokens collapse to <c>0.01ms</c> under <c>prefers-reduced-motion</c>,
    /// which covers every CSS transition in the app and nothing a script does:
    /// <c>startViewTransition()</c> and <c>element.animate()</c> read no custom property. <b>A
    /// token cannot reach a script</b>, and the stylesheet looking correct is why this is easy to
    /// miss.</para>
    ///
    /// <para><b>This was a whole-file grep wearing a per-entry-point docstring, and two reviewers
    /// demonstrated it independently.</b> The helper took the <em>first</em> <c>animate(</c> —
    /// which is in the file's header comment, above any brace — so it fell back to returning the
    /// whole file, and "the block calling this gates on <c>still()</c>" became "the file mentions
    /// <c>still()</c> somewhere". A new entry point added below, gated by nothing, was green here
    /// <em>and</em> in the browser harness, which drives only the entry points it knows about.
    /// Comments are stripped first now, and every occurrence is checked rather than the first.</para>
    /// </summary>
    [Fact]
    public void EveryScriptedAnimationAsksWhetherMovementIsWanted()
    {
        var motion = Scripts.SingleOrDefault(s => s.Name == "motion.js");

        Assert.False(motion.Text is null,
            "motion.js has gone. If scripted motion moved to another file, point this test at it: "
            + "the reduced-motion setting is unreachable from the stylesheet.");

        Assert.Contains("prefers-reduced-motion", motion.Text, StringComparison.Ordinal);
        Assert.Contains("matchMedia", motion.Text, StringComparison.Ordinal);

        var code = WithoutJsComments(motion.Text);

        var calls = AnimationCalls
            .SelectMany(needle => Occurrences(code, needle).Select(at => (Needle: needle, At: at)))
            .ToList();

        Assert.True(
            calls.Count >= 3,
            $"Found {calls.Count} animation calls in motion.js; expected at least the three entry "
            + "points. If they moved, point this test at them rather than letting it pass empty.");

        foreach (var (needle, at) in calls)
        {
            var body = EnclosingBlock(code, at);

            Assert.True(
                body.Contains("still()", StringComparison.Ordinal),
                $"A call to {needle} is not gated on still(), so a reduced-motion user gets the "
                + "full animation. The stylesheet's 0.01ms tokens do not reach this code.");
        }
    }

    /// <summary>
    /// <b>A view transition that is never released leaves the page frozen.</b>
    ///
    /// <para>While one is open the live DOM is hidden behind a snapshot, so a navigation that
    /// throws, or a handler disposed mid-flight, strands the user looking at a still image of
    /// the app with no way back. That is the worst failure available in this file, and it is
    /// invisible in every ordinary run because the release normally arrives.</para>
    ///
    /// <para>So the script must release on a timer as well as on the render. This asserts the
    /// timer exists <em>and</em> that it is armed after the transition is opened, since a
    /// <c>setTimeout</c> that an early return skips is not a safety net.</para>
    /// </summary>
    [Fact]
    public void AnOpenViewTransitionIsAlwaysReleased()
    {
        var motion = Scripts.Single(s => s.Name == "motion.js").Text;

        Assert.Contains("setTimeout", motion, StringComparison.Ordinal);
        Assert.Contains("clearTimeout", motion, StringComparison.Ordinal);

        var timer = motion.IndexOf("setTimeout", StringComparison.Ordinal);
        var opens = motion.IndexOf("startViewTransition", StringComparison.Ordinal);

        Assert.True(opens >= 0, "Nothing opens a transition; this test has lost its subject.");
        Assert.True(
            timer > opens,
            "The safety timer is armed before the transition is opened, so an early return skips "
            + "it and leaves the snapshot up for ever.");
    }

    /// <summary>
    /// <b>The transition is opened from the <c>LocationChanging</c> handler itself.</b>
    ///
    /// <para><c>LocationChanged</c> is the hook already in this layout and the one anybody would
    /// reach for, and it fires <em>after</em> the navigation, when the old page is gone and there
    /// is nothing left to snapshot. The failure is silent — no error, just no animation, which is
    /// indistinguishable from a browser that does not support it.</para>
    ///
    /// <para><b>The first version of this test asserted four substrings over the whole file and
    /// checked none of that.</b> A reviewer moved the snapshot into <c>Moved</c> — the
    /// <c>LocationChanged</c> handler — left <c>OpenTransition</c> registered and empty, and every
    /// one of the four still matched: the whole suite and all six browser harnesses stayed green.
    /// So this resolves the registered handler by name and asks what <em>its</em> body does.</para>
    ///
    /// <para>The behavioural half is <c>TheShellOpensATransitionOnNavigation</c> in the bUnit
    /// project, which drives a real navigation and reads the interop back. Both are wanted: this
    /// one names the mechanism, that one proves it fires.</para>
    /// </summary>
    [Fact]
    public void TheTransitionIsOpenedFromTheLocationChangingHandler()
    {
        var layout = File.ReadAllText(Path.Combine(WebRoot, "Layout", "MainLayout.razor"));

        // Which method is registered as the changing handler?
        var registered = Rx(@"RegisterLocationChangingHandler\(\s*(\w+)\s*\)").Match(layout);
        Assert.True(
            registered.Success,
            "Nothing registers a LocationChanging handler, so the snapshot cannot be taken while "
            + "the old page is still on screen.");

        var handler = registered.Groups[1].Value;

        // ...and what does that method's body do? Anchored on the declaration, so moving the call
        // into LocationChanged's handler no longer satisfies it.
        var body = MethodBodyOf(layout, handler);

        Assert.True(
            body.Contains("Motion.Begin()", StringComparison.Ordinal),
            $"'{handler}' is registered as the LocationChanging handler but does not open the "
            + "transition. If the snapshot is taken from LocationChanged instead, it captures the "
            + "page that has already gone.");

        // Released after the render, or the app freezes behind a snapshot on its first navigation.
        Assert.Contains("Motion.End()", layout, StringComparison.Ordinal);

        // The registration is disposed. bUnit creates a layout per test, and an undisposed handler
        // outlives the component that registered it.
        Assert.Contains("_leaving?.Dispose()", layout, StringComparison.Ordinal);
    }

    /// <summary>
    /// The body of a C# method declared in <paramref name="source"/>, found by name and matched by
    /// brace depth from its opening brace.
    /// </summary>
    private static string MethodBodyOf(string source, string name)
    {
        var declared = Rx($@"\b{System.Text.RegularExpressions.Regex.Escape(name)}\s*\(")
            .Matches(source)
            .Select(m => m.Index)
            .ToList();

        foreach (var at in declared)
        {
            var open = source.IndexOf('{', at);
            if (open < 0) continue;

            // A call site — `RegisterLocationChangingHandler(OpenTransition)` — has no body of its
            // own, so require the brace to be close enough to be this declaration's.
            if (source[at..open].Contains(';', StringComparison.Ordinal)) continue;

            var depth = 0;
            for (var i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0)
                {
                    var body = source[(open + 1)..i];
                    if (body.Trim().Length > 0) return body;
                    break;
                }
            }
        }

        Assert.Fail($"Could not find a method body for '{name}'.");
        return string.Empty;
    }

    /// <summary>The three chrome bands, each appearing exactly once in the component tree.</summary>
    private static readonly string[] SingletonSelectors = [".banner", ".steps", ".budget"];

    /// <summary>
    /// <b>No two <em>elements</em> can share a <c>view-transition-name</c>.</b>
    ///
    /// <para>A duplicate name is a spec error and the browser abandons the whole transition, so
    /// the symptom is that nothing animates anywhere — which reads as "not supported" rather than
    /// as a mistake in this stylesheet.</para>
    ///
    /// <para><b>Checking that the declared names differ is not that check</b>, and both reviewers
    /// broke it the same way: one name on a selector matching many elements is unique as a string
    /// and duplicated on the page. <c>.panel { view-transition-name: panel }</c> passed here, and
    /// driven against real Chrome produced <c>InvalidStateError: Transition was aborted because of
    /// invalid state</c> — every transition in the app dead. So the <em>selector</em> is what is
    /// checked, against the three known to match exactly one element.</para>
    /// </summary>
    [Fact]
    public void EveryViewTransitionNameIsOnASingletonSelector()
    {
        // A local would fail CA1861 under warnings-as-errors; see SingletonSelectors.

        var rules = Rx(@"([^{}]+)\{([^{}]*?view-transition-name:\s*([A-Za-z-][\w-]*)[^{}]*)\}")
            .Matches(WithoutCssComments(AppCss))
            .Select(m => (Selector: Normalise(m.Groups[1].Value), Name: m.Groups[3].Value))
            .ToList();

        Assert.True(
            rules.Count >= 3,
            $"Found {rules.Count} view-transition-name rules, expected the three chrome bands. If "
            + "they moved, point this test at them rather than letting it pass on an empty list.");

        foreach (var (selector, name) in rules)
        {
            Assert.True(
                SingletonSelectors.Contains(selector, StringComparer.Ordinal),
                $"'{selector}' carries view-transition-name: {name} and is not one of the selectors "
                + $"known to match exactly one element ({string.Join(", ", SingletonSelectors)}). A name on "
                + "a selector matching several elements is a spec error, and the browser responds "
                + "by abandoning every transition in the app.");
        }

        var duplicated = rules.GroupBy(r => r.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicated.Count == 0, $"Shared name: {string.Join(", ", duplicated)}.");
    }

    /// <summary>Every index at which <paramref name="needle"/> occurs.</summary>
    private static IEnumerable<int> Occurrences(string text, string needle)
    {
        for (var at = text.IndexOf(needle, StringComparison.Ordinal);
             at >= 0;
             at = text.IndexOf(needle, at + 1, StringComparison.Ordinal))
        {
            yield return at;
        }
    }

    /// <summary>
    /// JavaScript with its comments blanked out, so a match inside prose cannot anchor anything.
    ///
    /// <para><b>This is what two reviewers broke independently.</b> The first <c>animate(</c> and
    /// the first <c>startViewTransition</c> in <c>motion.js</c> are both in the file's header
    /// comment, above any brace — so a helper that took the first occurrence and searched upwards
    /// for a <c>{</c> found none and fell back to the whole file, turning a per-entry-point check
    /// into a whole-file grep.</para>
    ///
    /// <para>Blanked rather than removed, so every index still lines up with the original.</para>
    /// </summary>
    private static string WithoutJsComments(string js)
    {
        var outp = new System.Text.StringBuilder(js.Length);
        var i = 0;

        while (i < js.Length)
        {
            if (i + 1 < js.Length && js[i] == '/' && js[i + 1] == '/')
            {
                while (i < js.Length && js[i] != '\n') { outp.Append(' '); i++; }
                continue;
            }

            if (i + 1 < js.Length && js[i] == '/' && js[i + 1] == '*')
            {
                while (i + 1 < js.Length && !(js[i] == '*' && js[i + 1] == '/'))
                {
                    outp.Append(js[i] == '\n' ? '\n' : ' ');
                    i++;
                }

                for (var k = 0; k < 2 && i < js.Length; k++, i++) outp.Append(' ');
                continue;
            }

            outp.Append(js[i]);
            i++;
        }

        return outp.ToString();
    }

    /// <summary>The smallest braced block containing <paramref name="at"/>.</summary>
    private static string EnclosingBlock(string text, int at)
    {
        var open = text.LastIndexOf('{', at);

        while (open >= 0)
        {
            var depth = 0;

            for (var i = open; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}' && --depth == 0)
                {
                    // A block that closes before the call is a sibling, not an ancestor.
                    if (i > at) return text[(open + 1)..i];
                    break;
                }
            }

            open = text.LastIndexOf('{', open - 1);
        }

        return text;
    }

    /// <summary>
    /// The contents of the file's one <c>@media print</c> block, comments stripped first so a
    /// brace inside one cannot end the block early. That there is only one is its own test.
    /// </summary>
    private static string OnlyPrintBlockOf(string css)
    {
        var body = WithoutCssComments(css);
        var start = body.IndexOf("@media print", StringComparison.Ordinal);
        Assert.True(start >= 0, "No @media print block.");

        var open = body.IndexOf('{', start);
        var depth = 0;

        for (var i = open; i < body.Length; i++)
        {
            if (body[i] == '{') depth++;
            else if (body[i] == '}' && --depth == 0) return body[(open + 1)..i];
        }

        throw new InvalidOperationException("The @media print block is not closed.");
    }

    /// <summary>
    /// The declarations of the print rule whose selector list contains <paramref name="selector"/>,
    /// or null. Selector-and-declaration have to be checked together: separately, "this
    /// selector is mentioned" and "this declaration appears somewhere" are both satisfied by
    /// a stylesheet that does the opposite of what is intended.
    /// </summary>
    /// <summary>
    /// The normalised declarations of every rule in the whole stylesheet whose selector ends
    /// in <paramref name="target"/> — so a more specific rule further down, which is what
    /// actually wins the cascade, is read too. Asserting on "the first rule with this class in
    /// it" is defeated by adding a second one and never touching the first.
    /// </summary>
    private static List<string> RulesTargeting(string target) =>
        [.. Rx(@"([^{}]+)\{([^{}]*)\}")
            .Matches(WithoutCssComments(AppCss))
            .Where(rule => rule.Groups[1].Value
                .Split(',')
                .Select(Normalise)
                .Any(s => s.EndsWith(target, StringComparison.Ordinal)))
            .Select(rule => Normalise(rule.Groups[2].Value))];

    private static string? PrintRuleFor(string selector)
    {
        // Every rule that names the selector, not the first. A second rule setting something
        // unrelated on the same element — `print-color-adjust`, as it happened — shadowed the
        // one being asserted on, and the assertion then failed against a declaration block
        // that was never its subject.
        var matching = Rx(@"([^{}]+)\{([^{}]*)\}")
            .Matches(OnlyPrintBlockOf(AppCss))
            .Where(rule => rule.Groups[1].Value
                .Split(',')
                .Select(Normalise)
                .Contains(Normalise(selector), StringComparer.Ordinal))
            .Select(rule => rule.Groups[2].Value)
            .ToList();

        return matching.Count == 0 ? null : string.Join(';', matching);
    }

    /// <summary>
    /// Rough relative luminance of a #RGB or #RRGGBB value, 0 (black) to 1 (white). Rough is
    /// enough: the question is "is this ink or is this paper", not a contrast ratio. A value
    /// that is not a hex colour throws rather than guessing — a token that stopped being a
    /// colour is exactly the change this must not wave through.
    /// </summary>
    private static double Luminance(string value)
    {
        var hex = Rx(@"#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})\b").Match(value);

        if (!hex.Success)
            throw new InvalidOperationException($"'{value}' is not a hex colour.");

        var digits = hex.Groups[1].Value;
        if (digits.Length == 3)
            digits = string.Concat(digits.Select(c => new string(c, 2)));

        var r = Convert.ToInt32(digits[..2], 16) / 255.0;
        var g = Convert.ToInt32(digits[2..4], 16) / 255.0;
        var b = Convert.ToInt32(digits[4..], 16) / 255.0;

        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }
}
