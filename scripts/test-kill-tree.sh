#!/usr/bin/env bash
# Proves `stop_server` kills the whole tree and frees the port. Directly, by reading the pids.
#
#   ./scripts/test-kill-tree.sh                  # every case, including a real wrangler
#   ./scripts/test-kill-tree.sh --skip-wrangler  # the synthetic ones only. NOT a full run
#
# It also covers the other thing `scripts/e2e/process.sh` owns — `redacted_tail`, which keeps a
# raw sign-in token out of the log tail a failing `start_server` prints — for the reason that file
# is separate at all: it is the only part of this harness's shell that can be sourced and tested.
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
# WHAT IT THEN FOUND, WHICH IS WHY THERE ARE SEVEN CASES AND NOT FOUR.
#
# The first CI run with this step in it — `33949251306`, `ubuntu-latest` — went red exactly where
# it was pointed:
#
#     KILL-TREE CHECK WRANGLER_TREE: FAIL — [OUTCOME] every pid in the tree is gone and port 8880
#     is still listening, so something outside the tree of 10448 is holding it.
#
# Every enumerated pid died, so the holder was **a process that did not exist when the tree was
# enumerated**. Measured afterwards on a real `wrangler pages dev`: kill the `workerd` holding the
# port and miniflare logs "The Workers runtime crashed unexpectedly and is being restarted" and
# spawns another that rebinds the port. The old depth-first kill was racing that supervisor, and
# on Linux — where `children_of` forked `cat` per `/proc` entry between one kill and the next — it
# lost. See `kill_tree`'s comment in `scripts/e2e/process.sh` for the whole of it.
#
# Three cases exist because of that finding, and each is breakable here rather than only on the
# runner:
#
#   - `RESPAWNING_TREE` reproduces the supervisor hermetically. Remove `freeze_tree`'s `kill -STOP`
#     and it goes red.
#   - `ORPHANED_LISTENER` reproduces the *verdict* — a port held by a pid in nobody's tree —
#     deterministically, and is the end-to-end test of the `port_holders` backstop.
#   - `PROC_NET_PARSE` drives that backstop's two `/proc` parses against a synthetic `/proc`, the
#     way `PROC_PARSE` already drives `children_of`'s.
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
# reports seven verdicts, so it is its own step in `build.yml` and prints its own summary line.

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
      echo "  --skip-wrangler   run only the synthetic trees. NOT a full run: the tree that"
      echo "                    actually leaked is wrangler's, and no fixture reproduces its"
      echo "                    depth and its restart-on-crash supervisor together."
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

# How many processes on this machine are running a given command. Used only by the marker check in
# `drive_tree_case`, and `ps` again rather than anything under test.
#
# **The pattern goes through the environment, and that is not style.** The first version was
# `ps -eo command= | grep -c -F -- "$1"`, and `grep`'s own command line contains the pattern — so it
# counted itself, reported one survivor of a tree that had been killed perfectly, and blamed the
# fix. `awk` reading `ENVIRON` keeps the string out of every argv on the machine.
count_matching() {
  ps -eo command= 2>/dev/null \
    | PP_MARK="$1" awk 'index($0, ENVIRON["PP_MARK"]) { n++ } END { print n + 0 }'
}

