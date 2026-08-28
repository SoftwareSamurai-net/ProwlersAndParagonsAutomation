# Hosting

Where the running site lives, and why each piece is where it is. The operational
step-by-step for a fresh deploy is [`DEPLOYING.md`](DEPLOYING.md); for turning accounts
on it is [`ACCOUNTS-SETUP.md`](ACCOUNTS-SETUP.md). This is the map that says why those
two documents look the way they do.

## The chain

```
Browser
  │  DNS (softwaresamurai.net, Cloudflare-managed)
  ▼
Cloudflare Pages project — one hostname, superheroes.softwaresamurai.net
  │
  │  Static Blazor WebAssembly app under wwwroot/, uploaded by
  │  .github/workflows/deploy.yml. _redirects sends every unmatched path
  │  to index.html with 200 so a shared link opens the app, not step one.
  │
  ├─ functions/api/[[path]].js  →  worker/index.js
  │     │  Two lines of routing; every /api/* request goes through here.
  │     │  The rules engine does NOT run here — it runs in the browser.
  │     │
  │     ├─ D1 binding: env.DB → database prowlers-and-paragons
  │     │      (accounts, sessions, characters, all as opaque bytes;
  │     │      migrations in d1/migrations/*.sql, wrangler-applied)
  │     │
  │     └─ Env vars (all encrypted):
  │            RESEND_API_KEY  — auth for the mail send
  │            MAIL_FROM       — no-reply@superheroes.softwaresamurai.net
  │            SITE_URL        — https://superheroes.softwaresamurai.net
  │
  └─ Sign-in flow
        Browser → /api/auth/request { email }
                      │
                      ▼
                  worker/mail.js → Resend HTTP API
                      │
                      ▼
                  Resend (external) → SMTP → user's inbox
                      │
                  Magic link → back to /api/auth/consume
```

Nothing here uses AWS, a VPS, a container registry, or a Docker image. One
Cloudflare account plus one Resend account is the whole hosting footprint.

## Why each piece is where it is

### Cloudflare Pages, not a Worker on `workers.dev`

The session cookie is the reason. It is `Secure; HttpOnly; SameSite=Lax` and set
for the origin the API answers on. If the browser reaches the API at a different
origin from the app, that cookie is third-party — and Safari and Chrome's storage
partitioning drops it. Sign-in works everywhere it can be tested and then quietly
fails for real users. Pages Functions run on the same origin as the static site,
which is what stops that from happening.

### D1, not KV or an external database

D1 is SQLite bound directly into the Pages project as `env.DB`. That means real
foreign keys, real indexes and real transactions with no network hop and no
connection string — the schema is expressive enough to say what the invariants
actually are, and the binding removes a whole class of credential management. The
schema is versioned in `d1/migrations/*.sql`; applying them is one wrangler command
against the remote database.

Everything the client sends is an opaque byte string as far as the server is
concerned. The rules engine lives in the browser and is the authority on cost
and legality; the server stores what it is given and hands it back on request.

### Resend, not SES / Postmark / SendGrid / Mailgun

The site sends exactly one kind of message: a magic link. Free tier on Resend
(3k emails/month, 100/day) is comfortably above the volume, the SDK is one HTTP
POST from a Worker with no dependencies, and domain verification is three DNS
records. Everything larger optimises for problems this site does not have.

The domain still has to be verified — SPF and DKIM `TXT` records on
`superheroes.softwaresamurai.net` — because unverified mail claiming to come from that
domain would be dropped or spam-foldered on arrival. Records live in the same
Cloudflare account that runs Pages, so both sides are managed in one place.

