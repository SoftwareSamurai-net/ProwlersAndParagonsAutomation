#!/usr/bin/env bash
# Proves `stop_server` kills the whole tree and frees the port. Directly, by reading the pids.
#
#   ./scripts/test-kill-tree.sh                  # both trees: the synthetic one and a real wrangler
#   ./scripts/test-kill-tree.sh --skip-wrangler  # the synthetic one only. NOT a full run
#
# ------------------------------------------------------------------------------------------------
# WHY THIS EXISTS, AND WHY AN OUTCOME CHECK WAS NOT ENOUGH.
#
# `PROGRESS.md` item 10 records `kill_tree` as the one fix in this harness that was never watched
# to work. `stop_server`'s Linux arm was `pkill -P`, which kills *direct* children only; a real
# `wrangler pages dev` tree is four processes deep, so `workerd` outlived its step still holding a
# port. The recursive walk of `/proc/<pid>/stat` was written to fix that. The identical defect then
# reappeared on macOS, which has no `/proc` at all, so `children_of` returned nothing there too —
# silently — and `kill_tree` again killed only the `npx` wrapper.
#
# **Both halves were verified by outcome: "no leaked processes after a run".** That check passes on
# Linux *while the leak continues*, because `next_free_port` steps over the held port and never
# asks for it again — so a green run cannot distinguish "nothing leaked" from "something leaked and
# nothing looked". The runner has been emitting `something is still listening on port N after 30s`
# for every twin of every run, and the run has been green every time.
#
# So this asks the question the other way round. It starts a tree, records **every pid in it from
# an instrument that is not the one under test**, calls `stop_server`, and then probes each pid
# with `kill -0`. A surviving process is named. Nothing here reads "the run finished cleanly" as
# evidence of anything.
#
# ------------------------------------------------------------------------------------------------
# THE POSITIVE CONTROL, WHICH IS MOST OF THE VALUE.
#
# "Everything in the tree is dead" is satisfied perfectly by a tree that never started, and
# "nothing is listening on the port" by a fixture that never bound it. Three of this repository's
# four historical guard faults were exactly that. So **before** the stop, each case asserts that
# the tree had at least three live processes at three different depths and that the port really was
# listening — and reports a failure of that as `[CONTROL]`, separately from `[OUTCOME]`, because
# "the fixture did not run" and "the kill did not work" are different bug reports.
#
# **The tree is enumerated with `ps -eo pid,ppid` and not with `children_of`.** Using the function
# under test to collect the pids it is then asked about would make an empty answer look like a
# clean kill, which is the precise shape of the fault this file exists to catch.
#
# **And the fixture is a genuine multi-process tree, which takes some care.** The obvious
# `bash -c 'node -e "…" & node -e "…" & wait'` collapses: `bash -c` with a single command *execs*
# it, so you get one process where you meant three, and a `kill_tree` that only kills the pid it
# was handed passes against it. The fixture below is a bash wrapper that backgrounds a node
# spawner, which itself spawns a node listener — three processes at three depths, with the port
# held by the deepest, so two levels of recursion are needed to reach it.
#
# ------------------------------------------------------------------------------------------------
# IT IS NOT IN ./scripts/count-tests.sh, FOR THE REASON THAT FILE ALREADY GIVES.
#
# `docs/guide/testing.md` states it for `./scripts/e2e.sh`: a script that reports *verdicts*
# rather than a test count cannot be totalled with four suites' thousands without making the
# total meaningless, and a missing count there has to be an error rather than a zero. This one
# reports three verdicts, so it is its own step in `build.yml` and prints its own summary line.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# The functions under test. Sourcing this runs nothing — that is why they are in a file of their
# own rather than in `scripts/e2e.sh`, which cannot be sourced without publishing a site.
# shellcheck source=scripts/e2e/process.sh
. "$root/scripts/e2e/process.sh"

skip_wrangler=0

while [ $# -gt 0 ]; do
  case "$1" in
    --skip-wrangler) skip_wrangler=1 ;;
    -h|--help)
      echo "usage: $0 [--skip-wrangler]"
      echo ""
      echo "  --skip-wrangler   run only the synthetic tree. NOT a full run: the tree that"
      echo "                    actually leaked is wrangler's, and it is four processes deep."
      exit 0
      ;;
    *) echo "error: unrecognised argument '$1'" >&2; exit 2 ;;
  esac
  shift
done

failures=0
checks=0

pass() {
  checks=$((checks + 1))
  echo "KILL-TREE CHECK $1: PASS — $2"
}

