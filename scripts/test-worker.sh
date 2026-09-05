#!/usr/bin/env bash
# Runs the accounts API's tests.
#
# The server is JavaScript because Cloudflare Workers is, so it needs a JavaScript runtime that
# this repository's .NET toolchain does not bring. There are two ways to have one:
#
#   * Node 22 or later on PATH, which this script uses if it finds it.
#   * Docker, otherwise. The image is the official Node one and nothing is installed on the
#     machine — which is how these tests were written, on a machine with no Node at all.
#
# Node 22 is the floor for both, because the tests run the real migration against real SQLite
# through `node:sqlite`, and because `worker/corpus.js` imports JSON with an import attribute.
# D1 is SQLite, so the two statements in `worker/db.js` that close a race by being one statement
# are executed here rather than described.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if command -v node >/dev/null 2>&1; then
    major="$(node --version | sed 's/^v\([0-9]*\).*/\1/')"

    if [ "$major" -ge 22 ]; then
        echo "Running the accounts API's tests on local Node $(node --version)."
        cd "$root"
        exec node --test "tests/worker/*.test.mjs"
    fi

    echo "Local Node $(node --version) is too old (22 or later is needed); falling back to Docker." >&2
fi

if ! docker version --format '{{.Server.Version}}' >/dev/null 2>&1; then
    echo "error: these tests need Node 22+ or a running Docker daemon, and neither is available." >&2
    echo "       Install Node (winget install OpenJS.NodeJS.LTS) or start Docker Desktop." >&2
    exit 1
fi

echo "Running the accounts API's tests in Docker (no local Node)."

# MSYS_NO_PATHCONV stops Git Bash rewriting the container-side paths into Windows ones, which
# fails with "the working directory 'W:/' is invalid" and looks like a Docker fault.
MSYS_NO_PATHCONV=1 exec docker run --rm \
    -v "$(cd "$root" && pwd -W 2>/dev/null || echo "$root"):/repo" \
    -w /repo \
    node:22-alpine \
    node --test "tests/worker/*.test.mjs"
