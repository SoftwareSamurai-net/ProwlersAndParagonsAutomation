#!/usr/bin/env bash
# Drives the assembled application: the real published site, served by the real `wrangler pages
# dev`, in real Chrome.
#
#   ./scripts/e2e.sh                    # publish, serve, drive, and drive every twin
#   ./scripts/e2e.sh --driver dotnet    # the same, with the Playwright driver
#   ./scripts/e2e.sh --real-only        # skip the negative controls (NOT a full run — see below)
#
# ------------------------------------------------------------------------------------------------
# THERE ARE TWO DRIVERS AND THIS SCRIPT IS NEITHER OF THEM.
#
# What this file owns is everything *around* a drive: publishing, parsing the wrangler version out
# of deploy.yml, starting the server from a directory with no `functions/` in it, building each
# deliberately-broken twin, driving it, and deciding what the verdicts mean. A driver takes a URL
# and prints three kinds of line. That split is the whole reason a second driver cost a flag here
# rather than a rewrite.
#
#   node    scripts/e2e/drive.mjs — a hand-rolled DevTools Protocol client, five checks. The
#           default, and the one with a track record.
#   dotnet  tests/e2e — Microsoft.Playwright, the same five plus `A11Y`, which runs axe-core
#           inside the page and which the hand-rolled client cannot do at all.
#
# **Neither is retired and the second has not replaced the first.** `PROGRESS.md` item 10 states
# the condition under which `scripts/e2e/` goes, and removing a working harness before its
# replacement has a record is how an upgrade becomes a regression.
#
# Stage one of `PROGRESS.md` item 10, and **anonymous only**: no account, no credential, no
# bypass. Everything behind sign-in is stage two's business and nothing here reaches for it.
#
# ------------------------------------------------------------------------------------------------
# WHAT THIS ANSWERS THAT NOTHING ELSE DID.
#
# Three suites, nineteen browser verdict harnesses and a pixel diff of eight proof pages all ran
# before this existed, and none of them ran the application. A proof page is *markup and CSS*
# rendered through bUnit against the real stylesheets — which is what catches a contrast fault or
# a broken layout, and is not the running app. So none of these was exercised by anything:
#
#   - Blazor WebAssembly actually booting.
#   - `js/*.js` interop for real. bUnit answers **null** to every interop read.
#   - Client-side routing. `Areas.Of` is asserted by unit test; no browser ever followed a link.
#   - The real `_headers`. A `file://` proof page has no Content-Security-Policy at all, and this
#     one is generated per deploy from a hash of the import map Blazor writes.
#
# **And one thing sharper than any of those.** A feature shipped here — tested, adversarially
# reviewed twice, merged — while *nothing in the application ever wrote to the store it read
# from*. Every unit and component test passed, and passed honestly, because every one of them
# called the store directly. Nothing in this repository asked whether a feature was reachable by
# an ordinary person doing an ordinary thing. A driver has no other way in, so it asks that
# question by construction.
#
# ------------------------------------------------------------------------------------------------
# THE WRANGLER VERSION IS READ OUT OF deploy.yml AND NEVER GUESSED.
#
# `.github/workflows/deploy.yml` carries `# wrangler=<version>` on its `uses:` line, and
# `WranglerIsPinnedToOneVersion` holds that comment, the action's `wranglerVersion:` input and
# `scripts/apply-migrations.sh`'s own constant to being one version. This script parses the same
# comment `build.yml`'s dry-run does — so it declares no version of its own and cannot become a
# fourth thing to keep in step. **If the parse fails this script refuses**, for the reason
# build.yml states in as many words: a fallback is precisely how the point gets lost quietly.
#
# ------------------------------------------------------------------------------------------------
# NO npm DEPENDENCY, AND NO package.json.
#
# This repository has never had one. `scripts/visual/png.mjs` is a hand-written PNG decoder rather
# than `pixelmatch` for the same reason, and the trade is the same here: Puppeteer is a large tree
# that downloads a second Chrome, and what is actually needed is a WebSocket and a dozen DevTools
# methods. `scripts/e2e/cdp.mjs` is that, against Node's own global `WebSocket`.
#
# **The Playwright driver does not break that and it is worth saying why, because it obviously
# looks as though it should.** Both its packages are NuGet, restored by the `dotnet restore` this
# repository already runs — measured at +4 seconds on the runner. And it does not download a
# browser: `Channel = "chrome"` launches the Google Chrome already on the machine, the same one
# `cdp.mjs` finds and the same one `ubuntu-latest` ships. So there is no `playwright install`
# step, nothing to cache, and no *third* renderer beside the runner's Chrome and the digest-pinned
# selenium/standalone-chrome the pixel goldens need — which docs/guide/testing.md records as
# costing 32,462 pixels of disagreement on a single page.
#
# ------------------------------------------------------------------------------------------------
# EVERY CHECK HAS A DELIBERATELY-BROKEN TWIN, AND THAT IS NOT OPTIONAL.
#
# CLAUDE.md: *a check is not done until you have broken it and watched it fail*, and *a denylist
# of spellings cannot make a verdict honest — build the broken twin*. So beside the real site this
# builds one twin per check: the same published output with one documented line changed
# (`scripts/e2e/defects.mjs`), served the same way, driven by the **byte-identical** harness. The
# real site must report every check green **and** each twin must report its own check red.
#
# Three properties, each of which has a history:
#
#   - **A twin must SAY FAIL**, not merely fail to say PASS. Three of this repository's four
#     historical guard faults were a harness that never ran being read as a harness that passed,
#     so `drive.mjs` catches inside each check and prints a verdict either way.
#   - **A twin throws if its documented line has moved**, so it cannot quietly stop reproducing
#     its defect and start passing for the wrong reason. Same shape as `ProofPages.WithDefect`.
#   - **Every check must have a twin.** The check names the real run reported are compared against
#     the check names the twins cover, and a check with no twin fails this script. Adding one
#     without a negative control is a red run rather than a silence. The *converse* — a twin whose
#     check nobody runs — moved to `E2eDriverTests` when the second driver arrived, because this
#     script runs one driver and cannot tell "no driver has this check" from "not this one". The
#     comment at that comparison says so at length; do not restore the equality test.
#
# `--real-only` exists for the ten-second local loop while a check is being written. It prints
# that the run proved nothing about whether the checks can fail, and CI never passes it.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# ------------------------------------------------------------------------------------------------
# Where everything lives.
#
# **`wrangler pages dev` is run from `$root`, and that placement is the configuration.** Wrangler
# bundles a `functions/` directory found in the *working directory* — there is no flag for it — so
# running from the repository root bundles the accounts API, and the server this harness drives is
# the whole application rather than its static half.
#
# **This was the other way round for the whole of stage one, and the reversal is the change worth
# reading.** It ran from `$work` specifically so that no `functions/` was found: wrangler said "No
# Functions. Shimming...", every `/api/` address fell through `_redirects` to `index.html`, and the
# app read an unparseable answer as "anonymous". That was a real answer to a real question — stage
# one had no database to bind and no business inventing one — but it meant nothing behind sign-in
# was reachable, and it meant the six anonymous checks were passing against a server that was not
# the deployed shape.
#
# **What made the reversal cheap is measured rather than assumed: all six anonymous checks pass
# unchanged against the bundled server.** `/api/me` answers `401 {"error":"Nobody is signed in."}`
# instead of an unparseable `index.html`, and the app treats both as "anonymous" — so there is one
# server configuration here and not two. That mattered: two would have meant two publishes, two
# starts and two drives per run, against a Build job already measured at 18–19 minutes.
#
# **And the site directories are named relative to `$root` on purpose.** Wrangler is Node, so it
# cannot read a Git Bash path like `/c/Users/...`; passing `.e2e/site` from inside `$root` needs no
# conversion and therefore cannot be got wrong on one platform and not the other. The two paths
# that *must* be absolute — the D1 state directory, which wrangler resolves against `--cwd` rather
# than against the process — are written as `$root/...`, which MSYS converts on the way to a native
# binary and Linux passes through.
work="$root/.e2e"
site="$work/site"
twins="$work/twins"
logs="$work/logs"

