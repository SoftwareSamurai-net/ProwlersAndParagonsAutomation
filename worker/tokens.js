// Minting a sign-in token, and the one place its link is built.
//
// **One mechanism, two lifetimes.** A token is single-use and time-limited by construction —
// `login_tokens.expires_at` is set per row at mint time and `used_at` burns it on first spend
// (`db.spendLoginToken`) — so a link that should last longer is a longer `expiresAt`, never a
// second kind of token or a second table. `worker/auth.js` mints one for the public request path
// at `TOKEN_LIFETIME_MS`; `worker/invitations.js` mints one for a freshly-added address at the
// longer `INVITATION_TOKEN_LIFETIME_MS`. Both go through the functions here, so there is one
// place that hashes a token and one place that builds the URL it travels in.

import { hash } from './crypto.js';
import * as db from './db.js';

/**
 * Mint a token, store only its hash, and hand back the raw token — the only place it exists
 * outside whichever mailbox it is sent to. Nothing here decides who receives it or how long it
 * lives; both are the caller's decision.
 */
export async function mintSignInToken(env, deps, { email, now, lifetimeMs }) {
    const token = deps.newSecret();

    await db.putLoginToken(env.DB, {
        tokenHash: await hash(token),
        email,
        expiresAt: now + lifetimeMs,
    });

    return token;
}

/**
 * Where a token signs somebody in.
 *
 * <p>The one place this URL is built — a probe or a second mail path that assembled its own
 * would be a second place to keep the query parameter's name in step with `auth.verify`.</p>
 *
 * <p>Falls back to a bare `/signin` if the deployment has no `SITE_URL` rather than throwing: the
 * public request path already refuses outright on a missing `SITE_URL` before it ever reaches
 * this function (`auth.requestLink`'s own `configurationFailure`), so this only runs there with
 * the setting present. A caller that reaches this without one gets a relative link rather than a
 * crash.</p>
 */
export function signInLink(env, token) {
    const site = typeof env.SITE_URL === 'string' ? env.SITE_URL.replace(/\/+$/, '') : '';

    return site + '/signin?t=' + token;
}
