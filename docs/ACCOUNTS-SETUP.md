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

**Workers & Pages → your Pages project → Settings tab → Bindings section**

> **The Pages project and the D1 database do not have the same name, and this page used to say
> they did.** On this deployment the database is `prowlers-and-paragons` and the Pages project is
> **`prowlers-and-paragons-chargen`** — the repository variable `CLOUDFLARE_PAGES_PROJECT` is what
> tells the deploy which. It costs nothing until you type one into a `wrangler` command meant for
> the other, at which point you get *"Project not found"* about a project that is plainly there in
> the dashboard. `npx wrangler pages project list` settles it.

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
The domain is the one the site is served from — **`superheroes.softwaresamurai.net`**, which is
attached to the Pages project already. It has to be a domain you control: **`pages.dev` cannot
send mail**, so the `*.pages.dev` address is not an option here even though the site answers on it.

### 4a. Resend

Sign in to **[resend.com](https://resend.com)**. Then:

1. **Domains → Add Domain →** enter `superheroes.softwaresamurai.net`. Resend shows you SPF, DKIM and an
   MX record to add. Add them to that domain's DNS in the **Cloudflare dashboard**, under
   **Websites → `softwaresamurai.net` → DNS → Records**, each with proxy status **DNS only**
   (the grey cloud — proxying a `TXT` record breaks verification). Wait for Resend to verify
   (usually a few minutes; the row goes green).

   **Cloudflare appends the zone to whatever you type in the Name field**, and that is the one
   way this step fails silently. Resend prints each record's name in full —
   `resend._domainkey.superheroes.softwaresamurai.net` — and pasting that whole string produces
   `resend._domainkey.superheroes.softwaresamurai.net.softwaresamurai.net`, a record at an
   address nothing will ever look up. Enter the part *before* the zone only:
   `resend._domainkey.superheroes`. Cloudflare shows you the full name it will save; read it
   before saving. There is no error and no warning — the row simply stays *Pending* for ever,
   which reads as slow propagation rather than as a typo.

   **`amazonses.com` in the values is not a mistake.** Resend sends through Amazon SES, so the
   SPF `include:` and the bounce-handling `MX` both name Amazon hosts. Nothing has gone wrong
   and nothing needs an AWS account.

   **Verify the subdomain, never the apex, and this is not a style preference.** The apex
   `softwaresamurai.net` already carries a full Proton Mail configuration — the `MX` records that
   receive the owner's personal mail, an SPF `TXT`, three DKIM `CNAME`s and a `_dmarc` `TXT` at
   `p=quarantine`. Every record Resend asks for lands at `superheroes.softwaresamurai.net`
   instead, which is a different DNS node, so none of them touches any of that: mail keeps
   arriving at Proton because the apex `MX` is not involved.

   **What would break it is a second SPF record at the apex.** SPF permits exactly one `TXT`
   record per DNS name; two produce a `permerror` and every message the domain sends starts
   failing authentication. So do not "add Resend to the existing SPF line" — the subdomain gets
   its own, and the apex keeps Proton's untouched. (If the two ever genuinely had to share a
   name they would be merged into one record with both `include:` mechanisms, but nothing here
   requires that.)

   DMARC works out without anything further: `_dmarc.softwaresamurai.net` carries no `sp=` tag,
   so the subdomain inherits `p=quarantine`, and Resend's DKIM signature — issued for the
   verified subdomain — aligns with the `From` domain under DMARC's default relaxed mode.
2. **API Keys → Create API Key →** name it something like *Prowlers and Paragons production*,
   permission **Sending access**, and scope it to the one **Domain** you just verified rather
   than leaving it able to send for all of them. Copy the key it shows once — a `re_…` string —
   because you cannot see it again, only replace it.

   The key can be created before verification finishes if you would rather not wait, but a send
   against an unverified domain is refused, so there is nothing to test until the row is green.

### 4b. Three variables on the Pages project

**In the Cloudflare dashboard:**

**Workers & Pages → your Pages project → Settings tab → Environment variables**

Add three, all for **Production** and **encrypted** (click the encrypt checkbox — the Resend key
is a real credential):

| Name | Value |
|---|---|
| `RESEND_API_KEY` | the key Resend gave you |
| `MAIL_FROM` | `no-reply@superheroes.softwaresamurai.net` — or any address on the verified domain |
| `SITE_URL` | `https://superheroes.softwaresamurai.net` — no trailing slash |
| `ADMIN_EMAIL` | your own address — the one account that can always sign in and manage the rest |

**`MAIL_FROM` need not be a mailbox that exists.** Nothing ever delivers to it — it is the `From`
line and nothing else, and Resend checks only that the domain part is one you verified. A reply to
a sign-in email goes nowhere, which is the intent.

