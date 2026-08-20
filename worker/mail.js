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
        // The provider's body can echo the address, so it is not put in the message. The status
        // is what somebody reading logs needs; the rest is in Resend's own dashboard.
        throw new Error('The mail provider refused the send (HTTP ' + response.status + ').');
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
