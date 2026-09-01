#!/usr/bin/env bash
# A whole-tree Qodana scan of a commit, with the two traps that make one worthless closed.
#
# Run it before pushing. CI runs Qodana in *PR mode* — only changed files — so its count is not
# comparable to anything; this is how you see the real number.
#
#   ./scripts/qodana-scan.sh              # scans HEAD
#   ./scripts/qodana-scan.sh <commit>     # scans any commit
#
# ------------------------------------------------------------------------------------------------
# TRAP 1: a scan that never ran looks exactly like a clean scan.
#
# `qodana` exits **0** when it cannot find a project to inspect, printing its own `--help` and one
# line of error into a log nobody reads to the end. A `grep` for the summary line then finds
# nothing — which is indistinguishable from a scan that found nothing wrong. That has happened
# here, and it is why this script asserts the report exists and parses before reporting a count,
# and exits non-zero if it does not.
#
# TRAP 2: Docker Desktop cannot see Git Bash's `/tmp`.
#
# `/tmp` in this shell is an MSYS mount that does not exist inside the Docker VM, so
# `-v /tmp/export:/data/project/` mounts an **empty directory**, the container finds no
# `qodana.yaml`, and you are in trap 1. That is the exact way this went wrong. Every host path
# below goes through `pwd -W` to become a real Windows path, and the mount is proved by looking
# for `qodana.yaml` from inside the container before the scan starts.
#
# TRAP 3: scanning the working directory is not scanning the commit.
#
# `bin/` and `obj/` are not in the export and are very much in your working directory. The same
# commit reported **0** from a clean export and **1471** in place, including `.CSharpErrors` on
# test files that build clean. So this exports with `git archive` and never mounts the tree.
set -euo pipefail

commit="${1:-HEAD}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
image="jetbrains/qodana-cdnet:2026.2"

cd "$root"

if ! docker version --format '{{.Server.Version}}' >/dev/null 2>&1; then
    echo "error: this needs a running Docker daemon. Start Docker Desktop." >&2
    exit 1
fi

sha="$(git rev-parse --short "$commit")"

# Under the repository, not under /tmp — see trap 2. Gitignored; see .gitignore.
work="$root/.qodana-scan"
rm -rf "$work"
mkdir -p "$work/project" "$work/results"

# **The export is deleted on the way out, and on Windows that is a bug fix rather than tidiness.**
# Qodana builds the project it is given, so the export does not stay the clean tree `git archive`
# wrote — it grows `obj/Release/net10.0/…` under every test project. In a worktree whose root is
# already 125 characters, the deepest of those lands around 288 and Windows' 260-character limit
# starts refusing operations on it: `git worktree remove` fails with **"Filename too long"**, having
# already deregistered the worktree, which leaves half a gigabyte of orphaned files that git can no
# longer help you delete. That happened.
#
# **`results/` and the log survive**, because they are what the run is for and what a failure is
# read from. The export does not need keeping: it is `git archive <commit>` and one command
# reproduces it exactly.
#
# On EXIT rather than at the end of the happy path, so the SARIF-missing bail-out and a Ctrl-C
# leave no more behind than a clean run does. A trap that only fires when nothing went wrong is a
# trap that never fires on the runs that matter.
trap 'rm -rf "$work/project"' EXIT

echo "Exporting $commit ($sha) to a clean tree — no bin/, no obj/ (trap 3)."
git archive "$commit" | tar -x -C "$work/project"

# The positive control on the export itself. Without it, a `git archive` that produced nothing
# scans an empty directory and lands in trap 1.
for required in qodana.yaml; do
    if [ ! -f "$work/project/$required" ]; then
        echo "error: $required is not in the export of $sha, so the scan would inspect nothing." >&2
        exit 1
    fi
done

project_path="$(cd "$work/project" && pwd -W 2>/dev/null || echo "$work/project")"
results_path="$(cd "$work/results" && pwd -W 2>/dev/null || echo "$work/results")"

# The positive control on the *mount* — see trap 2. If Docker cannot see the export, this fails
# here with a clear message instead of thirty seconds later as a silent success.
echo "Checking Docker can actually see the export at $project_path"
if ! MSYS_NO_PATHCONV=1 docker run --rm -v "$project_path:/data/project/" \
        --entrypoint sh "$image" -c 'test -f /data/project/qodana.yaml' 2>/dev/null; then
    echo "error: the container cannot see qodana.yaml at /data/project/." >&2
    echo "       The host path '$project_path' is not visible to Docker Desktop." >&2
    echo "       This is trap 2: a Git Bash path such as /tmp/... mounts as an empty directory." >&2
    exit 1
fi

echo "Scanning $sha with $image. This takes a few minutes."
set +e
MSYS_NO_PATHCONV=1 docker run --rm \
    -v "$project_path:/data/project/" \
    -v "$results_path:/data/results/" \
    "$image" --save-report > "$work/qodana.log" 2>&1
scan_status=$?
set -e

sarif="$work/results/qodana.sarif.json"

# **Trap 1, closed.** The exit code is not trusted on its own — it is 0 for "I could not find a
# project". A report that exists and parses is the only evidence a scan happened.
if [ ! -s "$sarif" ]; then
    echo >&2
    echo "error: no SARIF report was written, so NOTHING WAS INSPECTED." >&2
    echo "       A count of zero here would be a scan that never ran, not a clean tree." >&2
    echo "       Container exit status was $scan_status. The last of its output:" >&2
    tail -20 "$work/qodana.log" >&2
    exit 1
fi

# Count and group by file. The tool's own summary counts by rule and never by file.
MSYS_NO_PATHCONV=1 docker run --rm -v "$results_path:/results" node:22-alpine node -e '
const report = JSON.parse(require("fs").readFileSync("/results/qodana.sarif.json", "utf8"));
const runs = report.runs || [];
if (!runs.length) { console.error("SARIF holds no runs — nothing was inspected."); process.exit(1); }

const results = runs.flatMap(r => r.results || []);
const byFile = new Map();
for (const result of results) {
    const uri = result.locations?.[0]?.physicalLocation?.artifactLocation?.uri ?? "(no file)";
    if (!byFile.has(uri)) byFile.set(uri, []);
    byFile.get(uri).push(result.ruleId);
}

console.log("");
console.log("Findings: " + results.length);
for (const [file, rules] of [...byFile].sort((a, b) => b[1].length - a[1].length)) {
    console.log("  " + String(rules.length).padStart(4) + "  " + file);
    for (const rule of [...new Set(rules)].sort()) console.log("          " + rule);
}
if (!results.length) console.log("  (none — and the report exists, so this is a real zero)");
'

echo
echo "Full report: $work/results/qodana.sarif.json"
echo "Log:         $work/qodana.log"
