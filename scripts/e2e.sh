#!/usr/bin/env bash
# Drives the assembled application: the real published site, served by the real `wrangler pages
# dev`, in real Chrome, over the DevTools Protocol.
#
#   ./scripts/e2e.sh                 # publish, serve, drive, and drive every twin
#   ./scripts/e2e.sh --real-only     # skip the negative controls (NOT a full run — see below)
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
#   - **Every check must have a twin.** The check names the real run reported and the check names
#     the twins cover are compared, and a mismatch fails this script. Adding a sixth check without
#     a negative control is a red run rather than a silence.
#
# `--real-only` exists for the ten-second local loop while a check is being written. It prints
# that the run proved nothing about whether the checks can fail, and CI never passes it.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# ------------------------------------------------------------------------------------------------
# Where everything lives.
#
# **`wrangler pages dev` is run from `$work`, and that placement is the configuration.** Wrangler
# bundles a `functions/` directory found in the *working directory* — there is no flag for it — so
# running it from the repository root would bundle the accounts API, which needs a D1 binding this
# stage deliberately does not have. From here there is no `functions/` to find, wrangler says
# "No Functions. Shimming...", every `/api/` address falls through `_redirects` to `index.html`,
# and the app reads an unparseable answer as "anonymous" — which is exactly what stage one wants
# and is the app's own documented behaviour rather than a special case for this harness.
#
# **And the site directories are named relative to `$work` on purpose.** Wrangler is Node, so it
# cannot read a Git Bash path like `/c/Users/...`; passing `site` from inside `$work` needs no
# conversion and therefore cannot be got wrong on one platform and not the other.
work="$root/.e2e"
site="$work/site"
twins="$work/twins"
logs="$work/logs"

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

# The command that drives one site. Overridable **only** as a test seam, the same reason as
# `PP_CHROME_BIN` in scripts/visual-regression.sh and `WRANGLER_BIN` in
# scripts/apply-migrations.sh: a driver that never returns cannot be arranged with the real one,
# and a deadline that has never been watched to fire is a claim rather than a guard.
#
# **An array and not a string, which is where `WRANGLER_BIN`'s shape does not carry over.** That
# one is `npx --yes wrangler@<version>` and word-splits safely because it contains no path. This
# repository's own checkout is under "Personal Projects", so a `$driver` left to split would tear
# the script's path in half at the space and run `node` against a directory that does not exist.
if [ -n "${PP_E2E_DRIVER:-}" ]; then
  read -r -a driver <<< "$PP_E2E_DRIVER"
else
  driver=(node "$root/scripts/e2e/drive.mjs")
fi

real_only=0

while [ $# -gt 0 ]; do
  case "$1" in
    --real-only) real_only=1 ;;
    -h|--help)
      echo "usage: $0 [--real-only]"
      echo ""
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

# The first port at or after `$1` that nothing is listening on.
#
# **Occupied ports are skipped, never cleared.** An earlier version killed whatever held the port
# it wanted, which is a fine way to end somebody's unrelated dev server on the same number.
next_free_port() {
  local port="$1"

  while [ -n "$(listeners_on "$port")" ]; do
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
    local holders
    holders="$(listeners_on "$port")"
    [ -z "$holders" ] && return 0

    local pid
    for pid in $holders; do
      if on_windows; then taskkill //F //T //PID "$pid" >/dev/null 2>&1 || true
      else kill -9 "$pid" >/dev/null 2>&1 || true
      fi
    done

    sleep 1
  done

  echo "::warning::something is still listening on port $port after 30s of asking it not to."
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
    # Children first: killing the wrapper alone can leave wrangler running.
    pkill -P "$pid" >/dev/null 2>&1 || true
  fi

  kill "$pid" >/dev/null 2>&1 || true
  wait "$pid" 2>/dev/null || true

  # The backstop, not the mechanism. If the tree kill above missed something, this names it.
  release_port "$port"
}

trap stop_server EXIT INT TERM

# start_server <directory-relative-to-$work> <port> <log-name>
start_server() {
  local dir="$1" port="$2" name="$3"
  local log="$logs/$name.log"

  server_port="$port"

  (
    cd "$work" || exit 1
    CI=1 WRANGLER_SEND_METRICS=false CLOUDFLARE_API_TOKEN='' \
      exec npx --yes "wrangler@${wrangler_version}" pages dev "$dir" \
        --ip 127.0.0.1 --port "$port" > "$log" 2>&1
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
start_server site "$port" real || exit 1

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

# `name:CHECK` per line.
mapfile -t defect_lines < <(node "$root/scripts/e2e/defects.mjs" --list)

if [ "${#defect_lines[@]}" -eq 0 ]; then
  echo "::error::defects.mjs listed no twins, so there is no negative control for anything."
  exit 1
fi

# **Every check must have a twin, and this is the guard that says so.** The names the real run
# reported and the names the twins cover have to be the same set; a sixth check added without a
# negative control fails here rather than joining the suite unproven.
checks_run=$(grep -o '^E2E CHECK [A-Z][A-Z_]*' "$real_log" | sed 's/^E2E CHECK //' | sort -u)
checks_twinned=$(printf '%s\n' "${defect_lines[@]}" | cut -d: -f2 | sort -u)

if [ "$checks_run" != "$checks_twinned" ]; then
  echo "::error::the checks the driver runs and the checks the twins cover disagree."
  echo "::error::  driven:  $(echo "$checks_run" | tr '\n' ' ')"
  echo "::error::  twinned: $(echo "$checks_twinned" | tr '\n' ' ')"
  echo "::error::A check with no twin has never been watched to fail. Add one to"
  echo "::error::scripts/e2e/defects.mjs, or remove the check."
  exit 1
fi

twin_failures=0

for line in "${defect_lines[@]}"; do
  name="${line%%:*}"
  check="${line##*:}"

  echo ""
  echo "--- twin '$name' — $check must fail ---"

  # Throws, loudly, if the one documented line it substitutes has moved.
  node "$root/scripts/e2e/defects.mjs" --build "$site" "$twins/$name" "$name" || {
    echo "::error::twin '$name' could not be built — see above. Until it can be, $check has no"
    echo "::error::negative control and has not been watched to fail."
    exit 1
  }

  # A port of its own rather than the one just released: nothing then depends on how quickly the
  # previous server let go of it, and a socket in TIME_WAIT cannot be read as this server.
  port="$(next_free_port "$((port + 1))")"
  start_server "twins/$name" "$port" "twin-$name" || exit 1

  twin_log="$logs/twin-$name-drive.log"
  twin_status=0
  timeout -k 10s "$drive_deadline" "${driver[@]}" "http://127.0.0.1:${port}" \
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
      | sed 's/^E2E CHECK [A-Z][A-Z_]*: FAIL — //')"
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
  echo "::error::$twin_failures of ${#defect_lines[@]} twins did not turn their check red."
  exit 1
fi

echo "All ${#defect_lines[@]} twins turned their own check red."
echo ""
echo "E2E: PASS — $expected checks green against the real site, and each one watched to fail."
