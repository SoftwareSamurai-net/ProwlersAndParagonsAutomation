// What kind of failure this was, and what is safe to write down about it.
//
// **Two audiences want opposite things from one failure.** A visitor needs to know whether to
// retry, wait, or report — and nothing else, because an internal message is both meaningless to
// them and a disclosure. The owner needs to know what actually threw. A closed set of categories
// is the middle ground: each side renders the same category its own way, and neither is handed
// the other's answer.
//
// **A category is assigned where a failure is caught, never at a throw site.** `mail.js` and
// `db.js` know nothing about any of this — `index.js` wraps the two subsystems on the way in and
// tags whatever comes back out of them, so there is one place to read to know how a failure is
// classified. A category per throw site would become a description of the internals by
// enumeration, which is the disclosure this exists to avoid.

/**
 * The whole set, and it is closed.
 *
 * Small on purpose. `unknown` is the default and must stay reachable: a taxonomy with no default
 * grows a category for every new failure, and the pressure is then to classify by guessing.
 * Anything unclassified lands here rather than being invented.
 *
 * **A category names the subsystem that failed, never the request that reached it.** That is a
 * security property rather than a tidiness one — see the note in `index.js`.
 */
export const CATEGORIES = Object.freeze(['mail', 'storage', 'configuration', 'unknown']);

/**
 * Where a category is kept on an error.
 *
 * A symbol rather than a property name, so it cannot collide with anything a provider's own error
 * object carries, and non-enumerable so it never lands in a `JSON.stringify` of an error.
 */
const CATEGORY = Symbol('pp.category');

/**
 * Tag an error with the subsystem it came out of, and hand it back to be thrown.
 *
 * **The first tag wins.** The mail wrapper's work happens inside a request that is already
 * running against a storage-wrapped database, so an error that has been classified by an inner
 * boundary must not be relabelled by an outer one — the innermost boundary is the one that knows
 * which subsystem it is.
 */
export function tag(error, category) {
    if (error !== null && typeof error === 'object' && !(CATEGORY in error)) {
        Object.defineProperty(error, CATEGORY, { value: category, enumerable: false });
    }

    return error;
}

/** The category on an error, or `unknown` — the honest answer for anything untagged. */
export function categoryOf(error) {
    const named = error !== null && typeof error === 'object' ? error[CATEGORY] : undefined;

    return CATEGORIES.includes(named) ? named : 'unknown';
}

/** An error saying this deployment is wrong, rather than that something went wrong in it. */
export function configurationFailure(message) {
    return tag(new Error(message), 'configuration');
}

/**
 * The database, with every way it can fail tagged.
 *
 * A missing binding is `configuration` rather than `storage`: nothing failed, there is nothing
 * there. It answers with a statement that throws on use rather than by handing back undefined, so
 * the failure names itself instead of arriving as a `TypeError` on `undefined.prepare` and
 * landing in `unknown`.
 */
export function taggedStorage(db) {
    if (db === null || db === undefined || typeof db.prepare !== 'function') {
        return {
            prepare() {
                throw configurationFailure(
                    'No D1 binding is available, so nothing can be read or written. '
                    + 'See docs/ACCOUNTS-SETUP.md.');
            },
        };
    }

    return {
        prepare(sql) {
            try {
                return taggedStatement(db.prepare(sql));
            } catch (error) {
                throw tag(error, 'storage');
            }
        },
    };
}

/**
 * One prepared statement, with every way it can fail tagged.
 *
 * `bind` is re-wrapped rather than mutated because D1 returns a *new* statement from it while the
 * test harness returns the same object; wrapping whatever comes back is correct for both.
 */
function taggedStatement(statement) {
    return {
        bind(...values) {
            try {
                return taggedStatement(statement.bind(...values));
            } catch (error) {
                throw tag(error, 'storage');
            }
        },
        async first(...args) {
            try { return await statement.first(...args); } catch (e) { throw tag(e, 'storage'); }
        },
        async all(...args) {
            try { return await statement.all(...args); } catch (e) { throw tag(e, 'storage'); }
        },
        async run(...args) {
            try { return await statement.run(...args); } catch (e) { throw tag(e, 'storage'); }
        },
    };
}

/** The mail provider, with its failures tagged. One call, wrapped where the other one is. */
export function taggedMail(send) {
    return async (...args) => {
        try {
            return await send(...args);
        } catch (error) {
            throw tag(error, 'mail');
        }
    };
}

/** Every address the router answers, as a pattern. */
const KNOWN_ROUTES = Object.freeze([
    '/api/auth/request', '/api/auth/verify', '/api/auth/signout', '/api/me',
    '/api/me/display-name',
    '/api/characters', '/api/campaigns',
    // The two membership lists are exact addresses and each is filed under its own name: a GM's
    // inbox failing and a player's standings failing are different faults with different causes,
    // and folding them together would make the log say only "memberships".
    '/api/memberships', '/api/memberships/inbox', '/api/memberships/join',
    '/api/rulebook/power',
    '/api/rulebook/search', '/api/rulebook/contents', '/api/rulebook/passage',
    '/api/admin/error-log',
    // The list of players in the caller's own campaigns. Its own name rather than folded in with
    // the row below, because a list that cannot be drawn and one account's cap that cannot be set
    // are different faults with different causes.
    '/api/admin/accounts',
    '/api/transcripts',
]);

