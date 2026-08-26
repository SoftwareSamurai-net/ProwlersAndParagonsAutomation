#!/usr/bin/env bash
# Renders a fixed set of proof pages in headless Chrome and compares each against a committed
# golden PNG, pixel by pixel, with a small tolerance for the kind of jitter that is not a
# regression. Nine browser harnesses in this repository already assert verdicts — sticky,
# narrow, motion, theme, shortcut, insets — and none of them look at a pixel. Four palettes and
# three new screens were being judged by eye. This is the missing tenth.
#
#   ./scripts/visual-regression.sh                  # compare against the committed goldens
#   ./scripts/visual-regression.sh --update-goldens  # regenerate the goldens instead
#
# ------------------------------------------------------------------------------------------------
# THE GOLDENS MUST BE LINUX-RENDERED, NEVER FROM A WINDOWS RUN.
#
# Font hinting and antialiasing differ between Windows' and Linux's text rasterisers, so a golden
# captured by a Windows Chrome would disagree with every pixel of text CI ever renders — not by a
# little, by enough to fail this check on every run forever, on a page nobody touched. So the
# actual screenshot step never runs the host's own Chrome unless the host is already Linux (which
# is what a GitHub Actions runner is): everywhere else — this includes a Windows or macOS
# developer's machine — it runs inside `selenium/standalone-chrome`, a Docker image that ships
# real Google Chrome (not a distro-patched Chromium) on Linux. That is deliberate and is not an
# approximation of "a documented docker run on a Linux image"; it *is* one, invoked automatically
# so nobody has to remember the command. `--update-goldens` goes through the identical path, so
# regenerating them from a Windows machine still writes Linux-rendered PNGs.
#
# (A from-scratch Debian image with `apt-get install google-chrome-stable` was tried first and is
# a fine approach in general — it is what a network with unfiltered access to deb.debian.org
# would want — but the network this was built on mangles Debian's signed release file through
# some proxy in the middle, `Clearsigned file isn't valid, got 'NOSPLIT'`. The Selenium image
# needs no apt access at all: it is pulled already built.)
#
# ------------------------------------------------------------------------------------------------
# NO IMAGE-DIFF PACKAGE IS INSTALLED, ON PURPOSE.
#
# `npm view pixelmatch version` answers fine from here, but this repository has never had a
# `package.json` or a `node_modules` anywhere in it — the accounts server's entire suite runs on
# `node --test` with nothing installed, deliberately, and adding the first npm dependency for a
# CI convenience is a worse trade than ~150 lines of plain Node. `scripts/visual/png.mjs` decodes
# and encodes the one PNG shape Chrome's `--screenshot` produces (8-bit, non-interlaced, RGB or
# RGBA) using only `node:zlib`; `scripts/visual/diff.mjs` walks the two pixel buffers.
#
# ------------------------------------------------------------------------------------------------
# THE TOLERANCE BELOW IS NOT THE WHOLE TOLERANCE — READ diff.mjs's OWN HEADER TOO.
#
# An adversarial audit found that a percentage-of-differing-pixels tolerance alone cannot see a
# colour shift applied uniformly across an entire page — no single pixel's delta ever gets large
# enough to be counted as "differing", however many pixels are nudged the same small amount. So
# diff.mjs now checks two independent measures and fails if *either* is exceeded: this script's
# `--tolerance` still governs the count-of-differing-pixels measure (`--max-diff-percent`), and a
# second, whole-image mean-absolute-channel-difference measure is diff.mjs's own default and is
# not overridden from here — see that file's header comment for the arithmetic behind both
# defaults, and for why a uniform shift and a percentage-of-pixels tolerance need genuinely
# different instruments rather than one number tuned harder.
#
# ------------------------------------------------------------------------------------------------
# --virtual-time-budget IS NOT OPTIONAL, AND NEITHER IS THIS COMMENT'S EXISTENCE.
#
# `.panel` carries `animation: rise var(--enter) both`, which starts at `opacity: 0`. A bare
# `--screenshot` fires before that finishes and captures a washed-out page — which has already
# been misdiagnosed once in this repository as a palette fault. Every screenshot below waits
# 5000ms of virtual time, comfortably past the animation, before Chrome is asked for a frame.
#
# ------------------------------------------------------------------------------------------------
# AND --force-prefers-reduced-motion, BECAUSE 5000ms OF VIRTUAL TIME TURNED OUT NOT TO BE ENOUGH.
#
# The budget above is a *wait* and a wait is a race, so it settles an animation only as reliably as
# the page is fast. It was not enough for `rules-reference`, the page with the most content: two CI
# runs on commits that changed nothing that page renders disagreed on **59.6% of its pixels**, mean
# channel difference 5.622 — and the diff image is unambiguous, three panel interiors solid and the
# headings outside them untouched. That is `.panel`'s entrance caught at two different moments.
#
# **The old tolerance was hiding it.** At `--channel-threshold 24` most of those per-pixel deltas
# did not count as differing at all, so the page read as identical while being flaky; tightening
# the comparator is what surfaced it. A page that is flaky under a strict comparator was always
# flaky — it just had nothing able to say so.
#
# So a golden is captured with motion **off** rather than with a longer guess. The app honours
# `prefers-reduced-motion` by setting its three duration tokens to `0.01ms`, which the motion
# harness proves on every CI run, and `both` on the animation means the resting frame is the same
# frame either way. The capture is therefore the settled page by construction rather than by
# arriving late enough — which is what a golden should have been all along. The budget stays,
# because it also covers font loading and layout, and belt-and-braces costs nothing here.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
wwwroot="$root/web/wwwroot"
goldens_dir="$root/tests/visual-goldens"
work_dir="$root/.visual-regression"
actual_dir="$work_dir/actual"
diff_dir="$work_dir/diff"

