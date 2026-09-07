namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// A character built while signed in is on the <em>server</em>, and follows the account into a
/// browser that has never seen it.
///
/// <para><b>This is the check the whole harness was argued for.</b> <c>PROGRESS.md</c> item 10
/// records a feature that shipped here — built, tested, adversarially reviewed twice, merged —
/// while nothing in the application ever wrote to the store it read from, because every unit and
/// component test called the store directly. <c>BUILD</c> asks the same question of local storage.
/// This asks it of the account, which is the half a single browser cannot answer: whatever one
/// context finds might be what it left there itself.</para>
///
/// <para><b>Control</b>, in order, and each is a different way the run could be worth nothing:</para>
///
/// <list type="bullet">
///   <item>the first context signed in as the account that will own the character;</item>
///   <item>the application really put it on the wire — a <c>/api/characters</c> response came back
///     under 400, which is what "nothing ever wrote to the store" would not produce;</item>
///   <item>and it put <em>this</em> character on the wire: the shell says "Saved" while the whole
///     name stands in the field, which it does only while a completed write has reported the
///     version the character is at now. A name is typed one letter at a time, so without this the
///     check could open its second browser on the strength of the first letter having been
///     written — and a prefix found there could not be told apart from a harness reading early;</item>
///   <item>the second context began with local storage that has never heard of this character, so
///     anything it shows came over the network;</item>
///   <item>the second context signed in as somebody — <see cref="Account.SignIn"/> throws a control
///     failure if the seeded link left the banner naming nobody.</item>
/// </list>
///
/// <para><b>Outcome</b>: the wizard in the second context holds the character. One sentence, and
/// the ordering is the fix: an <c>Outcome(reader == owner)</c> used to run <em>before</em> the
/// navigation that asks it, so <c>second-context-is-another-account</c> went red on the identity
/// and never reached the wizard at all — deleting the navigation, the wait and the assertion
/// changed neither the real run nor the twin.</para>
///
/// <para><b>The identity assertion is gone rather than moved</b>, because against the real site it
/// could not fail: <c>SAVE_1</c> and <c>SAVE_2</c> are seeded for the same account at the same
/// address, so it was true by construction and could only ever fire in a twin. The leak it named is
/// caught better without it — <c>second-context-is-another-account</c> hands the second context a
/// token minted for a different invited account, and if the server ever served this character to
/// whoever asked, that twin would report <c>ACCOUNT_SAVE</c> <em>green</em> and <c>e2e.sh</c> would
/// fail the run on a twin that cannot turn its own check red. Which is the louder verdict of the
/// two, and it does not depend on a check reading a name it seeded itself.</para>
/// </summary>
public static class AccountSave
{
    /// <summary>
    /// The character, and a name of its own.
    ///
    /// <para>Deliberately not <c>BUILD</c>'s "Harness Test Hero": these two checks run in the same
    /// process against the same site, and a shared name would let one of them read the other's
    /// character and report it as its own.</para>
    /// </summary>
    private const string Name = "Account Bound Hero";

    /// <summary>Not <c>BUILD</c>'s tier either, for the same reason.</summary>
    private const string Tier = "Street Level";

    /// <summary>The local-storage key the application writes the character index under.</summary>
    private const string IndexKey = "pp.character.v1.index";

    public static Check Check => new("ACCOUNT_SAVE", Run);