# Where the local D1 lives, and the name every server and every seed reaches it by.
#
# **`--persist-to` is given to both `wrangler d1` and `wrangler pages dev`, and they have to agree
# or the harness seeds one database and serves another.** They would still both work: the sign-in
# would simply refuse, `RULES` would go red on its control, and the run would report a defect that
# is not in the application at all. So this is one variable, used in both places.
d1_state="$work/d1state"

# The addresses the seeded world uses. `worker/invitations.js` makes whoever `ADMIN_EMAIL` names
# the bootstrap administrator, so this is deliberately **not** the address the driver signs in as —
# see `scripts/e2e/seed.mjs`, which declares both and is the only place they are written down.
e2e_owner_email="owner@e2e.invalid"

# The first port tried. Each server gets its own rather than waiting for one to be released:
# a stale listener on a port a new server then fails to bind is not hypothetical — it happened
# while this script was being written, and the symptom was a server that answered nothing while
# reporting itself ready.
first_port=8788

# How long one drive of the site may take before it is killed and named.
#
# **This file capped everything except the thing it exists to run, which is the same gap it just
# fixed one level down.** `scripts/visual-regression.sh` bounded the *comparison* and not the
# *capture*, and a wedged Chrome duly spent 1666 seconds saying nothing and cancelled the Build
# job — see `docs/guide/hosting.md`. Every wait inside `drive.mjs` is bounded (45s for a render,
# 30s for a navigation) and `start_server` gives up after 180s, but nothing bounded a *drive*:
# six servers times five checks times two waits is well past the 30-minute job cap, so a browser
# that stopped answering here would cancel the job in exactly the same way.
#
# **One value, used by both the `timeout` and the messages quoting it**, same rule as the two
# deadlines in the visual check.
#
# 300s against a measured worst case of about 105s — that is the `base-href-dropped` twin, which
# spends two deliberate 45-second timeouts proving deep links cannot load the framework. The real
# site's five checks are about 26 seconds. So this is not a performance budget; it is far enough
# above the honest cost that a loaded runner cannot trip it.
#
# `-k 10s` escalates to SIGKILL, because a driver killed mid-run does not get to run its own
# `close()`. Its Chrome can outlive it: on a runner that is collected when the job ends — the
# cancelled run's log shows exactly that, `Terminate orphan process: (chrome)` — and locally
# `release_port` does not cover it, so a hang here may leave one headless Chrome behind.
drive_deadline=300s