# Pinned by digest rather than `:latest`, so a future pull of this image can never silently move
# the pixels these goldens were checked against — the exact failure this whole script exists to
# rule out, just moved from Windows-vs-Linux to today-vs-six-months-from-now. Bump deliberately
# (`docker pull selenium/standalone-chrome:latest && docker inspect ... RepoDigests`) alongside a
# `--update-goldens` run, never as an incidental side effect of an unrelated change.
docker_chrome_image="selenium/standalone-chrome@sha256:cd778b6f38d99d1e14a05a767f576aff2face98d5202a2192d857614c265ec4d"

update_goldens=0
# Matches diff.mjs's own default for --max-diff-percent — see this file's and that file's header
# comments for the arithmetic (tight enough that a 24x24 solid block fails at 1280x900, loose
# enough for a handful of stray antialiased pixels). Kept as a literal here rather than reading
# diff.mjs's default at runtime, so `--tolerance` on the command line has an honest value to
# override *from*.
tolerance=0.02

while [ $# -gt 0 ]; do
  case "$1" in
    --update-goldens) update_goldens=1 ;;
    --tolerance) tolerance="$2"; shift ;;
    -h|--help)
      echo "usage: $0 [--update-goldens] [--tolerance PERCENT]"
      exit 0
      ;;
    *)
      echo "error: unrecognised argument '$1'" >&2
      exit 2
      ;;
  esac
  shift
done

mkdir -p "$actual_dir" "$diff_dir" "$goldens_dir"

# ------------------------------------------------------------------------------------------------
# Regenerate the proof pages. This is plain bUnit rendering — no browser, no antialiasing, just
# component markup written to a file — so it runs identically on any OS and does not threaten the
# Linux-only rule above. Skipped only when the pages are already fresh, so a caller who just ran
# the existing proof-harness step in the same job is not paying to rebuild.
if [ "${PP_PROOF_ALREADY_BUILT:-0}" != "1" ]; then
  echo "Rendering proof pages (PP_PROOF=1 dotnet test tests/ProwlersAndParagons.Web.Tests)..."
  PP_PROOF=1 dotnet test "$root/tests/ProwlersAndParagons.Web.Tests" \
    --configuration Release >/tmp/pp-proof-build.log 2>&1 \
    || { echo "::error::proof-page generation failed:"; tail -60 /tmp/pp-proof-build.log; exit 2; }
