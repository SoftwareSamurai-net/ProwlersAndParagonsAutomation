#!/usr/bin/env bash
# Finding a free port, and stopping a server and everything it started.
#
# **Sourced, never executed.** `scripts/e2e.sh` sources this; so does `scripts/test-kill-tree.sh`,
# which is the whole reason it is a file of its own. It defines functions and two variables and
# runs nothing, so sourcing it has no effect a caller has to undo.
#
# ------------------------------------------------------------------------------------------------
# WHY THIS IS NOT STILL INSIDE e2e.sh, AND THE ARGUMENT IS NOT TIDINESS.
#
# `kill_tree` fixed a real leak and had never been watched to work. `PROGRESS.md` item 10 records
# both halves of that: `stop_server`'s Linux arm used `pkill -P`, which kills *direct* children
# only — wrangler's tree is four processes deep — so `workerd` outlived its step still holding a
# port; the fix walks `/proc/<pid>/stat` recursively; the identical defect then reappeared on
# macOS, which has no `/proc`, so `children_of` returned nothing there too, silently.
#
# **Both fixes were verified by outcome — "no leaked processes after a run" — and on Linux that
# check passes while the leak continues**, because `next_free_port` walks past the held port and
# never asks for it again. A green run cannot distinguish "nothing leaked" from "something leaked
# and nothing looked". So the fix needed a check that starts a tree, stops it, and asserts
# *directly* that every process in it is dead and the port is free — and `e2e.sh` cannot be
# sourced to get at these functions, because sourcing it publishes a site and starts a browser.
#
# That check is `scripts/test-kill-tree.sh`. It is a step in `build.yml` before the e2e steps, so
# the Linux arm below is exercised by something that reads the pids rather than the outcome.

# ------------------------------------------------------------------------------------------------
# The server currently running, if any. `stop_server` reads both and is also the EXIT trap, so it
# has to be able to run against a script that never started one.

server_pid=''
server_port=0

on_windows() {
  case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) return 0 ;;
    *) return 1 ;;
  esac
}

# The Windows pids listening on a port. Empty on anything else, and on Windows empty means the
# port is free.
listeners_on() {
  local port="$1"
  on_windows || return 0
  # **`tr -d '\r'` is not cosmetic.** `netstat.exe` ends every line with CRLF, so without it each
  # pid comes back as `1234\r` — which `taskkill` refuses as an invalid argument, silently, while
  # `release_port` loops for thirty seconds and then reports that something is still listening.
  # That is exactly what it did, and the warning was the only sign.
  netstat -ano 2>/dev/null \
    | tr -d '\r' \
    | awk -v want=":$port" '$1 == "TCP" && $4 == "LISTENING" && index($2, want) { print $5 }' \
    | sort -u
}

