# The accounts server

Read before touching `worker/` or `functions/`, or anything about sign-in, invitations, display names, the error log or the administrator pages.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## The accounts server

`worker/` is a Cloudflare Pages Functions server over D1, reached through the one routed file
`functions/api/[[path]].js`. It is JavaScript because Workers is, so it is invisible to
`dotnet test` and has its own suite: `./scripts/test-worker.sh` (local Node 22+, or Docker).
Setting it up is `docs/ACCOUNTS-SETUP.md`; the reasoning is in `PROGRESS.md`.

- **It is an allow-list, not a sign-up, and the refusal is silent.** Only an address on the
  invitation list may ask for a sign-in link; every other address gets the same `204` a sent link
  gets, because anything else makes the endpoint a way of asking who is on the list, one address
  at a time. **The bootstrap is `ADMIN_EMAIL`, an environment variable, and nothing is seeded into
  the database** — a committed address would be this repository owner's own, silently making him
  the administrator of every fork. A deployment with neither the variable nor a row allows nobody,
  which is the direction this should fail in. **What the list does not hide is time**: an invited
  address waits on a call to the mail provider and an uninvited one returns at once. Recorded
  rather than padded, because padding trades the real defence for the look of one.
- **Withdrawing an invitation ends that address's sessions and keeps its characters.** Deleting
  the row alone is a gesture — the person is holding a month-long cookie — and deleting their work
  would make one button on an administration page the most dangerous control in the application.
  Adding the address back gives them everything as they left it.
- **Adding an address mails it a one-click sign-in link, and the link carries a real token on
  purpose.** `worker/tokens.js` mints and hashes it exactly the way the public request path does
  — same table, same single-use guarantee — and only the lifetime differs:
  `INVITATION_TOKEN_LIFETIME_MS` in `worker/auth.js` is three days against the public path's
  fifteen minutes, a trade that is acceptable here and nowhere else because an administrator chose
  this address on purpose, rather than a stranger typing one in. **The row still grants the
  permission and the mail is only ever a shortcut to using it**: `worker/invitations.js` writes
  the invitation first and mails second, catches a failed send, and answers the admin page with
  `mailed: false` rather than a 500 that would read as nothing having happened — the address can
  still ask for an ordinary link. **The failure is still written to `error_log`, `mail` category**,
  because the fault that breaks this breaks every ordinary sign-in too and the owner should be
  able to find it from either. Do not let a probe or a second builder assemble this message's link
  itself; `signInLink` is the one place either sender's URL is built, same as the token mint.
- **The administrator's page is reached by its address, not by a link that appears for some
  people.** `Identity` still carries a key and a name and no role, deliberately, so the browser
  holds no claim about who somebody is; the server checks on every request and answers an
  ordinary account with the same `404` an unrouted address gets, so the page cannot be discovered
  by trying. The link on the account panel is therefore shown to everybody signed in, and an
  account it is not for is told so plainly.
- **`/admin` is its own `Area`, and the reason is the one recorded for the recordings.** Six
  numbered creation steps and a running Hero Point total above a list of email addresses are an
  offer to continue something the reader is not doing, and the budget is a different subject in
  the same six-label format. `Areas.Of` answers it; `MainLayout` draws neither there. **It now
  covers the portfolio and the sign-in page too** — there are four areas; see [`browser.md`](browser.md), "Four areas, and the
  address decides which".
- **A decision is recorded as a fact, and that is what `0007` is for.** Approving and rejecting
  leave `has_approved` and `has_pending` in states the player has already seen — a rejection
  reverts their standing to the sentence it showed before they sent anything — so `decision` is
  the only thing distinguishing *turned down* from *approved earlier* and from *never sent*. Both
  list rows carry it because they land in one record on the browser's side. Nothing clears it: a
  new submission shadows it and the next decision overwrites it. `decided_at` is stored beside it
  and stays off the wire until something draws a time.
- **Ending a membership is one address with two meanings, and the column that matches is what
  decides which.** `DELETE /api/memberships/{id}` is a player leaving *and* a GM removing
  somebody; nothing in the request says which, so nothing in it can claim a role it does not have.
  The row goes and the campaign's clone with it — **the opposite of deleting a campaign**, which
  keeps every membership precisely so that writing the campaign back is a complete undo. The
  asymmetry runs the same way as everywhere else here: the player's statement is a bare
  `player_user_id = ?` so somebody can walk out of a game the GM has thrown away, and the GM's
  carries the `EXISTS` so a deleted campaign's surviving rows stay survivable. It answers 204 even
  when nothing matched — the end state asked for is true either way, and a 404-or-204 split would
  say whether an id exists — with one refusal, 409 to a GM whose campaign is gone, because a
  removal that did not happen must not be reported as one. See `docs/CHARACTERS-API.md`.
- **It holds no rules and must never gain one.** A character is stored as an opaque string it
  never parses — the engine decides cost and legality and runs in the browser. A second place
  that understood the shape of a character is a second place to keep in step.
- **No password anywhere.** A magic link; the token and the session are both stored as SHA-256
  and never in the clear, so a dump of the database lets nobody sign in as anybody. The session
  is an `HttpOnly` cookie, so the WebAssembly app never holds a credential.
- **Same origin is load-bearing.** Pages Functions rather than a Worker on `workers.dev`,
  because a cookie set by another host is a third-party cookie that browsers now partition
  away. Move the API to its own hostname and sign-in stops working and nothing else does.