# ------------------------------------------------------------------------------------------------
# Case 1 — the `/proc` parse, driven on this machine whatever this machine is.
#
# **The Linux arm cannot be reached on a Mac and the Mac is where this is written**, so
# `children_of` takes its process table from `$proc_root` and this builds a synthetic one out of
# the real `ps` output. It proves the *parse* — the `(comm) ` trim, the field offset, the ppid
# comparison — and nothing about killing anything; cases 3 to 6 do that against real processes,
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
# Case 2 — the OTHER `/proc` parse, and the one CI run 33949251306 made necessary.
#
# `port_holders` answers "which process has this port's listening socket open" out of the kernel's
# own tables, because `kill_tree` cannot reach a process that is not in the tree and that run
# proved one can exist. Two parses have to be right and neither can be driven on a Mac, so this
# builds both: a synthetic `net/tcp`/`net/tcp6` and a synthetic `<pid>/fd/` of real symlinks whose
# targets are `socket:[<inode>]`, exactly as the kernel writes them.
#
# **Every row that is not the answer is a decoy for a specific wrong reading**, because "it found
# the right pid" is satisfied by a parser that found every pid:
#
#   - the same port in **state 01** rather than `0A` — an established connection, not a listener;
#   - a listener on the **next port up**, whose hex differs by one digit;
#   - a listener whose **remote** address carries the wanted port, which is what a client of our
#     own server looks like: reading field 3 instead of field 2 names the client;
#   - a process holding a **pipe** and a plain file rather than a socket.
#
# The IPv6 row is a second *answer* rather than a decoy, held by a second pid, so that a reader of
# `net/tcp` alone comes back one pid short instead of coming back right.
proc_net_parse_case() {
  local fake want other holders
  fake="$(mktemp -d)"
  # shellcheck disable=SC2064 # $fake is wanted expanded now, not at trap time.
  trap "rm -rf '$fake'" RETURN

  want=8880           # 22B0
  other=8881          # 22B1

  mkdir -p "$fake/net"
  {
    echo "  sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode"
    printf '   0: 0100007F:%04X 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1001 0 4242001 1 0 100 0 0 10 0\n' "$want"
    printf '   1: 0100007F:%04X 0100007F:C001 01 00000000:00000000 00:00000000 00000000  1001 0 4242002 1 0 100 0 0 10 0\n' "$want"
    printf '   2: 0100007F:%04X 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1001 0 4242003 1 0 100 0 0 10 0\n' "$other"
    printf '   3: 0100007F:1F90 0100007F:%04X 0A 00000000:00000000 00:00000000 00000000  1001 0 4242004 1 0 100 0 0 10 0\n' "$want"
  } > "$fake/net/tcp"
  {
    echo "  sl  local_address                         remote_address                        st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode"
    printf '   0: 00000000000000000000000001000000:%04X 00000000000000000000000000000000:0000 0A 00000000:00000000 00:00000000 00000000  1001 0 4242005 1 0 100 0 0 10 0\n' "$want"
  } > "$fake/net/tcp6"

  mkdir -p "$fake/900101/fd" "$fake/900102/fd" "$fake/900103/fd" "$fake/900104/fd" \
    "$fake/900105/fd" "$fake/900106/fd"
  ln -s 'socket:[4242001]' "$fake/900101/fd/3"      # the IPv4 listener  — an answer
  ln -s 'socket:[4242005]' "$fake/900102/fd/7"      # the IPv6 listener  — an answer
  ln -s 'socket:[4242003]' "$fake/900103/fd/3"      # listening on $other
  ln -s 'socket:[4242002]' "$fake/900104/fd/3"      # connected, not listening
  ln -s '/dev/null'        "$fake/900105/fd/0"
  ln -s 'pipe:[4242001]'   "$fake/900105/fd/1"      # same number, not a socket
  # **Every decoy socket has an owner on purpose**, this one most of all: it is the client whose
  # *remote* address is the wanted port. Leave it unowned and reading field 3 instead of field 2
  # answers "nobody" — which is red, but red for the wrong reason and indistinguishable from a
  # parser that read no table at all. Owned, the wrong reading names 900106 and says so.
  ln -s 'socket:[4242004]' "$fake/900106/fd/5"

  local saved="$proc_root"
  proc_root="$fake"

  # ---- positive control: the synthetic table is being read at all ------------------------------
  # Without this, every assertion below is satisfied by a parser that read nothing: "the answer is
  # 900101 900102" would fail loudly, but "the decoys are absent" would pass perfectly.
  if ! port_in_use "$want"; then
    proc_root="$saved"
    fail PROC_NET_PARSE "[CONTROL] port_in_use could not see the synthetic listener on $want, so"\
" the fixture's net/tcp was never read and nothing below tested a parse."
    return
  fi

  holders="$(port_holders "$want" | tr '\n' ' ')"
  local other_holders; other_holders="$(port_holders "$other" | tr '\n' ' ')"
  local quiet; quiet="$(port_holders 8882 | tr '\n' ' ')"

  proc_root="$saved"

  if [ "$holders" != "900101 900102 " ]; then
    fail PROC_NET_PARSE "[OUTCOME] the /proc socket lookup answered '$holders' for port $want and"\
" '900101 900102 ' is the whole of the answer. 900101 holds the IPv4 listener and 900102 the IPv6"\
" one; 900103 listens on $other, 900104 is connected rather than listening, and 900105 holds a"\
" pipe with the same number. A missing pid means one table is not read; an extra one means the"\
" state column, the hex port, the local/remote column or 'socket:' is not being tested."
    return
  fi

  if [ "$other_holders" != "900103 " ]; then
    fail PROC_NET_PARSE "[OUTCOME] the lookup answered '$other_holders' for port $other, and"\
" 900103 is the only listener on it — so the port is not really discriminating and the answer"\
" above was right by luck."
    return
  fi

  if [ -n "$quiet" ]; then
    fail PROC_NET_PARSE "[OUTCOME] the lookup answered '$quiet' for port 8882, which has no row in"\
" either table at all."
    return
  fi

  pass PROC_NET_PARSE "the /proc socket lookup named both listeners on $want across net/tcp and"\
" net/tcp6, and none of the five decoys."
}