# Whether anything is listening on a port. Portable, and portable is the point.
#
# **`listeners_on` above is Windows-only — `on_windows || return 0` — so on Linux it reported every
# port free, and `next_free_port` therefore never skipped anything.** That was invisible for as
# long as this script ran once per job: ports are handed out by incrementing, so a fresh run
# starting at 8788 never collided with itself. Running it twice in one job broke it immediately —
# the second run started again at 8788, walked up to 8793, and got
# `Address already in use (127.0.0.1:8793)` from workerd, three twins in.
#
# **It reads the listener table; it does not try to connect.** A `/dev/tcp` probe was the first
# attempt and it is wrong in a way worth writing down, because it looked right and passed once: a
# connection *consumes a slot in the server's accept backlog*, so against a server that is
# listening but not accepting, the first probe succeeds and the second is refused. The port then
# reads as free on the very call that matters. Found by testing the probe against a deliberately
# small backlog — `next_free_port` returned the occupied port while `port_in_use` on its own had
# just said the port was busy. A real `wrangler pages dev` accepts, so this would have worked in
# practice and failed the first time something did not; a probe with a side effect is not a probe.
#
# `ss` is on `ubuntu-latest`; `netstat` is the Windows path and answers the same question there.
# TIME_WAIT is deliberately not counted: it is not a listener, `SO_REUSEADDR` lets the next server
# bind over it, and treating one as occupied would skip ports for no reason.
port_in_use() {
  local port="$1"

  if on_windows; then
    [ -n "$(listeners_on "$port")" ] && return 0
    return 1
  fi

  # **`/proc/net/tcp` is the primary, not the fallback, and that ordering was arrived at by
  # checking rather than assuming.** `ss` is the obvious tool and it is *not* on every Linux image
  # — `mcr.microsoft.com/dotnet/sdk:10.0` has no iproute2 at all. Making the harness depend on it
  # would trade a wrong answer for a hard failure on some machine nobody tested. The kernel's own
  # table needs no package, cannot be missing on Linux, and lists listeners without opening a
  # connection to them.
  #
  # **Not opening a connection is the whole point.** The first version of this probe was
  # `/dev/tcp`, which looked right and passed once: connecting consumes a slot in the server's
  # accept backlog, so against a server that listens but is not accepting, the first probe
  # succeeds and the second is refused — and the port reads as free on the call that matters.
  # Found by testing the probe against a deliberately small backlog, where `port_in_use` said
  # "busy" and `next_free_port` immediately handed that same port back.
  #
  # State `0A` is TCP_LISTEN. TIME_WAIT is `06` and is deliberately not counted: it is not a
  # listener, `SO_REUSEADDR` lets the next server bind over it, and treating one as occupied would
  # skip ports for no reason.
  local hex
  hex="$(printf '%04X' "$port")"

  local table
  for table in /proc/net/tcp /proc/net/tcp6; do
    [ -r "$table" ] || continue
    awk -v want=":$hex" '$4 == "0A" && index($2, want) == length($2) - length(want) + 1 { found = 1 }
         END { exit !found }' "$table" && return 0
  done

  if [ -r /proc/net/tcp ]; then
    return 1
  fi

  # Not Linux and not Windows — a Mac, most likely. `ss` if it is there, and otherwise refuse
  # rather than guess: guessing "free" is precisely what produced `Address already in use` from
  # workerd three twins into a run, and a fallback that can be silently wrong is worse than none.
  #
  # **`lsof` before `ss`, because on the Mac this branch names, `ss` is the one that is absent.**
  # iproute2 is Linux's; macOS ships `lsof` in the base system and no `ss` at all, so this arm
  # refused on the very platform its own comment says it is for — the run died at the first port
  # check with "no /proc/net/tcp and no 'ss' on PATH". CI is Linux and takes the branch above, so
  # nothing there could ever have seen it.
  #
  # `-sTCP:LISTEN` is what keeps this equivalent to the `0A` test above rather than merely close
  # to it: it matches listeners only, so a socket in TIME_WAIT is not counted as occupying the
  # port — the same distinction the Linux arm makes deliberately, for the same reason.
  if command -v lsof >/dev/null 2>&1; then
    lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1 && return 0
    return 1
  fi

  if command -v ss >/dev/null 2>&1; then
    ss -ltn 2>/dev/null | awk -v want=":$port$" '$4 ~ want { found = 1 } END { exit !found }'
    return $?
  fi

  echo "::error::cannot tell whether port $port is free: no /proc/net/tcp and no 'ss' on PATH." >&2
  echo "::error::Refusing rather than guessing it is free — see this function's comment." >&2
  exit 2
}

# The first port at or after `$1` that nothing is listening on.
#
# **Occupied ports are skipped, never cleared.** An earlier version killed whatever held the port
# it wanted, which is a fine way to end somebody's unrelated dev server on the same number.
next_free_port() {
  local port="$1"

  while port_in_use "$port"; do
    port=$((port + 1))
  done

  echo "$port"
}