- **Every refusal to sign in says the same thing**, and asking for a link always answers 204 —
  otherwise the endpoint is a way of asking whether an address has an account here.
- **`wrangler pages deploy <dir>` bundles a `functions` directory found in the working
  directory, not in the directory being uploaded.** There is no flag; the placement *is* the
  configuration, and getting it wrong deploys a healthy-looking site that signs nobody in.
- **Diagnosing the mail path is `node scripts/probe-mail.mjs`, never a deploy.** The server drops
  the provider's `message` field on purpose — it can quote the address, and it reaches a visitor's
  screen and a log line — so a refusal arrives as a status and a machine code and nothing else.
  That is right, and it means the owner cannot see the sentence naming the broken field. The probe
  reads it locally from `.dev.vars`. **It sends what the server sends**, importing `signInMessage`
  from `worker/mail.js` rather than assembling a lookalike, and two tests hold it there — a
  hand-written probe was tried first with a different key and a literal `YOUR_ADDRESS` in `to`,
  returned *the same provider code the site was returning* for an unrelated reason, and read as a
  confirmation. **A probe that builds its own payload can agree with the bug.**
  - **Read the status, not the code.** Resend answers a bad key with `name: validation_error` at
    `401` — the same name a malformed field gets at `400`. `docs/ACCOUNTS-SETUP.md` said the code
    told the four checks apart; it does not, and four deploy cycles were spent on the strength of
    that. `401` is the key, `403` usually the domain, `400` a field in the message.
  - **Never write to `.dev.vars`, and never delete it.** It is gitignored and holds a live
    credential, so there is no reflog, no stash and nothing to recover — and a provider will not
    show a key twice. Testing this probe destroyed the owner's, which is why `PP_DEV_VARS` exists:
    point it at a scratch file. The rule generalises past this one path — **`ls` a target before
    any `>`, `rm` or `mv`, and do not assume a file is yours because you wrote one like it.**
- **A failure is classified into four categories, and the set is closed.** `mail`, `storage`,
  `configuration`, `unknown`, in `worker/errors.js`. The visitor gets the category and a
  reference and nothing else; the owner gets a row in `error_log`, readable by hand with
  `wrangler d1 execute` (see `docs/ACCOUNTS-SETUP.md`) and, now, through a panel on `/admin`.
  **This reverses an earlier decision recorded here — "there is no admin endpoint and there must
  not be one" — and the reversal is deliberate, not drift.** The reasoning against it was sound
  at the time: `Identity` carried a key and a name and no role, so "am I an admin" was not a
  question the client could ask. What changed underneath it is the invitation list: the *server*
  now answers exactly that question on every request, via `invitations.isAdministrator(env,
  user)`, to gate `/api/admin/invitations` — and `/admin` already answers an ordinary account the
  same `404` an unrouted address gets, so the page cannot be discovered by trying. A read-only
  `/api/admin/error-log`, gated by that identical check, adds no role to `Identity` and no new
  concept; it is the same question asked once more. **The precedent this used to cite against
  itself is gone**: `users.character_limit` was a *write* with no gate built for it, raised by hand
  in SQL against the live database, and that is now a third address behind the identical check —
  see "The accounts screen" below. What is unchanged is the reasoning, which the screen follows
  rather than reverses: the gate is the one that already existed, and the write is scoped so that
  nobody can raise their own.
  - **A category is assigned where a failure is caught, never at a throw site.** `handle()`
    wraps the two subsystems on the way in — `taggedStorage` round the D1 binding,
    `taggedMail` round the send — so `db.js` and `mail.js` know nothing about any of it. A
    category per throw site becomes a description of the internals by enumeration, which is the
    disclosure this exists to avoid. The first tag wins: a storage failure raised *inside* the
    mail call stays `storage`, because the innermost boundary is the one that knows.
  - **`unknown` must stay reachable.** A taxonomy with no default grows a category for every new
    failure, and the pressure is then to classify by guessing.
  - **A category may never depend on whether an account exists**, and this is a security property
    rather than a style rule. Asking for a link always answers 204 precisely so the endpoint
    cannot be used to ask whether an address is registered; a category that appeared only for
    known addresses would put that oracle straight back through the error path. `errors.test.mjs`
    provokes the same subsystem failure for a registered and an unregistered address and requires
    **byte-identical** bodies — which is also why the reference is injected through `deps` like
    the clock, since a random one per failure makes every body differ for an unrelated reason.
  - **`configuration` must never advise retrying**, because retrying cannot set an environment
    variable. That is the category the one failure this site has actually had would have landed
    in. `AccountsContractTests` scans the sentence — and note that *"trying again will not help"*
    is deliberately allowed and deliberately pinned: it is the denial, not the advice. The scan
    carries a positive control on the `mail` sentence, which is known to advise retrying, or an
    absence-only assertion would pass against a regex that captured nothing.
  - **The row is bounded by construction, not by a cap somebody remembers to enforce.** The
    primary key is `(category, route)` and `route` is a *pattern* from a closed list, so
    `/api/characters/{id}` is one row however many ids a caller invents — otherwise the error log
    is a table anybody passing by can fill, with a caller-chosen string in it. Occurrences are
    counted against the one row rather than appended: **`occurrences` is the record of what was
    dropped**, because a silently truncated log reads as a quiet period.
  - **The retention window rolls inside the write statement**, the same shape as `countAttempt`,
    so a stale row starts a fresh count rather than continuing last month's into this morning's
    outage. A prune written as a separate pass is a prune that does not happen.
  - **The logger may never throw.** The thing that just broke is often the database it writes to,
    and a logger that threw out of the catch would cost the visitor the reference and category
    that are the entire visitor-facing half of the design.
  - **Redaction buys less than it looks like and is still worth having.** `users.email` is in
    that database in the clear already, so an error row is not a new exposure *boundary*; what it
    protects is that the log — the artefact most likely to be pasted into an issue — does not
    carry an address. It over-redacts on purpose: any run of twenty or more token-alphabet
    characters goes, with no test for randomness, because a session secret is 43 base64url
    characters and a hash is 64 hex ones and neither is guaranteed to contain a digit.
  - **The absence tests all carry a positive control, and it is not optional.** Every assertion
    about redaction is an absence, and an absence is satisfied completely by a logger that writes
    nothing — the failure shape this repository has shipped four times. Each asserts a row was
    written *and* that the message still says what happened, since a `redact` returning the empty
    string would satisfy every absence while destroying the column.
  - **`console.error` in the catch is one JSON object, not a formatted sentence**, so
    `wrangler pages deployment tail` can filter and read it. `worker/index.js` computes the
    category, route pattern, exception kind, redacted detail and reference once and shares the
    same object with the write to `error_log` — a second computation here could redact
    differently from the row the caller's own reference points at. The exception's raw message is
    never in it, for the same reason the visitor is not shown it either.

