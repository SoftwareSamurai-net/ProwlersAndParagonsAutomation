// Seeding a sign-in from outside the application, which is what stage two turns on.
//
// ------------------------------------------------------------------------------------------------
// WHY THIS IS A ROW AND NOT A SEAM, AND WHY THAT IS NOT A LOOPHOLE IN "NOTHING REACHES PAST THE
// BROWSER".
//
// `PROGRESS.md` item 10 argues it at length and the argument is settled. `worker/tokens.js` stores
// **only the SHA-256 of a sign-in token** — `worker/crypto.js`'s `hash`, plain WebCrypto, which
// Node provides identically — the raw token travels by email, and `db.spendLoginToken` verifies by
// hash lookup and burns the row in one statement. So a harness can do exactly what an email does:
//
//   1. generate a token here and hash it;
//   2. write the row into the **local** D1;
//   3. drive Chrome to `/signin?t=<raw token>`.
//
// Everything after that is the application's own verify path — hash lookup, expiry test, single-use
// burn, invitation check, session cookie — reached by a real navigation. **Nothing is bypassed and
// nothing is faked but the row, which is what an email would have caused.** There is no code in the
// shipped bundle, no secret, no localhost test and nothing to compile out, which is the whole
// difference between this and the authentication seam the original stage-two plan called for.
//
// **And it does not weaken the rule the driver obeys.** That rule is about the *browser*: no
// `localStorage.setItem` to arrange a state, no calling into a component, no `el.click()` from
// inside the page — because a harness that sets up its own world stops answering "is this reachable
// by an ordinary person doing an ordinary thing". A reader whose mail has arrived is an ordinary
// person, and every step they take from the link onwards is taken here by the browser.
//
// ------------------------------------------------------------------------------------------------
// WHY THIS LIVES BESIDE THE SHELL RATHER THAN IN A DRIVER.
//
// The brief asked the question and the answer came out one-sided once the alternatives were
// written down. Writing a row needs four things a driver has no business knowing: the pinned
// wrangler version, the database id out of `d1/wrangler.toml`, the `--persist-to` directory the
// server was started against, and the migration state. `scripts/e2e.sh` already owns all four —
// it parses the version, starts the server and made the directory. That held for a second reason
// too, while there were two drivers: a seed inside one of them would have been a seed the other
// could not have, which would have made the twins a run exercises depend on which driver was
// asked for, in a second place. There is one driver now, but the first reason stands on its own.
//
// So the shell seeds and hands the driver *what an email would have handed a reader*: a raw token
// and the address it was minted for, in the environment. A driver still knows a URL and some
// opaque strings, which is the property that made it easy to reason about.
//
// ------------------------------------------------------------------------------------------------
// PROFILES: THE REAL RUN AND ONE PER SEED TWIN, ALL MINTED IN ONE PASS.
//
// A token is single-use by construction, so the real run and each twin need their own. They are all
// minted here, in one `wrangler d1 execute --file`, rather than re-seeding between drives: a twin
// then costs one `--only` drive against a server that is already up, instead of a wrangler
// invocation and a server start apiece. Measured, that is the difference between a seed twin
// costing about ten seconds and costing about forty.
//
// `scripts/e2e/defects.mjs` stays the one place a negative control is declared — a seed defect
// there names the slots it overrides, and the two properties `buildTwin` has are enforced below:
// **an unknown name throws**, and **a twin whose plan is identical to the real one throws**.

import { createHash, randomBytes } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

import { DEFECTS, kindOf } from './defects.mjs';

/**
 * The *kinds* of account this harness signs in as. Each profile below gets its own copy of every
 * one it uses, at an address of its own.
 *
 * <p><b>`@e2e.invalid` on purpose.</b> `.invalid` is reserved by RFC 2606 and can never resolve, so
 * no row here can be confused for somebody's real address if one of these databases is ever looked
 * at by hand — and nothing that mails a link is reachable from the harness anyway.</p>
 *
 * <p><b>`grantsAdmin` is the whole of the ADMIN check's subject.</b> `reader` is invited and is not
 * an administrator, which is the account `/admin` must refuse; `adminTwin` is the same shape with
 * the flag set, which is the account it must not.</p>
 *
 * <p><b>A copy per profile, and that was a defect found by running a twin rather than by reading
 * it.</b> With one shared `saver`, the real run built a character on that account and the
 * `second-context-is-another-account` twin then signed in as the same account, was handed the
 * character the real run had left there, and typed its name into a field that already held one —
 * so the twin went red on "the name field never held what was typed" instead of on the identity it
 * exists to test. A twin that fails for the wrong reason has not been watched to fail. Each profile
 * therefore gets a world of its own, and no drive can inherit another's.</p>
 */