# **Kill whatever is still listening on a port this script started a server on, and wait until
# nothing is.** Each server gets a port of its own, so this is not needed to let the next one
# bind — it is needed so a run does not leave `workerd` processes behind on a developer's machine.
#
# Only ever called with a port `start_server` has just used, and only after the wrapper process
# has been asked to stop: the pid it reads out of `netstat` is a real Windows pid, which is the
# whole reason this is the cleanup rather than a pid translation — see `stop_server`.
#
# **Its warning is the only signal Linux ever had, and it is a signal about the port rather than
# about the tree — which is the trap `PROGRESS.md` item 10 is about.** It fired on every twin of
# every CI run for weeks while the run stayed green, because `next_free_port` steps over a held
# port. Do not read a run with no warning as proof that `kill_tree` worked; that is the outcome
# check that hid this. `scripts/test-kill-tree.sh` is what proves it.
release_port() {
  local port="$1"
  local deadline=$((SECONDS + 30))

  while [ "$SECONDS" -lt "$deadline" ]; do
    port_in_use "$port" || return 0

    # Only Windows can name the holder; `kill_tree` in `stop_server` is what does the work
    # everywhere else, and this is the backstop that says so if it did not.
    if on_windows; then
      local pid
      for pid in $(listeners_on "$port"); do
        taskkill //F //T //PID "$pid" >/dev/null 2>&1 || true
      done
    fi

    sleep 1
  done

  echo "::warning::something is still listening on port $port after 30s of asking it not to."
}

# Where `children_of` reads the process table. `/proc` in every real use.
#
# **It is a variable so that the Linux arm can be driven on a machine that has no `/proc`**, which
# is every developer machine this project is written on. `scripts/test-kill-tree.sh` builds a
# synthetic tree of `stat` files from the real process table and points this at it, so the parse
# — the `(comm) ` trim, the field offset, the ppid comparison — is exercised on macOS as well as
# on the runner. That is a test of the *parse* and not of the kill; only CI runs the real Linux
# arm against real processes, which is why the script is a step in `build.yml`.
#
# Same shape and same justification as `WRANGLER_BIN` in `scripts/apply-migrations.sh` and
# `PP_E2E_CHROME` in `scripts/e2e.sh`: a seam a test sets, never something production reads.
proc_root="${PP_PROC_ROOT:-/proc}"

# The direct children of a pid, from the kernel rather than from a tool.
#
# **`pgrep -P` is the obvious way and it is not guaranteed to be there** — the same lesson
# `port_in_use` just learned about `ss`, which is missing from `mcr.microsoft.com/dotnet/sdk:10.0`.
# `/proc/<pid>/stat` cannot be missing on Linux. Its fourth field is the parent pid, and the second
# is the command name in parentheses, which can itself contain spaces — so the parse starts after
# the last `)` rather than counting fields from the left, which is the bug every naive
# `/proc/*/stat` reader has.
#
# **`return 0` at the end, and it is a fix rather than a flourish.** The loop's last statement is
# `[ "${2:-}" = "$parent" ] && echo "$pid"`, so the function returned the status of the *final*
# `/proc` entry it looked at — which is almost never a child, so `children_of` almost always
# returned 1. That is
# invisible in `for child in $(children_of …)`, where `set -e` exempts a command substitution in a
# word list, and fatal in `children="$(children_of …)"`, where it is an assignment and `set -e`
# kills the script before the next line runs. Measured: a three-line probe doing exactly that
# exited 1 having printed nothing. Nothing in `e2e.sh` used the second form, so the landmine had
# never gone off; `scripts/test-kill-tree.sh` uses it, which is how it was found.
children_of() {
  local parent="$1" entry pid stat

  # **`/proc` is Linux's and macOS has none, so this returned nothing there — and returning
  # nothing is silent.** `kill_tree` then killed only the pid it was handed, which is the `npx`
  # wrapper; wrangler's own node and the `workerd` under it survived and kept the port. That is
  # the identical defect the Linux arm below this function was written to fix (`pkill -P` killing
  # direct children only), reappearing on a platform the fix could not reach. Measured: six
  # `wrangler`/`workerd` groups still listening on 8788-8793 after a completed run, one per
  # server, with `release_port` warning about every one of them and the run still reporting PASS.
  #
  # `pgrep -P` gives direct children and is in the macOS base system; `kill_tree` already
  # recurses, so one level is all this has to answer — the same shape the `/proc` walk provides.
  if [ ! -d "$proc_root" ]; then
    pgrep -P "$parent" 2>/dev/null
    return 0
  fi

  for entry in "$proc_root"/[0-9]*; do
    pid="${entry##*/}"
    stat="$(cat "$entry/stat" 2>/dev/null)" || continue
    stat="${stat##*) }"

    # After the trim, field 2 is ppid: "<state> <ppid> ...".
    set -- $stat
    [ "${2:-}" = "$parent" ] && echo "$pid"
  done

  return 0
}


