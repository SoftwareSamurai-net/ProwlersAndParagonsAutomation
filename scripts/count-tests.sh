#!/usr/bin/env bash
# The five suites, counted by running them.
#
# ------------------------------------------------------------------------------------------------
# WHY THIS IS A SCRIPT AND NOT A LINE IN PROGRESS.md
#
# That row carried the five figures in prose and **went wrong four separate ways** — its own text
# recorded all four before this script replaced it:
#
#   1. Summands that did not add up, because they were copied out of mid-branch commit messages
#      and three more commits landed after them.
#   2. A count read off `main`'s CI and compared against a local baseline measured days earlier,
#      from which somebody concluded that one test exists on Linux and not on Windows. It does not.
#   3. The same again, one slice later.
#   4. A row rewritten on a branch while `main` rewrote it too — five rebases in one afternoon,
#      three of them on this line alone.
#
# Every one of those is the same fault: a figure that lives in someone else's process, written down
# where it cannot be re-derived. So it is not written down. Run this.
#
# ------------------------------------------------------------------------------------------------
# WHAT IT DOES NOT DO
#
# It does not compare against a stored number, and there is deliberately nothing to compare
# against: a committed baseline is the thing that goes stale, and a test count is not a quality
# gate — the suites failing is. This prints what is there, and `git stash`-free comparison against
# another commit is `git worktree add` plus a second run.
set -uo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

fail=0
counted=""

# **Every count is read out of a line the runner printed, and a missing line is an error rather
# than a zero.** A suite that did not run and a suite with no tests are indistinguishable from a
# grep that found nothing, and this repository has shipped that mistake more than once — see
# `docs/guide/testing.md` on the harness that reported PASS because it never executed.
# **Not a command substitution**, and that is the fix rather than the style: `x="$(require …)"`
# runs the function in a subshell, so the `fail=1` inside it is set in a process that then exits
# and the caller goes on to sum a "?" — which is what the first version of this script did, and
# what its own arithmetic error reported. It assigns through a named variable instead.
# **Zero is refused as well as empty, and that was found by breaking this.** Pointing the pixel
# comparator's glob at a file name that does not exist did not produce silence — `node --test`
# matched nothing, printed `pass 0`, and this script totalled the other four and called it a
# result. A suite that did not run and a suite with no tests are the same line, which is the
# failure this whole function exists to catch; none of these five has ever held fewer than 14
# tests, so zero is not a state to report, it is a state to refuse.
require() {
    local what="$1" value="$2"
    if [ -z "$value" ] || [ "$value" -eq 0 ]; then
        echo "error: no count for $what — it did not run, or its output has changed." >&2
        fail=1
        counted=""
    else
        counted="$value"
    fi
}

echo "Running all five suites. This takes a couple of minutes."
echo

dotnet_out="$(dotnet test --nologo -v q 2>&1)"
engine="$(echo "$dotnet_out" | grep -oE 'Passed: +[0-9]+.*ProwlersAndParagonsAutomation\.Tests' | grep -oE 'Passed: +[0-9]+' | grep -oE '[0-9]+' | head -1)"
web="$(echo "$dotnet_out" | grep -oE 'Passed: +[0-9]+.*ProwlersAndParagons\.Web\.Tests' | grep -oE 'Passed: +[0-9]+' | grep -oE '[0-9]+' | head -1)"

if echo "$dotnet_out" | grep -q 'Failed!'; then
    echo "error: a .NET suite failed. Counting a red suite tells you nothing." >&2
    fail=1
fi

require 'the engine suite' "$engine"; engine="$counted"
require 'the bUnit suite' "$web"; web="$counted"

accounts="$(./scripts/test-worker.sh 2>&1 | grep -oE '[[:space:]]pass [0-9]+' | grep -oE '[0-9]+' | head -1)"
require 'the accounts suite' "$accounts"; accounts="$counted"

visual="$(./scripts/test-visual.sh 2>&1 | grep -oE '[[:space:]]pass [0-9]+' | grep -oE '[0-9]+' | head -1)"
require 'the pixel comparator' "$visual"; visual="$counted"

gate="$(./scripts/test-deploy-gate.sh 2>&1 | grep -oE '[[:space:]]pass [0-9]+' | grep -oE '[0-9]+' | head -1)"
require 'the deploy gate' "$gate"; gate="$counted"

printf '%-28s %s\n' "engine" "$engine"
printf '%-28s %s\n' "bUnit (components)" "$web"
printf '%-28s %s\n' "accounts server" "$accounts"
printf '%-28s %s\n' "pixel comparator" "$visual"
printf '%-28s %s\n' "deploy migration gate" "$gate"

if [ "$fail" -eq 0 ]; then
    echo "--------------------------------------"
    printf '%-28s %s\n' "total" "$(( engine + web + accounts + visual + gate ))"
else
    echo
    echo "No total: at least one suite did not report, so any sum would be a guess." >&2
fi

exit "$fail"