const ACCOUNTS = {
    reader: { local: 'reader', grantsAdmin: 0 },
    adminTwin: { local: 'admin', grantsAdmin: 1 },
    saver: { local: 'saver', grantsAdmin: 0 },
    saveOther: { local: 'somebody-else', grantsAdmin: 0 },
};

/**
 * The address one profile's copy of an account kind lives at.
 *
 * <p>The local part is what `worker/auth.js` names the account on first sign-in, and it is what
 * every "signed in as somebody" assertion reads out of the banner — so it carries the profile,
 * and a failure line says which drive's world it belonged to.</p>
 */
const addressOf = (kind, slug) =>
    `${ACCOUNTS[kind].local}${slug === 'real' ? '' : `-${slug}`}@e2e.invalid`;

/**
 * One token a check is handed, named for what the check does with it.
 *
 * <p>Each slot becomes two environment variables — <c>&lt;env&gt;_TOKEN</c> and
 * <c>&lt;env&gt;_EMAIL</c> — because a check has to assert <em>who</em> it was signed in as, not
 * only that it was signed in as somebody. That assertion is what the ADMIN check's positive control
 * is made of, and it is what the ACCOUNT_SAVE twin turns red.</p>
 *
 * <p><b>ACCOUNT_SAVE has two because a token is single-use.</b> Its whole question is whether a
 * character followed the account rather than the browser, which needs a second browser context
 * signing in again from nothing.</p>
 */
const SLOTS = {
    ADMIN: { account: 'reader', env: 'PP_E2E_ADMIN' },
    RULES: { account: 'reader', env: 'PP_E2E_RULES' },
    SAVE_1: { account: 'saver', env: 'PP_E2E_SAVE_1' },
    SAVE_2: { account: 'saver', env: 'PP_E2E_SAVE_2' },
};

/**
 * How long a seeded token is good for, and how far in the past an expired one sits.
 *
 * <p>Fifteen minutes is `auth.TOKEN_LIFETIME_MS` — the same lifetime the real mail path mints, so
 * the row is the row an email would have caused rather than a longer-lived convenience. An hour in
 * the past is far enough that no clock skew between this process and the worker could rescue it.</p>
 */
const LIFETIME_MS = 15 * 60 * 1000;
const LONG_EXPIRED_MS = 60 * 60 * 1000;

/**
 * SHA-256 as lower-case hex — `worker/crypto.js`'s `hash`, which is what the table stores.
 *
 * <p><b>Exported so the agreement can be asserted rather than asserted about.</b> This is Node's
 * `crypto` and that one is WebCrypto; if the two ever disagreed, every seeded row would be a hash
 * the server looks up and never finds, and the whole of stage two would fail as "the link did not
 * sign anybody in" with nothing saying why. `tests/worker/e2e-seed.test.mjs` runs both over a
 * fixed input.</p>
 */
export const hash = secret => createHash('sha256').update(secret, 'utf8').digest('hex');

/**
 * A fresh token, shaped like the ones `worker/crypto.js` mints: 256 bits, base64url.
 *
 * <p>Not imported from there, deliberately. That module is WebCrypto and this one is Node's
 * <code>crypto</code>; what has to agree between them is the <em>hash</em>, which is SHA-256 either
 * way, and a token's alphabet is nobody's business but the mailbox it lands in. Importing worker
 * code into the harness would make the harness a second caller of a module whose only caller today
 * is the server.</p>
 */
const newToken = () => randomBytes(32).toString('base64url');

