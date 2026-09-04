namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// The rulebook is gated, and an account gets through the gate.
///
/// <para><b>This is the first thing in this repository that has ever driven a signed-in session.</b>
/// Every check before it was anonymous by construction — `scripts/e2e.sh` served the site from a
/// directory with no `functions/` in it, so there was no server to sign in to. `PROGRESS.md` item
/// 10 listed "anything behind sign-in" as the largest open square in its own table.</para>
///
/// <para><b>Control, in two halves, and the first half is what makes the second worth reading.</b>
/// Anonymous, <c>/rules</c> must refuse: the page shows its "Sign in first" panel and no book. That
/// is what proves the prose asserted below came from the gated server rather than having been in
/// the published bundle the whole time — a check that only ever looked after signing in would pass
/// just as happily against a rulebook shipped to everybody, which is the exact leak this gate
/// exists to prevent. Then the seeded token is spent and the application reports an account.</para>
///
/// <para><b>Outcome</b>: the same address now renders the book — chapters listed with their printed
/// pages, and a search that comes back with a passage. Both are answered by `worker/rulebook.js`
/// out of a corpus bundled into the server, so neither can be satisfied by anything static.</para>
///
/// <para><b>What the twin proves and what it does not.</b> `signin-token-already-expired` seeds a
/// token that is already past its expiry, so `db.spendLoginToken` refuses it and this check goes
/// red on its control. That watches the sign-in half fail; it does not watch the *reading* half
/// fail, because the published site holds no line whose substitution could break a server-side
/// answer. `scripts/e2e/defects.mjs` says so in its header rather than leaving the gap implied.
/// </para>
/// </summary>
public static class Rules
{
    public static Check Check => new("RULES", Run);

    /// <summary>
    /// A word the book certainly contains, and one whose absence would be a rules-data fault rather
    /// than a harness one.
    /// </summary>
    private const string Query = "knockback";

    private static async Task<string> Run(Harness harness)
    {
        // ---- Control, half one: anonymous, the book is refused. ------------------------------
        await harness.Open("/rules");

        var anonymous = await harness.Eval<string[]>(Account.PanelTitles);

        Harness.Control(anonymous.Contains("Sign in first"),
            $"anonymous, /rules showed [{string.Join(", ", anonymous)}] rather than refusing. "
            + "Either the gate is gone — which is a real defect and the reason this half exists — "
            + "or a session from an earlier drive is still live, which makes everything below "
            + "vacuous.");

        Harness.Control(!anonymous.Contains("Search the book"),
            "anonymous, /rules rendered the search panel, so the book is reachable without an "
            + "account");

        // ---- Control, half two: sign in the way a link does. ---------------------------------
        await Account.SignIn(harness);

        // ---- Outcome: the same address, now with the book behind it. -------------------------
        await harness.Open("/rules");

        try
        {
            await harness.WaitFor($"{Account.PanelTitles}.includes('What is here')",
                "the chapter list to arrive from the server", 20_000);
        }
        catch (CheckFailedException)
        {
            var titles = await harness.Eval<string[]>(Account.PanelTitles);

            throw new CheckFailedException(
                "signed in, /rules never listed the book's chapters — it settled on "
                + $"[{string.Join(", ", titles)}]. The account exists (the control above proved "
                + "it), so this is /api/rulebook/contents refusing or answering nothing.");
        }

        // `.chosen > li` is the row `ChosenRow` renders. Counted before the search runs, when the
        // only list on the page is the chapter list — the results arrive in a second one, and
        // counting after would be counting both.
        var chapters = await harness.Eval<int>("document.querySelectorAll('.chosen > li').length");

        Harness.Outcome(chapters > 0,
            "the chapter panel rendered with no chapters in it, so the server answered with an "
            + "empty book");

        // A search, because the contents list is one route and the reading path is another — and
        // the reading path is the one a person at a table actually uses. Typed and submitted
        // rather than fetched: nothing may reach past the browser.
        await harness.Page.FillAsync("#rules-search", Query);
        await harness.Page.ClickAsync("form:has(#rules-search) button[type=submit]");

        // "What the book says" is the results panel's own title — `RulesReference.Answering`, and
        // the unscoped spelling of it, since this searches the whole book rather than a chapter.
        // Waiting on the title rather than on a row is deliberate: the panel is only rendered at
        // all when the server came back with `Found > 0`, so its presence is the answer.
        try
        {
            await harness.WaitFor(
                $"{Account.PanelTitles}.includes('What the book says')",
                $"a result panel for \"{Query}\"", 20_000);
        }
        catch (CheckFailedException)
        {
            var titles = await harness.Eval<string[]>(Account.PanelTitles);

            throw new CheckFailedException(
                $"searching the book for \"{Query}\" produced no answering panel — the page shows "
                + $"[{string.Join(", ", titles)}]. /api/rulebook/search is the gated route this "
                + "half exercises, and the page prints \"nothing found\" rather than a panel when "
                + "the server answers with no matches.");
        }

        return $"anonymous /rules refused, signed in it served {chapters} chapters and answered a "
            + $"search for \"{Query}\"";
    }
}