# Every descendant of a pid, depth first, then the pid itself.
#
# **One level at a time rather than a process group**: `( ... ) &` in a non-interactive shell is
# not a group leader, so `kill -- -$pid` names nothing, and turning on job control to make it one
# changes how every other background command in this script behaves.
#
# **Depth first is load-bearing and not a style choice.** Killing the parent first orphans its
# children onto init, and an orphan's ppid is 1 — so the walk that would have found them next
# returns nothing and they survive holding whatever they held. Measured on a real
# `wrangler pages dev` tree, which is four processes deep: `npx` -> node -> node -> `workerd`,
# with `workerd` holding the port. Two levels of recursion is not enough for it.
kill_tree() {
  local pid="$1" child

  for child in $(children_of "$pid"); do
    kill_tree "$child"
  done

  kill -9 "$pid" >/dev/null 2>&1 || true
}

# Stop the server `server_pid`/`server_port` name, and everything it started.
stop_server() {
  local pid="$server_pid"
  local port="$server_port"
  server_pid=''

  [ -n "$pid" ] || return 0

  # **MSYS pids and Windows pids are two namespaces, and getting the translation wrong cost three
  # debugging rounds. All three are written down because each looked like a different bug.**
  #
  # `$!` is an MSYS pid; `taskkill` speaks Windows pids. So:
  #
  #   1. **`taskkill //PID <msys pid>`** names a process Windows has never heard of. It failed
  #      silently, nothing died, and the `wait` below never returned — the script sat there for
  #      ever after printing five green verdicts.
  #   2. **`ps -W` to translate** was worse. `-W` adds Windows-only processes, listed *with their
  #      Windows pid in the first column*, so matching `$1 == <msys pid>` can hit a completely
  #      unrelated process — and the tree kill then takes out whatever that was. It took out the
  #      driver, which printed no verdict at all and read exactly like a harness unable to see its
  #      own defect.
  #   3. **Killing only the port's listener** leaves the supervisor alive, and wrangler restarts
  #      `workerd` — so the port came back, on a new pid, as fast as it could be cleared. Thirty
  #      seconds of that is what `release_port`'s warning was reporting.
  #
  # `ps` *without* `-W` lists MSYS processes only, so the first column is unambiguously an MSYS pid
  # and the fourth is its Windows pid. `//T` then takes the whole Windows tree — `npx` here is a
  # shell script that runs node, which runs `cmd`, which runs node, which runs `workerd` — which
  # is the supervisor and its worker together, so nothing is left to restart anything.
  if on_windows; then
    local winpid
    winpid="$(ps 2>/dev/null | tr -d '\r' | awk -v p="$pid" '$1 == p { print $4 }' | head -1)"
    [ -n "${winpid:-}" ] && taskkill //F //T //PID "$winpid" >/dev/null 2>&1 || true
  else
    # **The whole tree, not the children, and that distinction leaked a server on every Linux
    # run.** This was `pkill -P "$pid"`, which kills *direct* children only — and wrangler's tree
    # is `npx` -> node -> node -> `workerd`, so `workerd` survived, kept the port, and outlived
    # the step. Nobody noticed while the script ran once per job, because the leaked port was
    # never asked for again; the second run in one job walked straight into it. `release_port` did
    # not cover it either: it reads pids out of `netstat`, which is Windows-only, so on Linux it
    # returned immediately having done nothing and warned about nothing.
    kill_tree "$pid"
  fi

  kill "$pid" >/dev/null 2>&1 || true
  wait "$pid" 2>/dev/null || true

  # The backstop, not the mechanism. If the tree kill above missed something, this names it.
  release_port "$port"
}
