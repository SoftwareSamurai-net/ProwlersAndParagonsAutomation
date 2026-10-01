using Microsoft.Playwright;

namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// The rules actually loaded with content — not just "the build succeeded".
///
/// <para><b>This is the check <c>PROGRESS.md</c> item 5 says is missing.</b> No other e2e check
/// reaches the Powers, Pros or Cons screens: <c>Boot</c> checks the shell, <c>Build</c> drives tier
/// selection and naming and reads the Hero Point budget, and <c>Routes</c> checks headings. The
/// trim analyzer has no warning for <c>RulesRepository</c>'s reflective
/// <c>JsonSerializer.Deserialize&lt;T&gt;</c> calls, so a clean, trimmed publish proves nothing
/// about whether a model's properties survived — and a source-generation attempt at the same
/// payload win was tried and found exactly this failure mode: six non-nullable collection
/// properties coming back null (see <c>engine/RulesRepository.cs</c> and
/// <c>RulesLoadingTests.NoCollectionOnAnyLoadedRulesModelComesBackNull</c>). This check reads the
/// published, trimmed site the way a visitor does and asserts that two of those six —
/// <c>PowerModel.PowerPros</c> and <c>PowerCons</c> — came back with names in them, plus a generic
/// Pro resolved through <c>ProConApplicability</c>.</para>
///
/// <para><b>Control</b>: the "Add a Power" list's own count box (<c>.options-count</c>, drawn by
/// <c>OptionList</c>) reports a total in the hundreds. <c>OptionRow.Admitted</c> is what increments
/// that total, once per row the filter lets through — so a rules file the trimmer emptied, or a
/// <c>PowerModel</c> list that deserialized to nothing, would leave this at zero rather than the
/// rulebook's 141. Not asserted as exactly 141: the markup never prints the book's own count, so
/// claiming it here would be this check quietly becoming the source of truth for a number
/// <c>RulesFixture</c> already holds in the engine's own suite. "In the hundreds" is the honest
/// read of what the DOM states.</para>
///
/// <para><b>Outcome</b>: opening Strike — <c>data/rules/powers.json</c>, four <c>power_pros</c>,
/// two <c>power_cons</c> — and expanding its Pro and Con pickers shows its own Deflect Missiles,
/// Reach/Throw and Sweep Pros and its own Subdual and Weapons Cons, each printed straight from
/// <c>PowerProConModel.Name</c>, plus Armor Piercing — a generic Pro with no Range or Rank-type
/// constraint, so <c>ProConApplicability.ProsFor</c> admits it for every Power — resolved out of
/// <c>pros.json</c> and <c>Session.Rules</c> rather than out of the Power's own entry.</para>
///
/// <para><b>Anonymous, deliberately.</b> Nothing about the rules catalogue is gated behind an
/// account — unlike the rulebook corpus <c>RULES</c> reads — so this runs with no sign-in and no
/// second browser context.</para>
///
/// <para><b>Mutates local storage exactly as <see cref="Build"/> does</b>, by selecting a tier, so
/// it carries the same ordering constraint <c>Program.cs</c> states for <c>A11Y</c>: this must run
/// after it. It is placed directly after <c>BUILD</c> for that reason — both leave the wizard in a
/// state Next-control visibility and tab counts would read differently — and nothing after it
/// depends on which tier this check left selected.</para>
/// </summary>
public static class Powers
{
    public static Check Check => new("POWERS", Run);

    /// <summary>
    /// A Power with several of both, picked by reading <c>data/rules/powers.json</c> rather than
    /// guessing: Strike carries four <c>power_pros</c> and two <c>power_cons</c>, more of each than
    /// any other entry with both. Its baseline is "the greater of Might and Martial Arts"
    /// (<c>baseline_greater_of</c>), not a nominated Trait (<c>baseline_selected_trait</c>), so
    /// opening its editor needs no Trait chosen first — unlike a Power such as Boost.
    /// </summary>
    private const string PowerName = "Strike";

    /// <summary>Three of Strike's own four Pros, read from its <c>power_pros</c> entries.</summary>
    private static readonly string[] OwnPros = ["Deflect Missiles", "Reach/Throw", "Sweep"];

    /// <summary>Both of Strike's own Cons, read from its <c>power_cons</c> entries.</summary>
    private static readonly string[] OwnCons = ["Subdual", "Weapons"];