fail() {
  checks=$((checks + 1))
  failures=$((failures + 1))
  echo "KILL-TREE CHECK $1: FAIL — $2"
  echo "::error::$1: $2"
}

# Every descendant of a pid, deepest first, from `ps` — deliberately a different instrument from
# `children_of`, which is what is being tested. `ps -eo pid=,ppid=` is POSIX and answers the same
# on macOS, Linux and Git Bash.
descendants_of() {
  local roots="$1" table next found
  table="$(ps -eo pid=,ppid= 2>/dev/null | tr -s ' ')"
  found=""

  while [ -n "$roots" ]; do
    next="$(printf '%s\n' "$table" \
      | awk -v want="$(printf '%s' "$roots" | tr '\n' ' ')" \
          'BEGIN { n = split(want, w, " "); for (i = 1; i <= n; i++) if (w[i] != "") p[w[i]] = 1 }
           ($2 in p) { print $1 }')"
    [ -n "$next" ] || break
    found="$next
$found"
    roots="$next"
  done

  printf '%s' "$found" | awk 'NF'
}

live() { kill -0 "$1" 2>/dev/null; }

# ------------------------------------------------------------------------------------------------
# Case 1 — the `/proc` parse, driven on this machine whatever this machine is.
#
# **The Linux arm cannot be reached on a Mac and the Mac is where this is written**, so
# `children_of` takes its process table from `$proc_root` and this builds a synthetic one out of
# the real `ps` output. It proves the *parse* — the `(comm) ` trim, the field offset, the ppid
# comparison — and nothing about killing anything; cases 2 and 3 do that against real processes,
# and only CI runs them on Linux.
#
# **One entry deliberately has a `) ` inside its command name.** `/proc/<pid>/stat`'s second field
# is `(comm)` and a comm may contain spaces and parentheses, which is the bug every naive reader
# of that file has. The trim is `${stat##*) }` — longest match — so a comm of `we) ird` must still
# leave the ppid in field 2. That case is in the fixture because it is the one a reader would
# assume rather than check.
proc_parse_case() {
  local fake pid ppid rest count kids
  fake="$(mktemp -d)"
  # shellcheck disable=SC2064 # $fake is wanted expanded now, not at trap time.
  trap "rm -rf '$fake'" RETURN

  count=0
  while read -r pid ppid rest; do
    case "$pid" in ''|*[!0-9]*) continue ;; esac
    mkdir -p "$fake/$pid"
    printf '%s (%s) S %s 0 0 0 -1 4194304 100 0 0 0 1 2 3 4 20 0 1 0 900 0 0\n' \
      "$pid" "${rest:-x}" "$ppid" > "$fake/$pid/stat"
    count=$((count + 1))
  done < <(ps -eo pid=,ppid=,comm= 2>/dev/null | tr -s ' ' | sed 's/^ //')

  # A tree this machine's real process table does not contain, so the expected answers are known
  # rather than inferred: 900001 -> 900002 -> 900003, with a comm that carries a `) `.
  mkdir -p "$fake/900001" "$fake/900002" "$fake/900003"
  printf '900001 (wrapper) S 1 0 0 0 -1 0 0 0 0 0 1 2 3 4 20 0 1 0 900 0 0\n' > "$fake/900001/stat"
  printf '900002 (we) ird name) S 900001 0 0 0 -1 0 0 0 0 0 1 2 3 4 20 0 1 0 900 0 0\n' \
    > "$fake/900002/stat"
  printf '900003 (listener) S 900002 0 0 0 -1 0 0 0 0 0 1 2 3 4 20 0 1 0 900 0 0\n' \
    > "$fake/900003/stat"
  count=$((count + 3))

  if [ "$count" -lt 4 ]; then
    fail PROC_PARSE "[CONTROL] the synthetic process table has only $count entries, so nothing"\
" below was actually walked."
    return
  fi

  local saved="$proc_root"
  proc_root="$fake"

  # The assignment form on purpose: `children="$(children_of …)"` is what `set -e` kills on a
  # non-zero return, and `children_of` used to return the status of whichever `/proc` entry it
  # happened to look at last. That landmine had never gone off because `e2e.sh` only ever uses the
  # `for child in $(children_of …)` form, where `set -e` exempts a command substitution in a word
  # list. This line is the regression test for the `return 0` at the end of that function.
  kids="$(children_of 900001)"
  local one_deep="$kids"
  kids="$(children_of 900002)"
  local two_deep="$kids"
  kids="$(children_of 900003)"
  local three_deep="$kids"

  proc_root="$saved"

  if [ "$one_deep" != "900002" ]; then
    fail PROC_PARSE "[OUTCOME] the /proc walk answered '$one_deep' for the children of 900001,"\
