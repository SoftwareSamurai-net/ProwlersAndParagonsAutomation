namespace ProwlersAndParagons.E2e;

/// <summary>
/// Signing in, the way somebody coming back from their mail does.
///
/// <para><b>This does not reach past the browser, and the distinction is the whole of why stage two
/// did not need a seam in the application.</b> The one thing arranged outside the browser is a row
/// in the local database — a `login_tokens` row holding the SHA-256 of a token, which is exactly
/// what `worker/tokens.js` writes when the server mails somebody a link. `scripts/e2e/seed.mjs`
/// writes it and hands this driver the raw token through the environment; from there everything is
/// a real navigation to a real address, and the application runs its own verify path: hash lookup,
/// expiry test, single-use burn, the invitation gate, the session cookie.</para>
///
/// <para>So the harness fakes an <em>email</em>, not a session. Nothing is bypassed, nothing is
/// compiled out of the shipped bundle, and there is no localhost-only branch anywhere for a test to
/// have to assert the absence of.</para>
///
/// <para><b>The token arrives in the environment rather than on the command line</b> because
/// `scripts/e2e.sh` builds the driver's argument array once and reuses it for every drive, and
/// because a secret on a command line is a secret in every process listing. It is a throwaway
/// credential to a throwaway local database either way; the habit is cheap.</para>
/// </summary>
public static class Account
{
    /// <summary>What <c>scripts/e2e.sh</c> puts the seeded raw token in.</summary>
    public const string TokenVariable = "PP_E2E_SIGNIN_TOKEN";

    /// <summary>
    /// The name the signed-in banner shows: `worker/auth.js` takes it from the local part of the
    /// address, and `scripts/e2e/seed.mjs` seeds `player@e2e.invalid`.
    /// </summary>
    public const string SeededDisplayName = "player";

    /// <summary>The panel titles a page is currently showing.</summary>
    public const string PanelTitles =
        "[...document.querySelectorAll('.panel-head h2, .panel-head h3')]"
        + ".map(h => h.textContent.trim())";

    /// <summary>
    /// Spend the seeded token and come back signed in.
    ///
    /// <para><b>The positive control is that the application says who we are</b>, read off the
    /// sign-in page's own "Signed in as …" panel rather than off a cookie. A cookie proves a
    /// `Set-Cookie` header arrived; this proves the app asked the server who is calling and was
    /// told. Those are different claims and only the second is the one a signed-in check needs.
    /// </para>
    ///
    /// <para>Throws <see cref="ControlFailedException"/> if the sign-in did not happen, because for
    /// every check that calls this, signing in is the work rather than the outcome — a twin that
    /// seeds an expired token must report "the work did not happen" and not "the book was
    /// wrong".</para>
    /// </summary>
    public static async Task SignIn(Harness harness)
    {
        var token = Environment.GetEnvironmentVariable(TokenVariable);

        Harness.Control(!string.IsNullOrWhiteSpace(token),
            $"{TokenVariable} was not set, so this driver was never given a token to spend. "
            + "scripts/e2e.sh seeds one immediately before each drive; a run started by hand needs "
            + "to do the same.");

        // `?t=` and not `?token=`: `worker/tokens.js`'s `signInLink` builds the link and
        // `web/Pages/SignIn.razor` reads the parameter under that name. Getting it wrong is silent
        // — the page shows its ordinary form, the token stays unspent, and every signed-in check
        // goes red with nothing anywhere saying why.
        await harness.Open($"/signin?t={token}");

        // The page spends the token, then replaces its own address to take the secret out of the
        // history — so waiting on the panel rather than on the URL is what says the spend finished.
        try
        {
            await harness.WaitFor(
                $"{PanelTitles}.includes('Signed in')",
                "the sign-in page to report an account", 20_000);
        }
        catch (CheckFailedException)
        {
            var titles = await harness.Eval<string[]>(PanelTitles);

            throw new ControlFailedException(
                "spending the seeded token did not sign anybody in — /signin settled on "
                + $"[{string.Join(", ", titles)}]. The application ran its real verify path, so "
                + "this is the row the harness seeded being unusable (expired, already spent, or "
                + "for an address with no invitation) or the server having no database bound.");
        }

        var who = await harness.Eval<string?>(
            "document.querySelector('.panel-head h2') && "
            + "[...document.querySelectorAll('p strong')].map(e => e.textContent.trim())[0]");

        Harness.Control(who == SeededDisplayName,
            $"signed in as \"{who}\" rather than \"{SeededDisplayName}\", so the session belongs "
            + "to somebody the harness did not seed");
    }
}