    private static async Task<string> Run(Harness harness)
    {
        // **A context of its own, not the one every other check has used.** By the time this runs,
        // the browser is holding whatever `BUILD` left in local storage — and this check's whole
        // question is whether a character came from the account rather than from the browser.
        var first = await harness.FreshContext();
        var owner = await Account.SignIn(first, "SAVE_1");

        await Build(first, Name, Tier);

        // **The positive control that matters, and it is a write on the wire rather than a value
        // in this browser.** A signed-in reader's autosave goes to the account and not to local
        // storage — `AccountCharacterStore` picks one store per call — so "the application wrote
        // it down" cannot be read out of `localStorage` here at all, and asking there would be a
        // control that passes while nothing was saved anywhere.
        //
        // **A non-GET, because a GET would pass for free.** Signing in reads `/api/characters`
        // straight away; counting that as evidence of a write is exactly the shape of the defect
        // this check exists for — a feature reading a store nothing writes to.
        var (writes, said, onScreen) = await WaitForWrite(first);

        var asked = first.ResponsesSoFar()
            .Where(r => r.Url.Contains("/api/characters", StringComparison.Ordinal))
            .ToList();

        Harness.Control(writes.Count > 0,
            "the application never got a successful answer out of a write to /api/characters, so "
            + $"nothing was written to {owner}'s account at all. What it did ask: "
            + (asked.Count > 0
                ? string.Join(", ", asked.Select(r => $"{r.Method} -> {r.Status}"))
                : "nothing under /api/characters at all"));

        // **The second half of the control, and without it the outcome below is about this
        // harness's clock.** A write having been answered says a write happened; it does not say
        // *which* body was in it, and a name typed one character at a time is a run of writes. So
        // the finished name has to be the one the application is reporting: "Saved" appears when a
        // completed write reported the version the character is at right now — see
        // `CharacterSession.Saved` — so the word standing while the field holds the whole name is
        // the application saying this name, and not a prefix of it, reached the account.
        //
        // Asked here rather than assumed, because the difference is exactly the failure this
        // check reported once: a second browser was handed "Account Bound H". If that ever
        // happens again with this control satisfied, it is the account's answer and not the
        // harness reading too early — which is the whole reason to spend a wait on it.
        Harness.Control(said == "Saved" && onScreen == Name,
            $"the application never reported \"{Name}\" as saved to {owner}'s account: the name "
            + $"field held \"{onScreen}\" and the save status said \"{said}\". Opening a second "
            + "browser now would ask whether a character followed the account before the "
            + "application had said it went there, which is a question about this harness rather "
            + "than about the account");

        // ------------------------------------------------------------------------------------
        // A second browser, in effect: its own cookie jar, its own empty storage.

        var second = await harness.FreshContext();

        await second.Open("/");

        var before = await second.Eval<string?>($"localStorage.getItem('{IndexKey}')");
        Harness.Control(before is null || !before.Contains(Name, StringComparison.Ordinal),
            $"the second context already knew about \"{Name}\" before it signed in, so finding it "
            + "afterwards would say nothing about the server");

        var reader = await Account.SignIn(second, "SAVE_2");

        // **The outcome this check exists for, and it is first now.**
        //
        // An `Outcome(reader == owner)` used to sit above this navigation, and it swallowed the
        // twin whole: `second-context-is-another-account` signs the second context in as a
        // different invited account, so that line went red before anything below it ran, and
        // deleting the navigation, the wait and the assertion changed *neither* the real run nor
        // the twin. "The character followed the account" — the sentence the whole harness was
        // argued for — had no negative control.
        //
        // It is gone rather than reordered, because against the real site it could not fail:
        // `SAVE_1` and `SAVE_2` are seeded for the same account at the same address, so it was
        // true by construction and could only ever fire in a twin. **The leak it was there for is
        // caught better by the twin machinery**: if the server ever handed this character to
        // whoever asked, the twin below would find it, report ACCOUNT_SAVE green, and `e2e.sh`
        // would fail the run on a twin that cannot turn its own check red.
        //
        // Opening the account's character is what the sign-in page does on the way in — it reads
        // the account's own store and opens what it finds. Going to the wizard is a reader looking
        // at it, and the field is the application's own rendering of what came back.
        await second.Open("/build/finishing");

        string? held = null;

        try
        {
            await second.WaitFor(
                $"document.querySelector('#ft-name')?.value === {Literal(Name)}",
                $"{owner}'s character to come back from the account", 20_000);
            held = Name;
        }
        catch (CheckFailedException)
        {
            held = await second.Eval<string?>(
                "document.querySelector('#ft-name')?.value ?? null");
        }

        Harness.Outcome(held == Name,
            $"a fresh browser signed in as {reader} was shown \"{held}\" rather than \"{Name}\", "
            + $"which {owner} built and the application said it had written to the account. Either "
            + "the character was written to that browser rather than to the account — the defect "
            + "class this check exists for — or it did not follow the account here");

        return $"built as {owner} in one browser, and read back by {reader} in another that had "
            + "never held it";
    }

