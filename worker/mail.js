// Sending the magic link. One provider, one call, no SDK.

/**
 * Hands the link to Resend.
 *
 * The key is a Cloudflare secret and is never in this repository. `wrangler secret put
 * RESEND_API_KEY` is the only place it exists outside the dashboard — see
 * `docs/ACCOUNTS-SETUP.md`.
 *
 * Failure throws, and the caller decides what to do about it. It deliberately does not return
 * a boolean: a send that quietly failed is a person staring at an inbox, and that is worth a
 * 500 rather than the cheerful 204 the endpoint otherwise gives.
 */
export async function sendSignInLink(env, { to, link }) {
    const response = await fetch('https://api.resend.com/emails', {
        method: 'POST',
        headers: {
            authorization: `Bearer ${env.RESEND_API_KEY}`,
            'content-type': 'application/json',
        },
        body: JSON.stringify({
            from: env.MAIL_FROM,
            to: [to],
            subject: 'Your sign-in link',
            // Plain text as well as HTML: a link that exists only inside markup is a link some
            // mail clients will not show at all.
            text: 'Open this link to sign in. It works once and expires in 15 minutes.\n\n'
                + link
                + '\n\nIf you did not ask to sign in, ignore this — nothing has happened to any account.',
            html: '<p>Open this link to sign in. It works once and expires in 15 minutes.</p>'
                + '<p><a href="' + escapeHtml(link) + '">Sign in</a></p>'
                + '<p style="color:#666">If you did not ask to sign in, ignore this — nothing has '
                + 'happened to any account.</p>',
        }),
    });

    if (!response.ok) {
        // **The status alone was not enough, and the one time it mattered it cost a day.** A
        // refused send used to be reported as "HTTP 400" and nothing else, on the reasoning that
        // the provider's body can echo the address and the rest is in their dashboard. Both
        // halves of that are true and the conclusion was still wrong: a 400 is a validation
        // error, which is a sentence about *which field* the provider would not take — and a
        // refusal at validation never reaches the dashboard at all, so the place the reader was
        // sent to is empty precisely when this fires.
        //
        // So the provider's own code for the refusal goes in the message, and nothing else does.
        // `refusalName` is what keeps that promise: it takes the `name` field only, and only when
        // it looks like a code rather than like data. That is not a formatting nicety — an
        // address cannot pass it, because an address contains an `@` and a dot and the pattern
        // admits neither.
        const name = await refusalName(response);

        throw new Error('The mail provider refused the send (HTTP ' + response.status
            + (name ? ', ' + name : '') + ').');
    }
}

/**
 * The provider's own code for a refusal, or null.
 *
 * <p>Resend answers a refusal with `{ name, message, statusCode }`, where `name` is a fixed
 * machine code — `validation_error`, `missing_api_key`, `restricted_api_key` — and `message` is
 * prose that can quote what was sent. Only the first is taken, and only when it matches the
 * shape of a code: lower-case letters and underscores, and short. Anything else is dropped
 * rather than trimmed, because a value that is not a code is data, and data is the thing that
 * must not reach a log line.</p>
 *
 * <p><b>Nothing here may throw.</b> It runs while reporting a failure, so a body that is not
 * JSON, or is empty, or has already been read, has to produce a missing code — not a second
 * exception on top of the first, which would replace a diagnosable refusal with a stack trace
 * about parsing.</p>
 */
async function refusalName(response) {
    try {
        const body = await response.json();
        const name = body?.name;

        return typeof name === 'string' && /^[a-z][a-z_]{0,63}$/.test(name) ? name : null;
    } catch {
        return null;
    }
}

/**
 * The link goes into an attribute and a token is base64url, so this can only ever be a no-op
 * today. It is here because the day somebody puts a name or a return path into this message is
 * the day it stops being one, and the omission would not look like anything.
 */
function escapeHtml(text) {
    return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;');
}
