using Microsoft.Playwright;

namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// A character can be built, and it survives a reload.
///
/// <para><b>Control</b>: the application itself wrote to local storage. The index key is read
/// before and after, and the check requires it to have gone from holding nothing to naming this
/// character — <em>by the app's own hand</em>, from two clicks and some typing. This is the exact
/// control that would have caught the defect <c>PROGRESS.md</c> item 10 records: a manager, a
/// switcher and two undo buffers all reading an index nothing ever wrote to.</para>
///
/// <para><b>Outcome</b>: after a reload the tier and the name are still there.</para>
/// </summary>
public static class Build
{
    /// <summary>
    /// The character this check builds, and the tier it builds it at.
    ///
    /// <para><b>Not the default tier.</b> A check that picks whatever was already selected cannot
    /// tell a click that landed from a click that did nothing, which is the same vacuity as a
    /// control that never fires. <c>High Level</c> is 150 Hero Points against <c>Standard</c>'s
    /// 125, so the budget strip moves too.</para>
    /// </summary>
    private const string Name = "Harness Test Hero";

    private const string Tier = "High Level";
    private const string TierPoints = "150";

    /// <summary>The local-storage key the application writes the character index under. Read here, never written.</summary>
    private const string IndexKey = "pp.character.v1.index";

    public static Check Check => new("BUILD", Run);

    private static async Task<string> Run(Harness harness)
    {
        await harness.Open("/build");

        var before = await harness.Eval<string?>($"localStorage.getItem('{IndexKey}')");
        Harness.Control(before is null || !before.Contains(Name, StringComparison.Ordinal),
            "this browser already knew about the harness character before it was built");

        var tierCards = harness.Page.Locator(".cards button");
        Harness.Control(await tierCards.CountAsync() > 0, "the tier page offered nothing to click");

        // A real click — ILocator.ClickAsync dispatches an actual mouse event at the element's
        // centre, through the browser, and is retried while the element is not yet actionable.
        // `:has-text()` is a substring match, and "High Level" is not a substring of any of the
        // other five tier names, so it picks the one card unambiguously.
        var tierCard = harness.Page.Locator($".cards button:has-text(\"{Tier}\")");
        await tierCard.ClickAsync();

        await harness.WaitFor(
            "[...document.querySelectorAll('button')].some(b => b.querySelector('.name') "
            + $"&& b.querySelector('.name').textContent.includes('{Tier}') "
            + "&& b.querySelector('.name').textContent.includes('Selected'))",
            $"the {Tier} card to report itself selected", 15_000);

        await harness.Open("/build/finishing");

        var nameField = harness.Page.Locator("#ft-name");
        await nameField.ClickAsync();
        await nameField.PressSequentiallyAsync(Name);

        await harness.WaitFor($"document.querySelector('#ft-name').value === '{Name}'",
            "the name field to hold what was typed", 10_000);

        // The autosave is a fire-and-forget continuation on every change, so this waits for the
        // write rather than assuming it has already happened. A lapsed wait here is a real
        // finding: it means nothing in the application wrote the character down.
        string? stored;

        try
        {
            await harness.WaitFor(
                $"(() => {{ const raw = localStorage.getItem('{IndexKey}'); "
                + $"return !!(raw && raw.includes('{Name}')); }})()",
                "the application to write the character to local storage", 20_000);
            stored = await harness.Eval<string?>($"localStorage.getItem('{IndexKey}')");
        }
        catch (CheckFailedException)
        {
            stored = null;
        }

        Harness.Control(stored is not null,
            "the application never wrote this character to local storage");

        await harness.Reload();
        await harness.WaitForApp();

        var name = await harness.Eval<string?>("document.querySelector('#ft-name')?.value ?? null");
        Harness.Outcome(name == Name, $"after a reload the name field held \"{name}\"");

        await harness.Open("/build");

        var selected = await harness.Eval<bool>(
            "[...document.querySelectorAll('button')].some(b => b.querySelector('.name') "
            + $"&& b.querySelector('.name').textContent.includes('{Tier}') "
            + "&& b.querySelector('.name').textContent.includes('Selected'))");
        Harness.Outcome(selected, $"after a reload the {Tier} tier was no longer selected");

        // **The budget strip and not the page text, because the page text was a vacuous
        // assertion.** The first version of this check (in `scripts/e2e/drive.mjs`) asked whether
        // "150" appeared anywhere in `document.body.textContent` — and the tier list on this page
        // prints all six tiers' point totals whatever is selected, so it would have passed against
        // a character that restored nothing at all. This reads the strip's own `/ {budget}`, which
        // is the *session's* figure and comes from the restored tier.
        var strip = await harness.Eval<string?>(
            "document.querySelector('.budget-of')?.textContent?.trim() ?? null");
        Harness.Outcome(strip is not null,
            "after a reload there was no Hero Point budget strip on the page");
        Harness.Outcome(strip!.Contains(TierPoints, StringComparison.Ordinal),
            $"after a reload the budget strip read \"{strip}\" rather than the {TierPoints} "
            + $"the {Tier} tier sets");

        return $"built at {Tier}, named, and survived a reload";
    }
}