    /// <summary>
    /// Build a character by clicking and typing, which is the only way any of this may be arranged.
    ///
    /// <para>The same route <c>BUILD</c> takes and for the same reason: a check that reached the
    /// store by hand could not notice that nothing else reaches it.</para>
    /// </summary>
    private static async Task Build(Harness harness, string name, string tier)
    {
        await harness.Open("/build");

        var cards = harness.Page.Locator(".cards button");
        Harness.Control(await cards.CountAsync() > 0, "the tier page offered nothing to click");

        await harness.Page.Locator($".cards button:has-text(\"{tier}\")").ClickAsync();

        await harness.WaitFor(
            "[...document.querySelectorAll('button')].some(b => b.querySelector('.name') "
            + $"&& b.querySelector('.name').textContent.includes({Literal(tier)}) "
            + "&& b.querySelector('.name').textContent.includes('Selected'))",
            $"the {tier} card to report itself selected", 15_000);

        await harness.Open("/build/finishing");

        var field = harness.Page.Locator("#ft-name");
        await field.ClickAsync();
        await field.PressSequentiallyAsync(name);

        await harness.WaitFor($"document.querySelector('#ft-name').value === {Literal(name)}",
            "the name field to hold what was typed", 10_000);
    }

    /// <summary>
    /// Wait, bounded, for the application to write <em>this</em> character to the account, and
    /// report what it was answered, what the shell says about it, and what the field holds.
    ///
    /// <para><b>Two conditions, not one, and the second is the one that was missing.</b> The
    /// autosave is a fire-and-forget continuation on every change, so this polls what the browser
    /// was actually answered rather than assuming a write has landed — but a name typed one
    /// character at a time is a run of edits, and "some write was answered" is satisfied by the
    /// first letter. The shell's "Saved" is what distinguishes them: it stands only while a
    /// completed write reported the version the character is at now, so waiting for it, with the
    /// whole name in the field, is waiting for the finished name rather than for a prefix.</para>
    ///
    /// <para><b>Every arm returns rather than throwing, and the caller says what it found.</b> An
    /// empty list, an unsaid word and a half-typed field are all findings; none of them is a reason
    /// to wait for ever, and a timeout that reported nothing about the state it gave up in would
    /// leave the next reader guessing which of the three it was.</para>
    /// </summary>
    private static async Task<(List<(string Method, string Url, int Status)> Writes,
        string? Said, string? OnScreen)> WaitForWrite(Harness harness)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(20_000);

        while (true)
        {
            var writes = harness.ResponsesSoFar()
                .Where(r => r.Url.Contains("/api/characters", StringComparison.Ordinal)
                            && r.Method != "GET" && r.Method != "OPTIONS"
                            && r.Status < 400)
                .ToList();

            var said = await harness.Eval<string?>(
                "document.querySelector('.save-status')?.textContent.trim() ?? null");

            var onScreen = await harness.Eval<string?>(
                "document.querySelector('#ft-name')?.value ?? null");

            if ((writes.Count > 0 && said == "Saved" && onScreen == Name)
                || DateTime.UtcNow >= deadline)
            {
                return (writes, said, onScreen);
            }

            await Task.Delay(250);
        }
    }

    private static string Literal(string value) =>
        "'" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
                   .Replace("'", "\\'", StringComparison.Ordinal) + "'";
}
