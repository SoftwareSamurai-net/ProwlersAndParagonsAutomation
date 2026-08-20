# Turning accounts on

The code is deployed by the ordinary pipeline; **the account system does nothing until five
things exist that only the owner of the Cloudflare account can create.** Until then the site
behaves exactly as it did before: every visitor is anonymous, the character lives in their
browser, and the rulebook reader shows nothing. That is deliberate — see
[the seam](#what-happens-before-any-of-this-is-done).

Everything below is done once, by hand, by somebody signed in to Cloudflare and to a mail
provider. **Nothing here can be automated from this repository and nothing here is committed:**
a secret in a file is a secret in every fork and every clone.

Two things worth having open before you start:

- The Cloudflare dashboard: **[dash.cloudflare.com](https://dash.cloudflare.com/)**. The Pages
  project this repository deploys to lives under **Workers & Pages** in the left sidebar; the D1
  database you are about to create lives under **Storage & Databases → D1 SQL Database** in the
  same sidebar. Both are in the same account and the same sidebar; you never leave the dashboard
  for steps 1, 3 and most of 4.
- A terminal in this repository. Steps 1 and 2 run `npx wrangler`, which asks you to sign in to
  Cloudflare in a browser the first time. That is the only time this repository sees Cloudflare.

---

## 1. A database

**In a terminal in this repository:**

```bash
npx wrangler login
npx wrangler d1 create prowlers-and-paragons
```

The first command opens Cloudflare in a browser and asks you to authorise wrangler. The second
prints a block that ends with a `database_id` — copy it.

**Put it in [`d1/wrangler.toml`](../d1/wrangler.toml)**, replacing the `REPLACE-ME` placeholder,
and commit that file. The id names a database and grants nothing, so it is an identifier rather
than a credential.

You can also see the database exists by clicking through the dashboard: **Workers & Pages →
Storage & Databases → D1 SQL Database**. `prowlers-and-paragons` will be in the list.

## 2. The tables

**In a terminal, from the repository root:**

```bash
npx wrangler --cwd d1 d1 migrations apply prowlers-and-paragons --remote
```

`--cwd d1` is what makes wrangler read `d1/wrangler.toml`; the migrations are the `.sql` files
beside it. Leave `--remote` off to apply them to a local copy instead. You will be asked to
confirm each migration; the tables it creates are `users`, `login_tokens`, `sessions`,
`login_attempts` and `characters`.

**To sanity-check from the dashboard:** D1 SQL Database → the `prowlers-and-paragons` database →
**Tables** tab. All five should be there and empty.

**The schema is the thing the tests run against**, in real SQLite, so a migration that would not
apply fails on the pull request rather than here.

## 3. Bind the database to the site

**In the dashboard**, click through:

**Workers & Pages → your Pages project (`prowlers-and-paragons`) → Settings tab → Bindings
section**

Then **Add binding → D1 database** and fill in:

| Field | Value |
|---|---|
| Variable name | `DB` |
| D1 database | `prowlers-and-paragons` |
| Environment | Production |

Add a second binding under the same variable name for **Preview** if you want previews to work —
same D1 database, same variable name, environment set to Preview.

**The name has to be exactly `DB`.** The server reaches for `env.DB`; a binding under any other
name means every request answers 500, and nothing on the deploy says so.

> A direct-upload Pages project takes its bindings from the project, not from a `wrangler.toml`
> in the repository. That is why this is a dashboard step and why `d1/wrangler.toml` is in a
> subdirectory — a root one would change how `wrangler pages deploy` behaves, which cannot be
> rehearsed without these credentials.

## 4. A way to send mail

Two providers are involved and it does not matter which you do first, but you cannot skip either.
The domain is the one the site is served from — **`pp.softwaresamurai.net`** if you have the
custom domain attached, else the `*.pages.dev` address will not work because **`pages.dev` cannot
send mail**. Attach the custom domain first if it is not attached yet: **Workers & Pages → your
Pages project → Custom domains tab → Set up a custom domain**.

### 4a. Resend

Sign in to **[resend.com](https://resend.com)**. Then:

1. **Domains → Add Domain →** enter `pp.softwaresamurai.net`. Resend shows you SPF, DKIM and an
   MX record to add. Add them to that domain's DNS in the **Cloudflare dashboard**, under
   **Websites → `softwaresamurai.net` → DNS → Records**. Wait for Resend to verify (usually a
   few minutes; the row goes green).
2. **API Keys → Create API Key →** name it something like *Prowlers and Paragons production*,
   permission **Sending access**. Copy the key it shows once — you cannot see it again.

### 4b. Three variables on the Pages project

**In the Cloudflare dashboard:**

**Workers & Pages → your Pages project → Settings tab → Environment variables**

Add three, all for **Production** and **encrypted** (click the encrypt checkbox — the Resend key
is a real credential):

| Name | Value |
|---|---|
| `RESEND_API_KEY` | the key Resend gave you |
| `MAIL_FROM` | `no-reply@pp.softwaresamurai.net` — or any address on the verified domain |
| `SITE_URL` | `https://pp.softwaresamurai.net` — no trailing slash |

**All three are required, `SITE_URL` included.** It is what sign-in links point at, and the server
**refuses to send one at all** without it — `/api/auth/request` answers 500 and writes nothing.

That refusal is deliberate and replaced a fallback. The link used to be addressed from the origin
of the request, which is derived from the host it arrived on — and the CSRF check compares the
`Origin` header *against that host* rather than validating the host itself. So on a deployment
where more than one hostname routes to the Function (a Pages preview alias, a custom domain
mid-change), a caller who could influence the effective host received a link minted for it. The
link carries the raw token, because the token *is* the credential. A misconfigured deployment that
refuses costs one clear error in the logs; one that guesses costs somebody their account.

## 5. Redeploy so the new variables take effect

**A binding or environment variable added after the last deploy does not appear until the next
deploy.** Two ways:

- Push any change to `master` (a whitespace edit committed and pushed is enough).
- Or in the dashboard: **Workers & Pages → your Pages project → Deployments tab →** find the
  most recent deployment, click the `⋯` menu on the right, **Retry deployment**.

The deploy itself fails unless `/api/me` answers **401 carrying JSON**, so this step is also the
verification: a broken configuration turns the deploy red rather than shipping quietly.

---

## Checking it worked

The deploy does two of these for you and fails if either is wrong:

- `functions/api/[[path]].js` exists where wrangler looks for it, before uploading.
- `/api/me` answers **401 with JSON** afterwards.

That second one is the check worth understanding. **A site with no accounts API looks completely
healthy**: `_redirects` sends every unmatched path to `index.html` with a 200, so `/api/me`
answers with a page of HTML, the app reads that as "nobody is signed in", and everything works
except signing in — for ever, silently, exactly as designed. The client is built that way on
purpose so a missing server is a missing feature rather than a blank page, which means the
deploy is the only place the mistake is ever visible.

By hand:

```bash
curl -i https://pp.softwaresamurai.net/api/me
```

- **401** and `content-type: application/json` — working, nobody signed in.
- **200** and `content-type: text/html` — the functions did not deploy. Step 3, or `functions/`
  was not bundled.
- **500** — the functions deployed and the database did not. Step 1 to 3, and check the binding
  is named `DB`.

Then sign in for real: the page is at `/signin`. If the link never arrives, **Resend → Logs**
shows every send it accepted or refused, and the server deliberately answers 204 whether or not
it sent one — so the site cannot tell you, by design.

---

## Raising one account's character limit

Every account holds up to `users.character_limit` characters — **5** unless changed. Raising it
for one address, say to make it a GM account, is one statement against the real database:

```bash
npx wrangler --cwd d1 d1 execute prowlers-and-paragons --remote \
    --command "UPDATE users SET character_limit = 25 WHERE email = 'someone@example.test';"
```

Leave `--remote` off to run it against a local copy instead, same as the migrations above.

**Or in the dashboard:** D1 SQL Database → `prowlers-and-paragons` → **Console** tab → paste the
`UPDATE` statement and run it. Same effect; useful when you are already in the dashboard for
something else.

**This is deliberately not self-service and there is no endpoint for it** — see
[`docs/CHARACTERS-API.md`](CHARACTERS-API.md). A cap somebody can raise on themselves is not a
cap, so the only way to raise one is this command, run by hand by whoever administers the
database.

---

## What happens before any of this is done

Nothing breaks, and that is the property worth keeping:

- `Accounts` answers `Identity.Anonymous` for every failure — no network, a 401, or a page of
  HTML with a 200. Identity is asked for **before the first render**, so anything else would be
  a blank page rather than a missing feature.
- `AccountCharacterStore` then routes every character to the browser's own local storage, under
  the historical key `pp.character.v1`.
- `RulebookReader` answers null, and `RulebookEntry` renders nothing at all.
- **The character manager works locally too.** A visitor with no account gets the list, the
  Open/Discard rows, the *Start a new character* button and the import affordance, all in this
  browser. The cap is not enforced — local storage has no limit worth imposing — and the aside
  reads *In this browser* rather than *3 of 5*.

There is a test for each, and one that asserts all three together: `AccountTests`'
`NoServerMeansAnAnonymousVisitorRatherThanABlankPage`.

## What is deliberately not here

- **No password, anywhere.** Proving you can read the address is the whole of the check, so
  there is nothing to store, nothing to leak and nothing to reset.
- **No token in the browser.** The session is an `HttpOnly` cookie, so the WebAssembly app never
  holds a credential and an injected script cannot read one.
- **No third-party identity provider.** Nothing about who somebody is leaves the account the
  site is already deployed to.
- **No self-service for the cap.** `users.character_limit` is set by hand — see the section
  above. A cap somebody can raise on themselves is not a cap, and `Identity` deliberately does
  not carry a role field either, so "am I a GM" is not a question the client can ask.
- **No rules on the server.** The engine runs in the browser and is the authority on what a
  character costs and whether it is legal. The server stores bytes it never parses.