# **There are two drivers, and which one runs is an argument rather than a guess.**
#
# `scripts/e2e/drive.mjs` is the hand-rolled DevTools Protocol client this script was written
# for. `tests/e2e` is a C# one over Microsoft.Playwright, which drives the same five checks plus
# an axe-core accessibility check the other cannot do at all. Both print the same three lines
# this script reads, which is the only contract between them. **Neither is retired**;
# `PROGRESS.md` item 10 carries the condition under which the first one is, and until that is met
# the argument for keeping it is that it is the one with a track record.
#
# **`PP_E2E_DRIVER` stays, and is still only a test seam** — same reason as `PP_CHROME_BIN` in
# scripts/visual-regression.sh and `WRANGLER_BIN` in scripts/apply-migrations.sh: a driver that
# never returns cannot be arranged with a real one, and a deadline that has never been watched to
# fire is a claim rather than a guard. **It cannot carry a real driver any more, and that is why
# `--driver` exists.** `read -r -a` word-splits, this repository's own checkout is under
# "Personal Projects", and the .NET driver's path therefore tears in half at the space — so the
# seam that was fine for `node scripts/e2e/drive.mjs` silently could not express the second
# driver at all. `--driver` builds the array itself and never splits a path.
real_only=0
driver_name=node

while [ $# -gt 0 ]; do
  case "$1" in
    --real-only) real_only=1 ;;
    --driver)
      shift
      driver_name="${1:-}"
      case "$driver_name" in
        node|dotnet) ;;
        *) echo "error: --driver takes 'node' or 'dotnet', not '${driver_name}'" >&2; exit 2 ;;
      esac
      ;;
    -h|--help)
      echo "usage: $0 [--driver node|dotnet] [--real-only]"
      echo ""
      echo "  --driver      which harness drives the site. 'node' is scripts/e2e/drive.mjs, the"
      echo "                default and the one with a track record; 'dotnet' is tests/e2e, the"
      echo "                Playwright one, which also runs the accessibility check."
      echo "  --real-only   drive the real site and skip the twins. NOT a full run: it proves"
      echo "                nothing about whether any check is able to fail."
      echo ""
      echo "environment:"
      echo "  PP_E2E_CHROME                path to Chrome, if it is not where the driver looks"
      echo "  PP_E2E_SITE_ALREADY_BUILT=1  reuse publish/wwwroot instead of publishing again"
      exit 0
      ;;
    *) echo "error: unrecognised argument '$1'" >&2; exit 2 ;;
  esac
  shift
done


# ------------------------------------------------------------------------------------------------
# The wrangler the deploy uses. Read, never declared here — see this file's header.

wrangler_version=$(sed -n \
  's/^.*wrangler-action@v[0-9][0-9.]*.*# wrangler=\([0-9.]*\).*$/\1/p' \
  "$root/.github/workflows/deploy.yml") || wrangler_version=''

if [ -z "$wrangler_version" ]; then
  cat >&2 <<'EOF'
::error::Could not read a wrangler version out of .github/workflows/deploy.yml's
::error::'uses: cloudflare/wrangler-action@…  # wrangler=<version>' line.
::error::
::error::Refusing to serve the site with a guess. The whole point of pinning here is that this
::error::harness runs the same server the deploy does; a fallback would keep the run green while
::error::silently testing against a different wrangler. See WranglerIsPinnedToOneVersion.
EOF
  exit 2
fi

echo "deploy.yml pins wrangler@${wrangler_version}; serving with that."

# ------------------------------------------------------------------------------------------------
# Publish the site, unless a caller in the same job already did.

rm -rf "$twins" "$logs"
mkdir -p "$work" "$twins" "$logs"

# An array and not a string, which is where `WRANGLER_BIN`'s shape does not carry over: that one
# is `npx --yes wrangler@<version>` and word-splits safely because it contains no path.
if [ -n "${PP_E2E_DRIVER:-}" ]; then
  read -r -a driver <<< "$PP_E2E_DRIVER"
elif [ "$driver_name" = "dotnet" ]; then
  e2e_dll="$root/tests/e2e/bin/Release/net10.0/ProwlersAndParagons.E2e.dll"

  # **Built here rather than assumed, and the build output is not a verdict.** `dotnet run` would
  # interleave MSBuild's output with the three lines this script parses; `dotnet build` followed
  # by the dll keeps the driver's stdout to the protocol. If the dll is missing after a
  # successful build, something is wrong with the project rather than with the site, and saying
  # so here is cheaper than a driver that prints nothing and reads as a browser that hung.
  echo "Building the Playwright driver..."
  dotnet build "$root/tests/e2e/ProwlersAndParagons.E2e.csproj" \
    -c Release --nologo -v q > "$logs/driver-build.log" 2>&1 \
    || { echo "::error::building tests/e2e failed:"; tail -30 "$logs/driver-build.log"; exit 2; }

  [ -s "$e2e_dll" ] || {
    echo "::error::tests/e2e built without producing $e2e_dll."
    exit 2
  }

  driver=(dotnet "$e2e_dll")