# ------------------------------------------------------------------------------------------------
# The shared body of cases 3, 4 and 5: start a tree, prove it is alive and holding the port, stop it
# with the real `stop_server`, and probe every pid.
#
# `$1` is the check name, `$2` the port, and `$3` a command run as `bash -c` inside the same
# `( … ) &` shape `start_server` uses — so `$!`, and therefore `server_pid`, is the same kind of
# pid the harness actually hands `stop_server`.
drive_tree_case() {
  local name="$1" port="$2" launch="$3" ready_seconds="$4" marker="${5:-}"
  local log tree n_alive survivors root_pid escapees n_marked

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

  # A fixture that never spawned a marker satisfies "no marker survived" perfectly, which is three
  # of this repository's four historical guard faults. So count them before anything is killed.
  n_marked=0
  if [ -n "$marker" ]; then
    n_marked="$(count_matching "$marker")"
    if [ "$n_marked" -lt 5 ]; then
      stop_server
      fail "$name" "[CONTROL] only $n_marked processes are running '$marker', so the supervisor"\
" never had a set of siblings to replace and 'nothing was spawned during the kill' would hold"\
" against a fixture that spawns nothing at all."
      rm -f "$log"
      return
    fi
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

  # **The assertion the port cannot make.** `stop_server`'s port-holder backstop will find and
  # kill a respawned *listener*, so "the port is free" is true whether or not the tree kill was
  # complete — it passed with `freeze_tree`'s `kill -STOP` removed, which is how this check came to
  # exist. A process spawned during the kill that holds nothing is invisible to every other
  # assertion here and is exactly what leaked.
  if [ -n "$marker" ]; then
    escapees="$(count_matching "$marker")"
    if [ "$escapees" -ne 0 ]; then
      pkill -f "$marker" 2>/dev/null || true
      fail "$name" "[OUTCOME] $escapees process(es) running '$marker' outlived stop_server, out of"\
" $n_marked before it. Every one of them was spawned by a supervisor that was still running while"\
" its own tree was being killed, so it was born after the tree was enumerated and nothing killed"\
" it. That is the leak CI run 33949251306 reported, and the freeze in kill_tree is what stops it."
      rm -f "$log"
      return
    fi
  fi

  if port_in_use "$port"; then
    # **Name the holder, because the last time this fired it did not.** CI run 33949251306 said
    # "something outside the tree is holding it" and stopped there, and a whole session went into
    # working out what that something was. `port_holders` can answer it on all three platforms
    # now, so the next run's log carries the pid, its command line, and the tree that was killed —
    # which is the difference between a finding and another investigation.
    local holder held=''
    for holder in $(port_holders "$port"); do
      held="$held $(describe_pid "$holder");"
      kill -9 "$holder" 2>/dev/null || true
    done
    [ -n "$held" ] || held=' nothing on this machine has the socket open.'

    echo "::error::$name: port $port is still listening after stop_server. Held by:$held"
    echo "::error::$name: the tree that was killed was: $(echo "$before" | tr '\n' ' ')"
    fail "$name" "[OUTCOME] every pid in the tree is gone and port $port is still listening, so"\
" something outside the tree of $root_pid is holding it. Held by:$held Tree killed:"\
" $(echo "$before" | tr '\n' ' ')Server log: $(redacted_tail "$log" 8 | tr '\n' ' ')"
    rm -f "$log"
    return
  fi

  if [ -n "$marker" ]; then
    pass "$name" "$before_count processes held port $port and $n_marked of them were siblings a"\
" supervisor replaces on sight; after stop_server every one is gone, the port is free, and nothing"\
" was spawned while the tree was being killed."
  else
    pass "$name" "$before_count processes held port $port; after stop_server every one of them is"\
" gone and the port is free."
  fi
  rm -f "$log"
}

