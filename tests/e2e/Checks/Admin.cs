namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// An ordinary account is refused the page that manages who may have one.
///
/// <para><b>This is the check that is easiest to get vacuous, and the plan for stage two said so
/// before it was written.</b> "Assert the admin list is not shown" passes against a page that never
/// loaded, against a browser that is not signed in at all, and against a server that answered 500
/// to everything — three ways of being green while measuring nothing. Two of the four historical
/// guard faults in this repository were exactly that shape.</para>
///
/// <para><b>So the control is three separate claims, each of which can fail on its own.</b> The
/// application signed us in and says whose account it is (<see cref="Account.SignIn"/>); the admin
/// address rendered a page rather than a boot screen or an error; and the page settled on one of
/// its own three refusals rather than on "Looking…", which is what it shows while the answer is
/// still in flight and which would otherwise pass for a refusal.</para>
///
/// <para><b>Outcome</b>: the refusal is "Not this account" specifically — the one the server gives
/// a signed-in reader who may not manage the list. "Sign in first" is a different sentence for a
/// different state, and accepting either would let a check that lost its session report a pass.
/// </para>
///
/// <para><b>The twin turns this one red on its outcome, which is the stronger kind.</b>
/// `seeded-account-is-the-owner` seeds the invitation with `grants_admin = 1`, so
/// `worker/invitations.js`'s `isAdministrator` says yes and the page renders the list. Nothing
/// about the browser, the session or the page changes — only whether this account is allowed —
/// which is as close to the real defect as a negative control gets.</para>
/// </summary>
public static class Admin
{
    public static Check Check => new("ADMIN", Run);

    /// <summary>What `GateRefusal` shows a signed-in reader the page is not for.</summary>
    private const string NotForThisAccount = "Not this account";

    /// <summary>The panel the page draws once the server has let somebody in.</summary>
    private const string TheList = "Invited";

    private static async Task<string> Run(Harness harness)
    {
        await Account.SignIn(harness);

        await harness.Open("/admin");

        // The page asks the server on render and shows "Looking…" until it answers. Settling is
        // the control: a check that read the titles immediately would see neither the list nor a
        // refusal, and "the list is absent" is exactly what this check must not accept as a pass.
        try
        {
            await harness.WaitFor(
                $"{Account.PanelTitles}.some(t => t === '{TheList}' || t === '{NotForThisAccount}' "
                + "|| t === 'Sign in first' || t === 'Could not reach the site')",
                "the admin page to settle on an answer from the server", 20_000);
        }
        catch (CheckFailedException)
        {
            var titles = await harness.Eval<string[]>(Account.PanelTitles);

            throw new ControlFailedException(
                "/admin never settled — it is still showing "
                + $"[{string.Join(", ", titles)}]. Until it does, \"the list is not there\" is a "
                + "page that has not finished loading rather than a refusal.");
        }

        var settled = await harness.Eval<string[]>(Account.PanelTitles);

        // Named apart from the outcome below because it is a different bug report: this is the
        // session having been lost between signing in and arriving here, which would make the
        // refusal true and meaningless.
        Harness.Control(!settled.Contains("Sign in first"),
            "/admin refused with \"Sign in first\", so the session did not survive the navigation "
            + "— the refusal below would be about nobody being signed in rather than about this "
            + "account not being the owner");

        Harness.Control(!settled.Contains("Could not reach the site"),
            "/admin could not reach the server at all, so nothing here says what it would have "
            + "answered");

        Harness.Outcome(!settled.Contains(TheList),
            $"an ordinary account was shown the \"{TheList}\" list. The server decides this on "
            + "every request — `invitations.isAdministrator` — so this is the gate itself letting "
            + "somebody through, not a page drawing the wrong thing.");

        Harness.Outcome(settled.Contains(NotForThisAccount),
            $"/admin settled on [{string.Join(", ", settled)}] rather than \"{NotForThisAccount}\"");

        return $"signed in as an ordinary account, /admin answered \"{NotForThisAccount}\"";
    }
}