else
  driver=(node "$root/scripts/e2e/drive.mjs")
fi

if [ "${PP_E2E_SITE_ALREADY_BUILT:-0}" != "1" ]; then
  echo "Publishing the browser front end..."

  # `EnableDefaultCompressionFormats=false` for the same measured reason build.yml gives: Brotli
  # is over half the time of this step and nothing here reads a `.br` sidecar.
  dotnet publish "$root/web/ProwlersAndParagons.Web.csproj" \
    --configuration Release -p:EnableDefaultCompressionFormats=false \
    -o "$root/publish" > "$logs/publish.log" 2>&1 \
    || { echo "::error::publishing the site failed:"; tail -40 "$logs/publish.log"; exit 2; }

  # The real Content-Security-Policy, generated from the import map Blazor just wrote. Without
  # this the served site has no policy at all and the boot check's CSP assertion is vacuous —
  # which is the failure shape this whole repository keeps having to fix.
  "$root/scripts/write-cloudflare-headers.sh" "$root/publish/wwwroot"
fi

for required in index.html _headers _redirects; do
  [ -s "$root/publish/wwwroot/$required" ] || {
    echo "::error::publish/wwwroot/$required is missing or empty, so there is nothing to serve."
    exit 2
  }
done

rm -rf "$site"
cp -r "$root/publish/wwwroot" "$site"
echo "Serving $(find "$site" -type f | wc -l | tr -d ' ') published files."

# ------------------------------------------------------------------------------------------------
# The local database the signed-in checks drive against.
#
# **Read out of `d1/wrangler.toml`, never declared here**, for the same reason the wrangler version
# is read out of `deploy.yml`: a constant copied into a second file is a constant that drifts, and
# a harness that seeded one database while the server bound another would report a defect that is
# not in the application. If the parse fails this script refuses rather than guessing — the same
# rule, and for the same reason, as the version above.
database_id=$(sed -n 's/^[[:space:]]*database_id[[:space:]]*=[[:space:]]*"\([^"]*\)".*$/\1/p' \
  "$root/d1/wrangler.toml" | head -1)

if [ -z "$database_id" ]; then
  cat >&2 <<'EOF'
::error::Could not read a database_id out of d1/wrangler.toml.
::error::
::error::Refusing to bind a guess. `wrangler pages dev --d1 DB=<id>` and
::error::`wrangler d1 execute --local` key their local state by that id, so a wrong one is not an
::error::error anywhere — it is a second, empty database that the seed writes to and the server
::error::never reads. Every signed-in check would go red on its control, blaming the application.
EOF
  exit 2
fi

# **Migrated here rather than by `scripts/apply-migrations.sh`, which is a different job.** That
# script is the deploy's, it runs `--remote` against production, and it exists to refuse a
# destructive migration before an upload. This needs the opposite: a throwaway local database
# brought up to the same schema, with no gate and nothing to protect. The one thing they share is
# `d1/migrations`, which is the source both read.
echo "Migrating the local D1 in ${d1_state}..."
mkdir -p "$d1_state"

CI=1 WRANGLER_SEND_METRICS=false CLOUDFLARE_API_TOKEN='' \
  npx --yes "wrangler@${wrangler_version}" --cwd "$root/d1" \
    d1 migrations apply prowlers-and-paragons \
    --local --persist-to "$d1_state" > "$logs/d1-migrate.log" 2>&1 \
  || { echo "::error::migrating the local D1 failed:"; tail -30 "$logs/d1-migrate.log"; exit 2; }

# **The positive control for the migration, and it is not decoration.** A `d1 migrations apply`
# that matched no migrations exits zero and prints a table with nothing in it, which is
# indistinguishable from success right up until a signed-in check reports `no such table:
# login_tokens` as though the application were at fault.
if ! grep -q "0001_accounts.sql" "$logs/d1-migrate.log"; then
  echo "::error::the local D1 migration ran without naming 0001_accounts.sql, so the schema the"
  echo "::error::signed-in checks need may not be there. Read $logs/d1-migrate.log."
  exit 2
fi

# The database's name, beside its id above and read the same way — `d1 execute` takes the name and
# `pages dev --d1` takes the id, so both are needed and neither is written here.
database_name=$(sed -n 's/^[[:space:]]*database_name[[:space:]]*=[[:space:]]*"\([^"]*\)".*$/\1/p' \
  "$root/d1/wrangler.toml" | head -1)

[ -n "$database_name" ] || {
  echo "::error::Could not read a database_name out of d1/wrangler.toml. Refusing to guess: see"
  echo "::error::the database_id refusal above, which is the same failure from the other side."
  exit 2
}

