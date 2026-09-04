// The local database a signed-in check drives against, seeded from outside the application.
//
// ------------------------------------------------------------------------------------------------
// THIS IS NOT AN AUTHENTICATION BYPASS, AND THE DIFFERENCE IS THE WHOLE DESIGN.
//
// `PROGRESS.md` item 10's stage two was originally planned as a seam *in the application* that
// mints a session — a door held open in the shipped bundle, guarding the rulebook that is here by
// the author's personal permission, the recordings, the admin page and other people's characters.
// **That door did not have to be built.**
//
// Read off `worker/tokens.js` rather than reasoned about: a sign-in token is stored as **only its
// SHA-256** (`worker/crypto.js`'s `hash`), the raw token travels by email, and `db.spendLoginToken`
// verifies by hash lookup and burns the row in one statement. So a harness can do exactly what an
// email does, from outside:
//
//   1. mint a token with the shipped `newSecret` and hash it with the shipped `hash`;
//   2. insert the row into the **local** D1;
//   3. let the driver navigate to `/signin?t=<raw token>`.
//
// The application then runs **its real verify path** — hash lookup, expiry test, single-use burn,
// the invitation gate, the session cookie. Nothing is faked but a row, which is what an email
// would have caused. No code in the shipped bundle, no secret, no localhost test, nothing to
// compile out, and no test needed to assert the published bundle does not contain a seam.
//
// **`worker/crypto.js` is imported, not copied.** It is plain WebCrypto, which Node provides
// identically, so this file has no hash of its own that could drift from the server's. A second
// implementation of the one function that decides whether a token matches is exactly the kind of
// duplicate this repository keeps finding at the bottom of a bug.
//
// **And the query parameter is `?t=`, not `?token=`.** `worker/tokens.js`'s `signInLink` builds it
// and `web/Pages/SignIn.razor` reads it under that name. Written down because the plan for this
// slice said `?token=` in as many words, and a driver navigating to the wrong parameter would have
// sat on a sign-in form with no error anywhere — the token unspent, the check red, and nothing
// saying why.
//
// ------------------------------------------------------------------------------------------------
// WHY THE ADDRESSES ARE `@e2e.invalid`.
//
// `.invalid` is reserved by RFC 2606 and resolves nowhere, so a seeded row cannot address a real
// mailbox even if something here were ever pointed at a real mail provider. Nothing in this
// harness sends mail — that path is `scripts/probe-mail.mjs`'s job and is not a browser's — but
// the addresses are unusable rather than merely unused, which costs nothing.
//
// ------------------------------------------------------------------------------------------------
// USAGE — called by scripts/e2e.sh, which owns the server and therefore owns the database.
//
//   node scripts/e2e/seed.mjs --root . --sql-out .e2e/seed.sql [--defect <name>]
//
// Writes the statements to `--sql-out` and prints the raw token on stdout and nothing else, so the
// shell can capture it in one line. The defect names are declared in `scripts/e2e/defects.mjs` and
// are the negative controls for the signed-in checks; see that file's header for why a twin cannot
// be a broken *file* here.
//
// **This file does not run wrangler, and that split was forced rather than chosen.** It did, via
// `execFileSync('npx.cmd', ...)`, and Node refuses to spawn a `.cmd` without `shell: true` — a
// deliberate change made for a command-injection CVE — so the seed died on Windows with an
// `EINVAL` and a stack trace naming a hundred-character SQL string. Passing `shell: true` instead
// would have put that same SQL through `cmd.exe`'s quoting rules, which is precisely what the CVE
// was about. So the shell runs wrangler, as it does for every other wrangler invocation in this
// harness, and this file does the one thing it is here for: the hash the server will look up.

import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

/** The address the driver signs in as. Ordinary: invited, and not the deployment's owner. */
export const PLAYER = 'player@e2e.invalid';

/**
 * The address `ADMIN_EMAIL` is set to, and which nothing signs in as.
 *
 * **Deliberately not `PLAYER`, and the ADMIN check is why.** `worker/invitations.js` makes the
 * bootstrap administrator whoever `ADMIN_EMAIL` names, so pointing it at the seeded account would
 * sign the driver in as the owner — and a check that the admin page refuses an ordinary account
 * would then be measuring nothing at all.
 */
export const OWNER = 'owner@e2e.invalid';

