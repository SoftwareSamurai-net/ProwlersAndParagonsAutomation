using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The sheet with every name on it saying what it means.
///
/// <para>The descriptions were in <c>data/rules</c> the whole time and only the editors ever showed
/// one: a printed sheet says "Presence 6d" and "Plot Hook" and "TECH POWERS" and left a reader to
/// know what those were.</para>
///
/// <para><b>They are on by default now, and this file&apos;s negative half changed shape with it.</b>
/// It used to guard against <c>Explain</c> leaking out of its one address onto the review step, the
/// preview and every replayed recording — on the argument that forty names becoming controls would
/// change the deliverable. <b>It does not</b>: the print block shuts a tip outright, so the page out
/// of the printer is identical either way, and <c>PrintingASheetIsUnchangedByTheExplanations</c>
/// pins that. What the off case guards now is <c>Term</c>&apos;s fallback — the bare name it draws
/// when there is nothing to say — which is a live path and has to keep rendering exactly what the
/// sheet rendered before. So the off case is still asserted as hard as the on case, for a different
/// reason.</para>
/// </summary>
public sealed partial class ExplainedSheetTests
{
    private static IRenderedComponent<SheetView> Explained(RenderContext ctx) =>
        ctx.Render<SheetView>(p => p.Add(v => v.Explain, true));

    /// <summary>
    /// The sheet with the explanations turned off outright — which nothing in the app now does, and
    /// which is exercised here precisely because nothing does. It selects <c>Term</c>&apos;s
    /// bare-name fallback, and that path has to keep rendering what the sheet rendered before
    /// explanations existed; a setting nothing uses and nothing tests is a setting that rots.
    /// </summary>
    private static IRenderedComponent<SheetView> Plain(RenderContext ctx) =>
        ctx.Render<SheetView>(p => p.Add(v => v.Explain, false));

    /// <summary>
    /// What a reader sees, modelled the way a browser renders it — <see cref="SheetText"/>.
    ///
    /// <para><b>It used to live here, and moved when the sheet started explaining itself by
    /// default.</b> While one address drew terms, this file was the only one that had to model a
    /// reader's eye; now every sheet test does, because a term's cell holds the name and two copies
    /// of its description. Kept as a one-line forward so the assertions below read as they did.</para>
    /// </summary>
    private static string Visible(INode node) => SheetText.Visible(node);


    /// <summary>
    /// Every Ability and every Talent — all eighteen, bought or not, because the sheet prints them
    /// all — carries the rules data's own sentence.
    /// </summary>
    [Fact]
    public void EveryTraitOnTheSheetCarriesTheRulesDatasOwnDescription()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var cut = Explained(ctx);

        var described = cut.FindAll(".trait-table .term")
            .ToDictionary(
                t => t.QuerySelector(".term-name")!.TextContent,
                t => t.QuerySelector(".sr-only")!.TextContent,
                StringComparer.Ordinal);

        Assert.Equal(rules.Abilities.Count + rules.Talents.Count, described.Count);

