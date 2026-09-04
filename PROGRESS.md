# Progress

The single source of truth for what is done and what is left in this project.

**This file must be updated as part of any task that changes what is done or what remains.** Not afterwards, not in a follow-up — in the same change, so the record and the code land together. Previously this information lived in two places (the README roadmap and a gaps list in `CLAUDE.md`) and drifted out of step with reality; both now point here instead.

Keep it honest. A half-finished item stays open with a note on what is missing. "Done" means done and verified, not written.

---

## Current state

| | |
|---|---|
| Rulebook coverage | Everything character creation needs. Chapters 1–2 fully extracted and verified, plus Ch.6's custom gear and Ch.7's toxin Pros/Cons. Chapters 3, 4, 5 and 7 are play rules, 8 is the pre-built characters (transcribed in the tests) and 9 builds Villains by the Hero rules — see item 3. **All ten chapters of the printed text are extracted into `data/rulebook/` and searchable at `/rules` by anybody with an account, whole book or one chapter at a time**, which is a different store and a different claim: that is the book's prose, and only `data/rules/` is verified entry by entry against the page |
| Powers | 141 entries, all mechanically verified against Ch.2 pp.21–48 |
| Power-specific Pros/Cons | 106 entries across 62 Powers, verified |
| Custom gear features | 12 entries, verified against Ch.6 p.93 |
| Other rules data | Tiers, abilities, talents, pros, cons, perks, flaws, sources — all verified, nothing flagged |
| Tests | **Five suites, and the figures are not written down here.** Run `./scripts/count-tests.sh` — it runs all five, reads each count out of the line that runner printed, and refuses to total anything when a suite did not report. **The figures used to be in this cell and went wrong four separate ways**; the four are recorded in [`docs/guide/testing.md`](docs/guide/testing.md), where the lesson keeps being true after the numbers stop being. The five are the engine, the components under bUnit, the accounts server over real SQLite, the pixel comparator, and the deploy's migration gate (`./scripts/test-deploy-gate.sh`, a fifth suite because the gate is a decision over wrangler's output and a workflow cannot be executed by any of the other four). **A sixth thing drives the assembled application and is deliberately not one of the five**: `./scripts/e2e.sh` publishes the site, serves it with the `wrangler pages dev` version the deploy pins, and drives real Chrome — six checks with a positive control each and a deliberately-broken twin of the whole site each, through either of two drivers (`--driver node|dotnet`; the second is Playwright and adds the axe-core accessibility check). It reports verdicts rather than a test count, so `count-tests.sh` does not know about it; see [item 10](#10-driving-the-assembled-app--stage-one-is-built-stage-two-is-only-a-decision-about-effort) for what it does and does not reach. |
| Wizard | All six creation steps working, with back-navigation and `.txt` + `.json` export |
| Front ends | Two interactive, plus two for a machine — the terminal wizard, a Blazor WebAssembly app, `build --from`, and an MCP server somebody can connect to their own Claude. All on the same engine assembly |
| Hosting | **Live** at [superheroes.softwaresamurai.net](https://superheroes.softwaresamurai.net), with the `prowlers-and-paragons-chargen.pages.dev` fallback; deployed from `main` by GitHub Actions. **The deploy applies pending D1 migrations before the Pages upload, and the apply half is now proven rather than assumed.** The first run failed on a file mode rather than the credential everybody was watching; the run after it read the live database, found nothing pending, and shipped — which established D1 *Read* only, because a token holding just Read produces that exact log and then fails on the first migration that actually has to be applied. **`0007_decision_recorded.sql` was that migration.** On the deploy of `a978806` the gate read one pending file, classified it additive, applied it (`0007_decision_recorded.sql ✅`), **and then asked the database again** — `No migrations to apply!`, the script's own positive control, which is what makes this "the schema moved" rather than "wrangler exited 0". So **D1: Edit is granted and the whole mechanism has now run end to end.** See [`docs/guide/hosting.md`](docs/guide/hosting.md) |
| Accounts | **Invitation only, and sign-in works end to end. An account is now what opens the rulebook** — all ten chapters, searchable at `/rules`, plus the recordings and the two sample characters. **All seven D1 migrations are applied to the remote database.** `0007_decision_recorded.sql` was the first the deploy ever actually applied — every gate run before it found nothing pending and *skipped* — and it went in on the deploy of `a978806`, which is what proved the D1 **Edit** half of the token; see the Hosting row. **The six before it**, `0006` included — the owner applied it by hand, and the figure here is the deploy's own reading rather than a claim: `wrangler d1 migrations list --remote` answered *“No migrations to apply!”* on the run of 2026-09-01, so `apply-migrations.sh` skipped the apply and the Pages upload went ahead. This row said **0006 is pending** and was right when written; it went stale the moment somebody did the thing the gate exists to automate, which is the ordinary way a measured figure in this file stops being true. The `DB` binding is in place, `/api/me` answers `401` with JSON — checked by the deploy after every upload — and all four variables are set. **A link has been requested on the live site, delivered, and used to sign in** — watched, not tested, because no test can do it. The fault that blocked it for a week was the API key and not `MAIL_FROM`; see [item 8](#8-the-mail-provider-is-refusing-every-send--closed-and-the-reasoning-here-was-wrong). **Adding an address now actually mails it** a one-click, three-day link — see [the archive](docs/progress/); until now the admin page said an address "can sign in now" and nothing ever told them so |
| Printed sheet | One A4 page on the published Hero Sheet's layout; Hero and Villain ink on white paper — see [the archive](docs/progress/) |
| Static analysis | Zero warnings at CI strictness; a whole-tree Qodana scan reports zero — measured, not assumed, on a clean export of the commit carrying this row. **Two measurements in one day are the reason to go on distrusting the figure.** Against `main` at `9add547` the same scan reported **2**, both `InvalidXmlDocComment` on a single unclosed `<para>` in `WorkflowFilterTests`, which arrived with the executable-bit guard in #114 and was reported by nothing for four days. And on the eight-package NuGet bump it reported **5** — the same 2, plus three `MethodHasAsyncOverload` in `AdminPageTests.cs`, **a file that bump does not touch**: a package upgrade moved an inspection in code nobody edited, which is the case a pull-request-mode scan structurally cannot see. Both are fixed and both are in the entry in [the archive](docs/progress/). Qodana came off pull requests deliberately, so the local `./scripts/qodana-scan.sh` that `CLAUDE.md` requires before one is opened is the *only* thing between a branch and `main` — the answer to both of these is to run it rather than to put the workflow back. Earlier: 2 on the export of `76a4f80` (a local constant named `Opening`, and a `cref` to `IRulesSource` that does not resolve from the test project's namespace), 3 on `master`, 37 across three reconciled slices, 23 in the redesign slice — every one found by somebody re-running it, none by CI. **Do not name this commit's own sha here**: it was tried and an amend orphaned it within the hour, which is a dead pointer of exactly the kind this repository treats as worse than none. Re-run `./scripts/qodana-scan.sh` rather than repeating the figure |
| Known-wrong data | None outstanding. Every published Hero is now also checked for *legality*, not only cost — see [the archive](docs/progress/), on the two the tool used to refuse |
| Licence | MIT, in `LICENSE`, covering this repository's own code only. The game system is © LakeSide Games. `data/rules/` holds structured metadata and this project's own descriptions; `data/rulebook/` holds the book's text **by the author's permission to this repository's owner**, is not served by the public site, and does not travel with a fork |

The engine reproduces the printed Edge, Health and Resolve of all 20 pre-built Heroes in Chapter 8, and rebuilds **16 of the 20 to exactly their 125 Hero Point budget**. The remaining four each rebuild 1 HP out, for a recorded reason — see [Close the last four Heroes](#1-close-the-last-four-heroes), where the bound is stated exactly: it holds of what is *modelled*, and Shadow's printed Gear box carries a custom feature that would put him at +2.

---

## Remaining work

Roughly in the order that unblocks the most. **[Item 11](#11-answered-it-is-a-tool-for-running-and-playing-pp)
is answered and is the entry to read first** — the owner has said this is a tool for running *and*
playing P&P, which unblocks all eight of the things that entry lists and widens what item 3 counts
as in scope. **Nothing here is a defect.** The tool creates, prices, validates, prints and exports characters
through four front ends, a visitor with no account can watch a real conversation build one, and the
last thing that *was* broken — the MCP server, [item 18](#18-the-mcp-server-did-not-start--closed-nothing-had-started-and-there-is-now-nowhere-for-that-to-hide)
— is closed. What is left sorts into three kinds, and **the kind matters more than the number**,
because two of them are not work an agent can pick up:

- **Waiting on the owner, not on effort.** [13](#13-the-owners-branding-and-the-sign-in-email)
  (branding, and a kit that lives outside this repository),
  [21](#21-variants-of-one-character-are-a-naming-convention-doing-a-structures-job) (whether
  character variants deserve a mechanism — left ajar on purpose, and its own entry recommends
  deferring). **Item 10's second stage left this group on 2026-09-02**: the danger it was waiting on
  a decision about was an artefact of the plan, not of the problem, and seeding the local database
  needs no application change at all.
- **Ready to build, specified enough to start.**
  [12](#12-the-interface-the-owner-asked-for-which-needed-none-of-item-11s-answer) (the three-door
  rearrangement, unblocked now item 11 is answered),
  [14](#14-a-combat-simulator--a-second-engine-and-the-balance-question-is-now-live) (a combat
  simulator, explicitly a *second* engine),
  [15](#15-the-trait-cap-is-the-tiers-and-a-campaign-may-want-a-tighter-one) (a campaign-tighter
  Trait Cap), [16](#16-the-tool-costs-one-character-and-a-campaign-is-a-roster) (the tool costs one
  character and a campaign is a roster),
  [19](#19-the-account-cap-is-set-by-hand-in-sql-and-a-gm-cannot-see-what-a-player-holds) (a screen
  over behaviour that is already correct), [1](#1-close-the-last-four-heroes) (the last four Heroes,
  1 HP out each), and **stage two of
  [10](#10-driving-the-assembled-app--stage-one-is-built-stage-two-is-only-a-decision-about-effort)**
  — the signed-in half of the driver, which is a change to `scripts/e2e.sh`'s server setup plus a
  seeded login row, and no longer waiting on anybody. **Stage one is built**, so the rest of this
  group is no longer landing on top of a hole: a slice here now has something that would notice if
  it broke the running app.
- **Recorded, with nothing asking for them.** [1b](#1b-semantic-procon-constraints-are-still-unenforced)
  (semantic Pro/Con constraints, no consumer), [2](#2-what-the-sheet-still-cannot-say) (a mid-sheet
  page is anonymous, with no portable CSS answer),
  [5](#5-the-browser-payload-is-large--a-characteristic-not-a-defect) (payload size),
  [20](#20-xunitv3-400-is-a-test-platform-migration-and-it-is-measured-but-not-done) (a test-platform
  migration, measured and blocked on MTP v2 versus the .NET 10 SDK),
  [3](#3-remaining-rulebook-chapters--mostly-not-this-tools-business-while-it-was-only-a-character-generator)
  (the play chapters, in scope in principle since item 11 was answered).

(Item 4, the Power search's vocabulary, is closed — see below.)

**Several of these touch the same files, so they are not independent slices.** 12 and 16 both
rearrange the app's chrome; 15 and 16 both reach into what a campaign is allowed to say about a
character; 14 and 3 are the same question about play rules from two directions. Two branches that
merge cleanly can still contradict each other, so take them one at a time and re-read this file
between.

Each entry below says what a slice on it would actually involve, including which approaches are already spent. Read the entry here before starting.

### 1. Close the last four Heroes

Sixteen of the twenty published Heroes now rebuild to exactly 125 Hero Points. The other four are held at a known residual in `PrebuiltHeroes.BuildByHero`, each with a reason:

| Hero | Residual | Why |
|---|---|---|
| Herald (Scathach) | +1 | Strike carries four Pros and Cons at once — most likely a variant reading |
| Shadow | +1 | Unexplained |
| T-Kay | −1 | `Limited: only for Telekinesis` does not say which grade |
| Vigilant | −1 | Its Jo Sticks are *Upgraded*, a custom gear feature worth +2 — which would take him to +1, not to zero |

Nothing left is more than 1 HP out, and the test asserting that bound has been tightened from 6 to 2 and now to 1, so it stays true.

**The "residuals pair up" lead is spent.** It was worth chasing and it paid twice — see [the archive](docs/progress/) — but what closed Vector and Talon was reading the rulebook entry in each case, not the pattern. What is left is −1, −1, +1, +1, and four values one point either side of zero pair up by chance. Do not read more into it.

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

**Re-run independently, with a second instrument, and the negative result holds.** A scratch console project referencing `engine/` directly (not this test project) reconstructed all four Heroes plus three controls (Talon, Psidearm, Stronghold) and printed every Ability, Talent, Power, Perk and package line CostCalculator charges, computing each Power's cost by hand from its rate, baseline, Pros and Cons rather than only calling `PowerCost` — Strike's four modifiers on Scathach (Deflect +4, generic Phase Shift +4, Reach/Throw +2, Item −1), Invisibility's flat 9 with generic Item −1 and the Power's own Jamming −3 on Shadow, Danger Sense's `baseline_equal` on Perception, Armor's `baseline_half` on Toughness, Strike's `baseline_greater_of` Might/Martial Arts, T-Kay's Force Field and Telekinesis Pros (`zone_nova`, `area_burst`, `overload`) — all read straight from `data/rules/*.json` and all confirmed to the Hero Point. Every recomputed total matched the recorded residual and every recomputed Edge/Health/Resolve matched the printed sheet, for all seven Heroes. Mutating one Con (T-Kay's Limited grade to *somewhat limited*) correctly flipped her total to 125 — the instrument reacts to a real change rather than agreeing vacuously.

**One genuine narrowing came out of the line-by-line arithmetic that hadn't been stated before, and it rules out rather than explains.** Swing Line and Wall Crawling are both priced at 1 HP per 2 ranks — exactly the rulebook's own floor rate — so `Base` and `MinimumRankedCost` are numerically identical before any Con is applied, and the floor ("no Power can ever cost less... regardless of its Cons") then swallows the Item Con whole: Shadow's Swing Line and Wall Crawling, and Vigilant's Swing Line, all cost exactly what they would with no Con recorded at all. This is the rulebook's own stated rule working as intended, not a bug, and it is neutral — the same floor would have bound for the authors too, so it explains none of the four residuals. Recorded so the next attempt does not spend time re-deriving it.

**The other question a cheap instrument could finally answer: does any package other than the recorded "closest" one land any of the four on exactly 125?** No — swept across every package whose granted ranks the Hero's printed Traits do not fall below, none of the nine viable alternate (Hero, Package) pairings reaches 125. `PrebuiltHeroTests.NoOtherPackageLandsAnyOfTheFourUnclosedHeroesOnExactly125` pins this now, with a positive control (the candidate list must be non-empty) and was watched to fail: deliberately asserting against T-Kay's real civilian-package total (127) rather than 125 turned three of the four theory cases red, then was reverted.

**One thing the pages did add, and it widens rather than closes.** Shadow's Gear box prints `2 Pistols: 9d Ranged (Silenced)`. Silenced is a Ch.6 custom feature at 1 HP, and the pair is one price under his Two-Fisted — so transcribed, Shadow is **+2**, not +1. The "nothing more than 1 HP out" bound above holds only because gear features are not modelled on these transcriptions. Recorded rather than half-applied, exactly as Vigilant's Upgraded Jo Sticks are.

**What the breakdown did find was two defects, and neither is a Hero Point.** Both made a character printed in the rulebook one this tool refuses — see [the archive](docs/progress/). They were reachable only because nothing had ever asked the validator about the twenty; `EveryPublishedHeroIsALegalCharacter` now does.

Four rebuilds 1 HP out, each with a recorded reason — and one of them, Shadow, 1 HP further out than that once his printed gear is counted — remains a more honest state than four zeroes.

One thing genuinely cannot be modelled as things stand: Eidolon's `Omni-Power (Mind Link)` applies Telepathy's Pro to a *mimicked* Power. Pros are stored per Power, so there is nowhere for it to live. Eidolon reconciles anyway, so it costs nothing today.

### 1b. Semantic pro/con constraints are still unenforced

The invented per-Power lists are gone — see [the archive](docs/progress/). What is left is the half of the constraints that cannot be checked against anything the rulebook prints per Power: "Powers that inflict physical or energy damage", "Powers that can be activated and deactivated at will", "attack Powers", "Powers that last or can be maintained". These are shown to the player as a caveat on the option and left to the GM, which is how Ch.2 frames the list.

Enforcing them would need roughly seven booleans on each of the 141 Powers — about a thousand fresh judgements against the book. That is worth doing only if something downstream actually needs it, and the obvious candidate was assisted creation, where a model proposing a character benefits from the engine ruling out illegal combinations.

**Assisted creation has now shipped without them, and did not need them** — see [the archive](docs/progress/). A caveat is shown to whoever is proposing and left to the GM, which is what Ch.2 says it is. So this stays open with no consumer asking for it, and the caveat remains honest where the guess would not be.

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

**A second fault was masking this one and is fixed** — see [the archive](docs/progress/). Every
attempt was counted before the send, so five refusals spent the hourly allowance and every try
after that answered the same cheerful `204` a sent link gets. That is why the site said a link
was on its way, Resend's dashboard showed nothing and Cloudflare showed nothing: by then nothing
was being attempted.

### 1c. The extractor loses the book's paragraph breaks — **closed**

`RulebookProse` used to pull a Power's stat line and its Pros and Cons out of the flat run the
corpus held, leaving a description that was **still one paragraph** — LUCK's was 120 unbroken words.
The breaks were never missing from the PDF, only from the corpus: the page sets a paragraph start
with more vertical space above it than an ordinary wrapped line gets, and that gap is measurable.

**`PageReader` now measures it, and `ParagraphJoiner` (new) decides with it.** Every `Line` carries
`LeadingGap` — the baseline distance to the line physically above it in the same column-run, `null`
wherever that comparison would be meaningless (the top of a column, the line right after a heading,
the line right after a full-width break, or the first line of a fresh page). `ParagraphJoiner.Join`
starts a new paragraph, joined by `\n` instead of a space, when a line's gap exceeds **1.4x the
smallest gap measured elsewhere in that same passage**.

**The threshold is local to the passage, not a book-wide constant, and that was the real finding.**
The book does not set one leading throughout: 9pt Chapter 2 body text is normal-12pt, 8.5pt Chapter 8
prose is normal-10 to 12pt (the two wobble against each other with nothing meant by it), and the
Introduction's own 11pt single-column style is normal-**18pt** — wider than Chapter 2's own
*paragraph-break* gap of 18pt. A single fixed point value cannot be both "wider than Chapter 2's
normal line" and "narrower than the Introduction's normal line" at once; picked for one it invents a
break on every ordinary line-wrap of the other, or misses every real break in the first. Using each
passage's own smallest observed gap as its baseline sidesteps this, because a paragraph break only
ever *adds* space — the tightest gap in any passage is by definition an ordinary wrap.

**Measured across the whole book** (`RulebookExtractor --gaps <from> <to>`, a new diagnostic mode
alongside `--page`): ordinary leading clusters tightly per passage (12pt at 9pt type, 10–12pt at
8.5pt, 18pt in the Introduction), and the closest any genuine break in this book ever sits above its
own passage's normal leading is 1.5x (LUCK's second PRO block: 12pt normal, 18pt above it). 1.4x
clears that in both directions.

**Verified byte-for-byte before touching anything**: the extractor reproduced the committed corpus
exactly (`git hash-object` matched `git rev-parse HEAD:<file>` on all ten chapter files) — the
positive control on the whole toolchain. After the change, regenerating only ever swaps a joining
space for a joining newline: total character count and the whitespace-split word-token stream are
identical to `HEAD`, chapter by chapter, checked by script rather than eyeballed. **439 of 1,523
sections gained a total of 888 paragraph breaks**; the rest were already one paragraph and stay that
way. LUCK now reads as four: its own stat line, the description, the PRO Control block, the PRO
Unbelievable block — spot-checked against printed p.33, along with FORCE FIELD (p.29, two
description paragraphs split correctly at "You can shape your force field...") and the Introduction
(p.5, four paragraphs, matching the printed page exactly including the two 30pt breaks against an
18pt normal).

**The over-splitting guard is scoped to Chapter 2's Power entries, deliberately, not to the whole
book.** A scan for a paragraph starting with a lowercase letter — the cheap, strong signal of a break
invented mid-sentence — finds 84 instances outside Chapter 2, and every one of them traces to a
pre-existing, already-documented extraction limitation this slice did not touch and does not fix: a
table of three or more columns read across rather than down (the vertical-gap logic then also splits
it at row boundaries, without repairing the underlying scramble), and Chapter 8's small-capitals
field labels ("orIGIn:", "aBIlItIes") which extract with their case as stored, so a genuine,
*correct* field-to-field break can start with what looks like a lowercase letter. Asserting the whole
book would either need to special-case both (a denylist of the exact shape this repository's own
guidance warns against) or hide a real regression in the 116 Power entries this fix exists for behind
noise from pages it was never meant to touch. `RulebookCorpusTests.NoChapterTwoPowerParagraphContinuesMidSentence`
checks all of Chapter 2 (with a positive control: over 50 real splits actually reached) and finds
zero. What it cannot catch, honestly: a false split landing right after a full stop, or one whose
next word happens to be capitalized regardless (a proper noun, "I", a quoted sentence) — those still
read as English, which is why the spot checks against the printed page above exist alongside it.

**Watched to fail.** `ParagraphJoinerTests` (9 cases, unit-level against made-up passages) and
`RulebookCorpusTests.LucksDescriptionKeepsItsFourPrintedParagraphs` (corpus-level, real book) were
run against `ParagraphJoiner.Multiplier` mutated to 1000 — both went red, the corpus test with the
exact shape of the original bug: *"LUCK's description is still one flat run of 1343 characters."*
Restored via `git stash`, both suites green again afterwards, not just before.

The presentation side needed no change, as expected: `BookText` renders whatever paragraphs it is
handed and `RulebookProse.Read` already returned the passage's own `\n` splits, so the corpus gaining
real breaks shows them with no code change on the browser side. `RulebookProseTests` (34 cases, all
green) is the proof — it drives the real corpus, unmodified.

**Re-verified on merge rather than taken on report, and the first instrument was wrong.** A raw-byte
comparison of the corpus files said every chapter had gained characters *and lost words* — which
looks exactly like corruption and is an artefact of the measurement: in JSON a newline is the two
bytes `\` and `n`, so a byte-level word split stops seeing a break as whitespace and welds the words
either side of it into one token. **Decode the JSON before comparing it.** Done properly, and
section by section rather than chapter by chapter: all **1,523** sections have a byte-identical word
stream to the previous corpus, the section count is unchanged in every chapter, and the breaks added
total exactly 888. So no word was added, removed or reordered anywhere in the book — the only
question a check like that leaves open is *where* the breaks landed.

**And re-running the third suite found a real defect the first two could not see.** The corpus is
*baked* into the accounts server by `scripts/inline-rulebook.mjs`, and regenerating
`data/rulebook/` does not re-bake it — so `worker/corpus.js` still held the pre-change text while
the repository held the new. Both .NET suites were green throughout, because neither reads that
file; `./scripts/test-worker.sh` failed on *"the bake is byte-for-byte the JSON on disk"*, which is
the guard existing for exactly this. Left alone it would have shipped a `/rules` search serving the
old flat paragraphs from the server while every local check said the breaks were there. **Re-bake
after regenerating the corpus, and run all three suites — the trap is that the two loudest ones
cannot see it.**

On that, the 84 lowercase starts were classified rather than accepted: they fall in **45** sections,
of which **42 are Chapter 8 stat blocks carrying exactly one each** — the small-capitals case, and
the break itself is *correct* (Talon's, on p.144, separates his quote from `orIGIn:` precisely where
the page does). The remaining three sections hold 37 of the 84 between them and are all
multi-column tables — `SAMPLE THRESHOLDS` (23), `COMBAT STUNTS` (8), `SOURCES` (6) — each already
scrambled in the previous corpus, which was confirmed by reading the old text rather than assumed.
`DISTINCTIONS` on p.177 is the clearest case: every one of its fragments was already a bare sentence
tail before this change touched it. Regenerating from the real PDF afterwards reproduced all ten
committed chapters **blob-identical**, and the corpus guard was watched to fail end to end — the
joiner disabled and the corpus regenerated, giving both *"LUCK's description is still one flat run
of 1343 characters"* and, from the positive control beside it, *"only 0 paragraph breaks were found
across Chapter 2's Power entries to check"*.

### 2. What the sheet still cannot say

Found by an adversarial audit during the sheet-polish slice; real, and out of scope for it.

**A printed page in the middle of a sheet is anonymous.** Much less pressing now the sheet is one page for an ordinary character, but a Powers-heavy one still runs over. The name is on page one and in a colophon on the last; every page between them relies on the browser's own print header, which the user can switch off — and unticking it is exactly what the review step now tells them to do, because that header is also where the web address comes from. CSS has no portable answer: `position: fixed` renders once at the top of page two in Chrome, and Chrome supports neither `@page` margin boxes nor `counter(page)`. The only mechanism that genuinely repeats per page is a table `<thead>`, which would mean rebuilding the sheet as one table.

**The second half of this item is closed: a finding on the GM review step now names the step that
caused it.** `web/Services/FindingRoute.cs` is a sibling of `SheetFindings` under the same rule — it
reads `SubjectKind`, `SubjectId` and `OwnerId` and computes nothing. A Power's finding carries the
Power, so `Commands.RequestPower` (which the command palette already had) opens that Power's editor
on arrival rather than landing the reader on a list of what they own.

- **The budget deliberately gets no link.** Every purchase contributes to `HP_BUDGET_EXCEEDED`, so
  naming one step would name one of several answers as though it were the answer — the same
  reasoning that already keeps it off the rows, and the running total is on screen from every step
  anyway. `TheBudgetFindingGetsNoLinkAtAll` pins it.
- **Two findings were left bare by the first version, and a screenshot is what found them.**
  `FLAW_MIN_NOT_MET` and `UNKNOWN_PACKAGE` file at `Character` because what they are about is a
  count or a choice rather than a row, so they sat with no link among findings that had one — which
  reads as the feature half-working. **No markup assertion here would have caught it**: "every
  finding that has a route draws one" is true of a router that routes too little. The same
  screenshot showed the test fixture naming a package that does not exist (`superhero`, not
  `superhero_package`), which the page reported and six passing tests did not, because each looks
  for one code.
- **The link is `--ink`, not the `--heading` every other link on the site uses.** `.issues li` are
  the only tinted grounds in the app and `--heading` on `--accent-soft` measures 4.08:1 — under the
  floor, and already recorded twice here as the mistake made on hover states. `--ink` is the ink
  already carrying the message. **Both grounds are now in `EveryScreenPairInUseHoldsItsContrastFloor`
  and were not before**, so the validator's messages had been sitting on them unmeasured; they come
  out at 11.2:1 to 14.4:1 across the four palettes.
- **Six mutations, each watched to fail**: routing an Ability to the wrong section, dropping the
  Power id, routing the budget "somewhere plausible", turning the link into a button, dropping the
  section request from the click handler, and making the step peek rather than take.
- The findings list is deliberately **not** in the pixel manifest: its content is whatever the
  fixture happens to break, and a page whose content depends on the fixture is the trap
  `docs/guide/testing.md` records three proof pages falling into. `proof-review-findings.html` is
  generated to be looked at; `FindingRouteTests` is what holds it.

**The printed-page half above stays open** — CSS still has no portable answer for a running header.

**The 0d half of this item turned out to be a rules gap rather than a UI wrinkle, and is closed.** It was recorded as "a fresh sheet starts every Ability at 0d although the editor's floor is 1d without anything objecting". The floor was right and nearly everything else was wrong: Ch.2 states, once for Abilities (p.17) and again for Talents (p.18), that **no rank can be lower than 1d** and that ordinary people have 2d in every one — so a character has all eighteen Traits, 0d is not a low rank but a Trait nobody can be without, and the Talents editor's floor of 0d contradicted the book outright.

It was enforced nowhere, and it costs Hero Points: without a package a character pays for all eighteen at 1d, which is 18 HP before anything interesting. **The packages corroborate it** — the Civilian Package is 35 HP for 2d in all eighteen, which is 36 points of ranks, exactly the "small discount" the rulebook calls a package. All twenty published Heroes take a package, so every one of their Traits sits at or above its floor, which is why rebuilding them never caught this.

Now `TRAIT_BELOW_MINIMUM`, with `TRAIT_BELOW_PACKAGE` beside it for the other floor — a package's granted ranks cannot be lowered, which is the rule that proved Airmid's attribution impossible and was a test over the published Heroes before it was a check here. Both samples had to gain their missing Talents; both were illegal characters shipped as examples.

### 3. Remaining rulebook chapters — mostly not this tool's business **while it was only a character generator**

> **Read [item 11](#11-answered-it-is-a-tool-for-running-and-playing-pp) first, which has moved
> the ground under this entry.** Every "No — play" in the table below is an answer to the question
> *does a character generator need this?*, and the owner has since said the tool is for running
> **and** playing P&P. That does not make the table wrong — it is still a correct account of what
> character *creation* needs, and it is still why nobody should extract Ch.3–5 to finish the
> wizard. It does mean the column heading is now the narrower of the two questions this project
> asks, and that chapters 3, 4 and 5 are candidates for extraction on their own merits the moment
> anything under item 11 needs them. Item 11's combat simulator says so in as many words.

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

### 4. `search_powers` had no vocabulary for the effects players actually describe — **the 33/33 slice is closed; the benchmark it closed against is widened**

The MCP server's Power search is a word match, and when several Powers match the same words it
used to put them in name order under a caution calling them "the closest entries". **"Walks
through walls" was the case that reproduced it**: it returned twenty-two, of which twenty tied on
a single filler word, so which of them a caller saw was alphabetical, and Phasing sat eleventh.
"He shoots fire from his hands" was the same weakness the other way round: Blast was never
returned at all, because its description says "a damaging ranged attack" and names no element.

**Weighting each word by how much of the rulebook uses it was implemented and reverted early on**,
and that was the finding rather than the fix. It sorted "walks through walls" correctly and broke
"reads minds", which dropped Telepathy out of the first three because four Powers carry "mind" in
their names. Two examples are not evidence; a half-tuned scorer is worse than a dull one, because
it is wrong in places nobody has looked at rather than in the place they tested.

**A labelled set closed the loop on judging any of this**, and its own history is worth keeping
straight because this file drifted out of step with it once already. `PowerSearchExpectations.cs`
holds 33 sentences a player might actually say, each written by opening `data/rules/powers.json`,
reading a Power's own printed `description`, and writing the sentence — never by running the
search first and recording what came back. The two exceptions are quoted directly from this entry
rather than discovered by searching: "walks through walls" (Phasing) and "he shoots fire from his
hands" (Blast). `PowerSearchEvaluationTests.cs` is two tests over that set: `ReportTheCurrentScore`
is a measurement that never fails and prints a table; `TheScoreNeverGetsWorse` is the ratchet.
Widening the stopword list (dropping "through", "than", "anyone" and other connective words that
carry no information about a Power) raised the baseline from 24 of 33 to 25 — recorded further
down this file, under "`search_powers`: 24 of 33 to 25 of 33" — but that entry's own closing line
was already the honest read: **the two cases this item names by hand are not reachable by any
word-matching change at all**, because neither word is in the matching Power's own printed
description. This item's own text above still said "24 of the 33" after that slice landed, which
is the drift: read the test file's `Baseline` constant, not this paragraph, if the two ever
disagree again.

**Closed by giving Powers a searchable vocabulary as data, not by touching the scorer.** The
mechanism already existed on the browser side — `OptionRow` reads `Keywords`, "words it can be
found by but does not print" (see CLAUDE.md's `OptionList` section) — and it is the same `tags`
field `data/rules/powers.json` already carries for all 141 Powers, which `CharacterTools.Score`
already weighted at 6 points, between a name/id match and a category match. So the fix reuses
that field rather than inventing a second one: Phasing's `tags` gained `walls`/`walk` (its entry
says "pass through solid matter", never "walls"), Blast's gained `fire`/`shoot`/`shooting` (its
entry is "name the type of damage it inflicts when you buy it" — naming the element *is* the
Power, so those words are drawn from its own printed invitation), and 65 more Powers gained one to
five words each, chosen the same way: reading that Power's own description and asking what a
player would call the effect, with the labelled set closed rather than consulted query by query.
`search_powers` and `Mentions`/`Score` in `mcp/CharacterTools.cs` are byte-for-byte unchanged.

**`search_powers` now meets all 33 of 33 labelled expectations**, up from 25. `Baseline` in
`PowerSearchEvaluationTests.cs` is raised to 33 to match. Three additions collided with existing,
deliberately-worded regression tests and were dropped rather than kept: `fly` on Flight and `fast`
on Super Speed each turned a description-only match into a tag match for the exact fixed queries
`ADescriptionOnlyMatchIsNotReportedAsTheRulebookHavingNothing`/`SearchSaysWhenNothingMatchedByNameAtAll`
use to illustrate that flag ("he can fly", "heals fast"), and `read` on Telepathy broke
`ATightLimitDoesNotChangeWhatTheSearchFound`'s premise that a specific long query's top row is a
description-only match. None of the three was load-bearing for the 33/33 score — Super Speed's
`faster` and Telepathy's `mind` alone were enough — so they came out rather than the older tests
being rewritten to match a coincidence.

**`PhasingAndBlastAreFoundByTheirOwnVocabularyNotByTheScorer`** holds the two named cases to their
own bar directly, so a future change to unrelated vocabulary cannot let either slip back out of
range while the aggregate ratchet stays green on some other query's improvement. Broken and
watched to fail twice, once per Power: removing Phasing's `walls`/`walk` tags failed with
`Assert.Contains() Failure: Item not found in collection` / `Not found: "phasing"`; removing
Blast's `fire`/`shoot`/`shooting` tags failed the same way for `"blast"`. Both restored
immediately via `git stash` (per the stash-first discipline above) rather than a bare
`git checkout --`; `git diff` was empty before either commit.

**What this closes and what it does not.** The 33 labelled cases now all pass, and the two
PROGRESS.md named by hand specifically are pinned against regression. What is *not* claimed: the
underlying scorer still ties description-only matches at a flat 2 points regardless of how
distinctive the word is, so a query outside this set, worded around a Power with no vocabulary
written for it yet, can still land behind a wall of coincidences the way "he moves faster than
anyone can follow" used to. This slice made the haystack bigger where the 33 examples showed it
was missing words a player would actually reach for; it did not make the needle-finding smarter.
Vocabulary was added to 67 of the 141 Powers (the two named cases, the other seven the table
above's predecessor found failing, and a further pass across categories); the other 74 carry only
their original category tags. **`worker/search.js` needed no change and got none**: it is the
`/rules` full-text search over `data/rulebook/`'s book prose, a different corpus and a different
tool from `search_powers`'s 141 structured Power entries, and `tags` is not a field that corpus
has.

**A benchmark sitting at 33 of 33 stops measuring anything but its own absence of a regression**,
and that is what this closed slice had become: `TheScoreNeverGetsWorse` could only ever hold or
fail, never show an improvement, because there was nothing left in the set for one to show up
against. That is a property of a saturated benchmark, not evidence the search got better than the
paragraph above already claims — the underlying scorer is unchanged and the previous paragraph's
"what this does not close" is still exactly true. **The set is widened rather than the search
re-tuned**, for the same reason the original 33 exists at all: judging a scoring change needs a
set the change can be judged against, and one that already reads 100% cannot do that job.

39 more entries were added the same way as the first 33 — read a Power's own printed
`description` in `data/rules/powers.json`, write the sentence a player would say, never run
`search_powers` first — biased toward the 74 Powers left with only their original category tags
above, toward an effect landing on someone other than the caster (Blind, Cloud Minds, Emotion
Control, Life Drain, Power Absorption, Possession, Dazzle, Aura, Polymorph and others), and toward
four sentences with no Power behind them at all, on the baker's-sentence model. Neither `Score`
nor `Mentions` changed, and no Power's `tags` changed, for this slice.

**The honest score against the widened set is 60 of 72**, and `Baseline` in
`PowerSearchEvaluationTests.cs` is raised from 33 to 60 to match — a number below 100% on purpose,
since a benchmark that cannot fail is the problem this slice exists to fix. All twelve misses are
real gaps rather than scoring accidents: three of the four "should find nothing" sentences find a
weak coincidental hit instead (an office report's "numbers" lands on Languages' "a number of extra
languages" — ordinary English colliding with rulebook vocabulary, not a bug to chase); `cloud_minds`
and `buff` do not appear anywhere in a 25-row window for their sentences, confirming their Powers
still lack the "forget" / "rally the team" vocabulary this item already flagged as open;
`super_senses_lie_detection` misses its bar even though "lying" is the literal word in its own
printed description, because a description-only match is worth a flat 2 points regardless of how
distinctive the word is — the exact limitation the paragraph above never claimed to have fixed;
four more (`elemental_control`, `power_absorption`, `psi_screen`, `form_gaseous`) land just outside
their window; `gestalt` misses by a wide margin, kept in as a deliberately hard, obscure case
rather than dropped for being hard. None of the 39 new sentences were edited after this number was
measured — every miss above is a real gap, not a sentence that could as honestly have named a
different Power.

### 10. Driving the assembled app — **stage one is built; stage two is only a decision about effort**

**Asked for by the owner after a slice where two real defects were found by screenshotting and none
by the suites.** Both were in the same class: they existed only once markup, stylesheet and layout
were put together on a page. The question was what would have caught them without a person looking.

**Stage one is built.** `./scripts/e2e.sh` publishes the site, serves it with the `wrangler pages
dev` version `.github/workflows/deploy.yml` pins, and drives real Chrome — six checks, anonymous
only, no credential and no bypass: the app boots; a character is built by clicking and typing and
survives a reload; a chosen theme survives a reload and is stamped before the app boots; all four
palettes are reached through the two switches in the settings menu and resolve to four different
sets of colours; nine addresses are served at their own paths, with client-side routing proved by a
token that survives the click; and axe-core finds no accessibility violation across four palettes
and four addresses. Each check states a positive control before its outcome, and each has a
deliberately-broken twin of the whole published site that it is required to go red against. It runs
on every pull request.

**There are two drivers and the sixth check is why.** `scripts/e2e/drive.mjs` is the original, a
hand-rolled DevTools Protocol client; `tests/e2e` is a C# one over `Microsoft.Playwright`, which
runs the same five plus `A11Y` — axe-core inside the page, which the hand-rolled client cannot do.
`e2e.sh --driver node|dotnet` picks one and owns everything around a drive either way. The
Playwright driver adds **+4 seconds** to the runner's Restore step: `Channel = "chrome"` launches
the Chrome already on the machine, so there is no `playwright install`, nothing to cache, and no
third renderer to invalidate the pixel goldens against.

**What it does cost is a second drive, and the Build job is now 18–19 minutes against a 30-minute
cap** — 533s before this, then 1103s and 1132s on two consecutive green runs, steady to within one
percent per step. Measured, not projected: a projection from local timings said 13–14 minutes and
was wrong. **If that becomes tight, drop one driver from `build.yml`** — cheapest, reversible, and
the file stays. Scanning fewer palettes in A11Y is the second lever and costs real coverage.
Raising `timeout-minutes` is not a lever; see `docs/guide/hosting.md`.

**How it works, and every limit of it, is in [`docs/guide/testing.md`](docs/guide/testing.md)** —
read that before changing it. The account of building it, including four faults the harness found
in itself, is in [the archive](docs/progress/).

**What that closes and what it leaves open**, against the table this entry was originally built
around:

| Was not exercised by anything | Now |
|---|---|
| Blazor WebAssembly actually booting | **Closed.** The framework starting, its payload arriving with bytes in it, and the boot screen being replaced by a rendered page are three separate assertions, and the real `_headers` is in force so a Content-Security-Policy that refuses one of the app's own scripts is a red check |
| Every `js/*.js` interop | **Closed for `ppStore` and `theme.js`.** The character-storage path is driven by clicking, and `ppThemeStats.stamps` is read across a genuine reload rather than a re-executed module. `motion.js` and `palette.js` are still only a proof harness and a bUnit recorder |
| Client-side routing | **Closed.** A `NavLink` is clicked, and a per-document token surviving the click is what proves the router handled it rather than the browser reloading |
| Pages Functions against the real edge | **Open, and now deliberately so rather than by omission.** Nothing is bundled: wrangler takes `functions/` from the working directory, and the harness runs from one without it, so `/api/` falls through `_redirects` and the app reads an unparseable answer as anonymous — which is its own documented behaviour. Binding a local D1 is stage two's first step |
| **Anything behind sign-in** | **Open — stage two, below.** `/rules`, `/admin`, the portfolio, the replay and every account-storage path |
| Accessibility of the *rendered* app | **Closed for what a machine can see, and it found one thing.** axe's full default ruleset, four palettes by four addresses: 536 passing rule instances and a single violation — the wizard's disabled Next control, exempt under WCAG 1.4.3 and dropped by name with the figures written down (see the judgement call below). `theme.css`'s own contrast claims hold. **Screen readers stay owed** |

**Three gaps stage one does not close and did not claim to**, each named here so nobody reads the
five green checks as more than they are:

- **A `_redirects` regression is invisible locally.** `wrangler pages dev` *rejects* this site's
  own `/* /index.html 200` rule as an infinite loop and ignores it, then serves `index.html` for
  unmatched paths by its own default. So deep links work in the harness for a different reason
  than they work in production. Measured rather than assumed — wrangler names the rule in its
  startup output as the one invalid rule it found.
- **`motion.js` and `palette.js`.** The reduced-motion behaviour and the Ctrl-K chord are both
  behavioural and both still only asserted by a `file://` proof page.
- **Screen readers**, which stay owed and which no harness closes: it needs a person with a screen
  reader, and asserting `aria-pressed` is the string `"true"` is not the same as having been
  listened to. **This is a decision, not a backlog gap** — *"I don't care about accessibility /
  screen reader stuff. So defer until the application is finished. Which it is far from."* — the
  owner, 2026-08-27. Do not spend a slice on it and do not offer it as the next thing to do. It
  stays recorded because the debt is real, and it is owed on eight surfaces: the command palette,
  the pips, the sign-in page, the light/dark control, the row descriptions, the rules search, the
  row findings and the undo announcement.

  **The structural half is done and does not need revisiting.** `AriaReferenceTests` sweeps every
  rendered surface and resolves every `aria-describedby`, `aria-labelledby` and `aria-controls`
  token, so a dangling IDREF cannot reach whoever eventually does the real work — it carries a
  count as its own positive control, since every assertion in it is an absence and a sweep that
  rendered nothing would satisfy all of them. It cannot hear an announcement, and it is not
  progress against the eight surfaces above. Keep writing components to the rules in
  [`docs/guide/browser.md`](docs/guide/browser.md) — the `title`-attribute ban, string-valued ARIA
  booleans, conditional `aria-controls` — because those are cheap at the time and expensive to
  retrofit.

**A fourth gap, found while fixing a leak rather than by design: `kill_tree` itself is unproven.**
`stop_server`'s Linux arm used `pkill -P`, which kills direct children only — wrangler's tree is
`npx` → node → `workerd`, so `workerd` outlived its step still holding a port. The fix walks
`/proc/<pid>/stat` recursively. The identical defect then reappeared on macOS, which has no
`/proc`, so `children_of` returned nothing there too, silently, and `kill_tree` again killed only
the `npx` wrapper — measured at six `wrangler`/`workerd` groups still listening on 8788–8793 after
a completed run, with the run still reporting PASS. Both platforms are now fixed (macOS falls back
to `pgrep -P`), and **both were verified only by outcome** — zero leaked processes and no held
ports after a full run — **never by driving `kill_tree` directly**. `port_in_use` alone masks the
leak by stepping over the held port, so a green run cannot distinguish "nothing leaked" from
"something leaked and nothing looked". What is missing: start a server, call `stop_server`, and
assert directly that nothing is listening and no `workerd` process remains, on both the Linux path
(in a container, since the obvious `bash -c '…' &` fixture collapses to one process — `exec`
replaces it rather than forking a real multi-process tree to kill) and the macOS path. See
[`docs/progress/2026-09-04-the-stage-two-brief.md`](docs/progress/2026-09-04-the-stage-two-brief.md)
item 3 for the container recipe already worked out.

#### One thing the accessibility check found that is a decision, not a defect

**The wizard's Next control, before a tier is chosen, is faint in every palette**: measured in real
Chrome against the published site on 2026-09-04, `.disabled` on `/build` resolves to **2.23:1
(Hero/Light), 3.28:1 (Hero/Dark), 2.54:1 (Villain/Light), 3.22:1 (Villain/Dark)** against a 4.5:1
floor. `StepButtons.razor` renders it as an anchor with `aria-disabled="true"` and `app.css` paints
it at `opacity: 0.45`.

**It is not a conformance failure.** WCAG 1.4.3 exempts text that is part of an inactive user
interface component, and this control is inactive — `aria-disabled="true"`, `pointer-events: none`.
axe reports it only because it recognises the `disabled` *attribute*, which an anchor cannot carry.
The A11Y check therefore drops those nodes by name, counts them, and prints the count.

**It might still be worth changing, and that is the owner's call rather than a harness's.** A
disabled Next is exactly the thing a reader looks at to work out why they cannot go on, and 2.23:1
is faint enough that some readers will not. Raising `opacity` on `.btn.disabled` — or giving the
step a sentence saying what is missing, which the Campaigns page already does for a disabled Join —
would answer it. Nothing is broken until somebody decides that; the figures are here so the
decision is made against numbers rather than a glance.

#### What has to be true before `scripts/e2e/` is deleted

**Not yet, and this is the condition rather than a feeling.** The hand-rolled driver is green,
twinned, and the one with a track record; the Playwright one is a week old. A migration that
removes the working harness before the replacement has a record is how an upgrade becomes a
regression, so both run in `build.yml` and the deletion is a separate change nobody has made.

**The condition: twenty consecutive green `Build` runs on `main` in which the `--driver dotnet`
step reported six checks green against the real site and all six twins red.** Green is enough
because both drivers run in the same job — either going red fails it — so twenty green runs is
also twenty runs in which the two did not disagree. Count them with

```bash
gh run list --repo SoftwareSamurai-net/ProwlersAndParagonsAutomation \
  --workflow build.yml --branch main --limit 30 \
  --json conclusion,headSha --jq '.[] | "\(.conclusion) \(.headSha[0:8])"'
```

and read the `E2E: PASS` line out of the Playwright step of the oldest one in the window, so the
count is of runs that actually drove it rather than of runs that skipped it.

**Two things that are not the condition, said because they are the tempting shortcuts.** "The
Playwright one is nicer" is not a reason to delete a working check. And "CI is slow" is a reason to
drop one driver from the job, which is a different and reversible change — the file can stay.

**When it goes**, `scripts/e2e/cdp.mjs` and `scripts/e2e/drive.mjs` go together,
`scripts/e2e/defects.mjs` stays (both drivers share it), `e2e.sh`'s `--driver` flag becomes
unnecessary, and `E2eDriverTests`' cross-driver assertions need rewriting rather than deleting —
their own messages say so.

#### The argument this item was sharpened by, which is why stage one was worth more than it claimed

**A feature was built, tested, adversarially reviewed by two independent agents and shipped, while
nothing in the application ever wrote to the store it read from.** The manager's list, the banner's
switcher, `DiscardedCharacter` and both undo buffers all read `SavedCharacters`'s index; nothing
ever added a character to it. See [the archive](docs/progress/).

**That is not the defect class this item was written about, and the difference matters.** Everything
above argues about *assembly* — markup plus stylesheet plus layout, interop, routing, the real
edge. This one was none of those. Every unit and component test passed, and passed honestly,
because **every one of them called the store directly.** A test that reaches the machinery by hand
cannot notice that nothing else reaches it. **Nothing in this repository asked whether a feature is
reachable by an ordinary person doing an ordinary thing**, and a driver is the only kind of check
that asks that question by construction, because it has no other way in.

That is why neither driver may reach past the browser — no `localStorage.setItem` to
arrange a state, no calling into a component, and a real mouse event at real coordinates rather
than `el.click()` from inside the page. A harness that sets up its own world stops answering the
question it exists for.

#### And an honest limit, which is an argument against reading this as a substitute

**Three further defects in the slice that sharpened this were found by an adversarial review, and a
harness would have caught at most one of them.** They were: a store reporting a successful write
over a browser that had refused storage; a cap read before the write it was meant to gate, racing a
fire-and-forget autosave; and an undo left armed that would have duplicated the character it was
offered to rescue.

A driver reproduces the third if somebody thinks to press Undo after starting another character. It
will not reproduce the first without a browser configured to refuse storage, and it will not
reproduce the second at all reliably — a race that depends on a network round trip losing to a
button press is not something a harness *drives*, it is something a harness gets lucky about. All
three were found by a reader who was told to look for data loss and given nothing else.

**So the two are not substitutes and should not be argued for as one.** A harness answers "is this
reachable"; a hostile reader answers "what does this do when something goes wrong". That slice
needed both and neither would have been enough.

#### Stage two: seed the database, do not open a seam

**Stage two as originally written was the wrong solution to a solved problem, and the whole of its
danger was self-inflicted.** The plan assumed the only way to reach a signed-in session locally was
a seam *in the application* that mints one — an authentication bypass, guarding the rulebook that
is here by the author's personal permission, the recordings, the admin page and other people's
characters. **The door did not have to be built.**

Read off the code rather than reasoned about: `worker/tokens.js` stores **only the SHA-256 of a
sign-in token** (`worker/crypto.js`'s `hash`, plain WebCrypto, which Node provides identically),
the raw token travels by email, and `db.spendLoginToken` verifies by hash lookup and burns the row.
So a harness can do what an email does, from outside the application:

1. generate a token in Node and hash it — the same function, no shared code needed;
2. insert the row into the **local** D1 —
   `wrangler d1 execute <db> --local --command "INSERT INTO login_tokens (token_hash, email, expires_at) VALUES (…)"`;
3. drive Chrome to `/signin?token=<raw token>`.

The application then runs **its real verify path** — hash lookup, expiry test, single-use burn,
session cookie issued. Nothing is bypassed and nothing is faked but a row, which is what an email
would have caused. This is ordinary test-seeding, and it is strictly better than the seam: no code
in the shipped bundle, no secret, no localhost test, nothing to compile out, and no test needed to
assert the published bundle does not contain it.

**What it needs**, so nobody discovers it late: `functions/` has to be bundled, which means running
`wrangler pages dev` from the repository root and giving it a D1 binding — the one thing stage one
deliberately does not do, so this is a change to `scripts/e2e.sh`'s server setup and not only new
checks. A local D1 that exists and is migrated is `scripts/apply-migrations.sh`'s job already.
**What it still cannot reach** is the mail send itself, which is `scripts/probe-mail.mjs`'s job and
is not a browser's, and the deployed site, which cannot be seeded by anybody and should not be.

**And the zero-risk alternative stops being a fallback and becomes a second target.** Pointing the
same driver at the *deployed* site verifies the real edge, the real Functions and the real bundle
anonymously — so the local run seeds and signs in, and a deployed run proves production. Neither
needs an approval about danger any more; both are a decision about effort.

**The brief written to hand this to an agent is
[`docs/progress/2026-09-04-the-stage-two-brief.md`](docs/progress/2026-09-04-the-stage-two-brief.md).**
It is a prompt rather than an account — the one file in that directory that is not finished work,
and it says so in its own first paragraph. It carries the five pieces above as instructions, the
measured job cost as a constraint, stage one's three guard faults as worked examples, and six
questions it refuses to answer in advance. **This entry stays the authority**; if the two disagree,
the brief is the older document and the brief is wrong.

### 11. Answered: it is a tool for running *and* playing P&P

**The owner spent a session reading a competitor (PNP Ready) and brought back eight ideas.** This
entry used to open by putting them behind one question — *is this a tool for a player building a
character, or a table aid for a GM running a game?* — on the argument that answering "player"
made most of what follows overhead a reader navigates past.

**The owner has answered, and the answer dissolves the question rather than picking a side:**

> *"Its just simply both. Its a 'Running & Playing P&P Tool'."* — 2026-08-27

**So all eight are in scope, and the build order below stands** — not because a decision selected
them, but because their dependencies were always what ordered them. Nothing changes about *what*
gets built; what changes is that nothing below is blocked any more, and that "is this for the GM?"
stops being a question worth re-asking of each one.

#### What that answer settles, and what it does not

- **It settles the front door.** Item 12's three doors — build a character, run a game, look
  something up — were pitched before this was decided and read as a bet on the answer. They are
  not: three doors is what a tool for both halves has. `docs/guide/browser.md` already records
  that the banner was built anticipating a third room, because a flipping two-room label "fails
  for three".
- **It settles the three loose ends below from being *evidence* into being *work*.** The tier and
  Trait Cap stored per character, `UnlimitedBudget` living on the sheet, and Adversity appearing
  in no code at all were listed here as signs the answer was already "GM". They are now simply
  three things that are wrong for a tool that runs a game, and the first item — a campaign — is
  where the first two belong.
- **It does not license the engine growing a second job.** The combat simulator is still a
  **second engine beside `engine/`**, for the reason given under item 6 below: today's engine is
  the authority on cost and validity and knows nothing about resolving an action. "Both" is a
  statement about the product, not permission to put play rules into the thing that prices a
  character.
- **It does not reverse "an illegal character is reported, never repaired."** A GM-facing tool
  makes that rule more load-bearing, not less: the settled list already says the engine is a judge
  and does not make design decisions about somebody's character, and a table aid that quietly
  fixed a Hero would be doing exactly that on behalf of somebody not in the room.
- **It does not decide sharing.** Item 1 below is explicitly **single-user first, no sharing**,
  because that needs nothing new from the server — a campaign is another opaque blob beside the
  characters. Two people at one table is a separate decision and is not taken here.

#### The three loose ends, which were the evidence and are now the first work

Three things in this repository point the same way and none of them was put there on purpose.
They were written down as evidence for an answer nobody had given; with the answer given, each is
a defect that a campaign is the place to fix:

- **The tier and the Trait Cap are campaign facts stored per character.** `Sheet.SelectedTierId` is
  on the sheet and `TraitCap` derives from it, so five characters in one game can silently disagree
  about the power level and nothing notices. The rulebook puts these on the *game*: `ChooseTier`'s
  own copy says "The tier sets the Hero Point budget and the Trait Cap", and this file already
  records Iconic's 200+ as **GM discretion**, which is a sentence about a table.
- **`UnlimitedBudget` is a campaign setting wearing a character's clothes.** Its own remarks call it
  "a GM building to whatever a scene needs" — a way of working, stored on the sheet. It was split
  out of `IsVillain` for exactly this kind of reason; this is the same split one level up.
- **Adversity has nowhere to live.** The settled list says "Only Heroes have Resolve; the GM gets
  Adversity, spendable on any NPC." The word appears in two Flaw descriptions and **nowhere in any
  code at all** — measured. It is a GM-scoped currency the app models not once.

#### The eight, in the order their dependencies force

1. **A campaign.** A name, a power level, a trait cap, a sandbox flag, and the characters that
   belong to it — which then inherit those settings instead of each carrying their own.
   **Single-user first, no sharing**, because that needs nothing new from the server: a campaign is
   another opaque blob beside the characters.

   **Closed — see [the archive](docs/progress/), which supersedes the paragraph
   above.** The shape the owner settled on is fork and pull request: a campaign holds a *clone* of a
   character and the player's edits arrive as an approval request. So **"single-user first, no
   sharing" is no longer the design**, sharing is in, the server did need something new (a clone
   table, an approval slot version-checked against a stale decision, and a join code), and
   **campaigns are account-only with the local half deleted** — one kept in a single browser could
   never receive a submission. Three screens draw them.

   What follows is the storage half as it shipped a slice earlier, kept because every rule in it
   still holds. `pp.campaign.v1` is the one line that does not: there is no local campaign store any
   more, and nothing was lost by removing it because no screen had ever created one.

   - **The server never learns what a campaign is.** A payload it stores verbatim, a label and a
     `campaignId` the client supplies exactly as it supplies `label`. It does not know what a tier
     is and must not learn; see [`docs/guide/accounts-server.md`](docs/guide/accounts-server.md).
   - **Inherit into an empty field; offer into a full one.** Joining copies the campaign's tier and
     sandbox setting only when the character has no tier. When they disagree, **nothing is written**
     and a mismatch is reported. Repairing would be worse than usual in both directions: raising the
     tier turns an illegal character legal in silence, and lowering it moves Resolve, which is
     `(TraitCap − highestRelevantRank) × 2` — a figure the player paid Hero Points for.
   - **The campaign's Trait Cap is carried and never applied**, deliberately. There is a test that a
     campaign whose cap differs from its tier's leaves `CalculateResolve` returning exactly what it
     returns with no campaign at all. Do not add a `TraitCapOverride` to `CharacterSheet`.
   - **Deleting a campaign leaves its members naming it** — no cascade, no nulled column, no foreign
     key. `UNKNOWN_CAMPAIGN` is reported in the same shape as the engine's `UNKNOWN_TIER`, which
     keeps restoring the campaign a complete undo.
   - **Nothing bumped `StoredCharacter.CurrentVersion`.** It is 1, a mismatch is discarded in
     silence, and an absent `campaignId` reads back as null — which correctly means "in no
     campaign". Bumping it would have emptied every returning visitor's browser and every account.
     `SavedCharacterSummary`'s new fourth parameter has a default for the same reason, and both are
     guarded by tests that read a checked-in literal written before either existed.
   - **`CampaignId` is barred from `engine/` as an *indirection*, not as presentation.** Resolving
     it means asking storage, which is asynchronous, which `IRulesSource` is synchronous to forbid.
     `PresentationFlagsTests` now carries three names and says which bar each is under.
   - **`CampaignId` is deliberately not in `CharacterSession.IsWorthKeeping`.** Adding it would make
     picking a campaign create a listed, empty character the moment it happened — verbatim the bug
     that predicate was added to fix.

   **One thing found while building it, recorded because the design said otherwise.** The reasoning
   for the `routePattern` arm was that without it "a caller inventing ids writes one `error_log` row
   per id". That is not what happens: an unrecognised path already falls to `other`, so the table
   was bounded either way. The arm is still right and still landed — what it buys is a *legible*
   log, where a broken campaign route is distinguishable from a passing crawler — and the test
   asserts both the bound and the route name, the second being the half the arm is actually for.
2. **Headquarters.** *The cheapest of the eight and the strongest argument for the first*, because
   the rulebook made it campaign-scoped in print rather than by inference: the Perk is already in
   `data/rules/perks.json` at 1 HP per unit for 3 Base Points, and its own text reads **"Multiple
   Heroes can apply their Base Points to the same headquarters."** A shared object the app has
   nowhere to put.

   **This entry used to say Chapter 6's base-construction rules were "not extracted", and that was
   wrong in the way that makes a bounded job look like an unbounded one.** They are in
   `data/rulebook/ch06-equipment.json` in full — `BASE FEATURES` on printed page 100, then
   **22 named features** from `ALTERNATE HEADQUARTERS` to `WORKSHOPS` across pp.100–103, counted
   rather than estimated. **No PDF work is needed and none should be started.** What is missing is
   the *structuring* of that prose into `data/rules/`, which is a different and much smaller job:
   the existing 12 custom **gear** features in `data/rules/gear_features.json` are the template, and
   20 of the 22 fit it as it stands.

   **Item 3 of this list made exactly this mistake before**, about chapters 3–9, and the note there
   already says what it cost: *"That was wrong, and it made a finished job look unfinished."* Twice
   now the error has been the same one — reading "not in `data/rules/`" as "not extracted", when
   `data/rulebook/` is a second store holding the book's own text. **They are different claims about
   different files.**

   **The two that do not fit the gear template**, so nobody rediscovers them:

   - **Size** is priced *1 to 3* Base Points. `GearFeatureModel.CostRange`'s own doc comment pins
     itself to "the two features the rulebook prices at 1 to 2 HP", so this is a model change and
     not a data-only addition.
   - **Mobile** costs 0 and carries a cross-Perk dependency — the base's vehicular characteristics
     are bought with the Unique Vehicles Perk, which is itself unmodelled.

   **And the cheapness claim above survives the correction only for the extraction half.** The Perk's
   own printed text pools Base Points across several Heroes, and every engine type — `CharacterSheet`,
   `CostCalculator`, `RulesRepository` — is scoped to exactly one character. Somewhere for a shared
   headquarters to live, be costed and be jointly funded is a separate piece of work that nobody has
   sized, and it is downstream of the campaign rather than beside it.
3. **A dice roller.** P&P is a d6 pool system and the engine already knows every Trait's rank, so a
   roller on the sheet can offer "roll Might 6d" in one click. **That is the only version worth
   building**: a roller that does not know your character is a worse copy of an app everybody
   already has.
4. **A roll log**, per campaign.
5. **Dice analytics.** Successes, sixes, most-rolled Traits. It is fourth in a chain and its value
   is novelty until the three above exist — the competitor's own empty state admits the dependency.
6. **A combat simulator.** **This reverses item 3 of this list**, which says chapters 3–5 are play
   rules and "mostly not this tool's business". It is the pivot from character creator to play aid,
   and it is the honest reason to build 3 and 4. Architecturally it is a **second engine beside
   `engine/`, not an extension of it**: today's engine is the authority on cost and validity and
   knows nothing about resolving an action. Opposed pools, active defence, Resolve and Adversity
   spends, conditions — same `data/rules/`, a different question.
7. **A GM screen.** Whatever a GM wants to hand during play. Last, because it is a presentation of
   everything above.
8. **The information architecture that holds them**, which is the one item that needs none of the
   others and could be done tomorrow — see the next entry.

#### And the architecture was already expecting it

`docs/guide/browser.md` on the banner, written before any of this was discussed:

> Both avenues are offered from everywhere, rather than one link naming whichever half you are not
> in. That flipping label works for two rooms and **fails for three**.

The banner was built anticipating a third room. **That was offered here as a signal the shape was
right, and it now reads as the shape the answer asks for** — but it is worth being honest about
what it is: a note somebody wrote about a two-link nav, not a decision about the product. The
decision is the owner's sentence at the top of this entry.

---

### 12. The interface the owner asked for, which needed none of item 11's answer

**Four moves, none of which waits on a campaign existing.** Recorded together because they are one
rearrangement of the same chrome — and with [item 11](#11-answered-it-is-a-tool-for-running-and-playing-pp)
answered, the first of them is no longer a bet on which way it would go: **three doors is what a
tool for running and playing has.**

- **Three doors on the front door**, vertical: build a character, run a game, look something up.
  `Areas.Of` already reads the first path segment and `EveryAvenueIsOfferedFromEverywhere` already
  pins the offer on four routes, so the third is an addition rather than a rework.
- **The rules search moves into the banner, on `Ctrl`+`K`.** **`Ctrl`/`⌘`+`K` already opens the
  command palette** — `js/palette.js` binds it — so this is an extension of an existing surface, not
  a new one. It has to be argued rather than assumed, because that file *says in as many words to
  resist growing it*: today the palette offers Powers and navigation, and the rulebook is a
  different corpus behind an account gate.

  **The discoverability half is done — see [the archive](docs/progress/).** The
  banner carries a `Search` button with the chord printed beside it, on every route, with the
  modifier chosen at render time from the platform. **What is left in this bullet is the corpus,
  not the surface**: putting the *rulebook* behind that control means growing the palette onto a
  second body of text that is behind an account gate, which is the part `palette.js` says in as
  many words to resist and which still has to be argued rather than assumed.

  **What was decided while closing the first half, so it does not get re-litigated:**

  - It is a **button and not a text box**. A box that looked like a search field while searching
    Powers and step names would be the wrong promise twice over. When the rulebook does move
    behind it, the field is the right shape and the button is what it replaces.
  - The word is **Search**, and the palette still calls itself "Go to" inside. The label has to
    survive a glance in a strip of six controls; "Go to" between two underlined links read as a
    third link with no destination.
  - The modifier is answered by `ppPalette.onAMac` and worded by `Shortcuts.ReadModifier` — `Ctrl`
    or `Cmd`, never the looped-square glyph, which is in neither typeface this app names and would
    fall back to a system face on one platform only.
  - **A placeholder is not a label** still applies to the field when it arrives: it may carry the
    hint, and it may not be the only place the field is named.
- **Account and settings move to the right of the banner. — done**, see the completed entry at the
  top of this file. The bar is two sides with a hairline between them, the tools cluster is one
  idiom rather than three, and the account stopped being a `.banner-link`: an identity was wearing
  navigation's clothes.
- **The Hero/Villain switch moves into that settings menu — done**, with the light/dark switch
  beside it. It completes a decision already taken rather than reversing one: `docs/guide/browser.md`
  records that "Only the builder names the palette in the banner. A rules search is not a Hero or a
  Villain."

  **And it is what fixed the alignment the owner reported in the same breath.** "Search is
  vertically elevated" was measured at 1.25px and was a symptom: five idioms in one strip cannot be
  aligned, only reduced. `align-items: baseline` is the arithmetic half and lands the spread at
  0.00px; taking the two pills off the band is the half that made the row one kind of thing.
  Measured on every CI run by `proof-align.html`, against a twin that reproduces the defect.

  **What is *not* done from this bullet's neighbourhood: the third avenue.** Item 11's answer says
  three doors is what a tool for running and playing has, and `.avenue-nav` is built so the third
  costs one `NavLink` — but there is nothing behind it yet, and a door onto an empty room is worse
  than a wall.

#### Two smaller things from the same reading

- **The Hero Point limit does not need a full-width panel for one button. Done — see the completed
  entry at the top of this file.** Two cards as their own two-option group under a rule, not tiers
  7 and 8, and the flipping label is gone. The argument is kept in full up there.
- **`/rules`' "What is here" panel is inert rather than pointless. Done — see the completed entry at
  the top of this file.** The counts are gone and each row runs a search scoped to that chapter,
  through a `chapter=N` parameter on `/api/rulebook/search`. The argument below is kept because it
  is what the change was built to, and because it is the record of why the two easier routes were
  refused.

  **The scoping half needed a server change, and that is why this bullet was open.**
  `/api/rulebook/search` takes `q` and `limit` and nothing else, and `search()` in `worker/search.js`
  ranks the whole corpus. Neither honest route to a chapter-scoped answer exists on this side of the
  wire: **filtering the response client-side is filtering what survived a server cap** — `MOST_RESULTS`
  is 30 and a caller cannot raise it — so a chapter with real matches outside the top thirty comes
  back empty, and `found` would be a count of the whole book presented as a count of the chapter.
  That is the same "a search that always shows five rows reads as five answers" fault the page was
  designed against. **Searching the chapter's own title instead is worse**, not better: a row
  labelled with a chapter and its pages that answers with hits from three other chapters is exactly
  the "looks like a list of links and is not one" complaint in a new spelling.

  **What it took** was a `chapter=N` parameter on that route, applied **before** ranking, with
  `found` and `nothingMatchedByHeading` computed over the scoped set and *then* the list cut — and
  the last part came free, because `search()` already takes the chapters it ranks as its first
  argument and already computes both before cutting. So scoping is filtering that array, nothing in
  the ranking changed, and the worker's surface grew by one optional parameter.

---

### 13. The owner's branding, and the sign-in email

**`superheroes.softwaresamurai.net` is the owner's domain and the app carries none of their
identity.** A kit exists — an SVG logo and two favicon packs, HTML5 and ASP.NET — outside this
repository.

**And the sign-in email is the weakest thing the project sends.** The owner compared it against a
competitor's and the competitor's is better. It is built by `signInMessage` in `worker/mail.js`,
which `scripts/probe-mail.mjs` imports rather than reassembling — see item 8's account of why that
matters. Any change here is **outward-facing and costs the hourly allowance to test**, so it is
proofed against the probe and not against a real inbox.


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

**Recorded here rather than in a session handover note, which does not outlive the session.** Three agents that knew nothing about the work were asked, for every
guard test, to name a plausible bug it claims to cover but would not catch, **and to demonstrate
it by mutation rather than argue it**. They ran 64 mutations and **38 survived**. Five were in the
rulebook corpus and were fixed at the time; the remaining 33 were grouped into three slices.

**All three are closed** — A1 (the MCP server's twelve), A2 (browser and replay, thirteen) and A3
(engine and validator, eight), one completed entry each below. **They were worked concurrently on
three branches and reconciled afterwards**, which is why each entry quotes a test count measured
against its own branch rather than against this tree; the reconciled figure is the one in the
table at the top of this file. The merge touched only this file, `CLAUDE.md` and
`docs/HANDOVER.md` (since deleted) — no test and no source file was resolved by hand.

None of the 33 was a bug in the product. Every one was a **test that did not hold what it claimed
to hold**, which is a different and quieter problem: the suite's headline number goes up and its
grip does not.

### 9. Durable telemetry needs leaving Pages — **researched, costed, and deliberately deferred**

**The owner asked why the Cloudflare observability view is empty. It is empty because this is a
Pages project, and that is structural rather than a configuration gap.** Cloudflare's own
Pages-to-Workers compatibility matrix marks every durable observability feature ❌ for Pages —
**Workers Logs, Logpush, Tail Workers and Source Maps** — and passes only *real-time logs*, the
ephemeral tail. `observability` is not an inheritable Pages configuration key, so there is nothing
to switch on and **no plan that unlocks it**. Do not spend a cycle looking for the setting.

**Decision: deferred.** The owner's words — *"error log in /admin is fine for now… can upgrade
telemetry if we need to."* The durable half already exists and is now readable: the D1 `error_log`
table, surfaced at `/admin` behind the same gate as the invitation list. That is the thing that was
actually wanted; the dashboard was the assumed route to it.

**What Pages does have**, and it is a live-debugging tool rather than a record:

```bash
npx wrangler pages deployment tail --project-name=prowlers-and-paragons --environment production
```

Useful filters: `--status ok|error|canceled`, `--search <text>` (matches inside `console.log`
output, so one of the four categories `worker/errors.js` writes is a usable search), `--method`,
`--ip self`. **It streams only while the command runs**, nothing is stored, and high volume pushes
it into sampling and drops messages silently. For a fault discovered a week later — which is
exactly the shape of the mail-key outage this project already had — it tells you nothing, because
nobody was tailing at the moment it happened. That asymmetry is the whole reason the D1 table
exists.

**If it is ever wanted, the answer is migrating to Workers with static assets, and it is $0.**
Workers Logs is on the free plan: 200,000 events a day, 3-day retention — orders of magnitude
beyond this site's traffic. The migration is bounded and mechanical, and **both constraints this
repository would raise against it resolve favourably, for reasons worth having written down**:

- **The third-party-cookie objection does not apply.** `CLAUDE.md` says Pages Functions was chosen
  over "a Worker on `workers.dev`" because a cookie set by another host is partitioned away. That
  is correct, and it is an objection to **`workers.dev`** — a shared Cloudflare-owned domain — not
  to Workers. Workers take Custom Domains exactly as Pages does: Cloudflare routes
  `superheroes.softwaresamurai.net` straight to the Worker. Same-origin holds, on the condition
  already true today: keep the custom domain and never point the accounts API at `*.workers.dev`.
- **The `_redirects` SPA fallback survives byte for byte.** `web/wwwroot/_redirects` is one line,
  `/* /index.html 200`; the Workers equivalent is configuration rather than a file —
  `assets.not_found_handling: "single-page-application"` — and returns the identical
  200-with-`index.html`.

**And the migration has one trap that would break the site quietly, so it is recorded here rather
than rediscovered.** Workers-with-assets checks static files **first** and only reaches the script
for paths that match none. `/api/*` is not a static file, so in the naive setup **the SPA fallback
swallows the whole accounts API** — every address answering `index.html` with a 200. That is
precisely the failure the deploy workflow's "confirm the accounts API answers" step exists to
catch, and precisely why that step reads the *body* rather than believing the status. The fix is a
routing directive telling the Worker to run first for that prefix; get it wrong and the site looks
perfectly healthy while signing nobody in.

Also settled while looking: **OpenTelemetry export is viable but is the option this repository has
already rejected on principle** — `CLAUDE.md` declines third-party error services on the grounds
that nothing about who somebody is should leave the Cloudflare account this site deploys to, and
that property is worth more than a nicer dashboard. That reasoning is unchanged by anything above.

### 7. The pre-1.0 audit — **closed. Dead code and hot paths measured clean; the token side is costed but not implemented**

The adversarial half has run and been acted on: 126 mutations, 48 survivors, eleven streams. See
the completed entry, and `docs/notes/` for the mutation tables.

**The first of the two remaining bullets — "is it snapshotable to a fresh AI agent?" — is closed
by the split recorded in the entry in [the archive](docs/progress/).** `CLAUDE.md` is 290 lines and indexes ten
files under `docs/guide/`. The second bullet — "is the codebase as optimised as it should be?" —
is now audited too. Full writeup in the entry in [the archive](docs/progress/); the short version:

- **Dead code: two exports removed, nothing else found.** `worker/db.js`'s `userByEmail` and
  `worker/search.js`'s `corpusIndex` were `export`ed with no caller outside their own file — both
  now private. Everything else checked came back clean: elevating `IDE0051`/`IDE0052`/`IDE0060`/
  `CA1801`/`CA1812`/`CA1852` to warnings for a scratch rebuild found nothing beyond the expected
  `CA1812` false positives on reflection-deserialized test-transcription types; all 180 CSS class
  selectors in `app.css`, all five `web/wwwroot/js/*.js` functions and all 35 `web/Components/*`
  component tags trace to a real caller (`.boot`/`.boot-title`/`.boot-sub` live in
  `index.html`, not a `.razor` file — the first pass over just `*.razor`/`*.cs` missed them, which
  is itself worth recording: a "which files reference this" sweep in this repository has to
  include `wwwroot/index.html`); a spot-check of engine/sheets public types (`GearFormatter`,
  `RulebookStatLine`/`RulebookOption`, `ValidationSubject`, the `CreationRulesModel` nested
  records) traced every one to a real caller. **Proof**: `./scripts/test-worker.sh` — 166 passed,
  0 failed — after both removals; the two .NET suites are untouched by the change (JS-only) and
  still print the baseline counts below.
- **Hot paths: measured, no change landed.** `RulesRepository`'s lookups are already
  lazily-cached dictionaries built once; `CostCalculator` and `DerivedStatsCalculator` have no
  rebuilt-per-call dictionary or repeated full-collection scan on the paths that run per
  character. A `Stopwatch` over the 20 published Heroes, 2000 iterations each (40,000 calls per
  measurement, warmed up first): `TotalCost` **13.2 µs/call**, `CalculateEdge` +
  `CalculateHealth` + `CalculateResolve` combined **2.5 µs/call**, `CharacterValidator.Validate`
  **25.0 µs/call**. All three are already far below anything a keystroke-driven browser UI or a
  4,000-test suite would notice, so no optimisation was landed — the task's own rule is not to
  land one that cannot be shown faster, and there was nothing here worth the clarity this codebase
  spends on purpose to buy.
- **Payload: nothing found.** The one asset that looked like a candidate — `PublicSans-Italic-
  Variable.ttf` — is reached by `.power-entry.trait-sources` and `.sheet .quote`, both
  `font-style: italic`. No orphaned font, no duplicated data staged into `wwwroot` beyond what
  item 5 already documents and rules out of scope.
- **The token side is costed, not implemented — the owner's call, per the task that ran this
  audit.** `PROGRESS.md` is 5,069 lines / 446,711 characters / 71,769 words — roughly **90–110K
  tokens** to read in full, against **~15K tokens** for `Current state` + `Remaining work` +
  `How to maintain this` alone (60,829 of those characters). `Completed work` is the other
  ~89% of the file: 4,503 lines across 68 entries, prepended newest-first so far — the newest
  entry sits immediately after `Remaining work`. Proposed shape, not built: keep this file's
  `Current state`, `Remaining work` and `How to maintain this` as they are; move `Completed work`
  verbatim, same newest-first order, into a second file (e.g. `docs/PROGRESS-COMPLETED.md`);
  leave a short index in its place — one line per entry, newest first, linking into the archive.
  That cuts the mandatory read from ~100K tokens to ~15–18K, an ~85% reduction. The risk is the
  one this item already named: a reader who does not follow the link loses the reasoning that is
  the whole point of the file, exactly the risk `CLAUDE.md`'s guide split ran into and solved with
  `RepositoryGuideTests` — a tiling/set-comparison check that the split covers the original
  exactly and a pointer cannot rot silently. The same discipline (an analogous test holding the
  index and the archive to each other) would have to be built alongside the split, which is why
  this remains costed and not done: it is a slice of its own, as the previous note said, and
  still the owner's call rather than something to do as a side effect of an audit.

### 9. Visual regression testing — **closed, and then closed properly**

> **Read this heading note first.** When this entry was written the check covered seven pages;
> four were then dropped because a locally-rendered golden could not agree with CI's Chrome, and
> the entry below still describes the seven-page version. All seven are back, and the goldens now
> come from `.github/workflows/visual-goldens.yml` on `ubuntu-latest` — the same Chrome that
> compares them. The comparator itself also turned out to be unable to see a uniform whole-page
> colour shift, which is a hole this entry's confident tone did not anticipate. Both are in the
> completed entry above.

Nine browser harnesses asserted verdicts — sticky, narrow, motion, theme, shortcut, insets — and
none of them looked at a pixel, so four palettes and three new screens were judged by eye. Closed
by `scripts/visual-regression.sh`, wired into `.github/workflows/build.yml` right after the
existing proof-harness step:

- Screenshots seven proof pages at a fixed 1280×900 viewport (the four palettes via
  `proof-shell-*.html`, plus the front door in both light and forced dark, plus the rules
  reference) with `--virtual-time-budget=5000` — the `.panel` entrance animation is the exact
  trap named throughout this file, and 5000ms clears it with margin.
- Compares each against a committed PNG under `tests/visual-goldens/` with `scripts/visual/diff.mjs`,
  a ~150-line plain-Node PNG decoder/differ using only `node:zlib` — no image-diff package is
  installed, on purpose: this repository has never had a `package.json`, and adding the first npm
  dependency for a CI convenience is a worse trade than the ~150 lines.
- **The goldens are Linux-rendered, never from this Windows machine.** On a Linux host (CI) the
  script drives the Chrome already on PATH; everywhere else it drives `selenium/standalone-chrome`
  in Docker — real Google Chrome, not a distro-patched Chromium, so a developer's own machine
  produces the same pixels CI would. The goldens committed here were generated exactly that way,
  from this Windows machine, through that Docker path — verified pixel-identical across two
  independent runs.
- **Broken and watched to fail, not just reasoned about.** `--primary` on the Hero-light palette
  was changed from `#1B4F9C` to `#2E8B57` and the screenshot regenerated: the check failed on
  exactly the three pages that token reaches (`shell-hero-light`, `front-door-hero-light`,
  `rules-reference` — its Search button), reporting pixel counts, percentages and a bounding box
  each time, and left the other four pages (a different palette or a page not using `--primary`)
  reporting pixel-identical. Reverted, re-verified green.
- A first attempt at the Docker path used `zenika/alpine-chrome`, which pulls without any
  apt access and is the common choice for "headless Chrome in Docker" — and turned out to render
  a forced dark colour scheme differently from `ubuntu-latest`'s real Google Chrome on the same
  flag, which would have meant goldens that agreed with themselves and disagreed with CI forever.
  A from-scratch Debian image with `apt-get install google-chrome-stable` was tried next and hit
  this environment's network mangling Debian's signed release file
  (`Clearsigned file isn't valid, got 'NOSPLIT'`) — not a Windows-vs-Linux problem, a
  this-sandbox-vs-`deb.debian.org` one. `selenium/standalone-chrome` sidesteps both: it is
  pre-built with real Google Chrome and needs no package-manager access at all.

---

### 14. A combat simulator — a second engine, and the balance question is now live

**The owner wants to run encounters through the raw engine, over MCP rather than the browser, so
balance can be measured instead of guessed.** This is not a change to `engine/`, and
[`CLAUDE.md`](CLAUDE.md) already settles why: play rules do not go into `engine/`, which is the
authority on cost and validity and knows nothing about resolving an action. **A combat simulator is
a second engine beside it**, and that entry was written before anybody asked for one.

**What makes it worth doing now is that there is something to measure.** Twenty-eight Pinnacle City
NPC sheets exist at known tiers — Street Level through Iconic — against a stated 100-point party.
Whether a Standard-tier Lynchpin is survivable for four 100-point Heroes, or whether Schism's
Growth-above-the-cap trigger is a fair Arc Two problem, are questions with numeric answers nobody
has computed.

**The first cost is not the simulator, it is the data.** Chapters 3, 4 and 5 are extracted into
`data/rulebook/` as prose, but *only* `data/rules/` is verified entry by entry against the page —
so the action, combat and Resolve/Adversity rules would need the same treatment the 141 Powers got
before any simulation could be trusted. Skipping that produces a simulator that is confidently
wrong, which is worse than none.

Not started. No estimate. Recorded so the architecture note above is not rediscovered from scratch.

### 15. The Trait Cap is the tier's, and a campaign may want a tighter one

`CharacterValidator` takes the Trait Cap from the chosen tier — 12d at Standard, 8d at Street Level.
**A campaign can impose a tighter ceiling that no tier expresses.** The Pinnacle City setting caps a
non-superhuman NPC at **6d — peak human, with 3d an average adult** — which is a house rule the tool
cannot see, so a sheet breaking it still validates `ok: true`.

Today this is audited by hand. It was audited by hand three times across twenty-eight sheets in one
session and held every time, including across five independently-built clusters — but that is a
property of that session, not of the tool, and nothing stops the next sheet breaking it silently.

**The obvious shape is a `--trait-cap` override on `build`, or a field on the character file,
reported under its own issue code so the repair is mechanical.** One thing to settle before
building it, because it is not cosmetic: **the Trait Cap is also what Resolve is computed from** —
`DerivedStatsCalculator` takes it off the gap between the cap and the highest relevant rank. So an
override changes derived stats, and whether a *house* cap should move Resolve, or only gate
validation while the tier's cap keeps doing the arithmetic, is the actual design question. The flag
is the easy half.

Not started.

### 16. The tool costs one character, and a campaign is a roster

**Found by using it for its actual purpose for the first time** — statting twenty-eight NPCs for a
campaign in one sitting, across several agents working in parallel. Nothing below is a defect;
every one of them is the shape of a tool built to cost *a* character meeting a job that is about
*all of them*.

- **`--from` takes one file.** Twenty-eight characters is twenty-eight process starts, and every
  re-validation after an edit is another twenty-eight. Shell loops were written for this four
  separate times in one session. `--from-dir`, or a repeatable `--from`, would make a roster-wide
  re-check one command and one process.
- **Export filenames carry a timestamp and nothing overwrites.**
  `CharacterSheetRenderer` builds `{safeName}_{yyyyMMdd_HHmmss}`, so re-exporting a roster after an
  edit *adds* a set rather than replacing one. Twenty-eight characters reached fifty-six `.txt`
  files before anybody noticed, and a de-duplication script had to be written to find the newest of
  each. A stable-name mode — `--overwrite`, or a `--out` that replaces — is the fix; the timestamp
  is right for a single export and wrong for a roster.
- **There is no cross-sheet question the tool can answer.** Every one that came up had to be a
  throwaway script against the JSON: *which Traits on any of these sheets exceed 6d, and does a
  Power justify it*; *is this character's power ladder monotonic across its three tiers*; *which
  sheets are spending their remaining budget on Contacts because Contacts is the cheapest dial*.
  The last two of those each caught a real defect — a "progression" sheet that was **weaker** than
  the one below it, and five sheets padded with invented contact categories. Those are exactly the
  questions a roster owner has, and the tool cannot be asked any of them.
- **`--no-build` is undocumented and is the thing that makes concurrent use safe.** Several agents
  running `dotnet run -- build` in one working tree collide on the compiler. `dotnet run --no-build`
  fixes it completely and appears in neither `--help`, nor the skill, nor any guide. It was found by
  guessing.

None of this needs new rules knowledge — it is all the same engine, called differently.

**The browser half of this finding is closed — see [the archive](docs/progress/).**
The same twenty-eight NPCs are what broke the character manager, and a roster page that can be
filtered, grouped by game and read at a glance is what came of it. **Nothing above is affected**:
every bullet here is about `cli/` and `sheets/` — one file per `--from`, timestamped export names,
no cross-sheet question — and the browser cannot answer any of them. The cross-sheet questions in
particular stay open and are the most valuable of the four.

### 21. Variants of one character are a naming convention doing a structure's job

**Left ajar rather than decided.** The owner's roster holds *Cael Hughes — Emergence*, *— Realised*
and *— After School Specials*; two characters called *Emir Hughes*; and *Lena (true capability — GM
eyes only)* beside *Lena (as observed)*. Those are versions and secrets, expressed in a name because
there is nowhere else to put them. Asked whether it deserved a mechanism, the answer was *"perhaps a
thing to think about"* — 2026-09-01.

**The recommendation it was left on, which stands until somebody has used the roster for a while:**
ship the filter and the grouping first and see whether they carry it. Typing `hughes` already
gathers all four, and the two Emir Hugheses stopped being a mystery the moment they were drawn under
different game headings. A structure invented from two examples is a structure the third example
does not fit.

**If they do not carry it, the honest shape is a version-of relationship and not a tag.** These are
not arbitrary buckets — one is *the same character later*, and the other is *the same character as
two audiences see them*. A tag would model neither, and would be the fourth grouping mechanism
beside campaigns, games and names.

**One thing to weigh first, because it is a fact and not a preference:** a variant that is "the same
character at a higher tier" is exactly what a campaign clone already is, one level down — see the
completed campaign entry. Whatever is built here should be checked against that shape before it is
designed, or the tool will hold two different answers to "another version of this character".

### 17. Closed: `master` in the prose after the branch became `main`

The workflow triggers were fixed first and the prose was not, which is not cosmetic: this file's own
Current state table said the site "deploys from `master`" while `main` said `main`, and that line
produced the only real conflict in an otherwise clean rebase — one where **both sides held a true
fact the other lacked** (this branch knew migration 0006 was pending; `main` knew the deploy branch
had moved), so neither `--ours` nor `--theirs` was correct and it had to be resolved by hand.

**Swept, and the rule that decided each mention is the point.** A sentence a reader would *act on*
must be true today; a sentence recording what happened must not be rewritten, because rewriting it
is how a record stops being one. So:

- **Fixed, all present tense.** `docs/guide/hosting.md`'s two Qodana lines — a guide is
  instructions, and they contradicted the comment inside `qodana_code_quality.yml` itself, which
  already said `main`. Here: "Qodana … runs on `master` and weekly", "a scan of `master` **as
  merged**", "Qodana still watches `master`", and the Hosting entry's "deployed by GitHub Actions on
  every push to `master`".
- **Left exactly as written, all past tense.** Qodana having drifted to 3 *on `master`*, the
  migration gate's refusal having blocked `master`, PR #73 having gone into `master`, the three
  visual-golden entries, and the mutation recorded as "taking Qodana off `master`". Each is a
  statement about a day that happened.
- **One rephrased rather than corrected**, because both halves were true and only one still is: the
  `--branch` entry's "New projects default to `main`; we deploy `master`" was the *cause* of the
  404 it describes. It now says "when this was found we deployed `master`", so the hazard still
  reads as general — a `--branch` label that disagrees with the configured production branch
  deploys nothing to production and goes green — without the entry claiming a mismatch that no
  longer exists.

**The counts this item used to carry were themselves wrong** — "fourteen mentions here and six
across `docs/`" against sixteen lines and eighteen occurrences, drifted by later commits, which is
the same failure the Tests row above records. That is why the sweep is recorded by *rule* rather
than by number: a number in this file is a thing that goes stale, and the rule does not.

**No guard, and deliberately.** A grep for `master` would have to allow every historical mention
above, so it would be an allowlist of exactly the lines a person already decided about — the shape
`WorkflowFilterTests` records rejecting for the same reason. The triggers themselves *are* guarded,
which is the half whose failure is silent.

### 18. The MCP server did not start — **closed. Nothing had started, and there is now nowhere for that to hide**

**The registered binary was not on disk.** `~/.claude.json` named
`%LOCALAPPDATA%\ProwlersAndParagons\mcp-server\ProwlersAndParagons.Mcp.exe` and neither that
folder nor its parent existed, so the client launched a path that returned `No such file or
directory` and reported `CONNECTION_CLOSED`. **The instruction to "read the server's own stderr"
could never have been carried out**: there was no process, so there was no log — which is exactly
why a session spent looking for one found nothing.

The server itself was never at fault. Over stdio it answers `initialize` and `tools/list` with
4,448 bytes of protocol and one line on standard error, all six tools present.

**The fix is that there is no longer an install path to go stale.** [`.mcp.json`](.mcp.json) at the
repository root registers the server project-scoped, at a path relative to the checkout — so a
clone, another machine, another operating system and a git worktree are all correct with no absolute
path to install. Two guards in `McpSetupDocumentationTests` hold it, both broken and watched to
fail; the point of moving the registration into the repository is that a check can see it at all,
which was impossible while it lived in one file on one machine.

**It runs `dotnet exec mcp-server/ProwlersAndParagons.Mcp.dll` rather than the build tool, and that
sentence used to read "through the build tool ... with nothing published".** The registration was
launching out of `mcp/bin/Release`, which is the directory a Release build of this repository writes
to — so a connected server failed `dotnet build --configuration Release` at 10 warnings and 2
errors until it was stopped. One command after a clone or a pull is the cost, and it buys back every
Release build and every `dotnet test --configuration Release`:
`dotnet publish mcp/ProwlersAndParagons.Mcp.csproj -c Release -o mcp-server`. Measured both ways,
with the guards broken and watched to fail, in
[the archive](docs/progress/2026-09-02-the-lock-was-the-servers-read-path.md).

What is left open is small and belongs to whoever meets it: Claude Code gates a repository-proposed
server behind **one approval per checkout**, granted outside an interactive session by
`enabledMcpjsonServers` in that checkout's git-ignored `.claude/settings.local.json` — not by the
key of the same name in `~/.claude.json`, and not visibly, because `claude mcp list` says *Pending
approval* either way. The publish route in the guide is still the right one for Claude Desktop or a
client that is not working inside a checkout. Full account, including the MSIX-redirection hazard that may or may
not have caused the original loss, in [the archive](docs/progress/2026-09-02-the-mcp-server-that-was-never-there.md).

### 19. The account cap is set by hand in SQL, and a GM cannot see what a player holds

Two halves of one screen, and **the mechanism for the interesting half already exists** — this item
is a UI over behaviour that is already correct, not a change to it.

**The cap already does the right thing when it is lowered below what somebody holds.** `putCharacter`
in `worker/db.js` is one `INSERT … SELECT` whose `WHERE` *is* the check:

```sql
WHERE EXISTS (SELECT 1 FROM characters WHERE user_id = ? AND id = ?)
   OR (SELECT COUNT(*) FROM characters WHERE user_id = ?)
       < (SELECT character_limit FROM users WHERE id = ?)
```

The first clause lets an id the account already owns through **however full the account is**, so
dropping somebody from 25 to 3 while they hold ten keeps all ten openable, editable and saveable,
and simply refuses the eleventh until they delete themselves back under. That is exactly the
desired behaviour and it needs no migration, no new column and no data change — **the number is the
whole mechanism.** It is also concurrency-safe by construction, for the reason the doc comment
gives: a read-then-write would let two simultaneous PUTs both see room and both land.

So what is missing is only the screen:

- **Set the number.** A row per account in `/admin` — email, current character count, editable
  `character_limit` — beside the invitations list already gated by `invitations.isAdministrator`.
  This retires a real ops hazard: [`docs/guide/accounts-server.md`](docs/guide/accounts-server.md)
  already calls `users.character_limit` "a *write* with no gate", and today raising a cap means
  someone running SQL against the production database by hand.
- **Surface what a player holds.** A read-only list of their sheets. **Narrowed rather than
  closed by the sheet view on the approval screen**, and the difference is worth keeping: a GM can
  now read the campaign's *clone* of any member's character at any time — a sheet that member
  deliberately sent and the GM accepted. This item is about their own `characters` rows, which is a
  different set, is not a thing anybody sent, and is still unreachable.

**Scoped to campaign membership, and that is the decision rather than a detail.** The list shows the
players in the GM's own campaigns — joined through `campaign_members` — not every account on the
server. `isAdministrator` is one person today, so an all-accounts list would not bite yet; it would
the moment a second GM is ever made an administrator, and a privilege that only misbehaves later is
the kind this project has been bitten by before. The cost is accepted knowingly: **a GM cannot see a
player's characters that are not in one of their campaigns, and should not.**

**Show `label`, `updated_at` and the count. Do not open the payload.** `characters.payload` is an
opaque blob the server never parses — the same property that keeps `campaigns` dumb — so listing a
character's *tier* would mean parsing sheets server-side and giving the accounts server an opinion
about what a character is. It has never had one. If a tier column is wanted later, the honest way is
a column written by the client that already knows, not a server that learns to read.

Not started.

### 20. xunit.v3 4.0.0 is a test-platform migration, and it is measured but not done

**Dependabot raised it as a chore ([#112](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/112)) with [#111](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/111) chained to it, and both were closed deliberately rather than merged or ignored.** 4.0.0 defaults to Microsoft.Testing.Platform v2, and MTP v2 refuses the VSTest target on the .NET 10 SDK this repository pins:

```
Microsoft.Testing.Platform.MSBuild.targets(320,5): error : Testing with VSTest target is no
longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later.
```

`dotnet build --configuration Release -p:ContinuousIntegrationBuild=true` is **clean at 0 warnings** on the bump. Only `dotnet test` fails, which is the tell: this is about how tests are invoked, not about the code.

**The recipe works and is written down here so the slice does not start from scratch.** `dotnet.config` with a `[dotnet.test.runner]` section does *not* take on SDK 10.0.303 — `dotnet test --help` says the opt-in is `global.json`, and it is right. Adding

```json
"test": { "runner": "Microsoft.Testing.Platform" }
```

beside the existing `sdk` block, on a branch carrying both bumps, was measured to give:

- `dotnet test --configuration Release -p:ContinuousIntegrationBuild=true` → `total: 4810, failed: 0`
- `--no-build` works (`build.yml` depends on it), and so does naming one project (`visual-goldens.yml` depends on that, and it reported `total: 769`)
- **both `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` delete cleanly** — removed from both test projects, all 4810 still run. Under MTP the test project self-hosts and the VSTest adapter is dead weight, which is why #111 was closed rather than merged: the correct change there is a removal, and a runner major without the framework major is a pairing xunit does not ship as a pair and nobody has tested.

**What makes it a slice rather than a key is the guide it invalidates, and the news there is good.** [`docs/guide/testing.md`](docs/guide/testing.md) records, as a measured fact under VSTest, that *a crashed test process still prints `Passed! - Failed: 0`* and tells the reader to grep for `Catastrophic`. Under MTP that is gone. A deliberate stack overflow — a `[Fact]` calling an unbounded recursion — printed:

```
Test run summary: Zero tests ran
  error: 1
  total: 0
Test run completed with non-success exit code: -1073741571
```

So the trap the guide exists to warn about is **closed**, not reworded. But that is a claim about a check, and this repository's rule is that a claim about a check is worth nothing until somebody has broken it and watched it fail — so the guide gets rewritten around a freshly-proved failure mode, not edited to match this paragraph. Two smaller consequences travel with it: the per-project `Passed!` lines are replaced by one combined `total:`, which `testing.md` tells a reader to count and which the Tests row above is a breakdown of; and `global.json` is read by `actions/setup-dotnet` in three workflows, so the opt-in is not local to the test projects.

**Not started.** Dependabot will re-raise both when 4.0.1 or 4.1.0 lands, which is a fine moment to do it properly. The measurements above are from 2026-09-01 and are worth re-taking rather than trusting — they were made against SDK 10.0.303 and MTP 2.3.3.

## Completed work

**It is in [`docs/progress/`](docs/progress/), one file per slice, and it is not here for a
reason worth reading once.** Every finished slice used to append its account at the top of this
section — the same anchor, in the same file, every time — so any two branches open at once
conflicted on bytes neither had anything to do with. The roster slice rebased five times in one
afternoon and every conflict was this heading. The archive was also **85% of this file**, which
made the part people are sent here to read the part they had to scroll past.

**So: finish a slice, write `docs/progress/YYYY-MM-DD-your-slug.md`.** Two branches on the same day
write two filenames and merge clean. `ProgressArchiveTests` holds the shape — including that this
section stays a pointer — and neither this file nor that directory is skipped by the build any
more, because a skipped path whose contents a test checks is a change that merges without the build
that would have caught it.

**What stays here is what is not finished**, plus the state table at the top — which is what
`CLAUDE.md` sends a reader to this file for.

## How to maintain this

When you finish a piece of work:

1. Take it out of **Remaining**, and write the account of it in **a file of its own** —
   `docs/progress/YYYY-MM-DD-a-short-slug.md`. Say what changed and *why*; the reasoning is the part
   that is expensive to recover. See [`docs/progress/README.md`](docs/progress/README.md) — it is a
   directory rather than a section here because a shared anchor made every concurrent branch
   conflict on it.
2. Update **Current state** if the headline numbers moved (entry counts, coverage). **The test
   figures are not among them any more** — that row names `./scripts/count-tests.sh` instead,
   because a number recorded in prose here went wrong four times and the script cannot.
3. If the work revealed new gaps, add them to **Remaining** rather than leaving them in a commit
   message.
4. Link the PR.

If a task turns out to be partly blocked, say so explicitly in the item and name the blocker. An item that quietly narrows its own scope is worse than one that stays open.
