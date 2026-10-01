namespace ProwlersAndParagons.E2e;

/// <summary>
/// Signing a reader in the way their mail would have, and nothing more than that.
///
/// <para><b>What this driver is handed is a raw sign-in token and the address it was minted for.</b>
/// <c>scripts/e2e/seed.mjs</c> writes the row — only the SHA-256 of the token, which is all
/// <c>worker/tokens.js</c> ever stores — into the <em>local</em> D1 that <c>scripts/e2e.sh</c>
/// migrated and started the server against. From there this is an ordinary navigation to
/// <c>/signin?t=…</c>, and the application runs its own verify path: hash lookup, expiry test,
/// single-use burn, invitation check, session cookie. <b>Nothing is bypassed and nothing is faked
/// but the row, which is what an email would have caused.</b></para>
///
/// <para><b>Not knowing where the row came from is the point.</b> A driver that wrote to a database
/// would have stopped being a thing that only knows a URL. The shell owns the wrangler version,
/// the database id and the <c>--persist-to</c> directory already; it hands each drive the token in
/// the environment, and a check treats it as opaque.</para>
///
/// <para><b>A missing token is a failure and never a skip.</b> A signed-in check that quietly did
/// nothing because its environment was empty would be the exact shape this whole harness exists to
/// stop: a green verdict over work that never happened.</para>
/// </summary>
public static class Account
{
    /// <summary>
    /// The token and address <c>scripts/e2e/seed.mjs</c> minted for one slot.
    /// </summary>
    /// <param name="slot">
    /// <c>ADMIN</c>, <c>RULES</c>, <c>SAVE_1</c> or <c>SAVE_2</c> — the names <c>seed.mjs</c>'s own
    /// <c>SLOTS</c> table declares. Each is a token of its own because a sign-in token is
    /// single-use by construction, which is a property of the application and not of the harness.
    /// </param>
    public static (string Token, string Email, string DisplayName) Seeded(string slot)
    {
        var token = Environment.GetEnvironmentVariable($"PP_E2E_{slot}_TOKEN");
        var email = Environment.GetEnvironmentVariable($"PP_E2E_{slot}_EMAIL");

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email))
        {
            // **Neither a control failure nor an outcome one: this is the harness not having been
            // arranged.** `Runner` reports anything that is not one of its two assertion types as
            // `[HARNESS]`, and says in its own comment that a twin whose only red verdict is a
            // HARNESS one has not been watched to fail for the reason it claims. That is exactly
            // the right reading here — a drive with no tokens proves nothing either way — and it
            // still says FAIL, because a verdict that is not printed is the failure shape this
            // whole protocol exists to avoid.
            throw new InvalidOperationException(
                $"no sign-in token was seeded for {slot}: PP_E2E_{slot}_TOKEN and "
                + $"PP_E2E_{slot}_EMAIL are what scripts/e2e.sh puts in front of a drive after it "
                + "has migrated and seeded the local D1. Run this through ./scripts/e2e.sh rather "
                + "than against a bare URL — a signed-in check with nothing to sign in as is a "
                + "failure and must never be a skip.");
        }

        // What `worker/auth.js` names an account on its first sign-in: the address up to the `@`.
        // Derived rather than seeded as a third variable, because a name that disagreed with the
        // address would make every "signed in as somebody" assertion below quietly weaker.
        return (token, email, email[..email.IndexOf('@', StringComparison.Ordinal)]);
    }

    /// <summary>
    /// Drive a seeded link and report whether it left the reader signed in. <b>Does not throw.</b>
    ///
    /// <para><b>The banner is what is read, and it is read on purpose rather than the sign-in
    /// page's own panel.</b> Every page in this application carries it, so the same assertion works
    /// wherever a check goes next — and a check that asserts a page refused it needs to be able to
    /// say, on <em>that</em> page, that the reader was signed in as somebody while it did.</para>
    ///
    /// <para><b>Silent because a caller may want to judge the answer somewhere else</b>, which is
    /// not fastidiousness about where a message comes from. <see cref="SignIn"/> below throws here,
    /// which is right for a check whose subject is another page entirely — but it means a twin that
    /// breaks the sign-in dies at a line every signed-in check shares, and everything the check
    /// went on to assert then has no negative control at all. <c>RULES</c> spends its link this way
    /// and judges it at <c>/rules</c> for exactly that reason; <c>Checks/Rules.cs</c> carries the
    /// measurement.</para>
    /// </summary>
    public static async Task<bool> Spend(Harness harness, string slot)
    {
        var (token, _, displayName) = Seeded(slot);

        await harness.Open($"/signin?t={Uri.EscapeDataString(token)}");

        try
        {
            await harness.WaitFor(
                $"document.querySelector('.banner-account')?.textContent?.trim() === "
                + $"{Quoted(displayName)}",
                $"the banner to name {displayName}, which is who the {slot} token was minted for",
                20_000);

            return true;
        }
        catch (CheckFailedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Spend a seeded link and come back signed in, or say the work did not happen.
    ///
    /// <para><b>It throws a control failure, not an outcome one.</b> A reader who could not be
    /// signed in has not been refused by anything; every sentence a check would go on to write
    /// about what they were shown is a sentence about a stranger.</para>
    ///
    /// <para><b>Use it where the sign-in is the <em>arrangement</em> and not the subject.</b> A
    /// check whose own assertions can distinguish a stranger from an account should call
    /// <see cref="Spend"/> instead and say so itself — otherwise a seed twin aimed at that check
    /// lands here, in a helper three checks share, and proves nothing about any of them.</para>
    /// </summary>
    public static async Task<string> SignIn(Harness harness, string slot)
    {
        var (_, email, displayName) = Seeded(slot);

        if (await Spend(harness, slot)) return displayName;

        var banner = await harness.Eval<string?>(
            "document.querySelector('.banner-account')?.textContent?.trim() ?? null");

        throw new ControlFailedException(
            $"the seeded {slot} link did not sign anybody in — the banner reads "
            + $"\"{banner}\" rather than \"{displayName}\" ({email}). The application spent the "
            + "token and refused it, which is what it does for a token that has expired, been "
            + "used, or belongs to an address the invitation list no longer carries.");
    }

    /// <summary>A string as a JavaScript literal, so a name with a quote in it cannot break a wait.</summary>
    private static string Quoted(string value) =>
        "'" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
                   .Replace("'", "\\'", StringComparison.Ordinal) + "'";

    /// <summary>Whether the banner still names this account. Read as a control, never waited on.</summary>
    public static async Task<bool> StillSignedInAs(Harness harness, string displayName) =>
        await harness.Eval<string?>(
            "document.querySelector('.banner-account')?.textContent?.trim() ?? null")
        == displayName;

    /// <summary>The whole of the rendered page's text, which is what a reader is actually told.</summary>
    public static Task<string> PageText(Harness harness) =>
        harness.Eval<string>("document.body.innerText");
}
