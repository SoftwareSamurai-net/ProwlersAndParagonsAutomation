using System.Text.RegularExpressions;

namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// The rulebook is real prose to an account and a refusal to everybody else.
///
/// <para><b>Both halves are needed and neither is the whole check.</b> "The account can read it"
/// is satisfied by a rulebook that is simply public — which is the one thing this text must not be,
/// since it is here by the author's personal permission. "A stranger is refused" is satisfied by a
/// server that refuses everybody, including the reader who paid for an account with their address.
/// So one run does both, in two contexts, and reports the pair.</para>
///
/// <para><b>Control</b>: the seeded link signed somebody in; <c>/rules</c> rendered its own heading;
/// the banner still names them there; and the search actually ran — the page came back with a
/// result panel rather than being left in the state it started in.</para>
///
/// <para><b>Outcome</b>: at least one passage came back, and it carries a printed page citation,
/// which is the thing that makes a rules answer checkable against the copy on the table. Then, in a
/// context that has never signed in, the accounts server answers the rulebook prefix <c>401</c> and
/// the page says so in its own words.</para>
///
/// <para><b>The 401 is read off the wire rather than inferred from the sentence</b>, because a page
/// that never asked anybody would print the same sentence. That is a read and not a reach past the
/// browser — see <see cref="Harness.Responses"/>.</para>
///
/// <para><b>Why the anonymous half asks for the contents rather than typing a search.</b> An
/// anonymous reader never gets a box to type in: <c>RulesReference.razor</c> asks the server what
/// there is to read before rendering anything, and its own comment says why — "the server refuses
/// every address under the book's prefix the same way, so one refusal here is the answer for all
/// four". Driving a search would mean synthesising a request the application does not make, which
/// is reaching past the browser to prove a point the browser already made.</para>
/// </summary>
public static class Rules
{
    private const string Heading = "Rules reference";

    /// <summary>
    /// What is typed into the box.
    ///
    /// <para>A word the book certainly uses and that is not in the page's own furniture, so a
    /// result row cannot be the placeholder or a chapter title read back. The placeholder offers
    /// "knockback", which is deliberately not this.</para>
    /// </summary>
    private const string Query = "damage";

    /// <summary>The refusal, out of the page's <c>_refused</c> branch.</summary>
    private const string Refusal = "Reading the book needs an account";

    /// <summary>Every address the book is served under. What a stranger must be refused.</summary>
    private const string RulebookPrefix = "/api/rulebook/";

