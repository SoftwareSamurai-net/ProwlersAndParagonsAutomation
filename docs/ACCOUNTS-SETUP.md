# Turning accounts on

The code is deployed by the ordinary pipeline; **the account system does nothing until five
things exist that only the owner of the Cloudflare account can create.** Until then the site
behaves exactly as it did before: every visitor is anonymous, the character lives in their
browser, and the rulebook reader shows nothing. That is deliberate — see
[the seam](#what-happens-before-any-of-this-is-done).

Everything below is done once, by hand, by somebody signed in to Cloudflare and to a mail
provider. **Nothing here can be automated from this repository and nothing here is committed:**
a secret in a file is a secret in every fork and every clone.

---

## 1. A database

```bash
npx wrangler d1 create prowlers-and-paragons
```

It prints a `database_id`. Put it in [`d1/wrangler.toml`](../d1/wrangler.toml), replacing the
placeholder, and commit that — the id names a database and grants nothing, so it is an
identifier rather than a credential.

## 2. The tables

```bash
npx wrangler --cwd d1 d1 migrations apply prowlers-and-paragons --remote
```

`--cwd d1` is what makes wrangler read `d1/wrangler.toml`; the migrations are the `.sql` files
beside it. Leave `--remote` off to apply them to a local copy instead.

**The schema is the thing the tests run against**, in real SQLite, so a migration that would not
apply fails on the pull request rather than here.

## 3. The binding

On the Pages project → **Settings** → **Bindings** → **D1 database bindings**, add:

| Variable name | Database |
|---|---|
| `DB` | `prowlers-and-paragons` |

Add it for **Production**, and for Preview if you want previews to work.

**The name has to be exactly `DB`.** The server reaches for `env.DB`; a binding under any other
name means every request answers 500, and nothing on the deploy says so.

> A direct-upload Pages project takes its bindings from the project, not from a `wrangler.toml`
> in the repository. That is why this is a dashboard step and why `d1/wrangler.toml` is in a
> subdirectory — a root one would change how `wrangler pages deploy` behaves, which cannot be
> rehearsed without these credentials.

## 4. A way to send mail

Sign in to [Resend](https://resend.com), add the domain the site is served from, and add the
SPF and DKIM records it asks for to that domain's DNS. **A `pages.dev` address cannot send
mail**, so this needs a domain you control — which is also the reason to attach
`pp.softwaresamurai.net` to the project if it is not attached yet.

Then create an API key and set it as a **secret** on the Pages project → **Settings** →
**Environment variables**, marked *Encrypt*:

| Name | Value |
|---|---|
| `RESEND_API_KEY` | the key Resend gave you |
| `MAIL_FROM` | e.g. `no-reply@pp.softwaresamurai.net`, on the verified domain |
| `SITE_URL` | e.g. `https://pp.softwaresamurai.net` — no trailing slash |

`SITE_URL` is what sign-in links point at. Leave it unset and the server uses the origin the
request arrived on, which is right for one domain and wrong the moment there are two.

## 5. A cookie that works

Nothing to configure — but the session cookie is `Secure` and `SameSite=Lax`, and it is issued
for the origin the API answers on. **That is why the API is Pages Functions on the same origin
rather than a Worker on `workers.dev`:** a cookie set by a different host is a third-party
cookie, and Safari and Chrome's partitioning drop it. If the API is ever moved to its own
hostname, sign-in stops working and nothing else does.

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

Then sign in for real: the page is at `/signin`. If the link never arrives, Resend's own
dashboard shows every send it accepted or refused, and the server deliberately answers 204
whether or not it sent one — so the site cannot tell you, by design.

---

## What happens before any of this is done

Nothing breaks, and that is the property worth keeping:

- `Accounts` answers `Identity.Anonymous` for every failure — no network, a 401, or a page of
  HTML with a 200. Identity is asked for **before the first render**, so anything else would be
  a blank page rather than a missing feature.
- `AccountCharacterStore` then routes every character to the browser's own local storage, under
  the historical key `pp.character.v1`.
- `RulebookReader` answers null, and `RulebookEntry` renders nothing at all.

There is a test for each, and one that asserts all three together: `AccountTests`'
`NoServerMeansAnAnonymousVisitorRatherThanABlankPage`.

## What is deliberately not here

- **No password, anywhere.** Proving you can read the address is the whole of the check, so
  there is nothing to store, nothing to leak and nothing to reset.
- **No token in the browser.** The session is an `HttpOnly` cookie, so the WebAssembly app never
  holds a credential and an injected script cannot read one.
- **No third-party identity provider.** Nothing about who somebody is leaves the account the
  site is already deployed to.
- **No character list.** One character per account, and `characters.user_id` is the primary key
  rather than a convention. A list is a different interface and a different set of screens; it
  is much easier to get right once one character round-trips.
- **No rules on the server.** The engine runs in the browser and is the authority on what a
  character costs and whether it is legal. The server stores bytes it never parses.
