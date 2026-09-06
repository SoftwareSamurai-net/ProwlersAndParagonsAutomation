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
# Quoting a server's log without quoting a sign-in token out of it.
#
# **Every failure arm of `start_server` prints the tail of a wrangler log, and that log is a request
# log.** Stage two drives `/signin?t=<raw token>`, so the line for that navigation carries the
# bearer secret itself — and a failure tail is the one part of this harness that is copied into a
# CI run's public output, an issue, or a chat window. The token is single-use and local to a
# throwaway D1, so this is not a breach; printing a credential into a log because nobody thought
# about it is a habit, and the habit is what is being fixed.
#
# `[?&]t=` and not a bare `t=`, so a word ending in `t=` inside a message is left alone. The value
# is base64url — `A-Za-z0-9_-` — which is what `scripts/e2e/seed.mjs` mints and what
# `worker/crypto.js` mints beside it.
#
# Here rather than in `scripts/e2e.sh` because this is the file that can be sourced, and therefore
# the only part of the harness's shell that can be tested: `scripts/test-kill-tree.sh` drives it.

# redacted_tail <file> [lines]
redacted_tail() {
  tail -"${2:-30}" "$1" | sed 's/\([?&]t=\)[A-Za-z0-9_-][A-Za-z0-9_-]*/\1<redacted>/g'
}

# ------------------------------------------------------------------------------------------------
# The server currently running, if any. `stop_server` reads the first two and is also the EXIT
# trap, so it has to be able to run against a script that never started one. `server_log` is where
# that server's output went, which is what `say_server_state` quotes.

server_pid=''
server_port=0
server_log=''

# ------------------------------------------------------------------------------------------------
# WHETHER THE SERVER OUTLIVED THE DRIVE, AND WHAT IT SAID ON THE WAY OUT.
#
# **The harness could report eight refused connections and not one word about the server.** CI run
# `34040527190` (attempt 1) is the whole argument: `BOOT` passed, `A11Y` then spent 45 seconds
# waiting for `/build` to render, and the remaining seven checks each reported
# `net::ERR_CONNECTION_REFUSED` as if it were their own finding. `wrangler pages dev` had died
# mid-drive — and `scripts/e2e.sh` printed the driver's verdicts, called `stop_server`, and said
# nothing about whether there had still been a server to stop or what its log's last words were.
# Eight red lines, no evidence, and the only honest reading of the run was "something happened".
#
# The asymmetry that made that possible: `start_server`'s two failure arms have printed a
# `redacted_tail` since the day they were written, because a server that never comes up is
# obviously a server question. A server that comes up and then dies is the same question, and
# nothing asked it.
#
# **`capture_server_state` has to run before `stop_server`, and that ordering is the whole of it.**
# Afterwards there is nothing left to ask: the pid is killed and reaped, the port is released, and
# "was it alive when the drive ended" has no answer any more. So it is a separate function from the
# printing, called at the moment the drive returns, whatever the verdicts were.
server_state=''

# capture_server_state — read the running server's fate into `server_state`.
#
# **`kill -0` and not `ps`**: a bash background job that has exited is reaped by bash's own SIGCHLD
# handler, so its pid is gone from the table while bash still remembers the status. That is why the
# exit status comes from `wait` and why `wait` is only reached once `kill -0` has said the process
# is not there — waiting on a live one would block until the server was stopped, which is exactly
# the information this is trying to preserve.
#
# **A status of 127 is bash saying it cannot tell you**, not wrangler's own: `wait` answers 127 for
# a pid that is not a job of this shell. Said in the message rather than smoothed over, because a
# harness that invents an exit status is worse than one that admits it has none.
#
# **The port is asked separately, because the pid is not the server.** `$!` is the `npx` wrapper;
# `workerd` is three levels below it and is what actually holds the socket. "The wrapper is alive
# and nothing is listening" and "the wrapper is gone and something still is" are both states this
# harness has seen, and neither is describable by the pid alone.
capture_server_state() {
  if [ -z "$server_pid" ]; then
    server_state='not running at all (nothing was started, or it had already been stopped)'
    return 0
  fi

  if kill -0 "$server_pid" 2>/dev/null; then
    server_state="still ALIVE (pid $server_pid)"
  else
    local status=0
    wait "$server_pid" 2>/dev/null || status=$?

    if [ "$status" -eq 127 ]; then
      server_state="ALREADY DEAD (pid $server_pid; no exit status — it is not a job of this shell)"
    else
      server_state="ALREADY DEAD (pid $server_pid, exit status $status)"
    fi
  fi

  if port_in_use "$server_port"; then
    server_state="$server_state, and something is still listening on port $server_port"
  else
    server_state="$server_state, and nothing is listening on port $server_port"
  fi

  return 0
}