        foreach (var trait in rules.Abilities.Select(a => (a.Name, a.Description))
                     .Concat(rules.Talents.Select(t => (t.Name, t.Description))))
        {
            // The sentence itself, not merely that there is one: a component that put the name in
            // both places would satisfy a presence check.
            Assert.Equal(trait.Description, described[trait.Name]);
        }
    }

    /// <summary>A Power on the sheet carries its own entry's description, not its neighbour's.</summary>
    [Fact]
    public void APowerCarriesItsOwnDescription()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var cut = Explained(ctx);

        foreach (var chosen in ctx.Session.Sheet.SelectedPowers)
        {
            var power = rules.GetPower(chosen.PowerId)!;

            var term = cut.FindAll(".power-entry .pname .term")
                .Single(t => t.QuerySelector(".term-name")!.TextContent == power.Name);

            Assert.Equal(power.Description, term.QuerySelector(".sr-only")!.TextContent);
        }
    }

    /// <summary>
    /// <b>"Armor 8d", never "Armor8d".</b> Putting a component between a Power's name and its rank
    /// is exactly the change that has produced that bug twice in this repository — Razor strips the
    /// leading whitespace inside an element that follows an expression — and it is invisible to any
    /// test that reads source or strips tags out of markup.
    /// </summary>
    [Fact]
    public void APowersRankIsStillSeparatedFromItsName()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var ranked = ctx.Session.Sheet.SelectedPowers
            .Select(p => rules.GetPower(p.PowerId)!)
            .First(p => p.RankType is not ("default" or "special"));

        foreach (var explain in (bool[])[true, false])
        {
            // `Visible` rather than TextContent, which would carry both hidden copies of the
            // Power's description — and rather than markup with the tags replaced, which reads
            // "Armor 8d" out of a component printing "Armor8d". See the helper.
            var name = Visible(ctx.Render<SheetView>(p => p.Add(v => v.Explain, explain))
                .FindAll(".power-entry .pname")
                .Single(e => Visible(e).StartsWith(ranked.Name, StringComparison.Ordinal)));

            Assert.Matches($@"^{System.Text.RegularExpressions.Regex.Escape(ranked.Name)} \d+d$", name);
        }
    }

    /// <summary>A Perk and a Flaw carry theirs too — including a Flaw's narrative detail beside it.</summary>
    [Fact]
    public void PerksAndFlawsCarryTheirDescriptions()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var cut = Explained(ctx);
        var terms = cut.FindAll(".sheet-section .term")
            .ToDictionary(
                t => t.QuerySelector(".term-name")!.TextContent,
                t => t.QuerySelector(".sr-only")!.TextContent,
                StringComparer.Ordinal);

        Assert.NotEmpty(ctx.Session.Sheet.Perks);
        Assert.NotEmpty(ctx.Session.Sheet.Flaws);

        foreach (var perk in ctx.Session.Sheet.Perks)
            Assert.Equal(rules.GetPerk(perk.PerkId)!.Description, terms[rules.GetPerk(perk.PerkId)!.Name]);

        foreach (var flaw in ctx.Session.Sheet.Flaws)
            Assert.Equal(rules.GetFlaw(flaw.FlawId)!.Description, terms[rules.GetFlaw(flaw.FlawId)!.Name]);
    }

    /// <summary>
    /// A Powers group's heading is a Source, and a Source is the one heading on the sheet that
    /// carries a rule. "TECH POWERS" says nothing about what a Source is.
    /// </summary>
    [Fact]
    public void ASourceHeadingExplainsWhatASourceIs()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var cut = Explained(ctx);

        var headings = cut.FindAll(".powers > h3 .term")
            .Select(t => t.QuerySelector(".sr-only")!.TextContent)
            .ToList();

        Assert.NotEmpty(headings);
        Assert.All(headings, h => Assert.Contains(h, rules.Sources.Select(s => s.Description)));

        // "Abilities", "Gear", "Perks" explain themselves and must not gain a control.
        Assert.Empty(cut.FindAll(".sheet-section:not(.powers) > h3 .term"));
    }

    /// <summary>
    /// The four big figures explain themselves through their labels, using the same sentences the
    /// derived-stats step prints under them — one set of rules, two ways of asking.
    /// </summary>
    [Fact]
    public void TheFourFiguresCarryTheRuleBehindThem()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var cut = Explained(ctx);

        var labels = cut.FindAll(".stat-block .label .term")
            .ToDictionary(
                t => t.QuerySelector(".term-name")!.TextContent,
                t => t.QuerySelector(".sr-only")!.TextContent,
                StringComparer.Ordinal);

        Assert.Equal(4, labels.Count);
        Assert.Contains("Danger Sense", labels["Edge"], StringComparison.Ordinal);
        Assert.Contains("Toughness", labels["Health"], StringComparison.Ordinal);
        Assert.Contains("Trait Cap", labels["Resolve"], StringComparison.Ordinal);
        Assert.Contains("Hero Points", labels["Hero Points"], StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The sheet explains every name on it without being asked.</b> This is the owner&apos;s
    /// report: <i>"the &apos;explain this character sheet&apos; button is still present, instead of
    /// that just being the default way the sheet renders."</i> Asserted on the bare component with
    /// no parameter passed, so it is the default under test and not an argument this test supplied.
    /// </summary>
    [Fact]
    public void TheSheetExplainsEveryNameOnItByDefault()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var sheet = ctx.Render<SheetView>();

        Assert.NotEmpty(sheet.FindAll(".term"));
        Assert.NotEmpty(sheet.FindAll(".term-name"));
        Assert.NotEmpty(sheet.FindAll(".row-tip"));
    }

    /// <summary>
    /// And off is still off — <c>Term</c>&apos;s bare-name fallback draws no control at all.
    /// Nothing in the app selects this any more, which is exactly why it is asserted: the same
    /// fallback is what a Power with no description in the rules data gets, and that is not
    /// hypothetical.
    ///
    /// <para>The positive controls are beside it, because "no controls found" is satisfied
    /// completely by a sheet that failed to render.</para>
    /// </summary>
    [Fact]
    public void ASheetDrawnWithoutExplanationsHasNoControlOnItAtAll()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var plain = Plain(ctx);

        Assert.Empty(plain.FindAll(".term"));
        Assert.Empty(plain.FindAll(".term-name"));
        Assert.Empty(plain.FindAll(".row-tip"));

        Assert.NotEmpty(plain.FindAll(".sheet-section"));
        Assert.NotEmpty(ctx.Render<SheetView>().FindAll(".term-name"));
    }

    /// <summary>
    /// The review step in particular, because that is the page whose sheet goes on paper — and it
    /// is the page the owner was looking at when they reported the button. This assertion is the
    /// exact reverse of the one it replaces.
    /// </summary>
    [Fact]
    public void TheReviewStepsSheetIsTheExplainedOne()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        Assert.NotEmpty(ctx.Render<Review>().FindAll(".term-name"));
    }

    /// <summary>
    /// Every word the plain sheet prints, the explained sheet prints too. Wrapping forty names in a
    /// component is exactly the change that could drop one, and a name missing from a sheet is the
    /// kind of wrong that looks entirely right.
    /// </summary>
    [Fact]
    public void ExplainingTheSheetChangesNoneOfItsWords()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        static string Words(IRenderedComponent<SheetView> sheet) => Visible(sheet.Find(".sheet"));

        // Word for word identical, which is the strongest form this can take: the hidden copies of
        // each description are what `Visible` drops, so what is left on the explained sheet is
        // exactly what the plain one prints. Wrapping forty names in a component is precisely the
        // change that could quietly drop one, and a name missing from a sheet looks entirely right.
        // **Explicitly off against the default, rather than the default against `Explain="true"`.**
        // Now that the default is on, the second of those would compare a render with itself — a
        // test that passes by construction and proves nothing about dropping a name.
        Assert.Equal(Words(Plain(ctx)), Words(ctx.Render<SheetView>()));
    }

    /// <summary>
    /// <b>There is no second address for the same sheet, and nothing offers one.</b> Both the link
    /// and the <c>/build/sheet</c> page it pointed at are gone: with the sheet below already
    /// explained, the link offered a way to the page you were already on.
    ///
    /// <para>The positive control matters here more than usual — "no link to <c>build/sheet</c>" is
    /// satisfied by a review step that rendered no links at all. So the panel&apos;s other controls
    /// and the sheet itself are asserted present in the same breath.</para>
    /// </summary>
    [Fact]
    public void NothingOffersASecondAddressForTheSameSheet()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var review = ctx.Render<Review>();

        Assert.DoesNotContain("build/sheet",
            review.FindAll("a").Select(a => a.GetAttribute("href")));
        Assert.DoesNotContain("Explain this sheet", review.Markup, StringComparison.Ordinal);

        Assert.Contains("Print this sheet", review.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(review.FindAll(".sheet"));
    }

    /// <summary>
    /// <b>The printed page is unchanged by the explanations, which is the whole reason the default
    /// could move.</b> The old default was off on the argument that the printed sheet is the
    /// deliverable and a sheet that gained forty controls would be a different document. The premise
    /// is right and the conclusion did not follow, because the description never reaches paper.
    ///
    /// <para><b>This test asserted the wrong three things first, and an adversarial review broke the
    /// real mechanism and watched it stay green.</b> It checked that <c>.tip-wrap</c> was in the
    /// print block's hide list — but <c>.tip-wrap</c> belongs to <c>Tooltip</c> and a <c>Term</c>
    /// has no such ancestor — and that <c>clip-path</c> appeared *somewhere* in the file. Both are
    /// true and neither is what keeps a description off paper. Setting <c>.row-tip</c>'s own rule to
    /// <c>display: block</c> left the whole suite passing and put every description on the printed
    /// page, which is the exact regression this exists to prevent. <b>Breaking a guard and watching
    /// it fail is not enough if you break something the guard was never about</b> — that is the
    /// null mutation this repository already records, wearing a disguise.
    ///
    /// <para>What actually holds is one rule: <c>.row-tip</c> is <c>display: none</c> and is opened
    /// only by <c>:hover</c> and <c>:focus-visible</c>, neither of which a sheet of paper can be in.
    /// So that is what is asserted, at the selector level rather than by looking for a string
    /// anywhere in the file.</para>
    /// </summary>
    [Fact]
    public void NoRuleOpensATermsDescriptionExceptOnHoverOrFocus()
    {
        var css = Stylesheet();

        // **Split on commas, because a selector list is not one selector.** A rule reading
        // `.option:hover .row-tip, .sheet .row-tip { display: block }` opens the tip
        // unconditionally through its second branch while the string `:hover` is still present in
        // the first — and asking the combined selector let exactly that mutation through. Found by
        // running it, which is the only way this kind of hole is ever found.
        var opens = Rules(css)
            .Where(r => r.Body.Contains("display: block", StringComparison.Ordinal))
            .SelectMany(r => r.Selector.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(branch => branch.Contains("row-tip", StringComparison.Ordinal))
            .ToList();

        // The positive control: some rule really does open it, or a stylesheet that had stopped
        // styling the tip at all would satisfy every assertion below by having nothing to check.
        Assert.NotEmpty(opens);

        foreach (var branch in opens)
        {
            Assert.True(
                branch.Contains(":hover", StringComparison.Ordinal)
                || branch.Contains(":focus-visible", StringComparison.Ordinal),
                $"`{branch}` opens a term's description without asking for hover or focus, "
                + "so it would open on paper too — and the printed sheet is the deliverable.");
        }

        // And the shut rule is really there, unqualified: without it the selectors above would be
        // opening something that was never closed.
        Assert.Contains(
            Rules(css).Where(r => r.Selector == ".row-tip"),
            r => r.Body.Contains("display: none", StringComparison.Ordinal));
    }

    /// <summary>
    /// And the term itself gives up every mark that says it was ever a control, so the word prints
    /// as a word. This half was always right — it is the print block's own doing, and it is the only
    /// part of a term's print behaviour that the print block is responsible for at all.
    /// </summary>
    [Fact]
    public void PrintingATermPrintsThePlainWord()
    {
        var css = Stylesheet();

        var print = css[css.IndexOf("@media print", StringComparison.Ordinal)..];
        Assert.True(print.Length > 0, "app.css no longer has a print block at all.");

        // `Single`, not `SingleOrDefault` plus a null check: these are value tuples, so the
        // default is ("", "") rather than null and the check would always pass. The CI build's
        // analyzers caught that; a plain `dotnet test` did not.
        var rule = Assert.Single(Rules(print), r => r.Selector == ".term-name");

        Assert.Contains("text-decoration: none", rule.Body, StringComparison.Ordinal);
        Assert.Contains("cursor: auto", rule.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The description's other copy — the one <c>aria-describedby</c> names, which cannot be
    /// <c>display: none</c> without leaving the accessibility tree — is a clipped 1px box, and a
    /// clipped box contributes nothing to a printed page.
    /// </summary>
    [Fact]
    public void TheDescriptionsOtherCopyIsAClippedBox()
    {
        var rule = Assert.Single(Rules(Stylesheet()), r => r.Selector == ".sr-only");

        Assert.Contains("clip-path: inset(50%)", rule.Body, StringComparison.Ordinal);
        Assert.Contains("position: absolute", rule.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Nothing the sheet explains shares a name with anything else it explains.</b> A term's id
    /// is derived from its name, so two different things called the same word would put one id on
    /// two elements carrying <em>different</em> sentences — and <c>aria-describedby</c> resolves to
    /// the first, so the second would be described by the wrong one, silently.
    ///
    /// <para><b>It holds today and nothing was pinning it.</b> The five categories the sheet draws
    /// terms for — Abilities, Talents, Powers, Perks, Flaws — collide on no name at all. Two
    /// collisions do exist in the rules data (<c>Collapsible</c>, <c>Repair</c>) and neither
    /// reaches a term: both are between a Power's own Con and something in another file, and Pros
    /// and Cons print as a stat line rather than as terms. That is a fact about today's data, not a
    /// property of the design, which is exactly why it needs a test — it costs nothing now and
    /// catches the entry that would break it.</para>
    ///
    /// <para><b>What this deliberately does not forbid</b> is the same Power selected twice, which
    /// <c>CharacterValidator</c> allows with a warning. Those two terms share an id and share a
    /// sentence, so whichever one <c>aria-describedby</c> resolves to is right — the collision
    /// <c>Term</c>'s own remarks already call the one worth having.</para>
    /// </summary>
    [Fact]
    public void NoTwoThingsTheSheetExplainsShareAName()
    {
        using var ctx = new RenderContext();
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var named = rules.Abilities.Select(a => (Kind: "Ability", a.Name))
            .Concat(rules.Talents.Select(t => (Kind: "Talent", t.Name)))
            .Concat(rules.Powers.Select(p => (Kind: "Power", p.Name)))
            .Concat(rules.Perks.Select(p => (Kind: "Perk", p.Name)))
            .Concat(rules.Flaws.Select(f => (Kind: "Flaw", f.Name)))
            .ToList();

        // The positive control: all five categories really were read, so "no collisions" is not
        // the answer an empty list gives.
        Assert.True(named.Count > 150, $"only {named.Count} names read; the sheet explains more.");

        var clashes = named
            .GroupBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(n => n.Kind).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(g => $"{g.Key} is a {string.Join(" and a ", g.Select(n => n.Kind).Distinct(StringComparer.Ordinal))}")
            .ToList();

        Assert.True(clashes.Count == 0,
            "Two things the sheet explains share a name, so one id would carry two different "
            + "descriptions and aria-describedby would resolve to the wrong one: "
            + string.Join("; ", clashes));
    }

    private static string Stylesheet() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "web", "wwwroot", "css", "app.css"));

    /// <summary>
    /// Every rule in a stylesheet as a selector and a body, comments stripped.
    ///
    /// <para><b>Rules rather than a search for a string in the whole file</b>, which is the fault
    /// the test above was written with: <c>clip-path</c> appearing somewhere in <c>app.css</c> says
    /// nothing about which selector carries it, and a guard that cannot name the rule it is about
    /// cannot notice that rule changing.</para>
    ///
    /// <para>Nested blocks — <c>@media</c>, <c>@supports</c> — have their own braces, so the
    /// at-rule's opening line is skipped rather than treated as a selector, and the rules inside it
    /// come back as ordinary rules. That is what lets a caller slice the print block off the front
    /// and ask the same question of it.</para>
    /// </summary>
    private static IEnumerable<(string Selector, string Body)> Rules(string css)
    {
        var text = CssComment().Replace(css, " ");

        foreach (var match in CssRule().Matches(text).Cast<Match>())
        {
            var selector = match.Groups[1].Value.Trim();
            if (selector.StartsWith('@') || selector.Length == 0) continue;

            yield return (Collapse(selector), match.Groups[2].Value);
        }
    }

    private static string Collapse(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CssComment();

    /// <summary>A selector and the declarations between its braces. Nested braces are not matched,
    /// which is what makes an <c>@media</c> line fall out as an at-rule rather than a selector.</summary>
    [GeneratedRegex(@"([^{}]+)\{([^{}]*)\}")]
    private static partial Regex CssRule();

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
    /// Escape closes a description and moving off brings it back, which WCAG 1.4.13 asks for and
    /// the obvious implementation leaves out: a tip that can only be closed with a pointer is not
    /// dismissable by somebody who is not using one.
    /// </summary>
    [Fact]
    public void EscapeDismissesADescriptionAndLeavingTheTermRestoresIt()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var cut = Explained(ctx);
        var term = cut.FindAll(".trait-table .term")[0];

        Assert.DoesNotContain("dismissed", term.QuerySelector(".row-tip")!.ClassList);

        term.QuerySelector(".term-name")!.KeyDown("Escape");
        Assert.Contains("dismissed",
            cut.FindAll(".trait-table .term")[0].QuerySelector(".row-tip")!.ClassList);

        cut.FindAll(".trait-table .term")[0].QuerySelector(".term-name")!.MouseLeave();
        Assert.DoesNotContain("dismissed",
            cut.FindAll(".trait-table .term")[0].QuerySelector(".row-tip")!.ClassList);
    }

    /// <summary>
    /// <b>The description is always in the document, and is what <c>aria-describedby</c> names.</b>
    /// Hiding the one element with <c>display: none</c> would take the sentence away from a screen
    /// reader while every rendering test stayed green — which is why there are two copies, and why
    /// the id has to resolve.
    /// </summary>
    [Fact]
    public void EveryDescribedByPointsAtSomethingThatIsThere()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var cut = Explained(ctx);
        var ids = cut.FindAll("[id]").Select(e => e.GetAttribute("id")).ToHashSet(StringComparer.Ordinal);

        var described = cut.FindAll("[aria-describedby]").ToList();

        Assert.NotEmpty(described);
        Assert.All(described, e => Assert.Contains(e.GetAttribute("aria-describedby")!, ids));
    }
}
