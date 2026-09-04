# Killing a process and everything under it, on Linux. Sourced, never run.
#
# ------------------------------------------------------------------------------------------------
# WHY THIS IS A FILE OF ITS OWN.
#
# It was fifteen lines inside `scripts/e2e.sh`, and it fixed a real leak: `stop_server` used
# `pkill -P "$pid"`, which kills *direct* children only. Wrangler's tree is `npx` -> node ->
# `workerd`, so `workerd` survived, kept the port, and outlived the step. Nobody noticed while the
# script ran once per job, because the leaked port was never asked for again; the second run in one
# job walked straight into it.
#
# **The fix was never watched to work, and the green runs did not prove it** — which is the whole
# reason this moved. `port_in_use` steps over a held port by choosing another one, so the harness is
# green whether or not the tree died. A leak here shows up as a runner that quietly accumulates
# `workerd` processes until the job ends, and as nothing at all in the log.
#
# So it is here, where `tests/shell/kill-tree.test.sh` can source it and drive it against a tree it
# built on purpose. One implementation, driven directly; `scripts/e2e.sh` sources this same file.
#
# ------------------------------------------------------------------------------------------------
# THE FIXTURE THAT DOES NOT WORK, WRITTEN DOWN SO NOBODY REBUILDS IT.
#
# The obvious way to make a three-deep tree is `bash -c 'bash -c "sleep 300"' &`, and it produces
# **one** process, not three: `bash -c` with a single simple command *execs* it rather than forking,
# so each layer replaces itself and what is left is one `sleep`. A test built on it passes against a
# `kill_tree` that only ever kills the pid it was handed — which is precisely the bug this exists to
# prevent. The test appends `; true` at each level to stop the exec optimisation.

# The direct children of a pid, from the kernel rather than from a tool.
#
# **`pgrep -P` is the obvious way and it is not guaranteed to be there** — the same lesson
# `port_in_use` learned about `ss`, which is missing from `mcr.microsoft.com/dotnet/sdk:10.0`.
# `/proc/<pid>/stat` cannot be missing on Linux. Its fourth field is the parent pid, and the second
# is the command name in parentheses, which can itself contain spaces — so the parse starts after
# the last `)` rather than counting fields from the left, which is the bug every naive
# `/proc/*/stat` reader has.
children_of() {
  local parent="$1" entry pid stat

  for entry in /proc/[0-9]*; do
    pid="${entry##*/}"
    stat="$(cat "$entry/stat" 2>/dev/null)" || continue
    stat="${stat##*) }"

    # After the trim, field 2 is ppid: "<state> <ppid> ...".
    set -- $stat
    [ "${2:-}" = "$parent" ] && echo "$pid"
  done
}

# Every descendant of a pid, depth first, then the pid itself.
#
# **One level at a time rather than a process group**: `( ... ) &` in a non-interactive shell is
# not a group leader, so `kill -- -$pid` names nothing, and turning on job control to make it one
# changes how every other background command in the harness behaves.
#
# **Depth first, and that ordering is load-bearing.** Killing the parent first orphans its children
# to init, where `children_of` can no longer find them by walking down from the pid we were given —
# they are still running, still holding whatever they hold, and now unreachable by this function.
kill_tree() {
  local pid="$1" child

  for child in $(children_of "$pid"); do
    kill_tree "$child"
  done

  kill -9 "$pid" >/dev/null 2>&1 || true
}