" and 900002 is the only one. The ppid field is being read from the wrong offset."
    return
  fi

  if [ "$two_deep" != "900003" ]; then
    fail PROC_PARSE "[OUTCOME] the /proc walk answered '$two_deep' for the children of 900002,"\
" whose command name contains ') '. The '(comm)' trim is not the longest match, so a process"\
" name with a bracket in it hides every child it has."
    return
  fi

  if [ -n "$three_deep" ]; then
    fail PROC_PARSE "[OUTCOME] the /proc walk answered '$three_deep' for the children of a leaf,"\
" which has none."
    return
  fi

  pass PROC_PARSE "the /proc walk read $count entries and resolved a three-deep tree at every"\
" level, including a command name containing ') '."
}

# ------------------------------------------------------------------------------------------------
# The shared body of cases 2 and 3: start a tree, prove it is alive and holding the port, stop it
# with the real `stop_server`, and probe every pid.
#
# `$1` is the check name, `$2` the port, and `$3` a command run as `bash -c` inside the same
# `( … ) &` shape `start_server` uses — so `$!`, and therefore `server_pid`, is the same kind of
# pid the harness actually hands `stop_server`.
drive_tree_case() {
  local name="$1" port="$2" launch="$3" ready_seconds="$4"
  local log tree n_alive survivors root_pid

  log="$(mktemp)"

  (
    exec bash -c "$launch" > "$log" 2>&1
  ) &
  server_pid=$!
  server_port="$port"
  # `stop_server` clears `server_pid` on the way in, so the failure messages below cannot read it.
  root_pid="$server_pid"

  local deadline=$((SECONDS + ready_seconds))
  while [ "$SECONDS" -lt "$deadline" ]; do
    port_in_use "$port" && break
    if ! kill -0 "$server_pid" 2>/dev/null; then break; fi
    sleep 1
  done

  # ---- positive control, before anything is asserted about the stop -----------------------------
  tree="$root_pid
$(descendants_of "$root_pid")"
  tree="$(printf '%s\n' "$tree" | awk 'NF')"
  n_alive=0
  local p
  for p in $tree; do live "$p" && n_alive=$((n_alive + 1)); done

  if [ "$n_alive" -lt 3 ]; then
    stop_server
    fail "$name" "[CONTROL] the fixture is not a multi-process tree: only $n_alive live"\
" process(es) under $root_pid. A kill that recurses and one that does not are"\
" indistinguishable against it. Tree: $(echo "$tree" | tr '\n' ' ')$(tail -5 "$log")"
    rm -f "$log"
    return
  fi

  if ! port_in_use "$port"; then
    stop_server
    fail "$name" "[CONTROL] nothing was listening on port $port before the stop, so 'the port is"\
" free afterwards' would have held against a fixture that never bound it. Tail of the fixture's"\
" own output: $(tail -5 "$log")"
    rm -f "$log"
    return
  fi

  local before="$tree"
  local before_count="$n_alive"

  # ---- the thing under test --------------------------------------------------------------------
  stop_server

  # `kill -9` is delivered, not awaited: a process is reaped by its parent, and these are
  # orphaned onto init when their own parent dies first. Two seconds is generous for that, and it
  # is bounded rather than a wait — a survivor has to be *reported*, not waited for indefinitely.
  local settle=$((SECONDS + 5))
  while [ "$SECONDS" -lt "$settle" ]; do
    survivors=""
    for p in $before; do live "$p" && survivors="$survivors $p"; done
    [ -n "$survivors" ] || break
    sleep 1
  done

  survivors=""
  for p in $before; do
    live "$p" && survivors="$survivors $p($(ps -o comm= -p "$p" 2>/dev/null | tr -d ' '))"
  done

  if [ -n "$survivors" ]; then
    echo "::error::$name: these processes outlived stop_server:$survivors"
    for p in $before; do live "$p" && kill -9 "$p" 2>/dev/null || true; done
    fail "$name" "[OUTCOME] $before_count processes were started and$survivors survived"\
" stop_server. kill_tree did not reach every level of the tree."
    rm -f "$log"
    return
  fi

  if port_in_use "$port"; then
    echo "::error::$name: port $port is still listening after stop_server."
    fail "$name" "[OUTCOME] every pid in the tree is gone and port $port is still listening, so"\
" something outside the tree of $root_pid is holding it."
    rm -f "$log"
    return
  fi

  pass "$name" "$before_count processes held port $port; after stop_server every one of them is"\
" gone and the port is free."
  rm -f "$log"
}

