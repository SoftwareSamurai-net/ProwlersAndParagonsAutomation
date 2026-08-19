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

/**
 * Constant-time string comparison, for anywhere a secret is compared rather than looked up.
 *
 * Lookups by hash are already constant-time-ish because they are index probes on a value the
 * attacker cannot see; this exists for the cases that are not.
 */
export function sameSecret(a, b) {
    if (typeof a !== 'string' || typeof b !== 'string' || a.length !== b.length) return false;

    let diff = 0;
    for (let i = 0; i < a.length; i++) diff |= a.charCodeAt(i) ^ b.charCodeAt(i);

    return diff === 0;
}

/** An opaque user id. Not derived from the address — see the schema's note on `users.id`. */
export function newUserId() {
    return 'u_' + newSecret().slice(0, 22);
}

function base64url(bytes) {
    return btoa(String.fromCharCode(...bytes))
        .replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '');
}