fi

# ------------------------------------------------------------------------------------------------
# Pick the Chrome that will actually take the screenshots.
#
# On a Linux host with a Chrome on PATH (a GitHub Actions runner, or a Linux dev machine) this
# runs it directly — no Docker needed, no image to pull, and it is the Chrome CI's own dump-DOM
# harnesses already trust. Everywhere else, `run_chrome` transparently drives the same command
# inside `selenium/standalone-chrome`. Callers below do not know or care which branch ran; both
# take the same arguments and both write to the same host path.
use_docker=1
native_chrome=""
if [ "$(uname -s)" = "Linux" ]; then
  for candidate in google-chrome google-chrome-stable chromium-browser chromium; do
    if command -v "$candidate" >/dev/null 2>&1; then
      native_chrome="$candidate"
      use_docker=0
      break
    fi
  done
fi

if [ "$use_docker" -eq 1 ]; then
  if ! command -v docker >/dev/null 2>&1 || ! docker version >/dev/null 2>&1; then
    cat >&2 <<'EOF'
================================================================================
VISUAL REGRESSION SKIPPED — not run, not passed.

This host is not Linux and has no reachable Docker daemon, so there is no way
to render a page with the Chrome these goldens were made from without risking
a Windows-antialiased screenshot compared against a Linux one — which is
exactly the "fails forever on a page nobody touched" failure this script
exists to prevent. Install Docker Desktop (or run this on Linux, or in CI)
and re-run. This is a skip, not a pass: nothing below was compared.
================================================================================
EOF
    exit 3
  fi
  docker pull --quiet "$docker_chrome_image" >/dev/null
fi

# `flags` beyond the shared ones (extra per-page arguments, e.g. forcing a colour scheme that
# has no explicit `data-theme` page of its own). Empty is a valid value.
run_chrome() {
  local out_host="$1" width="$2" height="$3" page="$4" extra_flags="$5"

  if [ "$use_docker" -eq 0 ]; then
    local profile; profile="$(mktemp -d)"
    "$native_chrome" \
      --headless=new --no-sandbox --disable-gpu --allow-file-access-from-files \
      --hide-scrollbars --user-data-dir="$profile" --virtual-time-budget=5000 --force-prefers-reduced-motion \
      --window-size="${width},${height}" --screenshot="$out_host" $extra_flags \
      "file://$wwwroot/$page" >/dev/null 2>&1 || true
    rm -rf "$profile"
    [ -s "$out_host" ]
    return
  fi

  # The bind mount is the whole reason for MSYS_NO_PATHCONV: without it, Git Bash rewrites the
  # container-side `-v ...:/data` and `-w /data` into Windows paths and Docker refuses them —
  # documented already in scripts/test-worker.sh and scripts/qodana-scan.sh for this same reason.
  # `pwd -W` converts the *host* side; MSYS_NO_PATHCONV stops the *container* side being touched.
  local host_wwwroot; host_wwwroot="$(cd "$wwwroot" && pwd -W 2>/dev/null || pwd)"
  local out_name; out_name="$(basename "$out_host")"

  # **Retried, and this was found by watching it fail rather than assumed.** A container that
  # starts Chrome fine on its own occasionally produces no screenshot when several are launched
  # back to back — a transient contention on this host under Docker Desktop, not a fault in the
  # page or the flags: the identical command succeeds standing alone. A `--rm` container leaves
  # nothing to inspect after the fact, so the only honest response is to run it again rather than
  # to report a rendering failure that was actually a scheduling one. A distinct profile
  # directory per attempt rules out a stale singleton lock as the cause of the retry being needed.
  local attempt
  for attempt in 1 2 3; do
    rm -f "$wwwroot/$out_name"
    MSYS_NO_PATHCONV=1 docker run --rm --user root \
      --entrypoint google-chrome \
      -v "${host_wwwroot}:/data" -w /data \
      "$docker_chrome_image" \
      --headless=new --no-sandbox --disable-gpu --allow-file-access-from-files \
      --hide-scrollbars --user-data-dir="/tmp/pp-chrome-profile-${attempt}" \
      --virtual-time-budget=5000 --force-prefers-reduced-motion \
      --window-size="${width},${height}" --screenshot="/data/${out_name}" $extra_flags \
      "file:///data/${page}" >/dev/null 2>&1 || true

    if [ -s "$wwwroot/$out_name" ]; then
      mv "$wwwroot/$out_name" "$out_host"
      return 0
    fi

    echo "  (attempt $attempt produced no screenshot for $page, retrying)" >&2
  done

  return 1
}

