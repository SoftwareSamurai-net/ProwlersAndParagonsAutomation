// The decision of whether a deploy may proceed past the pending D1 migrations, and nothing else.
//
// **Everything below `decide()` and its two inputs is pure.** No network call, no filesystem
// read, no child process — `tests/deploy/migration-gate.test.mjs` drives every branch with
// canned strings, and that is only possible because this module never reaches outside the
// arguments it is given. `scripts/apply-migrations.sh` is what actually runs `wrangler`; the CLI
// entry point at the bottom of this file (guarded so importing this module never triggers it) is
// the one place that touches `d1/migrations` or `stdin`, and it exists only to hand real bytes to
// the pure functions above it.
//
// **The classifier is a judge, not a fixer — this repository's own rule for the rules engine,
// carried over on purpose.** A migration that reads as destructive is refused, never rewritten,
// never silently split. The one way past a refusal is the migration's own author naming the
// reason in the file, with the `pp:allow-destructive` marker below — never this script being
// edited to let one specific file through.

import { readFileSync, existsSync, readdirSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

// ---------------------------------------------------------------------------------------------
// classifyMigrationSql — does this migration's SQL contain a destructive statement?
// ---------------------------------------------------------------------------------------------

// Five patterns, matched case-insensitively against one statement at a time, after that
// statement's comments and string literals have been masked out. Each is a rule from
// CLAUDE.md/the task spec's own list:
//
//   DROP TABLE, DROP COLUMN, DELETE FROM, TRUNCATE — named directly.
//   ALTER TABLE ... RENAME TO — the other half of "a table rebuild". The three-statement form
//   of the SQLite 12-step recipe (CREATE TABLE ..._new, INSERT INTO ... SELECT, DROP TABLE) is
//   deliberately NOT matched as a separate combination: every recipe for it ends with either a
//   literal DROP TABLE or a literal RENAME TO somewhere in the file (there is no way to make the
//   old table disappear without one of the two), so the two patterns already in this list catch
//   every real rebuild without a second detector duplicating the same verdict.
const DESTRUCTIVE_PATTERNS = [
    /\bDROP\s+TABLE\b/i,
    /\bDROP\s+COLUMN\b/i,
    /\bDELETE\s+FROM\b/i,
    /\bTRUNCATE\b/i,
    /\bALTER\s+TABLE\b[\s\S]*\bRENAME\s+TO\b/i,
];

// The deliberate-override marker. It lives in the migration file rather than in this script, on
// purpose: the person who knows why a destructive statement is safe this one time is the person
// writing the migration, not whoever last edited the gate.
const OVERRIDE_RE = /^--\s*pp:allow-destructive:\s*(.+)$/;

/**
 * Replaces every character inside a `--` line comment, a `/* *\/` block comment, or a
 * single-quoted string literal with a space (newlines are kept, so line numbers do not shift).
 * The result is the same length as the input and is for *pattern matching only* — never shown
 * to a person, because it is unreadable by design.
 *
 * This is what keeps a comment saying "a DROP TABLE here would cascade" from being read as a
 * DROP TABLE, and a string literal quoting the words "DELETE FROM" from being read as one.
 */
function maskCommentsAndStrings(sql) {
    let out = '';
    let i = 0;
    const n = sql.length;

    while (i < n) {
        if (sql[i] === '-' && sql[i + 1] === '-') {
            while (i < n && sql[i] !== '\n') { out += ' '; i++; }
            continue;
        }

        if (sql[i] === '/' && sql[i + 1] === '*') {
            out += '  ';
            i += 2;
            while (i < n && !(sql[i] === '*' && sql[i + 1] === '/')) {
                out += sql[i] === '\n' ? '\n' : ' ';
                i++;
            }
            if (i < n) { out += '  '; i += 2; }
            continue;
        }

        if (sql[i] === "'") {
            out += ' ';
            i++;
            while (i < n) {
                if (sql[i] === "'" && sql[i + 1] === "'") { out += '  '; i += 2; continue; }
                if (sql[i] === "'") { out += ' '; i++; break; }
                out += sql[i] === '\n' ? '\n' : ' ';
                i++;
            }
            continue;
        }

        out += sql[i];
        i++;
    }

    return out;
}

/** Splits SQL into semicolon-terminated statements, ignoring semicolons inside comments or
 * string literals — found by locating them in the masked text, then slicing the *original* text
 * at the same offsets, so a reported statement is exactly what the migration file says. */
function splitStatements(sql) {
    const masked = maskCommentsAndStrings(sql);
    const statements = [];
    let start = 0;

    for (let i = 0; i < masked.length; i++) {
        if (masked[i] === ';') {
            statements.push(sql.slice(start, i + 1));
            start = i + 1;
        }
    }
    const tail = sql.slice(start);
    if (tail.trim().length > 0) statements.push(tail);

    return statements
        .map((s) => s.trim())
        .filter((s) => s.length > 0 && maskCommentsAndStrings(s).replace(/[\s;]/g, '').length > 0);
}

/** Drops whole leading comment lines from a statement so a refusal message quotes the SQL
 * itself, not the paragraph of prose above it in the file. */
function leadStatement(statement) {
    const lines = statement.split('\n');
    while (lines.length > 1 && /^\s*--/.test(lines[0])) lines.shift();
    return lines.join('\n').trim();
}

/**
 * classifyMigrationSql(sql) -> { destructive, statements, overridden }
 *
 * `statements` holds only the *offending* statements — the ones a destructive pattern matched,
 * in file order, with their own leading comments trimmed off — so a caller can quote one
 * directly in a refusal message. An additive migration returns an empty array here, never the
 * whole file.
 *
 * `overridden` is independent of `destructive`: it is true whenever the file contains the exact
 * `-- pp:allow-destructive: <reason>` line, whether or not anything in the file actually needed
 * it. Deciding what to do with a destructive-and-overridden migration is `decide()`'s job, not
 * this function's — this function only reports what is true about the SQL.
 */
export function classifyMigrationSql(sql) {
    const text = sql ?? '';
    const statements = splitStatements(text);

    const offending = [];
    for (const statement of statements) {
        const masked = maskCommentsAndStrings(statement);
        if (DESTRUCTIVE_PATTERNS.some((re) => re.test(masked))) {
            offending.push(leadStatement(statement));
        }
    }

    let overridden = false;
    for (const rawLine of text.split('\n')) {
        const m = OVERRIDE_RE.exec(rawLine.trim());
        if (m && m[1].trim().length > 0) { overridden = true; break; }
    }

    return { destructive: offending.length > 0, statements: offending, overridden };
}

// ---------------------------------------------------------------------------------------------
// readPending — parsing `wrangler d1 migrations list`'s own prose
// ---------------------------------------------------------------------------------------------

// Both of wrangler@4.127.0's answers, read against the real database before this was written
// (see docs/guide/hosting.md). Nothing else is "recognised" — a reworded release, a usage dump
// from the wrong wrangler version (3.90.0 cannot even parse `--cwd`; see the same guide), or an
// authorisation error must all fail rather than be misread as one of these two shapes.
const NOTHING_PENDING_RE = /No migrations to apply/i;
const SOMETHING_PENDING_RE = /Migrations to be applied/i;

// A row of wrangler's own ASCII-art table: a real vertical bar or the Unicode box-drawing one,
// a migration filename, and another bar. The filename shape (four digits, an underscore, then
// the rest) is what stops the header row ("Name") or the border from being misread as a file.
const TABLE_ROW_RE = /[│|]\s*([0-9]{4}_[A-Za-z0-9_-]+\.sql)\s*[│|]/;

/**
 * readPending(wranglerStdout) -> { recognised, pending }
 *
 * `recognised` is false for anything that is not one of the two shapes above — including empty
 * output, which matches neither regular expression and so falls through to the same refusal as
 * a genuinely unfamiliar answer. There is no third, "maybe" outcome: a caller that cannot tell
 * whether a migration is pending must refuse, not proceed.
 */
export function readPending(wranglerStdout) {
    const text = wranglerStdout ?? '';

    if (NOTHING_PENDING_RE.test(text)) return { recognised: true, pending: [] };

    if (SOMETHING_PENDING_RE.test(text)) {
        const pending = [];
        for (const line of text.split('\n')) {
            const m = TABLE_ROW_RE.exec(line);
            if (m) pending.push(m[1]);
        }
        return { recognised: true, pending };
    }

    return { recognised: false, pending: [] };
}

// ---------------------------------------------------------------------------------------------
// decide — the whole verdict
// ---------------------------------------------------------------------------------------------

// The exact text Cloudflare answered with when the deploy's token first tried this (see
// docs/guide/hosting.md and PR #104/#105's history) — checked first and unconditionally, because
// an unauthorised answer must never be misread as "nothing pending".
const UNAUTHORIZED_RE = /account is not valid or is not authorized|\[code:\s*7403\]/i;

function unauthorizedMessage(rawOutput) {
    return [
        'Refusing to deploy: wrangler could not read D1 migration state for this token.',
        'Cloudflare answered:',
        '  ' + rawOutput.trim().split('\n').slice(0, 4).join('\n  '),
        '',
        'This is not "nothing pending" — it is "cannot tell". Grant this deploy token D1: Edit',
        'on this account, alongside whatever it already has for Cloudflare Pages, then re-run',
        'the deploy. Treating this answer as all-clear is the exact mistake this gate exists to',
        'refuse: see docs/guide/hosting.md.',
    ].join('\n');
}

function unrecognisedMessage(rawOutput, listStatus) {
    const body = (rawOutput ?? '').trim();
    return [
        `Refusing to deploy: wrangler's answer (exit ${listStatus ?? 'unknown'}) matches neither`,
        'shape this gate knows — "No migrations to apply!" or "Migrations to be applied:" — so',
        'whether a migration is pending cannot be told from here. Reading prose means a reworded',
        'release must not be read as all clear, so this refuses rather than guessing.',
        '',
        body.length > 0 ? `wrangler's output:\n  ${body.split('\n').join('\n  ')}` : "wrangler produced no output at all.",
    ].join('\n');
}

function refusalStatementLine(entry) {
    if (entry.missing) return `  ${entry.name}: no local migration file content was supplied for it`;
    return `  ${entry.name}: ${entry.statements[0]}`;
}

function destructiveRefusalMessage(refused) {
    return [
        'Refusing to deploy: a pending migration contains a destructive statement, and this gate',
        'never applies one unattended.',
        '',
        ...refused.map(refusalStatementLine),
        '',
        'If this is deliberate, name the reason in the migration file itself, on its own line:',
        '  -- pp:allow-destructive: <reason>',
        'then re-run the deploy. That is the only way past this refusal — the check is not',
        'edited to let one specific migration through.',
    ].join('\n');
}

function applyMessage(pending, overriddenNames) {
    const base = `Applying ${pending.length} pending migration(s): ${pending.join(', ')}.`;
    if (overriddenNames.length === 0) return base;
    return `${base}\nA destructive statement in ${overriddenNames.join(', ')} was allowed through `
        + 'by an explicit pp:allow-destructive marker in the file.';
}

/**
 * decide({ listStdout, listStatus, migrations }) -> { action, exitCode, message }
 *
 * `migrations` is `[{ name, sql }]` for the files the caller has already matched to the pending
 * names `readPending` would report — this function does no filesystem read of its own. A pending
 * name with no corresponding entry is treated exactly like a destructive migration with no
 * override: refused, because content this gate cannot see is not content it can call safe.
 *
 * `action` is one of 'proceed' (nothing pending — do not even run `wrangler ... apply`), 'apply'
 * (safe to run it), or 'refuse' (abort the deploy). `exitCode` is 0 for the first two and 1 for
 * the third, so a shell driver can use it directly.
 */
export function decide({ listStdout, listStatus, migrations }) {
    const rawOutput = listStdout ?? '';

    if (UNAUTHORIZED_RE.test(rawOutput)) {
        return { action: 'refuse', exitCode: 1, message: unauthorizedMessage(rawOutput) };
    }

    const { recognised, pending } = readPending(rawOutput);

    if (!recognised) {
        return { action: 'refuse', exitCode: 1, message: unrecognisedMessage(rawOutput, listStatus) };
    }

    if (pending.length === 0) {
        return {
            action: 'proceed',
            exitCode: 0,
            message: 'Every migration in d1/migrations is already applied to the remote database.',
        };
    }

    const byName = new Map((migrations ?? []).map((m) => [m.name, m]));

    const classified = pending.map((name) => {
        const found = byName.get(name);
        if (!found) return { name, missing: true };
        return { name, missing: false, ...classifyMigrationSql(found.sql) };
    });

    const refused = classified.filter((c) => c.missing || (c.destructive && !c.overridden));

    if (refused.length > 0) {
        return { action: 'refuse', exitCode: 1, message: destructiveRefusalMessage(refused) };
    }

    const overriddenNames = classified.filter((c) => c.destructive && c.overridden).map((c) => c.name);

    return { action: 'apply', exitCode: 0, message: applyMessage(pending, overriddenNames) };
}

// ---------------------------------------------------------------------------------------------
// CLI entry point — the only part of this file that touches disk, stdin, or exit codes.
// ---------------------------------------------------------------------------------------------
//
// Guarded so that importing this module (as the test suite does) never runs any of this. Reads
// wrangler's own `d1 migrations list` output from stdin, its exit code from D1_LIST_EXIT_CODE,
// loads the SQL for whichever files it names as pending from D1_MIGRATIONS_DIR (defaulting to
// this repository's own d1/migrations), calls decide(), and prints exactly two things: a
// machine-readable `GATE_ACTION=<action>` first line for scripts/apply-migrations.sh to branch
// on without needing jq, then the human message. Exits with decide()'s own exitCode.

async function readStdin() {
    const chunks = [];
    for await (const chunk of process.stdin) chunks.push(chunk);
    return Buffer.concat(chunks).toString('utf8');
}

async function main() {
    const migrationsDir = process.env.D1_MIGRATIONS_DIR
        ?? path.join(path.dirname(fileURLToPath(import.meta.url)), '..', '..', 'd1', 'migrations');

    const listStdout = await readStdin();
    const listStatus = process.env.D1_LIST_EXIT_CODE !== undefined
        ? Number(process.env.D1_LIST_EXIT_CODE)
        : undefined;

    const { pending } = readPending(listStdout);

    // A name with no file on disk is omitted rather than sent through as empty SQL — decide()
    // then refuses it as "missing", which is the safe reading. Empty SQL would classify as
    // additive, which is the unsafe one.
    const migrations = pending
        .filter((name) => existsSync(path.join(migrationsDir, name)))
        .map((name) => ({ name, sql: readFileSync(path.join(migrationsDir, name), 'utf8') }));

    const result = decide({ listStdout, listStatus, migrations });

    process.stdout.write(`GATE_ACTION=${result.action}\n`);
    process.stdout.write(result.message + '\n');
    process.exit(result.exitCode);
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
    main();
}
