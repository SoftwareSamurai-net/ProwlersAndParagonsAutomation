# Hosting and deployment

Read before touching `.github/workflows/deploy.yml`, `web/wwwroot/_headers`, `_redirects`, or the content security policy.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## Hosting

Cloudflare Pages at `superheroes.softwaresamurai.net`, by `.github/workflows/deploy.yml` on push to `master`. Direct upload, not Cloudflare's Git integration — two deploy paths can disagree.

- **The deploy workflow must never trigger on `pull_request`.** That trigger runs a contributor's workflow file with the base repository's secrets in scope, which puts the Cloudflare token one PR away from anyone. Adding it would be the single most damaging change available in this repository.
- **`_headers` is generated, never hand-edited.** `scripts/write-cloudflare-headers.sh` hashes the inline import map Blazor writes into `index.html`, whose contents change whenever the framework assets are re-fingerprinted — a hard-coded hash would rot silently and stop the app booting on some later deploy. The script exits non-zero if it finds no inline script rather than shipping a policy that would break the site, and CI runs it too, so a broken policy fails on the PR.
- `style-src` needs `'unsafe-inline'` because the budget bar's width is a live inline style attribute. `script-src` does **not**, and should not gain it.
- **The edge injects scripts into the HTML after the policy has been written, so no hash here can ever cover one.** Two do it. The Web Analytics beacon is allowed by host — `https://static.cloudflareinsights.com` in `script-src`, and `connect-src` stays `'self'` because automatic setup on a proxied domain reports to this site's own `/cdn-cgi/rum`. Cloudflare's **Precursor** bot script is not allowed and is blocked in the console: its inline body carries a per-request token, so its hash differs on every response — two loads of the same page gave two hashes — and Cloudflare's own answer is a CSP nonce, which a static `_headers` file cannot mint. **Do not reach for `'unsafe-inline'` for it**; with a hash present browsers ignore `'unsafe-inline'` entirely, so it would do nothing unless the import-map hash were deleted too, which is the whole policy. Turn Precursor off in the dashboard (Security → Settings), and check Bot Fight Mode beside it — that force-enables JavaScript Detections, which injects the same way and cannot be turned off separately.
- `web/wwwroot/_redirects` sends every path to `index.html` with a **200**, not a redirect: a 302 would drop the path and land every shared link on step one.
- `<base href="/">` assumes a root path. A subdomain is fine; a subpath is not, and getting it wrong breaks every asset fetch at once.