- **`/api/admin/error-log` reads the table over the wire, gated exactly as
  `/api/admin/invitations`** — 401 signed out, the same 404 an unrouted address gets if signed in
  but not an administrator, 200 with the rows otherwise. **Read-only, on purpose**: there is no
  route here that deletes or clears a row, because the table needs none — see the migration. A
  panel on `/admin` renders it, beside who can sign in, using the identical gate the invitation
  list already had; an empty table reads as reassurance ("nothing has failed"), not as a blank
  page, and a row whose most recent failure is well in the past says so rather than reading as an
  ongoing outage.

- **A missing server is a missing feature, not a blank page** — and the shape that makes that
  work is also the shape that hides the mistake. `_redirects` serves every unmatched path as
  `index.html` with a 200, so a site without its Functions answers `/api/me` with HTML; the
  client parses the body rather than trusting the status, and answers `Identity.Anonymous`. **So
  the deploy is the only place the fault is ever visible**, and it checks for JSON there.
- **`data/rulebook/` is bundled into the server and never staged into `wwwroot`.** A file under
  `wwwroot` is a public URL; that placement is the entire access control, and there is a test on
  both sides of the repository. **The four recorded conversations the portfolio replays are
  bundled the same way**, into `worker/transcripts-corpus.js` by `scripts/inline-transcripts.mjs`,
  and answered at `api/transcripts` behind the same "signed in, nothing more" check as the
  rulebook routes — see [`replay.md`](replay.md). `ReplayLoader` fetches it once, on demand, rather than at
  startup, which is also what stopped every visitor's browser paying for four files almost none
  of them could ever open.
- **`/api/rulebook/search` narrows to a chapter by filtering the corpus it is handed, never by a
  parameter inside the ranking.** `search(chapters, query, limit)` in `worker/search.js` takes the
  chapters it ranks as its first argument, so `chapter=N` is the same search over a smaller book and
  nothing in the matching rule knows the option exists. **That is what keeps `found` and
  `nothingMatchedByHeading` describing the scoped set for free** — they are computed over the whole
  result and only then is the list cut, an ordering that file insists on for a recorded reason.
  `MOST_RESULTS` is untouched and still the server's, not the caller's.

  **The narrowing cannot be done on the browser's side, and the reason is the cap.** Keeping one
  chapter's rows out of the answer is filtering what survived a limit no caller may raise, so a
  chapter with real matches outside the best thirty comes back empty and `found` becomes the whole
  book's count presented as a chapter's. A test picks a real query and chapter where exactly that
  is true — `vehicle` in Ch.8, whose forty passages the unscoped thirty never reach.

  **A bad chapter is a status code, never an empty result**, and that is a decision rather than a
  convention: `found: 0` is this API's one way of saying *the book is silent on this*, and the front
  end prints it as a sentence about the rulebook. Answering it for `chapter=99` would make that
  sentence a claim about the text told on the strength of a typo. Not a number is `400` and a number
  the book has no chapter under is `404` — the pair `/api/rulebook/passage` already gives.

  **The per-chapter array is cached, keyed on the corpus as well as the number.** `search.js` keys
  its index on the *identity* of the array handed to it, so a fresh `filter()` per request would
  rebuild an index over that chapter's prose every request; and a cache keyed on the number alone
  would answer a second corpus out of the first one's entry, which is the fault `corpusIndex` one
  file over already carries a comment about.
