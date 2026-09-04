#!/usr/bin/env bash
# Installs what this repository's own scripts need and cannot run without.
#
# `dotnet test` is the only thing that runs on the .NET SDK alone. Eight of the twelve scripts
# under scripts/ need Node; two of those eight also want Docker or a real Chrome, and for a
# specific, measured reason each — see the two sections below rather than installing either
# blind. This script's job is only to get those binaries onto PATH at the versions this
# repository already pins elsewhere; it reads every version rather than restating one, for the
# same reason build.yml's dry-run reads deploy.yml's wrangler pin instead of hardcoding it: a
# restated version is a second place to keep in step, and it will not be kept.
#
# macOS with Homebrew only. Linux CI already has Node and Chrome preinstalled by the runner
# image; a Windows machine is covered by the `winget` line each script already prints when it
# cannot find what it needs. A third install path here would be a third thing to keep working.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

if [ "$(uname -s)" != "Darwin" ]; then
    echo "error: this script installs via Homebrew and only runs on macOS." >&2
    echo "       Linux: Node and Chrome are already on the CI runner image; nothing to do." >&2
    echo "       Windows: each script under scripts/ prints its own winget command when it" >&2
    echo "       cannot find what it needs — run the script you want and follow that message." >&2
    exit 1
fi

if ! command -v brew >/dev/null 2>&1; then
    echo "error: Homebrew is not installed. See https://brew.sh." >&2
    exit 1
fi

echo "== .NET SDK =="

# global.json is the one place this version lives; read it rather than restating it, same
# reasoning as the wrangler pin below.
dotnet_pinned="$(sed -n 's/.*"version": *"\([0-9.]*\)".*/\1/p' global.json | head -1)"
[ -n "$dotnet_pinned" ] || { echo "error: could not read the SDK version out of global.json." >&2; exit 1; }

if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q "^${dotnet_pinned%.*}"; then
    echo "  dotnet SDK $(dotnet --list-sdks | grep "^${dotnet_pinned%.*}" | tail -1 | cut -d' ' -f1) already installed."
else
    echo "  Installing .NET SDK ${dotnet_pinned} (global.json rollForward: latestMinor covers a"
    echo "  later patch of the same 10.0 line)."
    brew install --cask dotnet-sdk
fi

echo
echo "== Node =="

# Every script under scripts/ that needs Node checks for 22+ itself and falls back to Docker
# if it is missing — this just gets a real one onto PATH so that fallback never has to fire.
# The floor is the same one test-worker.sh states: node:sqlite and JSON import attributes.
node_floor=22

if command -v node >/dev/null 2>&1 && [ "$(node --version | sed 's/^v\([0-9]*\).*/\1/')" -ge "$node_floor" ]; then
    echo "  Node $(node --version) already on PATH."
else
    echo "  Installing Node ${node_floor} (brew install node@${node_floor}; not the default"
    echo "  'node' formula, so it is force-linked onto PATH explicitly)."
    brew install "node@${node_floor}"
    brew link --force --overwrite "node@${node_floor}"
fi

echo
echo "== wrangler, at the version deploy.yml actually deploys with =="

# The identical sed deploy.yml's own dry-run step in build.yml runs, so this can never read a
# different answer than CI does. See build.yml's comment above that line for why a fallback
# version was the actual hazard here, not the missing one.
wrangler_pinned="$(sed -n 's/^.*wrangler-action@v[0-9][0-9.]*.*# wrangler=\([0-9.]*\).*$/\1/p' \
    .github/workflows/deploy.yml)"
[ -n "$wrangler_pinned" ] || {
    echo "error: could not read a wrangler version out of deploy.yml's" >&2
    echo "       'uses: cloudflare/wrangler-action@…  # wrangler=<version>' line." >&2
    exit 1
}

echo "  deploy.yml pins wrangler@${wrangler_pinned}."
echo "  Nothing to install here: every script that shells out to wrangler runs it via"
echo "  'npx wrangler@${wrangler_pinned}', which npx fetches on first use and caches after."
echo "  Confirm now, or skip and let the first real script call do it:"
echo "    npx --yes wrangler@${wrangler_pinned} --version"

echo
echo "== Chrome, for the two checks that need pixel-identical rendering =="

# **This is the one thing here that Docker is not a fallback for.** docs/guide/testing.md
# records the measurement: even two different *real Linux* Chromes — the digest-pinned
# selenium/standalone-chrome image and ubuntu-latest's own — disagreed by 32,462 pixels on one
# proof page. A macOS-native Chrome disagrees with the Linux goldens far more than that, so
# installing it here does not make visual-regression.sh's comparison trustworthy — it cannot
# be, off Linux, at all. What it is for instead:
#
#   - scripts/e2e.sh drives the assembled app over the DevTools Protocol. It is not comparing
#     pixels, so any real Chrome answers the same DOM and network questions Linux Chrome would.
#   - Looking at a proof page yourself, which visual-regression.sh's own comment calls out as
#     the Docker path's actual local use: "for *looking* at a page locally."
#
# The goldens themselves must still come from .github/workflows/visual-goldens.yml on the
# runner — see testing.md — and nothing here changes that.
if [ -d "/Applications/Google Chrome.app" ]; then
    echo "  Google Chrome already installed."
else
    brew install --cask google-chrome
fi

echo
echo "== Docker: optional, and only for two things =="
echo "  Not installed by this script — Docker Desktop wants a license click-through and,"
echo "  on first run, a privileged helper; that is a decision to make once, not a default."
echo
if command -v docker >/dev/null 2>&1 && docker version --format '{{.Server.Version}}' >/dev/null 2>&1; then
    echo "  Docker is already running ($(docker version --format '{{.Server.Version}}'))."
else
    echo "  Without it, two scripts are unavailable rather than degraded:"
    echo "    - qodana-scan.sh always needs it; jetbrains/qodana-cdnet has no non-container form."
    echo "    - visual-regression.sh needs it for the pixel comparison specifically (see above);"
    echo "      it still runs everything else without Docker if PP_CHROME_BIN or a Linux native"
    echo "      Chrome is found, on this machine neither applies."
    echo "  Install from https://www.docker.com/products/docker-desktop/ if you want either."
fi

echo
echo "Done. Open a new shell (or 'hash -r') so PATH changes take effect, then:"
echo "  dotnet test                # the engine and the two .NET-hosted suites"
echo "  ./scripts/test-worker.sh   # the accounts API, now on local Node instead of Docker"
echo "  ./scripts/e2e.sh           # drives the assembled app in the Chrome just installed"