# say_server_state <what-ended> — the one line run 34040527190 needed, and the log behind it.
#
# **`redacted_tail` and never `tail`**, for the reason its own comment gives: this is a request log
# and stage two drives `/signin?t=<raw token>`, so an unredacted tail pasted into a CI run's public
# output carries the bearer secret with it.
#
# **The tail is printed only when the server is not alive, and that is not brevity for its own
# sake.** Most failures here are a check going red against a perfectly healthy server — a
# deliberately-broken twin's whole purpose is exactly that — and forty lines of `GET /css/app.css
# 200 OK` under every one of them buries the verdict that matters. When the server *is* alive the
# state line says so and names the log, which is the whole of what a reader needs to decide
# whether to open it.
say_server_state() {
  local what="$1"

  echo "::error::the server was ${server_state:-never asked about, which is a bug in this script}"\
" when ${what} ended."

  if [ -z "$server_log" ] || [ ! -s "$server_log" ]; then
    echo "::error::there is no server log to quote (${server_log:-none was recorded})."
    return 0
  fi

  case "$server_state" in
    *ALIVE*)
      echo "::error::its log is $server_log, not quoted here: the server outlived the drive, so"\
" what went wrong is above and not in it."
      ;;
    *)
      echo "::error::the last 40 lines of $server_log, with sign-in tokens redacted:"
      redacted_tail "$server_log" 40
      ;;
  esac

  return 0
}

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

# Where the `/proc` readers below take the process table and the kernel's socket tables. `/proc` in
# every real use.
#
# **It is a variable so that the Linux arms can be driven on a machine that has no `/proc`**, which
# is every developer machine this project is written on. `scripts/test-kill-tree.sh` builds a
# synthetic tree of `stat` files from the real process table and points this at it, so
# `children_of`'s parse — the `(comm) ` trim, the field offset, the ppid comparison — is exercised
# on macOS as well as on the runner; it builds a synthetic `net/tcp` and a synthetic `<pid>/fd/`
# for `port_holders` in the same way. That is a test of the *parse* and not of the kill; only CI
# runs the real Linux arms against real processes, which is why the script is a step in
# `build.yml`.
#
# Same shape and same justification as `WRANGLER_BIN` in `scripts/apply-migrations.sh` and
# `PP_E2E_CHROME` in `scripts/e2e.sh`: a seam a test sets, never something production reads.
proc_root="${PP_PROC_ROOT:-/proc}"

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
  for table in "$proc_root/net/tcp" "$proc_root/net/tcp6"; do
    [ -r "$table" ] || continue
    awk -v want=":$hex" '$4 == "0A" && index($2, want) == length($2) - length(want) + 1 { found = 1 }
         END { exit !found }' "$table" && return 0
  done

  if [ -r "$proc_root/net/tcp" ]; then
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