# ------------------------------------------------------------------------------------------------
# Case 3 — a synthetic three-deep tree. Fast, hermetic, no npm, and the one to mutate against.
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

# The listener every synthetic fixture below binds with: the deepest process, and the only one
# that binds. `$1` is the directory to write it into.
write_listener() {
  cat > "$1/listener.mjs" <<'EOF'
import { createServer } from 'node:net';
const server = createServer(() => {});
server.on('error', () => setTimeout(() => server.listen(Number(process.argv[2]), '127.0.0.1'), 10));
server.listen(Number(process.argv[2]), '127.0.0.1');
setInterval(() => {}, 1 << 30);
EOF
}

# ------------------------------------------------------------------------------------------------
# Case 4 — a supervisor that restarts what it loses, which is what wrangler is.
#
# **This is the CI failure, reproduced hermetically.** Measured on a real `wrangler pages dev`:
# kill the `workerd` holding the port and miniflare logs "The Workers runtime crashed unexpectedly
# and is being restarted" and spawns another, which rebinds the port. `SIGKILL` is
# indistinguishable from a crash, so the old depth-first `kill_tree` was racing a supervisor it had
# not yet killed — and on Linux, where `children_of` was a `/proc` scan forking `cat` per entry, it
# lost. The replacement was born after the tree had been enumerated, so nothing killed it and
# nothing listed it as a survivor: exactly the verdict run 33949251306 printed.
#
# ------------------------------------------------------------------------------------------------
# WHAT THIS ASSERTS THAT THE PORT CANNOT, AND WHY THAT TOOK A SECOND ATTEMPT.
#
# The first version of this case asserted only what every other case asserts — the tree is dead and
# the port is free — and **it passed with the fix removed**, which is the whole reason this
# paragraph exists rather than a green tick. `stop_server`'s port-holder backstop found the
# respawned listener by its socket and killed it, so the outcome was identical whether or not the
# tree kill had been complete. A check that cannot tell the fix from the backstop is not a check on
# the fix.
#
# So it asserts the property the freeze actually buys: **nothing in the tree spawned anything while
# the tree was being killed.** Every sibling here is a marker script that the supervisor replaces
# when it dies — miniflare's behaviour, applied to a process cheap enough to have twenty-five of.
# A depth-first kill starts killing siblings one at a time, the supervisor answers each death with
# a new marker, and the ones born after the enumeration are orphaned onto init holding nothing.
# They are invisible to "the port is free" and they are exactly what leaked.
#
# **Twenty-five siblings, and the count is the window rather than padding.** wrangler's supervisor
# has four children and the one holding the port is not the last of them, so the old code ran
# `children_of` for every remaining sibling *after* killing it and before killing their parent. A
# fixture whose supervisor has one child gives that code no window at all and passes against it.
#
# The positive control is that the markers were really there before the stop: "no marker survived"
# is satisfied perfectly by a fixture that never spawned one.
#
# So: remove the `kill -STOP` from `freeze_tree` and this must go red. That is the mutation this
# case exists to be broken by.
respawning_case() {
  local dir port
  dir="$(mktemp -d)"
  # shellcheck disable=SC2064
  trap "rm -rf '$dir'" RETURN

  write_listener "$dir"

  # The sibling. A script rather than a bare `sleep`, because its *path* is the marker: `ps` shows
  # `/bin/sh <this temp dir>/marker.sh`, and the temp dir is unique to this run. `sleep` on its own
  # is unidentifiable and every shell tail-execs a one-line script, which would lose the path.
  cat > "$dir/marker.sh" <<'EOF'
#!/bin/sh
while :; do sleep 30; done
EOF
  chmod +x "$dir/marker.sh"

  cat > "$dir/supervisor.mjs" <<'EOF'
import { spawn } from 'node:child_process';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const here = dirname(fileURLToPath(import.meta.url));
const port = process.argv[2];

// What miniflare does when workerd dies: assume it crashed, and start another.
const listener = () => {
  const child = spawn(process.execPath, [join(here, 'listener.mjs'), port], { stdio: 'inherit' });
  child.on('exit', () => listener());
};
listener();

// The same reflex on a cheap process, twenty-five times over: the siblings a depth-first kill has
// to walk, each of which is replaced the moment it dies.
const sibling = () => {
  const child = spawn(join(here, 'marker.sh'), [], { stdio: 'ignore' });
  child.on('exit', () => sibling());
};
for (let i = 0; i < 25; i++) sibling();

setInterval(() => {}, 1 << 30);
EOF

  port="$(next_free_port 8894)"

  drive_tree_case RESPAWNING_TREE "$port" "node '$dir/supervisor.mjs' $port & wait" 20 \
    "$dir/marker.sh"
}

