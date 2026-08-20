// Request and response plumbing. Two things here are security decisions rather than
// convenience: the same-origin check on anything that changes state, and the body size cap.

export const SESSION_COOKIE = 'pp_session';

/** A JSON response. */
export function json(body, init = {}) {
    return new Response(JSON.stringify(body), {
        ...init,
        headers: { 'content-type': 'application/json; charset=utf-8', ...(init.headers ?? {}) },
    });
}

/** Nothing to say, and it worked. */
export function noContent(headers = {}) {
    return new Response(null, { status: 204, headers });
}

/**
 * A refusal.
 *
 * <p>The message is for whoever is reading a network tab, never for a caller to branch on —
 * the status is the contract. In particular a sign-in failure says the same thing whatever
 * went wrong, so the endpoint cannot be used to ask whether an address has an account.</p>
 */
export function fail(status, message, extra) {
    // `extra` carries a reference id on the one path that mints one — see the catch in
    // `index.js`. Spread rather than named, so this stays the single shape every refusal takes:
    // `{ error }`, plus whatever the caller could add without saying anything about what went
    // wrong. Nothing passed here may carry a message from an exception.
    return json({ error: message, ...extra }, { status });
}

/**
 * The session cookie, or null.
 *
 * Parsed by hand because Workers has no cookie jar. Split on `;` and take the first `=`, so a
 * value containing `=` survives — base64url does not produce one, but a future secret might,
 * and a parser that quietly truncates a credential fails in the direction of locking people out.
 */
export function sessionCookie(request) {
    const header = request.headers.get('cookie');
    if (!header) return null;

    for (const part of header.split(';')) {
        const at = part.indexOf('=');
        if (at < 0) continue;

        if (part.slice(0, at).trim() === SESSION_COOKIE) return part.slice(at + 1).trim() || null;
    }

    return null;
}

/**
 * `Set-Cookie` for a fresh session.
 *
 * <p><b>HttpOnly is the point.</b> The browser app never holds the credential, so a script
 * injected into the page cannot read it — which matters more here than in most apps, because
 * the front end is WebAssembly and a token in local storage would be one XSS away from an
 * account takeover. Secure and SameSite=Lax are what stop it travelling over plain HTTP or
 * being attached to a cross-site form post.</p>
 */
export function setSessionCookie(secret, maxAgeSeconds) {
    return `${SESSION_COOKIE}=${secret}; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=${maxAgeSeconds}`;
}

/** `Set-Cookie` that removes the session. Same attributes, or the browser keeps the old one. */
export function clearSessionCookie() {
    return `${SESSION_COOKIE}=; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=0`;
}

/**
 * Whether a state-changing request came from this site.
 *
 * <p><b>SameSite=Lax already blocks the cross-site form post, and this is the belt to that
 * pair of braces.</b> Lax is a browser behaviour with a history of exceptions; an explicit
 * check costs one comparison and does not depend on the visitor's browser having got it
 * right. A request with no Origin at all is refused rather than allowed — every fetch the app
 * makes sends one, so the permissive reading buys nothing and gives up the check.</p>
 */
export function sameOrigin(request) {
    const origin = request.headers.get('origin');
    if (!origin) return false;

    try {
        return new URL(origin).origin === new URL(request.url).origin;
    } catch {
        return false;
    }
}

/**
 * The request body as JSON, or null if it is not usable.
 *
 * The cap is on the body itself rather than on Content-Length, which a caller writes and can lie
 * about. A character is a few kilobytes; a quarter of a megabyte is room for a very elaborate
 * one and far short of anything worth storing by accident.
 *
 * **And it is counted in bytes, which is what this comment used to claim while the code counted
 * something else.** `String.length` is UTF-16 code units, so a body padded with astral-plane
 * characters — two units each, four bytes each — reached about twice the stated limit before
 * tripping it. The existing test padded with ASCII, where the two measures agree, so it passed.
 */
export const MAX_BODY_BYTES = 256 * 1024;

export async function readJson(request) {
    const text = await request.text();
    if (new TextEncoder().encode(text).byteLength > MAX_BODY_BYTES) return null;

    try {
        const value = JSON.parse(text);
        return value !== null && typeof value === 'object' ? { value, text } : null;
    } catch {
        return null;
    }
}