/**
 * Which address this was, as a pattern rather than as the path that arrived.
 *
 * **The closed list is what bounds the error table.** `/api/characters/{id}` carries a
 * caller-chosen id, so storing the path itself would let anybody asking for a thousand made-up
 * ids write a thousand rows — and would put a caller-controlled string into the one table this
 * design exists to keep boring. A pattern from a fixed list can do neither: the row count is at
 * most the four categories times the handful of entries above.
 */
export function routePattern(request) {
    let path;

    try {
        path = new URL(request.url).pathname.replace(/\/+$/, '');
    } catch {
        return 'other';
    }

    if (KNOWN_ROUTES.includes(path)) return path;
    if (path.startsWith('/api/characters/')) return '/api/characters/{id}';

    // **A campaign id is caller-chosen exactly as a character id is, so it needs this arm for
    // exactly the same reason.** Without it, a caller asking for a thousand invented campaign ids
    // falls to `other` — which is one row, so the table is still bounded, but every one of those
    // failures is then indistinguishable from a failure at an address nobody routes. With it, the
    // campaign routes are as legible in the log as the character ones and still cost one row.
    if (path.startsWith('/api/campaigns/')) return '/api/campaigns/{id}';

    // **A membership id is caller-chosen in exactly the sense that matters here** — it is a string
    // in a URL that this server did not put there on this request — so it needs this arm for the
    // reason the two above have one: without it a broken approval is filed as `other`, beside
    // requests to addresses nobody routes, and the log stops being readable at the moment it is
    // most wanted.
    //
    // **The verb is deliberately not in the pattern**, though `PUT …/submission` and
    // `POST …/approve` are different code paths. `route` is half of a primary key and a pattern per
    // verb triples the rows this prefix can occupy for no gain: `kind` and `detail` already say
    // which statement threw.
    //
    // **And neither is the sub-path**, which is why `GET …/table` needed nothing added here or to
    // `KNOWN_ROUTES` when it was built. That list is compared against the path that arrived, so an
    // entry naming a caller-chosen id could never match one; the sub-paths under a membership are
    // filed under this one pattern for exactly the reason the verbs are.
    if (path.startsWith('/api/memberships/')) return '/api/memberships/{id}';

    // **The key under this prefix is an email address, which is exactly why the arm is here.**
    // Without it, a failure setting somebody's cap is filed as `other` beside every request to an
    // address nobody routes — and, worse than for the three above, the *path* under this prefix
    // names a person. Filing it as a pattern is what keeps an address out of a table whose whole
    // design is that it is safe to read aloud. Both sub-paths share the one pattern, verb
    // included, for the reason the membership arm gives: `route` is half of a primary key.
    if (path.startsWith('/api/admin/accounts/')) return '/api/admin/accounts/{key}';

    return 'other';
}

/** The exception's type, which is the part of it that is always safe to keep. */
export function kindOf(error) {
    if (error === null || error === undefined) return 'unknown';

    return typeof error === 'object'
        ? String(error.name ?? error.constructor?.name ?? 'Error')
        : typeof error;
}

/**
 * As much of a message as is worth keeping. Long enough to recognise a failure, short enough that
 * the table is not somewhere a whole query ends up.
 */
const MAX_DETAIL = 200;

/**
 * An exception's message with the parts nobody should read again taken out.
 *
 * **Be honest about what this buys.** `users.email` is in this database in the clear already, by
 * necessity, so an error row is not a new exposure *boundary*. What redaction protects against is
 * different and still worth having: the error log is the artefact most likely to be read aloud,
 * pasted into an issue, or screenshotted. An exception from D1 or from a mail provider can quote
 * a query or an address — `index.js` refuses to pass the message to the visitor for exactly that
 * reason — so what is kept here has the addresses and the long random strings taken out, and the
 * table is still never safe to publish.
 *
 * **It over-redacts on purpose.** Any run of twenty or more characters from the token alphabet
 * goes, with no test for whether it looks random: a session secret is 43 base64url characters and
 * a hash is 64 hex ones, and neither is guaranteed to contain a digit. The cost of taking out a
 * long identifier by mistake is a slightly less readable line; the cost of leaving a credential
 * in is the whole point of the exercise.
 */
export function redact(message) {
    if (typeof message !== 'string' || message === '') return '';

    const cleaned = message
        // Addresses first, so what is left of one is labelled as an address rather than
        // disappearing into the token rule below.
        //
        // **The local part is any run of characters that is not whitespace and not another `@`**,
        // rather than the RFC-ish `[A-Za-z0-9._%+-]+` it used to be. That set is not what an
        // address is allowed to hold — a unicode local part is ordinary, a quoted one may hold a
        // slash — and every character outside it ended the match early, so `dorián@example.test`
        // left `dori` in front of an `[address]` and `"a/b"@example.test` left the whole first
        // half in `error_log.detail`. Over-matching here costs a word of legibility either side of
        // the address, which is the trade this whole function is written to make.
        .replace(/[^\s@]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/g, '[address]')
        // A local part with no dotted domain is still an address, and D1 quotes them bare.
        .replace(/[^\s@]+@[A-Za-z0-9.-]+/g, '[address]')
        .replace(/[A-Za-z0-9_-]{20,}/g, '[redacted]')
        .replace(/\s+/g, ' ')
        .trim();

    return cleaned.length > MAX_DETAIL ? cleaned.slice(0, MAX_DETAIL - 1) + '…' : cleaned;
}