# The pids listening on a port, from the kernel rather than from a tool that may not be installed.
#
# ------------------------------------------------------------------------------------------------
# WHY THIS EXISTS: `kill_tree` CANNOT REACH A PROCESS THAT IS NOT IN THE TREE.
#
# CI run `33949251306` is the first direct evidence about the Linux leak `PROGRESS.md` item 10
# describes, and it says something the outcome check never could:
#
#     KILL-TREE CHECK WRANGLER_TREE: FAIL — [OUTCOME] every pid in the tree is gone and port 8880
#     is still listening, so something outside the tree of 10448 is holding it.
#
# Every pid the harness enumerated *died*. The port stayed. So the holder was not in the tree, and
# no walk of parent links — however correct — can find it. `kill_tree` below is fixed so that the
# holder stops being created (see its comment); **this is the backstop that names it when one is
# created anyway**, and the two are not alternatives. A leak that survives the structural fix has
# to become a pid and a command line in a log, not a warning about a number.
#
# **`/proc` alone, and that constraint is the whole design.** `ss`, `lsof`, `pgrep` and `python3`
# may all be missing — `port_in_use` already learned that about `ss`, which is not in
# `mcr.microsoft.com/dotnet/sdk:10.0`. `/proc/net/tcp` cannot be missing on Linux. So: find the
# LISTEN socket's inode in the kernel's own table, then find which process has that inode open.
#
#   1. `/proc/net/tcp` and `/proc/net/tcp6`, field 4 == `0A` (TCP_LISTEN) and field 2's port half
#      == the port in hex. **Field 2 and not field 3**: field 3 is the *remote* address, and a
#      connection *to* the listener has the wanted port there — reading the wrong column names the
#      client instead of the server. Field 10 is the socket inode.
#   2. `/proc/<pid>/fd/` — every fd is a symlink, and a socket's target is `socket:[<inode>]`. One
#      `ls -l` per pid rather than a `readlink` per fd, because a busy machine has tens of
#      thousands of fds and this runs on a cleanup path.
#
# macOS has no `/proc` and takes the `lsof` arm, which is the same arm `port_in_use` takes there
# and for the same reason: `lsof` is in the base system and `ss` is not. Windows reads `netstat`
# through `listeners_on`, unchanged. Nothing here kills anything; `stop_server` decides that.
port_holders() {
  local port="$1"

  [ "${port:-0}" -gt 0 ] 2>/dev/null || return 0

  if on_windows; then
    listeners_on "$port"
    return 0
  fi

  if [ -r "$proc_root/net/tcp" ] || [ -r "$proc_root/net/tcp6" ]; then
    local hex inodes table entry listing inode
    hex="$(printf '%04X' "$port")"

    inodes=''
    for table in "$proc_root/net/tcp" "$proc_root/net/tcp6"; do
      [ -r "$table" ] || continue
      inodes="$inodes $(awk -v want=":$hex" \
        '$4 == "0A" && index($2, want) == length($2) - length(want) + 1 { print $10 }' \
        "$table" 2>/dev/null | tr '\n' ' ')"
    done

    # No listening socket at all is a free port, not a holder nobody can name.
    case "$inodes" in *[0-9]*) ;; *) return 0 ;; esac

    for entry in "$proc_root"/[0-9]*; do
      [ -d "$entry/fd" ] || continue
      listing="$(ls -l "$entry/fd" 2>/dev/null)" || continue
      for inode in $inodes; do
        case "$listing" in
          *"socket:[$inode]"*) echo "${entry##*/}"; break ;;
        esac
      done
    done

    return 0
  fi

  if command -v lsof >/dev/null 2>&1; then
    lsof -nP -iTCP:"$port" -sTCP:LISTEN -t 2>/dev/null | sort -u
    return 0
  fi

  return 0
}