/** Fifteen minutes, the same as `auth.TOKEN_LIFETIME_MS`. Long enough for a drive by any margin. */
const LIFETIME_MS = 15 * 60 * 1000;

const args = process.argv.slice(2);

function flag(name, fallback = null) {
    const at = args.indexOf('--' + name);
    return at >= 0 && at + 1 < args.length ? args[at + 1] : fallback;
}

const root = flag('root', process.cwd());
const sqlOut = flag('sql-out');
const defectName = flag('defect');

if (!sqlOut) {
    console.error('usage: seed.mjs --sql-out <file> [--root <dir>] [--defect <name>]');
    process.exit(2);
}

const { hash, newSecret } = await import(
    pathToFileURL(join(root, 'worker', 'crypto.js')).href);

const { SEED_DEFECTS } = await import(
    pathToFileURL(join(root, 'scripts', 'e2e', 'defects.mjs')).href);

const defect = defectName ? SEED_DEFECTS.find(d => d.name === defectName) : null;

if (defectName && !defect) {
    console.error('no such seed defect: ' + defectName + '. Known: '
        + SEED_DEFECTS.map(d => d.name).join(', '));
    process.exit(2);
}

const now = Date.now();
const token = newSecret();
const tokenHash = await hash(token);

// **The whole world, every time, rather than rows added to whatever the last drive left.** A
// signed-in check that passed because a *previous* drive's session cookie was still good would be
// the same fault as a check reading a store nothing writes to: green, honest-looking, and about
// nothing. Each drive gets one account, one live token, and no session at all until the browser
// spends it.
//
// **The order is the foreign keys' order.** `sessions.user_id` and `characters.user_id` both
// reference `users` with `ON DELETE CASCADE`, so deleting users first would work — but
// `invitations.invited_by` is `ON DELETE SET NULL`, and a row quietly surviving with a null author
// is exactly the kind of leftover this reset exists to have none of.
const statements = [
    'DELETE FROM sessions',
    'DELETE FROM login_tokens',
    'DELETE FROM characters',
    'DELETE FROM users',
    'DELETE FROM invitations',
    "INSERT INTO invitations (id, email, grants_admin, invited_by, created_at) VALUES "
        + "('i_e2eseededplayer000000', '" + PLAYER + "', " + (defect?.grantsAdmin ?? 0)
        + ", NULL, " + now + ")",
    "INSERT INTO login_tokens (token_hash, email, expires_at) VALUES "
        + "('" + tokenHash + "', '" + PLAYER + "', "
        + (now + (defect?.lifetimeMs ?? LIFETIME_MS)) + ")",
];

// **A file rather than a `--command`, and it is the shell that feeds it to wrangler.** See this
// file's header: Node will not spawn `npx.cmd` without a shell, and putting this SQL through
// `cmd.exe`'s quoting is exactly the thing that restriction exists to prevent. `--file` also keeps
// the statements readable in `.e2e/` after a failed run, which a command line does not.
//
// Terminated with newlines rather than joined onto one line, for the same reason: a seed that goes
// wrong is read by a person, and eight statements on one line is not.
confirmTheDatabaseIsNamed(root);

writeFileSync(sqlOut, statements.map(s => s + ';').join('\n') + '\n');

// **The token, and nothing else, on stdout.** The shell captures this in one `$( )`; anything else
// printed here would be captured with it and handed to the browser as a token.
process.stdout.write(token);

/**
 * Whether `d1/wrangler.toml` still describes the database this is seeding.
 *
 * <p>Read rather than trusted, and read here rather than in the shell because this is the file that
 * would silently seed the wrong place. The shell binds the server by `database_id` out of the same
 * file; if these two ever name different databases, every signed-in check goes red on its control
 * and blames the application.</p>
 */
function confirmTheDatabaseIsNamed(at) {
    const toml = readFileSync(join(at, 'd1', 'wrangler.toml'), 'utf8');

    if (!/^\s*database_name\s*=\s*"[^"]+"/m.test(toml)
        || !/^\s*database_id\s*=\s*"[^"]+"/m.test(toml)) {
        throw new Error('d1/wrangler.toml no longer carries both a database_name and a '
            + 'database_id. scripts/e2e.sh binds the server by the id and runs this seed against '
            + 'the name, so a file missing either would seed one database and serve another — '
            + 'which fails as an application defect rather than as a configuration one.');
    }
}