# Mints a token, writes the world it belongs to, and prints the raw token — which is the one thing
# the driver needs and the only thing it is told. See scripts/e2e/seed.mjs's header for why this is
# test-seeding rather than an authentication bypass.
#
# **Node writes the SQL and this runs it**, rather than Node shelling out to wrangler itself: Node
# refuses to spawn `npx.cmd` without a shell, and putting the seed's SQL through `cmd.exe`'s
# quoting is the thing that refusal exists to prevent. seed.mjs's header records the failure.
#
# `$1` is an optional seed-defect name; with none, the world is the ordinary one.
seed_database() {
  local defect="${1:-}"
  local sql="$work/seed.sql"
  local args=(--root "$root" --sql-out "$sql")
  local token

  [ -n "$defect" ] && args+=(--defect "$defect")

  token="$(node "$root/scripts/e2e/seed.mjs" "${args[@]}")" || return 1

  CI=1 WRANGLER_SEND_METRICS=false CLOUDFLARE_API_TOKEN='' \
    npx --yes "wrangler@${wrangler_version}" --cwd "$root/d1" \
      d1 execute "$database_name" --local --persist-to "$d1_state" \
      --file "$sql" > "$logs/d1-seed.log" 2>&1 || {
    echo "::error::seeding the local D1 failed:" >&2
    tail -20 "$logs/d1-seed.log" >&2
    return 1
  }

  printf '%s' "$token"
}

# ------------------------------------------------------------------------------------------------
# Starting and stopping one server.

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

# The direct children of a pid, from the kernel rather than from a tool.
#
# **`pgrep -P` is the obvious way and it is not guaranteed to be there** — the same lesson
# `port_in_use` just learned about `ss`, which is missing from `mcr.microsoft.com/dotnet/sdk:10.0`.
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
# changes how every other background command in this script behaves.
kill_tree() {
  local pid="$1" child

  for child in $(children_of "$pid"); do
    kill_tree "$child"
  done

  kill -9 "$pid" >/dev/null 2>&1 || true
}

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
    # is `npx` -> node -> `workerd`, so `workerd` survived, kept the port, and outlived the step.
    # Nobody noticed while the script ran once per job, because the leaked port was never asked
    # for again; the second run in one job walked straight into it. `release_port` did not cover
    # it either: it reads pids out of `netstat`, which is Windows-only, so on Linux it returned
    # immediately having done nothing and warned about nothing.
    kill_tree "$pid"
  fi

  kill "$pid" >/dev/null 2>&1 || true
  wait "$pid" 2>/dev/null || true

  # The backstop, not the mechanism. If the tree kill above missed something, this names it.
  release_port "$port"
}

trap stop_server EXIT INT TERM

# start_server <directory-relative-to-$root> <port> <log-name>
start_server() {
  local dir="$1" port="$2" name="$3"
  local log="$logs/$name.log"

  server_port="$port"

  (
    cd "$root" || exit 1
    CI=1 WRANGLER_SEND_METRICS=false CLOUDFLARE_API_TOKEN='' \
      exec npx --yes "wrangler@${wrangler_version}" pages dev "$dir" \
        --ip 127.0.0.1 --port "$port" \
        --d1 "DB=${database_id}" --persist-to "$d1_state" \
        --binding "SITE_URL=http://127.0.0.1:${port}" "ADMIN_EMAIL=${e2e_owner_email}" \
        > "$log" 2>&1
  ) &
  server_pid=$!

  # **Readiness is the served body, not the status code, and not the log line.** Wrangler prints
  # "Ready on http://…" and then, if its worker died, answers requests by hanging for ever. The
  # marker below is the app's own boot screen: if that is not in the answer, whatever is
  # listening is not this site, and the run stops here rather than three minutes later inside a
  # check that reports the wrong thing.
  local deadline=$((SECONDS + 180))
  while [ "$SECONDS" -lt "$deadline" ]; do
    if ! kill -0 "$server_pid" 2>/dev/null; then
      echo "::error::wrangler exited before it was ready:"
      tail -30 "$log"
      return 1
    fi

    local body
    if body=$(curl -fsS --max-time 5 "http://127.0.0.1:${port}/" 2>/dev/null); then
      case "$body" in
        *'class="boot"'*)
          # **The server has to be the whole application, and this is what says so.** Wrangler
          # bundles `functions/` from its working directory and prints "No Functions. Shimming..."
          # when it finds none — which is stage one's server, and which every signed-in check would
          # then fail against while blaming the application for refusing a session it was never
          # offered. A directory moved, a `cd` lost in an edit, or a `functions/` renamed all land
          # here, and all of them are silent otherwise.
          if grep -q "No Functions" "$log"; then
            echo "::error::wrangler found no functions/ directory, so /api/ is not being served by"
            echo "::error::the accounts server at all — it falls through _redirects to index.html."
            echo "::error::Nothing behind sign-in can work against this server. wrangler bundles"
            echo "::error::functions/ from its working directory and this ran in $(pwd)."
            tail -20 "$log"
            return 1
          fi

          echo "  wrangler pages dev serving $dir on http://127.0.0.1:${port}"
          return 0
          ;;
      esac
    fi

    sleep 1
  done

  echo "::error::wrangler never served the site on port ${port} within 180s:"
  tail -30 "$log"
  return 1
}

