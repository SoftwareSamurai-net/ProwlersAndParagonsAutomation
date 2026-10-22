// The two secrets this system mints, and the one-way function that keeps them out of the
// database. Nothing here is bespoke cryptography: it is WebCrypto, which the Workers runtime
// and Node both provide, used the obvious way.

import { configurationFailure } from './errors.js';

/**
 * A fresh secret, URL-safe, 256 bits of randomness.
 *
 * Used for both the magic-link token and the session cookie. `crypto.getRandomValues` is the
 * CSPRNG; `Math.random` is not one and must never appear in this directory.
 */
export function newSecret() {
    const bytes = new Uint8Array(32);
    crypto.getRandomValues(bytes);
    return base64url(bytes);
}

/**
 * SHA-256 of a secret, as lower-case hex.
 *
 * **This is what is stored, and the secret itself never is.** A dump of the database then
 * contains no key to anything: a login token and a session cookie are both bearer secrets, so
 * storing either in the clear would mean the database *is* the credential.
 */
export async function hash(secret) {
    const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(secret));

    return [...new Uint8Array(digest)].map(b => b.toString(16).padStart(2, '0')).join('');
}

// A constant-time comparison used to live here, and it is gone rather than kept "in case".
// Nothing compares a secret in this server: both are looked up by their hash, which is an index
// probe on a value the caller never sees. An unused security helper is worse than none — it
// reads as though a comparison somewhere is protected, and it is the obvious thing to reach for
// in the one place that would not need it. Write it back when something compares a secret.

/** An opaque user id. Not derived from the address — see the schema's note on `users.id`. */
export function newUserId() {
    return 'u_' + newSecret().slice(0, 22);
}

/**
 * An opaque invitation id.
 *
 * <p>Not derived from the address either, and here the reason is narrower than it is for a
 * user: this id travels in the URL of the request that withdraws an invitation, and an address
 * in a URL is an address in every log, history and referrer between here and the browser.</p>
 */
export function newInvitationId() {
    return 'i_' + newSecret().slice(0, 22);
}

function base64url(bytes) {
    return btoa(String.fromCharCode(...bytes))
        .replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '');
}

/**
 * An HMAC-SHA256 of `message` under `secret`, as base64url, truncated to 22 characters — the same
 * length this file already mints an id's random half from.
 *
 * **Not a token and nothing here ever compares one.** A login token and a session are bearer
 * secrets looked up by their hash; this is neither. It is a one-way function of two things that
 * are not secret on their own (a campaign id, an account id) under one that is, so truncating
 * costs nothing an untruncated digest would have bought — nobody is meant to invert it, and 22
 * base64url characters is still 132 bits, which is not a space anybody sweeps to find a
 * collision.
 */
async function hmac(secret, message) {
    const key = await crypto.subtle.importKey(
        'raw', new TextEncoder().encode(secret), { name: 'HMAC', hash: 'SHA-256' },
        false, ['sign']);

    const signature = await crypto.subtle.sign('HMAC', key, new TextEncoder().encode(message));

    return base64url(new Uint8Array(signature)).slice(0, 22);
}

/**
 * The key that says two membership rows belong to the same player, without saying which one.
 *
 * <p><b>`campaignId + ':' + playerUserId`, under a secret only this server holds.</b> Stable
 * within one campaign — the same player's two characters in one game hash to the same value —
 * and meaningless across campaigns, because the campaign id is folded into the message: the same
 * player in a second game the caller also runs gets a different key, so nothing here lets a GM
 * compare two of their games against each other by a shared value. It names nobody: going from
 * this key back to an account needs the secret, which never leaves this server — the same one-way
 * property {@link hash} gives a token.</p>
 *
 * <p><b>Fails loudly rather than guessing when the secret is missing</b>, the same shape
 * `auth.requestLink` uses for `SITE_URL`: a deployment with no `PLAYER_KEY_SECRET` set cannot
 * honestly say which rows share a player, and answering anyway — with an empty string, or with a
 * hash of nothing — would be a `playerKey` that agrees across every campaign on every deployment
 * that forgot to set it, which is the opposite of what it exists to guarantee.</p>
 */
export async function playerKey(env, campaignId, playerUserId) {
    if (typeof env.PLAYER_KEY_SECRET !== 'string' || env.PLAYER_KEY_SECRET.length === 0) {
        throw configurationFailure(
            'PLAYER_KEY_SECRET is not set, so a GM inbox cannot say which rows share a player. '
            + 'See docs/ACCOUNTS-SETUP.md.');
    }

    return hmac(env.PLAYER_KEY_SECRET, `${campaignId}:${playerUserId}`);
}