# A pid and what it is running, for a failure message that names the holder instead of the port.
#
# **`/proc/<pid>/cmdline` first, `ps` second**, the same ordering and the same reason as everywhere
# else in this file: the kernel's own file cannot be missing on Linux, and `ps` is what answers on
# a Mac. The argument separator is a NUL, so `tr` is what makes it readable.
describe_pid() {
  local pid="$1" cmd=''

  if [ -r "$proc_root/$pid/cmdline" ]; then
    cmd="$(tr '\0' ' ' < "$proc_root/$pid/cmdline" 2>/dev/null)" || cmd=''
  fi

  [ -n "$cmd" ] || cmd="$(ps -o command= -p "$pid" 2>/dev/null)" || cmd=''
  [ -n "$cmd" ] || cmd='<no longer running>'

  printf '%s (%s)' "$pid" "$cmd"
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

  # **It stays a `::warning::` and `stop_server` stays exit-0, deliberately.** `stop_server` is the
  # EXIT trap: a non-zero return from it replaces the script's real exit status, so a cleanup
  # problem would either fail a run whose six checks were green or overwrite the code that says
  # *why* a red one was red. The check that fails the job for this fact is
  # `scripts/test-kill-tree.sh`, which asserts it directly and names the survivors — one loud
  # signal from the check built to be believed beats a second one that can corrupt a verdict.
  #
  # **What did change is what the line says.** It used to name only the port, which is the trap
  # `PROGRESS.md` item 10 is about: a number nobody can act on, printed on every twin of every run
  # for weeks. `port_holders` can name the process on all three platforms now, so it does.
  local holders holder described=''
  holders="$(port_holders "$port" 2>/dev/null || true)"

  for holder in $holders; do
    described="$described $(describe_pid "$holder");"
  done

  if [ -n "$described" ]; then
    echo "::warning::something is still listening on port $port after 30s of asking it not to."\
"Held by:$described See scripts/test-kill-tree.sh, which fails the job for this."
    return 0
  fi

  echo "::warning::something is still listening on port $port after 30s of asking it not to,"\
" and no process on this machine owns the socket — so it is not this run's to kill."
}

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

  # **`read` and not `$(cat …)`, and on this path that is a correctness change rather than a
  # micro-optimisation.** A command substitution forks a subshell and `cat` forks again, so a walk
  # of a runner's ~150 `/proc` entries was ~300 forks — *per level, per call*. `kill_tree` calls
  # this once per process in the tree, so the old walk spent hundreds of milliseconds between
  # killing one child and killing the next. That interval is exactly the window a supervisor uses
  # to respawn the child that was just killed; see `kill_tree`. A builtin redirect forks nothing.
  for entry in "$proc_root"/[0-9]*; do
    pid="${entry##*/}"
    stat=''
    read -r stat < "$entry/stat" 2>/dev/null || stat=''
    [ -n "$stat" ] || continue
    stat="${stat##*) }"

    # After the trim, field 2 is ppid: "<state> <ppid> ...".
    set -- $stat
    [ "${2:-}" = "$parent" ] && echo "$pid"
  done

  return 0
}


# `SIGSTOP` a pid and everything under it, printing the tree parent-first as it goes.
#
# **Freezing is what makes the kill below atomic, and nothing else does.** See `kill_tree`.
#
# The root is stopped *before* its children are enumerated, so it cannot fork one more between the
# two; each child is then stopped before its own children are read, for the same reason. By the
# time this returns, every process in the tree is stopped and the set is closed — nothing in it can
# create anything new, so the list is complete rather than a snapshot that was true once.
freeze_tree() {
  local pid="$1" child

  kill -STOP "$pid" >/dev/null 2>&1 || true
  echo "$pid"

  for child in $(children_of "$pid"); do
    freeze_tree "$child"
  done
}