# ------------------------------------------------------------------------------------------------
# The manifest: name : page : width : height : extra Chrome flags.
#
# The four palettes are the four `proof-shell-*` pages TheShell already writes — hero and villain
# each stamp an explicit `data-theme="dark"` variant, so the light pair need no forcing and the
# dark pair need no guessing at `prefers-color-scheme`. The front door has no such dark twin (only
# an explicit *light* one, `-hero-light`), so its dark capture forces the media feature directly;
# `preferredColorScheme=0` was verified against this exact Chrome to mean dark and `=1` light —
# not documented anywhere obvious, so measured directly rather than assumed. The rules reference
# is the third new screen and is deterministic (FakeApi test data, not a live search), so one
# capture of it is a real regression guard rather than a snapshot of whatever happened to be on
# screen.
#
# **The four shell captures were removed once and are back, because the reason they were removed
# has been fixed rather than tolerated.** They had been comparing a page generated by whichever
# Chrome ran `--update-goldens` against whichever Chrome ran the check: off Linux this script uses
# a digest-pinned Docker Chrome, CI uses the runner's own. For these four that difference was not
# noise — `shell-villain-light` disagreed by exactly 32,462 pixels across repeated CI runs,
# unchanged by forcing the colour scheme, while the three below came back pixel-identical on the
# same runs. Dropping them was recorded here as a retreat, with the fix named: generate the
# goldens in CI, from the Chrome that compares them, and commit those.
#
# That is now `.github/workflows/visual-goldens.yml` — `workflow_dispatch` only, because a golden
# regenerated as a side effect of an unrelated change is a regression signed off by nobody. Every
# golden under tests/visual-goldens/ is produced by it, so both sides of every comparison below
# are one renderer and the tolerance no longer has to absorb a Chrome-versus-Chrome difference.
# **So do not regenerate these from a developer machine**: the Docker path this script still
# carries is for *looking* at a page locally, and a golden written by it would reintroduce exactly
# the cross-renderer gap that removed these four in the first place.
#
# **And that gap now has a cause rather than a pixel count.** Running this script locally, six of
# the seven pages come back pixel-identical against the CI-rendered goldens and `shell-villain-light`
# does not — 35,410 pixels, in one band at (622,835)-(1166,899). Sampling it says what it is:
#
#     panel interior elsewhere on the page   255,253,249   (--surface)
#     page ground elsewhere on the page      248,243,236   (--bg)
#     the differing band, CI's Chrome        255,253,249   -> panel
#     the differing band, Docker's Chrome    248,243,236   -> ground
#
# The last panel's bottom edge lands a few pixels apart in the two renderers, and on this one page
# it falls inside the final 65 rows of a 900px viewport — so a sub-pixel layout difference flips a
# whole band from panel to ground. Not antialiasing, and nothing to do with the palette, which is
# why forcing the colour scheme never moved it. **This is the original 32,462-pixel disagreement,
# measured instead of guessed at.**
#
# It is left alone deliberately. CI is the authority, CI's goldens are what is committed, and CI is
# green; what a local run gets is one known page of seven disagreeing for a understood reason. The
# lever, if somebody wants to close it, is the capture height — put the boundary somewhere other
# than the viewport edge. **Judge that in CI and not here**: the height has been changed once
# already and reverted (`d0839ad`, "the flake was local, not CI"), and it would invalidate all
# seven goldens, so it costs a CI round trip to evaluate and may simply move the knife edge.
#
# One thing to expect when looking at the two dark goldens rather than to be alarmed by: the
# light/dark control in them reads AUTO, not DARK. The page stamps data-theme="dark" on the root so
# the palette is unambiguous, while the control's pressed state comes from the component's own
# default in a bUnit render, which nothing here sets. A fidelity gap in the harness, not a fault in
# the app, where choosing dark does both. These goldens are here for the palette.
#
# **Nothing in the string below is a comment.** It is double-quoted, so a `#` line in it is still
# parsed as a manifest row, and backticks in it are command substitution — a note written inside
# it ran `data-theme="dark"` as a command. Notes go here, above it.
manifest="
front-door-hero-light:proof-front-door-hero-light.html:1280:900:
front-door-hero-dark:proof-front-door-hero.html:1280:900:--blink-settings=preferredColorScheme=0
rules-reference:proof-rules-hero.html:1280:900:
shell-hero-light:proof-shell-hero.html:1280:900:--blink-settings=preferredColorScheme=1
shell-hero-dark:proof-shell-hero-dark.html:1280:900:
shell-villain-light:proof-shell-villain.html:1280:900:--blink-settings=preferredColorScheme=1
shell-villain-dark:proof-shell-villain-dark.html:1280:900:
"