# ------------------------------------------------------------------------------------------------
# Case 2 — a synthetic three-deep tree. Fast, hermetic, no npm, and the one to mutate against.
synthetic_case() {
  local dir port
  dir="$(mktemp -d)"
  # shellcheck disable=SC2064
  trap "rm -rf '$dir'" RETURN

  # The listener: the deepest process, and the only one that binds. Two levels of recursion have
  # to be walked to reach it.
  cat > "$dir/listener.mjs" <<'EOF'
import { createServer } from 'node:net';
createServer(() => {}).listen(Number(process.argv[2]), '127.0.0.1');
setInterval(() => {}, 1 << 30);
EOF

  # The middle process: spawns the listener as a real child and stays alive above it.
  cat > "$dir/spawner.mjs" <<'EOF'
import { spawn } from 'node:child_process';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const here = dirname(fileURLToPath(import.meta.url));
spawn(process.execPath, [join(here, 'listener.mjs'), process.argv[2]], { stdio: 'inherit' });
setInterval(() => {}, 1 << 30);
EOF

  port="$(next_free_port 8890)"

  # `node … &` then `wait`, not `exec node …`: the wrapper has to survive as a process of its own,
  # or the tree is two deep instead of three. This is the collapse the header warns about, one
  # level up.
  drive_tree_case SYNTHETIC_TREE "$port" \
    "node '$dir/spawner.mjs' $port & wait" 20
}

# ------------------------------------------------------------------------------------------------
# Case 3 — the tree that actually leaked: a real `wrangler pages dev`.
#
# **This is the case with the evidence behind it.** Measured on macOS on 2026-09-05, the tree is
# four processes deep and the port is held at the bottom of it:
#
#     97475  npm exec wrangler@4.127.0 pages dev …     <- $!, the pid stop_server is handed
#     97492   node                                     <- npx's own runner
#     97493    node                                    <- wrangler
#     97499     workerd                                <- holds 127.0.0.1:<port>
#
# So `pkill -P` reached one level of four, and any walk that stops short of three recursions
# leaves the listener. Nothing synthetic reproduces that depth by accident, which is why this case
# pays for an `npx` rather than trusting case 2 to stand in for it.
#
# **The wrangler version is read out of deploy.yml, never declared here** — the same parse
# `scripts/e2e.sh` and `build.yml`'s dry-run do, and for the same reason: a fourth copy of a
# version number is a fourth thing to keep in step. `WranglerIsPinnedToOneVersion` holds the other
# three together.
wrangler_case() {
  local dir port version

  version=$(sed -n \
    's/^.*wrangler-action@v[0-9][0-9.]*.*# wrangler=\([0-9.]*\).*$/\1/p' \
    "$root/.github/workflows/deploy.yml") || version=''

  if [ -z "$version" ]; then
    fail WRANGLER_TREE "[CONTROL] could not read a wrangler version out of deploy.yml, so this"\
" case would have tested some other wrangler than the one the harness serves with."
    return
  fi

  dir="$(mktemp -d)"
  # shellcheck disable=SC2064
  trap "rm -rf '$dir'" RETURN

  mkdir -p "$dir/site"
  echo '<!doctype html><html lang="en"><body><p>kill-tree fixture</p></body></html>' \
    > "$dir/site/index.html"

  port="$(next_free_port 8880)"

  # Byte for byte the shape `start_server` uses, because the pid `$!` names is part of what is
  # under test: a subshell that `exec`s npx, so `server_pid` is npx itself rather than a shell
  # above it.
  drive_tree_case WRANGLER_TREE "$port" \
    "cd '$dir' && CI=1 WRANGLER_SEND_METRICS=false CLOUDFLARE_API_TOKEN='' exec npx --yes wrangler@${version} pages dev site --ip 127.0.0.1 --port $port" \
    120
}

# ------------------------------------------------------------------------------------------------

echo "=== kill_tree, proved by reading the pids ========================================"
echo ""

proc_parse_case
synthetic_case

if [ "$skip_wrangler" -eq 1 ]; then
  echo ""
  echo "--skip-wrangler: THE TREE THAT ACTUALLY LEAKED WAS NOT DRIVEN."
  echo "The synthetic fixture is three deep; wrangler's is four, and the port is held at the"
  echo "bottom of it. Re-run without the flag before believing this."
else
  wrangler_case
fi

echo ""

if [ "$failures" -ne 0 ]; then
  echo "KILL-TREE: FAIL — $failures of $checks checks failed."
  exit 1
fi

echo "KILL-TREE: PASS — $checks checks green."