    /// <summary><c>Ch.4 p.72</c> — what <c>Cite</c> puts in each row's cost slot.</summary>
    private static readonly Regex Citation =
        new(@"Ch\.\d+ p\.\d+", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The panel a whole-book search fills, found by its own heading.
    ///
    /// <para><b>Scoped, and the first version of this check was not — which is exactly the vacuity
    /// this repository keeps shipping.</b> <c>/rules</c> draws two <c>ChosenList</c>s: the results,
    /// and "What is here", a list of the book's chapters that is on the page from the moment it
    /// loads and whose rows carry a page range in the same <c>.cost</c> slot. A bare
    /// <c>.chosen li .cost</c> therefore matched ten chapter rows before anything was typed, so the
    /// wait for a search to come back returned instantly and the citation assertion read
    /// <c>pp.5–8</c> off the table of contents. Measured, not imagined: it is what this check said
    /// the first time it was run.</para>
    ///
    /// <para><c>Answering</c> is the heading, and it is <c>What the book says</c> for a search that
    /// was not narrowed to a chapter — which this one is not.</para>
    /// </summary>
    private const string ResultsPanel =
        "[...document.querySelectorAll('section.panel')].find(s => "
        + "s.querySelector('.panel-head h2')?.textContent?.trim() === 'What the book says')";

    public static Check Check => new("RULES", Run);

    private static async Task<string> Run(Harness driver)
    {
        // A context of its own for each half, so neither the run's own browser nor the other half
        // can be what decides the answer. See the note in `Admin.cs`: a session cookie left behind
        // makes a later check's subject depend on what ran before it.
        var harness = await driver.FreshContext();

        var who = await Account.SignIn(harness, "RULES");

        await harness.Open("/rules");

        var heading = await harness.Eval<string?>(Harness.H1);
        Harness.Control(heading == Heading,
            $"/rules rendered \"{heading}\" rather than \"{Heading}\"");

        Harness.Control(await Account.StillSignedInAs(harness, who),
            $"the banner on /rules no longer names {who}, so this is not an account reading the "
            + "book");

        // The search box only exists once the server has said this account may read anything at
        // all, so its arrival is the first half of "the account was served the book".
        var box = harness.Page.Locator("#rules-search");

        try
        {
            await box.WaitForAsync(new() { Timeout = 20_000 });
        }
        catch (TimeoutException)
        {
            var refused = await Account.PageText(harness);

            throw new ControlFailedException(
                $"/rules never offered {who} a search box, so the book was not served to this "
                + $"account at all. The page says: \"{Excerpt(refused)}\"");
        }

        await box.ClickAsync();
        await box.PressSequentiallyAsync(Query);
        await box.PressAsync("Enter");

        // The results panel is drawn only once an answer has come back, and the page says in as
        // many words when the book has nothing — so either arriving means the search ran.
        await harness.WaitFor(
            $"!!({ResultsPanel}) || document.body.innerText.includes('Nothing in the book')",
            $"the server to answer a search for \"{Query}\"", 20_000);

        var rows = await harness.Eval<int>(
            $"(({ResultsPanel})?.querySelectorAll('.chosen li .cost').length ?? 0)");
        Harness.Control(rows > 0,
            $"a search for \"{Query}\" came back with no passages at all, so there is no prose "
            + "here to be right or wrong about");

        var citations = await harness.Eval<string>(
            $"[...(({ResultsPanel})?.querySelectorAll('.chosen li .cost') ?? [])]"
            + ".map(c => c.textContent.trim()).join(' | ')");

        Harness.Outcome(Citation.IsMatch(citations),
            $"no passage carried a printed page citation; the rows' cost slots read "
            + $"\"{citations}\". A rules answer that cannot be checked against the copy on the "
            + "table is the difference between this and remembering");

        var prose = await harness.Eval<int>(
            $"[...(({ResultsPanel})?.querySelectorAll('.chosen li .small.muted') ?? [])]"
            + ".filter(d => d.textContent.trim().length > 40).length");
        Harness.Outcome(prose > 0,
            $"{rows} passage(s) came back and none of them carried more than 40 characters of "
            + "text, so what was served is a list of headings rather than the book");

        // ------------------------------------------------------------------------------------
        // The other half: a reader who has not signed in.

        var stranger = await driver.FreshContext();

        await stranger.Open("/rules");

        Harness.Control(await stranger.Eval<string?>(Harness.H1) == Heading,
            "the anonymous context did not render /rules at all, so it was refused by nothing");

        Harness.Control(
            await stranger.Eval<string?>(
                "document.querySelector('.banner-account')?.textContent?.trim() ?? null")
            == "Sign in",
            "the second context was already signed in as somebody, so what it was served says "
            + "nothing about a stranger");

        await stranger.WaitFor(
            "document.body.innerText.includes('" + Refusal + "')"
            + " || !!document.querySelector('#rules-search')",
            "the server to answer an anonymous reader", 20_000);

        var refusals = stranger.ResponsesSoFar()
            .Where(r => r.Url.Contains(RulebookPrefix, StringComparison.Ordinal))
            .ToList();

        Harness.Control(refusals.Count > 0,
            $"the anonymous page never asked for anything under {RulebookPrefix}, so its sentence "
            + "about signing in is markup rather than an answer");

        Harness.Outcome(refusals.TrueForAll(r => r.Status == 401),
            $"the accounts server answered an anonymous reader "
            + $"{string.Join(", ", refusals.Select(r => $"{r.Status} for {Tail(r.Url)}"))} "
            + "rather than 401 for every rulebook address");

        var strangerText = await Account.PageText(stranger);
        Harness.Outcome(strangerText.Contains(Refusal, StringComparison.Ordinal),
            $"the anonymous page was refused by the server and did not say so; it reads "
            + $"\"{Excerpt(strangerText)}\"");

        return $"{rows} passage(s) with page citations for {who}, and "
            + $"{refusals.Count} rulebook address(es) answered 401 to a stranger who was told so";
    }

    private static string Tail(string url)
    {
        var at = url.IndexOf(RulebookPrefix, StringComparison.Ordinal);

        return at < 0 ? url : url[at..];
    }

    private static string Excerpt(string text)
    {
        var flat = string.Join(" ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()));

        return flat.Length <= 220 ? flat : flat[..220] + "…";
    }
}