**`ADMIN_EMAIL` is what makes anybody able to sign in at all.** This site is not a
sign-up: only addresses on the invitation list may ask for a link, and that list is managed at
[`/admin`](https://superheroes.softwaresamurai.net/admin) by somebody who is already signed in.
So the first entry cannot come from the list — managing it needs an account, an account needs an
invitation, and an invitation needs somebody to have added one. This variable is what breaks that
circle: the address in it is always allowed, always an administrator, and has no row of its own,
so it cannot be removed by a click.

**A deployment with no `ADMIN_EMAIL` allows nobody**, which is deliberate. Nothing is seeded into
the database, because a committed address would be this repository owner's own — silently making
him the administrator of every fork. A site that signs nobody in is visibly broken; one that lets a
stranger in is not.

**All four are required, `SITE_URL` included.** It is what sign-in links point at, and the server
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
curl -i https://superheroes.softwaresamurai.net/api/me
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

---

## Who can sign in

**Nobody, until you say so.** This site is not a sign-up: an address that is not on the invitation
list can ask for a link all day and nothing happens, and nothing on the page says so — the answer
is identical to a link being sent, because a page that distinguished them would be a way of asking
who is on the list.

- **The address in `ADMIN_EMAIL` is always allowed and always an administrator.** It has no row,
  so nothing on the page can remove it; changing it is a dashboard edit and a redeploy.
- **Everyone else is added at [`/admin`](https://superheroes.softwaresamurai.net/admin)** by
  somebody already signed in who may manage the list. There is no link to it in the site's
  navigation and no button that appears only for administrators — the browser holds no claim about
  who anybody is, so the page is reached by its address and refuses politely if it is not yours.
- **Adding an address sends nothing.** It lets that person ask for a link when they want one; the
  account is made the first time they sign in.
- **Withdrawing an invitation ends any session that address is holding**, so somebody signed in on
  another machine is signed out rather than left there for the rest of the month. **Their
  characters are untouched** — adding the address again gives them back exactly what they had.
- **You cannot withdraw your own**, and the page does not offer it: the next request would be
  refused, including the request to put it back.

---

## When no mail arrives

**The site cannot tell you why, and it is not being coy.** `/api/auth/request` answers `204`
whether a link went out or the address has no account, deliberately — anything else makes it a way
of asking whether somebody has an account here, one address at a time. So "a sign-in link is on
its way" is not a claim that one was sent.

What does distinguish the cases is the status, which the browser's network tab shows and the page
turns into a sentence:

| What you see | What it means |
|---|---|
| `500`, and "something went wrong at our end" | The mail provider refused the send. Quote the reference; the causes are below |
| `204`, and no mail | Either the send worked and the mail is elsewhere — spam, a slow relay — or the hourly allowance is spent |
| "Could not reach the site" | A network problem, not this site's |

**A `500` here is always this end.** The send is the last thing `requestLink` does, so a `500`
means the rate-limit row and the login token were both written: the Function is live, the `DB`
binding is right, and `SITE_URL` is set. Only the provider call is left. Check, in this order:

1. **Is the domain verified in Resend?** *Domains* → the row for
   `superheroes.softwaresamurai.net` must be **Verified**, not *Pending*. Correct DNS is not the
   same as a verified domain — the records can all be right while nobody has clicked *Verify DNS
   Records* — and a send against an unverified domain is refused with a `403` that leaves no
   trace at all in the *Emails* list.
2. **Is the API key for that domain, in that account?** *API Keys* → the key's permission is
   *Sending access* and its domain scope is the verified one. A key scoped to another domain, or
   copied from a second Resend account, is refused with a `401` or `403` and again logs nothing
   you can see.
3. **Is `MAIL_FROM`'s domain part exactly the verified domain?**
   `no-reply@superheroes.softwaresamurai.net` is right; the apex, a typo, or the `send.`
   subdomain are all refused.
4. **Has there been a deploy since the variables were added?** A variable added after the last
   deploy is not in the running Function — step 5 above.

**A refused send is reported every time, not five times.** It used to spend the hourly allowance,
and since a rate-limited request answers with the same `204` a sent link gets, the sixth attempt
began reporting success and kept doing so for an hour. Retrying was therefore the one thing that
silenced the error. It no longer is — but the allowance is real, so five *successful* sends to one
address in an hour will still go quiet, which is not the same fault.

**The status the provider gave is in the log, and the log is a live tail.** Pages Functions keep
nothing to read back, so an error nobody was watching for is gone. To see it, tail the deployment
in one terminal and ask for a link in another:

```bash
# The deployment id comes from the list above it; a tail with no id refuses in a
# non-interactive shell.
npx wrangler pages deployment list --project-name prowlers-and-paragons-chargen
npx wrangler pages deployment tail <deployment-id> --project-name prowlers-and-paragons-chargen
```

The line to look for names the status **and the provider's own code for the refusal**:
*The mail provider refused the send (HTTP 400, validation_error).* The code is what tells the
four checks above apart — `missing_api_key` and `restricted_api_key` are numbers 2,
`validation_error` is number 3, and a `403` about the domain is number 1.

**`wrangler pages secret list --project-name prowlers-and-paragons-chargen` says which of the
three variables exist**, without showing a value. It is the fastest way to rule out number 4: a
variable added after the last deploy is missing from that list until the deploy that picks it up.