**Verification is scoped to the subdomain because the apex belongs to Proton Mail.**
`softwaresamurai.net` carries the owner's personal mail configuration — `MX` records
pointing at `protonmail.ch`, an SPF `TXT`, three DKIM `CNAME`s and `_dmarc` at
`p=quarantine`. Resend's records all land at `superheroes.softwaresamurai.net`, a
different DNS node, so outbound send for this site and inbound personal mail do not
interact at all. The one change that *would* break Proton is a second SPF `TXT` at the
apex: the spec permits one per name and two produce a `permerror` that fails every
message the domain sends. DMARC needs nothing extra — the apex record has no `sp=`
tag, so the subdomain inherits `p=quarantine`, and Resend's DKIM signature for the
verified subdomain aligns with the `From` domain under the default relaxed mode.

Proton itself cannot serve this purpose, for a reason about the runtime rather than
about Proton: a Worker has no usable SMTP client, so the send has to be an HTTP API
call whatever handles the mailbox.

### The rulebook is bundled, not staged

`data/rulebook/ch02-characters.json` is baked into `worker/corpus.js` by
`scripts/inline-rulebook.mjs` and served through a routed function that first
asks who is calling. It is deliberately **not** copied into `wwwroot/` — a file
under `wwwroot/` is a public URL, and no amount of session checking in the
browser would make it not be one. That placement is the whole access control.

The bake exists because both import spellings broke somewhere: `import ... with
{ type: 'json' }` is not understood by wrangler 3.90's bundled esbuild, and
`assert { type: 'json' }` is deprecated in Node 22. The bake sidesteps both,
and the CI runs the deploy's own bundler at the pinned version so a wrangler-
vs-Node parse difference fails the pull request rather than the deploy after
it. That marker comment in `.github/workflows/deploy.yml` is load-bearing.

### The subdomain, not the apex

`superheroes.softwaresamurai.net` rather than `softwaresamurai.net` for the same reason
scoped API tokens beat root ones. A mistake in the Pages config, or a leaked
API token, can only affect one hostname. The apex and everything else on the
domain keeps working.

## How each piece can go wrong

The failure modes worth naming:

- **`DB` binding missing or misnamed.** Every `/api/*` answers 500. Not visible
  from the static site itself; the deploy has a `curl` check that catches it.
- **`SITE_URL` unset.** `/api/auth/request` returns 500 with no email sent. This
  is deliberate: the alternative was deriving origin from the request host,
  which is spoofable on a multi-host deploy — that risk crosses the credential
  boundary (the link carries the raw token), so the server refuses rather than
  guesses.
- **Functions not deployed.** `_redirects` catches every unmatched path with
  `index.html` at 200, so `/api/me` answers with HTML. The client reads that
  as "anonymous", the app runs fine, and sign-in never works. The deploy
  pipeline's `/api/me` check requires 401 + `application/json`.
- **Resend domain not verified.** Emails accepted by Resend, then bounced by
  the recipient's mail server. The endpoint still returns 204 by design — it
  cannot signal whether an address has an account.
- **DNS drifts off Cloudflare.** No custom-domain routing; visitors get the
  registrar's parking page or a connection refused, depending on what changed.

## Cost

- Cloudflare Pages, D1, Workers: free tier. The site is well inside the daily
  limits (100k Worker requests, 5M D1 reads).
- Resend: free tier.
- Domain: annual renewal on `softwaresamurai.net` at Cloudflare Registrar (at
  cost, no markup).

Total operational cost is the domain renewal. Every other line item is $0 at
current scale.

## Where the automation lives

- `.github/workflows/deploy.yml` — the deploy on push to `main`. Direct
  upload, wrangler pinned at 3.90.0. Includes the post-deploy `/api/me` check.
- `.github/workflows/build.yml` — both suites plus a `wrangler pages functions
  build` at the same pinned version, so a bundler incompatibility fails the PR.
- `scripts/inline-rulebook.mjs` — the bake step. Runs in CI before the bundle.
- `scripts/write-cloudflare-headers.sh` — the CSP generator. Runs in both CI
  and the deploy, hashing the current Blazor import map so the policy cannot
  rot silently.
- `d1/migrations/*.sql` — the schema, applied by
  `npx wrangler --cwd d1 d1 migrations apply prowlers-and-paragons --remote`.