# ------------------------------------------------------------------------------------------------
# Case 5 — the port held by a pid that is in nobody's tree, which is the verdict CI printed.
#
# **`kill_tree` is a walk of parent links, and an orphan has no link to walk.** Once a process is
# reparented onto init its ppid is 1, so no walk from `server_pid` can reach it however deep the
# recursion goes. Case 4 is where such a process comes from; this is the check that the harness
# clears one up regardless of where it came from, and it is deterministic rather than a race: the
# wrapper exits on purpose, immediately, leaving the listener behind.
#
# It is therefore the direct test of `port_holders` against real sockets and real pids — the
# `/proc` arm on the runner, the `lsof` arm on a Mac — where `PROC_NET_PARSE` only tests the parse.
#
# **The positive control is that the holder really is outside the tree.** If it were a descendant
# of `server_pid`, `kill_tree` would clear it and this case would pass while proving nothing about
# the backstop it exists for — which is this repository's most-repeated guard fault, a check that
# holds for the wrong reason.
orphan_case() {
  local dir port log holder tree
  dir="$(mktemp -d)"
  # shellcheck disable=SC2064
  trap "rm -rf '$dir'" RETURN

  write_listener "$dir"
  log="$dir/orphan.log"
  port="$(next_free_port 8898)"

  # The wrapper backgrounds the listener and exits at once, so the listener is reparented onto
  # init before anything asks who its parent is.
  (
    exec bash -c "node '$dir/listener.mjs' $port >'$log' 2>&1 & exit 0"
  ) &
  server_pid=$!
  server_port="$port"
  local root_pid="$server_pid"

  local deadline=$((SECONDS + 20))
  while [ "$SECONDS" -lt "$deadline" ]; do
    port_in_use "$port" && break
    sleep 1
  done

  if ! port_in_use "$port"; then
    stop_server
    fail ORPHANED_LISTENER "[CONTROL] nothing ever listened on port $port, so 'the port is free"\
" afterwards' would have held against a fixture that never bound it. Fixture output:"\
" $(tail -5 "$log" 2>/dev/null)"
    return
  fi

  holder="$(port_holders "$port" | head -1)"
  if [ -z "$holder" ]; then
    stop_server
    fail ORPHANED_LISTENER "[CONTROL] port $port is listening and port_holders named nobody, so"\
" the backstop under test has nothing to act on and the outcome below would be meaningless."
    return
  fi

  tree=" $(descendants_of "$root_pid" | tr '\n' ' ')$root_pid "
  case "$tree" in
    *" $holder "*)
      stop_server
      fail ORPHANED_LISTENER "[CONTROL] the listener ($holder) is still inside the tree of"\
" $root_pid ($tree), so kill_tree would clear it and this case would prove nothing about the"\
" port-holder backstop it exists for."
      return
      ;;
  esac

  stop_server

  if live "$holder"; then
    kill -9 "$holder" 2>/dev/null || true
    fail ORPHANED_LISTENER "[OUTCOME] $(describe_pid "$holder") was holding port $port from"\