- **The two halves are different languages and both suites stay green while they disagree.**
  `AccountsContractTests` is the only thing that reads both — addresses asked for against
  addresses routed, and the keys the server returns against the names the client binds. Do not
  write a guard there with `Contains`: the first version was one, and a rename walked through it
  because the same word occurred elsewhere in the server's own source.

  **That warning was already in this file and the routing guard was still a `Contains`.** It
  concatenated every `worker/*.js` and asked whether the literal `'/api/me'` appeared anywhere, so
  renaming the real routing condition passed — the literal survives in `worker/errors.js`'s
  `KNOWN_ROUTES`, a list built to bound the error log's row count that happens to name the same
  addresses. Proved twice, on two routes, defeated by two different unrelated duplicates. It reads
  `worker/index.js` **alone** now and extracts the routing *conditions* structurally: the exact
  paths compared with `===` and the prefixes compared with `startsWith`. **The prefix half is not
  optional** — `/api/characters/{id}` and `/api/admin/invitations/{id}` are reached by a
  `startsWith` and a `path.slice`, never an exact match, so an exact-only model flags both as
  unrouted on every real request. Both counts are bounded as a positive control: an extraction that
  has stopped matching yields an empty route set, which fails loudly rather than quietly.

- **Wrangler's bundled esbuild is older than Node's, and both suites plus a whole-tree Qodana
  scan will happily ship an incompatibility to the deploy.** This has happened once:
  `import ... with { type: 'json' }` in `worker/corpus.js` ran under Node 22 (both the accounts
  suite and my local `npx wrangler`) and failed on the deploy pipeline with
  *"Expected ';' but found 'with'"* — because `cloudflare/wrangler-action@v3` pinned wrangler at
  **3.90.0**, whose bundled esbuild predates JSON import attributes. (The action is `@v4` and the
  pin is `4.127.0` now, so that particular esbuild is behind us — **which is not a reason to
  unbake the corpus**. The bake is what makes `worker/corpus.js` byte-checkable against the JSON
  on disk, and the gap between wrangler's bundler and Node's is a permanent property of the two
  moving separately, not a fact about one version.) `assert { type: 'json' }`
  is the older spelling and is deprecated in Node 22; that trade breaks the tests instead of
  the deploy. **So the corpus is baked into `worker/corpus.js` as an object literal by
  `scripts/inline-rulebook.mjs`**, and both are guarded: `tests/worker/router.test.mjs` asserts
  the bake is byte-for-byte the JSON on disk, and the build workflow runs
  `wrangler pages functions build` at the same version the deploy uses (read out of
  `.github/workflows/deploy.yml`'s marker comment), so a wrangler-vs-Node parse difference
  fails the PR rather than the way to production. That marker comment is load-bearing — see it.


## A campaign is another opaque blob, and the server never learns what one is

`worker/campaigns.js` is `worker/characters.js` with a different table, and **it has to stay that
boring**. Four addresses under `/api/campaigns`, a client-minted `g_`-prefixed id, a client-supplied
label, a payload checked for being parseable JSON and stored verbatim. `d1/migrations/0005` adds the
table beside `characters` with the same five columns. The contract is `docs/CHARACTERS-API.md`.

- **It holds no rule and must never gain one** — the same sentence this file already applies to a
  character, and it bites harder here because a campaign's payload contains a *tier*, a *Trait Cap*
  and a *budget flag*, all of which look exactly like things a server could usefully check. It
  cannot: the engine is the authority on every one of them and it runs in the browser. A second
  place that understood a tier is a second place to keep in step.
- **The routes are inside the existing signed-in block, not beside it.** The gate is the thing
  being shared — "signed in, nothing more" — and a second block asking the same question is a
  second block that could forget to. `AccountsContractTests.TheCampaignAddressesAreRouted` reads
  that condition structurally and requires the campaign prefix to be in it.
- **`worker/errors.js` needs both halves, and they buy different things.** `/api/campaigns` in
  `KNOWN_ROUTES` lets the list be filed under its own name; the `path.startsWith('/api/campaigns/')`
  arm in `routePattern` stops every failure at a caller-chosen campaign id being filed as `other`.
  **Note what that arm does *not* buy**: an unrecognised path already falls to `other`, which is one
  row, so the table was never at risk of a row per invented id. What is at risk without it is the
  log being *legible* — a broken campaign route indistinguishable from a passing crawler.
  `EveryRoutedPrefixHasARoutePatternForTheErrorLog` holds the two files together.
- **`characters.campaign_id` is a duplicate of something inside the payload, and the owner approved
  it explicitly.** The server cannot derive it, so the client sends it beside `label` and the server
  stores and returns it — never deriving, never validating the reference, never joining. It exists
  so a list can group characters by game without deserializing and costing every payload it draws a
  row for, which is exactly what `SavedCharacterSummary` was created to avoid.
- **There is no foreign key, and deleting a campaign leaves its members naming it.** Owner-approved.
  A cascade would delete characters; a `SET NULL` would silently edit characters somebody did not
  have open, and neither can be undone by restoring the campaign. The browser reports the state
  instead — `UNKNOWN_CAMPAIGN`, deliberately the same shape as the engine's `UNKNOWN_TIER`.
- **No cap.** `users.character_limit` caps characters, and there is no campaign equivalent — **and
  if one is ever added it has to be written inside `putCampaign`'s statement**, the way the format
  check below is, not as a read in front of it.
- **There is a 409, and it is about the shape of a write, not about how many campaigns exist.**
  `write` reads `format` from the body, beside `label` and outside `payload` — an older build sends
  none of it, and that is read as format 0. `db.putCampaign`'s upsert carries
  `WHERE excluded.payload_format >= campaigns.payload_format` on its `ON CONFLICT … DO UPDATE`,
  which behaves like `DO NOTHING` when the condition is false: no row changes, `RETURNING id` comes
  back empty, and that empty result is what `write` reads as "refuse this one, 409". **It is the
  same race `putCharacter`'s cap check exists to close**, closed the same way — in the statement's
  own `WHERE`, not as a `SELECT` in front of it — because a read-then-write pair here would let two
  saves arriving together both see the older format as current.

  **The defect this fixes**: a stale tab reads a campaign leniently, dropping fields it predates —
  `Assets`, the table rules, the Immortality price — and used to write its own idea of the campaign
  straight back on Save, silently erasing all three. The message is written for the person, not the
  developer, because it is shown verbatim in the browser (`ApiCampaignStore.SaveAsync`,
  `Campaigns.razor`'s `Save`) — this build cannot compose a sentence about a format bump it has
  never seen, so only the server, which minted both numbers being compared, can say why:

  ```json
  { "error": "This tab is running an older version of the site. Saving now would erase settings it cannot see — reload the page and try again." }
  ```

  `d1/migrations/0009_campaign_format.sql` adds the column, `DEFAULT 0` so every row already
  written is telling the truth about the build that last touched it. `StoredCampaign.PayloadFormat`
  (`web/`) is the browser's own copy of the current value, with a doc comment saying what it counts.
- **`join_code` is the one field of a campaign this server can read, and it is a column rather than
  part of the payload for a reason no amount of discipline could get round**: redeeming a code means
  *finding* the campaign it belongs to, and that is a query. It is in the list because the GM has to
  be able to read it out. See the membership section below for the whole of it.
- **The campaign's shared vehicles and bases are inside the payload, and adding them changed
  nothing here.** Ch.6 pp.96 and 100 let a team pool their allowances on one object; the object is
  written down on the campaign and each member's sheet records only the Hero Points it put in. That
  is a `Campaign.Assets` list in the browser's own record and nothing else — **no route, no column,
  no parse, no migration**, because a campaign is stored as a string this server does not look
  into. The member reads it through `GET /api/memberships/{id}/table`, which already answers that
  string verbatim.
- **A field-by-field projection of that route would have broken it silently, and the test that was
  there could not have caught it.** `the live table discloses no byte a join has not already handed
  the same player` moves two fields the server could plausibly know the names of and compares byte
  for byte — so a handler rebuilding the payload out of the keys somebody had thought of answers
  both correctly and drops everything else. Written as exactly that projection, the whole worker
  suite stayed green apart from `a member reads a shared vehicle this server has never heard of`,
  which is why that test exists and why it names the object's id and a feature id individually. The
  member would have opened the Vehicles and bases step and been offered nothing, with no error
  anywhere.
- **Nothing bumped `StoredCharacter.CurrentVersion`, and nothing may.** It is 1, a mismatch is
  discarded in silence, and an absent `campaignId` deserialises to null — which correctly means
  "belongs to no campaign". Bumping it would empty every returning visitor's browser *and* every
  account. `0005` is an `ALTER TABLE`, not a rebuild, for the same reason: `campaign_id` is not part
  of a primary key, so nothing has to be copied and nothing can be lost copying it.

## A campaign's clone of a character, and the one place the scoping rule bends

`worker/memberships.js` is `campaigns.js` with two payload slots and a version, and **it has to stay
that boring**. Nine addresses — eight under `/api/memberships`, one under
`/api/campaigns/{id}/code` — inside the existing signed-in block. `d1/migrations/0006` adds
`campaign_members` and `campaigns.join_code`. The contract is `docs/CHARACTERS-API.md`.

- **It holds no rule and must never gain one**, the same sentence this file already applies to a
  character and to a campaign, and it bites hardest here: the two payloads are *characters*, the
  temptation is to compare them, and the diff is exactly what the engine exists to compute. Nothing
  here parses a payload and nothing here compares two.
- **The clones are not in `characters`, and that is the whole reason a table exists for them.** The
  cap is `COUNT(*) FROM characters WHERE user_id = ?`, so a clone stored there would spend one of
  the GM's own five slots per player: a GM with six players would hit their cap before building a
  single NPC. There is a test that six approved clones leave those five slots untouched, with the
  cap's own 409 asserted beside it as the control.
- **Two owners on one row, so every statement stays scoped to whoever is asking.** `gm_user_id` owns
  the campaign, `player_user_id` owns the character, and no query lets either name a third account's
  row. `getMembership`'s `(player_user_id = ? OR (gm_user_id = ? AND EXISTS (…the campaign…)))` is
  not a widening: each side is entitled to the row for a different reason, and a third account
  matches neither and gets the same 404 an id that never existed gets. **The GM's half carries
  that `EXISTS` and the player's deliberately does not** — see the deleted-campaign bullet below.
  A reader who takes the two
  halves for a symmetric `OR` will misread the inbox, this read and both decisions, all four of
  which carry it, and the `AND c.id = campaign_id` inside it is what keeps a GM's second
  campaign from standing in for the one they deleted.
- **`campaignByJoinCode` is the one read in `db.js` that is not scoped to the caller, and that is
  what a join code is.** A secret the GM minted and chose to hand out; holding it is the whole of the
  authorisation, the same shape as holding a sign-in link. Joining by a shared code cannot be built
  any other way. What bounds it is what it answers — the campaign's id, label and opaque payload, so
  the player can see the tier they are being asked to build to — and never an account id, a
  character, a clone, or anything about another member. **Called out in three places on purpose**:
  that statement's own comment, `memberships.js`'s header, and the contract.
- **A code that never existed and one the GM has replaced answer byte-identically**, so asking twice
  cannot tell somebody a code was once valid. Asserted with `deepEqual` on the body, not on the
  status.
- **The join is rate limited per account, through `db.countAttempt`**, and counted *before* the
  lookup — a limit applied only to successful joins is a limit on nothing. It is the only place in
  this server where a caller can probe for something belonging to somebody else, so the limit costs
  one statement and removes the question.
- **The membership id is the only id this server mints, bar one.** Every other one is the client's
  (`c_`, `g_`) so a PUT is idempotent. The exception is a handed-over Villain's copy on the GM's
  account — see the section below — which takes a fresh `c_…` because the player's own id under a
  GM who redeemed their own code would be the very row the handover removes. A membership is not created by a PUT to a known address — it is
  created by redeeming a code, at the one moment when the server is the only party that can see both
  accounts. Minting it here is also what keeps an account id off the wire: the GM approves `m_…` and
  is never told whose account is on the other side. `characterId` is answered to the player, who
  needs it, and withheld from the GM.
- **`pending_version` is the compare-and-swap, and skipping it is a real defect rather than a
  missing nicety.** The GM reads snapshot A, the player resubmits B, the GM presses Approve, and B —
  which nobody has looked at — becomes the clone. So the version is in the `UPDATE`'s `WHERE`, a
  mismatch matches no row, and the 409 carries the newer snapshot so the screen can redraw. **A
  decision naming no version is a 400**, never defaulted to the current one — defaulting would put
  the defect back, reachable by omitting one field. **And the version is never reset** when the
  pending slot clears, or a resubmission after an approval could reuse a number the GM is still
  holding.
- **`pending_payload IS NOT NULL` is in the same `WHERE`**, so a second click on Approve is refused
  rather than copying a null over the clone. Nothing waiting and a snapshot that moved are both 409
  and are told apart by whether one is attached.
- **One slot, and resubmitting overwrites it.** No history, no rollback. Owner-approved: what this is
  is a decision queue, and a row per submission would be a version-control system for characters.
- **`putCampaign` mints a join code on insert and keeps it with `COALESCE`.** A candidate is minted
  on every call, which is what makes the statement one statement — and the `COALESCE` is what stops
  a rename rotating the code and locking out everybody who was told the old one. Rotating is
  `rotateCode`, deliberately separate, and it evicts nobody: a code is redeemed once, into a
  membership that does not refer back to it.
- **The code's uniqueness is the index's, and the retry is in the handler.** A read that found no
  campaign holding a candidate, followed by a write that trusted it, is the race every statement in
  `db.js` is written to avoid. Bounded at three attempts — at 30^10 codes one collision is already
  not a thing that happens, and an unbounded retry against a broken database is a request that never
  answers.
- **`join_code` is nullable, and a `NOT NULL DEFAULT ''` would have been worse.** An `ALTER TABLE`
  cannot invent randomness for existing rows, so a campaign written before 0006 has no code until it
  is next written — a state the screen reports. With a shared empty-string default, every such
  campaign would collide on the unique index and the second row would be refused.
- **`worker/errors.js` needed both halves, and the two lists are two exact addresses rather than
  one.** `/api/memberships` and `/api/memberships/inbox` are filed under their own names because a
  GM's inbox failing and a player's standings failing are different faults; everything under one
  membership's id is `/api/memberships/{id}`, **verb included**, because `route` is half of a primary
  key and a pattern per verb triples the rows for no gain that `kind` and `detail` do not already
  give.
- **No cascade from `campaigns` to `campaign_members`**, for the same reason there is no foreign key
  on `characters.campaign_id`: a cascade would delete the clone the GM accepted — the campaign's own
  record of what was agreed — on the strength of one click that may have been a mistake. Deleting an
  *account* does cascade, on both columns, and there is a test on each: a membership names two
  accounts and means nothing with either gone.
- **`GET /api/memberships/{id}/table` is the second read scoped to somebody who does not own the
  campaign, and unlike the join code it is scoped to a row rather than to a secret.** Everything
  under `/api/campaigns` belongs to the GM, so a player's browser resolves no campaign at all for a
  game that is alive; what their screen draws is the copy of the table's rules written onto their
  character when it joined, and a GM who changes the game afterwards moves nothing on it. This is
  what lets the screen say so — `PROGRESS.md` item 30, and see [`browser.md`](browser.md) for the
  two lists it draws.
  - **`player_user_id = ?` and no GM arm.** `getMembership`'s `OR` exists because a membership has
    two owners; this read is for the half of that pair who cannot reach the campaign any other way.
    A GM reads their own campaign at its own address, and **a second address answering the owner is
    a second place that could disagree about what a campaign is** — so the GM of that very row is
    answered the same 404 a stranger is. An id that never existed and a campaign the GM has deleted
    answer it too, in the same words: a split would say whether an id exists, which is the reason
    the detail read above gives one sentence to two of them.
  - **`db.campaignForMember`'s join carries `c.user_id = m.gm_user_id`**, the same clause
    `getMembership`'s `EXISTS` does. A `g_…` is unique per account rather than globally and a
    campaign's id reaches every member of it, so matching on the id alone would hand a player a
    campaign belonging to an account they never joined. There is a test with two GMs on one id.
  - **What it excludes, and how that is held.** The answer is two keys — `campaignId` and
    `payload` — so no account id, no label, no join code, no character, no clone and nothing about
    another member. Asserted **by key set** rather than by a handful of absences, because an
    absence is satisfied by whatever nobody thought to name, and the fields a later hand adds here
    are the convenient ones: an id to save a lookup, the label to save a read.
  - **Still nothing is parsed.** The payload goes out as it came in, the way `join` answers the
    same bytes. A route lifting `Table` and `ImmortalityCost` out would be this server knowing the
    shape of a campaign — the rule two sections up, and the failure would be silent: nulls, from
    the first time the shape moved.
  - **Nothing was added to `KNOWN_ROUTES` or `routePattern`.** That list is compared against the
    path that arrived, so an entry naming a caller-chosen id could never match one; the sub-paths
    under a membership are filed under `/api/memberships/{id}` for exactly the reason the verbs
    are. `errors.js` says so where somebody would go looking.
- **The detail read sends no timestamps, and they were there for one commit.**
  `AccountsContractTests` caught the server sending `approvedAt` and `pendingAt` on a read the client
  bound nothing to — the exact drift that test exists for. Removed rather than bound: the timestamps
  are in the two lists, where a "sent three hours ago" belongs, and a field nothing draws is a field
  that rots. The 409 body lost a `pendingAt` and a `label` the same way.

## Approving a Villain hands it to the GM, and the server is told it is one

The owner's rulings of 2026-10-01: an approved Villain becomes the campaign owner's for good, inside
their cap; the player keeps no sheet and sees only its name. The contract is
`docs/CHARACTERS-API.md`; the tests are `tests/worker/nemesis.test.mjs`.

- **The server learns a snapshot is a Villain by being told, beside the snapshot, and acts on the
  word it stored.** This is the decision the slice was most likely to get wrong, and two wrong
  answers are close at hand. *Parse the payload* breaks the rule this file opens with. *Read
  `characters.kind`* looks like reuse and is the subtler fault: that column describes the player's
  sheet now, not the snapshot the GM read, so a player who sent a Villain and then flipped the
  switch would change what approving does — and a player who built in one browser has no row at
  all. So the submission carries `kind`, `submitToCampaign` writes it as `pending_kind` in the same
  statement as `pending_payload`, and approval reads it inside its own statements. Two tests flip
  the player's row after sending and require nothing to change.
- **The word is the client's and can lie, and the lie costs only the liar.** A Villain sent as a
  Hero is cloned and the player keeps it — the behaviour every submission had before this slice. A
  Hero sent as a Villain is given away by the player who sent it, after a warning they confirmed.
  Neither reaches anybody else's rows.
- **Three writes, one `batch`, chained by their own `WHERE`s.** D1 rolls a batch back on a throw,
  not on a statement that matched nothing — so each step names the row the step before would have
  written, and the first step carries the cap. `taggedStorage` forwards `batch` with each wrapper
  unwrapped, because D1 runs only its own statements. The harness's `batch` rolls back too, and a
  test makes the last step throw to prove the first two are undone.
- **A save naming a handed-over id is refused inside `putCharacter`'s own `WHERE`**, beside the
  cap, for the cap's reason: a tab still holding the Villain would otherwise write it straight
  back on its next autosave. The refusal is 410, told apart from the cap's 409 by a re-read on the
  refusal path only.

## An account has a name it can change

`display_name` is set once at first sign-in to the email's local part, and `PUT /api/me/display-name`
is how it stops being that. It needed no migration — the column has existed since `0001_accounts.sql`.

- **Scoped by construction, not by a check.** `db.setDisplayName` takes the id off the session the
  caller already authenticated with. There is no address and no id in the body, so the route has no
  way to name a row other than its own.
- **Uniqueness is deliberately never checked**, on either side. The name a fresh sign-in gets was
  never unique either, and "is this name taken" is the same oracle the invitation list exists to keep
  this site from answering, asked about names instead of addresses. A name is what the banner calls
  somebody; nothing reads it as proof of anything.
- **What is refused is a shape that cannot be rendered, not a judgement about what somebody calls
  themselves**: a control character (the banner is one line), and more than 60 characters.
- **Blank resets to the email's local part rather than being refused.** Storing an empty string
  would leave the banner naming nobody while `Identity.IsSignedIn` still read true, since that only
  checks the name is not null. A cleared name should look exactly like one nobody has set.
- `Identity` still carries a key and a name and **nothing else**. There is a test.


## The error log is visible to an administrator, and that reverses a recorded decision

`/api/admin/error-log` reads the `error_log` table; a panel on `/admin` renders it. Both are gated by
`invitations.isAdministrator` — 401 signed out, and the same **404** an unrouted address gets for an
ordinary account, so the endpoint cannot be found by trying. Read-only: there is no route that
clears or deletes a row.

**`0004_error_log.sql` said there would never be an admin endpoint, and its reasoning was sound at
the time**: `Identity` carried a key and a name and no role, so "am I an admin" was not a question
the client could ask, and inventing a role to answer it was a much larger change than the log needed.
The invitation work made it a question the *server* answers on every single request. So this adds no
role to `Identity`, no claim in the browser, and no new concept — it is the same gate the invitation
list already uses, asked once more. The migration comment records the reversal rather than being
left to contradict the code.

**The live database is `prowlers-and-paragons`.** That comment named `prowlers-accounts`, which does
not exist, so the worked example in it failed for anybody who followed it.


## The accounts screen: whose cap a GM may set, and how a row is keyed

`worker/adminAccounts.js` answers three addresses under `/api/admin/accounts`, gated by the
identical `invitations.isAdministrator` check the invitation list and the error log use, inside the
same routing block — 401 signed out, the same **404** an unrouted address gets for an ordinary
account. A panel on `/admin` renders them. The contract is `docs/CHARACTERS-API.md`.

- **This is a screen over a mechanism that was already correct, and that is the whole shape of it.**
  `users.character_limit` was raised by hand in SQL against the production database, which this file
  already called out as *a write with no gate*. Nothing about how the cap behaves changed:
  `db.putCharacter`'s `INSERT … SELECT … WHERE` still lets an id the account already owns through
  however full the account is, so dropping somebody from 25 to 3 while they hold ten keeps all ten
  openable and refuses only the eleventh. **Do not "fix" a lowered cap by deleting rows or by
  refusing the lowering**; the number is the entire mechanism and the test that drives the cap
  through the new endpoint and then reads the outcome through the character routes is what joins
  the two halves.
- **The scope is `campaign_members`, not every account, and the cost is accepted knowingly.** The
  list is the players in campaigns the *caller* is GM of. `isAdministrator` is one person today so
  an all-accounts list would not bite yet — it would the moment a second GM is ever made an
  administrator, and a privilege that only misbehaves later is the kind this project has been bitten
  by before. What that costs: **a GM cannot see a player's characters unless that player is in one
  of their campaigns, and should not.**
- **A row is keyed by `email`, and that is the decision the membership design makes you argue for.**
  Account ids are kept off the wire there precisely so a GM is never told whose account is on the
  other side of an `m_…`; nothing here weakens it, because this caller is *already* reading every
  address on the invitation list drawn beside this panel on the same page. The address is also the
  only key with the right cardinality: a membership id is per campaign and per character, so a
  player in two of this GM's games would be two rows, and a cap is a property of the account. It is
  percent-encoded in the path and read back through the one `normaliseEmail` the gate uses, so a
  capital letter names the same account here as it does at sign-in. **It never reaches `error_log`**
  — `routePattern` files everything under this prefix as `/api/admin/accounts/{key}`, and `redact`
  takes addresses out of a message — which is the half of the key decision that needed a guard
  rather than a paragraph.
- **The write is one statement and a plain `UPDATE` is right here.** `putCharacter` needs its
  `INSERT … SELECT` because it reads a count and decides on it, so the decision has to be inside the
  write; this one writes a number the caller supplied, and two administrators setting a cap at the
  same moment correctly leave whichever landed second. What *is* inside the `WHERE` is the scope —
  `email = ? AND id <> ? AND EXISTS (… campaign_members …)` — because a read that checked the
  membership followed by an `UPDATE` that trusted it would let a membership ended in between land a
  write on an account the caller may no longer see. `RETURNING` is how the caller learns which
  happened: a row back means in scope and now capped, nothing back means the same 404 an address
  nobody has ever used gives.
- **The caller is excluded from their own list and cannot cap their own address.** A GM can redeem
  their own join code, so without `u.id <> ?` they would be a player in their own campaign with an
  editable number beside their name — and `docs/CHARACTERS-API.md` states that a cap somebody can
  raise on themselves is not a cap. The exclusion is in **all three** statements — the list, the
  read behind their characters, and the write — so the rule survives somebody typing the address in
  rather than clicking a row. **The third had no test and the suite stayed green without it**, which
  is exactly the shape of hole this repository keeps finding: one clause of three covered by nobody,
  because the other two are the ones anybody thinks to drive.
- **Never the payload.** The characters list answers `label`, `updated_at` and the three index
  columns `0008` added — `kind`, `tier_id`, `spent` — every one written by the client, stored
  verbatim and handed back verbatim. Listing a tier by parsing a sheet would give this server an
  opinion about what a character is; it has never had one. **These are the player's own rows**,
  which are a different set from the campaign's clone the GM can already read: a clone is a sheet
  that member deliberately sent and this is not.
- **`worker/errors.js` needs both halves, as it did for campaigns and memberships.**
  `/api/admin/accounts` in `KNOWN_ROUTES` so the list is filed under its own name, and the
  `startsWith` arm in `routePattern` so a failure at one account's cap is not filed as `other`
  beside a passing crawler — and here the arm buys the redaction as well, because the path names a
  person. `EveryRoutedPrefixHasARoutePatternForTheErrorLog` holds the two files together and now
  covers this prefix.

## Inviting somebody emails them, and the token is the exception rather than the rule

`invitations.add` writes the row and then sends a one-click link. **It sent nothing at all until
somebody noticed**: adding an address granted permission and told nobody, while being called an
invitation, so an invited person had no way of knowing they could sign in.

- **`worker/tokens.js` is the only place a token is minted, hashed, or turned into a URL.** Both the
  public request path and the invitation go through it, so there is one mint rather than two that
  could drift on the hashing or the link.
- **`INVITATION_TOKEN_LIFETIME_MS` is three days against the public path's fifteen minutes**, and
  that is the whole difference: same table, same `used_at` single-use guarantee, same verify path. A
  longer-lived credential in an inbox is acceptable **only** because an administrator chose that
  address deliberately, which is not true of the public endpoint — do not carry the three days
  across.
- **A dead mail provider must not lose the invitation.** The row is written first; the send is
  caught; the failure reaches `error_log` as a `mail` category, because the fault that breaks this
  breaks ordinary sign-in too; and the page says which of the three things happened.
- **Re-adding an address already on the list sends nothing** and answers `alreadyAllowed`. So an
  address invited before this existed is not retrospectively mailed — withdraw and re-add.