failed=0
missing_golden=0

while IFS= read -r line; do
  [ -z "$(echo "$line" | tr -d '[:space:]')" ] && continue
  name="$(echo "$line" | cut -d: -f1)"
  page="$(echo "$line" | cut -d: -f2)"
  width="$(echo "$line" | cut -d: -f3)"
  height="$(echo "$line" | cut -d: -f4)"
  extra="$(echo "$line" | cut -d: -f5-)"

  if [ ! -f "$wwwroot/$page" ]; then
    echo "::error::$name: $page was not written by the proof harness — check ProofPages.cs"
    failed=1
    continue
  fi

  actual="$actual_dir/$name.png"
  rm -f "$actual"
  run_chrome "$actual" "$width" "$height" "$page" "$extra" || true

  if [ ! -s "$actual" ]; then
    echo "::error::$name: Chrome produced no screenshot for $page after retrying"
    failed=1
    continue
  fi

  if [ "$update_goldens" -eq 1 ]; then
    cp "$actual" "$goldens_dir/$name.png"
    echo "updated  $name -> tests/visual-goldens/$name.png"
    continue
  fi

  golden="$goldens_dir/$name.png"
  if [ ! -f "$golden" ]; then
    # A missing golden is not a pass and must not read as one — this is the exact failure shape
    # CLAUDE.md names four times over: a check satisfied by there being nothing to check.
    # And the instruction points at CI rather than at this script's own --update-goldens, which
    # off Linux writes a Docker-Chrome golden that CI's Chrome then has to agree with — the
    # cross-renderer gap that cost this check four pages once already.
    echo "::error::$name: NO GOLDEN at tests/visual-goldens/$name.png — generate it in CI with " \
         "'gh workflow run visual-goldens.yml --ref <branch>', then download and commit the " \
         "artifact. Do not write this one from a developer machine."
    missing_golden=1
    continue
  fi

  if ! node "$root/scripts/visual/diff.mjs" "$actual" "$golden" \
      --out "$diff_dir/$name.png" --max-diff-percent "$tolerance"; then
    failed=1
  fi
done <<< "$manifest"

if [ "$update_goldens" -eq 1 ]; then
  echo ""
  echo "Goldens written under tests/visual-goldens/. Review them (they are real PNGs) and commit."
  exit 0
fi

if [ "$missing_golden" -eq 1 ]; then
  echo ""
  echo "::error::one or more pages have no golden to compare against — see above. This is a" \
       "SKIP dressed as a failure on purpose: a missing golden must stop the build, not pass it."
  exit 1
fi

if [ "$failed" -eq 1 ]; then
  echo ""
  echo "::error::visual regression found a pixel difference over tolerance — see the diff PNGs" \
       "under .visual-regression/diff/"
  exit 1
fi

echo ""
echo "All pages match their goldens within ${tolerance}%."