# Every descendant of a pid and the pid itself, killed as a set that cannot regrow.
#
# ------------------------------------------------------------------------------------------------
# THE FAULT THIS WAS REWRITTEN FOR, WHICH IS A RACE AND NOT A MISSING LEVEL.
#
# This used to walk the tree and `kill -9` depth first, and CI run `33949251306` reported the exact
# symptom that leaves: every pid the test enumerated was dead and the port was still listening, so
# **the holder was a process that did not exist when the tree was enumerated.**
#
# Where it came from, measured on a real `wrangler pages dev` on 2026-09-05 rather than reasoned
# about: kill the `workerd` that holds the port and **miniflare starts another one**, which rebinds
# the same port inside a second. Its own log says so —
#
#     [wrangler:warn] The Workers runtime crashed unexpectedly and is being restarted (crash #1).
#     [wrangler:info] Updated and ready on http://127.0.0.1:8860
#
# — and the mechanism is in the pinned miniflare's source: `Runtime.updateConfig` attaches an exit
# handler to the `workerd` child that calls `onWorkerdCrashRestart`, which reassembles the config
# and spawns a replacement. `SIGKILL` is indistinguishable from a crash. `stop_server`'s Windows
# note has recorded this behaviour for months — "wrangler restarts `workerd`, so the port came back,
# on a new pid, as fast as it could be cleared" — without anyone connecting it to the Linux arm.
#
# **So the depth-first kill was racing the supervisor, and on Linux it lost.** The real tree is
# `npm exec` -> node -> node -> {esbuild, esbuild, workerd, workerd}: the killed `workerd` is not
# the last child, so the old code ran `children_of` for each remaining sibling *after* killing it
# and before killing their parent. On Linux that was a `/proc` scan forking `cat` ~150 times —
# hundreds of milliseconds of window. On macOS it is one `pgrep` fork, and macOS therefore won the
# race and looked fixed. That is the whole of the platform difference, and it is why the macOS
# green run was never evidence about the Linux one.
#
# **The fix removes the window rather than shortening it.** Freeze the whole tree with `SIGSTOP`
# first: a stopped supervisor cannot run the exit handler, so it cannot spawn a replacement, and a
# process that cannot fork cannot add to the set being killed. Only then kill, children before
# parents. `SIGKILL` is delivered to a stopped process — it is the one signal that cannot be
# blocked, queued or ignored — so no `SIGCONT` is needed and none is sent, because continuing a
# supervisor is precisely what must not happen.
#
# **Children before parents is still load-bearing, for the original reason.** Killing a parent
# first orphans its children onto init and an orphan's ppid is 1, so a walk that would have found
# them next returns nothing. `freeze_tree` prints parent-first, so this reverses it.
#
# **Not a process-group kill, and that was checked against wrangler's source rather than assumed.**
# The obvious reading of run `33949251306` is "the holder escaped into a session of its own, so kill
# the group" — and it is wrong twice over.
#
# *It would not help.* In the pinned miniflare, `Runtime.updateConfig` spawns `workerd` with
# `stdio`, `windowsHide` and `env` and **no `detached`** — an ordinary child, in the harness's own
# process group and session. The only `detached: true` in that file is the VS Code inspector
# watchdog, gated behind `process.env.VSCODE_INSPECTOR_OPTIONS`, which CI does not set; wrangler's
# own three are docker builds and a cloudchamber `ssh`, none of them on the `pages dev` path. There
# is no second group to reach for. Neither `setsid` nor `process.setpgid` appears anywhere in
# either package.
#
# *And it would be actively unsafe.* `( ... ) &` in a non-interactive shell is not a group leader,
# so the tree's pgid is the *harness's own* — `kill -- -$pgid` would take out `e2e.sh` itself.
# Turning on job control to give it a group of its own changes how every other background command
# in this script behaves.
#
# **The holder escapes by being born, not by changing session.** Kill `workerd`, the supervisor's
# exit handler runs `onWorkerdCrashRestart`, and the replacement is a pid that did not exist when
# the tree was enumerated; kill the supervisor next and that replacement is reparented onto init,
# where a ppid of 1 puts it outside any walk of parent links. That is the whole mechanism behind
# "every pid in the tree is gone and the port is still listening". The freeze closes the window it
# is born in, and `stop_server`'s socket lookup catches one born anyway.
kill_tree() {
  local pid="$1" frozen p ordered=''

  frozen="$(freeze_tree "$pid")"

  for p in $frozen; do
    ordered="$p${ordered:+ }$ordered"
  done

  for p in $ordered; do
    kill -9 "$p" >/dev/null 2>&1 || true
  done
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

  # **The backstop, and it is a mechanism now rather than only a complaint.** `kill_tree` above is
  # the fix; this is what happens when it is not enough, and CI run `33949251306` is the evidence
  # that "not enough" is a state this reaches: every pid in the tree dead, the port still held.
  #
  # Parent links cannot find a holder that was never in the tree — an orphan reparented onto init
  # has a ppid of 1 — so this asks the other question, "who has this socket open", and kills the
  # answer's tree. Only ever a port `start_server` has just used and only after its own process has
  # been asked to stop, so the holder is this run's or nothing; `next_free_port` skipping occupied
  # ports rather than clearing them is what keeps that true.
  #
  # Windows is unchanged and deliberately so: `release_port` there already reads `netstat` and
  # `taskkill //T`s the holder, which is this, done by the tools that platform has.
  if ! on_windows; then
    local holder
    for holder in $(port_holders "$port"); do
      echo "::warning::port $port outlived the tree kill, held by $(describe_pid "$holder")."\
" Killing it and its tree — see kill_tree's comment for why one can exist."
      kill_tree "$holder"
    done
  fi

  # The last word: if even that missed something, this names it rather than passing quietly.
  release_port "$port"
}