# ------------------------------------------------------------------------------------------------
# The real site.

echo ""
echo "=== The real site ================================================================"

port="$(next_free_port "$first_port")"
start_server .e2e/site "$port" real || exit 1

# **A token per drive, minted immediately before it, because a token is single-use.**
#
# `db.spendLoginToken` burns the row on first click, so one token cannot serve the real run and
# then a twin: the second drive would find it used, fail to sign in, and report the application as
# broken. Seeding per drive also resets the world — see seed.mjs — so no check can pass on a
# session an earlier drive left behind, which is the same fault as a feature reading a store
# nothing writes to.
#
# **The driver is told a token and nothing else.** It has no database, no wrangler and no
# credential; it navigates to `/signin?t=<token>` the way a person clicks a link in their mail, and
# the application does the rest. That is what keeps "nothing reaches past the browser" true here:
# the row is what an email would have caused, and everything after it is the app's own verify path.
export PP_E2E_SIGNIN_TOKEN
PP_E2E_SIGNIN_TOKEN="$(seed_database)" || {
  echo "::error::seeding the local D1 failed, so no signed-in check can run. See above."
  exit 1
}

[ -n "$PP_E2E_SIGNIN_TOKEN" ] || {
  echo "::error::the seed printed no token. A driver handed an empty one would sit on the sign-in"
  echo "::error::form and report every signed-in check red, blaming the application."
  exit 1
}

real_log="$logs/real-drive.log"
real_status=0

# **`pipefail` is what makes this the driver's status and not `tee`'s, and a `PIPESTATUS` line
# here used to throw it away.** This was `… || real_status=$?` followed by
# `real_status=${PIPESTATUS[0]:-$real_status}`, which looks like a belt to that brace and is the
# opposite: `set -o pipefail` at the top of this file already gives the pipeline the driver's
# exit code, so the `||` captures it correctly — and then the second line reads `PIPESTATUS`
# *after an assignment*, which is `0`. `:-` defaults only on empty or unset, and `0` is neither,
# so a real 124 became a 0 every time.
#
# Found by watching the deadline below fire: it exited 1, but through the summary-line arm rather
# than the timeout arm, because `real_status` was never anything but zero. Which means the
# non-zero-status check has been dead since this script was written, and the count check was
# quietly carrying it — a redundant guard covering for a broken one, and the reason to break a
# guard rather than read it.
timeout -k 10s "$drive_deadline" "${driver[@]}" "http://127.0.0.1:${port}" 2>&1 \
  | tee "$real_log" || real_status=$?

stop_server

if [ "$real_status" -eq 124 ]; then
  echo ""
  echo "::error::the driver did not finish within ${drive_deadline} against the real site and was"
  echo "::error::killed. Every wait inside it is bounded, so this is the browser or the server"
  echo "::error::having stopped answering rather than a slow check — read $real_log for how far it"
  echo "::error::got. Reported separately from a failed check because they are different faults."
  exit 1
fi

# **The count comes out of the harness's own summary line rather than being assumed.** A driver
# that died on its second check still prints two verdicts, and two greens out of five is not four
# reds — it is a run that did not happen.
ran_line=$(grep -o 'E2E RAN [0-9]* CHECKS, [0-9]* PASSED' "$real_log" | tail -1 || true)

if [ -z "$ran_line" ]; then
  echo ""
  echo "::error::the driver never printed its summary line, so it did not finish. Nothing below"
  echo "::error::this point can be believed; read $real_log."
  exit 1
fi

expected=$(echo "$ran_line" | sed 's/^E2E RAN \([0-9]*\) CHECKS.*/\1/')
passed=$(echo "$ran_line" | sed 's/.*, \([0-9]*\) PASSED$/\1/')

if [ "$expected" -eq 0 ]; then
  echo "::error::the driver ran zero checks. A list that has stopped matching reads exactly like"
  echo "::error::a suite that passed, which is this repository's oldest failure shape."
  exit 1
fi

if [ "$passed" -ne "$expected" ] || [ "$real_status" -ne 0 ]; then
  echo ""
  echo "::error::$((expected - passed)) of $expected checks failed against the real site. Each"
  echo "::error::failing line above says whether it was the positive control (the work did not"
  echo "::error::happen) or the outcome (it happened and was wrong)."
  exit 1
fi

echo ""
echo "All $expected checks passed against the real site."

if [ "$real_only" -eq 1 ]; then
  cat <<'EOF'

================================================================================
--real-only: THE TWINS WERE NOT DRIVEN.

So this run says the checks passed and says NOTHING about whether any of them is
able to fail. Four checks in this repository have passed while the thing they
measured never ran at all. Do not read this as a green run; re-run without the
flag before believing it.
================================================================================
EOF
  exit 0
fi

