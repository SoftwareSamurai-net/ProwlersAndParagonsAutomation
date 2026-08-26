# The replay

Read before touching `data/transcripts/`, `TranscriptLibrary`, `ReplayLoader`, or the recorded-conversation pages under `/admin/portfolio/replay`.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## The replay

`/admin/portfolio/replay` plays back four real conversations for somebody who has no way to hold one
— the MCP server needs a Claude of your own. The transcripts are in `data/transcripts/`, read by
`engine/TranscriptLibrary`, and bundled into the worker at build time — see `worker/transcripts-corpus.js`
and `scripts/inline-transcripts.mjs` — the same way the rulebook corpus is. They are fetched, all
four at once, from `api/transcripts` by `ReplayLoader`, on demand rather than at startup.

**They are behind the account pages now, and that reverses a settled decision deliberately.** The
older entry read that a visitor cannot bring their own Claude and the replay is the answer; the
site's owner has decided otherwise — this is not a sign-up, and the recordings and the two samples
are a thing to show somebody rather than a thing to publish. The pages are wrapped in `AdminOnly`,
which asks the server on every visit and holds no claim of its own.

**It is a real gate now, not a front door.** This entry used to say the opposite: the transcripts
were ordinary files under `wwwroot`, so anybody who knew a filename could fetch one and only the
*pages* were gated. They are bundled into the worker instead and answered only to a signed-in
caller at `api/transcripts` — the same placement that is the whole access control for the rulebook
corpus, and for the same reason: a file under `wwwroot` is a public URL, and no amount of checking
sessions in the browser would make it not be one. `tests/worker/transcripts.test.mjs` holds the
gate to a signed-in caller, with a positive control, and asserts the refused body carries none of
the recorded text. Moving them off `wwwroot` also took them out of every visitor's startup fetch —
`ReplayLoader` fetches once, the first time a component actually asks for a recording, so a visitor
who never opens the replay never asks the server for one at all.

- **A transcript holds characters, never answers about them.** A turn carries a `CharacterSheet`
  — the inputs — and the replay costs and validates it in the browser as the visitor reveals it.
  **If a transcript ever holds a Hero Point total, that is the bug**: the number would sit there
  looking identical while being wrong. `TranscriptTests` refuses a recorded line that quotes a
  Hero Point figure, an Edge, a Health or a Resolve. Ranks are allowed and should be — a rank is
  an input the transcript already carries. **Two rules do that, and both are needed**: one bans
  the *shape* — a number next to a word about money, in one clause — and one asks the engine
  what the figures actually are and bans those numerals, in digits and spelled out. The second
  exists because "She lands on 75 exactly, and the tier hands her 75 to spend" matches no
  vocabulary anybody could write; the first because "over by a full nineteen" is a quoted figure
  whether or not nineteen is the right answer. The scan reads the **character** as well as the
  prose, by reflection rather than by naming its free-text fields.
- **The question-count tests catch drift, not a questionnaire written to evade them.** A demand
  phrased with an unlisted verb scores zero, and the companion test counts the person's
  *replies*, so demands bundled into one turn cost one reply. That is recorded in the tests
  themselves; the real guarantee is the same one the prose has — read a changed transcript.
- **What the tests do not cover is whether a recorded sentence about the rules is true**, and
  that gap is not closeable by a regular expression. The characters are held to the engine and
  figures are banned from the prose, but a line saying "the Trait Cap is a limit on Abilities
  alone" passes everything. The cheap conversation shipped for two commits asserting the
  rulebook has no Power for detecting a lie — it has one, at Perception, for a flat price — and
  a person caught it, not a test. **Read a changed transcript against the rulebook.** A green
  suite says the characters are legal and no figure was quoted; it does not say the recording
  is accurate.
- **Several turns of one transcript may carry a character**, and the one about a draft that did
  not fit depends on it: the draft is stored as a draft, so it is *shown* not fitting rather than
  said to be. `TranscriptLibrary` reads them **strictly**, so a field a character no longer has
  fails at load rather than quietly emptying a section.
- **There is one sheet component.** `SheetView` and `DerivedStatBlocks` take an optional
  `Character`; they do not have replay-shaped twins. The four big figures come from
  `DerivedStatBlocks`, which read the character being built — so a replay printed the *visitor's*
  Edge, Health and Resolve under a recorded name until it was given the recorded one. A bUnit
  test loads a sample first so there is a different character present to be printed by mistake.
- **The hand-off gives the editors a copy**, round-tripped through `CharacterSheetJson`. The
  library is fetched once, the first time something asks `ReplayLoader` for it, and shared by
  every visit after that; handing the instance over lets the first edit rewrite the recording.
- **A failed transcript fetch must not stop the app.** Missing rules are a broken deployment;
  missing recordings are a missing demonstration. `ReplayLibrary.LoadAsync` catches, returns an
  empty library and carries the reason so the page can print it — and **it is a method rather
  than a block wherever it is called from, because that is where nothing could reach it.** It
  was once a `try`/`catch` in `Program.cs`'s top-level statements, back when the fetch happened
  at startup; deleting the `try` left the whole suite green while one 404 took the character
  generator to a blank page. One file short leaves *no* recordings rather than most of them,
  which is deliberate: a library holding three of four looks like a decision and answers the
  fourth address with "no such recording". `ReplayLoader` is what makes the fetch lazy —
  `WebPresentationTests.TheBrowserDoesNotFetchTheReplayLibraryAtStartup` holds `Program.cs` to
  never building one itself, and
  `ReplayRenderTests.NothingFetchesTheRecordingsUntilOneIsOpened` is the behavioural half, with
  a positive control: opening a recording really does ask.
- **Nothing on a replayed sheet may come from the visitor's own character**, and that is
  asserted by rendering the same character twice — once held by the session, once passed as a
  parameter over a *different* session character — and requiring the two pages to be identical.
  Naming the fields does not work: seven of them were free at once. Anything that legitimately
  comes from outside the character, which is `ShowBudget` and only `ShowBudget`, has to be
  passed explicitly in both renderings or it hides every illegitimate difference behind itself.
- **`ReplayLibrary.Find` is `OrdinalIgnoreCase` on purpose.** Blazor's route matching is
  case-insensitive, so `/Replay/The-Conductor` reaches the page and only the lookup can refuse
  it — which is a confident lie about a link that is fine. Same rule as `MainLayout`'s
  first-path-segment check, and both now have tests.
- **The Villain recording shows its budget finding rather than hiding it.** The GM review step
  hides `HP_BUDGET_EXCEEDED` in Villain mode; here it is shown with Ch.9 beside it, because one
  recording is about exactly that difference. **That branch claims no verdict at all** — not
  "legal", not "not legal yet" — and the reason is broader than the budget: this program is not
  the one that decides whether somebody's Villain is finished, and a word in a heading would be
  read as though it were. The findings themselves are all still printed and all still mean what
  they say.
- **The shell's budget bar does not render on a replay route.** It is the visitor's own
  character in the same six-label format as the recorded one below it, and the two were
  indistinguishable — worst on the Villain, whose own panel deliberately shows no budget, so
  the only budget on the screen belonged to somebody else entirely. `MainLayout` reads the
  first path segment; there is a test through the layout, because a page cannot see the shell.
- The replay does not change the app's palette while you watch; opening the character does, the
  way loading a sample does. Whether a *recorded* character is a Villain has nothing to do with
  what colour the visitor is wearing, which is why `SheetView` takes `ShowBudget` too.


