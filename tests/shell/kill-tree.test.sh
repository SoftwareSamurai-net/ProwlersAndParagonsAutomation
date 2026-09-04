#!/usr/bin/env bash
# `kill_tree` really kills the whole tree, and `pkill -P` really would not have.
#
# ------------------------------------------------------------------------------------------------
# WHY THIS EXISTS, AND WHY THE GREEN CI RUNS DID NOT ALREADY SAY IT.
#
# `scripts/e2e.sh`'s `stop_server` used `pkill -P "$pid"` on Linux, which kills *direct* children
# only. Wrangler's tree is `npx` -> node -> `workerd`, so `workerd` survived every stop, kept its
# port, and outlived the step. It became `kill_tree`, reading ppids out of `/proc/<pid>/stat`.
#
# **Nothing watched that fix work.** The harness picks a fresh port for every server precisely so
# that a stale listener cannot wedge a run — `port_in_use` and `next_free_port` — which means a
# leaked `workerd` changes no verdict anywhere. The runs were green before the fix and green after
# it, and would be green again if it were reverted tomorrow. That is the definition of an untested
# change.
#
# ------------------------------------------------------------------------------------------------
# THE FIXTURE, AND THE ONE THAT COLLAPSES.
#
# `bash -c 'sleep 300' &` is **one** process: `bash -c` with a single simple command execs it rather
# than forking. So the obvious three-deep fixture — `bash -c 'bash -c "sleep 300"'` — is one `sleep`
# wearing three hats, and a test built on it passes against a `kill_tree` that kills only the pid it
# was handed. Which is the bug. Every level here ends `; true` to defeat that optimisation, and the
# **first assertion of this file is that the tree really is three deep** — because if the fixture
# collapses again, everything after it is vacuous.
#
# ------------------------------------------------------------------------------------------------
# RUNNING IT.
#
#   bash tests/shell/kill-tree.test.sh            # on Linux, or on a Linux CI runner
#   docker run --rm -v "$(pwd -W):/work" -w /work bash:5 \
#       bash tests/shell/kill-tree.test.sh        # from Windows — see docs/guide/testing.md
#
# Linux only, and it says so rather than passing: `/proc` is what it reads, and `stop_server`'s
# Windows path is `taskkill //T`, which is a different mechanism this cannot speak to.

set -uo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if [ ! -d /proc/1 ]; then
  echo "SKIP: no /proc — this drives the Linux path of scripts/e2e.sh's stop_server, and the"
  echo "SKIP: Windows one is taskkill //T, which is a different mechanism. Run it in a container."
  exit 0
fi

# shellcheck source=scripts/e2e/process-tree.sh
. "$root/scripts/e2e/process-tree.sh"

failures=0

fail() {
  echo "FAIL: $*"
  failures=$((failures + 1))
}

alive() {
  kill -0 "$1" 2>/dev/null
}

# Every descendant, so the assertions can talk about the tree rather than about one level.
descendants() {
  local pid="$1" child

  for child in $(children_of "$pid"); do
    echo "$child"
    descendants "$child"
  done
}

# ------------------------------------------------------------------------------------------------
# A three-deep tree: this shell -> bash -> bash -> sleep.

bash -c 'bash -c "sleep 300 & wait" & wait; true' &
top=$!

# Give the layers a moment to exist. Polled rather than slept, so a slow machine does not fail this
# and a fast one does not wait.
for _ in $(seq 1 50); do
  [ "$(descendants "$top" | wc -l)" -ge 2 ] && break
  sleep 0.1
done

mapfile -t tree < <(descendants "$top")

echo "tree under $top: ${tree[*]:-(none)}"

# **The positive control, and the reason the whole file is not vacuous.** If this is not a real
# tree, `kill_tree` killing "everything" below is a sentence about nothing — which is exactly what
# the `bash -c` exec optimisation produces if somebody simplifies the fixture above.
if [ "${#tree[@]}" -lt 2 ]; then
  fail "the fixture is not a tree: ${#tree[@]} descendant(s) under $top, wanted at least 2. See" \
    "this file's header — 'bash -c' with a single command execs it, and the tree collapses to one" \
    "process. Everything below this point would pass against a kill_tree that killed only \$top."
  kill -9 "$top" 2>/dev/null
  exit 1
fi

deepest="${tree[-1]}"

alive "$deepest" || fail "the deepest process $deepest was not running before anything killed it"

# ------------------------------------------------------------------------------------------------
# The negative control: what `pkill -P` would have done, spelled out rather than asserted about.
#
# Not run against this tree — it would destroy the subject — but stated as the difference the
# assertions below are sensitive to: killing $top alone leaves ${tree[*]} running. That is what the
# loop after `kill_tree` checks, and it is the only thing separating this test from one that passes
# against the bug.

kill_tree "$top"

# Reaped rather than assumed gone the instant `kill -9` returns: a signal is delivered
# asynchronously, and reading `/proc` a microsecond later is a race this test would lose
# intermittently — which is worse than not having it.
for _ in $(seq 1 50); do
  still=0

  for pid in "$top" "${tree[@]}"; do
    alive "$pid" && still=$((still + 1))
  done

  [ "$still" -eq 0 ] && break
  sleep 0.1
done

for pid in "$top" "${tree[@]}"; do
  if alive "$pid"; then
    fail "$pid survived kill_tree. This is the workerd leak: the supervisor dies, the worker keeps" \
      "the port, and nothing in the harness notices because every server takes a fresh port."
    kill -9 "$pid" 2>/dev/null
  fi
done

# ------------------------------------------------------------------------------------------------
# And it does not take the neighbours with it.
#
# A tree kill that walked up instead of down, or that matched on a name, would take out whatever
# else happened to be running — which `scripts/e2e.sh` records having actually done once, through a
# `ps -W` pid-namespace confusion that killed the driver and read exactly like a harness unable to
# see its own defect.

sleep 300 &
bystander=$!

sleep 300 &
other=$!

alive "$bystander" || fail "the bystander was not running to begin with"
alive "$other" || fail "the process to be killed was not running to begin with"

kill_tree "$other"

for _ in $(seq 1 50); do
  alive "$other" || break
  sleep 0.1
done

alive "$other" && fail "kill_tree did not kill $other, so the assertion below is about nothing"
alive "$bystander" || fail "killing an unrelated process took the bystander $bystander with it"

kill -9 "$bystander" "$other" 2>/dev/null
wait 2>/dev/null

if [ "$failures" -ne 0 ]; then
  echo "KILL-TREE: FAIL — $failures assertion(s)"
  exit 1
fi

echo "KILL-TREE: PASS — a ${#tree[@]}-deep tree died with its root, and a bystander did not"