/**
 * An opaque membership id: `m_` plus 22 URL-safe characters.
 *
 * <p><b>The only id in this system the server mints for a client's row rather than accepting from
 * the client.</b> Every other one — `c_`, `g_` — is the browser's, so a PUT to a known address is
 * idempotent and needs no round trip to learn its own name. A membership is not created that way:
 * it is created by redeeming a join code, at the one moment when the server is the only party
 * that can see both accounts. Minting it here is also what keeps an account id off the wire — the
 * GM approves `m_…` and never learns whose account is on the other side of it.</p>
 */
export function newMembershipId() {
    return 'm_' + newSecret().slice(0, 22);
}

/**
 * The id a handed-over Villain takes on the GM's account — the one character id this server
 * mints rather than the browser.
 *
 * <p><b>Fresh rather than the player's own `c_…`</b>, because a GM who redeemed their own join
 * code is the player too, and the player's id under the GM's account would then be the row the
 * handover removes. It is the same shape the browser mints, so nothing on either side can tell
 * the two apart.</p>
 */
export function newCharacterId() {
    return 'c_' + newSecret().slice(0, 22);
}

/**
 * The alphabet a join code is written in: 30 symbols, with `I`, `L`, `O`, `U`, `0` and `1` left
 * out.
 *
 * <p><b>Because a join code is read aloud at a table and typed by somebody else.</b> `I`/`1`/`l`
 * and `O`/`0` are the pairs that go wrong on paper and in speech; `U` is out because a
 * ten-character code drawn from a full alphabet will occasionally spell something a GM would
 * rather not read out. Upper case only, for the same reason: a code with a case distinction is a
 * code somebody gets wrong over the phone.</p>
 */
const JOIN_CODE_ALPHABET = '23456789ABCDEFGHJKMNPQRSTVWXYZ';

/** How many symbols a code carries. Ten of thirty is about 49 bits. */
const JOIN_CODE_LENGTH = 10;

/**
 * A fresh join code: `XXXXX-XXXXX`.
 *
 * <p><b>Guessable-resistant, and the figure is the point rather than the formatting.</b> Ten
 * symbols from thirty is 30^10 ≈ 5.9 × 10^14 — so sweeping the space against a rate-limited
 * endpoint is not a thing that finishes, and a code is not the kind of secret a session cookie is
 * (it grants joining a game, and it can be replaced in one request).</p>
 *
 * <p><b>Rejection-sampled, not `% 30` over a raw byte.</b> 256 is not a multiple of 30, so a bare
 * modulo makes the first sixteen symbols of the alphabet slightly likelier than the last
 * fourteen — a small bias, and exactly the kind that is never noticed and never has to be there.
 * Bytes at or above 240 are drawn again.</p>
 *
 * <p>The hyphen is presentation and travels with the code, because it is what makes ten characters
 * readable. It is stripped on the way in — see `normaliseJoinCode` in `worker/campaigns.js` — so a
 * player who types the code without it, or in lower case, still gets in.</p>
 */
export function newJoinCode() {
    const symbols = [];

    while (symbols.length < JOIN_CODE_LENGTH) {
        const bytes = new Uint8Array(JOIN_CODE_LENGTH);
        crypto.getRandomValues(bytes);

        for (const byte of bytes) {
            if (symbols.length === JOIN_CODE_LENGTH) break;
            if (byte >= 240) continue;

            symbols.push(JOIN_CODE_ALPHABET[byte % JOIN_CODE_ALPHABET.length]);
        }
    }

    return symbols.slice(0, 5).join('') + '-' + symbols.slice(5).join('');
}

/**
 * A join code as this server stores and compares one, or `undefined` for something that is not a
 * code at all.
 *
 * **Normalised rather than refused.** A code is read aloud at a table and typed by somebody else,
 * so lower case, a missing hyphen, extra spaces and a hyphen in the wrong place are all things a
 * person does and none of them is a different code. What is refused is a value that cannot be one:
 * a non-string, or the wrong number of symbols once the punctuation is out.
 *
 * **The alphabet is checked, not merely the length.** A code containing a character the minter
 * cannot produce is a code that can never match a row — but letting it reach the query would make
 * the column's index answer about a value the system does not use, and a normaliser that passes
 * junk through is one a later caller trusts.
 */
export function normaliseJoinCode(value) {
    if (typeof value !== 'string') return undefined;

    const symbols = value.toUpperCase().replace(/[^A-Z0-9]/g, '');

    if (symbols.length !== JOIN_CODE_LENGTH) return undefined;

    for (const symbol of symbols) {
        if (!JOIN_CODE_ALPHABET.includes(symbol)) return undefined;
    }

    return symbols;
}

