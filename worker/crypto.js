// The two secrets this system mints, and the one-way function that keeps them out of the
// database. Nothing here is bespoke cryptography: it is WebCrypto, which the Workers runtime
// and Node both provide, used the obvious way.

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
