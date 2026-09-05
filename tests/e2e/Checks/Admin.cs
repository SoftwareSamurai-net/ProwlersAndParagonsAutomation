namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// A signed-in account that the invitation list does not make an administrator reaches
/// <c>/admin</c> and is told, plainly, that it is not theirs.
///
/// <para><b>This is the check that is easiest to get vacuous, and the brief that asked for it said
/// so.</b> "Not allowed" is satisfied by a page that failed to load at all, by a page that refused
/// because nobody was signed in, and by a page that never asked the server anything. Every one of
/// those would report the same green verdict as the behaviour actually under test. So the positive
/// control has three parts and all three come before the outcome:</para>
///
/// <list type="bullet">
///   <item>the seeded link signed somebody in — <see cref="Account.SignIn"/> reads the banner;</item>
///   <item><c>/admin</c> itself rendered, which is its own heading and not merely a shell;</item>
///   <item>the reader is <em>still</em> signed in as that account on this page, so the refusal
///     being read is the one given to an account rather than to a stranger.</item>
/// </list>
///
/// <para><b>Outcome</b>: the page says there is nothing here for this account, and does <em>not</em>
/// carry the list. Both halves, because a page that refused and also leaked the addresses would
/// satisfy the first on its own.</para>
///
/// <para><b>What makes the verdict mean something is the twin</b>, and it is a seeded row rather
/// than a broken site: <c>reader-is-an-administrator</c> signs the same check in as an account the
/// invitation list marks <c>grants_admin = 1</c>, and the page must then serve the list and turn
/// this check red. Nothing in the published bundle could have produced that — the rule lives in
/// <c>worker/invitations.js</c> — which is why <c>scripts/e2e/defects.mjs</c> grew a second kind of
/// defect rather than this check going untwinned.</para>
/// </summary>
public static class Admin
{
    /// <summary>What <c>Admin.razor</c> heads the page with, whichever way the gate answers.</summary>
    private const string Heading = "Who can sign in";

    /// <summary>The refusal, out of <c>GateRefusal.razor</c>'s <c>NotForYou</c> branch.</summary>
    private const string Refusal = "there is simply nothing here for this account";

    /// <summary>The refusal a reader who is not signed in at all gets. Must not be this one.</summary>
    private const string Stranger = "This page needs an account";

    /// <summary>The list itself, out of the <c>Loaded</c> branch. Must not be on screen.</summary>
    private const string TheList = "Everyone here can ask for a sign-in link";

    public static Check Check => new("ADMIN", Run);

    private static async Task<string> Run(Harness driver)
    {
        // **A context of its own, so this check cannot change what any other one sees.** Signing
        // the run's own browser in would leave a session cookie behind for whatever ran next, and
        // a check whose subject depends on what ran before it is not reproducible — which is the
        // fault `A11Y` had and the reason its position in the list is documented.
        var harness = await driver.FreshContext();

        var who = await Account.SignIn(harness, "ADMIN");

        await harness.Open("/admin");

        var heading = await harness.Eval<string?>(Harness.H1);
        Harness.Control(heading == Heading,
            $"/admin rendered \"{heading}\" rather than \"{Heading}\", so whatever is being read "
            + "below is not this page");

        Harness.Control(await Account.StillSignedInAs(harness, who),
            $"the banner on /admin no longer names {who}, so any refusal here is the one a "
            + "stranger gets and says nothing about an account that is not an administrator");

        // **Waited for, because "Looking…" is neither answer.** The gate is a request, and reading
        // the page before it comes back finds a third state that contains neither the refusal nor
        // the list — which would make the outcome below true for the wrong reason.
        await harness.WaitFor(
            $"document.body.innerText.includes({Literal(Refusal)}) "
            + $"|| document.body.innerText.includes({Literal(TheList)}) "
            + $"|| document.body.innerText.includes({Literal(Stranger)})",
            "the server to answer whether this account may manage the list", 20_000);

        var text = await Account.PageText(harness);

        Harness.Outcome(!text.Contains(Stranger, StringComparison.Ordinal),
            $"/admin refused {who} as though nobody were signed in. That is a different refusal "
            + "from the one this check exists for, and it would pass a check that only asked "
            + "whether the page said no");

        Harness.Outcome(text.Contains(Refusal, StringComparison.Ordinal),
            $"/admin did not tell {who} that the page is not theirs. What it said instead: "
            + $"\"{Excerpt(text)}\"");

        Harness.Outcome(!text.Contains(TheList, StringComparison.Ordinal),
            $"/admin served the invitation list to {who}, who the list does not make an "
            + "administrator");

        return $"signed in as {who}, who /admin rendered for and then refused by name";
    }

    /// <summary>A C# string as a JavaScript literal, for a wait that quotes it.</summary>
    private static string Literal(string value) =>
        "'" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
                   .Replace("'", "\\'", StringComparison.Ordinal) + "'";

    /// <summary>Enough of the page to tell one refusal from another in a failure line.</summary>
    private static string Excerpt(string text)
    {
        var flat = string.Join(" ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()));

        return flat.Length <= 220 ? flat : flat[..220] + "…";
    }
}