    /// <summary>
    /// A generic Pro from <c>data/rules/pros.json</c> with an empty <c>applies_to_ranges</c> and
    /// an empty <c>applies_to_rank_types</c>, so <see cref="ProConApplicability"/> — read, not
    /// guessed — admits it for every Power including Strike's touch-range baseline rank type.
    /// </summary>
    private const string GenericPro = "Armor Piercing";

    private static async Task<string> Run(Harness harness)
    {
        await harness.Open("/build");

        var tierCards = harness.Page.Locator(".cards button");
        Harness.Control(await tierCards.CountAsync() > 0, "the tier page offered nothing to click");

        await tierCards.First.ClickAsync();

        await harness.WaitFor(
            "[...document.querySelectorAll('button')].some(b => b.querySelector('.name') "
            + "&& b.querySelector('.name').textContent.includes('Selected'))",
            "a tier card to report itself selected", 15_000);

        await harness.Open("/build/characteristics");

        var powersTab = harness.Page.Locator(".tabs button:has-text(\"Powers\")");
        Harness.Control(await powersTab.CountAsync() == 1,
            $"expected exactly one Powers tab, found {await powersTab.CountAsync()}");

        await powersTab.ClickAsync();

        await harness.WaitFor(
            "document.querySelector('.options-count') "
            + "&& /\\d+ of \\d+/.test(document.querySelector('.options-count').textContent)",
            "the Powers list to report a count", 15_000);

        var countText = await harness.Eval<string?>(
            "document.querySelector('.options-count')?.textContent ?? null");

        var match = System.Text.RegularExpressions.Regex.Match(countText ?? "", @"(\d+) of (\d+)");

        Harness.Control(match.Success,
            $"the Powers list's count box read \"{countText}\", not a \"n of n\" count");

        // Not exactly 141 — the markup never prints the rulebook's own figure, and asserting the
        // exact number here would make this check the thing that goes stale when a 142nd Power is
        // added, rather than RulesFixture.PowerCount in the engine's own suite, which already is.
        var total = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        Harness.Control(total > 100,
            $"the Powers list reported only {total} Powers, not the rulebook's full catalogue");

        // Scoped to the exact row rather than a substring match: two other Powers' descriptions
        // mention "strike" in lower case, which Playwright's :has-text() would match too, and
        // GetByText(..., Exact: true) does not.
        var strikeRow = harness.Page.Locator("button.option").Filter(new LocatorFilterOptions
        {
            Has = harness.Page.GetByText(PowerName, new PageGetByTextOptions { Exact = true }),
        });

        Harness.Control(await strikeRow.CountAsync() == 1,
            $"expected exactly one Power row named {PowerName}, found {await strikeRow.CountAsync()}");

        await strikeRow.ClickAsync();

        await harness.WaitFor(
            $"[...document.querySelectorAll('h2')].some(h => h.textContent.trim() === '{PowerName}')",
            $"the Power editor to open on {PowerName}", 15_000);

        var addProButton = harness.Page.Locator("button:has-text(\"Add a Pro\")");
        Harness.Control(await addProButton.CountAsync() == 1,
            $"expected one \"Add a Pro\" control on {PowerName}'s editor, "
            + $"found {await addProButton.CountAsync()}");

        await addProButton.ClickAsync();

        var prosText = await harness.Eval<string>("document.body.textContent");

        foreach (var name in OwnPros)
        {
            Harness.Outcome(prosText.Contains(name, StringComparison.Ordinal),
                $"opening {PowerName} and expanding its Pro list, \"{name}\" — one of its own "
                + "printed Pros — was not on the page");
        }

        Harness.Outcome(prosText.Contains(GenericPro, StringComparison.Ordinal),
            $"opening {PowerName} and expanding its Pro list, the generic Pro \"{GenericPro}\" "
            + "was not on the page");

        var addConButton = harness.Page.Locator("button:has-text(\"Add a Con\")");
        Harness.Control(await addConButton.CountAsync() == 1,
            $"expected one \"Add a Con\" control on {PowerName}'s editor, "
            + $"found {await addConButton.CountAsync()}");

        await addConButton.ClickAsync();

        var consText = await harness.Eval<string>("document.body.textContent");

        foreach (var name in OwnCons)
        {
            Harness.Outcome(consText.Contains(name, StringComparison.Ordinal),
                $"opening {PowerName} and expanding its Con list, \"{name}\" — one of its own "
                + "printed Cons — was not on the page");
        }

        return $"{total} Powers listed; {PowerName}'s own Pros, Cons and a generic Pro all "
            + "printed their names";
    }
}
