# Deploying the browser front end


The site is **live on Cloudflare Pages** at [superheroes.softwaresamurai.net](https://superheroes.softwaresamurai.net), with the `*.pages.dev` fallback at [prowlers-and-paragons-chargen.pages.dev](https://prowlers-and-paragons-chargen.pages.dev). Deployed by [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) on every push to `master` that touches the app, the engine, the rules or the deploy itself.

There is no build step on Cloudflare's side: the workflow runs `dotnet publish`, writes the security headers, and uploads the result.

**There *is* a server-side component, and this document used to say there was not.** The character generator needs none — it runs entirely in the browser — but the accounts half is Cloudflare Pages Functions: one routed file, `functions/api/[[path]].js`, and everything it imports from `worker/`. That is what serves sign-in, saved characters, the rulebook reader and the recorded conversations, and it is why the deploy has a `curl` check that `/api/me` answers **JSON** rather than the app's own `index.html`. See [`ACCOUNTS-SETUP.md`](ACCOUNTS-SETUP.md).

**`wrangler pages deploy <dir>` bundles a `functions` directory found in the working directory, not in the directory being uploaded.** There is no flag for it; the placement *is* the configuration, and getting it wrong deploys a healthy-looking site that signs nobody in.

### One-time setup

1. **Create the Pages project.** Cloudflare dashboard → Workers & Pages → Create → Pages → *Direct Upload*. Name it `prowlers-and-paragons`, or set a repository variable `CLOUDFLARE_PAGES_PROJECT` to whatever you called it. Do **not** connect it to the Git repository — the workflow uploads, and having both would give you two deploy paths that can disagree.
2. **Create a scoped API token.** My Profile → API Tokens → Create Token → *Custom token*:
   - Permission: **Account → Cloudflare Pages → Edit**, and nothing else.
   - Account Resources: **only** the account holding this project.
   - Not the Global API Key, which can do anything to every zone on the account.
3. **Add the repository secrets** `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` (Settings → Secrets and variables → Actions).
4. **Deploy once and check it before touching DNS.** `gh workflow run deploy.yml --ref master`, then open the `*.pages.dev` URL. Attaching the domain first means debugging the site and the DNS at the same time.
5. **Attach the custom domain.** Pages project → Custom domains → `superheroes.softwaresamurai.net`. Cloudflare creates the CNAME itself. It serves the **production** deployment, so get step 4 green first.

You do **not** need to line the project's production branch up with this repository's.

That deserves a word, because it is a trap the first deploy fell into. Wrangler's `--branch` is a **label Cloudflare compares against the project's configured production branch** — not a branch it reads. If the two differ, the deploy lands as a *preview*: it succeeds, prints a `<branch>.<project>.pages.dev` alias, the workflow goes green, and the production URL and any custom domain answer **404**, with nothing in the logs to say why. New projects default to `main`; this repository is `master`.

So the workflow asks the project what it calls production and deploys to that, and then checks the production hostname actually serves a page before it will pass. The mismatch cannot happen, and if the site is somehow still not up, the deploy fails instead of reporting success.

### Notes on keeping it safe

The site is static, has no backend, no accounts and no cookies, and nothing a visitor types leaves their browser — so there is very little to attack. What is worth getting right is the blast radius around it:

- **A subdomain, not the apex.** The Pages project answers for `superheroes.softwaresamurai.net` only. Nothing about it touches routing for the rest of the domain, and a mistake in the Pages config cannot take the apex down with it.
- **The API token is scoped to Pages on one account.** If it ever leaked, the worst it can do is redeploy this one site. Rotate it in the Cloudflare dashboard and update the secret; nothing in the repository holds a copy.
- **The workflow never runs on `pull_request`.** That trigger would execute a contributor's workflow changes with the token in scope. Deploys happen only from `master`, after a merge.
- **Security headers ship with the site**, generated into `_headers` by [`scripts/write-cloudflare-headers.sh`](../scripts/write-cloudflare-headers.sh) and applied by Cloudflare to every response: a Content-Security-Policy that permits scripts from this origin and, for the Web Analytics beacon Cloudflare injects at the edge, from `static.cloudflareinsights.com` — plus `nosniff`, `Referrer-Policy: no-referrer`, `frame-ancestors 'none'` and a `Permissions-Policy` that turns off every device API the app does not use.

  The CSP is generated rather than written by hand because Blazor emits an inline import map into `index.html` whose contents change whenever the framework assets are re-fingerprinted. A hard-coded hash would rot silently and take the site down on some later deploy; the script hashes whatever was actually published, and fails the build rather than shipping a policy that would stop the app booting. `style-src` still needs `'unsafe-inline'` — the budget bar's width is a live number and arrives as an inline style attribute.

  Both CI and the deploy run the same script, so a change that breaks the policy fails on the pull request.
- **Your Pages project also answers on its `*.pages.dev` address.** That is public. If you would rather only friends reach it, put Cloudflare Access (Zero Trust) in front of the project — it covers both hostnames.

To check the policy locally, publish and generate the headers, then serve the result with any static host that applies them:

```bash
dotnet publish web/ProwlersAndParagons.Web.csproj -c Release -o publish && ./scripts/write-cloudflare-headers.sh publish/wwwroot
```

### One thing to know before you look at the network tab

The first load is about **27 MiB uncompressed** (roughly a third of that over the wire, since Cloudflare applies Brotli), and it is cached hard afterwards because every framework asset is fingerprinted. It is that large because IL trimming is disabled — see [PROGRESS.md](../PROGRESS.md) for why, and for what closing it would take.

---