/**
 * The plan for one profile: every slot's token, resolved against a twin's overrides.
 *
 * <p><b>Both of `buildTwin`'s properties are here, against a row instead of a line.</b> An override
 * naming a slot or an account this file does not mint throws, so a seed defect cannot quietly stop
 * pointing at anything; and a profile that comes out identical to the real one throws, so a twin
 * cannot silently become a second copy of the real run and pass for the wrong reason.</p>
 */
function profileFor(name, slug, overrides, now) {
    const tokens = {};

    for (const [slot, spec] of Object.entries(SLOTS)) {
        const override = overrides[slot] ?? {};
        const account = override.account ?? spec.account;

        if (!(account in ACCOUNTS)) {
            throw new Error(
                `twin '${name}' points slot ${slot} at an account called '${account}', which `
                + `scripts/e2e/seed.mjs does not mint. Known: ${Object.keys(ACCOUNTS).join(', ')}. `
                + `A negative control aimed at nothing is not a negative control.`);
        }

        tokens[slot] = {
            token: newToken(),
            account,
            email: addressOf(account, slug),
            expiresAt: override.expired ? now - LONG_EXPIRED_MS : now + LIFETIME_MS,
        };
    }

    return { name, slug, tokens };
}

/**
 * Everything the run needs: the real profile, and one per seed twin.
 *
 * <p><b>`defects` is a parameter so this can be driven over a list that is not the shipped one.</b>
 * Both of the properties below — an override naming a slot or an account this file does not mint
 * throws, and a twin whose plan is identical to the real run's throws — are only worth having if
 * they have been watched to fire, and the shipped list is (correctly) a list on which neither
 * does. `tests/worker/e2e-seed.test.mjs` passes synthetic defects that trip each one.</p>
 */
export function plan(now = Date.now(), defects = DEFECTS) {
    for (const defect of defects) {
        if (kindOf(defect) !== 'seed') continue;

        for (const slot of Object.keys(defect.seed.slots)) {
            if (!(slot in SLOTS)) {
                throw new Error(
                    `twin '${defect.name}' overrides a token slot called '${slot}', which `
                    + `scripts/e2e/seed.mjs does not mint. Known: ${Object.keys(SLOTS).join(', ')}.`);
            }
        }
    }

    const real = profileFor('real', 'real', {}, now);
    const profiles = { real };

    let index = 0;

    for (const defect of defects) {
        if (kindOf(defect) !== 'seed') continue;

        // A short slug rather than the twin's own name, because it goes into an email address and
        // `second-context-is-another-account@e2e.invalid` is a display name nothing on screen has
        // room for. The mapping is printed by `--plan`, so a failure line naming `saver-t3` can be
        // traced back to the twin in one line of the run's own output.
        const twin = profileFor(defect.name, `t${++index}`, defect.seed.slots, now);

        // **The "is it still reproducing anything" property.** Compared on what the *application*
        // can tell apart — which account each slot signs in as, and whether its token is live —
        // and deliberately not on the token strings, which differ between every profile by
        // construction and would make this assertion pass for free.
        const shape = p => Object.entries(p.tokens)
            .map(([slot, t]) => `${slot}=${t.account}${t.expiresAt < now ? ':expired' : ''}`)
            .join(' ');

        if (shape(twin) === shape(real)) {
            throw new Error(
                `twin '${defect.name}' seeds exactly what the real run seeds, so it is a second `
                + `copy of the real run and would pass for the wrong reason. Its 'seed.slots' `
                + `entry changes nothing the application can see: ${shape(real)}`);
        }

        profiles[defect.name] = twin;
    }

    return profiles;
}

/**
 * The SQL for every profile at once.
 *
 * <p><b>One statement per row and no `DELETE`.</b> The database this runs against is made fresh
 * under `.e2e/` by the same script that migrates it, so there is nothing to clean up — and a seed
 * that begins by emptying tables is one that would quietly work against a database it should never
 * have been pointed at.</p>
 *
 * <p><b>Values are numbers and quoted literals, never interpolated caller input</b>, because they
 * are all minted in this file: an address out of `ACCOUNTS`, a hex digest, and two integers.</p>
 */