" outside the tree of $root_pid and outlived stop_server. The port-holder backstop did not run,"\
" or did not find it."
    return
  fi

  if port_in_use "$port"; then
    fail ORPHANED_LISTENER "[OUTCOME] the orphan is dead and port $port is still listening."
    return
  fi

  pass ORPHANED_LISTENER "a listener orphaned onto init held port $port from outside the tree of"\
" $root_pid, and stop_server found it by its socket and killed it."
}

# ------------------------------------------------------------------------------------------------
# Case 6 — the tree that actually leaked: a real `wrangler pages dev`.
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
# Case 7 — the other thing `scripts/e2e/process.sh` owns: quoting a log without quoting a token.
#
# **Every failure arm of `start_server` prints the tail of a wrangler log, and that log is a request
# log.** Stage two drives `/signin?t=<raw sign-in token>`, so the line for that navigation carries
# the bearer secret — and a failure tail is the one part of this harness that gets pasted into a CI
# log, an issue or a chat window. `redacted_tail` is what those arms call now.
#
# **The positive control comes first and is most of the value**, for the reason this file's header
# gives twice over: "no token in the output" is satisfied perfectly by a redactor that printed
# nothing at all, and by a fixture whose lines never contained one. So this asserts that the
# ordinary line came through untouched and that the fixture really did carry a token, before it
# asserts that the token is gone.
redaction_case() {
  local dir log out

  dir="$(mktemp -d)"
  # shellcheck disable=SC2064
  trap "rm -rf '$dir'" RETURN

  log="$dir/server.log"
  {
    echo "[wrangler:info] Ready on http://127.0.0.1:8788"
    echo "[wrangler:info] GET /signin?t=zAe9_-QbT7xyKLmn 302 Found (4ms)"
    echo "[wrangler:info] GET /rules?chapter=4&t=SECOND_tok-99 200 OK (2ms)"
    echo "[wrangler:info] an ordinary line with no secret in it"
  } > "$log"

  out="$(redacted_tail "$log" 10)"

  if [ "$(printf '%s\n' "$out" | wc -l | tr -d ' ')" -ne 4 ]; then
    fail REDACTED_TAIL "[CONTROL] redacted_tail returned $(printf '%s\n' "$out" | wc -l | tr -d ' ')"\
" of 4 lines, so whatever the assertions below found, they were not reading this log."
    return
  fi

  case "$out" in
    *"an ordinary line with no secret in it"*) ;;
    *)
      fail REDACTED_TAIL "[CONTROL] the line with no token in it did not survive redaction, so a"\
" tail that says nothing would pass the outcome below."
      return
      ;;
  esac

  case "$out" in
    *"zAe9_-QbT7xyKLmn"*|*"SECOND_tok-99"*)
      fail REDACTED_TAIL "[OUTCOME] a raw sign-in token survived into the tail a failing"\
" start_server prints: $out"
      return
      ;;
  esac

  case "$out" in
    *"/signin?t=<redacted> 302"*)
      pass REDACTED_TAIL "both t= values are gone and the rest of the log is untouched"
      ;;
    *)
      fail REDACTED_TAIL "[OUTCOME] the token is gone but so is the address it was on, so a"\
" failing start_server no longer says what it was serving: $out"
      ;;
  esac
}

# ------------------------------------------------------------------------------------------------

echo "=== kill_tree, proved by reading the pids ========================================"
echo ""

proc_parse_case
proc_net_parse_case
synthetic_case
respawning_case
orphan_case
redaction_case

if [ "$skip_wrangler" -eq 1 ]; then
  echo ""
  echo "--skip-wrangler: THE TREE THAT ACTUALLY LEAKED WAS NOT DRIVEN."
  echo "The synthetic fixtures reproduce its shape and its restart-on-crash supervisor, but"
  echo "wrangler's real tree is four deep with the port at the bottom of it, and it is the one"
  echo "that leaked. Re-run without the flag before believing this."
else
  wrangler_case
fi

echo ""

if [ "$failures" -ne 0 ]; then
  echo "KILL-TREE: FAIL — $failures of $checks checks failed."
  exit 1
fi

echo "KILL-TREE: PASS — $checks checks green."