# ------------------------------------------------------------------------------------------------
# The twins. One per check, each required to turn its own check red.

echo ""
echo "=== The deliberately-broken twins ================================================"

# `name:CHECK:kind` per line, where kind is `file` or `seed` — see defects.mjs on why a signed-in
# check cannot have a file twin.
mapfile -t defect_lines < <(node "$root/scripts/e2e/defects.mjs" --list)

if [ "${#defect_lines[@]}" -eq 0 ]; then
  echo "::error::defects.mjs listed no twins, so there is no negative control for anything."
  exit 1
fi

# **Every check must have a twin, and this is the guard that says so.**
#
# **`[A-Z][A-Z0-9_]*` and not `[A-Z][A-Z_]*`, which was a real hole rather than tidying.** The
# pattern had no digits in it, so the first check name to contain one — `A11Y` — matched as the
# single letter `A`, and the comparison below would have disagreed for a reason that says nothing
# about twins. The identical fault was found by mutation in `E2eDriverTests` on the same day, in
# four places, which is the argument for looking wherever a pattern like this is copied.
checks_run=$(grep -o '^E2E CHECK [A-Z][A-Z0-9_]*' "$real_log" | sed 's/^E2E CHECK //' | sort -u)
checks_twinned=$(printf '%s\n' "${defect_lines[@]}" | cut -d: -f2 | sort -u)


# **Only one direction is checked here now, and the other moved. Read this before restoring it.**
#
# It used to require the two sets to be *equal*. That was right while there was one driver. There
# are two — `scripts/e2e/drive.mjs` and `tests/e2e` — and they do not run the same checks: the
# Playwright one adds `A11Y`, which needs axe-core and which the hand-rolled client cannot do. So
# equality would fail every run of the node driver, for a twin that is covered perfectly well by
# the other, and the only ways out of that are to delete a negative control or to keep two twin
# lists. Both are worse than what was lost.
#
# **What is kept is the direction whose failure costs a missed regression**: a check that is
# driven and has no twin has never been watched to fail, and joins the suite as a claim.
#
# **What moved is the orphan direction** — a twin naming a check nobody runs — into
# `E2eDriverTests.EveryCheckHasATwinAndEveryTwinHasACheck`, which reads *both* drivers' check
# lists and `defects.mjs` as source. That is strictly more than this script could ever see: it
# runs one driver, so it cannot tell "no driver has this check" from "not this one". And it costs
# a second in `dotnet test` instead of a publish, a server and a browser.
unproven=$(comm -23 <(echo "$checks_run") <(echo "$checks_twinned"))

if [ -n "$unproven" ]; then
  echo "::error::these checks were driven and have no deliberately-broken twin:"
  echo "::error::  $(echo "$unproven" | tr '\n' ' ')"
  echo "::error::  driven:  $(echo "$checks_run" | tr '\n' ' ')"
  echo "::error::  twinned: $(echo "$checks_twinned" | tr '\n' ' ')"
  echo "::error::A check with no twin has never been watched to fail. Add one to"
  echo "::error::scripts/e2e/defects.mjs, or remove the check."
  exit 1
fi

twin_failures=0
twins_driven=0