export function sqlFor(profiles, now = Date.now()) {
    const lines = [
        '-- Written by scripts/e2e/seed.mjs. Every row here is one an email would have caused.',
    ];

    // One invitation per address a token is actually minted for, and no others. An address with no
    // token is an account nothing signs in as, and a row nobody reads is a row that can rot.
    const invited = new Map();

    for (const profile of Object.values(profiles)) {
        for (const t of Object.values(profile.tokens)) {
            invited.set(t.email, ACCOUNTS[t.account].grantsAdmin);
        }
    }

    let id = 0;

    for (const [email, grantsAdmin] of invited) {
        lines.push(
            `INSERT INTO invitations (id, email, grants_admin, invited_by, created_at) VALUES (`
            // `i_` plus exactly 22 URL-safe characters, which is the shape every id this server
            // mints has and the shape `worker/invitations.js`'s ID_PATTERN accepts — so a row
            // seeded here can be withdrawn through the page like any other.
            + `'i_e2e${String(++id).padStart(19, '0')}', '${email}', `
            + `${grantsAdmin}, NULL, ${now});`);
    }

    for (const profile of Object.values(profiles)) {
        for (const [slot, t] of Object.entries(profile.tokens)) {
            lines.push(
                `-- ${profile.name}/${slot} -> ${t.email}`
                + `${t.expiresAt < now ? ' (deliberately expired)' : ''}`);
            lines.push(
                `INSERT INTO login_tokens (token_hash, email, expires_at) VALUES (`
                + `'${hash(t.token)}', '${t.email}', ${t.expiresAt});`);
        }
    }

    return lines.join('\n') + '\n';
}

/** `KEY=VALUE` per line, for `env` to put in front of a driver. */
export function envFor(profile) {
    const lines = [];

    for (const [slot, t] of Object.entries(profile.tokens)) {
        lines.push(`${SLOTS[slot].env}_TOKEN=${t.token}`);
        lines.push(`${SLOTS[slot].env}_EMAIL=${t.email}`);
    }

    return lines;
}

// ---------------------------------------------------------------------------------------------
// Called by scripts/e2e.sh.
//
//   --plan <sql-out> <json-out>   mint every profile: write the SQL to run, and the plan to read
//   --env  <json-in> [profile]    the environment one drive runs under; default profile 'real'

// **Guarded, for the same reason `scripts/e2e/defects.mjs`'s block is.** Without it this ran on
// import: `node -e "import('./scripts/e2e/seed.mjs')"` printed the usage line and exited 2, so
// `plan`, `sqlFor` and `envFor` could not be called from a test at all — the three functions whose
// two throwing properties are the whole of a seed twin's honesty. A module that kills whoever
// imports it is a module nothing can check.

const invokedDirectly = process.argv[1] !== undefined
    && import.meta.url === pathToFileURL(process.argv[1]).href;

const [command, ...rest] = invokedDirectly ? process.argv.slice(2) : ['--imported'];

if (command === '--imported') {
    // Nothing. Imported for plan, sqlFor, envFor and hash.
} else if (command === '--plan') {
    const [sqlOut, jsonOut] = rest;
    const now = Date.now();
    const profiles = plan(now);

    writeFileSync(sqlOut, sqlFor(profiles, now));
    writeFileSync(jsonOut, JSON.stringify(profiles, null, 2));

    const twins = Object.values(profiles).filter(p => p.name !== 'real');
    console.log(
        `seeded ${Object.keys(profiles).length * Object.keys(SLOTS).length} sign-in tokens `
        + `across ${Object.keys(profiles).length} world(s): the real run, plus `
        + `${twins.map(p => `${p.name} (${p.slug})`).join(', ')}.`);
} else if (command === '--env') {
    const [jsonIn, name = 'real'] = rest;
    const profiles = JSON.parse(readFileSync(jsonIn, 'utf8'));
    const profile = profiles[name];

    if (!profile) {
        console.error(
            `no seeded profile called '${name}'. Known: ${Object.keys(profiles).join(', ')}. `
            + `A drive with no tokens is a signed-in check with nothing to sign in as.`);
        process.exit(2);
    }

    for (const line of envFor(profile)) console.log(line);
} else {
    console.error('usage: seed.mjs --plan <sql-out> <json-out> | --env <json-in> [profile]');
    process.exit(2);
}
