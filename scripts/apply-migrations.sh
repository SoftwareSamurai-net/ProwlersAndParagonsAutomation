#!/usr/bin/env bash
# Applies pending D1 migrations, and aborts loudly rather than shipping past one it cannot read.
#
# This is what closes the gap docs/guide/hosting.md used to record as open: a migration merged
# into d1/migrations and a deploy of the code that depends on it were two separate acts, nothing
# reminded anyone, and the manual step between them was skipped once — production answered
# `D1_ERROR: no such column: campaign_id` on every character save until somebody noticed. This
# script is that manual step, run automatically, in the one place ordering can make it safe:
# BEFORE the Pages upload (see deploy.yml — migration-then-upload leaves the schema briefly ahead
# of code that is always written to tolerate a column or table that already exists, never behind
# it).
#
# The decision of whether to apply, refuse, or do nothing at all lives in
# scripts/d1-migrations/gate.mjs, and is pure — this script's only job is to hand it wrangler's
# real output and act on what it says. See that file's own header for why the split, and
# tests/deploy/migration-gate.test.mjs for the decision driven through every branch.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Pinned in exactly this one place. wrangler@3.90.0 — the version deploy.yml's
# cloudflare/wrangler-action@v3 bundles with for the actual Pages upload — does not understand
# `--cwd` at all and answers every `d1 migrations` invocation with that flag with a *usage dump*
# for the subcommand rather than a result (verified against the real binary: `npx --yes
# wrangler@3.90.0 --cwd d1 d1 migrations list prowlers-and-paragons --remote` prints "Unknown
# argument: cwd" and the command's own --help text). gate.mjs's readPending() would correctly
# call that "unrecognised" and refuse — but refusing on every single run because of a version
# mismatch is not what this exists to catch. 4.127.0's two answers ("Migrations to be applied:"
# with a table, or "No migrations to apply!") were read against the real production database
# before this was written.
WRANGLER_VERSION="4.127.0"

DB="prowlers-and-paragons"

# `--cwd d1` is how the existing manual command (docs/guide/hosting.md, docs/ACCOUNTS-SETUP.md)
# reaches d1/wrangler.toml, which is deliberately not at the repository root — see that file's
# own header comment for why a root wrangler.toml would change the Pages deploy's own behaviour.
CONFIG_DIR="$root/d1"

# The seam the test suite would use to drive this script with a fake `wrangler` rather than the
# real network-calling binary, if it chooses to: set WRANGLER_BIN to any command (a path to a
# stub script, "echo canned-output", …) and it runs in place of `npx --yes wrangler@<pinned>`.
# Left unset in ordinary use — this script's own behaviour is exercised at the shell level, not
# unit-tested against real Cloudflare state, which is why tests/deploy/migration-gate.test.mjs
# tests gate.mjs's pure decision instead and leaves this driver covered by its use in deploy.yml.
WRANGLER="${WRANGLER_BIN:-npx --yes wrangler@${WRANGLER_VERSION}}"

echo "Listing pending D1 migrations for '$DB' with wrangler@${WRANGLER_VERSION} (--cwd $CONFIG_DIR)..."

list_output="$(mktemp)"
trap 'rm -f "$list_output"' EXIT

# Captured whether or not wrangler itself exits non-zero — an authorisation failure or a
# malformed command both print to stdout/stderr and exit non-zero, and the gate needs to see
# that text to tell an auth failure from "nothing pending" rather than treat a non-zero exit as
# reason to stop before the gate ever runs.
set +e
# shellcheck disable=SC2086 # $WRANGLER is a deliberately word-split command, not a path.
$WRANGLER --cwd "$CONFIG_DIR" d1 migrations list "$DB" --remote > "$list_output" 2>&1
list_status=$?
set -e

echo "----- wrangler's own output (exit $list_status) -----"
cat "$list_output"
echo "------------------------------------------------------"

# Feeds the gate wrangler's exact stdout/stderr and exit code, and lets it read the SQL for
# whichever files it names as pending straight out of this checkout — never a copy, never a
# summary. Reports GATE_ACTION=<action> as the gate's own first line so this script can branch on
# it without needing jq, then the human-readable message on every line after that.
set +e
decision="$(D1_LIST_EXIT_CODE="$list_status" D1_MIGRATIONS_DIR="$root/d1/migrations" \
    node "$root/scripts/d1-migrations/gate.mjs" < "$list_output")"
gate_status=$?
set -e

action_line="$(printf '%s\n' "$decision" | head -n 1)"
message="$(printf '%s\n' "$decision" | tail -n +2)"
action="${action_line#GATE_ACTION=}"

echo "$message"

if [ "$gate_status" -ne 0 ]; then
    echo "::error::Refusing to deploy — see the gate's message above."
    exit "$gate_status"
fi

case "$action" in
    proceed)
        echo "Nothing pending; skipping 'wrangler d1 migrations apply'."
        ;;
    apply)
        echo "Applying the pending migration(s) now..."
        # shellcheck disable=SC2086
        $WRANGLER --cwd "$CONFIG_DIR" d1 migrations apply "$DB" --remote
        echo "Applied."
        ;;
    *)
        # The gate exited 0 but named neither known action — a contract mismatch between this
        # script and gate.mjs, not a state the deploy should ever guess its way past.
        echo "::error::gate.mjs reported action '$action', which this script does not recognise." >&2
        echo "::error::Refusing to guess whether that meant it was safe to deploy." >&2
        exit 1
        ;;
esac
