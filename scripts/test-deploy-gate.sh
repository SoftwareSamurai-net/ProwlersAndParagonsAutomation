#!/usr/bin/env bash
# Runs the D1 migration gate's own tests — scripts/d1-migrations/gate.mjs — under
# tests/deploy/*.test.mjs.
#
# This is plain Node, the same as the accounts server's suite (see scripts/test-worker.sh) and
# the visual-regression comparator's (see scripts/test-visual.sh, which this mirrors): no npm
# dependency, because this repository has never had a package.json or a node_modules anywhere in
# it. gate.mjs's own decision logic touches no filesystem and no network, so unlike the accounts
# server this suite has no real floor above Node's own `node --test` support — but it is pinned
# to the same Node 22 the rest of this repository's JavaScript targets, for one consistent answer
# to "what runs this" rather than a different one for every script.
#
#   * Node 22 or later on PATH, which this script uses if it finds it.
#   * Docker, otherwise. The image is the official Node one and nothing is installed on the
#     machine — the same fallback scripts/test-worker.sh and scripts/test-visual.sh use, for the
#     same reason: these tests were written on a machine that might not have Node.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

run_and_count() {
    # **Assert the count, not the exit code.** `node --test` with a glob that matches nothing
    # exits 0 and reports zero tests, which reads exactly like a suite that passed — the same
    # trap CLAUDE.md names for the accounts server's suite, in the same spelling.
    #
    # **`--test-reporter=tap` is explicit rather than relied upon as a default**, for the same
    # reason scripts/test-visual.sh gives: measured, not assumed, and Node's "tap when stdout is
    # not a TTY" default has already failed to hold once on this repository's own tooling.
    local tap="$1"; shift
    "$@" | tee "$tap"
    local passed
    passed=$(grep -c '^ok ' "$tap" || true)
    echo "The D1 migration gate's suite reported ${passed} passing tests."
    if [ "${passed:-0}" -lt 10 ]; then
        echo "::error::only ${passed} deploy-gate tests ran; the glob has stopped matching." >&2
        return 1
    fi
}

if command -v node >/dev/null 2>&1; then
    major="$(node --version | sed 's/^v\([0-9]*\).*/\1/')"

    if [ "$major" -ge 22 ]; then
        echo "Running the D1 migration gate's tests on local Node $(node --version)."
        cd "$root"
        run_and_count /tmp/pp-deploy-gate.tap node --test --test-reporter=tap "tests/deploy/*.test.mjs"
        exit $?
    fi

    echo "Local Node $(node --version) is too old (22 or later is needed); falling back to Docker." >&2
fi

if ! docker version --format '{{.Server.Version}}' >/dev/null 2>&1; then
    echo "error: these tests need Node 22+ or a running Docker daemon, and neither is available." >&2
    echo "       Install Node (winget install OpenJS.NodeJS.LTS) or start Docker Desktop." >&2
    exit 1
fi

echo "Running the D1 migration gate's tests in Docker (no local Node)."

# MSYS_NO_PATHCONV stops Git Bash rewriting the container-side paths into Windows ones, which
# fails with "the working directory 'W:/' is invalid" and looks like a Docker fault — the same
# trap documented in scripts/test-worker.sh.
MSYS_NO_PATHCONV=1 docker run --rm \
    -v "$(cd "$root" && pwd -W 2>/dev/null || echo "$root"):/repo" \
    -w /repo \
    node:22-alpine \
    sh -c 'node --test --test-reporter=tap "tests/deploy/*.test.mjs" | tee /tmp/pp-deploy-gate.tap; \
           passed=$(grep -c "^ok " /tmp/pp-deploy-gate.tap || true); \
           echo "The D1 migration gate'"'"'s suite reported ${passed} passing tests."; \
           [ "${passed:-0}" -ge 10 ] || { echo "only ${passed} deploy-gate tests ran" >&2; exit 1; }'
