# Progress

The single source of truth for what is done and what is left in this project.

**This file must be updated as part of any task that changes what is done or what remains.** Not afterwards, not in a follow-up — in the same change, so the record and the code land together. Previously this information lived in two places (the README roadmap and a gaps list in `CLAUDE.md`) and drifted out of step with reality; both now point here instead.

Keep it honest. A half-finished item stays open with a note on what is missing. "Done" means done and verified, not written.

---

## Current state

| | |
|---|---|
| Rulebook coverage | Everything character creation needs. Chapters 1–2 fully extracted and verified, plus Ch.6's custom gear and Ch.7's toxin Pros/Cons. Chapters 3, 4, 5 and 7 are play rules, 8 is the pre-built characters (transcribed in the tests) and 9 builds Villains by the Hero rules — see item 3. **All ten chapters of the printed text are extracted into `data/rulebook/` and searchable at `/rules` by anybody with an account**, which is a different store and a different claim: that is the book's prose, and only `data/rules/` is verified entry by entry against the page |
| Powers | 141 entries, all mechanically verified against Ch.2 pp.21–48 |
| Power-specific Pros/Cons | 106 entries across 62 Powers, verified |
| Custom gear features | 12 entries, verified against Ch.6 p.93 |
| Other rules data | Tiers, abilities, talents, pros, cons, perks, flaws, sources — all verified, nothing flagged |
| Tests | 4303 across three suites — 3730 on the engine, 449 rendering components with bUnit, 124 driving the accounts server over real SQLite — all run in CI at the same strictness as the build, plus nine browser harnesses driven by headless Chrome, one of them twice for reduced motion. **Measured on `master` at `9ff148e`, re-run after the merge rather than carried across from the branch.** This row has been wrong twice: three merged branches each claimed a different total, and the handover then copied one of them. Re-run the suites rather than adding to this number |
| Wizard | All six creation steps working, with back-navigation and `.txt` + `.json` export |
| Front ends | Two interactive, plus two for a machine — the terminal wizard, a Blazor WebAssembly app, `build --from`, and an MCP server somebody can connect to their own Claude. All on the same engine assembly |
| Hosting | **Live** at [superheroes.softwaresamurai.net](https://superheroes.softwaresamurai.net), with the `prowlers-and-paragons-chargen.pages.dev` fallback; deployed from `master` by GitHub Actions |
| Accounts | **Invitation only, and sign-in works end to end. An account is now what opens the rulebook** — all ten chapters, searchable at `/rules`, plus the recordings and the two sample characters. All four D1 migrations applied to the remote database, the `DB` binding is in place, `/api/me` answers `401` with JSON, and all four variables are set. **A link has been requested on the live site, delivered, and used to sign in** — watched, not tested, because no test can do it. The fault that blocked it for a week was the API key and not `MAIL_FROM`; see [item 8](#8-the-mail-provider-is-refusing-every-send--closed-and-the-reasoning-here-was-wrong) |
| Printed sheet | One A4 page on the published Hero Sheet's layout; Hero and Villain ink on white paper — see the completed item below |
| Static analysis | Zero warnings at CI strictness; a whole-tree Qodana scan reports zero — **measured on a clean export of `master` at `9ff148e`, not assumed**. It had drifted to 3 on `master` and to 37 across three reconciled slices before anybody checked, and the redesign slice put 23 there before they were fixed. Re-run `./scripts/qodana-scan.sh` rather than repeating the figure |
| Known-wrong data | None outstanding. Every published Hero is now also checked for *legality*, not only cost — see the completed entry on the two the tool used to refuse |
| Licence | MIT, in `LICENSE`, covering this repository's own code only. The game system is © LakeSide Games. `data/rules/` holds structured metadata and this project's own descriptions; `data/rulebook/` holds the book's text **by the author's permission to this repository's owner**, is not served by the public site, and does not travel with a fork |

The engine reproduces the printed Edge, Health and Resolve of all 20 pre-built Heroes in Chapter 8, and rebuilds **16 of the 20 to exactly their 125 Hero Point budget**. The remaining four each rebuild 1 HP out, for a recorded reason — see [Close the last four Heroes](#1-close-the-last-four-heroes), where the bound is stated exactly: it holds of what is *modelled*, and Shadow's printed Gear box carries a custom feature that would put him at +2.

---

## Remaining work

Roughly in the order that unblocks the most. **Nothing here is a defect** — the tool creates, prices, validates, prints and exports characters through four front ends, and a visitor with no account can watch a real conversation build one. What is left is four Heroes a Hero Point out, some polish on the printed sheet, one sub-tool nobody has needed, a Power search that orders ties by name, and a payload size.

[`docs/HANDOVER.md`](docs/HANDOVER.md) picks three of these and says what a slice on each would actually involve, including which approaches are already spent. Read it before choosing; read the entry here before starting.

### 1. Close the last four Heroes

Sixteen of the twenty published Heroes now rebuild to exactly 125 Hero Points. The other four are held at a known residual in `PrebuiltHeroes.BuildByHero`, each with a reason:

| Hero | Residual | Why |
|---|---|---|
| Herald (Scathach) | +1 | Strike carries four Pros and Cons at once — most likely a variant reading |
| Shadow | +1 | Unexplained |
| T-Kay | −1 | `Limited: only for Telekinesis` does not say which grade |
| Vigilant | −1 | Its Jo Sticks are *Upgraded*, a custom gear feature worth +2 — which would take him to +1, not to zero |

Nothing left is more than 1 HP out, and the test asserting that bound has been tightened from 6 to 2 and now to 1, so it stays true.

**The "residuals pair up" lead is spent.** It was worth chasing and it paid twice — see the completed item below — but what closed Vector and Talon was reading the rulebook entry in each case, not the pattern. What is left is −1, −1, +1, +1, and four values one point either side of zero pair up by chance. Do not read more into it.

**All four transcriptions have now been read line by line against the printed sheets, and all four are faithful.** Abilities, all twelve Talents, every Power and its rank, the Pros and Cons in each parenthesis, the Perks with their unit counts, the Flaws, and Edge/Health/Resolve — checked against the page for Scáthach (p.135), Shadow (p.140), T-Kay (p.143) and Vigilant (p.146).

**So the method that closed three Heroes is spent, and the conclusion is different from what it was.** Vector, Talon and Airmid were all *transcription* faults — a Power underpriced, a group costed per option, a whole Power dropped. These four are not. **The remaining ±1 HP is in the pricing model**, and finding it needs a per-element cost breakdown compared against a hand-computed expectation from the sheet, not another read of the page.

Two things established on the way, so nobody re-checks them:

- **`super_senses_night_vision` at 3 HP flat is correct.** Ch.2's Super Senses entry prices Acute X at 1 HP per 2 ranks, Night Vision at **3**, Thermal Vision at 2, and Radio Hearing, Telescopic Vision, Analytic X and Astral Sight at 1 each. Night Vision being the odd one looks like a data error and is not. The same entry confirms the group's effective rank is "your Perception or your Acute X rank, whichever is greater".
- **Vigilant's gear cannot close him.** His Gear box reads `Armored Suit: 7d Armor` and `2 Jo Sticks: 10d (s) Melee (Upgraded)`. The ratings are Ch.6 mundane armour and weapons, which are free; only *Upgraded* costs, at 2 HP once for the pair under Two-Fisted. He is 1 HP under, so transcribing it lands him on +1. It is left out rather than half-applied, and `PrebuiltHeroes` has no gear collection to put it in.

The two ambiguous grades (`Side Effect: collateral damage`, `Limited: only for Telekinesis`) remain judgement calls that could be revisited, but do not tune them just to force a zero — that is fitting the model to the answer. `Limited` is less ambiguous than "guess" suggests: see below, where the floor turns it into a two-way choice and the grade recorded is the one with an argument behind it.

**Revisited, and deliberately not closed.** The arithmetic was worked out and it is a trap:

- **T-Kay closes exactly** if `Limited: only for Telekinesis` is read as *somewhat limited* (−1) rather than *significantly limited* (−2). It sits on Lightning Reflexes, a flat 3 HP Power, so the grade is worth 1 HP after the floor — precisely her −1. **That is the tuning this item forbids.** The sheet prints no grade; "only for Telekinesis" reads at least as much like the harsher grade as the milder one, and the only thing recommending the milder one is that it makes the number come out. Deciding it needs the Power's entry in the book, not this file.

  **And it is a two-way choice, not a three-way one, because the floor collapses half of it.** Measured, by rebuilding her on each grade: −1 gives **125**, −2 gives 124, and −4 gives **124 as well** — an unranked Power floors at 1 HP, so 3 − 4 clamps to the same figure 3 − 2 produces. The two harsher readings are indistinguishable in her total.

  **The direction is what makes this worth stating.** She is 1 HP *under* budget, so closing her means making her more expensive — every harsher reading of the Con moves away from 125 or, past the floor, does not move at all. The only grade that closes her is the mildest, which is the hardest to defend: the +6 Edge applies to one Power out of five, and her sheet prints `Edge 8/14` to show it. So this is not "we cannot tell which of three grades the authors used". It is: **either they read "only for Telekinesis" as barely limiting, or their total is 1 out.** This project takes the reading it can defend and lets the point stand, rather than treating 125 as something the authors cannot have got wrong.
- **Vigilant cannot be closed by his gear.** His Jo Sticks are *Upgraded*, worth +2, and he is 1 HP under: transcribing the feature moves him to +1 rather than to 0. Adding it would make the transcription more faithful and the residual no smaller, so it is left recorded rather than half-applied.
- **Herald (Scathach) at +1 and Shadow at +1** have no candidate in the data at all. (Airmid was at +2 when this was written and is closed — see below.) Scathach's Strike carrying four Pros and Cons at once remains the most likely place for a variant reading to be wrong.

**The book was then opened, and it settled two of the three questions above.** `docs/` holds both PDFs — they are gitignored, so they are in the main working directory and **not in a worktree's `docs/`**, which is how they were missed at first.

- **T-Kay's grade is a judgement call by the rulebook's own words.** The Limited entry (Ch.2) reads: −1 "if the Power is somewhat limited", −2 "if it's significantly limited", −4 "if it's severely limited", and then *"Use this Con as a catch-all when nothing else seems appropriate."* There is no rule mapping "only for Telekinesis" onto a grade, so the milder reading has nothing recommending it except that it produces a zero. **Left as recorded**, at −2, which is also the reading with an argument behind it: a flat +6 Edge that applies to one Power out of five is significantly limited by breadth. The counter-argument is practical — Telekinesis is her 12d signature Power and she uses it constantly, so the restriction rarely bites — and the rulebook is vague enough to hold both. What settles it in favour of leaving it alone is that the alternative is chosen *by its result*.
- **Vigilant's Upgraded is confirmed printed** — his Gear box reads `2 Jo Sticks: 10d (s) Melee (Upgraded)`, and he has Two-Fisted, so the pair is customised for one price of 2 HP. He is 1 HP under, so transcribing it lands him on +1. It closes nothing and is left recorded rather than half-applied.
- **Herald (Airmid) is closed.** The lead was her package: she was recorded on the Superhero Package while her sheet prints nine of twelve Talents at 2d, and a package's granted ranks are a floor. Following it found the actual fault — **her sheet prints two Expertise Powers, "Expertise (Medicine: Ancient Remedies) 12d" and "Expertise (Science: Botany) 12d", and only the first was transcribed.** Expertise costs half a Hero Point per rank and takes its baseline from the nominated Trait, so 12d over Science 2d is ten purchased ranks and **exactly 5 HP** — which is what the wrong package was absorbing. With the second Expertise transcribed and the package corrected to the one her printed Talents allow, she rebuilds to 125 to the point. Both halves are forced by the printed page.
- **Scathach's transcription is verified faithful to the printed sheet** — every Ability, all twelve Talents, all eleven Powers, both her Edge/Health/Resolve and her Determination, and all four modifiers on Strike. The rulebook gives Strike two different deflection Pros, `Deflect` (+4, physical *and* energy) and `Deflect Missiles` (+2, physical only); her sheet prints the plain one and the data uses +4, which is right. So her +1 is in the pricing model, not in the data — which is a narrowing rather than an answer.
- **Shadow** still has no candidate at all.

**A second, independent argument for every package attribution now exists**, and it is what caught Airmid. The inference had rested entirely on which package lands the rebuild on 125 — an argument from a total, and totals can agree for the wrong reasons. A package's granted ranks are a *floor*, so a package is impossible if the sheet prints a Trait below it, whatever the total says. `PrebuiltHeroTests.NoHeroPrintsATraitBelowWhatItsPackageGrants` checks all twenty against that, and it matters most for the five whose totals do not land on 125 — exactly where the totals argument is weakest.

**What reading the book did find is that every one of the twenty page citations was ten pages out.** Chapter 8 runs from printed 127 to 146 and the transcription recorded 137 to 156 — the offset applied twice. This is the error `CLAUDE.md` already warns about ("was ten pages out in the chapter it was offered for"); the note was corrected and the transcription was not, because nothing read those numbers. `PrebuiltHeroTests.EveryHeroIsCitedInsideChapterEight` now does.

**The per-element breakdown this item asked for has now been done, and it is a negative result.** Every cost element of all four was printed out beside its rulebook entry and checked against the page:

| Hero | Rebuild | What the breakdown found |
|---|---|---|
| T-Kay | 124 | Flight 1 HP/rank, Force Field 1 HP/rank, Telekinesis 2 HP/rank, Area +2, Zone +2, Overload +2, Determination 5 HP per Resolve — **every element as printed**. The −1 is entirely the `Limited` grade |
| Herald (Scathach) | 126 | Strike's four modifiers confirmed on the page: Deflect +4, Phase Shift +4, Reach/Throw +2, Item −1. Weakness Detection 3 HP flat. **No mispriced element** |
| Shadow | 126 | Preparation 6 HP flat (Ch.2 p.38) and Swing Line 1 HP per 2 ranks (p.44) both confirmed printed |
| Vigilant | 124 | the same two confirmed |

**So the method this item prescribed is now spent as well.** The residual is not a mispriced element in any of the four — which is a real narrowing, because it was the last cheap explanation. What is left is an interaction: a floor, a baseline or a grouping applied where the authors did something else. Nothing points at which, and two examples would not be evidence if they did.

**One thing the pages did add, and it widens rather than closes.** Shadow's Gear box prints `2 Pistols: 9d Ranged (Silenced)`. Silenced is a Ch.6 custom feature at 1 HP, and the pair is one price under his Two-Fisted — so transcribed, Shadow is **+2**, not +1. The "nothing more than 1 HP out" bound above holds only because gear features are not modelled on these transcriptions. Recorded rather than half-applied, exactly as Vigilant's Upgraded Jo Sticks are.

**What the breakdown did find was two defects, and neither is a Hero Point.** Both made a character printed in the rulebook one this tool refuses — see the completed entry below. They were reachable only because nothing had ever asked the validator about the twenty; `EveryPublishedHeroIsALegalCharacter` now does.

Four rebuilds 1 HP out, each with a recorded reason — and one of them, Shadow, 1 HP further out than that once his printed gear is counted — remains a more honest state than four zeroes.

One thing genuinely cannot be modelled as things stand: Eidolon's `Omni-Power (Mind Link)` applies Telepathy's Pro to a *mimicked* Power. Pros are stored per Power, so there is nowhere for it to live. Eidolon reconciles anyway, so it costs nothing today.

### 1b. Semantic pro/con constraints are still unenforced

The invented per-Power lists are gone — see the completed item below. What is left is the half of the constraints that cannot be checked against anything the rulebook prints per Power: "Powers that inflict physical or energy damage", "Powers that can be activated and deactivated at will", "attack Powers", "Powers that last or can be maintained". These are shown to the player as a caveat on the option and left to the GM, which is how Ch.2 frames the list.

Enforcing them would need roughly seven booleans on each of the 141 Powers — about a thousand fresh judgements against the book. That is worth doing only if something downstream actually needs it, and the obvious candidate was assisted creation, where a model proposing a character benefits from the engine ruling out illegal combinations.

**Assisted creation has now shipped without them, and did not need them** — see the completed item below. A caveat is shown to whoever is proposing and left to the GM, which is what Ch.2 says it is. So this stays open with no consumer asking for it, and the caveat remains honest where the guess would not be.

### 8. The mail provider is refusing every send — **closed, and the reasoning here was wrong**

**Sign-in works. A link was requested on the live site, arrived, and signed somebody in** — the
first time that has happened, and the one claim in this file no suite backs.

**The fault was the API key, which this entry argued it could not be.** It read:

> a bad or wrongly scoped key is `401`/`403` and never reaches validation, and the domain is
> verified in Resend with every record present … so what is left is the *value* of `MAIL_FROM`

Every sentence of that is defensible and the conclusion was wrong. `MAIL_FROM` was correct
throughout. It was re-entered twice on the strength of this paragraph, each time followed by a
deploy and a fresh refusal, and the refusal never moved because the variable being changed was
never the broken one.

**What the reasoning got wrong is one measurable fact.** Resend answers a bad key with
`{"statusCode":401,"name":"validation_error","message":"API key is invalid"}` — the *same*
`name` a malformed field gets, at a different status. So a key fault does not announce itself as
`missing_api_key`, and `validation_error` says nothing about which of the four checks failed.
Replacing the deployed key with a fresh one on the same account made the identical body succeed.
**Why the old key produced a `400` rather than a `401` is not established** — most likely scoped
to another domain — and it is recorded as unexplained rather than guessed at, because guessing at
exactly this is what cost the evening.

**The instrument that closed it is `scripts/probe-mail.mjs`**, and it is the durable outcome. The
server drops the provider's `message` field on purpose — it can quote the address, and it reaches
a visitor's screen and a log line — so the owner could not see the sentence naming the broken
field. The probe reads it locally, from the owner's own credentials, in one command instead of a
deploy cycle. It **imports `signInMessage` from `worker/mail.js`** rather than assembling a
lookalike, which is not a nicety: a hand-written probe was tried first, carried a different key
and a literal `YOUR_ADDRESS` in `to`, returned *the same provider code the site was returning* for
an entirely different reason, and read as a confirmation. Two tests hold it to the shared builder
and both were watched to fail.

**Three things this cost, worth reading before diagnosing anything similar:**

- **A probe that builds its own payload can agree with the bug.** It reproduced the symptom while
  testing nothing the site does.
- **`wrangler pages secret list` shows that a secret exists and never what it is**, so "all four
  variables are set" is compatible with any of them being wrong. It rules out one cause and reads
  like it rules out four.
- **Testing the probe destroyed the credential it had just proved.** A throwaway `.dev.vars` was
  written at the real path and cleaned up afterwards, taking the owner's with it — gitignored, so
  no reflog and no stash, and the provider will not show a key twice. `PP_DEV_VARS` exists so
  nothing exercising the script has a reason to write where a person keeps a credential.

**A second fault was masking this one and is fixed** — see the completed entry below. Every
attempt was counted before the send, so five refusals spent the hourly allowance and every try
after that answered the same cheerful `204` a sent link gets. That is why the site said a link
was on its way, Resend's dashboard showed nothing and Cloudflare showed nothing: by then nothing
was being attempted.

### 2. What the sheet still cannot say

Found by an adversarial audit during the sheet-polish slice; real, and out of scope for it.

**A printed page in the middle of a sheet is anonymous.** Much less pressing now the sheet is one page for an ordinary character, but a Powers-heavy one still runs over. The name is on page one and in a colophon on the last; every page between them relies on the browser's own print header, which the user can switch off — and unticking it is exactly what the review step now tells them to do, because that header is also where the web address comes from. CSS has no portable answer: `position: fixed` renders once at the top of page two in Chrome, and Chrome supports neither `@page` margin boxes nor `counter(page)`. The only mechanism that genuinely repeats per page is a table `<thead>`, which would mean rebuilding the sheet as one table.

Smaller, from the same audits: the GM review step lists findings with no route back to the step that caused them.

**The 0d half of this item turned out to be a rules gap rather than a UI wrinkle, and is closed.** It was recorded as "a fresh sheet starts every Ability at 0d although the editor's floor is 1d without anything objecting". The floor was right and nearly everything else was wrong: Ch.2 states, once for Abilities (p.17) and again for Talents (p.18), that **no rank can be lower than 1d** and that ordinary people have 2d in every one — so a character has all eighteen Traits, 0d is not a low rank but a Trait nobody can be without, and the Talents editor's floor of 0d contradicted the book outright.

It was enforced nowhere, and it costs Hero Points: without a package a character pays for all eighteen at 1d, which is 18 HP before anything interesting. **The packages corroborate it** — the Civilian Package is 35 HP for 2d in all eighteen, which is 36 points of ranks, exactly the "small discount" the rulebook calls a package. All twenty published Heroes take a package, so every one of their Traits sits at or above its floor, which is why rebuilding them never caught this.

Now `TRAIT_BELOW_MINIMUM`, with `TRAIT_BELOW_PACKAGE` beside it for the other floor — a package's granted ranks cannot be lowered, which is the rule that proved Airmid's attribution impossible and was a test over the published Heroes before it was a check here. Both samples had to gain their missing Talents; both were illegal characters shipped as examples.

### 3. Remaining rulebook chapters — mostly not this tool's business

**Which pages have actually been read is now tracked page by page in
[`docs/RULEBOOK-COVERAGE.md`](docs/RULEBOOK-COVERAGE.md)**, with a resume marker, because this
table is a judgement about chapters and that is a record of pages. The two answer different
questions and the ledger is the one that can be resumed.

**This item used to say chapters 3–9 were "not extracted" and rank them "by usefulness to the wizard", with Combat third. That was wrong, and it made a finished job look unfinished.** Read against the table of contents, everything a *character generator* needs is extracted:

| Chapter | What is in it | Relevant to creating a character? |
|---|---|---|
| 3, Action (p.67) | Challenge rolls, assisting, contests | No — play |
| 4, Combat (p.73) | Combat, stunts, minions, gritty rules | No — play |
| 5, Resolve and Adversity (p.83) | Earning and **spending** Resolve | No — play. The Resolve a character *starts with* is Ch.2 p.60, extracted and implemented |
| 6, Equipment (p.87) | Gear limits, armour, weapons, **custom gear (p.92)**, gadgets, vehicles, headquarters | Custom gear features: **extracted**. Mundane gear is free and untracked. See below for the one gap |
| 7, Environment (p.105) | Disasters, falling, lifting, **toxins (p.108)** | No — play. The three toxin Pros/Cons are extracted |
| 8, Friends and Foes (p.111) | **Three things, not one**: NPC and animal stat blocks (p.111), Extras (p.120), and the twenty pre-built Heroes and Villains (p.126) | Only the last is transcribed, in the test suite where they verify the engine. The other two are GM material — characters the GM fields, not ones a player builds — so they are out of scope rather than missing. Recorded because "Ch.8 is the pre-built characters" was wrong about 15 of its 56 pages |
| 9, Creating Villains (p.167) | Villain guidance, GM tips | No mechanics to extract — Ch.9 builds Villains by the Hero rules, which is why the mode is presentation only |

**The one genuine gap is Ch.6's vehicles and headquarters (pp.94–104).** `unique_vehicle` and `headquarters` are Perks priced per unit — a Hero Point buys 25 Vehicle Points — and what those points buy is not modelled, so the perk is a cost and a free-text note. That is a sub-tool of its own (spend a vehicle's points on a vehicle), not a chapter to extract, and nothing else needs it.

### 4. `search_powers` ranks ties alphabetically

The MCP server's Power search is a word match, and when several Powers match the same words it
puts them in name order under a caution calling them "the closest entries". **"Walks through
walls" is the case to reproduce**: it returns twenty-two, of which **twenty tie on a single
word** — eighteen on "through" and two on "walls" — so which of them a caller sees is
alphabetical. Phasing is eleventh, where a caller asking for eight rows never sees it. (Measured
against the built server; an earlier version of this paragraph said twenty-one on "through", and
was wrong on both figures.) "He shoots fire from his hands" is the same weakness the other way
round: Blast is never returned, because its description says "a damaging ranged attack" and none
of those words is in it.

**Weighting each word by how much of the rulebook uses it was implemented and reverted**, and
that is the finding rather than the fix. It sorted "walks through walls" correctly and broke
"reads minds", which dropped Telepathy out of the first three because four Powers carry "mind"
in their names. Two examples are not evidence; a half-tuned scorer is worse than a dull one,
because it is wrong in places nobody has looked at rather than in the place they tested.

What shipped instead is the truth about each row — `matched_terms` names which of the caller's
words it matched, `found` and `more_beyond_these` say the list was cut, and the caution says
rows matching the same words are in no meaningful order and to search a more distinctive word.
The guide teaches all three.

Closing it properly needs a set of descriptions with expected answers — twenty or thirty, written
from the Powers rather than from the scorer — and then a scoring change measured against them.
That is a slice of its own, and until somebody wants it, an honest label beats a tuned guess.

### 5. The browser payload is large — a characteristic, not a defect

**The site works.** It is deployed, it loads, it builds characters — this is not a fault, and it was listed alongside real gaps for too long. The first load is **27 MiB uncompressed**, about a third of that over the wire once Cloudflare applies Brotli, and cached hard afterwards because every framework asset is fingerprinted, so a returning visitor pays nothing. Everything below is what it would take to make that number smaller, kept because the *reasons* are expensive to rediscover — not because anything is broken.

It is that large because **IL trimming is disabled**. `RulesRepository` deserializes with reflection-based `System.Text.Json`, so the trimmer is free to remove model properties it can only see through reflection, and the failure mode is not a build error but a silently empty rules set at runtime. `System.Private.Xml` alone is 3 MB of assembly nothing references.

Two ways to close it, neither free:

- **Root the engine assembly** for the trimmer (`TrimmerRootAssembly`). Smallest change, but it only preserves what is named, and a Power model gaining a property later would be trimmed away without a warning.
- **Source-generate the JSON contexts** (`JsonSerializerContext`) so deserialization stops being reflective at all. Better in principle, and it would speed up startup — **but it was tried, and it does not drop in.**

**What the source-generation attempt found, so the next one does not repeat it.** A context over the eleven rules types builds clean, loads every file, and returns **null** for six collection properties that are declared non-null with an `= []` initializer: `PowerModel.PowerPros` and `PowerCons`, and the four applicability lists on `ProModel` and `ConModel`. `CostCalculator.ResolveModifiers` dereferences the first of those for any Power carrying a Pro. The reflective reader honours the initializers; the generated one does not.

It surfaced loudly only because the applicability check had *just* started reading two of the six — the other four would have been a quiet wrong answer, which is the same shape as the trimming hazard the change was meant to remove. `RulesLoadingTests.NoCollectionOnAnyLoadedRulesModelComesBackNull` is what caught it and is kept: it walks every loaded model and fails on any non-nullable collection that came back null, whatever the cause. **Anything done here has to keep that test green.** A next attempt would need the models to stop declaring initialised `IReadOnlyList<T>` init-properties, which is a change to the engine's public shape rather than to how it is read.

**The local toolchain still cannot verify any of it.** `dotnet workload install wasm-tools` needs elevation, and without the workload the trimmer cannot run at all. Attempting the install unelevated leaves advertising manifests under `~/.dotnet/sdk-advertising` that make the SDK demand the workload and refuse to build `web/` at all; deleting that directory undoes it.

**Either needs a machine that can run the trimmer to verify.** It cannot run locally: the ILLink task host crashes without the `wasm-tools` workload, on the stock Blazor template too. CI can, so the work is possible — but "it built" is not evidence here, because a trimmed-away model is a runtime silence. Whatever is done needs a check that actually loads the published site and reads a rule out of it.

Not urgent. The site works, and a returning visitor pays nothing.

---

### 6. The mutation-audit backlog — **closed**, all 33

**Recorded here so it outlives [`docs/HANDOVER.md`](docs/HANDOVER.md)**, which is a note between
sessions and gets deleted. Three agents that knew nothing about the work were asked, for every
guard test, to name a plausible bug it claims to cover but would not catch, **and to demonstrate
it by mutation rather than argue it**. They ran 64 mutations and **38 survived**. Five were in the
rulebook corpus and were fixed at the time; the remaining 33 were grouped into three slices.

**All three are closed** — A1 (the MCP server's twelve), A2 (browser and replay, thirteen) and A3
(engine and validator, eight), one completed entry each below. **They were worked concurrently on
three branches and reconciled afterwards**, which is why each entry quotes a test count measured
against its own branch rather than against this tree; the reconciled figure is the one in the
table at the top of this file. The merge touched only this file, `CLAUDE.md` and
`docs/HANDOVER.md` — no test and no source file was resolved by hand.

None of the 33 was a bug in the product. Every one was a **test that did not hold what it claimed
to hold**, which is a different and quieter problem: the suite's headline number goes up and its
grip does not.

### 7. The pre-1.0 audit

The last pass before tagging `v1.0.0`, done as a separate slice, both halves cheapest to
delegate to no-context agents:

- **Is the codebase as optimised as it should be?** Dead code marked for removal, hot paths on
  the engine, payload waste, and the token side — files a subagent has to load before it can
  do anything useful.
- **Is it snapshotable to a fresh AI agent?** What can a new session read to know what this
  repo is and where the load-bearing pieces are, without re-tracing every past decision? The
  audit's job is to say what would improve `CLAUDE.md` — a redraft, or smaller pointer files
  for common tasks.

Do it once the HTTP API stops moving, so audit targets are not shifting under it.

---

## Completed work

### A front door with two avenues, the whole book searchable, and the sheet while you build

**The open half of the visual redesign, and it turned into an information-architecture change
rather than one widget.** The handover named three candidates for what a first screen should
demonstrate — a dice roller, a live cost, a verdict — and asked for the choice to be taken with the
owner before anything was built. It was, and the answer was none of the three: **present the
avenues.** A rules reference and a character builder, with the portfolio moved out of the way, and
the working assumption that somebody arriving is here to build a character rather than to look a
rule up.

**One thing the handover did not name, and it shaped the whole slice.** `/` was both the first
screen *and* step one of the wizard, so a visitor who had not decided what they came for met step
one of a job they had not chosen. `ChooseTier.razor` had already recorded exactly that objection
when the two sample characters were moved off it — *a demonstration is not a step in making your own
character* — so parking a demo on `/` would have re-opened a decision already taken. The builder
moved to `/build` instead and `/` became the chooser.

#### What landed

- **Four areas, decided from the first path segment.** `""` is the front door, `build` the six
  creation steps, `rules` the reference, `admin` (with `signin`) the account pages. An unrouted
  address falls to the front door rather than to the builder: the default is what the not-found page
  gets, and a numbered step list with one step marked current, above "no such address", offers to
  continue something that never started.
- **Both avenues offered from everywhere**, replacing one banner link that flipped its own label to
  name whichever half you were not in. That works for two rooms and fails for three — it identifies
  a destination only while there is exactly one elsewhere.
- **The front door carries figures and none of them is written into the page.** 141 Powers off the
  rules the app is running; the spend beside a character in progress from the same `TryCost` call
  the budget strip makes, and shown only when the engine can give one. A figure typed into
  `Home.razor` would be the one number on the site nobody had checked, on the page whose whole claim
  is that the numbers are real.
- **`/rules` searches all ten chapters and cites the printed page.** 725KB baked into the worker,
  224KB gzipped, well inside the limit. The matching rule is `Mentions` from the MCP server, ported.
- **Hovering an option or a Trait says what it is.** The lists priced things and never said what
  they were; the descriptions were in `data/rules` the whole time with nothing showing them.
- **Phase 4: the sheet drawn beside the editors** on the characteristics step, with `--column`
  widened on the token so all five bands follow it.
- **The portfolio — recordings and both samples — moved behind the account pages.**

#### The entitlement decision, and what it lifted

Broadening the corpus past Chapter 2 was recorded as the owner's call and a blocker. He took it:
**an account may read the book**, and the older restrictions on shipping the rulebook text and the
published characters are lifted. `scripts/inline-rulebook.mjs` globs `data/rulebook/` rather than
naming files, and the test that used to assert it named exactly one chapter now asserts it names
**none** — a filename in that script is a list that goes stale the first time a chapter is added,
and the failure would be a chapter silently missing from the search rather than anything visibly
broken.

#### What the search rule actually buys here, which is narrower than it buys in the MCP server

The ported rule matches word by word with a shared-prefix allowance, never by substring, because
substring matching once answered *"she bakes bread in the city"* with **Plasticity** — and a wrong
match that looks plausible costs more than a miss.

**The first version of the comment in `worker/search.js` claimed the wider property, and it is
false.** It said the baker's sentence has to stay at nothing found. It does not and cannot: over
there the haystack is 141 short Power entries, here it is the whole book, and "city" is a word the
text genuinely uses — *City of Heroes* in the introduction, "a city, forest, jungle" in Attuned.
**Twenty-one real matches, measured.** What is worth pinning is that the sentence must not reach
Plasticity, with the positive control beside it, since a search that has stopped working satisfies
every absence. Watched to fail: swapping the word test for `String.includes` turns four tests red.

#### Three faults that only a screenshot could find

A green suite is not a working app — three visible defects survived 4,133 tests here and four pieces
of developer jargon survived 4,186. Proof pages were written for all three new screens and driven
rather than rendered at rest, and looking at them found:

- **The Trait name's dotted underline was drawn in `--rule`**, the hairline token, which under a
  word is invisible. It was the only marking on the control, so the control had no marking.
  `--muted` now.
- **A CSS comment claimed the preview keeps the sheet's three columns.** It does not:
  `.sheet-columns` is `auto-fit, minmax(280px, …)`, so a ~560px column fits one or two. The comment
  is corrected rather than the layout — three at 180px each is worse, and the three-column
  arrangement is a fact about the paper.
- **The search results sat unframed between two panels**, reading as an unfinished section rather
  than as the answer to the box above.

#### SheetView did not redraw, and nothing could have told us

It reads the session and takes no parameter that changes, so Blazor has nothing to compare and skips
it when the parent re-renders. Measured, with the tab strip above it reporting one Power beside a
sheet still drawing twelve blank rules. **It was invisible while the only sheet on screen was the
review step's**, where the character is finished before anybody looks. It subscribes now, and only
when it is showing the session's own character — a recording is handed over as a parameter, and
tying it to the visitor's edits is the influence the replay renders two pages to forbid. Both
directions are asserted.

#### Three guards were shaped by the code rather than by the claim

- **The contract scanner** required `searchParams.get('field')` on the expression, so hoisting the
  search parameters into a local — which a handler reading two parameters wants to do — reported a
  field the server plainly reads as unread. A guard that dictates the shape of the code it inspects
  is a guard that gets worked around rather than fixed.
- **`NoScreenCalcNamesARawLength`** refused `100vh`, which names the container exactly as `100%`
  does and can agree with no token. Widened — **pinned to the figure 100 rather than to the unit**,
  because exempting the unit is the mistake this file already records twice: an allow-list let `9pt`
  through and its thirty-unit replacement let `9dvmin` and `4PX` through. Watched: `37svh` is still
  refused.
- **The corpus sync test** asserted exactly one chapter.

#### Everything watched to fail

Every new guard was broken and seen to go red, and in three cases the rendered tests stayed green
through the mutation, which is the whole reason the stylesheet guards exist:

| Mutation | What went red | What stayed green |
|---|---|---|
| Word matching → `String.includes` | 4 accounts tests | — |
| `:focus-visible` removed from the row tip | 1 CSS guard | all 9 rendered tests |
| `position: static` on the preview | 1 CSS guard | all 7 rendered tests |
| `--column` widening removed | 1 CSS guard | all 7 rendered tests |
| `calc(37svh - …)` | the widened calc guard | — |

#### What this did not do, and is honest about

- **The portfolio gate is a front door rather than a lock.** The transcripts are still ordinary
  files under `wwwroot`, so anybody who knows a filename can fetch one; only the *pages* are gated.
  Making it real means serving them from the worker as the rulebook is, which would also take them
  out of every visitor's startup fetch. Not attempted here; it is a refactor of
  `ReplayLibrary.LoadAsync` and of `Program.cs`, and the recordings hold nothing secret.
- **The old `/portfolio` and `/replay` addresses now 404.** Deliberate: the content is
  account-gated, so a public link that still worked would be the wrong answer, and one that arrives
  wearing the wrong chrome is worse than one that breaks.
- **The preview is on the characteristics step alone**, because the finishing step is where the free
  text is and a whole sheet behind every keypress is the render cost the front-end plan warns about.
  Asserted, with a control.
- **Still no visual regression testing**, and this slice makes the gap worse: three new screens, four
  palettes, every screenshot judged by eye.

#### Merged and deployed

[#73](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/73) went into `master` as
**`9ff148e`**, with Build, Deploy and Qodana green on the merge commit.

**The new server routes were then checked in production rather than inferred from a green deploy.**
`/api/rulebook/contents`, `/api/rulebook/search` and `/api/me` all answer `401` with
`application/json`. That is the check worth making rather than fetching a page: `_redirects` serves
every unmatched path as `index.html` with a **200**, so a route that never shipped comes back
looking like a working page and only the body tells you. A JSON refusal proves the address is routed
*and* that the gate is on it.

**4,303 tests** — 3,730 engine, 449 bUnit, 124 accounts — and a whole-tree Qodana scan at **0**,
both re-measured on `master` after the merge rather than carried across from the branch. That is
the discipline this file's own Tests row exists to enforce, and the merge is exactly where it has
been broken before.


### Three branches reconciled into one, and the four things that only collided

`#67` (invitation list, and the send that was never made), `#68` (four palettes on two axes) and
`#69` (categorised error reporting) were built in parallel off the same commit. Each was green on
its own and all three reported `MERGEABLE` against `master`, which is a statement about *text*
and says nothing about whether they agree. Four things only existed once they were in one tree.

**Two migrations both numbered `0003`.** Different file names, so git merged them silently and the
schema had two. The invitation table keeps `0003` and the error log became `0004_error_log.sql`,
with `db.js`'s "has migration 0004 been applied?" and the harness's explicit list following it —
appended rather than inserted, because `migration.test.mjs` indexes that list by position. Nothing
would have failed; the two would simply have applied in alphabetical order for ever.

**A misconfigured deployment named who was on the invitation list.** The gate was written above
the `SITE_URL` check, so a missing setting answered an invited address with a 500 and a stranger
with `204` — an oracle for list membership, available to anybody, on exactly the failure this site
has actually had. It is the same property `#69` refuses for account existence, on the axis `#67`
introduced, and neither branch could see it because neither contained both halves. The deployment
check goes first now; `a broken deployment answers an invited and an uninvited address
identically` pins it with byte-identical bodies and two positive controls. **Two channels stay
open and are recorded rather than papered over**: an uninvited address does not wait on the mail
provider, and while that provider refuses everything an invited address gets a 500 where a
stranger still gets `204`. Closing either means mailing strangers or padding every refusal to the
length of a send.

**A test that passed for the wrong reason.** `an error log that cannot be pruned does not stop
anybody signing in` asks for a link and asserts one was *sent*. It builds an `env` of its own, so
it calls `handle` directly and misses the harness scaffolding that quietly invites the address a
request names — and the new gate then answered `204` with no mail, which is the exact shape of the
pass it was looking for. It invites by hand now.

**The sign-in copy became false in both directions.** `#68` tightened "if that address *can have*
an account here" to "*has* an account here" while `#67` made the site invitation-only. An invited
address that has never signed in has no account row and still gets a link; an uninvited one gets
the same sentence and no mail. "Can have" is the word that covers both, and the reason is in a
comment rather than on screen.

Beyond the collisions: a whole-tree Qodana scan of the merged tree found **six**, all in `#67`'s
new files and none of them ever reported — that PR's Qodana check came back `NEUTRAL`, and CI runs
Qodana in PR mode regardless, so no whole-tree number for the merged tree existed. Three were real
and are fixed, two are the reflection-bound-DTO objection this repository already has a scoped
name for, and the scan is back to a measured zero. `AddingAnAddressPutsItOnTheList` turned out not
to check the list — deleting `await Reload()` left it green, because the page's own confirmation
sentence satisfied an assertion against the whole markup; it reads the rows now, and the weakness
predated the merge. Three documented claims had gone false: the settled list still said "two
palettes", the setup guide listed six tables while naming only one of the two new ones, and the
handover's error-reporting section named account existence as the only axis a category must not
betray. And `publish/` is gitignored, which it should have been before — both workflows publish
there, so reproducing the CI step that checks the Content-Security-Policy leaves 717 files of
build output in the tree.

**What was not done:** none of the three slices' own work was revisited or re-reviewed. Each was
reviewed on its own PR; this reconciled only where they met.

### Four palettes on two axes, and the print bug that would have shipped with them

**Light/dark is now independent of Hero/Villain.** The two used to be one switch — Hero was a
light theme, Villain a dark one — so somebody who wanted a dark screen had to make their Hero a
Villain to get it. There are four token sets now: hero-light and villain-dark are the two that
always existed, with their values unchanged, and hero-dark and villain-light are new.

**Villain-light was the one with a real risk in it** — a crimson-and-gold identity on white that
does not just become Hero in different hues — and what made it tractable is that a villain-on-white
already existed and nobody had noticed: the *print* palette, whose crimson and brass had been
measured as ink on paper years of commits ago. It is those, on a warm oyster ground rather than
Hero's cool near-white, keeping villain-dark's 2px rules and tight heading tracking. Identity
survives the change of ground by weight as much as by hue. Both candidates — warm paper and cool
— were built as proof pages and looked at side by side before one was chosen.

**The slice's real finding is a print bug that every existing guard would have missed.** The
handover prescribed `:root[data-theme="dark"][data-mode="x"]` blocks so an explicit choice beats
the system. That is specificity (0,3,0); the print block is (0,2,0), and `@media` contributes
nothing to specificity. So a reader in dark mode would have printed the full-bleed near-black
page the print block exists to prevent — with `PrintKeepsThePaperWhiteAndTheInkReadable` green,
because it read the print block's own declarations rather than resolving the cascade against the
screen blocks. Measured in a browser before any CSS was written, not reasoned about. The fix is
`@media screen` on the dark half: they do not apply on paper at all.

**And the guard built to catch it did not, at first.** Its replacement resolves the whole
stylesheet for a given state — but the first version applied admitted rules in **source order**,
which is not the cascade, and passed with `screen` deleted from the OS-dark media query. That was
the mutation that mattered: an earlier, wider mutation (`@media all`) had *appeared* to be caught
and was not — it tripped the resolver's refusal to model an unknown at-rule, which is an honest
refusal and not the catch it looked like. Weighing specificity, the same mutation fails on exactly
the three states where a dark system reaches paper, and names it.

One defect found in the contrast instrument itself, inherited from #65: its token-name regex was
`--[a-z-]+`, which does not match `--shadow-1`. Every shadow, space and type token was silently
dropped from every palette it resolved.

**The preference is per-browser and attached to nothing else** — `pp.theme.v1` in local storage,
never sent to the server, not on `CharacterSheet` and not on the account. A theme on the sheet
would travel through an export and change the screen of whoever imported somebody else's
character; one on the account would let a signed-in reader on a shared machine impose it on the
next. `js/theme.js` is loaded from `<head>` and is the only render-blocking script in the app,
because the payload is ~27 MiB and a theme applied from C# lands seconds after the reader has
already seen the wrong one.

**Persistence turned out to have no guard at all, and could not have a C# one.** Deleting the
`localStorage.setItem` — so a choice applies for the visit and is forgotten on reload — left all
4,115 tests green: the C# side checks that the right word goes out and that a stored value is read
back, and both are true of a script that stores nothing. `proof-theme.html` drives the shipped
file in a browser and re-executes the module, which is what a reload does; it is in the build
workflow beside the other harnesses. Its own positive control was wrong first — re-executing the
module re-declares `ppThemeStats`, so the counter *resets* rather than going up, and asserting the
reset is what makes it a control.

**Separately, four places on screen explained the app to a developer**, all found by the owner
reading it. The sign-in page explained that it would not say whether an address has an account —
noise to somebody signing in, and an advertisement of the defence. The replay page accounted for
who would pay for the model, in a sentence that had also stopped being true ("no accounts, no
server"), and sent a reader wanting the live version to `docs/MCP-SETUP.md`. A sample character
was vouched for by "there is a test that says so". A panel said "nothing was pre-computed". All
four behave identically; the reasoning moved into `@* *@` comments. Two guards hold it:
`NoPageExplainsItselfToADeveloper` grew eight phrases, and `NoPagePointsAtAFileInThisRepository`
is structural.

**Still open from the redesign brief, and deliberately not in this slice:** making the substance
visible rather than described — numbers as design material, the Hero Point budget as the hero
moment, and a first screen that demonstrates the mechanic instead of listing features. That is the
larger half of the brief and it is easier to build against four settled palettes than alongside
them.

### The Hero Point budget becomes the hero moment — a first step, not the whole brief

**The handover named the Hero Point budget as the cheapest, strongest starting point for "make
the substance visible", and this slice is that step alone** — not the first-screen dice-style
demonstration, not Phase 3's validation-on-the-row or undo, not Phase 4's live sheet preview.
Those stay open below.

**The number a player watches continuously used to be a full step smaller than the numbers they
see occasionally.** `DerivedStatBlocks` already sets Edge, Health, Resolve and the Hero Point
total at `--text-3xl` on the derived-stats step and on the sheet; the sticky strip printed the
same total at `--text-xl` in the one place it changes every few seconds while a character is
being built. Raised to match — the app's largest numeral, not a caption beside one — and set in
`--heading` rather than plain ink, the same role the tier cards and the active step already
carry. Both are text roles already held to their 4.5:1 floor on `--panel` in all four palettes,
so nothing new needed measuring.

**The breakdown disclosure became a small bar chart, not only a row of numbers.** The six
categories `TotalCost` sums — Package, Abilities, Talents, Powers, Perks, Gear — now each draw a
meter sized to their own share of the spend, using the same `--accent` fill on `--panel-sunk`
track the sticky rail above them already uses: one visual idiom applied twice, not a second one
invented. Trait Cap is not a spend and carries no meter; it sits below the six as a rule, set
apart the same way the sheet sets a rule apart from a figure. The meter is decoration — the
numeral beside it already carries the same figure in words, so the track is `aria-hidden`.

**Proved by breaking, on both the new engine-adjacent logic and the CSS no bUnit test can see.**
`HpBudgetBar.Share` forced to return 0 failed `TheBreakdownShowsEachCategorysShareOfTheSpend`'s
width assertions (`width:0%` where `width:38%` was expected) — restored, and the whole suite
re-run green afterwards, not only before. `.budget-figure strong`'s `font-size` reverted to
`--text-xl` failed `TheWatchedFigureIsTheAppsLargestNumeral` the same way, which is the test that
exists precisely because a stylesheet-only regression is invisible to every rendered-markup
assertion in the project.

**A stale doc comment in the file was corrected in passing.** `HpBudgetBar.razor`'s own opening
comment still claimed the strip was "Hidden entirely in Villain mode" — true before the sandbox
toggle existed, and contradicted three paragraphs later in the same file and by
`AVillainIsStillHeldToTheTiersBudget`. Left as found, it is exactly the kind of thing `CLAUDE.md`
warns a stale note becomes: something the next reader trusts because it is close to the code.

**Still open, and larger than this slice:** *(all but two of these are closed by the entry above —
see "A front door with two avenues".)*

- ~~**The first screen that demonstrates rather than describes.**~~ **Done, and not as any of the
  three candidates.** The choice was taken with the owner and the answer was to present the
  avenues: `/` is a chooser, the builder moved to `/build`, and the front door's figures are the
  engine's. What the entry above adds is the reason none of the three fitted — the tier page was
  step one as well as the first screen.
- **Phase 3's validation-on-the-row and undo**, from `docs/FRONT-END-PLAN.md`. **Still open.**
- ~~**Phase 4, the sheet as a live preview column.**~~ **Done**, with `--column` widened on the
  token at 1500px so all five bands follow it.
- **No visual regression testing**, unchanged and now worse: three new screens on top of the four
  palettes, every screenshot judged by eye.

### Only invited addresses, and a page that says which

The site could mail a sign-in link to any address anybody typed into it. That is the ordinary
shape for a public sign-up and it is not what this site is — an account is what puts the
rulebook's own text on screen, and who may read that belongs to the owner and to people he has
named. So there is a list, and a page that manages it.

**The gate is silent, and it has to be.** An address that is not on the list gets the same `204`
a sent link gets, for the same reason a rate-limited request does: any other answer makes the
endpoint a way of asking who is on the list, one address at a time. It is checked again when a
link is spent, because fifteen minutes is long enough to be withdrawn in.

**The first invitation cannot come from the list**, since managing it needs an account and an
account needs an invitation. `ADMIN_EMAIL` breaks that circle: the address in it is always
allowed, always an administrator, and has no row, so no click can remove it. **Nothing is seeded
into the database** — a committed address would be this repository owner's own on every fork, and
a deployment with neither the variable nor a row allows nobody, which is the safe direction.

**Withdrawing ends the sessions that address is holding and keeps its characters.** Deleting the
row alone is a gesture against somebody holding a month-long cookie; deleting their work would
make one button on an administration page the most dangerous control here.

**The page holds no claim about who is reading it.** `Identity` still carries a key and a name
and no role — the decision recorded when accounts were built — so the server answers an ordinary
account with the same `404` an unrouted address gets, and the page is reached by its address
rather than by a button that appears for some people. `/admin` is a third `Area` for the reason
the recordings are the second: the six creation steps and a running Hero Point total mean nothing
above a list of email addresses.

**Sixteen tests on the server and nine on the page, and two things were found by mutating them.**
Five deletions in the server — the gate, the gate on spending a link, who may reach the page,
ending a withdrawn address's sessions, and the refusal to withdraw your own — each turn a named
test red. The browser's five found one guard that was not guarding: the fixture's administrator
was *also* the deployment's address, whose row has no id, so a page offering a withdrawal on every
row with an id passed. The fixture now has four rows and tells the two cases apart. The other is
recorded in the harness: nearly every test in the accounts suite predates the list, so the harness
invites the address a request names — and there is a test that this scaffolding is really doing
something, because a bypass that had stopped working would leave the whole suite passing for the
wrong reason.

**What is still not proven is a link arriving.** The provider refusal above is unchanged by any of
this, and no invitation is worth anything until somebody can receive one.

### A refused send spent the allowance that would have reported it

The first person to try signing in to the live site got "a sign-in link is on its way to it", no
mail, and nothing in either dashboard to say why. Both halves of that were this repository's doing.

**The rate limit counted attempts, not messages.** `requestLink` counts against the address and
against the source before it calls the provider, and a refused send left the count spent. The
limit is five an hour, and the two answers this endpoint gives are deliberately identical — a
rate-limited request and a sent link are both `204`, so that nobody can use it to ask whether an
address has an account. So the sixth attempt stopped reporting the failure and started reporting
success, for the rest of the hour. **The shape hides itself**: somebody retries *because* no mail
arrived, and retrying is the one action that silences the error naming the fault.

The fix is `db.refundAttempt`, called on the failure path only: an attempt is spent on a message
rather than on a request, so the limit still bounds the mail one address or one machine can cause.
What it no longer bounds is requests against a provider that is refusing all of them — which is
the trade, and it buys back the only signal there is that something at this end is broken.

Two tests, one per bucket. The second is not redundant: nothing in that suite sets
`CF-Connecting-IP` unless a test says so, so a refund written for the address alone would pass
every assertion about the address. Both were confirmed by deleting the two refund calls from the
committed fix and watching them go red, and each carries the positive control that the limit still
bites on mail that was actually sent — a refund that had broken the counting outright would
otherwise look like a pass.

**It did not fix sign-in**, and the open item above says what is still wrong: the provider is
still refusing. What it fixed is that the site now says so every time instead of five times.

### Error reporting: four categories, a recorded row, and a message with the addresses out

**One failure, two audiences that want opposite things.** A visitor needs to know whether to
retry, wait or report — and nothing else, because an internal message is both meaningless to them
and a disclosure. The owner needs to know what threw. Before this the visitor got one flat
sentence and the owner got a live tail: close it and the error was gone, so any failure nobody
happened to be watching for was unrecoverable. [#66](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/66)
did the cheap half — a 500 stopped being reported as an unreachable site, and gained a reference.
This is the rest, scoped in `docs/HANDOVER.md` before it was built.

**A closed set of four — `mail`, `storage`, `configuration`, `unknown` — in `worker/errors.js`.**
Each side renders the same category its own way: the 500 body carries `{ error, reference,
category }` and `SignIn.razor` maps the category to a sentence, replacing the single
`LinkRequest.Failed` message with one per category.

- **The category is assigned where a failure is caught, never at a throw site.** `handle()` wraps
  the two subsystems on the way in — `taggedStorage` round the D1 binding, `taggedMail` round the
  send — so `db.js` and `mail.js` know nothing about categories and one file says how a failure is
  classified. A category per throw site would be a description of the internals by enumeration,
  which is the disclosure the design exists to avoid. The first tag wins, so a storage failure
  raised *inside* the mail call stays `storage`.
- **`configuration` never advises retrying**, because retrying cannot set an environment variable.
  That is the category the sign-in failure that prompted all of this would have landed in — and
  `SITE_URL` missing now throws rather than answering with its own bare 500, so the one failure
  this site has actually had is the one a visitor could not report and the owner could not find
  afterwards. It can be both now.
- **`unknown` stays reachable and is the default at both ends**, including for a category the
  client does not recognise. A taxonomy with no default grows a category for every new failure,
  and the pressure is then to classify by guessing.

**The owner's half is one D1 table read by hand, and there is no admin endpoint.** `Identity`
carries a key and a name and no role — there is a test asserting the wire identity holds nothing
else — so "am I an admin" is not a question the client can ask, and inventing a role to answer it
is a far larger change than this needed. The precedent is `users.character_limit`, raised by hand
in SQL on the reasoning that a cap you can raise on yourself is not one. `docs/ACCOUNTS-SETUP.md`
carries the `wrangler d1 execute` command and the table of what each category means.

**Bounded by construction rather than by a cap somebody remembers to enforce.** The primary key is
`(category, route)` and `route` is a *pattern* from a closed list, so `/api/characters/{id}` is
one row however many ids a caller invents — otherwise the error log is a table anybody passing by
can fill, with a caller-chosen string in it. Occurrences count against the one row: **the count is
the record of what was dropped**, because a silently truncated log reads as a quiet period. The
retention window rolls inside the write statement, the same shape as `countAttempt`, so a stale
row starts a fresh count rather than continuing last month's into this morning's outage; a prune
written as a separate pass is a prune that does not happen.

**On redaction, what it buys and what it does not.** `users.email` is in that database in the
clear already, by necessity, so an error row is not a new exposure *boundary*. What it protects is
that the error log — the artefact most likely to be read aloud, pasted into an issue or
screenshotted — does not carry somebody's address. It over-redacts on purpose: any run of twenty
or more token-alphabet characters goes, with no test for whether it looks random, because a
session secret is 43 base64url characters and a hash is 64 hex ones and neither is guaranteed to
contain a digit. **The table is still never safe to publish.**

**The three tests that were the point, and one property that is security rather than style:**

| Pinned | How |
|---|---|
| Nothing anybody should read twice reaches the row | An exception quoting an address and a token-shaped string, provoked through the real mail boundary — **with a positive control that a row was written at all**, and a second that the message still says what happened, since a `redact` returning `""` satisfies every absence while destroying the column |
| The public body carries a category and a reference and no exception text | The same provoked failure, asserting the body has exactly the three keys and none of `Resend`, `422`, the address or the token |
| **The category never varies with account existence** | The same subsystem failure for a registered and an unregistered address, requiring **byte-identical** bodies. Asking for a link always answers 204 precisely so the endpoint cannot be used to ask whether an address is registered, and a category that appeared only for known addresses would put that oracle back through the error path |

That last one is why the failure reference is injected through `deps` like the clock: a random one
per failure makes every body differ for a reason that has nothing to do with the question.

**Every guard was broken and watched go red — seventeen mutations, all seventeen red**, and the
suites re-run after the reverts rather than only before. The ones worth naming: a logger that
silently writes nothing (11 red — the shape this repository has shipped four times), redaction
that keeps addresses (4), that keeps long random strings (3), and that returns the empty string
(2); `unknown` defaulting to `storage`; the route stored as the arrived path; occurrences frozen at
one; the retention cutoff never firing; nothing pruning; the logger rethrowing out of the catch;
the category dropped from the body; **the category made to depend on whether the address had an
account** (11); a client wire name renamed off the server's; the configuration sentence advising a
retry; *no* sentence advising a retry, which fires the positive control rather than the assertion;
a rendered category deleted; and the server growing a fifth category.

Five of the first twelve came out **inert** on the first pass — a multi-line `perl` substitution
that matched nothing — and were rewritten line-based until they bit. An inert mutation reads
exactly like a guard that held; it is worth checking that the file actually changed before
believing a green run.

Not done, and deliberately: **no third-party error service** — nothing about who somebody is
currently leaves the Cloudflare account, and that is worth more than a nicer dashboard — and **no
stack traces to the client in any environment**, since there is no debug build of a deployed site
and a flag that turns them on is a flag one mistake from being on.

### Characters, plural: a manager, imports, and the export the app was not writing

**The accounts slice deliberately stopped at one character**, and this closes it. Up to five per
account (25 for a GM, `users.character_limit`), a list with per-row Open/Discard, and an import
affordance folded into the same panel. Both halves the old panel was tested for came across: it
asks before discarding, only when there is something to lose, and the clear still lands after the
save that emptying the sheet fires.

**"Download to keep" is the reason the whole slice is not smaller than it looked.** The importer
was written against the strict inputs shape — which is what `build --from` reads and what a
character actually is — and the app's existing "Download as data" writes the *report*: derived
stats, costs, findings. The strict reader refuses it, correctly, because reading it back would
rebuild a character from its own conclusions. Both halves were right on their own and the pair was
useless: a player could download their character and had nowhere to take it. Found by the import
agent, which noticed there was nothing for it to import.

**Which character is open is a local pointer, and that had to be, for a reason a test caught after
it went wrong.** A fresh browser signing in has no such pointer, and minting a new id there asked
the server for a character that could not exist — the visitor started on an empty sheet while
theirs sat on the server under an id this browser had never heard of, which is the feature broken
in the case it exists for. `ApiCharacterStore` now lists first and adopts the most recent one.

**The identity/role tension held.** `Identity` is still a key and a name — no claims, no token, no
role — so "am I a GM" is not a question the client can ask. What the UI needs is a *number*, and
that rides on the list response as `limit`. The cap is set by hand in SQL, with no self-service
endpoint, on the reasoning that a cap you can raise on yourself is not one.

**The fan-out earned itself three times on things no single agent could see**, and this is worth
recording because the pattern is expensive if wielded badly:

- The character silently stopped following you to another browser — the local-pointer trap above,
  caught by `ACharacterFollowsItsAccountToAnotherBrowser`.
- The importer had nothing to import — the export gap above, caught by
  `ExportImportRoundTripTests`.
- `RenderContext` claimed to wire the same services as `Program.cs` and was false twice in one
  afternoon. That fails as *every* render test at once on "Unable to resolve service", loud about
  everything and silent about the one missing line. `RenderContextWiringTests` now compares the
  two, and its own positive control caught my first exemption list being wrong.

**`AccountsContractTests` earned its keep for the third time**: it went red the moment the server
dropped `/api/character` while the browser still asked for it, with both language suites green.
Nothing but that one test can see across the seam.

**One honest caveat.** The manager's "open now" row marking resolves through `ppStore`, and
bUnit's loose interop answers null, so a static render can't show it. It works in a real browser;
the proof carried a note saying so rather than claiming I saw it.

**One deliberate blast radius the fan-out cost, recorded rather than glossed.** Three build
agents at 24–37 minutes each; the fix-audit reviewer 30 more. The mutation-verification demand in
each brief drove most of that — "break every guard and re-run the full suite" is about 50s per
mutation, and one agent did 19 of them. That is the discipline that catches six-of-nine theatre
in the fix pass, but the demand has to be *scoped*: mutate the security and ordering guards,
filter tests to affected classes, and let landing come before verification rather than block on
it. See the memory note about it.

### #57: bake the rulebook, bundle on CI

Master's deploy went red after #55 landed — the wrangler pinned in `deploy.yml` predates JSON
import attributes. The corpus is baked into `worker/corpus.js` by `scripts/inline-rulebook.mjs`,
and CI now runs `wrangler pages functions build` at the pinned version so a bundler difference
fails the PR. Details and both guards are in `CLAUDE.md`'s accounts-server section.

### Accounts: one character, one account, and the book behind a sign-in

**The character used to live in one browser and nowhere else.** Close it on another machine and it
was gone; the only way to share one was to download a file; and the rulebook's own text had no way
to know who was reading it. This closes all three.

**Cloudflare Pages Functions over D1, in the account the site already deploys to** — chosen over a
hosted identity provider and over a "sync key" that would not have been an account at all. The
deciding argument was the session cookie: an API on `workers.dev` is a different origin, so its
cookie is a third-party cookie and Safari and Chrome's partitioning drop it. Same-origin Functions
cost one directory and no new bill. **If the API is ever moved to its own hostname, sign-in stops
working and nothing else does.**

**A magic link, so there is no password anywhere.** Proving you can read the address is the whole
of the check, so there is nothing to store, nothing to leak and nothing to reset. Two secrets are
minted — the link's token and the session — and **neither is ever stored in the clear**: the
database holds SHA-256 of each, so a dump of it lets nobody sign in as anybody. The session is an
`HttpOnly` cookie, which means the WebAssembly app never holds a credential and an injected script
cannot read one. That keeps true the claim the MCP server already made for this project: it handles
no credentials.

**Four things the server refuses, each because the alternative is a silent hole:**

- **A link works once**, enforced by `UPDATE … WHERE used_at IS NULL … RETURNING` — one statement,
  because read-then-write lets two requests both redeem the same link.
- **Every sign-in refusal says the same thing**, whether the token was never issued, has expired or
  is spent. Nothing legitimate needs the difference.
- **Asking for a link always answers 204**, so the endpoint cannot be used to ask whether an
  address has an account here, one address at a time. The rate limit is silent for the same reason,
  and its window rolls inside the statement rather than in a read-modify-write — the alternative is
  a limit that stops counting exactly when it is under load.
- **A state-changing request must carry this site's own `Origin`**, and one with no `Origin` at all
  is refused rather than allowed. `SameSite=Lax` already blocks the cross-site form post; this does
  not depend on the visitor's browser having got that right.

**The server never parses a character.** The payload arrives as JSON, is checked for being JSON and
being under a quarter-megabyte, and is written down verbatim; a read hands the same bytes back. The
engine is the authority on what a character costs and whether it is legal, it runs in the browser,
and a second place that understood the shape would be a second place to keep in step. A test sends
key order and spacing no serialiser would reproduce and requires them back unchanged.

**One character per account, and `characters.user_id` is the primary key rather than a convention.**
A list is a different interface and a different set of screens; it is much easier to get right once
one character round-trips, and the handover said so explicitly. *(The next slice does that widening
— see the entry above.)*

**The book is bundled into the server rather than copied into `wwwroot`, and that placement is the
entire access control.** A file under `wwwroot` is a public URL, and no amount of checking sessions
in the browser would make it not be one. Chapter 2 only — where the Powers are — because adding
chapters is a decision about what an account is entitled to read and should not happen by a glob.
*(**That decision has since been taken and this is now all ten chapters** — see "A front door with
two avenues" below. The placement is unchanged and is still the whole access control.)*
The lookup needs no table of its own: it joins on the heading, which is the join
`RulebookCorpusTests` already holds the corpus to across a hundred and sixteen entries.

**`Tooltip` was the wrong container and is not used.** A Power's entry is several paragraphs to
read, not a sentence to glance at, so it is a disclosure — and it renders **nothing at all** when
there is nothing to show. That last part is most of the design: about a fifth of the 141 Powers
have no printed entry of their own, because Super Senses' sixteen options share one between them,
so a row of apologies would appear under Powers that are perfectly fine.

**A missing server is a missing feature, never a blank page.** Identity is asked for before the
first render, so every failure — no network, a 401, a five-second timeout — answers with the
anonymous visitor. **Including the one that looks like success:** `_redirects` serves every
unmatched path as `index.html` with a 200, so a deploy without its Functions answers `/api/me` with
a page of HTML. The client parses the body rather than believing the status.

**That property is also why the deploy is the only place the mistake is ever visible**, and it now
checks: the routed function must exist before uploading, and `/api/me` must answer 401 *carrying
JSON* afterwards. Without that second check, a site with no accounts API looks completely healthy
and signs nobody in for ever.

**`StoredCharacter` was extracted so the two stores cannot disagree.** Local storage and the server
keep the character in very different places, and the temptation is to let each own its envelope —
at which point a version bump or the null-repair lands in one of them and a character saved on a
laptop restores wrongly on a phone.

**Nothing about the rules learns any of this**, and `AccountsContractTests` enforces it the way
`PresentationFlagsTests` enforces the Hero/Villain flag — with a positive control, because a scan
for eight names is satisfied completely by eight names that no longer exist. It also asserts
`engine/` and `sheets/` make no HTTP call at all, which is the form that would catch an account
arriving under a name the scan does not know.

**The two halves are written in different languages and both suites stay green while they
disagree.** The server is JavaScript and the client is C#; each is tested thoroughly alone, and
nothing but `AccountsContractTests` reads both. It compares the addresses the browser asks for
against the ones the server routes, and the keys of the object `identityOf` actually returns
against the names the client actually binds. **That last one was a `Contains` first and a mutation
walked straight through it:** renaming the server's `displayName` to `display_name` left it green,
because the word still occurred in `db.js` as a parameter name. Searching concatenated files for a
word says nothing about where the word is.

**Tested against real SQLite running the real migration**, through a D1-shaped shim — D1 *is*
SQLite, so the two statements that close a race by being one statement are executed rather than
described. A hand-written fake would have passed for either. **Thirteen mutations were applied and
all thirteen caught**, but two of the first results were worthless: removing an `expires_at > ?`
from a query left three parameters bound to two placeholders, so what went red was a broken
statement rather than the missing check. Redone by binding `0` for the timestamp instead, which
keeps the arity and changes only the answer.

**And one "survivor" was a hole in the harness, not in the code.** The mutation script ran
`dotnet test` and not the accounts suite, so a rename the Node tests caught was reported as
surviving. A mutation harness that does not run every suite reports the wrong answer confidently.

**Two faults were found by looking at a rendered page, and neither was visible to any test:**

- The signed-in proof rendered the **signed-out form** under a heading saying "Signed in". Loading
  a sample raises the session's change event, which saves the character, which asks who is here and
  *remembers the answer* — so the harness had to sign in before loading. `SignInPageTests` exists
  because of it.
- The entry's left edge was `var(--rule-weight) solid var(--accent)`, which is **1px in Hero and
  2px in Villain**, and gold on the light sunk ground **measured 1.57:1** — no edge at all. It
  looked entirely deliberate in the Villain proof, where the same declaration measured 5.62:1. It
  is now `3px solid var(--heading)`: 6.76:1 and 5.62:1, both measured, and 3px in both.

**The contrast probe was wrong before it was right**, and the way it was wrong is worth recording:
computed colours come back as `rgb(0–255)` *or* `color(srgb 0–1)`, and reading both on one scale
measured everything against black and reported 1.00 for a pair that is plainly legible. It carries
a white-on-black positive control now, which must read 21.

**`CouldOverride` in `WebPresentationTests` had a real over-broad rule**, found by this slice
needing it: `border-radius` starts with `border-`, so the helper reported that a radius was what
the cascade resolved for a `border-left` and refused to answer about correct CSS. Four names are
now excluded — radius, collapse, spacing and image are separate properties sharing a prefix. This
*narrows* a guard, so the three real overrides were re-checked afterwards and all three still
refuse.

**Two reviewers who knew nothing about this found eight things, and one was a real leak.**

- **The rulebook reader cached the book's text across a sign-out**, in the same tab. Its own
  comment claimed the cache was "per visit and per scope, so signing out and back in re-asks" —
  and **Blazor WebAssembly has one DI scope for the life of the app**, so a scoped service is a
  singleton and signing out is SPA state with no reload. A signed-in visitor on a shared machine
  could open a Power's entry, sign out, open the same Power, and be handed the book's own text
  out of the dictionary with the server — which would have refused — never asked. Reproduced
  first, then fixed by comparing the identity key rather than trusting an event to be raised: a
  guarantee that depends on an event is one somebody can remove by editing another file.
- **The magic link's domain came from the request's own host.** The CSRF check compares the
  `Origin` header *against that host* rather than validating the host, so where more than one
  hostname routes to the Function a caller who could influence it received a link minted for it —
  carrying the raw token. `SITE_URL` is now required and the server refuses to send without it,
  the same way `RulesLocation.Find` refuses rather than guessing.
- **The body cap counted UTF-16 code units while its comment said bytes**, so a body padded with
  astral-plane characters reached about twice the limit. The existing test padded with ASCII,
  where the two measures agree, which is exactly why it passed.
- **The per-client rate limit had no test at all** — deleting half the guard left all 36 passing,
  because nothing in the suite set the header it keys on, so every call counted as one `unknown`
  source. It has two tests now, and the second asserts `X-Forwarded-For` is *not* read: anybody
  may write that header, and reading it would be a limit somebody steps around with a string.
- **`login_attempts` was never swept** — one permanent row per address and per source ever seen.
  Nothing looked wrong, because the counting stayed correct; what grew was the table.
- **The banner's two controls were untested.** Hardcoding the account link to "Sign in" and
  making `Pressed` always return `"true"` — both buttons announcing pressed, which is invalid
  ARIA — each left all 333 tests green. The cause is worth recording: `Find(".banner-link")`
  returns the **first** match, and `AreaTests` uses it for the link immediately before this one.
- **`aria-controls` had no guard**, so making it unconditional — naming an element not in the
  document while closed — passed. Now asserted absent when closed and resolvable when open.
- **The address-contract regex had a character-class hole**: `[a-z/]` meant renaming a route to
  `api/auth/verify-token` matched nothing, so it was dropped from the list and the test passed
  while the two halves genuinely disagreed. A pattern that answers "not an address" when it means
  "I cannot read this" is worse than none.

**And one fault was in the test stub rather than the code**: `FakeApi`'s verify route answered a
hardcoded identity whatever it was asked, so a test about two accounts on one machine was quietly
a test about one. All seven new guards were then mutated and all seven bite.

**Then a third reviewer was pointed at the fixes rather than the code, and six of the nine did not
hold.** This has been the highest-yield reviewer for six sessions running and it earned it again:
every one of the six caught only the mutation it had been shown, and a *variant* reaching the same
end state walked past it with every suite green. What was wrong was the same thing each time — the
guard asserted the absence of one spelling instead of the property.

| The fix | The variant that got past it | What it is now |
|---|---|---|
| `SITE_URL` required | trust `X-Forwarded-Host` *as well*, leaving the refusal intact — mailed a link to `evil.attacker.test` | the link's origin must **equal** `SITE_URL`, with six hostile host headers set |
| the cap counts bytes | "correct" the count by +2 per surrogate pair — right for emoji, wrong for CJK — stored 307 KB | asserted with a three-byte character as well as a four-byte one |
| `X-Forwarded-For` not trusted | trust `X-Real-IP` too — 26 links against a cap of 20 | nine spoofable headers varied at once |
| `login_attempts` swept | restrict the `DELETE` to `key LIKE 'email:%'` — 51 stale `ip:` rows | both kinds of key asserted by name |
| the identity contract | return `key: null` — same field names, so the name-comparison passed | the key must be the account's own id, and match `/api/me` |
| `CouldOverride` narrowed | `all: unset` after the rule — the box lost background, padding and edge in any browser | `all` overrides everything, checked first |

**The `key: null` one is the worst-shaped of the six**: the server would establish the session and
set the cookie while the client read a null key as "nobody is signed in" — a live session its owner
is told they do not have. A contract on field names is not a contract.

**It also found a second lie in the stub, live and uncovered:** `FakeApi` had one character slot
shared by every account, so any test of two accounts against the character store would have been a
test of one — and would have passed against a server with no notion of ownership at all. The real
server's own tests do cover ownership, so nothing in production was wrong; what was missing was the
ability to tell. `TwoAccountsOnOneMachineDoNotShareACharacter` is that ability.

All six variants were then re-run against the strengthened guards and all six now go red.

**What is not done, and needs the account owner rather than a commit:** none of it runs until a D1
database, a binding named `DB`, a Resend key and the DNS records for a sending domain exist.
[`docs/ACCOUNTS-SETUP.md`](docs/ACCOUNTS-SETUP.md) is the five steps, and a test asserts it names
every environment variable the server actually reads — documentation of a configuration nobody in
this repository can try out rots silently otherwise.

**One cost, stated rather than hidden:** a failed save is silent. Local storage effectively cannot
fail; a network can, and the character then exists only in that tab.

> **"Saved" feedback — done, on a branch not yet merged.** `MainLayout` shows the word beside the
> account link once `CharacterSession.Saved` reports a write-through completed, and nothing before
> that — there is deliberately no "saving…" state, since nobody outside the store knows how long a
> write takes. It does not solve the *failed* save this paragraph is actually about: `SaveAsync`
> still never throws, by design (see `ICharacterStore`'s own doc comment), so a save that genuinely
> fails still says nothing. What it buys is the more common gap — nobody could tell a *successful*
> save had happened either, which read as no feedback at all rather than as silence about failure
> specifically.
>
> **The race worth recording:** two saves can be in flight together (a slow account save from one
> edit, a fast one from the next) and finish in either order. `CharacterSession.Version` — bumped on
> every `NotifyChanged` — is what a completed save is checked against, rather than a bare
> `bool` latched by whichever event happens to run last; a stale completion cannot un-confirm a
> newer one. `SaveStatusTests.AStaleCompletionCannotUnconfirmANewerSave` pins it, though the
> harness environment resolves the local-storage path synchronously, so what it actually exercises
> is the version comparison rather than a truly overlapping pair of writes — the closest anything
> here gets to a real out-of-order race without a controllable double for `ICharacterStore`.


### One site, two areas: the play aide and the portfolio

**The first piece of actual reorganisation rather than polish**, asked for by the repository's
owner, who observed — correctly — that Phases 0–3 improve what is on screen without ever asking
whether the right things are on screen.

The tool and the demonstrations *of* the tool were the same screens. Somebody who came to build a
character walked past a recording of a stranger's conversation and a pair of pre-made characters to
reach the tier list, and the chrome above every page was the character generator's: six numbered
steps, one of them marked as the step you are on, offered to a portfolio visitor who is not taking
any of them.

- **`Areas.Of` answers which half an address is in**, from the first path segment, once, and
  case-insensitively because Blazor's routing is. `MainLayout` draws the step band and the budget
  strip only in the tool. The banner's cross-link points at the half you are *not* in — it used to
  say "Watch one being built" from everywhere, so the only cross-link a player ever saw pointed
  away from what they were doing.
- **The two samples moved to `/portfolio`; "Start a new character" stayed**, because throwing away
  the character you are building belongs to building. The recordings moved under
  `/portfolio/replay`.
- **The old `/replay` addresses are still portfolio addresses, and that is a fix rather than a
  courtesy.** Moving the route made them Play, so the budget strip — the visitor's *own* character
  — came back over somebody else's recorded one with nothing saying whose was whose, which is the
  exact fault it was hidden there to prevent. A shared link that still works but arrives wearing
  the wrong chrome is worse than one that breaks. It was caught by a test, not by looking.
- **`HasSomethingToLose` moved onto the session.** Three pages now ask it before replacing a
  character and two copies had already drifted apart by a field.

**Three mutations, three caught** — dropping the legacy prefix, matching by `StartsWith` so
"portfolios" would be captured, and an ordinal comparison so a capitalised link wears the wrong
chrome. One first attempt was a no-op that passed and had to be re-applied properly.

### The storage and identity seam

**No accounts yet and no behaviour change** — every visitor is anonymous and the character is in
this browser exactly as before. What changed is that the shape is now the one accounts need, which
was the owner's explicit choice over building the split first and retrofitting later.

`ICharacterStore` is the smallest thing every caller uses, so a server-backed store is a
registration change. `IIdentitySource` answers who the character belongs to, asynchronously
because a real one has to ask something — making it synchronous now would mean changing every
caller later, which is the whole point of the seam. It carries a key and a name and deliberately
no claims, token or expiry: **the wrong authentication model is harder to remove than none.**

**The anonymous key stays `pp.character.v1` exactly**, which is the compatibility promise —
suffixing it for consistency would empty every returning visitor's browser, silently, looking like
storage cleared rather than a bug. An account's characters land beside it, so signing in on a
shared browser cannot overwrite what the anonymous visitor was building, and clearing one slot
leaves the other. Five tests pin it, with a positive control that the shipped identity really is
anonymous.

**The larger half is now done** — see [accounts](#accounts-one-character-one-account-and-the-book-behind-a-sign-in)
below. The seam held: the two interfaces did not change shape, and the app's behaviour for a
visitor with no account is byte-identical to what it was.


### Phase 3, second slice: the pips become the control

They were `aria-hidden` decoration beside a `+`/`−` stepper, so the only way from 2d to 9d was
seven clicks. Clicking the fifth pip sets 5d, the arrows move by one, Home and End go to the ends.

**`role="slider"` rather than a radio group.** A rank is a value on a bounded, ordered range, and
one focusable element beats twelve — eighteen Traits would otherwise add 216 tab stops. The stepper
stays: it is discoverable, it is a bigger touch target, and a slider beside its own buttons is an
ordinary pairing. The individual pips stay `aria-hidden`, because they are the slider's own
rendering and a reader told "4d Noteworthy, slider" does not also want twelve unlabelled children.

**The announced minimum is the package floor where there is one**, not the Trait's own 1d, because
a package's granted ranks cannot be lowered below the package rank. A slider announcing a bound it
will not go to tells a screen-reader user something untrue about the control in front of them, and
clicking a pip below that floor clamps up rather than asking for a rank the validator would then
report.

**Nothing suppresses the browser's default on those keys through Blazor, and that is not a gap —
it is the wrong layer for it.** Blazor fixes `preventDefault` at render time rather than per event,
so suppressing it on this element would also swallow Tab and trap focus inside a rank row — much
worse than what it would fix. Left and Right need no such thing: the pips are horizontal and a CI
harness holds this app to no horizontal overflow, so those two scroll nothing regardless.

> **Home and End — done, on a branch not yet merged.** `Sliders`/`wwwroot/js/slider.js` is the
> small interop shim this paragraph said would close it: one native `keydown` listener per rank
> row, attached once on first render, answering to exactly Home and End and nothing else. It
> follows the same guarded-interop shape as `Motion`, `Shortcuts` and `Theme` — a `try`/`catch`
> swallowing a missing script rather than throwing out of every rank's render. bUnit cannot see a
> real `preventDefault`, so the meaningful proof is a browser harness, `proof-slider.html`, driven
> in the build workflow the same way `proof-motion.html` and `proof-shortcut.html` are; a mutation
> that suppressed every key (not only Home/End) was applied and watched the harness fail on
> exactly the Tab-trapping case this note has warned about for three sessions.

**Five mutations, five caught** — the minimum ignoring the package floor, the keys bypassing the
clamp, a click off by one, the pips exposed as twelve children, and a handler answering every key.

**And one defect found by measuring rather than by looking.** The same slice enlarged the click
target by giving each pip padding, with `background-clip: content-box` intended to leave the fill
as drawn. A probe reading `getBoundingClientRect` against the computed padding and border showed it
had not: these are `border-box`, so the pip's box went 7px to 11px and its painted fill went 7px to
5px, with the border no longer hugging it. That is a deliberate design quietly altered to fix a
secondary concern, invisible to every test here and about two pixels to the eye. Reverted. If it is
revisited, **measure the painted width rather than reasoning about the box model.**


### A skip link, and the shell's landmarks — done, on a branch not yet merged

There was neither before this. A reader tabbing from the address bar met the banner's two links and
five buttons, then the step band, then the sticky budget strip, on every single route, before
reaching anything the page was actually about.

- **`<a class="skip-link">` is the first thing `MainLayout` writes**, before the banner — order is
  the whole of what makes it a skip link rather than a link with the right words in the wrong
  place. Off-screen by `transform`, not `display: none`, so it stays in the accessibility tree and
  reachable by keyboard while invisible; `:focus` brings it on screen. `<main>` carries
  `id="main-content"` and `tabindex="-1"` so the jump actually moves focus rather than only
  scrolling — a plain anchor jump to a non-focusable element moves the viewport and leaves the
  caret wherever it already was.
- **The landmarks were already mostly right** — `<header>`, two `<nav>`s each with their own
  `aria-label`, one `<main>` — this only added the id/tabindex and a test that pins the count and
  the naming on more than one route, since the step band's own `<nav>` only exists once a tier is
  being built.
- **`LandmarkTests` and `WebPresentationTests.TheSkipLinkIsOffscreenUntilFocused`** cover the
  markup and the CSS separately, for the reason this file states everywhere else: a rendering test
  cannot see whether a rule actually hides or reveals the link, and a source-reading test cannot
  see where an element landed in the render order. Mutations applied and watched fail: moving the
  skip link after the banner, removing a `<nav>`'s `aria-label`, and deleting the `:focus` rule.


### Tooltips, and the attribute that is not one

**A `title` attribute is not a tooltip, and that is the whole reason this is a component.** It
never appears on a touch screen, is unreliable for keyboard users, cannot be styled, cannot be
dismissed, and is announced inconsistently by screen readers — and none of that is visible to a
compiler, to a rendering test, or to somebody reading the markup and finding it perfectly
reasonable. It is the easiest way to undo this work because it is the obvious thing to write, so
there is a guard refusing the attribute outright across every component.

**The trigger is a real button**, which is what makes the tip reachable without a mouse at all: a
tap focuses it and focus opens it. A `<span>` with a mouse handler is a tooltip only for people
using a mouse.

**The handlers are on the wrapper, not the button.** WCAG 1.4.13 asks for hoverable, dismissable
and persistent; a tip that vanishes when you move the pointer towards it fails the first. Escape
covers the second — a tip that can only be closed by moving a pointer is not dismissable by
somebody who is not using one.

**The tip is always in the document, hidden by a class — deliberately the opposite of the budget
breakdown.** There, `aria-controls` is written only while the target exists, because it genuinely
does not. Here an `aria-describedby` pointing at nothing whenever the tip was closed would dangle
for all but a moment, and a description a reader has to *hover* for is one a screen-reader user
never gets. Hidden by `visibility` and `opacity`, never `display: none`, which would take the
accessible description with it while leaving every rendering test green.

**The id is derived from the term rather than generated.** A fresh one per render would break the
replay's strongest guard, which renders one character twice and requires the two pages to be
identical — it would report a difference on every run that is not one.

**Both call sites are supplementary and a test says so.** The Trait Cap in the budget breakdown and
the no-limit toggle on the tier page; the Trait Cap's *figure* is asserted still printed beside its
tip, because the failure mode of adding a tooltip is quietly moving something into it, at which
point the readers who cannot open it have lost something that used to be on the page.

**Five mutations, five caught — after one false pass that exposed a real hole.** Moving the hover
handlers onto the button reported as a survivor, because the mutation had only *added* them and
left the wrapper's in place; `git diff --numstat` showed additions only. Applied properly it was
caught — but only after the test was strengthened, because the original asserted enter and leave
and never the structural fact that delivers the behaviour: `mouseenter` does not bubble, so what
keeps the tip open under the pointer is that the tip is *inside* the element carrying the
handlers. The test now asserts that containment.

**And one defect no test could have found: the tip opened upward.** That is the conventional shape
and it is wrong here — the budget breakdown hangs off a strip stuck to `top: 0`, so an upward tip
is clipped by the window edge exactly where it is most likely to be opened. It opens downward now.
Found by rendering one and looking at it.

**Deliberately not built: a Power's rulebook text on hover.** `data/rulebook/` has the prose and is
deliberately not in the browser payload, so that wants the reference surface rather than a tooltip
parameter. See the handover.


### The palette becomes the character's, and the Hero Point limit becomes its own toggle

**Asked for by the repository's owner, and it reverses an entry in the settled list.** Villain used
to mean two things at once — a colour scheme *and* no Hero Point budget — and `CLAUDE.md` refused a
mode field on `CharacterSheet` for exactly that reason: the second half is mechanical, and a
mechanical flag on the sheet is the browser deciding a rule. Splitting them is what makes the field
defensible, and the split is the substance of this change rather than a side effect of it.

**`IsVillain` and `UnlimitedBudget` are on the character, and nothing in the rules can see them.**
The first is why: an exported sheet should still be a Villain when it is read back, which a palette
held beside the character could never manage because it was never in the file. `PresentationFlagsTests`
asserts that no file under `engine/` or `sheets/` so much as names either field — **with a positive
control, which is not optional**, because a scan for two names is satisfied completely by two names
that no longer exist. Both were proved by mutation: a `Lenient(sheet) => sheet.IsVillain` dropped
into `CharacterValidator` was named and refused, and renaming a flag in the guard's own list failed
the control.

**Ch.9 builds Villains by exactly the Hero rules**, so "no budget" was never a fact about Villains —
it is a GM building to whatever the scene needs, which a Hero campaign does too. The sandbox is an
independent toggle on the tier page, where the budget is introduced. A Villain can be held to a
tier's points; a Hero need not be. Three tests encoded the old conflation and were rewritten rather
than deleted, including one whose *name* asserted the opposite of the new behaviour.

**Without a limit the strip is a running total rather than absent.** Absent was the old behaviour
and it took the breakdown with it, so somebody building without a limit lost the one panel that says
where the points went. No cap, no remaining figure, and no rail — a `progressbar` needs a maximum to
be a proportion of, and one drawn against the tier's points would put back on screen the limit that
was just switched off, while announcing a figure to a screen reader that nothing is measured
against.

**The validator is still never told, and still reports the finding.** The browser shows a total and
`build --from` reports everything the engine returns: a report that dropped a finding on the
strength of a flag in its own input would be worth less than no report. The engine answers; hosts
present.

**One route puts the palette on the document, and the guard is what found that.** Three call sites
used to push `ppSetMode` themselves — the tier page's samples, the replay hand-off, and the switch.
The palette follows the character now, so the layout applies it on the render after any change of
character, and the other three are gone. That made the call *render-reached*, at which point
`TheAppsOwnScriptsAreCalledOnlyThroughMotion` failed: `ppSetMode` had been on its by-hand allow-list
as a call only ever reached by a click, and that claim had just stopped being true. It goes through
`Theme` now, guarded like `Motion` and `Shortcuts`; unguarded it would have thrown out of every
render of the shell. **The allow-list shrinking is the point** — an entry on it is a claim, not a
permission.

**One mutation reported a false pass and had to be re-run.** The first attempt at the validator
mutation used `perl -0pi`, which on this machine exits 0 and edits nothing; `git diff --numstat`
printed no change and the guard "passed". `CLAUDE.md` records that exact trap. Check the numstat
every time.


### Phase 3 of the front-end plan, first slice: the command palette

`Ctrl-K` from any route opens a box that offers the six creation steps and, once something has
been typed, the Powers. It is the plan's own "single highest-leverage affordance" and the first
of Phase 3's two slices; the pips-as-control, keyboard navigation inside the option lists,
validation on the row where the mistake is made, and undo are the second.

**The matching rule is the lists' rule.** `OptionFilter` grew a static `Matches` that its
instance `Admits` now calls, so the palette and the five pickable lists cannot answer the same
query differently — a reader who has learnt that "plast" finds Plasticity in the Powers list has
learnt something about this app, not about one list. The tallying wrapper stays where it was,
because a list counts what it drew and a palette does not.

**The six steps moved out of the step band and into `Commands`, which both draw.** Two lists
would drift, and a palette offering a step the band does not have — or missing one it does — is
worse than no palette. The test compares what each actually renders rather than reading the
source.

**Choosing a Power requests it; it never adds it.** A Power needs ranks, variants and its Pros
and Cons chosen, and the editor is the one component that knows how to price them, so the palette
hands the Power over and changes nothing about the character. The request is **read once**: the
step holding the editor re-renders on every keystroke elsewhere on it, and a request that stayed
set would reopen the editor over whatever the reader had moved on to, repeatedly, with nothing on
screen explaining why. The step above it *peeks* to decide which section to show and the section
itself takes and clears — consuming it in the step would leave the reader on the right tab with
nothing open, which is the failure that looks most like the feature working.

**Interop goes through `Shortcuts`, the same bargain `Motion` makes.** All three calls are
reached from a render, so a `palette.js` that 404s or fails to parse would otherwise throw out of
`OnAfterRenderAsync` on every render of the layout, which is every page. A missing keyboard
shortcut must not take the app with it: the six steps are still one click away in the band and
every Power is still in the list on its own step.

**The component dispatches its whole event handler, not just the redraw.** The key listener
arrives from the browser rather than from Blazor, and Blazor refuses off-dispatcher state changes
outright when it can tell — which is how this was found, as a real fragility rather than a test
artefact.

**Nine mutations, nine caught, and one defect found only by looking.** Six against the rendered
component and its service — Powers offered to an empty box, arrows clamping instead of wrapping,
`aria-selected` frozen while the ring still moved, the request peeked instead of taken, the band
growing a step list of its own that had drifted, and the palette matching by a rule of its own —
and three against the script, driven in Chrome: the chord no longer taking the key from the
browser, the listener firing on any `k`, and focus dropped on the body instead of restored.

The tenth finding had no test at all. **The current-row marking was a `--rule-weight` hairline,
legible in Hero and very nearly invisible in Villain**, where `--accent` sits on a near-black
ground — and that edge is the only thing on screen saying what Enter is about to do. It is now
the 3px edge `.option` already marks a chosen row with. Found by screenshotting both palettes and
opening both, which is the rule this project keeps re-learning.

**A sixth browser harness, `proof-shortcut.html`, and it exists because bUnit cannot reach the
door.** Ctrl-K is heard by a listener on the document, which no render tree contains, so every
assertion about what the arrow keys do sat on top of an opening chord nothing checked — the exact
shape of gap this repository has shipped four times. It drives the real `wwwroot/js/palette.js`
with synthetic key events, asserts the negative cases (a bare `k`, a `Ctrl-J`, a second
registration) because without them a listener that fires on every key passes the lot, and checks
focus in both directions, since a palette that takes focus and does not give it back is a
keyboard trap. `ppPaletteStats` is the positive control, asserted before anything that depends on
it. It runs in CI beside the other five.

**What it deliberately does not do.** No preventDefault inside the component: Blazor decides that
at render time rather than per event, so a blanket suppression on the box would stop the letters
reaching it and the palette could not be typed in. Nothing here computes a Hero Point, and the
palette is a way to reach a control rather than a second place a character can be changed.


### Qodana's 32 findings on #46 — and two were real defects, not style

All 32 were in test files, none in production code, and Qodana runs in PR mode so several were
pre-existing rather than new. Most were spelling: seven redundant `using` directives, six redundant
verbatim prefixes, a redundant name qualifier, a redundant default argument.

**Two were worth the scan on their own.**

- **`EffectiveValue` carried two `<summary>` blocks.** The doc for `ScreenHalfOfAppCss` — eighteen
  lines explaining why the `@page` box is stripped, and recording that an earlier version of the
  note cited a compensating check that did not compensate — had been **stranded 175 lines from its
  method** by an insertion, leaving `ScreenHalfOfAppCss` with no documentation at all and
  `EffectiveValue` with two summaries and an unclosed `<para>`. This is the exact defect
  `CLAUDE.md` already records from an earlier slice, repeated by the same mechanism: line-based
  splicing. **The compiler sees none of it**, and neither does any test in this repository.
- **A lambda parameter named `rule` shadowed a `Match` named `rule`** eight lines above it, in the
  same method, with different types. Renamed.

The seven `AccessToDisposedClosure` were fixed by removing the capture rather than suppressing the
inspection — the fill actions take the sheet as a parameter now, the Power is resolved before the
render that used it, and the interop counter is a local function over `ctx.JSInterop` rather than a
delegate over `ctx`. `CLAUDE.md` asks for a rationale beside any deliberate exception; none was
needed, because none of these needed an exception.

### Phase 2 of the front-end plan: motion that carries meaning — **in progress**

**The first thing found was that the guard this phase must not break did not exist.** The handover
names `proof-sticky.html` as the measured check on the budget strip — the strip stays put only
because its containing block is the document, and View Transitions is precisely the change that
would wrap it. The file was on disk and **had never been committed**: no generator in `ProofPages`,
`web/wwwroot/proof-*.html` is gitignored, and regenerating the proofs deleted it. So did
`proof-measure.html`, `proof-narrow.html` and `proof-narrow-shell.html`, the three harnesses
prerequisite 8 leans on for the 375px measurements. Four measured checks the handover treats as
standing were one `PP_PROOF=1` run from gone, and absent entirely in a fresh worktree.

`TheStickyStrip` is now a generator beside the others, so it survives a clean checkout, and
`TheStickyHarnessMeasuresRatherThanAsserts` runs on every build — not under `PP_PROOF`, which is the
mistake Phase 1's fix-audit found in the marker checks and would have reproduced exactly.

**The harness states a verdict token rather than leaving it to the eye**, so the check is read out of
a dumped DOM instead of a screenshot. The baseline measures `.budget` top at 117.0 before a scroll
and **0.0** after, with `.steps` bottom at −483.0 — stuck, against a page that genuinely scrolled.

**And the instruction for reading it was itself defeatable, which only looking at the dump showed.**
The first version said to assert on `STICKY: PASS` in the page. That string appears **twice** in a
dumped DOM — once as the verdict and once inside the harness's own script source — so the assertion
passes on a harness whose script never fired, which is the failure mode being guarded against. The
verdict is written to `document.title` as well, which the script alone writes and whose resting value
is neither verdict, so the three states are distinguishable. `MustNotShow` refuses a hard-coded
`say(true, …)`; a proof that cannot fail is worse than no proof, because it is read as evidence.

**Item 1, continuity across steps, is in.** The three chrome bands carry a `view-transition-name`,
so the browser matches each to itself either side of a navigation and interpolates rather than
cross-fading the whole window; what is left in the `root` group is the content of `main`, which is
the only thing that changed. `wwwroot/js/motion.js` is 55 lines and the only script added — no
animation library, for the reasons in the plan. The CSP is untouched: `script-src 'self'` already
allows a same-origin file.

**`LocationChanged` is the wrong hook and it is the one already in the layout.** The API animates
between two snapshots and the first has to be taken while the old page is still on screen; by the
time `LocationChanged` fires there is nothing left to capture. `RegisterLocationChangingHandler`
runs before the navigation, so `begin()` snapshots and holds the transition open on a promise and
`OnAfterRenderAsync` resolves it once the new step has rendered. The failure mode of getting this
wrong is silent — no error, just no animation, indistinguishable from an unsupported browser.

**Two of the four new guards were theatre, and a variant is what showed it.** Both assert that
`motion.js` *mentions* something — `still()`, `setTimeout` — and both pass against a script doing
the opposite of what it says:

- `if (… || !still()) return` — one character — serves the animation to exactly the people who
  asked for none, and every string assertion still passes.
- `setTimeout(() => {}, 1000)` is a safety net that catches nothing. That one matters more than it
  reads: while a transition is open the live DOM sits behind a snapshot, so a release that never
  arrives leaves a frozen picture of the app with no way back.

**The fix is not two more string assertions.** The property is behavioural, so the instrument has
to run the code — the same class of mistake as `Contains`, one level up. `proof-motion.html` loads
the shipped `motion.js`, stubs `startViewTransition` to observe it, and asks three questions: does
`begin()` open a transition, does `end()` release it, does an unreleased one free itself. It is run
twice, the second under Chrome's `--force-prefers-reduced-motion`, **and every expectation inverts**
— which is the half no source scan can reach. Re-run against both variants it catches both, and
discriminates: the inverted gate fails checks 1 and 2, the dead timer fails only check 3.

**Item 2 is in, on a clock a test can seek — and the first explanation of why was wrong.** The
counting figure was written on `requestAnimationFrame`, found to be unverifiable, and parked with
the note that "rAF does not fire under `--headless=new --dump-dom`". **That named the wrong cause
and would have misdirected the next session**, because it points at the dump mode. The cause is
`--virtual-time-budget`: it suppresses frame production, so neither rAF nor the document timeline
advances, while `setTimeout` continues to fire. Measured both ways —

| flags | result |
|---|---|
| `--screenshot --dump-dom`, no virtual time | `RAF-FIRED-1` |
| `--virtual-time-budget=8000` + any of `--dump-dom`, `--screenshot`, `--run-all-compositor-stages-before-draw` | `NO-FRAME`, and `waapi=running@0` |

**That matters beyond this feature**: every screenshot in this repository needs
`--virtual-time-budget`, because `.panel` animates from `opacity: 0` and a bare capture photographs
it mid-animation. So the flag this project cannot work without is the flag that makes frame-driven
animation unobservable. Anything animated here has to be checkable by *seeking* rather than by
waiting.

Which is why the counting figure is `element.animate()`. Not because rAF is impossible — it runs
perfectly in a real browser and still pumps the redraw — but because a WAAPI animation's
`currentTime` is **settable**, and `draw()` is a pure function of it. Seeking the clock and calling
the same `draw` a visitor's frame calls is a test of the shipped path. Seeked at t = 0, .25, .5,
.75, 1 the figure reads **10, 16, 19, 20, 20**: in bounds, monotonic, and resting on the engine's
number.

**Every harness now carries a positive control, and the reason is a tally rather than a
principle.** Three separate checks in this slice passed because the feature under test never ran —
an inverted gate meant no transition opened, an unlinked stylesheet meant no count started, and an
iframe that fails to load reports `clientWidth === scrollWidth` over an empty document, which is a
clean "nothing overflows". So each harness asserts the work happened — `ppMotionStats.transitions`,
`ppMotionStats.counts`, an element count, a scroll position that actually moved — before asking
whether the outcome was right.

**All five were then deliberately broken, and the exercise paid for itself immediately.**

| break | caught by | result |
|---|---|---|
| `.budget { position: static }` | sticky | `STICKY: FAIL`, others unaffected |
| `.panel { min-width: 460px }` | both narrow harnesses | `NARROW: FAIL` ×2 |
| `.budget-strip { padding-left: 60px }` | insets | **survived at first** |
| the reduced-motion gate inverted | motion, both modes | `MOTION: FAIL` |
| the count rests one short | motion | `at t=1 showed 19`, and the monotonic check too |

**The third one is the finding.** `getBoundingClientRect()` returns the *border* box, so padding
moves the content on the page without moving the number the harness read: one band sat 60px out of
line and the spread still reported `0.00px`. It measures the content edge now. Nothing but breaking
it would have found that — it had been passing, against a real layout, the whole time.

A sixth mutation — deleting the assigned resting frame so the last value is interpolated — was
**not** caught, and that is correct rather than a hole: the easing reaches exactly 1 at `t=1`, so
`Math.round(from + (to - from) * 1)` is already `to`. The mutation changes nothing observable. It
is recorded because "a guard missed this" and "this mutation was a no-op" look identical in a



**The adversarial round: two reviewers, ten findings between them, every one demonstrated by
mutation rather than argued.** Three were bugs in shipped code, not in the guards — the rate this
project records for new guards ("a third to a half are theatre") held, and understated it.

**What was wrong with the code:**

- **`ppCount` leaked one live `Animation` per count.** `fill: "forwards"` keeps a finished
  animation *relevant*, so it stayed attached to the single `<strong>` in the budget strip —
  measured growing 1, 2, 3 … 10 over ten counts, for the life of a session. The cleanup now hangs
  off `clock.finished` rather than the rAF pump, which also makes it *drivable*: frames are
  exactly what `--virtual-time-budget` suppresses, so a tidy-up tied to the pump could not be
  tested at all.
- **An interrupted count could come to rest on a figure the engine no longer returns.** The
  cancel sat below the early returns, so a call taking the immediate path left an older count
  pumping — and a probe showed the abandoned count writing **100** after the newer one had settled
  on **42**. That is the shape `CLAUDE.md` forbids in as many words. The cancel is above every
  early return now.
- **`motion.js` had quietly become load-bearing for navigation itself.** `OpenTransition` awaits
  interop on *every* internal navigation and `ChosenList` on every render; a 404 or a parse error
  would have thrown out of both. `Motion` swallows the script's failure — **a service rather than
  a `try` at each call site, for the same reason `ReplayLibrary.LoadAsync` is a method**: a block
  inside a component is where nothing can reach it.
- **`HpBudgetBar` held an `@ref` to an element Villain mode does not render** and called interop
  against it on every change. Blazor never clears an `@ref` when its element stops rendering, and
  the call was absorbed by `ppCount`'s null guard — invisible, and load-bearing without anybody
  having written that down.

**What was wrong with the guards — eight of them:**

| guard | it passed while… |
|---|---|
| the `LocationChanging` hook | the snapshot was taken from `LocationChanged` instead — **no cover at all**, suite and all six harnesses green |
| `view-transition-name` uniqueness | one name sat on `.panel`; driven Chrome returns `InvalidStateError` and abandons every transition |
| the reduced-motion gate | a new ungated entry point was added; its helper anchored on the first `animate(`, **which is in the file's header comment** |
| the safety-timer ordering | it compared indices against that same comment |
| the narrow harnesses | `overflow-x: clip` hid 352px of unreachable content; and leftward overflow is invisible to both measurements |
| the insets harness | one band ran 96px short, printed in its own evidence |
| the sticky harness | `position: fixed` reported as `sticky`, because `before` was measured and discarded |
| every proof page | the app's own `<div id="app">` wrapper was missing, so ancestor-borne faults could not be seen |

**The bUnit test written to close the worst of those passed against the mutation on its first
attempt**, because it recorded the address from its own handler rather than correlating with the
moment `Begin` ran. It counts interop calls already made when a `LocationChanging` handler fires,
which is the only thing that separates the two hooks.

**Two findings are recorded rather than fixed, and deliberately.**

- **A held-open transition swallows pointer input.** Measured: `elementFromPoint` over a button
  returns the `::view-transition` overlay rather than the button, for ~260ms normally and up to
  1000ms if `end()` never arrives. `pointer-events: none` on the pseudo would let the click
  through — **to the new page, while the visitor is still looking at a snapshot of the old one**,
  which trades a dead click for a wrong one. 260ms of inert overlay is what every implementation
  of this API does. Recorded with the numbers so the next session can weigh it rather than
  rediscover it.
- **There is no `aria-live` anywhere**, so the counting figure spams nothing — but crossing into
  over-budget is announced to nobody either.

  > **Done, on a branch not yet merged.** A `sr-only` sibling of `.budget-figure`, never inside it
  > — exactly the placement this bullet asked for. Its text is computed by a method that compares
  > the current over-budget state against what it was last time and only writes new words on an
  > actual flip, so an ordinary change in spend that leaves the character on the same side of the
  > line says nothing twice, and loading an already-over-budget character announces nothing at
  > all (there is no crossing to describe — it arrived that way). `BudgetStripTests` pins both
  > halves, and a mutation that inverted the flip check — announcing on *no* change instead of on
  > a real one — was applied and watched the crossing test fail.

One latent defect is also recorded: `_midTransition` is released by any render of `MainLayout`,
not specifically the navigation's, so a render batch flushing in between would close the
transition early and snapshot the old page twice. Not reachable today — no step-navigation path
writes to the session before navigating — and it becomes live the first time one does.

**The fix-audit — a reviewer pointed at the fixes rather than the code — was the most valuable of
the three, and its first finding was a bug the previous round had *introduced*.** Of the ten fixes
audited, three held, one held while shipping something worse, and six did not hold. That is close
to the rate this project has recorded four sessions running, and it was found by asking for a
*variant* rather than a re-run.

**Severity 1: the counting figure came to rest on the previous Hero Point total.** Moving the
tidy-up onto `clock.finished` calls `clock.cancel()`, after which `currentTime` is `null` — and
`draw` read that through a nullish default as `t = 0`, so a pump frame still scheduled at
completion wrote the *old* figure back over the answer and rescheduled itself for ever. Worse than
the leak it replaced, live on the branch, and **found with no mutation applied at all**.

The reason nothing saw it is the reason the fix was made in the first place:
`--virtual-time-budget` produces no frames, so no pump was ever pending in any driven check. The
property that made the cleanup testable is the property that hid the regression. It is fixed four
ways, each sufficient alone — a null `currentTime` reads as the end rather than the beginning, the
pump stops on it, the pump checks it still owns the element, and the finished handler cancels the
pending frame — and the check that catches it needs no frames: capture the record's `draw`, finish
the clock, call `draw` once more. Against the buggy version it reports *"settled on 60, then a late
frame showed 40"*.

**What else did not hold, and the variant that showed it:**

| fix | the variant that walked past it |
|---|---|
| `view-transition-name` on a singleton selector | the rule put in `theme.css`, which was never read — and the same rule spelled `VIEW-TRANSITION-NAME`, which CSS treats as identical and a case-sensitive regex does not |
| the cancel above the early returns | moving it *below* them: the harness's interrupt used 78→42, which never takes an early return, while `HpBudgetBar` produces `from === to` routinely |
| every animation gated on `still()` | a module-level helper — `EnclosingBlock` still fell back to returning the whole file, which contains `still()`; and only `motion.js` was scanned |
| `Motion` guarding the interop | calling `Js.InvokeVoidAsync("ppLand", …)` directly again: the fix guarded two call sites, not the property |
| the `ShowBudget` guard | correct code with **zero cover** — deleting the line left everything green |
| the insets harness | a margin on a band's first child: the band's own box and padding are untouched, so both spreads still read `0.00` |
| the narrow harnesses | a 300px `::before` at `left: -320px` — `querySelectorAll` returns no pseudo-elements |
| the structural hook guard | a *comment* naming `Motion.Begin()` left in the handler while the real call moved. Its bUnit half caught it; the structural half was worth nothing alone |

**And one the audit found outside the ten:** `Begin(); End();` in the changing handler passes every
check — a transition opened, from the right hook, and released — while animating nothing, because
the second snapshot is taken before Blazor renders. The release belongs to the render, and that is
asserted now.

**Three fixes held under attack**, and the audit said what it tried: the `#app` wrapper (also
confirming that `transform` and `contain` on that wrapper genuinely do *not* unstick the strip, so
the harness's own docstring overstates them), the sticky harness's `before > 20`, and the overlap
check.

**My own new guard was theatre once in this round too.** The release-ordering check read its
counter synchronously after `begin()`, and a release resolves a promise — so it reported zero
whether or not `begin()` had released, and passed the exact variant it was written for. It settles
first now.
**Item 2's last part: a row arriving in a chosen list lands, and `--ease-emphasised` arrives with
it.** Phase 0 withheld that token deliberately — an overshoot curve wants something that should
read as *landing*, and until now nothing did. A row moving from the picker into the character is
the one thing that does, and it is the token's only user; an overshoot on a state change reads as
a wobble.

**Which row is new is decided by a `data-landed` mark in `motion.js`, not by a key in the
component.** Blazor reuses DOM nodes, so the render tree does not answer that cheaply — and a mark
survives something a key does not: a filter re-ordering the list is not twelve arrivals.
`firstRender` is passed *through* rather than used to skip the call, so restoring a saved character
marks its rows without playing a dozen animations at once, and the next genuine addition still
lands alone. Both halves are asserted, and both were broken to prove it:

| break | result |
|---|---|
| land on first render too | `a first render marks rows without landing them` fails, and so does `rows already present do not land again` — 2 animations where 0 belong |
| hard-code the curve instead of reading the token | `effect easing "ease-in-out" vs token "cubic-bezier(0.34, 1.56, 0.64, 1)"` |

The second is the one no CSS test could have caught: the animation is built in script, so a curve
that drifts from the token is invisible to every stylesheet scan. The harness reads the easing back
off the running effect and compares it against the computed token.

`getAnimations()` is the positive control throughout — it asks the browser what is actually
running rather than trusting a counter this code also owns.
**The two source guards on `motion.js` are theatre and stay theatre, so a browser runs in CI.**
They assert the script *mentions* `still()` and `setTimeout`, and both pass against
`|| !still()) return`. `ubuntu-latest` ships Chrome, so the build workflow drives all five
harnesses and requires each to *say* PASS in its `<title>` — asserted on the positive, because a
harness whose script never ran leaves resting text that is neither verdict, and grepping for FAIL
would call a broken harness green. The source guards are kept beside it: they run where the
browser does not, and they now claim only what they can support.

results table and are not the same fact.
**Four harnesses were missing, not one.** `proof-sticky`, `proof-measure`, `proof-narrow` and
`proof-narrow-shell` were all uncommitted scratch. All four are generators now. The restored
measurements: nothing overflows at 375px on either page, and all four chrome bands sit on the same
column to 0.00px.

The source guards are kept beside it. They are cheap, they run in CI where the browser does not, and
what they now claim is only what they can support.

### Phase 1 of the front-end plan: density and hierarchy, which was mostly deletion

Three of the plan's four items in full, the fourth split — see the end of this entry, which says
what was left and why.

**One chrome band, in place of three.** The banner ran full width, then a step list with its own
bottom rule inside the 1100px column, then the budget as a shadowed white card inset from the
window: **about 215px of furniture before the page heading, on every step**, reading as four
stacked pieces. It is **163px** now and reads as one — banner, steps, strip, rail, contiguous and
all full width.

- **The step list and the strip are siblings of `main` rather than children of it**, and that is
  load-bearing twice over. Full width without a bleed: the negative-margin hack that pulled the
  strip out of the shell's padding is gone from three sites, along with the pair of
  narrow-viewport rules that had to be kept in step with it — **so the 8px overflow they caused
  at 375px is now unreachable rather than guarded.** An invariant is better deleted than guarded
  when the thing it constrains can be removed.
- **And a wrapper around both rows would have broken the sticky strip.** `position: sticky` is
  bounded by its parent, so a short chrome `div` holding both would unstick it the moment the band
  scrolled past — the whole span it exists to survive. Measured through an iframe: the strip sits
  at 116 before a scroll and at **0** after scrolling 600, with the step list at −484. Joined the
  band and scrolled away, which is what the plan asked for.
- What replaces the deleted guard is a real requirement in the direction that cannot overflow: the
  shell and all three chrome columns cap on `--column` and reserve the same padding, **discovered
  per media query rather than listed**. It failed on its first run and was right to — the
  narrow-viewport rule pads the three bands in one grouped rule, and the padding reader filtered
  on the whole selector string being equal, which is the **same comma-list weakness a fix-audit
  had found in the sticky-strip guard two commits earlier.**

**Two boxes that were drawn around boxes.** The options scroller carried its own border inside a
panel that is already a ruled box with a heading strip, so a list of rows with their own
separators sat three nested edges deep; only the top rule survives, which does a different job —
separating the list from the filter box, which is a control and not a row. And the derived step
wrapped four ruled figures in a bare untitled panel, a box round four boxes separating nothing.

**Six empty states that said nothing.** "None yet.", "None.", "Nothing yet." — a full stop
restating a fact the reader can already see, on the one screen where a tool is least useful and
best placed to help. Each now names the next action, and where a rule stands behind it, the rule:
the Flaws tab says the rules ask for a minimum at creation and **reads the figure from the same
place its own heading reads it**, and the Gear step says ordinary gear is free so the only thing
that spends Hero Points is a custom feature. They are an `EmptyState` component rather than a
class applied six times — one owner per repeated class, and the guard gets one element to find
rather than an enumeration that goes stale.

**The tab strip marks what is untouched, and only three sections can be marked.** That is a rules
matter rather than a convenience: Ch.2 floors every Ability and Talent at 1d, so a character has
all eighteen and **cannot be without them** — those sections are never empty, and their 0 HP means
a package covered the cost rather than that nobody has been there. Powers, Perks and Flaws are
genuinely collections. The marker is a ring rather than a colour.

**The Sources editor becomes a grid.** Eighteen fields between the two pickers, each a short label
over a 200px control, stacked one per row — the right-hand two thirds of the panel spent on
nothing, and the Talents picker taller than a laptop viewport. Six rows become two, twelve become
three. The rank rows on the tabs are deliberately left alone: those are a table read down.

**Two guards were written after a mutation showed they were needed, not before.** The picker test
rendered `AbilitiesTab`, which is where the app puts the component — and that tab passes
`IsPro="false"` and nothing else, so **the Pro half of the wording was never rendered** and putting
"this Power does" into it passed. And the untouched marker **shipped with no guard at all**; the
negative half of the test that now covers it is the load-bearing one, since marking a Trait section
would report eighteen Traits a character cannot be without as missing.

**Two process failures of mine, both traps this repository had already written down.**

- **The mutation harness reverts with `git checkout -- <file>`, which restores the last
  *committed* state** — and the empty-state edits were not committed when I mutated those two
  files, so the revert discarded them. `docs/HANDOVER.md` says "Commit before letting anything
  mutate files… That has cost rework twice." It is three times now, by somebody who had just
  finished reading it.
- **And I committed the damage, because the command was `dotnet test | grep … && git commit`** —
  which gates the commit on grep finding lines, not on the tests passing. What caught it was the
  new guard **refusing to pass when it could find no `.empty-state` element at all**, rather than
  asserting nothing over an empty set: the "refuse a subject you never found" rule doing its job
  one commit after being written. Every run since captures the output and asserts on the absence
  of `Failed!` before committing.
- A third, smaller: `git diff --numstat` is **blind to an untracked file**, so mutating a
  brand-new component read as "never applied" *and* could not be reverted — the mutation was
  silently left in the working tree, which is worse than either failure alone. Both harnesses
  refuse an untracked target now.

**Verified by looking as well as by testing**, since every visual bug in this project's history was
found that way: both palettes at 1400px, the chrome band and the empty editors read on a rendered
page, 375px measured at `overflow 0px` with the bands correctly edge-to-edge, the sheet still
**three pages in both palettes**, and every one of 75 section children still inset symmetrically.

**A proof of the shell, and a proof of the editors holding nothing** — the two states no page this
harness wrote had ever shown. Every other proof renders components into a bare `.shell` div, so
the three chrome bands never appeared together and "how much does the chrome cost" had no answer;
and every other proof loads a sample, so an empty list was invisible. Part of why six empty states
stayed full stops for so long is that nobody could see them.

**What was left, and why.** The plan's fourth item is "a real grid on wide screens", and it names
**Phase 4** in its own text: above ~1400px the editors and *a live sheet preview* sit side by side.
The preview is Phase 4's, and without it a second column has nothing in it — while at today's
1100px column two editor panels would be ~530px each, too narrow for a Power list with a stat line
under every name. Widening `--column` globally would also widen the sheet and the replay, which is
a design decision Phase 1 has no business making as a side effect. So the half that stands alone
was done (the Sources grid) and the half that needs Phase 4 waits for it. **Item 4 is not
finished; it is split, and the remaining half is listed under Phase 4 in the plan.**

**An adversarial reviewer then found five real defects and demonstrated that six of six of the new
guards held nothing.** The worst of the five was visible in a proof page this change added, and
which I generated and never opened.

- **The chrome had no bottom edge at all on three whole classes of screen.** `.steps` lost its
  bottom rule on the argument that the budget strip beneath carries the edge for both — and the
  strip renders nothing in **Villain mode** (Ch.9 gives Villains no budget), on **the tier page
  before a tier is chosen**, which is the first screen a new visitor sees, and on **every
  `/replay` route**. On all three the step chips sat on the page ground with the heading following
  on the shell's padding alone. `proof-shell-villain.html` showed it plainly; I screenshotted the
  Hero one and wrote "verified by looking… both palettes". **Generating a proof is not looking at
  it.**
- **"Untouched" was inverted for the default path, and my test pinned the mistake.** The claim was
  that Abilities and Talents can never be marked, because Ch.2 floors every Trait at 1d and 0 HP
  there means a package covered the cost. The first half is true of a *finished* character and is
  exactly why a fresh one needs telling; the second is false, because `AbilityCost` walks
  `AbilityRanks`, which is empty on a new sheet — **so no package also costs 0 HP.** The two
  sections that most needed marking were the two forbidden from saying so, on a character the
  engine reports eighteen `TRAIT_BELOW_MINIMUM` errors deep. The predicate is now whether a rank
  is *recorded*, which tells the cases apart properly: choosing a package writes its granted ranks
  into the sheet, so a packaged character reads as touched at 0 HP.
- **The banner was the one band not on `--column`**, while the note in `MainLayout` offered it as
  the example the others follow. Fixed with an inner column rather than a `padding-inline: max(…
  calc((100% − …) / 2))`: the percentage is a raw length the stylesheet's own rule refuses, and the
  wrapper makes the banner structurally identical to the other three bands, so **one guard covers
  four instead of three plus a special case.** The guard then immediately caught that the
  narrow-viewport rule had not been told about it — 24px against 16px, the same figure as the
  bleed bug.
- `.field`'s own margin **doubled the row gap** in the new Sources grid, 32px against 16px; and
  `.options` lost the bottom rule that **marks where 141 Powers are clipped**, so a row cut through
  its own stat line read as a rendering fault rather than as a scroller.

**The six guard survivors, each fixed as a property rather than as a case.** A new breakpoint
widening `.shell` alone passed, because `max-width` was read in the base rules only while the doc
claimed queries were discovered. Centring was not read at all, so a strip with `margin: 0` sat
flush against the window edge with the labels above and the heading below still on the column. The
band elements were outside the guard entirely, so padding on `.budget` shifted the column inside it
and stopped the rail running edge to edge. **Deleting the whole `.empty-state` rule left all eight
`EmptyStateTests` green plus both ownership tests** — the class is still on the element and every
assertion reads markup, which is the `.hp` trap this file records verbatim, reproduced by the change
that cites it. Pinning Powers to permanently untouched passed, because the test filled Perks alone.
Naming the *Ability* in the picker passed, because the ban listed one of three subjects. And
**misstating the creation minimum to the player passed** — the one empty state that quotes a rules
figure had nothing checking the figure.

**Then one more, of my own making and worse than any of them: I fixed the missing edge and wrote no
guard, so a mutation put it straight back.** That is the same failure as the six, one level up —
fixing the defect rather than the class of defect — on the most severe finding of the round. It is
guarded now, together with the three conditions that are the *reason* for it, because a guard whose
premise has quietly gone is worse than none.

**The two proof harnesses genuinely cannot be guarded much, and that is stated rather than papered
over**: they are generators gated on `PP_PROOF`, so with it unset they are no-ops and a reviewer
duly commented out five of six sections with the suite at full count. What is checkable is that a
page which *is* written shows what it claims to — including a negative that catches the dangerous
shape, since wrapping the shell proof in a column defeats its whole purpose and every positive
marker survives it.

**Then a fix-audit, and its central finding is that this was one mechanism rather than twelve
problems.** Of the twelve claims: two held, two did not, six held only against the mutation shown to
them, and two fixes were correct while the defect they repaired reverted green. **`EffectiveValue`
and `HorizontalPaddingTokenOf` each read a single CSS spelling of the property they were asked
about**, so four separate guards fell the same way — `border-bottom-color: transparent` beat a
`border-bottom` check, `border-left-width: 0` beat a `border-left` check, `margin-left: 0` beat a
`margin` check, and `padding-inline` beat a padding check that knew only the physical pair. Closing
the one helper converted four near-misses at once.

- **`EffectiveValue` reads every declaration that decides a property** — the property, its
  longhands, and its logical equivalents — in source order, and **refuses to answer when the last of
  them is a spelling it does not model.** An unreadable answer is a red test, which is the safe
  direction; modelling the whole cascade is a bigger job than any of these guards needs. **Order is
  what makes that correct rather than merely strict**: `.budget-toggle` writes `border: none` and
  then `border-bottom: …`, which the cascade resolves as the author meant, so a check refusing any
  related spelling would fail on correct CSS. It caught exactly that on its first run.
- **A zero width is not a visible edge.** `border-left: 0 solid var(--rule)` contains no `none`,
  names the right token, and draws nothing — and got past both edge guards. Refused in any unit now,
  along with a transparent ink.
- **Two guards were not passing `exact: true`**, so a rule matching no element in this app supplied
  the value they read. That is verbatim the defeat recorded on the `exact` parameter itself from the
  previous audit, reached again by guards written after it.
- **The media-query scan ended at the first newline-brace**, so a query written on one line was not
  found at all and its contents were swallowed into whichever block did end that way — which is how
  a band-only breakpoint evaded a guard whose own comment says the queries are discovered. It
  brace-matches now. **CSS formatting is not a property a guard may depend on.**
- **Four defects reverted green because they were fixed and not guarded**: the options scroller's
  clip mark, the grid-cell margin reset, and the untouched ring's whole CSS rule — **the `.hp` trap
  again, on the sibling of the feature this round had just closed it for.**
- **Two sentence guards were satisfiable with the words wrong.** The picker's ban read the `Target`
  enum, which is better than one word and still not the property wanted: "what this **Trait** does"
  names no enum member and is false of a piece of Gear. And the Flaws figure was checked without the
  claim around it, so *"the rules make them optional, though at least 1 buys extra Resolve"* passed
  while the rules require 1–3. Both pin the clause now.
- **Deleting the `.banner-inner` element while its CSS stayed passed everything**, and the end state
  is worse than the defect it fixed — the banner's contents then have no padding at all. A CSS guard
  cannot see a missing element, so the markup is asserted where the markup is built.
- **And the proof-page markers were near-theatre for a reason I had not seen: they sat after
  `if (!Asked) return`**, so the one mutation the negative half exists for was invisible to CI and to
  every ordinary run — including the run whose count the commit quoted. The page builders are
  extracted and asserted by a test that runs always, the mode is checked against the filename (the
  Villain proof could be made a copy of the Hero one), and the empty-editor page carries a marker per
  section rather than two the tab strip supplied on its own.

Fourteen mutations were re-run after the fixes and all fourteen are caught.

3854 tests to **3877**. Zero warnings at CI strictness. **Payload: unchanged** — one new component
file, no new asset.

### Phase 0 of the front-end plan: the scales nothing after them can be consistent without

[`docs/FRONT-END-PLAN.md`](docs/FRONT-END-PLAN.md) puts this first because it is invisible on
its own and every later phase is cheaper for it. **Nothing here changes what the app does.**

**The counts in the plan were an undercount, and the measured ones are the reason this was
worth doing.** The plan named "twenty separately-chosen spacing values and eleven font sizes".
Measured off the screen half of `app.css`: **twenty-seven** distinct lengths on padding, margin
and gap, and **twenty** font sizes — nineteen in rem plus the body's own 15.5px — of which
**ten sat between 0.68rem and 0.9rem**, a range no reader can resolve into ten steps. That is
not a design, it is a history of individual decisions, and the reason nothing could have told
you so is that every one of them was locally reasonable.

**Nine spacing rungs and seven type rungs replace them**, plus three elevation steps and a
second easing. 169 lengths rewritten; every rung is used by at least one rule and no rule names
a length outside the scales.

- **Steps of 2px at the bottom and 4px above it, not a strict 4px base.** Four of the old values
  sat between 4.8px and 7.2px — tag padding, pip gaps, the gap in a row of controls — and a
  4px-only scale collapses that whole range onto either 4px or 8px, which is a factor of two on
  the tightest spacing in the app. The half-steps stop above 8px, where 2px is invisible anyway.
- **Two type rungs are anchored to existing values rather than to the ratio, and both for a
  measured reason rather than a taste one.** `--text-xs` is exactly **0.72rem** because that is
  the size `--muted` was measured against: the note on it holds it to 4.5:1 rather than 3:1
  *because* it carries explanatory prose at this size, and a scale that rounded the bottom rung
  down to 0.67rem to fit a ratio would have invalidated that measurement silently — the colour
  would still pass its own test, at a size nobody had checked. `--text-3xl` is exactly 2.15rem
  because it is the masthead and nothing sits above it for a ratio to answer to. The base is
  0.97rem, the 15.5px the body has always been, so prose does not reflow for a round number.
- **Elevation was one `--shadow` carrying the banner, every panel, the sheet, the tier cards and
  the sticky strip.** The consequence was not that the page looked wrong — it is that nothing on
  it had a *height*. `--shadow-3` is claimed by the budget strip alone, which is the only element
  that moves independently of the document, and that is asserted **by count**: spreading the top
  step back across the page would undo the distinction without changing a single value.
- **There is deliberately no `--ease-emphasised`**, though the plan named one. An overshoot curve
  wants something that should read as *landing*, and the only candidate is a row arriving in a
  list, which is Phase 2. Declaring it now ships a token no rule asks for — and this app has
  already shipped a `.label-line` class applied to nothing, found by looking at a rendered page
  rather than by any test. `--ease-out` is added and used, on the four things that travel.

**Held by the same rule as colour and typeface, and the rule refuses both spellings.**
`NoScreenRuleNamesARawSpacingOrTypeLength` bans a raw length in padding, margin, gap or
font-size — **in px as well as rem**, because px is what somebody reaching for a value rather
than a rung would naturally write, and a rem-only check leaves that door open. The print block is
out of scope by design: it is mm and pt, a different medium with its own scale and its own tests.
Three literals are exempt, each **paired with the selector it belongs to** and each asserted to
still exist, because an exemption whose selector was renamed away permits its declaration
everywhere and says nothing.

**Three things were found by doing this rather than by planning it, and the first was mine.**

- **The scripted rewrite produced `-var(--space-6)`, which is not valid CSS.** A minus sign in
  front of a `var()` invalidates the whole declaration, so the browser drops it — the budget
  strip would have quietly stopped bleeding to the shell's edges, and the `margin-bottom` on the
  same line would have gone with it. **Nothing about the page would have looked broken**; it
  would have looked as though the bleed had never been written. Four sites, all `calc(-1 * …)`
  now, and the comment beside the first says why.
- **Three existing typographic guards read their font size out of the declaration with a regex,
  and stopped working the moment the sizes became tokens.** The obvious repair — accept a
  `var()` and skip the range check — turns three *measured* assertions into three assertions
  that a property is present, which is the exact weakness all three of their doc comments record
  being hardened against. They **resolve the scale** instead, so they are stronger than before:
  `--text-sm: 2rem` in theme.css now fails the trait-Source-line guard, which no literal read
  could ever have seen.
- **"The bleed has to follow the shell's padding" was a comment asking to be remembered.** It
  had to be: two unrelated literals have no relationship to assert, which is why the 8px overflow
  at 375px got in. Two references to one token do, so it is
  `TheBudgetStripsBleedMatchesTheShellsPadding` now — asserted at every breakpoint, with the
  media queries **discovered rather than listed**, so a third breakpoint is covered the day it is
  added rather than the day somebody remembers it.

**Eighteen mutations were run against the new guards before any reviewer saw them; seventeen
applied and all seventeen were caught.** The other two are the finding worth keeping: **a
mutation aimed by line number at a file that had since gained four lines of comment deleted a
comment instead, passed, and reported as a survivor.** The harness now asserts the file actually
moved and prints the numstat, so "the mutation never applied" and "the guard held" stop looking
identical — which is the same failure as reading `Passed!` off a crashed run, one level down.

**Verified by looking, not only by testing.** Both palettes at 1400px and the narrow viewport
through an iframe, which is **measured rather than eyeballed**: `clientWidth 360, scrollWidth
360, overflow 0px`, against the 368/360 that was the bug this replaces. The printed sheet was
rasterised and read — three sheets still three pages in both palettes, ink on white paper,
heading bars still a tint, Notes and Origin still ruled at a writable 4mm, gear still flush left.

**Two reviewers that knew nothing about it then found three code defects and eight guards that
held nothing.** Every one of the eight was demonstrated by mutation there and re-demonstrated
here after the fix.

**The three defects.** Two were found independently by both reviewers, which is worth noting: the
overlap was not redundancy, it was corroboration on the two that mattered.

- **`.sheet-section > table` read `width: calc(100% - 1.2rem)` and was a matched pair with the
  0.6rem inset on its siblings.** Phase 0 moved the inset onto the scale and left the width
  behind, so a table's right edge fell 3.2px short of every other child of its box *and* of the
  heading bar above it — measured at 8.00px of inset on the left against 11.20px on the right,
  five boxes a sheet, both palettes. **The change written to abolish paired literals left one
  standing one property name outside its own scope**, and no test could see it because `width` is
  not padding, margin, gap or font-size. `NoScreenCalcNamesARawLength` closes the class rather
  than adding `width` to a list: what makes the bug possible is not the property, it is a number
  that has to agree with a token and has no way of doing so. Now 75 of 75 children of every
  section measure symmetric.
- **`--text-3xl` was declared 2.1rem while four documents called it "exactly 2.15rem… not to be
  tidied onto a ratio".** It had been tidied onto the ratio. So the one rung the notes single out
  as unpinnable was the one already off its stated anchor — and unlike `--text-xs` it had no
  test. It is 2.15rem and pinned, and the note now states what the anchor *costs*: a 1.26 top
  step rather than ~1.2, which is the honest version of "anchored, not derived".
- **`.replay-figures.spent-on` became a rule identical to its base**, 0.8rem against 0.85rem with
  both snapping to one rung. It was the modifier's only declaration, so the class did nothing
  anywhere while a component still emitted it — **the `.label-line`-applied-to-nothing shape this
  very entry cites as a lesson, reintroduced in the same commit.** The distinction was 6% and
  below perception, so the rule and the class go rather than inventing a new size difference:
  that is Phase 1's hierarchy work, not Phase 0's mechanical pass.

**The eight guards, and the transferable part of each.**

| Held nothing because | Now |
|---|---|
| The scale check read token **names** and never a value, and nothing else in the suite pinned any `--space-*`. `--space-4: 4rem` re-padded most of the app, the narrow shell and the strip's bleed from 12px to 64px, green | Every rung's value is recorded and asserted, and the rungs must be strictly increasing. Same pattern as the server instructions: where the value *is* the deliverable, the value is the assertion, and the duplicated literal buys a change having to be deliberate and visible in a diff |
| The declared set was computed from **theme.css alone**, so a `--space-9` declared in a `:root` block inside app.css joined the scale invisibly and the raw-length scan waved through every `var()` using it | Both stylesheets and `index.html` are checked to declare no rung at all. theme.css is the only file allowed to |
| The unit list was `px\|rem\|em\|ch\|vh\|vw\|%`, so `margin-top: 9pt` walked through — and so would mm, cm, in, pc, ex, lh, vmin, dvh and the container units. **An allow-list of units is the wrong shape for a ban** | Every CSS length unit, longest-first |
| **`@page` sits *above* `@media print`**, so it was inside the region this file calls the screen half, and the guard's own doc claim that print is out of scope was false for it. It passed only because `mm` was missing — closing that gap would have turned a live print declaration red | Excluded by name, and **asserted present before being removed**, so a moved or renamed page box fails rather than silently un-excluding itself |
| `BleedTokenOf` took the first negative token found **anywhere** in the shorthand and assigned it to both sides, so it could not tell a horizontal bleed from a vertical one. A margin pulling the strip 24px *up* over the step nav, with positive side margins and the rail left 48px wider than the strip, satisfied the pairing | Both the padding and the margin readers parse the four sides properly. Splitting on whitespace was the cause: `calc(-1 * var(--space-6))` contains three spaces |
| `Contains("position:sticky")` is satisfied by a declaration a **later one in the same block** overrides, so `position: static` un-stuck the strip while it kept the top elevation step — with the guard's own comment claiming that could not happen. **The identical shadowing trick the `.hp` guard in this file was already hardened against; the new guard did not inherit the fix** | The last `position` declaration wins, as the cascade does |
| The rank-word band is **absolute**, and the rank it glosses is a rung of the same scale, so setting the gloss to that rung left it exactly level with the figure it sits behind | The relationship is asserted. Before Phase 0 the two were unrelated literals in two rules and this could not be expressed at all — **the scales made a describable claim into a checkable one**, which is the clearest thing Phase 0 bought |
| The 1px exemptions are justified **entirely** by the `border-bottom` they sit against. Replace it with `text-decoration: underline` and the padding is dead decoration with the stated reason false, and a guard checking the declaration string passed. Its own doc calls a stale exemption "worse than a missing one" | Each exemption carries its precondition, looked for across every rule targeting the element — `.hp` supplies the `.power-entry .head .hp` case by inheritance, so requiring it in the same block asserted something never true |

Three elevation steps may also no longer be three copies of one shadow, which a name-only check
could not have told apart either.

**And a method finding, which is the one to carry forward.** **Two reviewers running concurrently
in one worktree poison each other** — both mutate files and revert with `git checkout`, so one
caught the other's `--text-sm: 2rem` and read it as a finding, both lost runs to `index.lock`,
and one's cleanup deleted the other's harness. Give each its own worktree. Relatedly, **a numstat
check taken *before* the test run does not catch a mutation reverted mid-run**; the harness checks
after as well now, which is the same lesson as reading `Passed!` off a crashed run, one level down.

The headline count also read 3849 against a tree of 3850 for one commit, copied from a run taken
before the last test was added. Both reviewers spent a finding on it, which is a waste of a
reviewer: **take the number from the run.**

**Then a fix-audit — a reviewer pointed at the fixes rather than at the code — and it found that
nine of the eleven caught only the mutation demonstrated to them.** That is the fourth session
running this reviewer has been worth more than the passes before it, and the second time it has
found most of a round of fixes to be narrower than claimed. Both figures it re-measured from the
documents checked out (75 of 75 children symmetric, `overflow 0px`, 3852 tests), which is the
other half of its job.

**Its central finding is one root cause behind three of the nine, and the correct pattern was
already in this file twice: a later declaration of the same thing beats a `Contains`.** Each of
the three reached the bad end state by declaring the thing *again* rather than by editing what the
guard was reading — a duplicate `--space-4: 4rem` under `--space-8` (workhorse rung at 64px, full
suite green), `.budget, .breakdown { position: static }` later in the file (strip un-stuck, top
elevation step kept), and `border-bottom: none` instead of deleting the line. It is fixed once, as
`EffectiveValue`: comma lists split, suffix-matched, last declaration wins.

**Two doors needed no scale token at all, and that is the more useful lesson.**
`--table-inset: 1.2rem` beside `width: calc(100% - var(--table-inset))` restored the table
misalignment byte for byte — 3.20px on all fifteen tables — and `--pad-lg: 4rem` re-padded an
element with a raw length under a name no rule about the scales could match. **Narrowing the
earlier check to `--space-*` and `--text-*` defended the names of the scales rather than the
property that makes a scale mean anything**, which is that there is one place lengths are decided.
`app.css` may now declare no custom property at all — free, because it declares none.

**And the unit list was the wrong shape twice, the second time knowingly.** The fix's own doc
comment said "an allow-list of units is the wrong shape for a ban" and then shipped a longer
allow-list, which `9dvmin`, `3svb`, `2lvi` and `4PX` walked through — twelve viewport units
missing and the match case-sensitive besides. It is inverted now: a digit followed by letters or a
percent is a length, whatever the letters are, so a unit from a future specification is caught the
day it ships.

Three more where the fix asserted more than it checked, which is the shape this project keeps
being bitten by:

- **`ScreenHalfOfAppCss` strips every `@page` block while `ThePageIsA4WithMargins` read only the
  first** — and the stripping cited that test as the compensating check. A second `@page` after
  the A4 block printed the sheet A5 landscape at margin 0, seen by nothing.
  `ThereIsExactlyOnePrintBlockInEachStylesheet` exists for this failure one at-rule over; the page
  box now has its equivalent, pseudo-pages included.
- **`TokenIn`/`NegativeTokenIn` still took the first token found anywhere inside a side**, which is
  the first-match weakness the four-side parser was written to remove, surviving one level down.
  `calc(-1 * calc(-1 * var(--space-6)))` computes to **plus** 24px and read as a bleed;
  `calc(var(--space-6) * 3)` is 72px of padding read as matching a 24px bleed. Both anchored to
  the whole side, so a side doing arithmetic fails safe rather than being guessed at.
- **The rank-word guard named `.trait-table td` as "the rank it glosses", and the two never render
  on the same surface** — `.rank-word` comes from `RankRow.razor` and `.trait-table` from
  `SheetView.razor`. The claim was about a pair nobody can see together, and it passed only by
  being accidentally conservative. Shrinking `.stepper .value`, the figure it actually sits beside,
  put the gloss exactly level with it, green. Compared against that now, with an assertion that
  the two really do render together.

**One route needed a second round.** Reading the exemption's precondition as a *value* closed
`border-bottom: none` and did not close `.sheet .budget-toggle { border-bottom: … }` — a selector
matching nothing in this app, supplying the reason while the real rule lost its border. **Suffix
matching is right for "what applies to this element" and wrong for "does this rule still say
this",** and a source-reading test cannot know which selectors match real elements. So each
exemption records the selector that must carry its reason, matched exactly — which for `.hp` is
the base rule rather than the exempt selector, because the letter-spacing is inherited.

**Two of the fixes were mine to break again in the same sitting**, both caught by running rather
than by reading: the inverted unit regex allowed whitespace between number and unit, so
`margin: 0 auto` read as the length "0 auto"; and the precondition field carried a trailing colon
into a pattern that appends its own, demanding two and matching nothing, which reported every
exemption's reason as missing. And `CA1875` — an analyzer error **only the
`ContinuousIntegrationBuild` flag reports**, which a plain `dotnet test` was green over.

**One of my own re-runs was a misaimed mutation reported as a survivor, for the second time this
slice.** The comma-list rule was inserted at `.boot-sub`, line 84, which is *earlier* in the file
than `.budget` at 239 — so the cascade genuinely resolved to sticky and the mutation never reached
the state it was testing for. Re-aimed after the rule it had to override, both it and a
more-specific-selector variant are caught. **Placement in the cascade is part of aiming a
mutation, not a detail of it.**

3841 tests to **3854**. Zero warnings at CI strictness. **Payload: unchanged** — no file added,
no byte of CSS beyond the token declarations.

### The visual redesign: two faces, and the filter box somebody actually asked for

All six items of Slice B, chosen after looking at [pnpready.com](https://www.pnpready.com/) —
a companion app for this game whose scope is not worth chasing and whose presentation is.

**1. Two typefaces with distinct jobs**, which was the single biggest difference. **Oswald** for
display and **Public Sans** for body, both self-hosted under `web/wwwroot/fonts/`, both SIL Open
Font License with the licence text shipped beside them — a condition of redistributing them, not
a courtesy, and this repository redistributes them on every deploy and every fork. Public Sans
over Source Sans 3 on payload: 103 KB variable against 642 KB for the same job. Both variable, so
one file covers every weight.

**The tokens are the point, not the faces.** `--font-display` and `--font-body` live in
`theme.css` and nothing else names a face, which is the same discipline already holding for
colour, radius and duration — so the house style is one edit and no component can drift.
`NoComponentNamesATypeface` checks both spellings, because `font:` shorthand carries a family
too and `font: inherit` is all over `app.css`.

**Two guards, both demonstrated by mutation.** A font file that goes missing degrades the whole
app to the system fallbacks *silently* — the stacks name fallbacks deliberately, so a failed load
still leaves a readable page, which means nothing but the bytes on disk can catch a renamed file.
And a family may not ship without its licence.

**2. Labels carry the structure of the long forms** on screen, rather than borders alone. Written
first as a `.label-line` class that was applied to nothing — dead CSS, found by looking at the
rendered page — and now on `label` itself, which is where a long form needs it: the Sources
editor is eighteen fields in a column. `SheetSection`'s centred bar heading is untouched, because
it is right on *paper* and the published Hero Sheet prints it that way.

**3. The tier choice is a card grid**, six cards each carrying its consequence on a line of its
own, built as a `cards` modifier on `OptionList` rather than as new markup — which also deleted
the hand-rolled Panel grid the page used to carry.

**4. Every derived figure shows the rule it came out of.** **A statement of the rule and never a
working of it**: the engine returns a number, not the terms it added up, and reconstructing them
in the browser would be the one thing this front end exists not to do. Off on the sheet, because
the published sheet prints no formulas and one page is a margin four blocks of small print would
spend. **It overlaps the "Where they come from" panel on that page and was left overlapping** —
the panel's value is the live working with the character's own numbers, which the rule under the
figure cannot be; if one of the two goes, it is the panel's rule paragraphs and not the workings.

**5. The rulebook's word beside each rank** — `4d Noteworthy`, `4d Proficient`. Presentation only:
`rank_guide` has been on every entry in `abilities.json` and `talents.json` the whole time, read
into the models the whole time, and shown by nothing. **Both tables stop at 6d and above that
there is deliberately no word**, so a 7d Trait shows an empty cell rather than the nearest one,
which would be this program inventing a rung the book does not have.

**6. One filter box, in the component all five pickable lists share.** The only item here that
came from somebody using the thing: scrolling 141 Powers. **The Powers tab had the only search
box in the app** and it now has none of its own — the box moved into `OptionList`, so Pros, Cons,
Perks, Flaws and gear features got one by being lists of options. It reads a row's tags as well as
its text, because the Powers box did and tags are never printed, so moving it would otherwise
have quietly narrowed the one list that already worked.

**Two bugs found by looking at the rendered page, neither of which any test would have caught:**

- **The count read `282 of 282` where the rulebook has 141 Powers.** A row inside a
  `CascadingValue` is reached from *both* directions when the value changes — the parent
  re-renders the fragment holding it, and the cascading value notifies its subscribers — so
  `OnParametersSet` runs twice per pass and every row counted itself twice. **It read as a
  plausible number beside a list nobody counts.** The row now counts itself once per pass however
  often it is asked, rather than the list assuming how often Blazor will ask; the test asserts the
  count against the rows underneath it rather than against a figure from the rules.
- **A tier card printed `TRAIT CAP 12D`.** Every label in this redesign is uppercased and that
  line carries a *rank*, which the rulebook writes `12d`. The card is now the one label that is
  not uppercased, for a rules reason rather than a taste one.

**And then the same bug a second time, which is what turned it into a guard.** Uppercasing every
form `label` made "Custom features (Ch.6, p.93)" read `CH.6, P.93` — a rulebook citation in a
notation the rulebook does not use. Several labels are whole sentences besides: a Pro's narrative
constraint reads "Player must define the specific condition when purchasing." So `label` keeps
the face, the tracking and the muted ink, and drops the capitals.

**`UppercasedTextTests` guards the class, in two halves, and one half is not enough.**

- The **rendered** half takes its selectors from whatever `app.css` actually uppercases today —
  never a list somebody remembered to update — and asserts that no such element on a rendered
  page carries a rank or a citation. Reinstating the capitals on the tier card's cost line fails
  it, quoting `Trait Cap 8d`.
- The **source** half exists because the rendered half **could not see the case that caused it**.
  The gear labels live several interactions deep, and rendering that page with a character
  loaded produces *zero* labels — so the mutation left the rendered theory green. That is exactly
  "a runtime test is only worth the paths it drives", found by mutating rather than by trusting a
  new test because it was new. It is conditional on the stylesheet, so the constraint lifts if
  the capitals ever go.

**The printed sheet was re-proofed on paper, not on screen**, through the bUnit-plus-headless-
Chrome route with `--print-to-pdf` and `--no-pdf-header-footer`, and rasterised with Docnet plus
ImageSharp pinned below 4.0. **Three sheets print as three pages in both palettes** — the new
metrics did not cost the one-page property — white paper, navy or crimson ink, heading bars still
a tint. That harness is now `ProofPages`, which writes nothing unless `PP_PROOF` is set.

**What this did not close.** The fonts ship as `.ttf` and would be roughly 40% smaller as
`.woff2`; there is no converter and no network on this machine, and it is a one-line change per
face when there is. It also adds ~372 KB to a payload item 5 already calls large.

**The fonts were verified as far as the published output**, not merely the build: `.NET` static
web assets do not copy into `bin/wwwroot`, so a build tells you nothing about what ships. A
`dotnet publish` puts all three faces and both licences in `wwwroot/fonts/`.

**An adversarial review then found eight holes, one of which reverted the slice.** Every one was
demonstrated by mutation there and re-demonstrated here after the fix.

- **Both `@font-face` families could be pointed at the same file.** Every heading, label, figure
  and section bar rendered in the body face — with both tokens declared, both different, both
  asked for, every file present and licensed, and four font guards green. **Nothing correlated a
  family to its own file**, so the headline item of this slice silently reverted and the app
  looked exactly as it had before. `EachFamilyIsServedItsOwnFile` is three lines in a test that
  already computed both halves.
- **The rank word had no rendering coverage at all** — its only guard was the CSS rule check,
  which passes while the word is wrong, invented or hidden. Three mutations went through: reading
  the 6d word for every rank above it (*the exact failure the component's own comment says is
  prevented*), an off-by-one printing the 5d word beside a 4d Trait, and `display: none` on the
  class. `RankWordTests` renders it against the rules' own guide with two anchors from the printed
  tables, and the CSS half now refuses `display: none` over every rule targeting the class.
- **The licence guard was `File.Exists`**, so a 23-byte stub reading "Oswald is a nice font."
  satisfied it. **This is the one green in the slice that carries a legal claim.** It reads the
  licence text and the reserved font name now, so one family's licence cannot stand in for the
  other's.
- **Four of the five lists could drop their filter box silently**, and matching by *description*
  was untested while four placeholders promise it. Both were one test each — and **both had to
  drive the page rather than render it**: the gear features are two clicks in and the Pro/Con list
  is behind a toggle, so the first version of that test passed against a page with no options on
  it at all, which is the same blind spot in a new coat.
- **`index.html` was scanned by neither the colour nor the typeface rule**, and it is the other
  file in the payload that can carry CSS — `style-src 'unsafe-inline'` means an inline block there
  applies rather than being blocked.
- And a comment cited **Elasticity**, which is not a Power in this rulebook. `CLAUDE.md` records
  that exact slip being made once before from a stale note; this is the second time.

**One finding was about method rather than code, and it is in the traps list now.** A crashed test
process still prints `Passed!  -  Failed: 0`: the endless render loop this component's guard
prevents ends in a stack overflow, 31 of 153 tests never run, and the summary line reads as green.
The exit code is 1 so CI catches it — a person grepping for `Passed!` does not.

**A second review measured rather than read**, resolving the `color-mix()` tokens itself and
validating them against Chrome's own resolution, so its numbers are figures rather than
impressions. It found a **real WCAG failure**: Villain `--heading` on `--accent-soft` is
**4.08:1**, and 1.4.3 applies to a hover state. It also found the print-specificity trap for
the **third time in this file**, `PointsRule` unreachable, item 4's formulas duplicating the
panel below them on the only page they appear, no cache header on 381 KB of fonts, and the
Iconic tier card printing its one sentence twice — which, because the cards are an
equal-height grid row, cost the two cards beside it five lines of dead white.

**Then a fix-audit — a reviewer pointed at the fixes rather than the code — and it was worth
more than either review before it.** Three of the fixes did not hold:

- **`EachFamilyIsServedItsOwnFile` caught the mutation it was written for and missed two
  routes to the same regression.** `FontFaces()` filtered out any face whose `src` it could not
  parse, so an **absolute** `url("/fonts/PublicSans-Variable.ttf")` — live under
  `<base href="/">` — dropped the Oswald face from the family check *and* from the licence
  check, and the app served Public Sans for every heading with all 96 tests green. **The filter
  written to make the guard robust widened the hole it was closing.** The second route was
  overwriting one font file with the other's bytes, which no name correlation can see; the
  check now reads the TrueType `name` table.
- **The licence guard's three strings all appear in an OFL file's first nine lines**, so
  `head -9` — 383 bytes, permission grant and warranty disclaimer deleted — passed.
- **`UppercasedTextTests` closed with `Assert.True(seen >= 0)`**, a tautology, and **13 of its
  25 selectors matched nothing on any rendered page**. Setting `.verdict` to
  "Trait Cap 12d (Ch.9, p.150)" rendered `TRAIT CAP 12D (CH.9, P.150)` with the suite green —
  the exact bug class the file exists for. It renders fourteen components now and **refuses a
  selector it never found**, with six named exemptions.

**And the fix for the contrast finding was itself half a fix**: moving the tier card off
`--accent-soft` left `.btn:hover` on the identical failing pair and `.btn.danger:hover` on a
worse one (3.94:1 Villain, 4.55:1 Hero). There are far more buttons than tier cards, so the
stylesheet carried a comment calling the pair a fault eleven lines above two live instances.

3772 tests to **3841** — 3645 on the engine, 196 in bUnit. Zero warnings at CI strictness.

### The budget bar becomes chrome, and the plan for what the front end could be

The budget was a bordered panel costing about **110px above every step** — roughly a quarter of
a laptop viewport, six times over, most of it a seven-part table consulted occasionally while
occupying the space continuously. A persistent budget is *ambient status*, and status belongs
where the banner is rather than in the column the reader scrolls.

It is a **sticky strip** now: the spend set large against its budget as a denominator, what is
left beside it, a **3px rail** bled to the width of the shell along its own bottom edge, and the
breakdown disclosed on request. About 40px, and it stays put. Whether the disclosure is open is
a field on the component — deliberately **not** on `CharacterSheet`, which is what gets exported
and restored.

**It shipped unreviewed and had three defects**, all found by the fix-audit and all now covered
by `BudgetStripTests`: the negative-margin bleed is fixed while the shell's padding is not, so it
overflowed 8px at 375px; the `progressbar` announced `valuenow=132` against `valuemax=125` when
over budget — invalid, and disagreeing with its own clamped fill — and had no accessible name;
and `aria-controls` pointed at an element that only exists while open.

[`docs/FRONT-END-PLAN.md`](docs/FRONT-END-PLAN.md) is the plan for the rest, in six phases. **Its
one load-bearing decision is that there is no animation library**: `element.animate()` does
everything on the list, the payload is already item 5 below, and the View Transitions API does a
thing no library can. The shortlist is named if that is overruled.

### A1, A2 and A3 reconciled onto one branch, and the arithmetic that says nothing was dropped

The three sub-slices of the mutation audit were worked **concurrently, one branch each**, all
three forked from `5867340`. That is why the three entries below each read as though they were
the last session: none of them could see the other two.

**They collide on four files and nothing else** — `PROGRESS.md`, `CLAUDE.md`, `docs/HANDOVER.md`
and `ValidationIssueStructureTests.cs`. The first three are documents and were resolved by hand;
each branch had rewritten the same counts and the same "the last session did X" paragraph, so the
conflict was real but its resolution is prose. **The fourth resolved itself and was checked rather
than trusted**: A1 made `CaseNames` and `Build` `internal` so `McpServerTests` could drive the same
sheets, A3 added 592 lines of new tests elsewhere in the file, and the two never touch the same
declaration — so the clean auto-merge is clean for the right reason, not by luck. No test and no
source file was resolved by hand.

**The check that the merge lost nothing is the test count, and it reconciles exactly.** Base
`5867340` was 3322 engine + 124 bUnit = 3446. A1 added 79 engine, A2 6 engine and 19 bUnit, A3 222
engine. Predicted 3629 + 143; measured **3629 + 143 = 3772**, zero warnings, at
`ContinuousIntegrationBuild=true`. A merge that silently dropped a test file would land under that
number, and a merge that duplicated one would land over it.

**The count is necessary and not sufficient, so each slice's flagship guard was re-run by mutation
*on the merged tree*.** A test can survive a merge and stop biting: the count only says the method
is still there, not that the assertion inside it still fails when it should. Five mutations, each
confirmed applied with `git diff --numstat` before the suite was believed — A2's tier substitution
in `SheetView` (4 red), A2's `Find` made case-sensitive (3 red), A2's `print-color-adjust: economy`
(1 red), A1's substring matching restored in `Mentions` (8 red), A1's `ReadEverything` no longer
reading the embedded guide (1 red), and A3's `DUPLICATE_PRO` reporting `ValidationSubject.Ability`
(2 red). All still bite on the merged tree.

**One near-miss worth recording, because the search for it is what should be copied.** Three
`git stash` commits were left dangling on the A1 branch and one of them holds a test —
`TheStartupCheckReadsTheEmbeddedGuideAndNotOnlyTheRules` — whose name appears nowhere in the merged
tree. It had been **renamed**, not lost: it is `TheStartupCheckReadsTheGuideAndNotOnlyTheRules`, and
deleting `_ = _guide().Length;` from `ReadEverything` turns it red. But a name comparison against
dangling work is a cheap check that found the one thing worth checking, and `git fsck
--lost-found` after a parallel-branch merge costs a minute.

**Each entry below still quotes the count measured on its own branch**, deliberately — rewriting
them to the reconciled figure would make three true statements into three false ones. The figure
for this tree is the one in the table at the top of this file.

### The MCP server's twelve guards that held nothing — slice A1 of the mutation audit

**Verified on Linux at CI strictness as well as on Windows**, from a `git archive` export of the
commit rather than in place: 3401 + 124 green, zero warnings. That step is not ceremony here — one
of the fixes in this slice *was* a Windows-shaped assertion that passed locally and would have
failed the container (`Path.IsPathRooted(@"C:\Users\…")` is `false` on Linux, because a backslash
is not a separator there and `C:` is not a root). It was caught by reading rather than by running,
and the run is what confirms nothing else of the shape is left.

Twelve tests claimed to pin behaviour and did not. Each is recorded with the mutation that
defeated it, because the mutation is the evidence and an argument is not. Every one was confirmed
to survive the suite *before* being fixed, and confirmed to fail it after — with the production
code reverted in between, so no fix is a claim.

**The two that mattered.**

**A `Console.WriteLine` inside a tool body reached the client's standard output with both guards
green.** `NothingWritesToStandardOutput` scans each line for the token `Console.`, so `Console`
and `.WriteLine(…)` on two lines contains it on neither.
`TheBuiltProgramSpeaksNothingButTheProtocol` sent `initialize`, `notifications/initialized` and
`tools/list` and stopped — so it never entered a tool body at all, and covered the startup path
and the handshake and nothing else. Confirmed live: line 2 of the JSON-RPC stream was
`searching for turns invisible`. The same write in `ListOptions` *was* caught, and only because
`ReadEverything` calls it at startup.

The fix is that the runtime test now **calls all six tools**, and each answer has to carry
something only the far end of that body produces — a call answered "unknown tool" would otherwise
satisfy it while running no code at all. **Not another regex**, which would only move the hole:
the spelling after a multi-line one is a helper in another file, or a library. `CLAUDE.md` claimed
the two halves were complementary and is corrected; so is the scan's own doc comment. Its
deadline is 30 seconds and it says which read ran out, because "no greeting at all" and "a request
that never came back" are both real mutations and mean different things.

**`search_powers` could be widened back to substring matching by one line** — the failure
`Mentions`' own summary calls "worse than no match", since it arrives looking exactly like a real
answer. Confirmed live: `search_powers("she bakes bread in the city")` returns **Plasticity**.
Every existing search test was a positive assertion or a negative on a query whose words happen
not to be substrings of anything. It is pinned now by four fragments that occur inside a Power's
name and nowhere in the rules files as a word — `city`/Plasticity, `ration`/Regeneration,
`art`/Martial Arts, `kinesis`/Telekinesis — each with the positive control beside it, plus the
baker's sentence held at `found: 0`. **Both `CLAUDE.md` and the method's own summary named
*Elasticity*, which is not a Power in this rulebook**; a reproduction would have searched for an
entry that is not there. There turned out to be a **third** copy, in this file — the correction
missed it, and a reviewer pointed at it rather than a test, because nothing greps prose for the
name of a Power that does not exist. All three now say Plasticity.

**The rest, where one payload assertion replaced the fields somebody remembered.** This is the
shape of the whole slice: twelve separate field assertions is what produced the gaps, because a
field added later is a field nobody wrote an assertion for.

| Was free | Now |
|---|---|
| `power_detail`'s `cost_variants` → `null`, plus `category`, `stat_line`, `rank_type`, `cost_type`, `max_rank`, `unit`, `description`, `source_ref` each replaceable with a constant | Every field of all 141, against the model, **with the key set asserted** so a new field fails until it is read. `cost_variants` was the sharp one: it is the only place a caller learns the accepted keys, and `check_character` refuses a `per_rank_variable` Power for lacking one — the tool taught a dead end the judge then closed |
| `list_options`' gear-feature `cost_type` and `grades` (nulling the second kills the two graded features' keys), flaw `flaw_type`, perk `unit`, talent `ordinary_human_rank` | Every field of every entry of all ten catalogues, plus the entry count and the report's own key set. `TheCatalogueNumbersAreTheRulesOwn` stays — it reads the numbers pairwise and says why each matters; this is the completeness half |
| An issue could drop `owner_id` and `options` — the two a repair loop cannot work without | Every field of every issue, **driven from `ValidationIssueStructureTests.Cases`**, which is itself held to the validator's source, so the codes covered are the ones the validator can construct rather than a list that goes stale. `CaseNames` and `Build` are `internal` for that reason: a second copy would be the stale one. A companion test asserts the sheets really do reach all six optional fields, since two nulls compared to two nulls is how this gap arose — and it checked **five** when this sentence first claimed six, which is the "three of four" shape a reviewer has caught here before. The sixth is asserted now |
| `QUESTION-POLICY.md`'s schema **prose and comments** — only the fenced block goes through the strict reader, and with its comments stripped. Rewriting the sentence for a Pro to `ProId`/`Count` passed | Every quoted PascalCase name anywhere in the document must be a property in the character's object graph, by reflection rather than against a list here. Reading is strict, so a proposer following the prose gets an unreadable character |
| **The whole Claude Desktop half of `docs/MCP-SETUP.md`** — breaking both blocks' `command` paths passed, because every other test regexes `claude mcp add` | Both blocks must parse as JSON, key the server under the same name the Code commands use, and give an **absolute** path ending in the binary this repository builds. One block keeps its doubled backslashes and one has none, which is the difference the pair exists to show |
| The startup diagnostic the guide's first troubleshooting bullet sends a stuck reader to find | Read off standard error by the runtime test, and required to name the rules directory |
| `CharacterServer.Instructions` reduced to `"creation_guide check_character the engine decides"` | A length floor, the "never state a figure" clause, and a sentence count. Note the asymmetry that made it worth fixing: every tool description had a 60-character floor and the instructions — the model's only guidance *before* it picks a tool — had none |
| `ReadEverything` no longer reading the embedded guide | Asserted, and **honestly caveated**: a resource cannot be un-embedded from a loaded assembly, so there is no runtime arrangement in which the guide is absent. The line that reads it is what is checked, and the test says that is a limitation rather than a preference |
| The search limit's upper bound: `Math.Clamp(limit, 1, 25)` → `(limit, 1, 400)` passed, because every row searched "armor", which matches fewer than 25 | Wide rows on a query that overflows the ceiling, and a test asserting that it really does — so the clamp rows prove something about the clamp |

**What the two adversarial reviews then found, which is the part worth reading.** Fourteen further
findings across two passes, **most of them inside the fixes above**, then six more from a third
review pointed at those. All are closed and each was demonstrated the same way. (An earlier version
of this sentence said "nine of them"; the figure is not reconcilable from the list either way,
depending on how the two `ReadEverything` findings are grouped, so it is not stated as one.) Four
patterns, and they are the transferable part:

1. **A marker that proves a code path ran must be something only that path can produce.**
   `character_sheet`'s marker was the character's name — which the test sends as the argument and
   `check_character` echoes back as `character.name`. So wiring the `character_sheet` wire name to
   `CheckCharacter` passed, and a client asking for the printed sheet would have got JSON. It was
   the only test that drives that wiring at all, since every other one calls the method in process.
   It is the sheet's masthead now.
2. **A driven test covers the arguments it sends and nothing else.** Every tool's *refusal* branch
   was undriven, so a two-line `Console.WriteLine` in `TryReadCharacter` — reached by a misspelled
   field, the commonest first mistake there is — passed the source scan and the runtime test both.
   Every tool is now called twice, once to its answer and once to its refusal.
3. **A source-reading guard is worth what its instrument can see, and mine could see very little.**
   `ReadEverything` could go back to warming one catalogue (`ListOptions(Categories[0])`) with the
   grep green, because the token `Categories` survives; and the read of the embedded guide could be
   deleted and replaced with a *comment* mentioning it, which is what somebody removing that line
   would actually write. The first is now a runtime theory over `RulesRepository.DataFileNames` —
   a directory missing any one rules file must be refused, which is the property, and the two older
   tests both used a directory holding `tiers.json` alone that throws whatever else is broken. The
   second was defeated twice more — a comment mentioning the token, then `nameof(QuestionPolicy)` —
   before being made a real runtime test: the guide is handed to `CharacterTools` the way its clock
   already is, so a guide that throws is an arrangement a test can build and deleting the read
   fails. **A token is not a read, and no amount of text-matching makes it one.**
4. **A phrase assertion cannot survive a "not" in front of the phrase.** The baseline note and the
   server instructions were each pinned by required phrases and each inverted while keeping every
   one of them — "do **not** stack on top of this baseline, and the Trait Cap applies to the total,
   which is the baseline alone", and "It is a **myth** that the arithmetic is not guessable". Both
   are asserted verbatim now. Where the content of a sentence *is* the deliverable, the sentence is
   the assertion, and the duplicated literal buys the only thing that matters: a change to it has
   to be deliberate and visible in a diff. The question policy's two confusable shapes needed the
   same treatment for a different reason — swapping the two groups whole leaves both field sets
   valid and only the English inverted, which no structural rule can see.

The rest, briefly: `grades` could be *narrowed* rather than nulled, dropping the −4 grades of the
two Cons the guide calls mandatory (key sets are exact both ways on every payload asserted whole;
three one-sided `Except` checks remain, each documented in situ where the field set is genuinely
open); `pros.own` and
`cons.own` were not read at all, so three fields could go constant and a new one appear unnoticed;
`Zip` truncates in silence, so an emptied array ran every loop zero times and fired no key check;
the baseline `note` — the one string in `power_detail` that is not the engine's answer — could be
inverted to say purchased ranks *replace* the baseline, across all 27 baseline Powers; the question
policy could name a real field on the *wrong type*, which reflection over a flat name set cannot
see, so each shape it writes out is now matched against the type that has those fields; the Desktop
`command` paths could point somewhere no publish command produces, and `(\.exe)?$` accepted `.exe`
on the macOS block — and then, once the extension rule existed, it asked `StartsWith("C:")`, so a
`D:` path dropped the `.exe` and passed; `force_field`'s `area_burst` grades were pinned nowhere, so
losing `burst` from the only place a caller learns it was invisible, and all three of its own-text
allowances are pinned by key now; `Judgement`'s guarded engine calls were still unentered, because
the unpriceable character went only to `character_sheet`, which never reaches them; a *corrupt*
rules file, as against a missing one, was covered nowhere though `Program.cs` catches
`JsonException` for exactly it; and one *opposite* failure — an extra, entirely legitimate line on
standard error broke the runtime test, which reads as a stdout regression and is nothing of the
kind, so it drains until a line names the rules directory.

**What is still not closed, stated rather than left to be found.** The stdout pair covers the
startup path, the handshake, and both branches of every tool; a write reachable only from an
argument shape nothing sends is still seen by neither guard. A `list_options` field that is null
for every entry in its catalogue would be caught now that key sets are exact, but the general point
stands: these tests are worth the payloads they ask for.


Newest first. Link the PR so the reasoning stays findable.

### Thirteen guards on the browser and the replay that were not guarding anything

The audit backlog in `docs/HANDOVER.md` listed 38 surviving mutations across the whole tree,
grouped by subsystem. This closes the browser-and-replay group — **all thirteen, each
demonstrated by re-applying the mutation, confirming red, reverting and confirming green.**

**The first five are one bug class, and it is the one `ReplayRenderTests` was written for.** The
four `.stat-block` figures and the Power ranks were genuinely pinned to the recorded character;
nothing else on the page was. The tier in the masthead and the colophon, the budget sub-line, the
Quote, the Motivation, the Description, the Connections and a rankless Power's stand-in rank could
all be moved from the printed character to the visitor's own with the suite green — so a replay
would print Vera Nunn's name over somebody else's tier, budget, words and connections.

Five more named assertions would have moved the boundary rather than closed it: the eighth field
somebody adds is unguarded again the day it is added. So the test compares **two renderings of the
same character** — one where the session holds it, one where the session holds somebody else
entirely and it arrives as a parameter — and asserts they are the same page. Every read of
`Session.Sheet` that should have been a read of the parameter is a difference between them,
whatever field it lives in. Three cases it cannot reach have their own tests: the stand-in rank
(no recorded character has a rankless Power, so it is built from the Hero sample), the budget flag
the replay passes for a Villain, and a capitalised address, which Blazor routes happily and
`ReplayLibrary.Find` then refused.

**The transcript honesty rules were narrower than they read.** The figure scan read `Title`,
`Blurb` and the recorded lines — never the character, which is what the sheet at the end is
printed from, so a Hero Point total in a Motivation, a Quote, a Description, a Connection or a
Flaw's narrative detail passed. It walks the character by reflection rather than naming six
fields, because a list of field names is exactly what goes stale here. Its word set was the
engine's vocabulary and not the page's: `ReplayVerdict` labels the gap `Over by` and `Left`, and
"nineteen over … three to spare" matched none of `HP|hero points|points|edge|health|resolve|
budget`. Those positional words now count, at a **one-word window** rather than three — at three,
Vera Nunn's "an apron over a cardigan" is a quoted figure.

And questions were counted by `'?'`, so seven imperative demands asked nothing at all — a
questionnaire, in the one file whose job is to demonstrate the opposite. The counter recognises a
demand now, and is honest in its own doc comment about being a heuristic; a second test counts the
**person's replies**, which no phrasing can get round.

**Three CSS rules were asserted by presence rather than by value**, which is the same mistake in
three places: `print-color-adjust: exact` could become `economy` (every heading bar prints white —
the failure the rule's own comment describes), `.hp` could be set at 2.4rem/800 (a Hero Point cost
three times the size of the rank beside it), and the 7pt print floor matched only sizes already
written in `pt`, so the two densest blocks on the sheet could go to `0.3rem` and `4px` unseen. The
floor now requires every printed size to be in points, which it has to be to mean anything: `rem`
is relative to a root size the print block resets.

**"A failed transcript fetch must not stop the app" had no test**, because the guarantee was a
`try`/`catch` in top-level statements that nothing can reach. Deleting it was green, and one 404
then took the whole character generator to a blank page. It is `ReplayLibrary.LoadAsync` now,
which takes the fetch as an argument and can therefore be handed one that fails — four tests,
including that one file short leaves **no** recordings rather than most of them, plus a source
test that `Program.cs` actually goes through it. Both halves are needed: a guarded loader nobody
calls guarantees nothing.

**And the strip-tags trap was in the helper `SheetRenderTests` uses to catch it.** `Rendered`
replaced every tag with a newline and one test then collapsed all whitespace, so
`<b>Armor</b><span>8d</span>` read as "Armor 8d" — the string the assertions look for, produced by
the bug they exist to find. Demonstrated both ways: splitting a Pros line into per-word elements
is red against the concatenated text nodes and green against the old helper.

**Then two reviewers found fourteen ways round the fixes, and thirteen are closed.** That is the
most useful number in this entry: the first pass at closing a finding is roughly half right, and
the review that goes looking for the *hole in the fix* has now been the most valuable one three
sessions running.

**Nine were one shape wearing different clothes: a check that reads one place while the thing it
guards is decided somewhere else.** Three CSS guards read the first matching rule, or one rule by
its exact selector, while the cascade reads the last — so a second `.hp` rule further down, or a
second `font-size` inside the same block, or `print-color-adjust: economy` written after `exact`,
all did what the guard forbade with the suite green. They read every declaration of every rule
that targets the class now. The 7pt floor read `font-size` and never the `font` shorthand, which
sets a size without writing the property; and it reads declared sizes, so `zoom: 0.55` on the
printed sheet left every declaration legal and printed the stat lines at about 4pt. The shorthand
and page-scaling are both refused outright. The source check on `Program.cs` asserted the guarded
loader was *called*, which stays true if the fetch is hoisted back outside it — so `LoadAsync`
takes the `HttpClient` and there is nothing left to hoist. And the path it fetches from was
pinned by a `Contains`, which `data/transcript` satisfies against `data/transcripts`: the
one-character mistake the test existed to catch, passing it.

**Two were preconditions that had quietly stopped being true.** The sheet-equality pool crossed
tiers on one row of four, because Vera Nunn is the only recorded character who is not Standard —
so the masthead, the Trait Cap and the budget sub-line would have stopped being covered the day
she changed, with nothing failing to say so. And the verdict panel's Perks and Gear rows were
asserted against the engine while every character in play had zero of both, which cannot tell two
characters apart; that panel now gets the same two-visitors treatment the sheet does. The
Villain's budget finding was asserted to be *not called illegal* and never asserted to be
**shown**, though showing it is what that recording is about.

**One was not closeable by vocabulary, and needed a different kind of rule.** "She lands on 75
exactly, and the tier hands her 75 to spend" quotes her spend and her budget in one sentence and
matches no word list anybody could write. So a second rule asks the engine what the figures are
and refuses those numerals outright, in digits and spelled out — which is what `CLAUDE.md` says
the rule is. It sits beside the vocabulary rule rather than replacing it: the vocabulary rule is
about *shape*, and "over by a full nineteen" is a quoted figure whether or not nineteen is the
right answer.

**The one still open is recorded rather than fixed**, because it cannot be fixed by a test.
`IsARequest` recognises the phrasings a demand is normally written in; an unlisted verb —
"Settle the tier. Work out whether she is one Power or several." — scores zero. Its companion
counts the person's *replies*, so seven demands bundled into one turn cost one reply. An earlier
version of that note called the reply count "the half no wording can defeat", which was wrong and
now says so. Four hand-written recordings that change rarely have one real guarantee, and
`CLAUDE.md` already states it for the prose: **read a changed transcript.**

### Slice A3 of the mutation audit: eight guards that did not hold, and the three that were the wrong shape

Eight findings from the mutation audit recorded in `docs/HANDOVER.md`, all in the engine and
validator. **None of them was a bug in the product.** Every one was a test that did not hold what
it claimed to hold, demonstrated by a mutation that left the whole suite green — and every fix
here is demonstrated by the same mutation turning it red, then reverting to green.

**The first three are one bug in three places, and it is worth naming as a class: a lookup that
silently skips what it omits turns its own omissions into exemptions nobody chose.**

- **A validation issue could name the wrong kind of thing.** `EachCodeReportsTheKindOfThingItIsAbout`
  did `TryGetValue(...) continue` over a table missing **eighteen of the validator's forty-five
  codes**, so `DUPLICATE_PRO` could be relabelled an Ability problem — verbatim the failure the
  test's own doc comment exists to prevent, and one a repair loop follows into the wrong
  dictionary. The fix is the shape rather than the eighteen entries: the table is now indexed
  rather than probed, so an unlisted code throws, and `TheKindOfEveryCodeIsWrittenDown` holds it
  to the validator **in both directions** — a new code fails until somebody writes down what it is
  about, and a removed one fails until its line goes.
- **An issue could offer options of the wrong kind entirely.** `EveryOptionOfferedIsOneTheRulesAccept`
  asked only whether each string was an id of *anything*, as a union over ten collections — which
  every id in the rules satisfies. So `POWER_WITHOUT_SOURCE` could offer the six Ability ids: a
  repair loop writes `"agility"` into a Power's Source, gets `UNKNOWN_SOURCE` back, and never
  terminates. What each code offers is now written down per code, and **a code carrying options
  that nothing characterises fails** rather than passing on the union.
- **The "every code is provoked" guarantee was spelling-shaped**, and so was every structural
  invariant downstream of it. The scan was `"([A-Z]+(?:_[A-Z]+)+)"`, which requires an underscore:
  a clean A/B on the same unreachable check had `TOO_MANY_CONNECTIONS` going red and
  `TOOMANYCONNECTIONS` staying green. It now matches by case. **The part that matters is that it
  is driven against a synthetic source**, in `TheCodeScanIsNotDefeatedByHowACodeIsSpelled` —
  reading the shipped validator cannot distinguish a pattern that finds every code from one that
  finds every code somebody happened to spell with an underscore. This is the third time that scan
  has been too narrow, and both earlier escapes were also *how the code was written*.

And the five specific ones:

- **A Power could be deleted from a sample character.** The budget assertion was `spent <= budget`
  and nothing else, and "fills every section" only asks whether a section is non-empty — so
  deleting `stun` from the Hero was green. The budget is now checked at both ends, the Powers each
  sample carries are recorded, and a second test says *why those Powers*: a baseline that is half a
  Trait against one that equals it, a rate below 1 HP per rank, a rankless Power, two Super Senses
  options, a Power carrying a Con, a Source-less Power. Naming the ids catches a deletion; the
  shapes catch a replacement that costs the preview the thing it was previewing.
- **A Power description could be replaced with unrelated prose.** `verified_fields` carries
  `description`, and that flag is a boolean claim about a page somebody read which **survived any
  edit to the text it was made about**: Armor's whole description became "A quiet afternoon in the
  garden, with tea." and every test stayed green, the verified flag included. The descriptions are
  now digested in `CanonicalPowerDescriptions`, so the claim is bound to the text and the failure
  names the page to go and read.

  **Three similarity framings were measured first and none of them is a rule** — recorded so
  nobody re-derives them. A description need not repeat its own Power's name: 48 of the 141 do
  not, and that is good writing. Word overlap against the printed entry in `data/rulebook/` fails
  because these descriptions are deliberately *re-worded* rather than quoted — Blind Fighting's
  shares one distinctive word in nine with the page it came from ("sight" for "vision", "fight"
  for "combat"), which is the policy working. Ranked against all 1439 sections of the book, 130 of
  the 141 match their own entry best, but Blind Fighting comes **215th** and the Super Senses
  options cannot be scored at all, because the book gives the group one entry rather than one per
  option. Every framing needs a threshold plus named exemptions. **What no test can say is whether
  a description is *true*** — a wrong sentence digests like any other. That is the same limit
  `CLAUDE.md` already records for the replay transcripts, and it is closed by reading the page.
- **An applicability caveat could state the opposite of the rulebook.** Penetrating's became
  "Applies to absolutely any Power at all, no conditions." and the suite stayed green, because the
  only test asked whether it was non-blank and ended in a full stop. This matters more than it
  would elsewhere: **the design is that a caveat is shown to the player instead of being enforced**,
  so its wording is the entire deliverable and there is no mechanism behind it to be right when
  the sentence is wrong. The fifteen are now transcribed in `CanonicalCaveats` with the printed
  clause behind each, **and the clause is asserted to appear verbatim under that option's own
  heading in `data/rulebook/ch02-characters.json`** — under its own heading rather than anywhere
  in the chapter, which a reviewer showed is a different thing: Carrier Attack transcribed with
  Ongoing's real printed sentence passed a whole-chapter search. A caveat must also *restrict* —
  every one opens "Only for" or "Not for" — which refuses the inversion even if the record is
  edited to agree with it. And the recorded clause has to say what it constrains: containment
  accepts any fragment, so trimming one to the boilerplate "This Pro applies to" was a correct
  quotation of the right entry that says nothing, and passed. What is left uncaught is a caveat
  that restricts the *wrong* thing, changed in both places at once; the printed clause sits
  beside it so a reader can see.
- **The pickers' documented "rules-file order" had no test**: `.Reverse()` on `ProsFor` was green.
  It is not cosmetic — Ch.2 prints Pros and Cons alphabetically and a player is looking one up by
  name. Asserted as a subsequence of the rules file, so which options a Power is offered stays
  `IsApplicable`'s answer.
- **`GradesFor`'s defensive intersection is behaviourally dead against the shipped data**, and
  that was the honest finding rather than a defect: every grade an allowance records is one the
  option prices, guaranteed by `EveryOwnTextAllowanceResolvesAndCitesItsPrintedText`, so replacing
  it with `return allowance.Grades.ToList();` changes nothing. It was still worth a test, because
  a guard whose only evidence comes from data that cannot exercise it is a guard nobody knows the
  state of — and the test had to be a **synthetic** allowance naming a key the Pro does not price,
  since no reading of the shipped data can reach it.

**One test-file defect, fixed:** `SampleCharacterTests` called `Sample("Hero")` against a
`which == "hero"` comparison — ordinal and case-sensitive — so it silently built the **Villain**
and the Hero's export path went untested under a test named for it. `Sample` now refuses a name
that is not a sample rather than falling through to the other one.

**The lesson from the previous slice held throughout**: a guard test that reads the shipped data
cannot tell you the mechanism reads it too. Three of the fixes here are driven against synthetic
input for exactly that reason — the code scan, the `GradesFor` allowance, and the caveat record's
anchor in the corpus.

**Then two adversarial passes — one told nothing about the work, one pointed at the fixes — found
six more, and three of them were the same defect one table over.** Both reproduced all nine
mutations as red first, so the fixes hold; what they found is what the fixes did not reach.

- **A kind table keyed by code cannot see a swap *within* a code's list**, and seven codes serve
  more than one kind. Changing `CheckTraitSources`' Ability argument to Talent reported
  `UNKNOWN_SOURCE` on `toughness` as a Talent problem with the suite green — the same
  never-terminating repair loop the slice was about. Closed by the rule the table cannot state:
  **the id has to name a thing of the kind claimed.** Deliberately with no exemption list — half
  these findings report an id the rulebook does *not* have, so "resolves against the rules" alone
  would have to excuse them, and excusing them is what let it through. An id resolves against the
  rules **or** against the part of the character the kind names.

  **The first version of that rule skipped `Character`, and this entry claimed it "covers every
  finding without excusing one", which was wrong** — a third review pass demonstrated it. Eleven
  codes are Character-kinded by design and two more may be Character *or* Power, so mutating a
  kind *towards* Character walked through the rule, through the "a subject is named" invariant,
  and through the table, which accepts any entry in a code's list: `PER_UNIT_WITHOUT_UNITS` could
  report a Power id as a fault of "the character". Character is checked now, against the three
  things that belong to a sheet rather than a Trait — a Perk, a package, a Pro or Con.
- **The code scan was still spelling-shaped**, one spelling further on: `"TooManyConnections"` was
  invisible to every guarantee built on it. Widening the pattern again would not have closed it —
  a pattern can always be out-spelled — so the case is now **enforced where a code is written**.
- **And it read one file**, so moving the codes to a constants class or making the validator
  partial voided all three guarantees silently. Both are ordinary refactors. It reads the engine
  now.
- **`MustOfferOptions` had no exhaustiveness check** — the identical hole to `ExpectedKinds`, left
  open on its sibling. A new code with `Options = []` omitted from the list passed both it and the
  option check, the first by not listing it and the second by having nothing to walk. Read off the
  source now, because a code that offers options only *sometimes* is what a behavioural check
  cannot see.
- **The sample budget floor could not see the Powers at all.** A Standard-tier package plus
  eighteen Traits clears half the budget alone, so a Hero with **no Powers whatsoever** passed it
  — and this file's own account of the fix said otherwise. Corrected, with the Powers costed
  separately.
- **The "every shape" test read `powers.json` rather than the sample**, so `Prerequisite` and
  `CostPerRank` stayed true while the preview stopped showing any of it: zeroing Armor, Danger
  Sense and Resistance left every shape "present" and Armor printing its bare baseline instead of
  4 free ranks and 4 bought reaching 8. It asserts on the selections now.
- The picker-order narrowing covered Pros only, so `ConsFor` reduced to `.Take(1)` passed it.

**Two of the round-two assertions were wrong when first written, and measuring corrected them** —
worth recording, because both were plausible: the Hero's Powers cost **20 HP of 125**, not a fifth
of the budget, and `pros.json` is **not** alphabetical (Zone/Nova sits between Area/Burst and
Armor Piercing, following the printed pairing), which is what makes the Pro half of the order
check real where the Con half cannot be.

**A third pass then re-reviewed those fixes. All six held; it found five more**, and the pattern
across all three rounds is worth stating plainly: *the escape is always one indirection past
wherever the rule was written.*

- The `Character` skip above — this file's own overclaim, now corrected.
- **`OwnerId` had no invariant at all**, only three spot tests, so the two sites they miss could
  hold the printed *name* instead of the id. That exact regression is recorded in the validator's
  own comment as having happened once already.
- **`UNKNOWN_TRAIT_SOURCE` was never provoked on the Talent side**, so the Talent arm of its
  option list was dead and could be made to offer perk ids with the suite green — the round-one
  defect alive on a branch no sheet visited. **The "every code is provoked" guarantee is per
  code, not per construction site**, and that gap is now demonstrated rather than theoretical.
  The case list reaches it.
- The spelling convention reads two call shapes, so **a third helper is invisible to it** — the
  same escape one indirection on.
- `MustOfferOptions` catches a code added with `Options = []` but not one written with no
  `Options` line at all and delisted in the same breath. That is a two-file coordinated edit, so
  rather than chase it the Source findings are now asserted outright — which is the case the
  option check existed for.

**A fourth pass re-reviewed those. All five held; it found four more, and two of them were this
file overclaiming again.** Both are recorded rather than quietly corrected, because the shape of
the mistake is the useful part: *a fix that enumerates one spelling of a thing gets out-spelled,
including when the thing being enumerated is the fix.*

- **The helper pin was the same mistake one spelling on.** It matched methods *returning* a
  `ValidationIssue`, so `AddFinding(List<ValidationIssue> into, string code, …)` slipped past on
  the angle bracket — and appending to a passed-in list is this validator's house idiom, not an
  exotic shape. A generic `Finding<T>` got past it too. This entry claimed "the set of methods
  that build a finding is pinned"; it was not. **Construction sites are counted now** — the thing
  that actually makes a finding, which cannot be hidden behind a signature — and the rule is that
  exactly one of them takes its code from a variable.
- **There are five places that offer the six Sources, not four.** This entry said four, and the
  test did too: it counted a line shared by the Ability and Talent arms twice and missed the
  fifth entirely. The one missed was the **blank** Source — a Trait that names a Source and then
  names none, which has its own sentence because printed through the other one it read "names a
  Source, '', that is not one of the six". Its options could be made perk ids with the suite
  green: the same dead-branch defect as the Talent arm, in the same method, one round later.
- **Requiring words after the boilerplate opener was not enough.** "This Pro applies to Powers
  that" cleared it, and twelve of the fifteen entries open "Powers that…" after their opener, so
  nearly all of them could be trimmed to the same empty phrase. It is the *content* word that has
  to survive.
- And that check refused a record holding the substantive clause **alone** — verbatim, under the
  right heading, strictly more informative — telling the reader to go and find a fifth opener in
  a book where nothing was wrong. The opener is optional now.

3446 tests to **3668** — 3544 on the engine, 124 in bUnit. Zero warnings at CI strictness.

### The rulebook corpus was materially wrong, and its tests could not see it

`data/rulebook/` shipped in [#43](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/43)
described as the book's text, verbatim beneath each heading. It was not, and the way it was wrong
is the dangerous way: **the damaged prose still reads as English.**

**The extractor split every page at a fixed midpoint and emitted the left half, then the right.**
That is right for the two-column body and destroys anything set full width — it cuts each line in
two and files the halves in different blocks. **Every chapter opening in the book is set full
width.** Chapter 2's read:

> "run game more Characters include all beings in the game world, from the Heroes the GM. They
> include not only sentient beings but also animals, and so on."

against a page that prints "…from the Heroes **run by the players to the Villains, Foes, Minions,
and Extras run by** the GM. They include not only sentient beings but also animals, **monsters,
mindless undead, unthinking robots, career politicians,** and so on." Two runs gone, the orphans
parked at the front, and nothing about the result looks broken.

Four more defects, all of which the suite passed:

- **135 of 1303 sections had no text at all.** Chapter 9 ended with eighteen sections scraped off
  the blank Hero Sheet form on printed p.189, under headings like `EEDDIITTIIOONN`.
- **83 sections carried a doubled page number mid-sentence** — "…per extra force field. 2299 PRO
  Inviolate…". The display faces are faked bold by drawing the text twice a fraction of a point
  apart, so the two passes interleave.
- **The rotated marginalia was read as prose**, giving `retpahc` — "chapter" reversed — as a
  section heading.
- **Two facing entries on printed p.52 were one section**, headed `OVERKILL PHASE SHIFT`. That is
  the two-column interleave `CLAUDE.md` already warned about, in the shipped data.

**Measured rather than asserted.** A word-adjacency check against the PDF, calibrated so the two
entries the old test vouched for score 2–6%, put **114 of 1095 sections at 10% or more fabricated
adjacencies**. The damage concentrated in chapter openings, tables and Ch.8's stat blocks; the
narrative Power entries were sound throughout, and Force Field's load-bearing sentence was
verbatim correct.

**The extractor no longer exists as a scratch project.** It was one, and by the time the output
was found to be wrong it was gone — so the corpus could be neither audited nor regenerated. It is
now `tools/RulebookExtractor/`, in the solution so it cannot rot:

```bash
dotnet run --project tools/RulebookExtractor -- <rulebook.pdf> data/rulebook
```

It finds the gutter **per page** and decides a line crosses it by whether a **word actually sits
astride it** — not by whether the line's outermost words fall either side, which is equally true
of two facing headings sharing a baseline, and is exactly how p.52 merged. Full-width lines then
break the page into bands, and the columns are read within a band. Furniture goes by rule rather
than by string: the running foot by position, the purchaser watermark **by font** (6pt Helvetica,
780 words — four per page across 195 pages, and nothing else in the book), the vertical chapter
title by text orientation. Doubled glyphs are removed by testing that two glyphs are the same
character *and physically on top of each other*, which "2299" is and a legitimate "aa" is not.

**Words are split on the page's own space glyphs.** Guessing from letter gaps turned "FORCE FIELD"
into "FORCEFIELD" — in the condensed display face a word space is barely wider than the gap
between two letters. A gap break is kept alongside, because two facing headings have no space
glyph between them at all.

**`RulebookCorpusTests` was almost pure shape-checking and now asserts content, every guard
demonstrated by mutation.** All the defect shapes were re-injected and every one goes red:
blanking a section's text, the given name of the watermark, a doubled page number, `retpahc`,
collapsing a chapter's citations onto one page, overlapping two chapters' ranges, stripping the
character names from Ch.8, and **the original scramble itself, pasted back verbatim.**

**Three tests do the heavy lifting, and each exists because a reviewer defeated what was there
before.** An adversarial pass rotated all 1,492 section bodies onto the wrong headings and the
suite stayed green; it deleted 90% of the book and the suite stayed green; it blanked all 33
sections of Chapter 5 and the suite stayed green.

- `TheCorpusStillHoldsTheWholeBook` — a floor on total prose. A null check cannot see truncation.
- `EveryPowerEntryOpensWithItsOwnPrintedStatLine` — **the one that ties a body to its own
  heading**, across 116 entries rather than four spot checks, by cross-checking the Range against
  `data/rules`. It is also the sharpest reading-order check there is: Ch.2 sets one Power after
  another down two columns, so any column mistake shows up at once as an entry opening with its
  neighbour's tail.
- `EveryPublishedCharacterIsNamedInChapterEight` — the names come from `PrebuiltHeroes`, which is
  held to the printed sheets, so it cannot be satisfied by whatever the extractor produced.

**And `ColumnLayout` is now unit-tested against synthetic pages**, because nothing in CI ran a
line of the extractor: the committed corpus can only show you layouts the book happens to
contain, and every failure here has been layout-shaped. Disabling either gutter detector fails
those tests.

**An adversarial review found the first attempt at this was not sound, and the headline defect was
still in the data by a new route.** That reviewer is the reason this entry describes a working
extractor rather than a plausible one, and what it caught is worth recording:

- **Sixteen pages were still column-scrambled**, 10% of the corpus, because the gutter search took
  the strict emptiest point and then measured the run at exactly that count. On printed p.83 the
  minimum sits on a **1pt spur** where two lines happen to end, while the real 20pt gutter beside
  it is crossed by seven — so the spur failed the width test, the page was called single-column,
  and both columns were emitted interleaved. The same defect this change exists to fix, reached
  from the other direction, on the very page used to demonstrate the fix.
- **108 headings the old corpus had were gone**, including every named character in Ch.8 — see
  above; a name is followed straight by its "hero"/"villain" label, so it never got a body and was
  discarded as empty.
- **The tests were close to theatre**, which is what the three content tests above now answer.
- **A claim in every chapter file was false.** The embedded note said the doubled glyphs were
  collapsed; the de-duplicator written for that job turned out to change **not one byte** of the
  output, because every element that doubles lives in the running foot and is dropped by position.
  It is gone, and the note now says what actually happens.

**And fixing the gutter took four attempts, each of which broke something the last one had fixed.**
Worth stating plainly, because the lesson is that this is not one rule: raising the tolerance fixed
p.83 and left the band too wide, so every line on p.81 read as full-width; switching to per-line
gaps fixed p.81 and broke **26 of Chapter 2's Power entries**, because on an ordinary two-column
page almost every line sits in one column and contains no gap at all. **The old extractor, for all
its faults, got all 116 of those right** — measured, not assumed, and that measurement is what
stopped a regression shipping as a fix. The answer is two detectors with a test each.

**Known limits, stated rather than tuned away.** Chapter 8 sets its stat-block headings in small
capitals, which arrive as `aBIlItIes` and `FlaWs`; two rules were tried — point size, then ascender
height — and the second was worse than the disease, uppercasing "Points" to "POINTS" while leaving
the real cases untouched. A table of three or more columns is read across rather than down. Both
are recorded in the extraction note in every chapter file. Tuning a heuristic until it looks right
on the two examples to hand is the thing this repository forbids everywhere else.

Chapter 9 now ends at printed 188. Printed 189 is the blank Hero Sheet form — a form, not prose.

### Two Heroes in the rulebook this tool refused, and the question nobody had asked

Item 1 asked for a per-element cost breakdown of the four Heroes that do not reconcile. Doing
it settled the residuals as far as they go — see that item, where the answer is that no element
is mispriced — and turned up something the ±1 hunt was not looking for: **two published
characters that this tool reports illegal.**

**Nothing in the suite had ever validated the twenty.** The hero tests ask what a Hero costs
and what their Edge, Health and Resolve come to. None asked whether the character the authors
printed is one the validator accepts. Running that once found both faults immediately.

- **Blastwave's six energy types came back as four `DUPLICATE_PRO` errors.** His Energy
  Absorption prints six, so five copies of Also X, and the Pro says so in its own entry: "You
  can absorb one extra type of energy … **each time you select this Pro**" (Ch.2 p.28); Energy
  Form's copy prices it "for every 2 extra Hero Points" (p.30), and the generic Affect
  Inanimate reads "You can apply this Pro multiple times" (p.48). `CostCalculator` has always
  charged every copy — which is exactly what lands Blastwave on his printed 125 — so the two
  halves of the engine were contradicting each other about the same sheet, one pricing it and
  the other refusing it. Repeatability is now `repeatable` on the option rather than a list of
  ids in the validator. **Only three entries carry it.** The other five Also X entries are
  priced per unit, where the extra Sources are a quantity on one selection and a second copy
  really would charge the same thing twice.
- **T-Kay's printed `Force Field 12d (Zone)` came back `PRO_NOT_APPLICABLE`.** Force Field is
  Self range and the Zone Pro applies to Ranged and Touch Powers, so the validator refused it
  and neither editor would offer it — a Hero in the rulebook could not be built here at all.
  The Power's own entry overrides the option in as many words: "Apply the **Zone** Pro to
  shield large areas, the **Ranged** Pro to shield things at a distance, or the **Area** Pro to
  shield large areas at a distance" (p.29). That is the same shape as Deflection covering both
  attack types, which the book also states as prose rather than as a marked PRO.

**`pros_allowed_by_own_text` is not the `available_pros` list coming back, and the distinction
is the whole reason it is safe.** That list was this project's guess at which options suited a
Power and it filtered *absolutely* — 68 Powers offered no generic Pro at all. This one records
a sentence the rulebook prints inside a Power's entry, needs one behind every id, and can only
ever widen. There is a test that no other Power claims it and that the three Pros reach no
other Self-range Power. A sweep of Ch.2 for prose naming a generic Pro found exactly one other
case — Illusions and the Zone Pro — and it is deliberately left alone: no printed character
exercises it, and the rulebook prices Zone by the base Power's range and gives no figure for a
Zone-range one. Force Field has the same gap and the Ranged price is charged, which is recorded
in its entry rather than smoothed over; neither reading closes T-Kay, who is 124 at +2 and 126
at +4.

Both fixes were demonstrated by mutation: removing the `repeatable` flags fails on Blastwave,
disabling the own-text exemption fails on T-Kay. The first attempt at both mutations **silently
did not apply** — `perl -pi` edited nothing and the suite stayed green, which looks exactly
like a fix that holds. `git diff --numstat` is what caught it, which is the check the handover
already insisted on for the reason it gives.

**Three adversarial reviews, by agents told nothing about the work, and none of them could make
the change certify an illegal character.** All three verified every rulebook quotation and page
citation in it, and one re-ran the Ch.2 sweep independently and got the same two hits. What they
found instead was that the containment was looser than this entry's own first draft claimed:

- **Two of the new paths were dead, proven by mutation rather than argued.** Stubbing the
  *generic* half of the repeatable lookup to `return false` left the whole suite green — the
  only repeat test used Also X, which resolves through the Power-specific branch, so Affect
  Inanimate was covered by nothing. And replacing the data lookup with
  `power.Id == "force_field" && …` hard-coded also left it green, so the JSON field was
  behaviourally dead as far as the tests could tell and a second entry added later would have
  done nothing while they said it was fine. A third mutation loosened the Power-specific branch
  to "anything repeats" and stacked three of Flight's Levitation Con into a character 2 HP
  cheaper with an empty error list — the "three Burnouts cancelled a 12d Ability" hole again,
  one lookup over.
- **The grade keys were the real defect, and a comment is not an enforcement.** Zone/Nova and
  Ranged are priced by the base Power's Range; Force Field is Self, which the rulebook does not
  price. `zone_ranged` (+2) and `zone_touch` (+4) were both accepted with no finding, so the
  same printed character costed two ways depending on which key was typed, and the only reason
  T-Kay came out at 124 was that the test fixture happened to pick one. The decision now lives
  in the data as a per-allowance grade list, intersected with what the option actually prices,
  and the validator and both editors read it through one seam.
- **The widening overrode more than it claimed.** It returned early above the rank-type check as
  well as the Range check, and because the applicability method takes the shared interface, an
  id in a field named for Pros would have exempted a Con sharing it. Neither was reachable with
  today's data; neither was prevented. Both are now, with tests that drive the mechanism against
  a synthetic Power rather than observing the shipped data.
- **The citation moved out of a free-text `notes` string** — which no code read and no test
  asserted — into a modelled field a test requires to be present, alongside a check that every
  id claimed resolves to a real Pro and every grade named is one that Pro prices. The standard
  for adding an entry was documentation, and documentation is what drifted last time.
- **The MCP server was serving a document that contradicted itself**: Force Field is
  `"range": "self"` and its Ranged Pro row said `applies_to_ranges: ["touch","zone"]`, with
  nothing to distinguish that from a bug. Rows now carry `allowed_by_this_power_text` with the
  printed sentence, the narrowed grades, and `repeatable` beside `needs_variant` — which was
  missing, so a model had no machine-readable signal that Also X may be listed five times.
- **And the sheet printed `Also X, Also X, Also X, Also X, Also X`.** Legal, and useless. One
  shared formatter now collapses a repeat to `Also X ×5` for the text export and both browser
  surfaces, so it cannot read one way on the sheet and another on the tab.

The reviews also found eleven things wrong with the written record, including two places where
this file contradicted itself within five lines about the 1 HP bound, a claim in `SKILL.md` that
would now teach a model to drop a legal Pro, and a `README.md` bullet still asserting the
unqualified rule. All corrected here.

**A fourth review was pointed at the fixes rather than the code, and three of the eight did not
hold while two held halfway.** That is the same proportion this file records from each of the
last three slices, and the same shape every time: a fix correct on inspection and pinned by
nothing.

- **The MCP fields were an untested claim.** Setting `repeatable` false in both serialisers and
  the printed sentence to null left the whole suite green.
- **The repeat collapse was too.** `PowerFormatter.ModifierLine` had no test, and reverting all
  three call sites to a plain join was invisible. It now has both — unit tests for the function
  and wiring tests for the text export and both browser surfaces, because a formatter test
  exercises the function and not the wiring, which is exactly the distinction that let this
  through.
- **"One shared formatter" was untrue of the terminal**, where two surfaces still joined raw and
  one dropped the variant key, so two grades of Charges read as one option listed twice.
- **The Range-alone claim was pinned by nothing**: the test's rank-type half used Degrades,
  which is a Con, so the Pros-only guard refused it before ordering could matter — two
  assertions that were really one. The rulebook has no Pro carrying a rank-type constraint, so
  the test now builds one.
- **`GradesFor` was covered and none of its three consumers was.** The published Heroes exercise
  the accept path only, since T-Kay is recorded with the grade that is allowed — so the refusal,
  which is the entire point of the narrowing, was never run. The wizard is still the known CLI
  gap; what changed there is that its option label no longer quotes a grade the prompt will not
  offer.

### The other half: four recorded conversations, replayed with the engine run for real

The MCP server serves people who code and can bring their own Claude. A visitor to the site
cannot bring one — that was settled before this slice started and was not re-investigated: a
claude.ai subscription cannot be lent to a third-party site, the API is separate billing with
no dependable free tier, and custom connectors are gated to paid plans. The two options were a
proxy the owner funds, which costs money, invites abuse and breaks the static-site property the
README advertises, or a replay. This is the replay.

**The half worth showing off is the engine deciding, and that half is live.** Four conversations
in `data/transcripts/` are played back at the visitor's pace at `/replay`; every Hero Point
figure, every derived stat and every finding beside them is computed in their browser, from the
character the transcript carries, as they reveal it. At the end the character is handed to the
existing editors so they can change a rank and watch the numbers move.

**No transcript holds a number, and that is the whole design rather than a discipline.** A turn
carries a *character* — the inputs — and never an answer about one. Several turns of one
conversation may carry one, which is what lets the recording about a draft that did not fit show
the draft not fitting rather than merely say so. `TranscriptTests` holds the prose to it: no
recorded line may quote a Hero Point figure, an Edge, a Health or a Resolve. Ranks are
deliberately allowed, because a rank is an input the transcript already carries.

**The transcripts were produced by driving the real server, not written as dialogue**, and the
searching is what shaped two of them. "Punches through time" returns Time Travel, Time Stop,
Precognition and Blink among twenty-one matches, which is the ambiguity rather than the answer,
and is why the policy asks whether it is one Power or several.

**And the cheap one records a mistake rather than a success, because that is what happened.**
Asked for a character who knows when she is being lied to, the search — "knows when someone is
lying, reads intentions" — came back with a mind-shield, an out-of-body Power and a radar sense,
all matched on the word "knows". The conclusion drawn was that the rulebook has no Power for it,
and the transcript said so, and **it was wrong**: `super_senses_lie_detection` does exactly that,
at Perception, for a flat price. What was skipped is the part of the answer that says how many
matched and that the list was cut — the guide's own instruction is to search a more distinctive
word before concluding the rulebook has nothing, and `found: 0` is the only case that means it.
Asking again in the describer's words rather than the model's returns two rows with it first.
That is now what the recording shows, which makes it the more useful of the four: this is the
failure the search's caution fields exist to prevent, made by the person who wrote them.

**The four cover what makes the design visible**: one at Street level where the interesting part
is a search that nearly buried the answer; one whose first draft is over a Standard budget and
has to give something up, with the trade offered and taken; one where four words could be one
Power or three; and a Villain, who has no Hero Point budget at all under Ch.9 — so the replay
shows that finding and explains it rather than hiding it, which is what the GM review step does.
Every character was cross-checked three ways: the MCP server, `dotnet run -- build --from`, and
the rendered page asserted figure by figure against `CostCalculator` in bUnit.

Smaller decisions worth keeping:

- **There is one sheet component and it gained a parameter rather than a twin.** `SheetView` and
  `DerivedStatBlocks` now take an optional character instead of always reading the one being
  built. That was not tidiness: the four big figures come from `DerivedStatBlocks`, so before it
  was given the recorded character to read, a replay printed the *visitor's* Edge, Health and
  Resolve under a recorded character's name. There is a bUnit test that loads a sample first, so
  there is a different character present to be printed by mistake.
- **What is handed off is a copy**, round-tripped through the character's own JSON shape. The
  library is read once at startup and shared by every visit, so handing the instance over would
  let the first edit rewrite the recording — after which the replay plays back a character
  somebody changed, and nothing on the page would say so.
- **The transcripts are read strictly**, the way a submitted file is. They are data that nothing
  recompiles, so the way they rot is a rename: read leniently, a transcript whose `AbilityRanks`
  had been renamed would replay a cheaper character with its abilities silently gone.
- **A failed transcript fetch does not stop the app.** The rules are a broken deployment if they
  are missing; the recordings are a demonstration, and refusing to let somebody build a character
  because a demo file did not arrive is the wrong trade in every direction. The reason travels
  with the empty library and the page prints it, so it is not a silent nothing.
- **The label is the first thing under the heading**, not a note at the bottom, and it says both
  halves: the words are a recording, the figures are not. A notice that only appears at the end
  has been read after it was needed, so the test asserts its position in the rendered text and
  not merely its presence.
- **The shell's budget bar does not render on a replay route.** It is the visitor's own
  character, in the same six-label format as the recorded one directly below it, and nothing on
  the page said whose was whose — worst on the Villain, whose own panel deliberately shows no
  budget, leaving the only budget on screen belonging to somebody else.
- **A failed load is not a bad link.** Both reach the same branch, and the page answered both
  with "that address does not name one of the recorded conversations" — so a deploy that missed
  the transcripts would tell everyone following a good shared link that they had typed it wrong.

**Three adversarial reviews, by agents told nothing about the work, and the worst thing in it was
in the demonstration rather than the code.** None could make a figure on the page disagree with
the calculator, or make the hand-off mutate the recording. What they found:

- **The lie-detection error above**, which is the one that mattered: a recorded line asserting
  something about the rulebook that the rulebook contradicts, on a page whose whole claim is that
  these conversations really happened.
- **A trade the recording offered that would not have worked.** "Take the storm down two ranks
  and she keeps the foresight" saves 6 against an overspend of 12 — the prose invited the visitor
  to make a change and watch, and the change would have left the character still over. Four is
  the true figure and lands it exactly. The guard tests covered the two *stored* drafts and had
  nothing to say about a trade described only in words.
- **Six of seven mutations survived the new tests.** Swapping the two speaker labels credited
  every line in every recording to the wrong side and nothing went red; putting Health and
  Resolve back on the visitor's own character passed under a comment naming all three; a figure
  written into a `Title` or a `Blurb` was unguarded because the honesty scan read `Text` only;
  numbers written as words walked past it; and the budget allowance was blanket, so the cheap
  character pushed to 146 against a 75 budget still passed every test here while the page
  rendered "Over by 71" beside a line saying she comes in under it. All closed, each with the
  mutation named in the test that now catches it.
- **Two unguarded engine calls on the sheet** — `PerkCost` and the gear line — which the replay
  did not introduce but did widen: an unknown id throws during render, and a throw during render
  in the browser takes down the app rather than one box. That reaches a restored character as
  much as a recorded one.
- **A whole-tree Qodana scan run in place reports 1471 findings for a commit that reports 0 from
  a clean export**, `.CSharpErrors` included, on files that build clean. Export before scanning;
  the note is in `CLAUDE.md`.

**A fourth review was pointed at the fixes rather than at the code, and found three more — one
of them inside a fix.** That is the same proportion this file already records from the last two
slices, and the same lesson: a fix without a mutation behind it is a claim.

- **The Hero Point box on a replayed sheet could still print the visitor's own total.** The test
  meant to close this went from asserting one of three boxes to three of four, and the box it
  kept missing is the headline figure a GM checks a character against. It also searched the
  box's whole text, so `105` over a sub-line reading "of 75" satisfied a search for "75". It
  reads the value element now and compares it whole.
- **Powers on a replayed sheet could print the wrong effective rank**, for the same reason and
  with nothing looking. A rank is what a player rolls.
- **The route check was case-sensitive while Blazor's routing is not**, so `/Replay/…` served a
  recording with the budget bar over it.
- The honesty regex did not include the bare word "points", and the Perk and Gear guards added
  in the previous round had no test at all — removing them left the whole suite green.

**Twice in this slice, a mutation pass reverting with `git checkout -- .` took uncommitted work
with it** — both times work written minutes earlier, both times needing to be redone from the
transcript of what had been changed. The repository already recorded this hazard from
[#30](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/30) in its single-file
form; it is written here in the whole-directory form because knowing about it was not enough.
**Commit before letting anything mutate files**, and verify a mutation applied — `git diff
--numstat` non-empty — before believing a green result, because a silently-failed edit and a
passing test look identical.

**And one limit is stated rather than closed: nothing checks whether a recorded sentence about
the rules is true.** The characters are held to the engine and figures are banned from the
prose, but a line claiming "the Trait Cap is a limit on Abilities alone" passes everything here.
The lie-detection error is what that looks like when it happens, and a person caught it. A green
suite says the characters are legal and no figure was quoted; read a changed transcript against
the rulebook before merging it.

### Conversational creation, half of it: the MCP server, and the questions worth asking — [#39](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/39)

`mcp/` is a stdio MCP server wrapping the same engine, so somebody can connect their own Claude,
describe a character out loud, and get a legal costed one back. It handles no credentials and
holds no key — the conversation happens in the client they already pay for. The setup a stranger
needs is in `docs/MCP-SETUP.md`; the dependency arrows hold at compile time, because `mcp/` references
`engine/` and `sheets/` and cannot reference `cli/`.

**The transport was the easy half and the question policy is the deliverable.** A description
under-determines dozens of fields and almost all of them can be defaulted without anybody
caring. Four change the character materially:

| | |
|---|---|
| **Which tier** | It sets the budget and the Trait Cap, and everything else is measured against them. Never guessed |
| **One Power or several** | "Punches through time" is Strike plus Blink, or Omni-Power, or Alternate Form. **The question a model is most tempted to answer silently**, and the one that most changes the build |
| **What they are deliberately ordinary at** | Every character has all eighteen Traits and the points for a 10d come from somewhere. A characterisation question, not an arithmetic one |
| **Where it comes from** | One of six Sources. Costs nothing and changes no rank, so it is inferred and stated rather than asked unless genuinely open |

Everything else — rank spread, which package, which Flaw, gear, Perks — is decided and *shown*.
**Ask at most three questions**: a questionnaire is a worse interface than a wizard, and the
wizard already exists.

That reasoning lives in **`mcp/QUESTION-POLICY.md`**, which is embedded in the assembly and
served verbatim as the `creation_guide` tool, so the document the next person reads and the one
the assistant is taught are the same bytes. `McpQuestionPolicyTests` holds it to the standard
`SkillDocumentationTests` holds the skill to: its example character goes through the strict
reader and the validator, every id in it must exist, and the four questions are asserted by name.

**Six tools, chosen by what a conversation needs rather than by mirroring the engine.**
`cost_character` beside `validate_character` is the engine's API: no turn of a conversation
wants a price without knowing whether the thing priced is allowed, and a separate costing tool
is an invitation to quote a number for a character that breaks a rule. So `check_character`
does both and is the only place the word "legal" is decided. The ten catalogues are one
`list_options` for the mirror-image reason. Powers get `search_powers` and `power_detail`
because 141 entries are searched rather than listed.

**Nothing in it computes a Hero Point**, and the test for that is not a reading of the code:
every figure in the report is asserted equal to the calculator's own answer for the same sheet,
figure by figure rather than by total, over legal and illegal characters alike — because a
front end that always said "legal" would pass a suite run only over legal ones.

Four descriptions were run through the published binary, which is how two of the decisions
above were found rather than reasoned:

- **A cheap character** came back with `TRAIT_BELOW_PACKAGE` on an Intellect of 2d under a
  package that grants 3d — the loop working, on a first draft written by hand.
- **"Superman, but also a detective"** came back at 152 against a 125 budget, `remaining: -27`,
  with the spending breakdown naming where it went. That is the number to quote and the trade
  to offer, never a quietly weaker character presented as what was asked for.
- **"Punches through time"** returned Time Travel, Time Stop, Precognition and Blink — which is
  the ambiguity, not the answer, and is why the policy asks whether it is one Power or several.
- **"He plays the trumpet so beautifully people weep"**, the interesting one, returned four
  unrelated Powers matched on a word inside their descriptions. **A search that always returns
  its five best rows reads as five answers however carefully the caution is worded**, so
  `nothing_matched_by_name` and a note now say it outright. The same run found that substring
  matching answered "she bakes bread in the city" with **Plasticity**; matching is word by word
  with a shared-prefix rule now, because a match like that is worse than none — nothing in it
  looks wrong. (This entry said *Elasticity*, which is not a Power in this rulebook. It was the
  third of three copies of that mistake and the one the first correction missed.)

Smaller things worth keeping: standard output carries the protocol and nothing else, asserted by
reading the source for `Console.` followed by anything but `Error` (the obvious check for
`Console.WriteLine` passes while `Console.Out.Write` ships); the rules are found beside the
binary rather than by walking up for a `.sln`, because a client launches the published program
from a directory of its own choosing; and `RulesLocation.Find` returns null rather than a guess,
since a repository built for a directory that is not there fails on the first tool call instead
of at startup.

**Three adversarial reviews, by agents told nothing about the work, and the search flag above
was the worst thing in it.** None of them could make `check_character` certify a bad character
or quote a figure that was not the calculator's — that ordering held under every hostile shape
they threw at it. What they found instead:

- **The honesty flag lied, in the direction that matters.** `nothing_matched_by_name` came with
  "which usually means the rulebook has no Power for this" — so *"he can fly"* returned Flight
  and then told the assistant there is no Power for flight, because "fly" is not a prefix of
  "Flight" and the entry matched on the word inside its own description. The flag was right and
  the advice was wrong. The three cases are now told apart, and `found: 0` is the only one that
  means the rulebook has nothing. **Two tests straddled this and neither could see it**: one
  asserted `"flying"` finds Flight, the other asserted the flag's meaning over four curated
  queries, and both passed while contradicting each other.
- **It was also computed after the list was cut to `limit`.** With `limit: 1`, a name match at
  position two became "nothing matched by name at all". `limit` is the caller's and no test had
  ever passed one.
- **With no matches at all it still said "name the nearest"** — an invitation to name a Power
  that was never returned, which is the failure this tool exists to prevent.
- **The startup check passed itself.** It warmed one catalogue, so a directory holding nothing
  but `tiers.json` started cleanly and then threw out of five of the six tools — the exact
  failure its own comment claimed to prevent.
- **A mistyped `PROWLERS_RULES_DIR` fell through to the shipped copy**, silently. The setup guide's
  troubleshooting is what sends a stuck user to set that variable.
- **Five guard tests were theatre**, and the mutations were demonstrated rather than argued:
  `AnUnknownPowerIsReportedWithTheNearMisses` never read `did_you_mean`; the Power detail test
  checked own Pros and not own Cons, so serving one in place of the other was invisible across
  106 options; `AWarningDoesNotMakeACharacterIllegal` used the Hero, which has no warnings, and
  compared zero to zero; the guide test compared `QuestionPolicy.Text` with a method returning
  `QuestionPolicy.Text`; and the catalogues asserted only that ids resolved, so reporting every
  Pro as costing nothing passed.
- **`ENGINE_COULD_NOT_ANSWER` was documented as one of three verdicts and produced by no test.**
  It cannot be reached through any character — the validator refuses every sheet the engine
  cannot price — so `Judgement.Report` is now a seam that a test can drive directly, and the
  guarantee keeping the branch dark is asserted where it lives rather than claimed in a comment.
- **Prose:** three claims were wrong, including one of this entry's own ("over legal and
  illegal alike" was true of the verdict test and not the figures test — the figures test now
  covers both), and `CLAUDE.md`'s "nothing may make `TotalCost` negative" describes a floor that
  is not in the code. What actually holds is `NEGATIVE_UNITS` and `checked`.

**A fourth review was pointed at the fixes rather than the code, and two of the eight did not
hold** — which is the same finding this file already records from the last slice, in the same
proportion.

- **The test for the truncation fix did not bite.** Its query's top row matched by name, so
  cutting the list to one still left a name match in it and the buggy and fixed versions
  agreed. Reintroducing the bug left all 94 tests green. The query now ranks a
  description-only row first and every name match below the cut.
- **Nothing asserted that `Program.cs` calls `ReadEverything`.** The unit test covered the
  method; swapping the program back to warming one catalogue left the suite green while the
  binary started cleanly on a one-file rules directory. There is now a test that runs the
  built program.
- **The runtime standard-output test was not the backstop its own comment claimed.** It drove
  the binary through the SDK's client and asserted the session worked — and a real stray line,
  spelled to evade the source scan, left the client perfectly happy. The client skips what it
  cannot parse, which is exactly why the test now reads the stream itself and requires every
  line to be a JSON-RPC message. Verified by mutation, both ways.
- And one of the new tests **hung** rather than failed when its mutation was applied, because
  the failure it looks for is a server that keeps running. It bounds its own wait now.

**A fifth review, of the whole slice, and a whole-tree Qodana scan.** The scan reports zero
again; getting there found that three tool parameters guarded against a `null` while declaring
themselves non-null, so the guards read as dead code — and a client really can send
`{"category": null}`, checked against the built binary. Two of the scan's findings predate this
slice and made the "reports zero" claim untrue: a doc comment pointing at a test renamed in
[#33](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/33), and a redundant
`Cast`.

The review found nothing that certifies a bad character, and four things a stranger would meet:

- **A correctly named field holding the wrong kind of value was reported as a misspelling.**
  `"might": "8d"` is the rank written the way the rulebook writes it — the likeliest first
  mistake there is — and the answer sent a repair loop hunting for a spelling error that did
  not exist. The two are told apart now by reading the same text leniently: lenient reading
  ignores unknown field names and nothing else, so if it succeeds the name was the problem.
- **A blank `PROWLERS_RULES_DIR` still fell through to the shipped copy in silence.** The
  refusal had landed on the argument and not on the variable, which is the one the setup guide tells
  a stuck user to set and the one a client's config writes as `""`.
- **`SKILL.md` said "a minimal legal character is a tier and one flaw"**, which the 1d Trait
  floor made false in
  [#34](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/34): it comes back
  with eighteen errors. Two documents teaching the same JSON shape disagreed, and the wrong one
  was the older and more linked.
- **The tier lookup in `Judgement` was outside its guards** while the class summary said every
  engine call was guarded. Unreachable today only because the validator makes the same lookup
  first, which is an accident of ordering.

**And one finding was left open on purpose, which is the interesting one.** `search_powers`
ranks "walks through walls" by putting twenty-one Powers on two points each — every one of them
matching only the filler word "through", Phasing among them — so which eight a caller sees is
alphabetical, under a caution calling them the closest entries. Weighting each word by how much
of the rulebook uses it was implemented and **reverted**: it fixed that query and broke "reads
minds", which dropped Telepathy out of the first three because four Powers carry "mind" in their
names. A half-tuned scorer is worse than a dull one, and tuning it properly needs its own
evidence rather than two examples. What shipped instead is the truth about each row —
`matched_terms` says which of the caller's words it matched, `more_beyond_these` says the list
was cut, and the caution says rows matching the same words are in no meaningful order. **The
ranking is a known limitation, recorded rather than papered over.**

**What this deliberately did not do** is the browser replay demo — the other half of the
handover's slice, and a slice of its own. A visitor with no Claude account has no way to bring
their own inference, and the recommendation there stands: replay real transcripts with the
engine running for real in WebAssembly, and label the replay as a replay.

### Nine ways an illegal character was reported legal, and the one thing they had in common — [#31](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/31), [#32](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/32), [#33](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/33), [#34](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/34)

The validator had 26 checks and looked thorough. It was thorough about **the shapes a menu can produce**. Both editors pick from lists and count upwards, so a whole class of invalid character was unreachable — and the checks had been written, implicitly, against what the callers could build. The headless command let a *file* in, and the class became reachable all at once:

| | The character that came back legal at exit 0 |
|---|---|
| An unknown tier id | 99d Ability, **zero findings** — the budget and the Trait Cap both hang off the tier, so one typo switched off both limits |
| The same Con twice | Six Abilities at the cap for **0 HP**, empty issue list; scaled up, 216 HP inside a 125 budget |
| A negative quantity on a Perk | *Paid* the character 1000 Hero Points |
| A quantity large enough to wrap | A total of −2,094,967,284 HP passed the budget check |
| Overkill on any Ability | 12d Intellect for 6 HP — the Brute Option is Might, and the id was never checked |
| No 1d Trait floor | Up to 18 HP undercharged, and eighteen Traits at a rank nobody can hold |
| A Trait below its package floor | Free, and therefore silent |
| An unknown starting package | Silently no package at all |
| Unenforced applicability | The Ranged Pro on a Self-range Power |

**Three things explain all of it, and they are worth remembering because they will recur.**

- **Free mistakes are silent mistakes.** A Con past a floor, a Trait under its package rank, a modifier on an unbought Ability: none moves a total, so no test can see one. The mistakes that *did* move a total were all found years ago.
- **Floors launder invalid input into plausible output.** A Power floors at 1 HP per 2 ranks, gear at 0, an Ability at `Math.Max(0, …)`. Each is a real rule. Together they mean nonsense does not error — it rounds up into a believable number. Three Burnouts do not crash; they produce 0 HP and a clean report.
- **The strongest test in the suite is structurally blind to some of this.** Rebuilding the twenty published Heroes to exactly 125 is the best end-to-end check here, and **all twenty take a package**, so every Trait of theirs sits at or above a floor. It cannot see the 1d minimum, the package floor, or anything that only bites a package-less character. A rule can be missing for years while it passes.

**What actually found them**: adversarial reviewers given hostile input, and reading the printed page line by line. Reasoning about the rules found none of them.

Also in this run: **Herald (Airmid) closed** — her sheet prints two Expertise Powers and one was never transcribed, worth exactly the 5 HP her wrongly-attributed package was absorbing, so 16 of 20 Heroes are now exact and the bound is 1 HP. **All twenty Hero page citations were ten pages out**, the error `CLAUDE.md` already warned about, still live because nothing read those numbers. And the rulebook itself turned out to be in `docs/` all along — `*.pdf` is gitignored, so it is absent from a worktree's `docs/`, and checking there reads as "there is no rulebook".

### Assisted character creation, and the three ways a character could be wrong and not be told — [#30](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/30)

This closes the assisted-creation item, which was numbered 5 when it was open — not the item
numbered 5 above, which is newer. `dotnet run -- build --from character.json` costs and validates a
character, writes both exports and exits 0, 1 or 2 — legal, illegal, unreadable — with one
JSON report on standard output for all three. A skill at
`.claude/skills/prowlers-and-paragons-character/` teaches the schema and the
propose/validate/repair loop.

**The ordering is the whole value and nothing in the new code computes a Hero Point.** A model
proposes; `CostCalculator` and `CharacterValidator` decide. Inverted, this would be a random
number generator with good prose — and it is only safe in this direction because the engine is
now trustworthy enough to be the judge.

**It reports and never repairs.** An over-budget character comes back with every issue and the
caller gives something up. Auto-clamping was rejected outright: a player whose concept did not
fit should find that out.

**A command was the right surface rather than an API or an MCP server**, and the reason showed
up immediately: it is testable, and the wizard is not. `HeadlessBuildTests` drives the command
through its writers rather than shelling out, so a failure names a line. That narrows the CLI
gap rather than closing it — the wizard's own steps still have no harness, and the exporter was
already exercised by `SourceTests`, so this is not the first CLI coverage in the repository.

**Three real bugs came out of pointing it at characters a wizard could never produce.** All
three are the same shape — an id nobody checked, and a character reported as legal when it was
not — and all three were invisible to a front end that picks from a list:

- **A misspelled tier turned off both limits.** The Hero Point budget and the Trait Cap both
  hang off the tier, and both were skipped when the id could not be found. A 99d Ability on
  tier `"stanadrd"` came back with no findings at all: the worst answer a validator has, which
  is a confident wrong one. Now `UNKNOWN_TIER`.
- **An unknown starting package was silently no package**, so the character paid full rate for
  ranks the package would have covered. That is the exact shape of the bug that put seven
  published Heroes 4 HP over once already.
- **Ranks bought against a Trait that does not exist were charged and then ignored** — counted
  in the total, checked against the cap, printed nowhere. Found by the skill's own example,
  which named a Talent the rulebook does not have and produced a clean report.

**A `ValidationIssue` now carries the facts as well as the sentence** — subject kind, subject
id, the owning item for a gear feature, value, limit, and the options a choice must come from.
A sentence is enough for a person and not for a repair loop, which would otherwise have to
parse "Intellect is 40d, above the Trait Cap of 12d" back into the three facts it was built
from. Every message is byte-identical; all six properties are optional.

**The tests mostly use the structure rather than asserting it is populated**, because a
non-null check passes with the wrong id in the field, the value and the limit the wrong way
round, or an option the data will not accept. So they read an issue, apply the repair it
implies, re-validate and assert the finding is gone — and there is an invariant for each of
those three failure modes.

**Those invariants were worth only what their case list reached, and the case list was the
thing nobody was holding to anything.** They run over a hand-written set of sheets, and two of
the codes added in the same change were not in it — while one of the invariants would have
failed if they had been, because its list of acceptable options had no abilities or talents in
it. Three tests therefore ran green over a surface that excluded the work they were written
for. The list is now checked against the validator's own source: every code it can construct
has to be provoked by some sheet, with three exempt by name and reason. That check is the
reason the count of cases went from 13 to 20.

Three smaller things, each a trap rather than a decision:

- **The input is the `CharacterSheet` shape, not the JSON export.** The export is a report —
  costs, derived stats, findings, all of them answers — and reading it back would mean
  rebuilding a character out of its own conclusions.
- **A field name that is not part of a character is now refused rather than ignored.** A
  misspelled `AbilityRanks` silently drops every ability, and what arrives is a cheaper, legal
  character nobody notices is wrong. Strict only on the submit path: the browser restoring its
  own storage wants the opposite, since a field removed in a later build should cost it a
  field rather than the character.
- **The deserialization moved into `engine/CharacterSheetJson`** so the browser's local storage
  and the command cannot drift. Its subtleties — `Populate` for the get-only collections, and
  the nulls the deserializer puts where the type system says it cannot — were found by an app
  that would not start, and are worth exactly one copy. Deleting the `Populate` line alone
  fails 26 tests across both projects, which is what that guard is for.

**Four adversarial reviews, by agents told nothing about the work, and they found more than
the slice itself did.** Ten reproduced ways to crash the command, ten shapes of character that
made the validator throw rather than report, fifteen surviving mutations, and thirteen false or
misleading claims in the prose. What is worth carrying forward:

- **A negative `Units` on a per-unit Perk was worth unlimited Hero Points.** `PerkCost`
  multiplies a price by a quantity and the perk total had no floor, so
  `{"PerkId":"contacts","Units":-1000}` paid the character 1000 HP and a sheet with every Trait
  at the cap came back `"ok": true`, exit 0, no issues. **This is the failure the whole slice
  exists to prevent**, reached through the one field nothing bounded — and it was found by
  someone attacking the command, not by anyone reasoning about the rules.
- **`Validate` was the one unguarded engine call, under a comment claiming every call was
  guarded.** Ten shapes of ordinary hand-written JSON came out as a stack trace with nothing on
  standard output and an exit code outside the three: unknown Pro, Con, Perk and nominated-Trait
  ids, variant and grade keys that were present but wrong, and nulls where an id belongs. Each
  is now reported by name with its choices attached, and `CheckHpBudget` carries a `catch` as
  well — the specific checks are what a repair loop acts on, the `catch` only promises the
  validator answers at all. **The new `UNKNOWN_TIER` check had made several of these newly
  reachable**, by fixing the tier so that the budget check ran.
- **Two places priced a character on the strength of the wrong flag**, so a warning about being
  at the cost floor could take the whole validation down with it.
- **The exports overwrote each other.** The file name is the character's plus a timestamp to
  the second, and two runs in one second left one pair of files with both runs reporting them —
  the first caller told its sheet was at a path holding somebody else's character. A repair
  loop runs many times faster than that.
- **A test whose name was the thing it did not check.** `TheExportsAreWrittenWhereTheCallerAsked`
  asserted that the *reported* path existed, which is true of wherever it wrote — so ignoring
  `--out` entirely passed it. Two more had vacuous repair loops: `foreach` over an option list
  with no assertion that the list had anything in it, so emptying it made the test pass by
  doing nothing.
- **`SkillDocumentationTests` validated the document against a second copy of the code.** It
  converted the subject-kind enum to its wire name itself instead of asking the command, so the
  two could disagree and both stay green — `gearfeature` on the wire, `gear_feature` in the
  document. It asks the command now.
- **Thirteen prose claims were wrong**, including two in this entry: "nine tests" for the
  `Populate` guard (26) and "the first coverage the CLI has ever had" (the exporter was already
  covered). Also a doc comment's "4 to 15 Hero Points" (it is 1 to 4), and a claim that 26
  construction sites were unchanged when every one had been edited. The reviewer checked each
  number rather than reading past it, which is the only way this file stays worth anything.

**A second round found more than the first, and the most valuable reviewer was the one asked
to audit the fixes rather than the code.** Four of the six it checked did not hold:

- **The quantity checks looked at five fields and there were six.** A Pro carries a quantity
  too, and a per-rank-per-unit Pro at −1000 drove a Power's rate to −498, which the rulebook
  floor caught at half a point per rank — so a 24 HP Power cost 6 in silence.
- **Making `TotalCost` checked was not enough**, because the wrap happened in the per-unit
  multiplications underneath it. Determination at 500,000,000 units cost **5 HP** and gave
  500,000,016 Resolve, at exit 0.
- **The validator still threw, for an eleventh shape.** The branch handling a Pro or Con printed
  inside a Power's own entry skipped the grade check and went straight past, so Drain carrying
  its own ungraded Only X produced the crash instead of the finding written for it.
- **An export could still leave half of itself behind.** The `.json` path is one character
  longer than the `.txt`, so at one name length the first write succeeded and the second did
  not — an orphan, under a base name that then looked taken, while the report said nothing had
  been written.

**And the worst thing found anywhere in the slice: `--from CON` hung for ever.**
`File.ReadAllText` on a Windows device name opens the console and blocks on a read with no end
— no output, no exit code, no end. `COM1` and `CONIN$` too; `NUL` and `PRN` happened to fail
politely, which is why the whole reserved set is refused by name rather than the three that
were caught. Worse than any crash, and the exact input the item that fixed `--from ""` had
asked about.

**The duplicate exploit is the one to remember.** Nothing rejected the same Con listed twice
and every cost floors at zero, so three Burnouts cancelled a 12d Ability exactly: six Abilities
at the Trait Cap for **0 Hero Points**, exit 0, and an issue list with nothing in it. Scaled up
it bought 216 HP of character inside a 125 HP budget; on Super Senses, where one floor covers
sixteen options, it bought the lot for 1 HP. The same shape appeared twice more — a flaw taken
twice paid Resolve twice for one drawback, and the Brute Option halved *any* Ability because
the id was never checked against Might.

**Two of the fixes were themselves dishonest, and the review said so.** A duplicate id in one
of the rules files throws the same kind of exception as a bad character, and the report blamed
the character — "a null where an id belongs, most likely" — for a fault in this program's own
data, which a repair loop would chase for ever. And a figure the engine could not supply with
no error beside it left a caller told to "fix the errors and try again" with nothing to fix.
Both now say whose fault it is.

**The process lesson, which cost real work.** A reviewer doing mutation testing restores each
file with `git checkout -- <file>`, and it reverted an uncommitted fix of mine in a file we
were both touching — the hazard this file already records for `git checkout -- .`, in its
single-file form. Commit before letting a mutation pass run, or give it its own worktree; the
re-run was given one.

**Also closed: the wizard's crash on a terminal it cannot read.** Recorded by the last health
check, in scope now because there is somewhere to send that caller. Spectre's `SelectionPrompt`
threw `NotSupportedException` out of the first step after correctly rendering the tier table;
it now says the terminal cannot be read and names the command that does not need one.

**What this deliberately did not do** is enforce the semantic Pro/Con constraints. Item 1b
named assisted creation as the one consumer that would justify about a thousand fresh
judgements against the book; the consumer exists now and does not need them, because a caveat
shown to whoever is proposing is what Ch.2 says the list is.

### Sources on Abilities and Talents, and the rank threshold that never existed

This closes what was item 2. `CharacterSheet` gains `AbilitySources` and `TalentSources`, `SourceGrouping` builds the `Abilities (…)` line a published sheet prints, and all four surfaces print it: the `.txt` sheet, the JSON export, the wizard's GM review, and the browser's `SheetView`. Both front ends can set it — a `TraitSourcePicker` on the Abilities and Talents tabs, and a Sources entry in the CLI's two rank menus — because a field no host can reach is the unused data this item was held open to avoid.

**The premise this item was written on was wrong, and finding that out was most of the work.** It cited "Ch.2 p.15" for *"the Sources for your Powers and Abilities with a rank of 7d or greater"*. Two things were wrong with that, and an adversarial review caught the second after the first had been fixed:

- **The sentence is on p.64**, in the Random Hero Generator, where it tells you which Traits to roll Sources for. Still Chapter 2 — an earlier pass "corrected" it to Ch.3, which was also wrong.
- **The Sources rule is on p.16, not p.15.** p.15 is Power Levels and Packages and has no Sources text at all. That error was inherited rather than introduced, and it was in `sources.json` and six other files; all are corrected now. The footers print each page number twice interleaved, which is what made it easy to get wrong — decode one, or use the table of contents.

What p.16 actually says is broader: *every* Ability, Talent and Power has a Source, and the Innate/Trained defaults hold *"at least when dealing with ordinary people. When dealing with supers and characters who aren't human, however, anything goes."* The clause "but these defaults aren't mandatory" appears **once in the book, in the p.64 sentence** — so quoting it as the Ch.2 rule, as this entry did until the review, was the same misattribution the entry was written to complain about.

**Read as a rank threshold it is contradicted by the sheets, in both directions.** Alabama Slammer marks 6d Perception and 6d Toughness; Citizen Soldier leaves 9d Willpower unmarked while marking his two 12s; Stronghold leaves 10d Intellect unmarked and marks 6d Agility. So the printed line is an **exception list** — the Traits whose Source is not the default — and there is no rule that derives it. It has to be stored, which is the whole argument for the field. `PrebuiltHeroTests.ThePrintedTraitSourcesAreNotARankThreshold` names both counterexamples so the derivation cannot be reinvented.

**Ten of the twenty sheets carry such a line and ten carry none**, and both halves are transcribed — a renderer inventing a line for every character would satisfy a test that only checked the ten that do. Three of the ten are printed `Abilities and Talents (All)`: both Heralds and Nano, where every Trait deviates, so the engine collapses a full set to that one line and a single Talent short of it does not collapse.

Three smaller decisions, each from the printed layout rather than from convenience:

- **A Trait on its default prints nothing, and setting one explicitly to its own default prints nothing either.** They are the same Source but not the same statement, so the picker removes the entry rather than storing it. Otherwise an ordinary character prints eighteen lines restating the rulebook at the reader.
- **Abilities on one Source are split by the Pros and Cons they carry.** The marking on a printed line covers the whole line — Stronghold's four Abilities share one `(Item: armor)` — so two Abilities with different Cons are two lines, never one line carrying a Con that applies to half of it. The engine prints `(Item)`: `SelectedProCon` has an id and a variant key and nowhere to keep "armor". That shortfall is recorded beside the transcription rather than tuned away.
- **A Source group can hold no Powers at all** — a Trait bought through powered armour on a character with no Tech Power — so the grouping is no longer gated on there being Powers, and three call sites that were gated on `SelectedPowers.Count` are not any more. The Powers *tab* still skips those groups, because it edits Powers and a heading with nothing under it says less than no heading.

**The persistence test had a blind spot this would have fallen into.** The round trip is deliberately checked against the engine's answers rather than a field list — but a Source costs nothing and changes no rank, so cost, Edge, Health, Resolve and every validation message are blind to it, and dropping `AbilitySources` from storage would have passed all five. The fix keeps the principle: it compares another *answer* — the Source headings and the trait lines under them — rather than adding two field names to a list that will go stale the same way.

A Trait with no Source is **not** reported by the validator, and that is the rule rather than a missing check: the rulebook supplies a default, so silence means "on its default". A Power has no default, which is why `POWER_WITHOUT_SOURCE` exists and no Trait equivalent does. An unknown Source id, or one recorded against a Trait that does not exist, is an error on both.

**Three rounds of adversarial review, and the third found more than the second.** What the reviews caught, beyond the citations above:

- **`(All)` was counted on the Source rather than on the line it appears on.** With a Con splitting the Abilities across two lines, the unmodified line read `Abilities (All)` while naming four of six.
- **A Trait recorded explicitly on its own default printed a line.** The editors strip such an entry, so it could only arrive from stored or hand-edited data — which is exactly the path that reaches the renderers without passing an editor. The filter moved into `SourceGrouping`, so the rule is now true of the engine rather than of two call sites.
- **A blank Source threw `ArgumentNullException` out of the validator** instead of being reported. The storage guard caught it so the app never died, but that is the ordering trap already fixed twice here.
- **Both editors offered the default twice** — seven options for six Sources — and the two spellings did different things: the blank removed the entry, the named one stored it.
- **The `.txt` sheet and the browser sheet both decided "is there a Powers section?" by counting Powers**, so a character built entirely out of powered armour printed `(none)` and lost the only record of where the armour came from. Reverting either left every test green.

**A third round found more than the second, and most of it was in prose rather than code.** A reviewer re-derived the page mapping from scratch and audited all 417 citations in the repository: **55 instances were wrong, in ten distinct errors**, nearly all of them predating this slice. Power Levels is p.15 and was cited as p.17 — including in a validator message a player reads. The twelve custom gear features are on p.93, not p.92, in 22 places including a field label in the browser. The Brute Option and the global "Half" rule were attributed to Ch.1; they are Ch.2 p.17 and the Introduction's Glossary p.7. Cons run to p.54 and Flaws to p.60, not 53 and 59. Four "quotations" were paraphrases or had words elided without an ellipsis. All corrected, and `sources.json`'s page is now asserted by a test, because reverting all six back to p.15 had left the suite green.

Two more code defects from the same round: `EffectiveAbilitySource` read the dictionary directly while everything else went through the default filter, so a stored blank made one JSON document contradict itself; and a Trait naming an unknown Source printed nowhere while a Power in the same state printed under the plain heading — the same principle applied to one and not the other. The CLI's Source menu also handled one Trait per visit, reprinting the whole rank table between each, for the four-Ability case it exists to serve.

**Five tests were theatre and are now not.** One asserted the opposite of its own doc comment and passed by taking only the first line of the answer; one accepted any print font size because it checked for the unit and not the value; one claimed to guard against deriving the line from rank while reading no production code at all (a 7d threshold in the engine left it green); and the `Talents (…)` line's text was unasserted everywhere, so the word and the ids could both have been wrong. `TraitSourcePicker` had no test at all — storing the default, dropping the change notification, and `@if (false)` round the whole control were all green.

Two of those were mine and are worth naming, because both are traps rather than slips. **The picker's tests all rendered the component directly**, so deleting it from both editor tabs left the suite green — the feature could vanish from the UI unnoticed. And they drove it with `Change("tech")`, which supplies the event value directly and never reads the option list, so **swapping every option's value from the Source id to its name also stayed green**; a real user picking "Tech" would have stored `"Tech"`, which is not an id. Both are now driven through the tab and through the values the markup actually offers.

**The remaining known gap is the CLI**: `ChooseTraitSource` and the review step's trait row have no tests, because there is no CLI test harness at all. Building one is a slice of its own, and the flow was read closely by a reviewer instead — which is how the missing loop and an unguarded `First` were found.

A health check over the whole solution afterwards came back clean — zero warnings at CI strictness, 2845 tests, no vulnerable packages, the published site carrying all twelve rules files at full size, and engine, `.txt` and `.json` agreeing on every figure for three characters. It found one thing, which predates this work and is recorded here rather than fixed in a slice it does not belong to: **the wizard crashes with a raw stack trace when its terminal is not interactive** (piped or redirected input, or CI). Spectre's `SelectionPrompt` throws `NotSupportedException` and nothing catches it, so `ChooseTierStep` dumps a stack trace after correctly rendering the tier table. A capability check and a plain message would fix it — but that is CLI behaviour, and there is nothing to verify the fix with until the harness above exists. **Fixed in the assisted-creation slice above**, which is where the caller hitting it finally had somewhere to be sent.

### Qodana reports zero, and the fix was not a baseline — [#25](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/25)

This closes what was item 3, and the conclusion was the opposite of the plan. A whole-tree scan reported **242** problems. 46 were real and were fixed. The remaining ~200 were three structural facts restated, and they are now silenced by name and by path in **`.editorconfig`** with the reason beside each — not baselined, and not by a severity floor, because both hide a finding rather than answer it.

**`qodana.yaml`'s `exclude:` list was the trap.** It accepts an inspection name, looks like it works, and does nothing: the .NET linter is ReSharper, which takes severities from EditorConfig. A named exclusion there is silently ignored and the finding still reports — verified by running the scan both ways, which is the only way to tell. The upside of the real mechanism is that Rider and the ReSharper command-line tools now agree with CI, which a `qodana.yaml` entry would never have given.

What is silenced, in one line each: `engine/Models/*.cs` exists to be deserialized by reflection and must keep its setters (four inspections, ~150 findings); the test transcription records document a rulebook page rather than being read; a `[Theory]` body asserting on its parameter is not a precondition guard; `JsonValue.Create(...)!` is load-bearing and removing it fails the warnings-as-errors build; and this codebase writes explicit constructors and named backing fields on purpose.

Among the 46 that were fixed, two were worth having: `PowerFormatter` compared a `double?` cost rate with `==`, and `CharacterSession` and `FileSystemRulesSource` carried three genuinely dead public members. Note that Qodana in CI runs in **pull-request mode** and inspects only changed files, so its count there is not comparable to a full scan — the command to reproduce one is in `CLAUDE.md`.

### The character survives a refresh — [#28](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/28)

It did not. The sheet lived in a scoped `CharacterSession` and nowhere else, so a reload — or opening a link someone sent, which `_redirects` serves with a 200 *precisely so links can be shared* — silently dropped the character and landed on "Choose a tier first". `CharacterStore` keeps it in the browser's local storage, reads it back in `Program.cs` **before the first render** (restoring in a component's `OnAfterRender` shows an empty sheet first, which reads as "your character is gone"), and writes through on every change.

**What is stored is `CharacterSheet` itself, not the JSON export.** The export is a report — derived stats, costs, validation findings, all of them answers rather than inputs — and reading it back would mean re-deriving a character from its own conclusions. The sheet is the inputs.

Nothing in the path may throw: a character saved by an older build, hand-edited storage, or a browser refusing local storage all mean "no character", and the app starts empty. A tool that will not open because of something it wrote itself is worse than one that forgets.

One engine change, and only one: `SelectedPower` has two constructors, and a deserializer given a choice makes none — it throws. `[method: JsonConstructor]` names the primary. That is the whole of it, and it is what lets a host round-trip a sheet without the engine growing a parallel set of data-transfer types to keep in step.

**The round-trip is asserted by comparing the engine's own answers** — same total cost, same Edge/Health/Resolve, same validation messages — rather than a list of fields, because a field list is exactly the thing that goes stale when somebody adds a field and does not think about persistence. They will not think about the list either.

Asset caching was checked at the same time and needed nothing: `scripts/write-cloudflare-headers.sh` already marks the fingerprinted framework assets `immutable` for a year, holds `/`, `/index.html` and the rules JSON at `no-cache`, and everything else falls to Cloudflare's revalidate-always default.

**The adversarial review of this branch found that the first version of the guard did not hold**, and the way it failed is the general lesson. It stripped nulls exactly one level deep — the four top-level lists, and a Power's missing Pros and Cons. A null one level below that (`"Pros":[null]`, a null `PowerId`, a null inside `AbilityModifiers`, gear with null `Features`) restored cleanly, passed the backstop in `Program.cs`, and then took the app down on the **first frame**, because the budget bar renders on every route and costs the sheet to do it. A blank page, from the class written to prevent one.

So the guard is no longer a list of shapes: the engine is asked to cost and validate the sheet once, and a payload it cannot answer for is not handed to the app — and is removed from storage rather than left to be re-read and re-fail on every visit. `InvalidOperationException` is deliberately **not** caught, because that is what the engine throws for a half-finished character, which is exactly the work this exists to keep. A list of shapes goes stale the first time somebody adds a field, and they will not be thinking about persistence when they do.

Two more from the same review. `Program.cs` threw away a character that had restored perfectly if only the JS call that sets the palette failed — two catches now, because the two halves fail differently and only one of them means "there is no character". And the confirm gate was on the wrong button: **loading a sample overwrites the stored character just as completely**, in one click, while reading as the safe option. All three controls ask now, and only when the sheet holds something to lose.

### The sheet in colour, and a test project that renders it — [#28](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/28)

**`tests/ProwlersAndParagons.Web.Tests` renders components with bUnit**, and it exists because of "Armor8d": Razor swallowed the space in `@name` + `<text> @(rank)d</text>`, the sheet printed the name and rank run together, it was fixed on the sheet, and the identical bug in a second spelling survived on the Powers tab for another whole slice. Every test this repository had read source files, and no source file looks wrong. Anything about what a component *produces* goes here now; anything about how it is *written* stays in `WebPresentationTests`.

It is a separate project because it is the only one that may reference `web/`, and referencing a Blazor WebAssembly project drags the whole component model in — the engine suite has no use for that. bUnit pulls AngleSharp at a version carrying a published advisory, so this project pins AngleSharp forward rather than suppressing NU1902; suppressing an advisory to make a build green is how a vulnerable dependency ships.

**The first version of these tests was audited and five mutations passed it**, which is worth recording because four of the five were things the tests' own comments claimed to guard. Deleting `colspan="2"` from the gear row flushed every piece of equipment against the right margin — the exact bug the file says it exists to catch. Dropping a section's `Title` printed a box with no heading. Putting `None.` back into an empty Perks box restored the regression the sheet was rebuilt to remove. Emptying the `.hp` rule set costs in the same face as ranks. Printing costs at 4pt was invisible to everything.

**The cleverest one is the general lesson.** A test forbids the string `Communications 0d`. The reviewer printed exactly that, visibly, by splitting it across two `<span>`s — because the helper that strips tags out of markup leaves a separator where each tag was. That is fine for a *positive* `Contains("Armor 8d")`, where a newline is not a space and a split element cannot fake a separator; it defeats every `DoesNotContain`. So the negatives read element text now, and the positives assert against **the engine's own answer** for every Power on both surfaces in both modes, rather than three named Powers on one page.

Also from that audit: the run-together guard counted its two sources summed and had exactly zero margin (the Villain sample has four Powers, the sheet seven, against a threshold of eight — one more sample Power and half of it went dark); `PowersTab` said "no rank" for a *ranked* Power sitting at 0d and ran Pros and Cons into one unlabelled list, disagreeing with the sheet about the same Power on both counts; `SheetSection` carried two dead parameters whose doc comment argued for the "None." regression; and `rule-line` was hand-written three times outside `RuledLines`, once inside a `MarkupString` that hand-rolled its own `HtmlEncode`.

**The printed sheet keeps its mode's colours.** The old rule was "both modes print light", which came out of a Villain sheet printing its near-black surface edge to edge. That is the wrong lesson: the fault was a dark *surface*, not colour. So the rule is now white paper and readable ink, and each mode restates its own `--heading`, `--rule`, `--accent` and `--muted` — Hero in navy and gold, Villain in crimson and brass, both on white. Colour appears as ink and as a tint behind a 6mm heading bar; nothing fills an area. `--primary` still prints white, because it is a fill token and the banner used it.

Also, all four from a read of the first coloured proof:

- **An unbought Trait prints `0d`**, not a rule to write on. 0d is a fact about the character; the blank rules are for Alias, Team, Origin, Notes and Details, which the engine has no answer for at all.
- **Hero Point costs are set apart from ranks** — smaller, lighter, letter-spaced, muted. A rank is what you roll; a cost is bookkeeping, and in the same face the sheet read as a receipt.
- **A baseline Power names its Trait the way the rulebook prints it.** `PowerFormatter` swapped underscores for spaces and stopped, so a stat line read "Baseline Rank (½ toughness)" — while the method's own doc comment claimed "(½ Toughness)".
- **A rankless Power now prints the rank that stands in for it**: "Against other Powers: Toughness 8d". It has no rank of its own, but it is not rankless when something Drains it, and that number was nowhere on the sheet.

Four print faults came out of the adversarial read of the stylesheet, and three of them are the same mistake in different places — **a rule that looks like it applies and does not**:

- **The `fill` boxes were opted into `break-inside: avoid`.** They stretch to the height of the tallest column and the Powers column is unbounded, so they are the *other* thing on the page that can exceed a page — and Chrome honours that request by pushing the whole box to the next page, which is precisely what left two thirds of page one white when the Powers box did it. They opt out now, for the same reason `.powers` does. There is a test naming both.
- **The 4mm line gap never reached Notes and Origin**, the two boxes whose entire purpose is being written on. `.sheet-section.fill > .ruled` sets `gap: 0` for the screen at specificity (0,3,0); the print rule was plain `.ruled` at (0,1,0) and lost regardless of source order. On a sparse character those lines could print about 2mm apart, which the rule's own comment calls "decoration".
- **`td[colspan]` beat `td:last-child` on source order alone** — equal specificity — so the fix for flush-right gear would have silently come undone if anyone moved it up the file. It is `tr > td[colspan]` now, and settled on specificity.
- Perks, Gear and Flaws printed at the 10.5pt body size beside 8pt trait tables, because only `.trait-table` carried the print size. That extra height is what tips a borderline character onto a second page.

### The sheet is the published Hero Sheet — [#27](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/27)

The previous pass made the printed sheet *correct* — A4, margins, ruled boxes, no mid-entry breaks, black on white in both modes. It did not make it a **character sheet**. It was a stack of full-width boxes down a page and a half: legible, and obviously the output of a program rather than something you would put on a table.

It is now modelled on the publisher's own **Ultimate Edition Hero Sheet** (`docs/…Hero_Sheet.pdf`, untracked — `*.pdf` is gitignored, get your own copy). A masthead of three boxes, three columns, a foot of free-text boxes, every section ruled with its heading centred in a bar. **The structure only**: no hex pattern, no wordmark, no colour scheme — those are LakeSide Games'.

**The reference is a form, and two things follow.** Every Ability and all twelve Talents print whether bought or not, with a rule where the number goes; and Alias, Team, Origin, Notes and Details — none of which the engine has — print as labelled blank rules rather than being dropped. That also answers the "an empty character prints five boxes saying None." finding from the last round: a blank sheet is now a usable blank *form*.

**One page, and it fills the page.** The three columns are equal height and the box marked `fill` in each absorbs the difference, so a short character does not print a third of a page with white underneath. That is `flex: 1` plus `justify-content: space-between` on the rules, not a tuned line count — the first attempt did count lines, and it tipped onto a second page the moment a character had a long Motivation.

Also: Hero Points joins Edge, Health and Resolve as a fourth big box, as on the published sheet — `105` over a small `of 125`, because `105/125` at that size runs straight out of the box, and a Villain has no budget to compare against so it shows the spend alone.

**On the browser's print header** — the web address, date and page number across the top. No page can remove it: it is the print dialogue's "Headers and footers" setting and it belongs to the person printing. The review step now says which switch to turn off. Chrome supports neither `@page` margin boxes nor page counters, so there is no CSS lever at all.

### The sheet is fit to hand to a player — [#25](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/25)

Mostly presentation. The engine is touched in three places and each is noted below: the validator's user-facing messages, one ordering bug it exposed, and a dead property. Three faults, done in the order that made each one smaller.

**The markup went behind components first.** 22 hand-written `class="panel"`, 21 `panel-head`, 19 `field`, 9 `sheet-section`, 8 `chosen`, 6 `stat-block`, 5 `options`, none of them shared. That is what made the print work expensive rather than the print work itself — ruled boxes and break rules had to reach every one of them. Nine components now: `Panel`, `Field`, `SheetSection`, `StatBlock`, `DerivedStatBlocks`, `OptionList`/`OptionRow`, `ChosenList`/`ChosenRow`, following the `RankRow`/`StepButtons` pattern that was already here.

It found a real display bug on the way: **the sheet printed "Armor8d"**. Razor strips the leading whitespace inside a `<text>` block, so a Power's name and its rank ran together on every ranked entry — and an adversarial pass then found the *same bug* still live on the Powers tab, where the separator sat inside a `<span>` instead. Both are one expression now.

**The printed sheet is the substance of the slice.** The whole print stylesheet was three lines that hid the navigation, and every consequence of that followed: no paper size, no margins, entries cut in half by page boundaries, no boxes — the screen builds them from `box-shadow` and panel fills, none of which print — and the palette printed as-is, so a Villain sheet was a full-bleed near-black page.

- **The palette is forced light for both modes**, as a third block of token overrides in `theme.css`. No rule anywhere else needs to know it is printing. `--primary` is a fill, so on paper it becomes white and the banner takes its weight from a doubled rule instead of a wash of ink; the derived tokens are restated rather than left as colour-mixes, because a mix of black into white is grey and grey prints as a smear.
- **A4, 14mm margins, ruled boxes, break control** on `.power-entry`, `.stat-block`, table rows, list items and the section boxes. `break-inside: avoid` is a request, not a guarantee — a box too tall for any page is broken rather than clipped, which is exactly the fallback a long Powers group needs — so it is safe to ask for on every box, and it stops a four-line Gear box straddling a page for nothing.
- **A running footer was tried and does not work.** `position: fixed` is not repeated per page by Chrome's print output; it renders once, at the top of page two, over the content. What does carry the character's name across every page is the **document title**, which the browser prints in its own header, so the review page leads its title with the name. A colophon prints once at the end. This is a real limitation rather than a solved problem: with the browser's own headers switched off, pages 2..n−1 carry no identification at all, and CSS has no portable answer — Chrome supports neither `@page` margin boxes nor page counters.
- **The sheet gained the Trait Cap and, for a Villain, a point total.** The Trait Cap lived only in the budget bar, which does not print, and it is a number a player consults mid-session. The Villain sheet printed no total at all, because the HP figure was gated on the budget being shown — but "how much character is this" is exactly what a GM wants from an antagonist.

**The judging was done from the PDF, not the screen**, which is the only way this is checkable: the sheet markup was captured from the running app, rendered against the live stylesheets with headless Chrome's `--print-to-pdf`, and the pages rasterised and read back. A three-sheet document forced breaks through every kind of block. Computed styles cannot tell you whether a break lands mid-entry.

**The copy stopped talking to developers.** The GM review step no longer says its exports are "built by `CharacterSheetRenderer` in the shared sheets layer… byte-for-byte what `dotnet run` produces"; the derived-stats page no longer credits `DerivedStatsCalculator`; validation findings no longer print `NO_TIER_SELECTED` at the reader. All of it stays in the `@* *@` comments and `@code` blocks, where it belongs. The machine codes are still in the `.json` export, because they are genuinely useful in a bug report, and the page says so. Rulebook references were left alone on purpose — chapters, page numbers and rule names are what a player wants.

**The tests were adversarially audited, and the first version of them was theatre.** An agent with no context on the work applied *thirteen* violations to `web/` at once — white ink on white paper, every page-break rule flipped to `auto`, the whole working UI un-hidden, a second `@media print` block undoing the first, a `<style>` block carrying `rgb()`, `class="wrapper panel"`, `<code class="tech">`, and the "Armor8d" bug reinstated — and all thirty-two tests passed. It also produced four *false* failures on legitimate edits, one of which was an em-dash entity (`&#8212;`) read as a hex colour.

That is worth recording because the lesson generalises: **a substring check against a whole file is almost always satisfied by something other than the thing being tested.** The rewrite parses instead — the print block is split into rules so a selector and its declaration are checked *together*, tokens are checked by value rather than by presence, and prose is derived by stripping tags, attributes, Razor expressions, comments and the `@code` block so a type name in a paragraph can be told apart from one in an expression. Where a test could not be made honest it was replaced by a general rule: no compound PascalCase type from `engine/` or `sheets/` in visible prose, rather than a denylist of the four phrases that prompted it.

**The validator's messages turned out to be the largest remaining developer-facing surface**, and no test in `web/` could ever have seen them — they are engine strings, printed verbatim on the GM review step and in both exports. They named files (`flaws.json`), printed raw ids at a player holding a book (`Power 'super_senses_thermal_vision'`), used form-field plurals (`flaw(s)`), and one told every Iconic-tier character that its tier "is marked needs_review" — jargon, and **false**: nothing in `data/rules/` carries such a flag and the check fires on the tier id regardless. `ValidationMessageTests` provokes every message from real sheets and holds them all to the rule, which found one more thing on the way: **an unknown Power id crashed the validator** rather than being reported, because the Trait Cap check asked for an effective rank before the unknown-id check had run. The same ordering trap had already been fixed once for gear.

Also fixed, from the same audits: `aria-pressed`/`aria-selected` were rendered as `aria-pressed=""` — Blazor's spelling for a true bool, which is invalid ARIA that reads as *not* pressed, so both controls announced the opposite of their state; a half-built `role="tablist"` with no tabpanel, no `aria-controls` and no roving focus, now plain buttons with `aria-current`; two sibling Pro/Con pickers emitting the same DOM ids, so a label focused its neighbour's control; the review page never redrawing on a mode switch, leaving an over-budget finding on a Villain sheet; `--muted` failing AA at 3.8–4.5:1 where it carries almost all the explanatory prose, now 6.0–7.5:1; a Hero focus ring at 1.8:1 that nobody could see, now its own token; every generic Pro and Con offered as a bare name and a price with its description unused; `"How many Hero Point (= 25 Vehicle Points)s?"`; ids humanised into `super senses thermal vision`; a rankless Power offered as a Boost baseline it could never raise; `color-mix()` with no flat fallback, which would have dropped the banner to unreadable rather than unstyled; gear rows set flush right; and the page heading opening with a focus ring drawn round it on every navigation.

**The rendering-test gap is closed.** See the entry above — `tests/ProwlersAndParagons.Web.Tests` renders components with bUnit, and it exists because of exactly this.

### Two sample characters, for previewing a sheet — [#23](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/23)

`SampleCharacters.Hero()` and `.Villain()`, offered on the tier page. An empty sheet previews nothing — no Source headings, no Pros and Cons, no gear line, every derived stat zero — so judging a layout or a palette change meant building a character first. These fill every section a printed sheet has.

They are this project's own characters rather than the published Ch.8 Heroes, which stay in the test suite where they verify the engine against printed numbers.

**`SampleCharacterTests` holds them to the same rules a player's character is held to** — legal, inside budget, fully priceable, every section filled, at least one Source heading, both exports rendering. That was not ceremony: writing them produced three genuine errors on the first run, all of which the tests named.

- Ranks bought on Powers that have none. `invisibility` and `lightning_reflexes` are `max_rank: 0`, priced flat.
- Danger Sense pushed to 15d against a 12d cap, and Resistance to 16d. Both take a baseline **equal to** an Ability rather than half it, so purchased ranks stack on 9 and 8 rather than on 4.

The lesson worth keeping: check `rank_type` and `prerequisite` before giving a sample any ranks. The Hero lands at 105 of 125 HP and the Villain at 119.

The Villain deliberately leaves one Power without a Source, so the sheet prints the plain `POWERS` fallback heading and the review step shows a warning beside a legal character. Both are worth exercising in a preview, and a test would otherwise be the only thing that ever saw them.

### The first real deploy, and the trap it walked into — [#21](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/21)

The site is up and the engine runs from Cloudflare: all six tiers render from the fetched rules, the Superhero Package costs 50 of 125, Armor at 4 purchased ranks with Burnout settles on **2 HP** rather than 0 — the rulebook floor, live — and both exports build with no CSP violations.

**`--branch` is a label Cloudflare compares against the project's configured production branch, not a branch it reads.** New projects default to `main`; we deploy `master`. The mismatch does not fail anything: the upload succeeds, wrangler prints a `master.<project>.pages.dev` alias, the workflow goes green — and the production URL and any custom domain answer 404, because no production deployment exists. Nothing in the logs says so.

The setup instructions omitted this, which is how it was found. Fixed three ways: the README makes the production branch its own numbered step and explains what going wrong looks like, the deploy step carries the same warning where someone editing `--branch` would read it, and the workflow now **checks the production hostname after deploying** and fails with the remedy in the error. A deploy step that passes while the site is 404 is worse than one that fails.

### A README audit, and the Roadmap section deleted for the second time — [#20](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/20)

Checked every factual claim in the README against the tree and the data. The counts all held — 141 Powers, the 71/62/3/2/1/2 split by `cost_type`, the 63/46/27/5 by `rank_type`, and every file's entry count. Five things did not:

- **The minimum-cost floor was documented as the reading that was already known to be wrong.** The README said "no power costs less than 1 HP per rank, or 1 HP per 2 ranks once a rate-reducing con applies". The floor is 1 HP per 2 ranks *always* — `MinimumRankedCost` has been that since [#7](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/7), and reading it the other way is what silently voided every Con on a 1 HP/rank Power. The code was right and the document described the bug.
- **The Iconic tier was described as flagged `needs_review`.** Nothing in `data/rules/` is flagged, and two other paragraphs of the same README said so. The open end is GM discretion, reported as an `ICONIC_TIER_OPEN_BUDGET` notice.
- **Qodana's code-scanning upload was described as "attempted but non-fatal".** It is skipped outright while the repository is private, deliberately — the workflow comment explains that a swallowed failure left a red annotation on every run.
- The project tree omitted `scripts/`, `Directory.Build.props`, `.github/workflows/` and `qodana.yaml`, three of which the README already referenced by name elsewhere.
- Two sections still described CLI-only behaviour as though it were the whole story.

**The Roadmap section had regrown a numbered summary of `PROGRESS.md`, and it had drifted again** — still advertising the Blazor front end and the Hero/Villain printable sheet as future work after both had shipped. That is the second time; the section says so now and carries only the pointer. A short version is not cheaper than one list, it is a second list nobody remembers to update.

### The two real Qodana findings in the browser front end — [#19](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/19)

**`Router.NotFound` was deprecated in .NET 10, and the build could not see it.** A `.razor` file sets a component parameter by string key rather than by referencing the property, so the C# compiler never encounters the `[Obsolete]` attribute — `dotnet build` reported zero warnings with warnings-as-errors on. Qodana's Razor-aware inspection is currently the only thing in this repository that would catch the next one, which is worth knowing before treating a green build as a clean bill of health for the components. Replaced with `NotFoundPage` and a real `Pages/NotFoundPage.razor`, verified against a published build: an unknown path renders it and keeps the address rather than redirecting, which is what the `200`-not-`302` fallback in `_redirects` exists to allow. Also five redundant empty statements in the characteristics tab switch.

**The other 243 findings were not fixed, and that is item 3's problem rather than this one's.** See it for the breakdown; the short version is that Qodana inspects only changed files, so moving `engine/` and `sheets/` re-reported all of them.

### Hosted on Cloudflare Pages — [#18](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/18)

`superheroes.softwaresamurai.net`, deployed by GitHub Actions on every push to `master` that touches the app, the engine, the rules or the deploy itself. Direct upload rather than Cloudflare's Git integration, so there is one deploy path rather than two that can disagree. Setup and the token scoping are in the README.

**The Content-Security-Policy is generated, and that is the part worth remembering.** Blazor emits an inline `<script type="importmap">` into `index.html` naming the fingerprinted framework assets, so its contents change whenever those are rebuilt. Under `script-src 'self'` an inline script is blocked and the app never boots — and the easy way out, `'unsafe-inline'`, gives up most of what the policy is for. `scripts/write-cloudflare-headers.sh` hashes the inline scripts of the `index.html` that was actually published, and **exits non-zero if it finds none**, because a hard-coded hash would rot silently and take the site down on some later deploy. CI runs the same script, so a policy that would break the app fails on the pull request instead.

`style-src` still carries `'unsafe-inline'`: the budget bar's width is a live number and arrives as an inline style attribute. That is the one concession, and it is scoped to styles.

The policy was verified by serving the published output through a host that applies `_headers`, not by reading it: the app boots with no violations, deep links resolve through `_redirects`, the mode switch works through JS interop, and — the one genuinely uncertain case — the `blob:` URL the `.txt`/`.json` download builds is not blocked.

Two security choices behind the arrangement, both about blast radius rather than the site itself, which is static and holds nothing:

- **The workflow never triggers on `pull_request`.** That trigger runs a contributor's workflow changes with the base repository's secrets in scope, which would put the Cloudflare token one PR away from anyone.
- **A subdomain and a token scoped to Pages on one account.** A leaked token can redeploy this one site and nothing else, and a mistake in the Pages config cannot reach the apex domain.

What it left open is payload size — see item 5.

### A browser front end, on the same engine — [#17](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/17)

A character can now be created end to end in a browser and exported, with the terminal wizard unchanged. This also closes what was item 7, the printable sheet with Hero and Villain styling — it belongs to a front end, and now there is one to put it in.

**The engine and the sheet exports are their own projects now, and that was the substance of the change.** Both used to be compiled into the root executable. A Blazor WebAssembly project cannot reference that — it would drag in Spectre.Console — and referencing the CLI would have inverted the one dependency rule this architecture has. So `engine/` and `sheets/` became class libraries, and `data → engine → sheets → host` is a fact of the build rather than a convention. `web/` has no calculator of its own and no way to reach one it does not reference, which is the guarantee the whole slice existed to test.

`sheets/` is new and is the less obvious half. `CharacterSheetExporter` built the two export documents and wrote them to disk in one method; the browser needs the same two documents but hands them to a download. The string-building moved out and the file-writing stayed, so both hosts emit byte-identical exports because there is only one copy of the code. It is a separate project because neither host may own it and `engine/` must stay free of presentation.

**Nothing in `engine/` changed.** No presentation code, no duplicated rules logic, no Hero/Villain flag on `CharacterSheet` — the mode is a palette and the only mechanical difference, that a Villain has no Hero Point budget (Ch.9), is handled by hiding the bar and filtering `HP_BUDGET_EXCEEDED` from the display. The validator is never told which mode is active, so the JSON export still records every issue.

Some things the build found:

- **`Content Include="..\data\rules\*.json" LinkBase="wwwroot\data\rules"` looks right and silently is not.** The asset gets registered with a content root of `wwwroot/` while the file stays outside it, so every request answers `200` with an empty body and the engine reports the rulebook as malformed JSON. The csproj copies the files into `wwwroot/data/rules/` before static-asset discovery instead, and errors if it finds none — the failure it guards against is a site that loads and then cannot start.
- **Trimming is off on publish.** `RulesRepository` deserializes with reflection-based `System.Text.Json`, so the trimmer may remove model properties it can only see through reflection, and the failure is not a build error but a silently empty rules set at runtime. Rooting the engine assembly would keep the smaller payload, but the local toolchain cannot run the trimmer at all — the ILLink task host crashes without the `wasm-tools` workload, on the stock template too — so that is a change nobody could verify here. Recorded in item 5.
- **Pros and Cons on Abilities offer Cons only, and that is the rulebook's answer rather than a shortcut.** Each option's entry states what it may be applied to; of 23 Pros and 28 Cons, exactly two name Abilities and both are Cons. The picker filters on that field, so the list follows the data.
- **Blazor's `#blazor-error-ui` needs a `display: none` rule of its own.** Without one it shows from the first paint and reports a failure that never happened — which it duly did, twice, before being noticed.

The palettes live entirely in `web/wwwroot/css/theme.css` as CSS custom properties on `:root[data-mode="hero"]` and `[data-mode="villain"]`. No component names a colour: a grep for hex literals and colour keywords across `app.css` and every `.razor` file returns nothing, which is what keeps the switch a one-attribute change. The role split matters more than the values — `--primary` is a fill and `--heading` is text, and they are kept apart even in the Hero theme where they coincide, because Villain `--primary` measures 2.0:1 on its surface and would be unreadable as type.

### The rules loader is decoupled from the filesystem — [#16](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/16)

`RulesRepository` called `File.ReadAllText` itself. A browser has no filesystem, so a Blazor WebAssembly build could not have run the engine at all — and the alternative, reimplementing cost and validation in JavaScript, is the one thing the architecture exists to prevent. `IRulesSource` is the seam, with a file-backed implementation for the CLI and an in-memory one for hosts that load the data themselves.

**The interface is deliberately synchronous.** Making it async would push `await` through every lazy collection on the repository and from there into `CostCalculator` and `CharacterValidator`, turning a pure instantly-callable engine into an async one for nothing. A host that can only load asynchronously does so once at startup and hands over strings. Fetching is the host's problem; answering questions about the rules is the engine's.

Both existing entry points are untouched, so no call site moved. `RulesRepository.DataFileNames` is new and is the contract a self-loading host works from — it cannot glob a directory that isn't there — with a test asserting it matches what actually ships, since a rules file added and not listed would leave a browser build silently running on an incomplete set. A missing file now throws naming the file and where it looked, rather than surfacing later as a null somewhere unrelated.

The tests hold the seam open rather than merely covering it: one builds a repository with no disk access whatsoever and checks it costs a character identically to the disk-backed one. That is the Blazor path, proven before the front end exists.

### Sources, and Powers grouped by them on every sheet — [#15](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/15)

**Six Sources** (Ch.2 p.16): Innate, Magic, Psychic, Super, Tech, Trained. Each names the Ability that stands in as a rankless Power's rank whenever Powers act on other Powers — Drain, Nullify, Dispel, Power Absorption, Power Mimicry. The split is even but not intuitive: Innate, Super and Tech use Toughness; **Trained uses Willpower**, not Toughness.

`GetRankAgainstPowers` is deliberately separate from `GetEffectiveRank`, which still answers 0 for a rankless Power. The default rank stands in *only* against other Powers; it is not the Power's rank. Folding it into the effective rank would feed Edge and Resolve figures the published sheets contradict, and a test pins that distinction.

**It is a rendering change too, and that was the point.** The `.txt` sheet, the JSON export and the wizard's GM review all listed Powers flat; they now print Source headings the way a published sheet does. The JSON gains `source`, `source_heading` and `rank_against_powers` — that last one is otherwise invisible, and is where the rule shows: Tech-Source Communications exports `effective_rank: 0` alongside `rank_against_powers: 5`.

All twenty published sheets have their grouping transcribed and a test asserts the engine reproduces each one's printed headings — Psidearm carries three groups, Alabama Slammer two, Talon one. A Power with no Source still prints, under a plain heading at the end, rather than being dropped from its own sheet.

**A correction to what this file said before.** It recorded that Abilities are printed with no Source marking. That is true of the Abilities block, but incomplete: the sheets record an Ability's Source as an `Abilities (…)` entry inside a Power group — Stronghold's four armoured Abilities sit under `TECH POWERS`. That became its own item, and is now closed — see the entry above.

### Pro/Con applicability is derived, not guessed — [#14](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/14)

Every Power carried hand-written `available_pros` / `available_cons` lists, and `ProConSelector` filtered on them absolutely — an option not on the list could not be selected at all. Those lists were this project's invention, and they were badly wrong: **68 of the 141 Powers offered no generic Pro whatsoever**, six Self-range Powers offered the Ranged Pro (which raises a Touch Power to Distant Range, and has nothing to raise on a Power that affects only you), and Self-range Teleportation offered the Touch Con for the same reason.

The rulebook never states applicability per Power. It states it inside each generic option — *"This Pro applies to Zone Powers"*, *"applies to Powers that only affect you"*, *"applies to Power Rank Powers and Baseline Rank Powers"*. So the 141 lists are deleted and the answer is derived from the option instead, by `ProConApplicability`.

**Ten entries constrain on something the rulebook prints for every Power** — its Range (Ch.2 p.19) or its Rank type. Those are enforced, each transcribed in a test naming the sentence it comes from. Every Power now offers Pros and Cons, and the counts move with Range as they should: 16 Pros on a Self Power, 18 on Zone, 19 on Touch and Ranged, and all 23 on the four Special-range Powers, where the book says such Powers "work in some unique way discussed in their descriptions" and so rules nothing out.

**The rest are deliberately not enforced.** See item 1b: they would need about a thousand fresh per-Power judgements, which is the same mistake in a new shape. They travel as a caveat displayed beside the option, and a test asserts a caveat never acts as a silent filter.

### Toxin Pros/Cons, custom gear, and two more Heroes closed — [#13](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/13)

Four pieces of work, two of which found real cost bugs.

**The three toxin Pros/Cons (Ch.7, pp.108-109).** The original extraction was scoped to Chapter 2, so it missed Caustic (−2) and Non-Lethal Disease (+2) on Stun, and Lethal Disease (+6) on Slay. A sweep of the whole book for a PRO/CON Hero Point marker returns exactly these three outside Ch.2 and nothing else, so Pros and Cons are now complete. Note Non-Lethal Disease is Stun, not Slay, despite being printed under Lethal Disease.

**Custom gear features (Ch.6, p.93).** Twelve features at 1–2 HP each, ten flat and two graded, plus ordinary Pros and Cons applied to a piece of gear. Gear has its own floor: *"no piece of gear can cost less than 0 Hero Points"*, where a Power floors at 1. The Item Con is deliberately **not** credited — Ch.6 says every piece of gear has it as a statement of what gear *is*, and Item is absent from the list of Cons the same page calls common on gear; crediting it would make every 1 HP feature free. Free-text mundane gear stays the wizard's default, since nearly all gear is free. Gear is the first thing to spend HP outside `TotalCost`'s four existing categories.

**Super Senses is one Power, and it was being overcharged.** Ch.2 says so outright: *"Regardless of the options you select, Super Senses is always considered a single Power."* Each option is a separate entry here only because each carries its own price — a storage decision that was leaking into the arithmetic. Cons and the minimum-cost floor are both written per Power, so both apply once to the group. The floor is what bit: most options cost 1 HP flat, so an Item Con recorded against a gear-mounted sense was swallowed by that option's own floor and worth nothing. The handover proposed a different fix for the same symptom — apply the Con to every option — which reaches the same numbers but multiplies a Con the sheet wrote once; rejected on the rules rather than the result. **Talon** closes exactly, Shadow moves +2 → +1, and Psidearm and Vigilant have one-option groups and correctly do not move. Super Senses is the only such group: Transformation says *"Regardless of which Transformation Power you possess"*, plural, and there is a test so this is not over-generalised.

**Vector's −6, the largest gap left, was Deflection.** Its entry says you pick physical *or* energy, and *"you can double the cost of this Power and spend 2 Hero Points per rank to be able to deflect both."* His sheet reads `Deflection (Physical and Energy) 10d`, so the parenthesis was buying that for free — worth +10. The other 4 was his starting package: packages are never printed and are inferred as whichever lands the rebuild on 125, and his Superhero attribution was a closest fit made while Deflection was underpriced. With it corrected the Hero Package is the only one that fits. To keep that honest, a new test re-runs the inference for every exact Hero and asserts exactly one package works — it passes for all fifteen, so no Hero rests on a package chosen because it helped.

**15 of 20 Heroes now rebuild to exactly 125**, and nothing left is more than 2 HP out, so that test's bound tightened from 6 to 2. Writing the gear validator also surfaced an ordering bug: gear that cannot be priced threw instead of reporting the gap, which the validator already guards against for Power selections. Fixed with the same pattern.

### Pros and Cons on Abilities, and what gear actually costs — [#9](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/9)

**Abilities can carry Pros and Cons.** The rulebook's Brute Option is Overkill applied to Might, and Stronghold buys four Abilities through his powered armour, so his sheet reads `Abilities (Agility, Might, Perception, Toughness) (Item: armor)`. Nothing modelled that. `CharacterSheet.AbilityModifiers` and `CostCalculator.AbilityCost` now do, including the Brute Option's half price and a floor of zero. Stronghold's Item Con on four Abilities is worth exactly −4, which is exactly what he was over by: **13 of 20 Heroes now rebuild to exactly 125**.

**Gear turned out to be a wrong assumption, not a missing feature.** This file previously listed gear as an unpriced cost contributing to the Hero Point gap. Chapter 6 says mundane gear is free and explicitly not tracked, and a Gear Limit is a cap on the Trait rank you can apply while using it, not a budget. So the wizard's free-text gear step was right all along, and the residuals recorded against "has gear" were misattributed — they are now corrected. What genuinely remains is custom *features* on mundane gear at 1–6 HP each, which is a much smaller and better-defined gap.

### Hero Pros/Cons transcription, and the package double-charge — [#8](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/8)

Transcribed the Pros and Cons each published Hero sheet carries, which turned the Hero Point reconstruction from a rough check into an exact one for most of them.

**It found a second cost bug, and a bigger one than the last.** With the Pros and Cons in, seven Heroes came out over budget by exactly 4 — including Citizen Soldier, who has no Pros or Cons at all, so it could not have been the new data. 4 is exactly what the Superhero Package saves: it costs 50 Hero Points for 3d in six Abilities and twelve Talents, which is 54 bought separately. `TotalCost` had been adding the package price **on top of** every rank at full price, charging twice for the ranks the package grants. That made taking a package strictly worse than not taking one, which cannot be right for something the rulebook sells "at a small discount".

With packages paying for what they grant, **12 of the 20 Heroes rebuild to exactly 125** — seven on the Superhero Package, four on the Hero Package, one on the Civilian. The sheets never print which package was taken, but for those twelve exactly one package lands the total on the point, so the inference is safe.

That is the whole engine end to end against numbers the authors published: package-aware ability and talent costs, baseline ranks, every cost type, and both generic and Power-specific Pros and Cons.

### Power-specific Pros and Cons — [#7](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/7)

Extracted the 102 Pros and Cons the rulebook prints inside individual Power entries, across 61 Powers. Completeness was checked by counting every PRO/CON marker in the chapter against the entries parsed: 102 markers, 102 entries, none unaccounted for.

These are not simply more generic Pros. Every generic one is a flat Hero Point change, but 23 of these are not: ten change the Power's cost **per rank** (Constructs' *Devices* is +2 per rank, so on a 6-rank Constructs it is +12, not +2), five are graded, five scale with how many extra Sources the Power reaches, and Alternate Form's *Independent Forms* scales per power level. `PowerProConModel` and `CostCalculator` now separate flat modifiers from rate modifiers to handle that.

**This found a real bug in the previous change.** The minimum-cost floor had been read as "1 Hero Point per rank", but the rulebook's parenthesis — "No Power can ever cost less than 1 Hero Point (or 1 Hero Point per 2 ranks) regardless of its Cons" — is the ranked form of the same rule, so the floor is 1 per *2* ranks. Read the old way, the floor sat exactly at the undiscounted cost of any 1 HP/rank Power, which silently made every Con on such a Power worth nothing. It went unnoticed until a test applied a Con to Armor and got no discount.

The wizard now offers a Power's own Pros and Cons first, marked as belonging to that Power, and prompts for a variant or quantity where one is needed.

### Chapter 1–2 rules verification and test suite — [#5](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/5)

Started as a README correctness check and turned into a full verification pass.

**Data.** Every power entry had carried `cost_per_rank: 1` with `cost_type: "per_rank"`, which was wrong for 91 of 125 — the rulebook prices Powers six different ways. There was no `range` field at all, and no rank-type distinction, so 46 rankless Powers were modelled as ranked and priced from ranks they cannot have. Buff was missing entirely. `powers.json` was regenerated from Ch.2 as 141 entries with correct range, rank type, costs and baselines, and verification moved from a single `needs_review` boolean to per-field `verified_fields` plus a `source_ref` page reference. All 141 descriptions were rewritten: the originals were invented, and 44 of the 46 rankless Powers described per-rank scaling that does not exist.

**Rules fixes.** Danger Sense *replaces* Perception when computing Edge rather than adding to it. Super Speed sets Edge to rank × 3 and had been missing entirely. Determination is 5 HP per Resolve with no rank, not 1 Resolve per rank — a 5× error. Overkill and Weak reduce the per-rank rate by 1 HP, not to a flat 0.5, which had mispriced every 2 and 3 HP/rank Power. The minimum cost is per rank, not 1 HP per Power.

**Tests.** 2053 tests wired into CI. `CanonicalPowers.cs` holds the Range/Rank/Cost printed for all 141 Powers; `RulesDataTests` holds the tier, ability, talent, pro, con, perk and flaw values; `PrebuiltHeroes.cs` transcribes the 20 published Heroes and asserts their printed Edge, Health and Resolve. Three of those Heroes independently confirmed the Danger Sense, Super Speed and Lightning Reflexes fixes.

**Other.** Pros, cons, perks, flaws, abilities, talents and tiers were all checked and found already correct; their flags are cleared. Four wrong claims in the README were corrected. `.gitignore` now excludes `*.pdf` repository-wide and CI fails if a PDF is ever tracked.

### Earlier

Predates this file, reconstructed from git history:

- **Toolchain, Qodana and README** — [#4](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/4). Qodana Community linter wired into CI, analyzer warnings as errors in CI only, README restored.
- **Back-navigation** between wizard steps, and **JSON export** alongside the `.txt` sheet. Both done — do not re-implement.
- **Initial extraction** of chapters 1–2 into `data/rules/`, and the three-layer `data → engine → cli` architecture.

---

## How to maintain this

When you finish a piece of work:

1. Move it out of **Remaining** and into **Completed** with a short account of what changed and *why* — the reasoning is the part that is expensive to recover.
2. Update **Current state** if the headline numbers moved (test count, entry counts, coverage).
3. If the work revealed new gaps, add them to **Remaining** rather than leaving them in a commit message.
4. Link the PR.

If a task turns out to be partly blocked, say so explicitly in the item and name the blocker. An item that quietly narrows its own scope is worse than one that stays open.