for line in "${defect_lines[@]}"; do
  IFS=: read -r name check kind <<< "$line"

  # A line that lost its third field would leave `kind` empty, and an empty kind falling through
  # to the file branch would try to build a site for a seed twin and fail with a message about a
  # missing directory. Named here instead.
  case "$kind" in
    file|seed) ;;
    *)
      echo "::error::defects.mjs listed '$line', whose kind field is '${kind:-(missing)}' rather"
      echo "::error::than 'file' or 'seed'. That list is this script's only contract with it."
      exit 1
      ;;
  esac

  # A twin for a check this driver did not run. Skipped rather than failed — the other driver
  # covers it, and `E2eDriverTests` is what proves *some* driver does. Reported so that a run's
  # own output says which negative controls it did and did not exercise, because "all twins
  # turned their check red" over a silently smaller list is this repository's oldest failure
  # shape wearing a green tick.
  if ! echo "$checks_run" | grep -qx "$check"; then
    echo ""
    echo "--- twin '$name' — skipped: this driver does not run $check ---"
    continue
  fi

  twins_driven=$((twins_driven + 1))

  echo ""
  echo "--- twin '$name' — $check must fail ---"

  # **Two kinds of twin, and they differ only in what is made broken.** Both serve a site with
  # `functions/` bundled and both are driven by the byte-identical harness; a `file` twin is a copy
  # of the published output with one documented line changed, and a `seed` twin is the *real*
  # published output served against a database seeded wrong. See defects.mjs on why a check that is
  # answered by the server cannot have the first kind.
  if [ "$kind" = "file" ]; then
    # Throws, loudly, if the one documented line it substitutes has moved.
    node "$root/scripts/e2e/defects.mjs" --build "$site" "$twins/$name" "$name" || {
      echo "::error::twin '$name' could not be built — see above. Until it can be, $check has no"
      echo "::error::negative control and has not been watched to fail."
      exit 1
    }

    twin_dir=".e2e/twins/$name"
  else
    twin_dir=".e2e/site"
  fi

  # A port of its own rather than the one just released: nothing then depends on how quickly the
  # previous server let go of it, and a socket in TIME_WAIT cannot be read as this server.
  port="$(next_free_port "$((port + 1))")"
  start_server "$twin_dir" "$port" "twin-$name" || exit 1

  # **Re-seeded for every twin, not only for the seed ones.** A token is single-use, so even a file
  # twin needs a live one or its own run would fail to sign in — and a twin that went red because
  # its token was spent by the previous drive is a negative control proving nothing at all. For a
  # seed twin this is also where the defect is applied.
  if [ "$kind" = "seed" ]; then
    PP_E2E_SIGNIN_TOKEN="$(seed_database "$name")"
  else
    PP_E2E_SIGNIN_TOKEN="$(seed_database)"
  fi

  [ -n "$PP_E2E_SIGNIN_TOKEN" ] || {
    echo "::error::twin '$name': the seed printed no token, so this twin proved nothing."
    exit 1
  }

  twin_log="$logs/twin-$name-drive.log"
  twin_status=0
  # **`--only $check`, because a twin needs one verdict and the other five cost minutes.**
  #
  # Exactly one line of a twin's run is read below: whether `$check` said FAIL. Everything else
  # the driver would do here is a server round trip and a browser boot against a site broken on
  # purpose in a way unrelated to it. Measured on the Playwright driver, where `A11Y` scans four
  # palettes across four addresses at 45 seconds a drive: six twins were spending four and a half
  # minutes re-measuring the accessibility of deliberately-broken sites, which no line of this
  # script looks at.
  #
  # **This cannot make a run quietly smaller, which is the only thing that would make it a bad
  # trade.** The real site above is driven with no filter at all, and it is that run whose check
  # names are compared against the twin list. Here, a name matching nothing would leave the
  # verdict line absent — and the "printed no verdict" arm below already calls that a failed
  # negative control rather than a pass.
  timeout -k 10s "$drive_deadline" "${driver[@]}" "http://127.0.0.1:${port}" --only "$check" \
    > "$twin_log" 2>&1 || twin_status=$?

  stop_server

  # **A hung driver stops the run, for the same reason a hung capture stops the visual check.**
  # Five twins at this deadline is twenty-five minutes on top of the real site, which is the
  # 30-minute job cap — and if the driver stopped coming back once, the remaining twins are going
  # to ask the same question of the same browser. The "printed no verdict" arm below is for a
  # driver that *finished* without reaching this check, which is a different and recoverable
  # thing; this one is not.
  if [ "$twin_status" -eq 124 ]; then
    echo "::error::twin '$name': the driver did not finish within ${drive_deadline} and was killed,"
    echo "::error::so $check has no verdict here and this twin proved nothing. Every wait inside the"
    echo "::error::driver is bounded, so read $twin_log for how far it got. Stopping rather than"
    echo "::error::driving the remaining twins through the same browser."
    exit 1
  fi

  # A twin's verdict is a FAIL it *printed*, never the absence of a PASS: a driver that fell over
  # before reaching this check leaves the line out entirely, and "not PASS" would call that a
  # working negative control.
  if grep -q "^E2E CHECK ${check}: FAIL" "$twin_log"; then
    echo "  ok    $check went red, as it must: $(grep "^E2E CHECK ${check}: FAIL" "$twin_log" \
      | sed 's/^E2E CHECK [A-Z][A-Z0-9_]*: FAIL — //')"
  elif grep -q "^E2E CHECK ${check}: PASS" "$twin_log"; then
    echo "::error::twin '$name' reports $check as PASSING. The check cannot see the defect it"
    echo "::error::exists to catch, so its green verdict against the real site means nothing."
    echo "::error::What this twin breaks: $(node "$root/scripts/e2e/defects.mjs" --why "$name")"
    twin_failures=$((twin_failures + 1))
  else
    echo "::error::twin '$name' printed no verdict for $check at all, so the harness did not"
    echo "::error::reach it. A missing verdict is not a failure — see $twin_log."
    twin_failures=$((twin_failures + 1))
  fi
done

echo ""

if [ "$twin_failures" -ne 0 ]; then
  echo "::error::$twin_failures of $twins_driven twins did not turn their check red."
  exit 1
fi

# **The count comes from the twins actually driven, not from the length of the list.** With two
# drivers the list is longer than any one run exercises, and "all 6 twins turned their check red"
# printed after driving 5 of them is a sentence that is not true — which is the shape of the four
# green-but-vacuous checks this whole harness exists to stop.
if [ "$twins_driven" -eq 0 ]; then
  echo "::error::no twin was driven at all, so nothing here has a negative control. The set of"
  echo "::error::checks the driver reported and the set defects.mjs twins have stopped"
  echo "::error::overlapping; read the two lists above."
  exit 1
fi

echo "All $twins_driven of ${#defect_lines[@]} twins turned their own check red."
echo ""
echo "E2E: PASS — $expected checks green against the real site, and each one watched to fail."
