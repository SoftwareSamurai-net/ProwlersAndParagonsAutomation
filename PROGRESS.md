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
| Tests | **Five suites, and the figures are not written down here.** Run `./scripts/count-tests.sh` — it runs all five, reads each count out of the line that runner printed, and refuses to total anything when a suite did not report. **The figures used to be in this cell and went wrong four separate ways**; the four are recorded in [`docs/guide/testing.md`](docs/guide/testing.md), where the lesson keeps being true after the numbers stop being. The five are the engine, the components under bUnit, the accounts server over real SQLite, the pixel comparator, and the deploy's migration gate (`./scripts/test-deploy-gate.sh`, a fifth suite because the gate is a decision over wrangler's output and a workflow cannot be executed by any of the other four). **A sixth thing drives the assembled application and is deliberately not one of the five**: `./scripts/e2e.sh` publishes the site, serves it with the `wrangler pages dev` version the deploy pins, and drives real Chrome. **How many checks it runs, what each is worth without its positive control, and which of them a given driver can reach are not written down here either** — run it, and read [`docs/guide/testing.md`](docs/guide/testing.md), which is where that account is kept up. This cell has already recorded that figure wrong once. It reports verdicts rather than a test count, so `count-tests.sh` does not know about it; see [item 10](#10-driving-the-assembled-app--stage-one-is-built-stage-two-is-only-a-decision-about-effort) for what it does and does not reach. |
| Wizard | All seven creation steps working — the seventh, Vehicles and bases, added 2026-09-10 for Chapter 6 — with back-navigation and `.txt` + `.json` export |
| Front ends | Two interactive, plus two for a machine — the terminal wizard, a Blazor WebAssembly app, `build --from`, and an MCP server somebody can connect to their own Claude. All on the same engine assembly |
| Hosting | **Live** at [superheroes.softwaresamurai.net](https://superheroes.softwaresamurai.net), with the `prowlers-and-paragons-chargen.pages.dev` fallback; deployed from `main` by GitHub Actions, which applies pending D1 migrations before the Pages upload and refuses rather than guesses. **What the deploy last did is not written down here** — it moves when somebody deploys rather than when somebody edits this file, which is how this cell went stale while nobody was looking at it. The workflow's own runs are the record. [`docs/guide/hosting.md`](docs/guide/hosting.md) carries the mechanism, the two failures that built it and what each token permission was proved by; `./scripts/test-deploy-gate.sh` drives the decision it makes |
| Accounts | **Invitation only, and sign-in works end to end. An account is now what opens the rulebook** — all ten chapters, searchable at `/rules`, plus the recordings and the two sample characters. **Which migrations exist, and which of them the remote database has, are not written down here**: `ls d1/migrations/` answers the first, `wrangler d1 migrations list prowlers-and-paragons --remote` answers the second, and the deploy asks that same question before every upload. This cell used to carry both figures and was wrong about each in turn — it named a migration as still to be applied and was right when it was written, then went stale the moment somebody did the thing the gate exists to automate, which is the ordinary way a measured figure in this file stops being true. The `DB` binding is in place, `/api/me` answers `401` with JSON — checked by the deploy after every upload — and all four variables are set. **A link has been requested on the live site, delivered, and used to sign in** — watched, not tested, because no test can do it. The fault that blocked it for a week was the API key and not `MAIL_FROM`; see [item 8](#8-the-mail-provider-is-refusing-every-send--closed-and-the-reasoning-here-was-wrong). **Adding an address now actually mails it** a one-click, three-day link; until now the admin page said an address "can sign in now" and nothing ever told them so |
| Printed sheet | One A4 page on the published Hero Sheet's layout; Hero and Villain ink on white paper |
| Static analysis | Zero warnings at CI strictness, which the build enforces rather than records. **The whole-tree Qodana figure is not written down here** — run `./scripts/qodana-scan.sh`. Since Qodana came off pull requests deliberately, that local run is the *only* thing between a branch and `main`, so the answer to a doubt about the figure is always to run it rather than to put the workflow back. **Every reading this cell ever carried was produced by somebody re-running the scan and none by CI**, and two of them landed in code the change under review had not touched — a package upgrade moving an inspection, which is the case a pull-request-mode scan structurally cannot see. [`docs/guide/testing.md`](docs/guide/testing.md) keeps that history, and the three traps that make a by-hand scan report clean when it inspected nothing. **Do not name a commit's sha here**: it was tried and an amend orphaned it within the hour, which is a dead pointer of exactly the kind this repository treats as worse than none |
| Known-wrong data | None outstanding. Every published Hero is now also checked for *legality*, not only cost, which is what found the two the tool used to refuse |
| Licence | MIT, in `LICENSE`, covering this repository's own code only. The game system is © LakeSide Games. `data/rules/` holds structured metadata and this project's own descriptions; `data/rulebook/` holds the book's text **by the author's permission to this repository's owner**, is not served by the public site, and does not travel with a fork |

The engine reproduces the printed Edge, Health and Resolve of all 20 pre-built Heroes in Chapter 8, and rebuilds **17 of the 20 to exactly their 125 Hero Point budget**. The remaining three each rebuild 1 HP out, for a recorded reason — see [Close the last three Heroes](#1-close-the-last-three-heroes), where the bound is stated exactly: it holds of what is *modelled*, and Shadow's printed Gear box carries a custom feature that would put him at +2.

---

## Remaining work

**This section is a todo list, and the tick is the orchestrator's signature.** It is not a
summary of the work — that is what the pull request is for, and duplicating it here is how this
file went stale in the first place. Each box names an item and nothing more; the entry below it is
the brief, and the PR is the account.

### What a tick means, and what it does not

**A box is ticked by the orchestrator, never by the agent that did the work**, and only after the
orchestrator has verified the work *itself* rather than read a report saying it was done. That
distinction is the whole point of the mechanism: this repository has shipped a feature that was
built, tested, adversarially reviewed by two independent agents and merged, while nothing in the
application ever wrote to the store it read from — see item 10. Every one of those reviews was
honest and every one was wrong, because each checked the thing the one before it had checked.

So, for a tick:

- **The orchestrator ran the check, and read the verdict.** Not "the agent reports the twin went
  red" — the orchestrator re-ran the twin and saw `FAIL`, and re-ran the real page and saw `PASS`.
  An agent asked to confirm its own guard fails will confirm it.
- **The mutation was not null.** A break that changes nothing observable proves nothing; the honest
  report is that the mutation was a no-op, not that the guard has a hole. See `CLAUDE.md`.
- **The suite is green, measured after the change and not before it.** A green run taken before a
  revert says nothing about the tree being committed.
- **The diff contains what the message claims.** `git show --stat HEAD` against the PR title; a
  missing file in that list is the whole failure, visible in one line.

**An item that was attempted and failed verification stays unticked and is marked `RELITIGATE`,
with one line saying what failed and a link to the run or PR.** It does not silently return to the
pool: an unticked box with no history is indistinguishable from work nobody has started, and the
next agent to pick it up would repeat the failure rather than answer it.

**Only ticked items are reported to the owner as done.**

### The list

Ordered roughly by what unblocks the most. **[Item 11](#11-answered-it-is-a-tool-for-running-and-playing-pp)
is answered and is the entry to read first** — the owner has said this is a tool for running *and*
playing P&P, which unblocks all eight of the things that entry lists and widens what item 3 counts
as in scope. **Nothing here is a defect.**

**Waiting on the owner — not work an agent can pick up**

- [x] **[13](#13-the-owners-branding-and-the-sign-in-email)** — the owner's favicons and manifest are served and the mark sits in the banner beside the wordmark, on all four palettes; the four shell goldens were regenerated on the runner. Verified by the orchestrator 2026-09-09: the manifest's colour format and its need for an unmasked icon each went red under mutation. Two choices are recorded in the entry for the owner

**Ready to build, specified enough to start**

- [ ] **[38](#38-a-villain-approved-into-a-campaign-becomes-the-gms-and-the-players-nemesis)** — **the next piece of work.** An approved Villain moves to the campaign owner's account for good, counted against their character cap; the player keeps no sheet, only the Villain's name on the campaign screen drawn as their nemesis with an effect worth a double take; Send on a Villain warns first that approval cannot be undone. All four questions answered by the owner on 2026-10-01
- [x] **[21](#21-variants-of-one-character-are-a-naming-convention-doing-a-structures-job)** — character variants get a mechanism. **Slice one built 2026-09-11** (see the pull request that carried it): a sheet may say it is a version of a root character — *later*, *as seen by another audience*, or an *alternate form* — the roster and the switcher draw the family as a tree, and a "Version of…" control makes or clears the link. Verified by the orchestrator: the cycle guard and the tree's read of the index field each went red under mutation. **Slice two built 2026-09-23**: `engine/AlternateForms.cs` checks a roster's `alternate_form` families through `build --from … --from …` — both pay for the Power at the same total, a form's power level is one the root paid for and not above the root's, one Resolve pool at the lowest of the forms. Verified by the orchestrator: the pool taken as the highest instead turned `ThePoolIsTheLowestOfTheForms…` red. **Slice three built 2026-09-29, and the item is closed**: the owner ruled on p.21's Trait Cap sentence — *the form declares, the family verifies*. An `alternate_form` sheet may carry its root's Trait Cap above its own tier, so `TRAIT_CAP_ABOVE_TIER` is waived on it, and `AlternateForms` reports `ALTERNATE_FORM_CAP_NOT_ROOTS` on a form whose cap above its tier is not the root's; Resolve and the pool follow from the declared cap. A family's findings now print on the roster and in the banner switcher, labelled Error or Warning, with the shared pool on the root's row, and the character server has a seventh tool, `check_alternate_forms`, that answers `ok: false` when any member did not parse. Verified by the orchestrator: dropping the waiver, accepting any form cap, waiving it for every sheet, printing an Error unlabelled and calling an unreadable roster ok each went red under mutation
- [x] **A Resolve and Adversity quick reference**, on the owner's ask of 2026-09-29, at `/reference/resolve` and open without an account: every entry of `data/rules/play/resolve.json` under For players, For the GM or Table limits, each with a table-facing `summary` (new on all 28 entries), its cost and its page. `web/` may name that one whole path and no other play file, by a narrowed `NothingInTheApplicationNamesAPlayRulesFile`, and reads it with its own strict display-only reader. Verified by the orchestrator: another play file named from `web/`, the exempt path split to hide it, the engine naming it, a hard-coded assisting rate, the description shown instead of the summary and program vocabulary in a summary each went red under mutation
- [x] **The approval diff reads part by part, and says when it was sent**, on the owner's report of 2026-09-30: a Power's row printed its whole line twice around an arrow, and a request carried no date. `DiffRow.Parts` marks each part of a row as kept, gone, new or changed and `DiffRows` draws it so on every screen that draws a diff; the roster row and the diff carry the sent and approved times the server was already sending. Verified: pairing switched off, the whole line drawn again, and the dates hidden each went red under mutation
- [x] **[1](#1-close-the-last-three-heroes)** — **closed by the owner's ruling of 2026-09-23**: the three Heroes still 1 HP out (Scáthach +1, Shadow +1, Vigilant −1) are dropped as a target. The rules here are adapted, so the smallest distance to fitting is taken as correct and no further investigation is owed. Shadow's and Vigilant's custom gear features stay untranscribed, because adding them would move both further out. The residual guard stays at 1
- [x] **[10](#10-driving-the-assembled-app--stage-one-is-built-stage-two-is-only-a-decision-about-effort) stage two** — the signed-in half of the driver, and `kill_tree` proved directly. Verified by the orchestrator 2026-09-05: nine checks green, nine twins red on the kind each declares, both drivers, no process left behind. **CI run 33949251306 then proved the Linux leak for real** — the port holder survived outside the tree — and the cause is recorded in the entry; the fix's Linux verdict was given by run 33960793977: all seven kill-tree checks green on `ubuntu-latest`, the real wrangler tree included, and no "still listening" warning anywhere in the job
- [x] **[10](#10-driving-the-assembled-app--stage-one-is-built-stage-two-is-only-a-decision-about-effort) retirement** — `scripts/e2e/drive.mjs` and `cdp.mjs` are deleted, on the condition the entry set: twenty consecutive green `Build` runs on `main` since run 34400118356, each with the Playwright step printing `E2E: PASS — 9 checks`, the twentieth being run 36674185863. Re-counted by the orchestrator 2026-09-30 from the twenty logs, not from the ticks. `e2e.sh` has one driver and no `--driver` flag, `build.yml` has one drive job, the twin/check comparison is equality again, and `E2eDriverTests` reads the one driver and refuses the deleted files back. Verified by the orchestrator: an empty `drive.mjs` recreated and a twin renamed to a check nobody drives each turned the class red
- [x] **[12](#12-the-interface-the-owner-asked-for-which-needed-none-of-item-11s-answer)** — the three-door rearrangement, and the rulebook corpus behind `Ctrl`/`⌘`+`K`. Verified by the orchestrator 2026-09-05; what remains of the search bullet is the banner field, recorded in the entry
- [x] **[14](#14-a-combat-simulator--a-second-engine-and-the-balance-question-is-now-live)** — a combat simulator, explicitly a *second* engine beside `engine/`. **All six slices have landed; (f) was run on 2026-09-07 as the demonstration the owner ruled sufficient, corrected on 2026-09-08 when the styles slice exposed non-combat Traits fighting, and its figures are in the entry.** Verified by the orchestrator: every figure produced by the orchestrator's own driver. **Slices (a) to (e) of six have landed** — Chapters 3, 4 and 5 as verified data under `data/rules/play/`, `play/`, the second engine that resolves a fight out of them, and `mcp-play/`, the second MCP server that runs encounters through it; **(f), the first measurement, was run on 2026-09-07.** The owner deferred it on 2026-09-06 until the campaign had a villain, then ruled on 2026-09-07 that a demonstrable run makes the feature complete and the real party is not required. The table it will run at is settled: no Gritty rule, and the GM's alternative to seizing the initiative — doubled Edge rather than going first, "so Super Speed stays awesome" — which is already `TableRules.GmAlternativeToSeizingInitiative`. **The encounter server takes a fight's table off the sheets it is handed since 2026-09-07** (see the pull request that carried it), which is how a campaign's toggles (item 29) reach a measurement without the server ever reading a campaign: sheets that carry a table must agree switch by switch (`TABLE_DISAGREES` names the pair and the first switch, in id order whatever order they arrived), a `table` on the call must agree with them (`CALL_TABLE_DISAGREES`), a sheet carrying none is accepted and named on page one — with its campaign, since an absent block cannot tell a sandbox character from a stale copy of a game that adopted something later — and the echo says where the table came from and can rebuild it. The review found the agreement order-dependent, an inert Gear Limit rank refusing a legitimate fight, a page-one line claiming a page for a fact no page states, and the sheets-and-call branch never naming a bare sheet. Verified by the orchestrator: the rank comparison and the id ordering each went red under mutation. The engine's own not-yet list is in `docs/guide/play-engine.md`, and it is shorter: **keeping hold, knockback, luring and team attacks are applied since 2026-09-07** — see the pull request that carried them, whose review found the server dropping the team flag it had just documented, an explosion throwing the sixes of a discarded roll, and an Adversity line written before the pool paid. Verified by the orchestrator: the lurer's forfeited turn and the team flag over the wire each went red under mutation. **p.85's three Adversity spends — suppress a Flaw, a misfortune, an act of villainy — are applied since 2026-09-07 too** (see the pull request that carried them): the pool is charged once, the printed eligibility and limits are enforced, the narrative half is required in the GM's words and recorded as theirs, and the once-per-story count is kept per encounter with the ledger saying so — carrying it across scenes is a slice of its own, as is the per-issue count on the suppression. The review found the GM's pool buying a Hero what their own Resolve buys, which every earlier fixture had missed by pointing every spend at a Villain. Verified by the orchestrator: a Foe handed villainy, and a Hero bought dice from the GM's pool, each went red under mutation. **p.75's cover, size and visibility are applied since 2026-09-07 as well** (see the pull request that carried them), and `Encounter.EntriesNotYetApplied` is empty: cover is a fact about a line of sight and lives on the attack, with the through-cover clause applied for real; size is the caller's word on the combatant, the factor derived from two sizes and never accepted as a band; the light is the scene's, set at `Begin` and echoed with the table; being invisible is the caller's word, since carrying the Power is not being invisible; Blind Fighting and Radar are read off the sheet and named on the ledger when they compensate. The orchestrator's own mutation moved the five-times band to three and survived — every band fixture sat exactly on a threshold — so the review drove every threshold from both sides, found the run report echoing neither size nor invisibility, a combatant size guard nothing drove, and a through-cover line that called two equal figures the greater. Verified by the orchestrator: the same mutation now turns four cases red. **Five of the seven Gritty switches are applied since 2026-09-07** (see the pull request that carried them): Hard Targets doubles a passive rank before either halving and the vulnerable-part negation is an attack's declaration; Close Range reads whether a Power reaches past Close off its own Chapter 2 Range and says on the ledger when a `zone` or `special` one is declined; the Drop is the caller's word that a character is ready, and its pairwise doubling is exact as one order, with a seizer still first and a ready seizer under the GM's alternative on fourfold Edge, both labelled readings; Friendly Fire derives the melee from the range bands and resolves the stray shot for real against the second target's own defence, picked off the dice source in base six so a melee of nine can reach its ninth; Slow Healing's in-scene clauses bite (conscious at zero or below, any point of damage puts them down, instant recovery restores nothing) and the daily bands are named on page one as outside the scene. The review found the stray shot unable to reach past six bystanders, a charge's impact escaping the any-damage clause, and a silent decline; each is fixed with a fixture, and a run with every switch off is proved byte-identical to a run on a bare table. Verified by the orchestrator: the Friendly Fire trigger boundary and the pick's die count each went red under mutation. **`RaisedGearLimit` and `GearLimitRank` stay listed**, because the default gear limit is not applied either — an `Attack` names a Trait and no item, a `Combatant` carries no gear, and a raised limit cannot honestly precede the default one; a scan is written to fail when that gap closes. **The GM's pool buys all ten of p.85's "anything a point of Resolve could do" since 2026-09-07** (see the pull request that carried it): seizing the initiative, instant recovery, the Fatal Damage rescue and stabilising are charged to Adversity for an NPC with the NPC's non-existent Resolve never touched, each keeping its own page's limits, and a Minion group is refused each by name off the entry that decides it — no Edge to seize with, no Health to come round to, no dying clock — with the deciding phrase held by a throw rather than interpolated. The review found a `points` of zero or below reaching the wire and minting Resolve or Adversity, the once-a-scene count untested against sharing, and p.85's announcement order documented and unguarded; each is fixed with a fixture, and the "not yet implemented" classifier's two-valued control now rests on the two Gear Limit switches with a guard that says so the day they are applied. Verified by the orchestrator: a Minion clock guard and the pricing floor each went red under mutation. **The item a full grab wins is state since 2026-09-07** (see the pull request that carried it): what a combatant walks in holding is the caller's word at `start_encounter`, a grab names its object and is refused for one the target is not recorded as holding — so a fight opened with nobody carrying anything has no grab in it, said out loud — a full grab moves the item to the winner for the page, an attack naming it marks it used, a `toss` drops it, letting go ends a partial grab for either party and both regain their active defences, an attack naming a contested item is refused by p.76's own words, and the page turn tosses what was won and never used or what a defeated holder still had. The review found the first build minting an object out of a sentence (every wire grab was for an item nobody held), no way to let go, a pinned character able to toss the weapon they were pinned with, the property never landing a grab on any seed, and the run report not echoing what anybody held. Verified by the orchestrator: the page-turn toss of a used item and the unheld-item refusal each went red under mutation. Still listed, with no intent to call them: multiple actions, stunts, ambushes, clobbering, defending others and the throwing table
- [x] **[15](#15-the-trait-cap-is-the-tiers-and-a-campaign-may-want-a-tighter-one)** — a campaign-tighter Trait Cap, and it moves Resolve. Verified by the orchestrator 2026-09-05
- [x] **[16](#16-the-tool-costs-one-character-and-a-campaign-is-a-roster)** — `build` checks a roster in one process and answers cross-sheet questions. Verified by the orchestrator 2026-09-05; the monotonic-ladder question waits on item 21
- [x] **[19](#19-the-account-cap-is-set-by-hand-in-sql-and-a-gm-cannot-see-what-a-player-holds)** — a GM sets a player's cap and sees what they hold, on `/admin`. Verified by the orchestrator 2026-09-05

**Recorded, with nothing asking for them**

- [ ] **[1b](#1b-semantic-procon-constraints-are-still-unenforced)** — semantic Pro/Con constraints, no consumer
- [x] **[2](#2-what-the-sheet-still-cannot-say)** — every printed page carries the character's name and `page N of M` in a `@page` margin box, in Chrome (measured on 153); Firefox and Safari print no margin boxes, so the document title and colophon stay as their fallback. Verified by the orchestrator 2026-09-23: a SheetView that never publishes the name turned five `RunningHeadTests` red. Not driven in a real browser on CI — the margin box was proved by hand on a dev server
- [x] **[3](#3-remaining-rulebook-chapters--mostly-not-this-tools-business-while-it-was-only-a-character-generator)** — every rules chapter is extracted as verified data: Chapters 3, 4, 5 and 7 and Ch.6 pp.87–90 on the play side, Ch.6 pp.88–104 on the creation side, all locked to the page and to the corpus. Verified by the orchestrator 2026-09-08: a Plate feature, a Lifting threshold, the Vehicle Point rate, a Size grade and a toxin's option each went red under mutation. What is left is Chapter 8's stat blocks, which are GM material rather than rules, and consuming what was extracted — item 32
- [ ] **[5](#5-the-browser-payload-is-large--a-characteristic-not-a-defect)** — payload size
- [ ] **[41](#41-two-play-tests-fail-on-every-windows-checkout)** — two play tests search for LF-joined text in files a Windows checkout writes CRLF, so they fail locally on every Windows machine and pass in CI; recorded, not fixed
- [x] **[36](#36-four-validator-checks-still-skip-a-gadgets-powers)** — every per-Power validator check walks one enumeration of every Power a sheet pays for, `EveryPaidPower`, so a Gadget's Powers draw the same findings as the character's own and a further check cannot forget them; `GadgetPowerWalkReadTests` holds every remaining direct walk of `SelectedPowers` to a written reason and a count. Verified by the orchestrator 2026-09-30: dropping the Gadget branch of the enumeration turned seven probes red, pointing `CheckPowerCosts` back at `SelectedPowers` turned the source guard red naming the line, and keying the duplicate pool by Gadget name instead of identity turned the same-name-Gadgets test red. One residual is recorded in the entry
- [x] **[37](#37-joining-a-campaign-is-one-character-at-a-time-and-each-join-re-reads-the-page)** — the join box lists the account's characters to tick and joins them in one press, a character not on screen written back by id with the pointer untouched, every refusal said per character, the code kept in the box, and the page's reads run in parallel. Verified by the orchestrator 2026-10-01: the by-id read switched to the pointer-moving one turned `TheNonOpenCharacterIsWrittenByIdAndThePointerDoesNotMove` red, and the in-flight flag removed turned `ASecondCallWhileTheFirstIsInFlightChangesNothing` red; the agent's nine mutations are in the pull request
- [x] **[39](#39-fourteen-pros-and-cons-ask-the-player-to-define-something-and-there-is-nowhere-to-write-it)** — every entry that asks the player to write something has a box labelled with its ask: `SelectedProCon.Detail` and `SelectedPower.Detail`, asked by the picker, the Power editor and the CLI, printed on the sheet, in both exports, the MCP shape and the diff; Expertise, Animation and Immortality's Vulnerable gained the ask in the data. Verified: the picker's wait, the sheet's words, the Ability source line and the JSON export each went red under mutation
- [x] **[40](#40-a-powers-own-pros-and-cons-read-like-the-generic-ones)** — every Pro and Con on the sheet is a term, a Power's own saying whose it is first, and the palette names rules terms by kind so *Vulnerable* and *Vulnerability* sit side by side labelled Con and Flaw. Verified: the owner prefix dropped, the Powers' own options left out of the palette, and the term key ignored each went red under mutation. The picker's grouping is deliberately untouched — see the entry
- [x] **[20](#20-xunitv3-400-is-a-test-platform-migration-and-it-is-measured-but-not-done)** — the test projects run on xunit.v3 4 under Microsoft.Testing.Platform, on the owner's ask of 2026-09-11. Verified by the orchestrator: `count-tests.sh` re-run and its refusal to total a red suite read; the crash trap the guide warned about proved closed with a real stack overflow, output quoted in the guide
- [x] **[22](#22-the-current-state-table-is-where-this-file-actually-conflicts)** — the Current state table's measured cells are pointers now, held there by `ProgressCurrentStateTests`. Verified by the orchestrator 2026-09-06
- [x] **[23](#23-this-files-own-claims-went-stale-in-sixteen-places)** — twenty-two dead pointers fixed, the second `### 9.` renumbered, and `ProgressPointerTests` holds every link, anchor, test name and sha in this file to resolving. Verified by the orchestrator 2026-09-06
- [x] **[24](#24-a-bunit-event-is-dispatched-not-applied-and-three-palette-tests-read-a-render-early)** — three palette tests raced the renderer and went red on CI one at a time; the whole class is swept and a guard fails the build on the next synchronous drive. Verified by the orchestrator 2026-09-06
- [x] **[26](#26-a-campaign-submission-carried-an-empty-sheet-under-a-real-characters-label)** — the owner found an approved campaign clone that was an empty sheet; the join and the submit now act on the character the row names, an empty sheet is refused and marked, and the autosave never writes one over a stored character. Verified by the orchestrator 2026-09-06
- [x] **[27](#27-the-session-must-hold-the-character-the-pointer-names)** — a failed read at sign-in or boot is now said on screen with a retry that re-adopts, the autosave and Start-another both refuse to write over a character this browser never read, and the notice follows the pointer. Verified by the orchestrator 2026-09-07: the unread-id recording, the Start-another refusal and the pointer-scoped notice each went red under mutation

- [x] **[28](#28-two-flakes-on-a-docs-only-pull-request-and-what-the-harness-said-about-them)** — three flakes seen in one day on trees that had passed: the e2e server dying mid-drive with nothing said about why, the in-process MCP teardown race, and a secret-scan regex timed out by a loaded runner. Each is swept class-wide. Verified by the orchestrator 2026-09-07: a lying aliveness test fails the kill-tree suite in thirteen seconds rather than hanging it, the MCP helper goes red with no EOF and with a faulted teardown step, a planted key in `web/` and in a Razor file turns the scan red, and both e2e drivers pass with every twin red on the merged tree

- [x] **[29](#29-a-campaigns-table-rules-and-its-immortality-price)** — the owner's toggles: a campaign carries the table's optional rules and its Immortality price, shown to every member, copied onto the sheet on join, priced from the sheet, and carried by the export. Verified by the orchestrator 2026-09-07: the price bound, and the suppression of a false "campaign gone" for members, each went red under mutation
- [x] **[30](#30-a-member-sees-the-copy-their-character-carries-not-the-campaigns-live-table)** — a member reads their game's live table through a route scoped to their own membership row, sees it beside the copy their character carries with a row per difference and a read-at age, and a character that joined before the GM decided anything is told so under its own finding. Verified by the orchestrator 2026-09-07: the campaign-owner join clause, and the finding's switches arm, each went red under mutation — the second only after a fixture the orchestrator's mutation showed was missing

- [x] **[31](#31-the-account-autosave-lost-an-edit-to-its-own-predecessor)** — one fire-and-forget write per keystroke against a last-write-wins server lost the later edit while the app said Saved; the autosave is serialised and coalesced, "Saved" can only understate what landed, and a guard holds the app to actually starting it. Verified by the orchestrator 2026-09-07: the coalescing flag and the app's `Start()` line each went red under mutation

- [x] **[32](#32-fold-chapter-6-into-the-sheet-and-the-fight)** — the owner's ask of 2026-09-08: the extracted equipment, gadgets, vehicles, headquarters and environment became mechanics — the Gear step picks from the catalogue, `CostCalculator` prices a vehicle, a base and a Gadget in their own currencies, the sheet prints them, the palette offers them, the fight reads scenery Structure, and a campaign holds the shared objects its members fund. Verified by the orchestrator 2026-09-10 across four pull requests: the Gear-Limit cap and the budget-silencing flag, the knockback's smash-through tie, the headquarters currency and the base-budget boundary, and the shared object's id filter and kind clause each went red under mutation. **The printed sheet gained its second page on 2026-09-29**, carrying vehicles, bases, Gadgets (each with the Powers it holds, which the `.txt` export now lists too) and campaign contributions. It renders only for a character who owns one, so everybody else still prints one page. Verified by the orchestrator: the page shown with nothing owned, a Gadget's Powers dropped from the page or the export, and the page break set to `auto` each went red under mutation
- [x] **[33](#33-decisions-chapter-6-left-to-the-owner)** — twelve Chapter 6 design questions, **all twelve answered by the owner on 2026-09-10 and every ruling that was work is built**, across two pull requests the same day. Verified by the orchestrator: each guard broken and watched red independently of the agent that wrote it — and one that did not go red (the shared-vehicle prerequisite path) got the test it was missing before the merge

(Item 4, the Power search's vocabulary, is closed — see below.)

**Several of these touch the same files, so they are not independent slices.** 12 and 16 both
rearrange the app's chrome; 15 and 16 both reach into what a campaign is allowed to say about a
character; 14 and 3 are the same question about play rules from two directions. Two branches that
merge cleanly can still contradict each other, so take them one at a time and re-read this file
between.

Each entry below says what a slice on it would actually involve, including which approaches are already spent. Read the entry here before starting.

### 1. Close the last three Heroes

**Closed 2026-09-23 on the owner's ruling, and not to be reopened.** The owner's words: the least distance to making them fit is probably correct when the rules have been adapted. So the three residuals below are accepted as they stand and are not treated as defects. The engine has priced custom gear features since this entry was written (`gear_features.json`, `GearCost`), and Shadow's Silenced pistols and Vigilant's Upgraded Jo Sticks are still deliberately left out: transcribing them would put Shadow 2 HP out and Vigilant 1 HP out, the wrong direction. `PrebuiltHeroTests` keeps the |residual| ≤ 1 bound, so a pricing change that moves any of the three further out still fails. What follows is the record of the investigation.

Seventeen of the twenty published Heroes now rebuild to exactly 125 Hero Points. The other three are held at a known residual in `PrebuiltHeroes.BuildByHero`, each with a reason:

| Hero | Residual | Why |
|---|---|---|
| Herald (Scathach) | +1 | Strike carries four Pros and Cons at once — most likely a variant reading |
| Shadow | +1 | Unexplained |
| Vigilant | −1 | Its Jo Sticks are *Upgraded*, a custom gear feature worth +2 — which would take him to +1, not to zero |

Nothing left is more than 1 HP out, and the test asserting that bound has been tightened from 6 to 2 and now to 1, so it stays true.

**The "residuals pair up" lead is spent.** It was worth chasing and it paid twice — but what closed Vector and Talon was reading the rulebook entry in each case, not the pattern. What is left is −1, +1, +1, and three values one point either side of zero say nothing. Do not read more into it.

**All four transcriptions have now been read line by line against the printed sheets, and all four are faithful.** Abilities, all twelve Talents, every Power and its rank, the Pros and Cons in each parenthesis, the Perks with their unit counts, the Flaws, and Edge/Health/Resolve — checked against the page for Scáthach (p.135), Shadow (p.140), T-Kay (p.143) and Vigilant (p.146).

**So the method that closed three Heroes is spent, and the conclusion is different from what it was.** Vector, Talon and Airmid were all *transcription* faults — a Power underpriced, a group costed per option, a whole Power dropped. These are not. **The remaining ±1 HP is in the pricing model**, and finding it needs a per-element cost breakdown compared against a hand-computed expectation from the sheet, not another read of the page.

Two things established on the way, so nobody re-checks them:

- **`super_senses_night_vision` at 3 HP flat is correct.** Ch.2's Super Senses entry prices Acute X at 1 HP per 2 ranks, Night Vision at **3**, Thermal Vision at 2, and Radio Hearing, Telescopic Vision, Analytic X and Astral Sight at 1 each. Night Vision being the odd one looks like a data error and is not. The same entry confirms the group's effective rank is "your Perception or your Acute X rank, whichever is greater".
- **Vigilant's gear cannot close him.** His Gear box reads `Armored Suit: 7d Armor` and `2 Jo Sticks: 10d (s) Melee (Upgraded)`. The ratings are Ch.6 mundane armour and weapons, which are free; only *Upgraded* costs, at 2 HP once for the pair under Two-Fisted. He is 1 HP under, so transcribing it lands him on +1. It is left out rather than half-applied, and `PrebuiltHeroes` has no gear collection to put it in.

The two ambiguous grades (`Side Effect: collateral damage`, `Limited: only for Telekinesis`) remain judgement calls that could be revisited, but do not tune them just to force a zero — that is fitting the model to the answer. `Limited` is less ambiguous than "guess" suggests: see below, where the floor turns it into a two-way choice and the grade recorded is the one with an argument behind it.

**Revisited, and deliberately not closed.** The arithmetic was worked out and it is a trap:

- **T-Kay is closed, by the owner's ruling of 2026-09-06 and not by this file**: `Limited: only for Telekinesis` is *somewhat limited* (−1), and she rebuilds to exactly 125. It sits on Lightning Reflexes, a flat 3 HP Power, so the grade was worth exactly her −1 — which is why this entry had refused to pick it: the only argument for the milder grade was that it made the number come out, and that is the tuning this item forbids. What changed is not the arithmetic but who decided. The sheet prints no grade and the book maps no phrase onto one, so it is an ambiguity the owner may rule on, and a ruling is not a fit. `PrebuiltHeroes.cs` records the grade as the ruling with its date, `docs/guide/testing.md` carries the ruling-versus-tuning distinction, and nobody is to re-derive the grade from her total in either direction.

  **And it is a two-way choice, not a three-way one, because the floor collapses half of it.** Measured, by rebuilding her on each grade: −1 gives **125**, −2 gives 124, and −4 gives **124 as well** — an unranked Power floors at 1 HP, so 3 − 4 clamps to the same figure 3 − 2 produces. The two harsher readings are indistinguishable in her total.

  **The direction is what makes this worth stating.** She is 1 HP *under* budget, so closing her means making her more expensive — every harsher reading of the Con moves away from 125 or, past the floor, does not move at all. The only grade that closes her is the mildest, which is the hardest to defend: the +6 Edge applies to one Power out of five, and her sheet prints `Edge 8/14` to show it. So this is not "we cannot tell which of three grades the authors used". It is: **either they read "only for Telekinesis" as barely limiting, or their total is 1 out.** This project takes the reading it can defend and lets the point stand, rather than treating 125 as something the authors cannot have got wrong.
- **Vigilant cannot be closed by his gear.** His Jo Sticks are *Upgraded*, worth +2, and he is 1 HP under: transcribing the feature moves him to +1 rather than to 0. Adding it would make the transcription more faithful and the residual no smaller, so it is left recorded rather than half-applied.
- **Herald (Scathach) at +1 and Shadow at +1** have no candidate in the data at all. (Airmid was at +2 when this was written and is closed — see below.) Scathach's Strike carrying four Pros and Cons at once remains the most likely place for a variant reading to be wrong.

**The book was then opened, and it settled two of the three questions above.** `docs/` holds both PDFs — they are gitignored, so they are in the main working directory and **not in a worktree's `docs/`**, which is how they were missed at first.

- **T-Kay's grade is a judgement call by the rulebook's own words — which is what made it the owner's to make; this bullet is the record of the argument as it stood before the ruling.** The Limited entry (Ch.2) reads: −1 "if the Power is somewhat limited", −2 "if it's significantly limited", −4 "if it's severely limited", and then *"Use this Con as a catch-all when nothing else seems appropriate."* There is no rule mapping "only for Telekinesis" onto a grade, so the milder reading has nothing recommending it except that it produces a zero. **It was left as recorded**, at −2, until the ruling, as the reading with an argument behind it: a flat +6 Edge that applies to one Power out of five is significantly limited by breadth. The counter-argument is practical — Telekinesis is her 12d signature Power and she uses it constantly, so the restriction rarely bites — and the rulebook is vague enough to hold both. What settles it in favour of leaving it alone is that the alternative is chosen *by its result*.
- **Vigilant's Upgraded is confirmed printed** — his Gear box reads `2 Jo Sticks: 10d (s) Melee (Upgraded)`, and he has Two-Fisted, so the pair is customised for one price of 2 HP. He is 1 HP under, so transcribing it lands him on +1. It closes nothing and is left recorded rather than half-applied.
- **Herald (Airmid) is closed.** The lead was her package: she was recorded on the Superhero Package while her sheet prints nine of twelve Talents at 2d, and a package's granted ranks are a floor. Following it found the actual fault — **her sheet prints two Expertise Powers, "Expertise (Medicine: Ancient Remedies) 12d" and "Expertise (Science: Botany) 12d", and only the first was transcribed.** Expertise costs half a Hero Point per rank and takes its baseline from the nominated Trait, so 12d over Science 2d is ten purchased ranks and **exactly 5 HP** — which is what the wrong package was absorbing. With the second Expertise transcribed and the package corrected to the one her printed Talents allow, she rebuilds to 125 to the point. Both halves are forced by the printed page.
- **Scathach's transcription is verified faithful to the printed sheet** — every Ability, all twelve Talents, all eleven Powers, both her Edge/Health/Resolve and her Determination, and all four modifiers on Strike. The rulebook gives Strike two different deflection Pros, `Deflect` (+4, physical *and* energy) and `Deflect Missiles` (+2, physical only); her sheet prints the plain one and the data uses +4, which is right. So her +1 is in the pricing model, not in the data — which is a narrowing rather than an answer.
- **Shadow** still has no candidate at all.

**A second, independent argument for every package attribution now exists**, and it is what caught Airmid. The inference had rested entirely on which package lands the rebuild on 125 — an argument from a total, and totals can agree for the wrong reasons. A package's granted ranks are a *floor*, so a package is impossible if the sheet prints a Trait below it, whatever the total says. `PrebuiltHeroTests.NoHeroPrintsATraitBelowWhatItsPackageGrants` checks all twenty against that, and it matters most for the five whose totals do not land on 125 — exactly where the totals argument is weakest.

**What reading the book did find is that every one of the twenty page citations was ten pages out.** Chapter 8 runs from printed 127 to 146 and the transcription recorded 137 to 156 — the offset applied twice. This is the error `CLAUDE.md` already warns about ("was ten pages out in the chapter it was offered for"); the note was corrected and the transcription was not, because nothing read those numbers. `PrebuiltHeroTests.EveryHeroIsCitedInsideChapterEight` now does.

**The per-element breakdown this item asked for has now been done, and it is a negative result.** Every cost element of all four was printed out beside its rulebook entry and checked against the page:

| Hero | Rebuild | What the breakdown found |
|---|---|---|
| T-Kay | 125 on the ruling (124 at −2) | Flight 1 HP/rank, Force Field 1 HP/rank, Telekinesis 2 HP/rank, Area +2, Zone +2, Overload +2, Determination 5 HP per Resolve — **every element as printed**. The −1 is entirely the `Limited` grade |
| Herald (Scathach) | 126 | Strike's four modifiers confirmed on the page: Deflect +4, Phase Shift +4, Reach/Throw +2, Item −1. Weakness Detection 3 HP flat. **No mispriced element** |
| Shadow | 126 | Preparation 6 HP flat (Ch.2 p.38) and Swing Line 1 HP per 2 ranks (p.44) both confirmed printed |
| Vigilant | 124 | the same two confirmed |

**So the method this item prescribed is now spent as well.** The residual is not a mispriced element in any of the four — which is a real narrowing, because it was the last cheap explanation. What is left is an interaction: a floor, a baseline or a grouping applied where the authors did something else. Nothing points at which, and two examples would not be evidence if they did.

**The interaction hypothesis has now been swept too, on 2026-09-05, and it is negative.** Six alternative readings of every floor, baseline, grouping and package-discount rule the engine could plausibly have got backwards — plus the two combinations that cancel each other's breakage — were hand-computed across all twenty Heroes in a scratch project that first reproduced the recorded figures exactly. Nothing closes any of the four without breaking one of the sixteen, or lands past 125 on the far side. **T-Kay and Herald (Scathach) carry no floor-bound, baseline-derived or grouped element anywhere in their build**, which rules this whole space out for them by construction rather than by trial and confirms T-Kay's −1 is the `Limited` grade alone. Shadow and Vigilant do have a real floor interaction — Swing Line and Wall Crawling's own 1-HP-per-2-ranks floor swallowing their Item Con, recorded above — but every reading of it moves them the wrong way or past 125, and the two readings that would matter most are contradicted by the book's own words: "always considered a single Power" and "regardless of its Cons". Not tried, because the four have nothing to vary: Two-Fisted gear pricing (no gear is transcribed for them) and Overkill/Weak (none carries either).

**Re-run independently, with a second instrument, and the negative result holds.** A scratch console project referencing `engine/` directly (not this test project) reconstructed all four Heroes plus three controls (Talon, Psidearm, Stronghold) and printed every Ability, Talent, Power, Perk and package line CostCalculator charges, computing each Power's cost by hand from its rate, baseline, Pros and Cons rather than only calling `PowerCost` — Strike's four modifiers on Scathach (Deflect +4, generic Phase Shift +4, Reach/Throw +2, Item −1), Invisibility's flat 9 with generic Item −1 and the Power's own Jamming −3 on Shadow, Danger Sense's `baseline_equal` on Perception, Armor's `baseline_half` on Toughness, Strike's `baseline_greater_of` Might/Martial Arts, T-Kay's Force Field and Telekinesis Pros (`zone_nova`, `area_burst`, `overload`) — all read straight from `data/rules/*.json` and all confirmed to the Hero Point. Every recomputed total matched the recorded residual and every recomputed Edge/Health/Resolve matched the printed sheet, for all seven Heroes. Mutating one Con (T-Kay's Limited grade to *somewhat limited*) correctly flipped her total to 125 — the instrument reacts to a real change rather than agreeing vacuously.

**One genuine narrowing came out of the line-by-line arithmetic that hadn't been stated before, and it rules out rather than explains.** Swing Line and Wall Crawling are both priced at 1 HP per 2 ranks — exactly the rulebook's own floor rate — so `Base` and `MinimumRankedCost` are numerically identical before any Con is applied, and the floor ("no Power can ever cost less... regardless of its Cons") then swallows the Item Con whole: Shadow's Swing Line and Wall Crawling, and Vigilant's Swing Line, all cost exactly what they would with no Con recorded at all. This is the rulebook's own stated rule working as intended, not a bug, and it is neutral — the same floor would have bound for the authors too, so it explains none of the four residuals. Recorded so the next attempt does not spend time re-deriving it.

**The other question a cheap instrument could finally answer: does any package other than the recorded "closest" one land any of the four on exactly 125?** No — swept across every package whose granted ranks the Hero's printed Traits do not fall below, none of the viable alternate (Hero, Package) pairings reaches 125. `PrebuiltHeroTests.NoOtherPackageLandsAnyOfTheThreeUnclosedHeroesOnExactly125` pins this now — it held four Heroes until T-Kay's ruling — with a positive control (the candidate list must be non-empty) and was watched to fail: deliberately asserting against T-Kay's real civilian-package total (127) rather than 125 turned three of the then-four theory cases red, then was reverted. A second guard, `PrebuiltHeroTests.TheExactHeroListNamesEveryHeroRecordedExact`, now holds the per-Hero exact list and the recorded residuals to naming every published Hero exactly once, because the list had been one short of the count it claimed for as long as both existed.

**One thing the pages did add, and it widens rather than closes.** Shadow's Gear box prints `2 Pistols: 9d Ranged (Silenced)`. Silenced is a Ch.6 custom feature at 1 HP, and the pair is one price under his Two-Fisted — so transcribed, Shadow is **+2**, not +1. The "nothing more than 1 HP out" bound above holds only because gear features are not modelled on these transcriptions. Recorded rather than half-applied, exactly as Vigilant's Upgraded Jo Sticks are.

**What the breakdown did find was two defects, and neither is a Hero Point.** Both made a character printed in the rulebook one this tool refuses. They were reachable only because nothing had ever asked the validator about the twenty; `EveryPublishedHeroIsALegalCharacter` now does.

Three rebuilds 1 HP out, each with a recorded reason — and one of them, Shadow, 1 HP further out than that once his printed gear is counted — remains a more honest state than three zeroes.

One thing genuinely cannot be modelled as things stand: Eidolon's `Omni-Power (Mind Link)` applies Telepathy's Pro to a *mimicked* Power. Pros are stored per Power, so there is nowhere for it to live. Eidolon reconciles anyway, so it costs nothing today.

### 1b. Semantic pro/con constraints are still unenforced

The invented per-Power lists are gone. What is left is the half of the constraints that cannot be checked against anything the rulebook prints per Power: "Powers that inflict physical or energy damage", "Powers that can be activated and deactivated at will", "attack Powers", "Powers that last or can be maintained". These are shown to the player as a caveat on the option and left to the GM, which is how Ch.2 frames the list.

Enforcing them would need roughly seven booleans on each of the 141 Powers — about a thousand fresh judgements against the book. That is worth doing only if something downstream actually needs it, and the obvious candidate was assisted creation, where a model proposing a character benefits from the engine ruling out illegal combinations.

**Assisted creation has now shipped without them, and did not need them.** A caveat is shown to whoever is proposing and left to the GM, which is what Ch.2 says it is. So this stays open with no consumer asking for it, and the caveat remains honest where the guess would not be.

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

**A second fault was masking this one and is fixed.** Every
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
section by section rather than chapter by chapter: all **1,525** sections (1,523 before the p.81 and p.107 titles were filed with their blocks) have a byte-identical word
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

**A printed page in the middle of a sheet is anonymous.** Much less pressing now the sheet is one page for an ordinary character, but a Powers-heavy one still runs over. The name is on page one and in a colophon on the last; every page between them relies on the browser's own print header, which the user can switch off — and unticking it is exactly what the review step now tells them to do, because that header is also where the web address comes from. `position: fixed` renders once at the top of page two in Chrome. **Closed for Chrome 2026-09-23**: Chrome has honoured `@page` margin boxes and `counter(page)`/`counter(pages)` since 131, so every page now prints the name and `page N of M` in its top margin — how it is wired, and why `--sheet-name` has no fallback, is in `docs/guide/printed-sheet.md`. **Firefox and Safari generate no margin boxes**; for them the document title and the colophon remain the answer, and nothing portable is left to try short of rebuilding the sheet as one table under a `<thead>`.

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

**The printed-page half above is closed for Chrome** (see its paragraph); Firefox and Safari keep the title-and-colophon fallback.

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
| 3, Action (p.67) | Challenge rolls, assisting, contests | No for creation — but **extracted as play data** under `data/rules/play/` for item 14, 2026-09-05 |
| 4, Combat (p.73) | Combat, stunts, minions, gritty rules | No for creation — but **extracted as play data** under `data/rules/play/combat.json` and `gritty.json` for item 14, 2026-09-06 |
| 5, Resolve and Adversity (p.83) | Earning and **spending** Resolve | No for creation — but **extracted as play data** under `data/rules/play/resolve.json` for item 14, 2026-09-05. The starting-Resolve *table* is this chapter's (p.83); Ch.2 p.60 adds Determination and the Flaw bonuses, and the engine implements all three |
| 6, Equipment (p.87) | Gear limits, armour, weapons, **custom gear (p.92)**, gadgets, vehicles, headquarters | Custom gear features: **extracted**. Mundane gear is free and untracked. See below for the one gap |
| 7, Environment (p.105) | Disasters, falling, lifting, **toxins (p.108)** | No — play. The three toxin Pros/Cons are extracted |
| 8, Friends and Foes (p.111) | **Three things, not one**: NPC and animal stat blocks (p.111), Extras (p.120), and the twenty pre-built Heroes and Villains (p.126) | Only the last is transcribed, in the test suite where they verify the engine. The other two are GM material — characters the GM fields, not ones a player builds — so they are out of scope rather than missing. Recorded because "Ch.8 is the pre-built characters" was wrong about 15 of its 56 pages |
| 9, Superhero Gaming (p.167) | Villain guidance, GM tips | No mechanics to extract — Ch.9 builds Villains by the Hero rules, which is why the mode is presentation only |

**Closed on 2026-09-08 by extraction, on the owner's instruction that every remaining chapter be surfaced as JSON, mechanically — see the pull request that carried it.** Three slices, each a verified file with a canonical transcription, a reflection walk, strict deserialisation coverage, the corpus as a second witness for every table, and an adversarial review that found real transcription defects in each: `data/rules/gear.json` (Ch.6 pp.88–93 — armour and its two features, shields, the eighteen-entry Weapon Features glossary, thirty-six mundane items under the rule that none is bought, Custom Gear, Pros and Cons on gear, and a copy of the three weapon tables held byte-equal to the play store's); `data/rules/gadgets.json`, `vehicles.json` and `headquarters.json` (Ch.6 pp.94–103 — a gadget *pays out* twice its Complexity in Hero Points; a vehicle is bought in Vehicle Points at 25 per Hero Point of the Perk, a headquarters in Base Points at 3, with six stock vehicles, fifty-four mundane rows, twenty-three vehicle features and twenty-two base features priced); and `data/rules/play/environment.json` (Ch.7 pp.105–109 — twenty-seven entries, ten tables, ninety-four rows, thirteen ambiguities recorded in the page's words, two of them later refuted by Chapter 2's own definition of a weight rank). None of the four creation-side files is on `RulesRepository.DataFileNames` yet — `RulesSourceTests` names each exemption and requires the file to exist and to be unloaded — because putting one there is a payload decision that belongs to the consumer slice, item 32. The reviews' findings worth keeping: the Submersible's printed 14 Vehicle Points is right once Radar's Sonar Con is read; Rappelling Gear's Easy (0) is the roll that uses it, not a break threshold; the armour Gear Limit is settled by p.87; p.7's "always round up" refutes every rounding ambiguity; and the credential scanner's token pattern matches a three-segment C# member path whose first segment is 24 characters or more, which `CanonicalEnvironmentRules` is.

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

**One driver, since 2026-09-30: `tests/e2e`, over `Microsoft.Playwright`.** There were two for a
month — `scripts/e2e/drive.mjs`, the original hand-rolled DevTools Protocol client, ran the first
five checks beside it — and the second was kept on probation until it had a record; the subsection
below says what the condition was and how it was met. The Playwright driver adds **+4 seconds** to
the runner's Restore step: `Channel = "chrome"` launches the Chrome already on the machine, so there
is no `playwright install`, nothing to cache, and no third renderer to invalidate the pixel goldens
against.

**What it does cost is a second drive, and for a month that drive ran last in one job**: 533s
before it, then 1103s and 1132s on two consecutive green runs, and a median 902s over the six
green `main` runs of 2026-09-23 to 09-29, of which the two drives were 627. Measured, not
projected: a projection from local timings said 13–14 minutes and was wrong. **The workflow is
now four jobs and the run's wall is about 4 minutes** — `publish` uploads the site once, the
two drivers each take it from there in a job of their own while `build` runs the suites, and
`scripts/e2e.sh` drives the twins four at a time instead of one after another. Measured on
PR #201's own runs: `build` 146s, the node drive 162s and the Playwright drive 215s,
every check the old job ran still running under the same command. The node drive and its job are
gone now, so the workflow is three jobs. **If it becomes tight again**, scanning fewer palettes in
A11Y is the lever left, and it costs real coverage. Raising `timeout-minutes` is not a lever; see
`docs/guide/hosting.md`.

**How it works, and every limit of it, is in [`docs/guide/testing.md`](docs/guide/testing.md)** —
read that before changing it. The account of building it, including five faults the harness found
in itself, is in [the archive](docs/progress/2026-09-02-driving-the-assembled-app.md).

**What that closes and what it leaves open**, against the table this entry was originally built
around:

| Was not exercised by anything | Now |
|---|---|
| Blazor WebAssembly actually booting | **Closed.** The framework starting, its payload arriving with bytes in it, and the boot screen being replaced by a rendered page are three separate assertions, and the real `_headers` is in force so a Content-Security-Policy that refuses one of the app's own scripts is a red check |
| Every `js/*.js` interop | **Closed for `ppStore` and `theme.js`.** The character-storage path is driven by clicking, and `ppThemeStats.stamps` is read across a genuine reload rather than a re-executed module. `motion.js` and `palette.js` are still only a proof harness and a bUnit recorder |
| Client-side routing | **Closed.** A `NavLink` is clicked, and a per-document token surviving the click is what proves the router handled it rather than the browser reloading |
| Pages Functions against the real edge | **Closed by stage two.** `wrangler pages dev` runs from the repository root with `functions/` bundled and a local D1 — `--d1 DB=<id> --persist-to .e2e/d1`, the id read out of `d1/wrangler.toml` and refused rather than guessed — and the six anonymous checks passed against it unchanged, because a real JSON `401` and a fallen-through `index.html` are the same "anonymous" to the app. One server configuration, cost 0 |
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

**A fourth gap, found while fixing a leak rather than by design: `kill_tree` itself was unproven — and is now proved directly, by `scripts/test-kill-tree.sh`**, which enumerates a real tree with `ps -eo pid,ppid`, states its positive control (three or more live processes and the port listening) and asserts every pid is gone after `stop_server`, across a synthetic three-deep tree, a real `wrangler pages dev`, and the `/proc` parser driven against a synthetic process table so the Linux arm is exercised on a Mac. It runs in `build.yml` before the e2e steps, and the port and process functions moved to `scripts/e2e/process.sh` so both can source them. Found on the way: `children_of` returned the exit status of the last `/proc` entry it read — invisible in a word list, fatal in `x="$(…)"`. The paragraphs below are the history that made it necessary.
`stop_server`'s Linux arm used `pkill -P`, which kills direct children only — wrangler's tree is
`npx` → node → `workerd`, so `workerd` outlived its step still holding a port. The fix walks
`/proc/<pid>/stat` recursively. The identical defect then reappeared on macOS, which has no
`/proc`, so `children_of` returned nothing there too, silently, and `kill_tree` again killed only
the `npx` wrapper — measured at six `wrangler`/`workerd` groups still listening on 8788–8793 after
a completed run, with the run still reporting PASS.

**macOS was fixed and Linux was not — and the first CI run of the direct proof (33949251306) said so in one line: *every pid in the tree is gone and port 8880 is still listening, so something outside the tree is holding it*. The cause, read out of miniflare's source rather than guessed: `workerd` is spawned with no `detached` and no new session, but its exit — `SIGKILL` included — is a crash to the supervisor, which respawns it; kill the supervisor next and the replacement reparents onto init, outside any parent-link walk. macOS won that race on window size alone. So `kill_tree` now freezes the whole tree with `SIGSTOP` before killing any of it, and `stop_server` finishes with a `/proc`-only port-holder lookup (`/proc/net/tcp{,6}` → inode → `/proc/*/fd`) that names and kills what escaped; `test-kill-tree.sh` has seven checks, two of them — `RESPAWNING_TREE` and `ORPHANED_LISTENER` — built to reproduce that run. The paragraph that follows was established by reading a CI log rather than by
reasoning.** The macOS half falls back to `pgrep -P` where there is no `/proc`, and a full local run
now leaves zero `workerd` processes and no held ports. The Linux half — `pkill -P` replaced with a
recursive walk of `/proc/<pid>/stat` — **does not work, and never did**: the runner emits
`something is still listening on port N after 30s of asking it not to` for **every twin, on every
run**. Twelve such warnings on `main` at `8f2add6` (run `33848074411`), eleven on the branch that
fixed macOS (`f0c77f2`, run `33899283677`) — so it predates that work and is not a regression from
it. The ports simply step upward, 8789 through 8799, as each abandoned server is stepped over.

**The reason nobody noticed is the reason this entry exists.** Both halves were verified by
*outcome* — "no leaked processes after a run" — and on Linux that check passes while the leak
continues, because `next_free_port` walks past the held port and never asks for it again. A green
run cannot distinguish "nothing leaked" from "something leaked and nothing looked", which makes the
outcome the wrong thing to measure. **Do not accept an outcome check as proof for this again.**

What is missing: start a server, call `stop_server`, and assert directly that nothing is listening
and no `workerd` process remains — on the Linux path (in a container, since the obvious
`bash -c '…' &` fixture collapses to one process: `exec` replaces it rather than forking a real
multi-process tree to kill) and on the macOS path. Only then diagnose why the `/proc` walk fails on
the runner; a second fix confirmed by the same blind outcome check would land exactly here again.
See
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

**Decided by the owner on 2026-09-06 — "make it look good; accessibility is not the goal" — and
changed.** `.btn.disabled` no longer fades the primary fill; it paints `--muted` on `--panel-sunk`
with a `--rule` border, the recessed secondary pairing `.btn.quiet` already uses one step further
sunk, so the control reads as present and not yet available rather than barely there. Measured
from the tokens: 6.01:1 Hero/Light, 7.26:1 Hero/Dark, 6.01:1 Villain/Light, 7.21:1 Villain/Dark,
held by `EveryScreenPairInUseHoldsItsContrastFloor`. Because that clears 4.5:1 outright, the `A11Y`
check's per-node exemption is **gone** rather than narrowed — the orchestrator's Playwright run
reports 560 passing rule instances and no violations with nothing exempt.

#### What had to be true before `scripts/e2e/drive.mjs` was deleted, and how it was met

**The hand-rolled driver was deleted on 2026-09-30, on the condition set here in advance rather
than on a feeling** — see the pull request that carried it. When this was written the hand-rolled
driver was green, twinned, and the one with the longer record, and the Playwright one was a week
old. A migration that removes the working harness before the replacement has a record is how an
upgrade becomes a regression, so both ran in `build.yml` until the count below was reached.

**The condition was: twenty consecutive green `Build` runs on `main` in which the `--driver dotnet`
step reported every check green against the real site and every twin red** — six of each when
this was written, nine of each since stage two added `ADMIN`, `RULES` and `ACCOUNT_SAVE`. Green is
enough because both drivers run in the same workflow, each as a job of it since PR #201 — either
going red fails the run — so twenty green runs is also twenty runs in which the two did not
disagree.

**The window closed at twenty on 2026-09-30, with run 36674185863.** The last red on `main` is run
34400118356 of 2026-09-09, which failed at the pixel comparison and not at either drive; every one
of the twenty `Build` runs since concluded green and its Playwright step printed `E2E: PASS — 9
checks green against the real site, and each one watched to fail`, read from each run's log rather
than from its tick, beside the node driver's `5 checks` line. One run inside the window,
36529480247, was cancelled by the concurrency group before it reached a drive; it is neither a
green nor a disagreement and was not counted. The recipe, kept because it is how the count was
made:

```bash
gh run list --repo SoftwareSamurai-net/ProwlersAndParagonsAutomation \
  --workflow build.yml --branch main --limit 30 \
  --json databaseId,conclusion,headSha --jq '.[] | "\(.databaseId) \(.conclusion) \(.headSha[0:8])"'
```

and, for each id back to the last `failure`, read the step's verdict out of the log —
`gh run view <id> --repo … --log | grep 'E2E: PASS'` — so the count is of runs that actually drove
it rather than of runs that skipped it.

**Two things that are not the condition, said because they are the tempting shortcuts.** "The
Playwright one is nicer" is not a reason to delete a working check. And "CI is slow" is a reason to
drop one driver from the workflow, which is a different and reversible change — the file can stay.

**What went, and what moved back.** `scripts/e2e/cdp.mjs` and `scripts/e2e/drive.mjs` went
together; `scripts/e2e/defects.mjs`, `seed.mjs` and `process.sh` stay, because the twins, the seed
and the process handling were never a driver's. `e2e.sh` lost its `--driver` flag and its
"skipped: this driver does not run" arms, and its twin/check comparison is equality in both
directions again — the orphan direction had moved to `E2eDriverTests` because one script running
one of two drivers could not tell "no driver has this check" from "not this one", and with one
driver running every check the two sets are the same claim. `E2eDriverTests` reads the one driver
now and refuses either deleted file back by name. `build.yml` is three jobs.

#### The argument this item was sharpened by, which is why stage one was worth more than it claimed

**A feature was built, tested, adversarially reviewed by two independent agents and shipped, while
nothing in the application ever wrote to the store it read from.** The manager's list, the banner's
switcher, `DiscardedCharacter` and both undo buffers all read `SavedCharacters`'s index; nothing
ever added a character to it.

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
3. drive Chrome to `/signin?t=<raw token>` (`worker/tokens.js`'s `signInLink`; this line said `?token=` until the driver was written).

The application then runs **its real verify path** — hash lookup, expiry test, single-use burn,
session cookie issued. Nothing is bypassed and nothing is faked but a row, which is what an email
would have caused. This is ordinary test-seeding, and it is strictly better than the seam: no code
in the shipped bundle, no secret, no localhost test, nothing to compile out, and no test needed to
assert the published bundle does not contain it.

**What it needs**, so nobody discovers it late: `functions/` has to be bundled, which means running
`wrangler pages dev` from the repository root and giving it a D1 binding — the one thing stage one
deliberately does not do, so this is a change to `scripts/e2e.sh`'s server setup and not only new
checks. **This entry said a migrated local D1 was `scripts/apply-migrations.sh`'s job already, and that was wrong**: all three of its wrangler calls carry `--remote` — it is the deploy gate. `e2e.sh` runs `wrangler d1 migrations apply --local` itself, at the version parsed from `deploy.yml`, and asks the database for its tables afterwards rather than believing the exit code.

**Stage two is built — see the pull request that closed it.** `scripts/e2e/seed.mjs` mints tokens, hashes them as `worker/crypto.js` does, and writes `login_tokens` and `invitations` rows in one `d1 execute --file`; the raw tokens reach the Playwright driver through `env` and the file holding them is removed when the run ends. Three checks, each with a twin: `ADMIN` — a signed-in non-administrator is refused by name after the page rendered and the banner names them; `RULES` — thirty passages with page citations to an account, and the rulebook prefix answers `401` to a context that never signed in; `ACCOUNT_SAVE` — a character written to the server comes back in a fresh context signed in as the same account, asserted before anything else. `defects.mjs` gains a second kind of defect, a seeded row (an administrator, an expired token, a second context as another account), stays the one place a negative control is declared, and now declares the *kind* of red each twin must produce — a `[HARNESS]` failure is a twin failing for the wrong reason and is reported as such. `ADMIN_EMAIL` is never bound, so `.dev.vars` cannot decide what the check measures. The rulebook's *content* assertions have no twin, and the guide says why: the corpus is baked into the worker and the gate is "somebody is signed in", so no bundle line and no seeded row can break one. The node driver reports five checks and names the four twins it skips. Job cost, measured piecewise: about +52s on the Playwright step and +8s on the node one, so 17½–20 minutes against the 30-minute cap; no lever pulled. Retirement count: **3 of 20**.
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

   **Closed, and this supersedes the paragraph above.** The shape the owner settled on is fork and pull request: a campaign holds a *clone* of a
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

  **The discoverability half is done.** The banner carries a `Search` button with the chord printed beside it, on every route, with the
  modifier chosen at render time from the platform.

  **The corpus is behind the control now — see the pull request that closed this item.** Signed
  in, three or more characters typed offer up to five of the book's own passages as a third group,
  "In the book", each with its `Ch.N p.NN` citation in the one spelling `/rules` uses; choosing one
  sends the question to `/rules`, which now takes it whether or not it is already the page on
  screen. Anonymous, the palette asks the server nothing and promises nothing. `palette.js` grew
  by **zero bytes** — `PaletteScriptTests` pins its digest — because the corpus grew the service
  and the component, which is where `docs/guide/browser.md` says the decisions live; the argument
  against growing the doorbell was honoured rather than overruled. A late answer never overwrites
  a newer query, rows are dropped the moment the box moves, and a `401` stops the offer instead of
  reading as a silent book. **And the banner field landed the same day, as the change of its own
  the previous sentence said it would be** — the `Search` button is a `type="search"` field now,
  eight characters wide, the chord still printed beside it from the same source, an `aria-label`
  that opens with the visible word and continues with the palette's own sentence (a placeholder is
  not a label). Focus opens nothing; the first keystroke, Enter, or a click on the empty field opens
  the palette with the text carried in, and keystrokes that land while focus is still crossing are
  forwarded rather than dropped. The field has no matcher and no corpus reader of its own. The
  offer of the book is settled before a carried word is asked, so a sign-in on `/account` with no
  reload cannot leave the word unanswered. `proof-align.html` measures seven items at a spread of
  0.00px against a twin at 2.00px, and — because the field is narrower than the button — the four
  shell goldens were regenerated by `visual-goldens.yml` and committed; the align proof does not see
  x-position, and the guide now says so. Nothing in `palette.js` changed by a byte. What no harness
  here can see is Safari's native search-field chrome, which is reset blind and recorded as such.

  **What was decided while closing the first half, so it does not get re-litigated:**

  - It was a **button and not a text box** while the palette searched Powers and step names only,
    because a box that looked like a search field would have been the wrong promise twice over;
    the rulebook moved behind it and the field replaced the button, exactly as this bullet said
    it would. The reversal is recorded in `docs/guide/browser.md` with the focus/keystroke decision.
  - The word is **Search**, and the palette still calls itself "Go to" inside. The label has to
    survive a glance in a strip of six controls; "Go to" between two underlined links read as a
    third link with no destination.
  - The modifier is answered by `ppPalette.onAMac` and worded by `Shortcuts.ReadModifier` — `Ctrl`
    or `Cmd`, never the looped-square glyph, which is in neither typeface this app names and would
    fall back to a system face on one platform only.
  - **A placeholder is not a label** still applies to the field when it arrives: it may carry the
    hint, and it may not be the only place the field is named.
- **Account and settings move to the right of the banner. — done.** The bar is two sides with a hairline between them, the tools cluster is one
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

  **The third avenue shipped in `1613c95`** — `Run` is the link, `BannerTests` pins three avenues
  — and this paragraph said it had not for three days afterwards, which is the drift item 23
  records. The campaign screens are what stand behind the door.

#### Two smaller things from the same reading

- **The Hero Point limit does not need a full-width panel for one button. Done.** Two cards as
  their own two-option group under a rule, not tiers 7 and 8, and the flipping label is gone. The
  argument for it is the rest of this bullet.
- **`/rules`' "What is here" panel is inert rather than pointless. Done.** The counts are gone and each row runs a search scoped to that chapter,
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

**Built 2026-09-08 from the kit the owner attached to issue #161 — see the pull request that carried it.** The HTML5 favicon pack (`favicon.svg`, `.ico`, 96px, the Apple touch icon, two manifest icons) is served from `web/wwwroot/` with `<link>`s in `index.html`, and `site.webmanifest` names the app, with `theme_color` the Hero palette's primary and `background_color` its surface. The mark in the banner is the eye cropped out of the kit's own `favicon.svg` — the first 81 of its paths on their own tile, derived from the shipped file so a clean checkout rebuilds it — placed inside `.banner-title` as an `<img>` with the vendor's name as its alt, so no component names a colour. The dark tile stays under the eye because 45 of those 81 paths are the eye's counters in that dark, and the brand red is invisible on the Villain banner's crimson without it. The review found both manifest icons declared maskable when the lockup bleeds to its edges (13% of the pixels fall outside the safe circle, and nothing was left for an unmasked draw), the publish-side image scan skipping a leading slash, the crop guard unable to tell the first 81 paths from the last, and a 320px measurement claimed for a harness that never takes one; each is fixed with a guard. Two choices are the owner's and stay open here rather than decided by an agent: whether the tile keeps its own dark or takes each banner's colour (the latter needs a second file — an inline SVG would be a component naming the brand's red); and which identity the installed app's chrome wears, since `theme_color` cannot follow the palette and a Villain player who installs the site gets a navy band above a crimson banner.

**Asked on 2026-09-06, the owner did not recognise the email comparison** ("I dont know what this means"), so that half is dropped until they raise it again. The kit is on their work PC; [issue #161](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/issues/161) is where they will attach it, and nothing here starts until it is there.


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
(engine and validator, eight). **They were worked concurrently on three branches and reconciled
afterwards**, which is why the three quoted a test count measured against their own branch rather
than against this tree; the count is not written down anywhere now — `./scripts/count-tests.sh`
is the answer. The merge touched only this file, `CLAUDE.md` and
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
npx wrangler pages deployment tail --project-name=prowlers-and-paragons-chargen --environment production
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

The adversarial half has run and been acted on: 126 mutations, 48 survivors, eleven streams —
`docs/notes/` carries the mutation tables.

**The first of the two remaining bullets — "is it snapshotable to a fresh AI agent?" — is closed
by the split that produced `docs/guide/`.** `CLAUDE.md` indexes one file per area under
`docs/guide/`, and `RepositoryGuideTests` holds its line budget. The second bullet — "is the
codebase as optimised as it should be?" — is now audited too; the short version:

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
  audit.** `PROGRESS.md` was 5,069 lines / 446,711 characters / 71,769 words when this was measured, before the archive split; it is 1,863 lines now — then roughly **90–110K
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

### 25. Visual regression testing — **closed, and then closed properly**

> **This entry was numbered 9 and is renumbered, because 9 was used twice.** The other 9 is
> durable telemetry, above. Two headings with one number is an ambiguous pointer in a file whose
> code comments cite items by number, and it is the concrete defect item 22 recorded. The number
> moved rather than the older entry's because nothing anywhere cites item 9; 25 is the next free
> number and is now taken, so a new item is 26. `ProgressPointerTests` fails the build on the next
> repeat.
>
> **Read this heading note second.** When this entry was written the check covered seven pages (eight goldens now);
> four were then dropped because a locally-rendered golden could not agree with CI's Chrome, and
> the entry below still describes the seven-page version. All seven are back, and the goldens now
> come from `.github/workflows/visual-goldens.yml` on `ubuntu-latest` — the same Chrome that
> compares them. The comparator itself also turned out to be unable to see a uniform whole-page
> colour shift, which is a hole this entry's confident tone did not anticipate. Both were fixed
> when they were found.

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
- **The goldens are Linux-rendered, never from a developer machine** (a Windows box when this was written; the Mac is the same story, since the pixel half needs Docker). On a Linux host (CI) the
  script drives the Chrome already on PATH; everywhere else it drives `selenium/standalone-chrome`
  in Docker — real Google Chrome, not a distro-patched Chromium, so a developer's own machine
  produces the same pixels CI would. The goldens committed here were generated exactly that way,
  from that Windows machine, through that Docker path — verified pixel-identical across two
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

**Planned, and the first of six slices has landed — see the pull request that carried it.** Slice (a) records Chapter 3 "Action" (pp.67–72) as verified data under `data/rules/play/` — `play_meta.json` for the dice model and `challenge.json` for the fifteen mechanics — each entry with `verified_fields`, a `source_ref`, an own-words description and, where the book is unclear, an `ambiguity` (twelve of twenty-one carry one; the sub-1d floor reached by several penalties, the odd pool for automatic successes, and what "extra" means in a group action are the three that change results). `CanonicalChallengeRules.cs` is the transcription; `PlayRulesDataTests` compares every fact field of every entry against it by a reflection walk, refuses rulebook prose in a description, requires every `source_ref` to be p.67–72 or the Glossary's p.7, and resolves the p.67 arm-wrestling example through the shipped JSON; Chapter 1's reprints of the bands, the thresholds and the success rule are read out of the corpus as a second witness. **The subdirectory is the placement decision**: every csproj copies `data\rules\*.json` non-recursively, `PlayPayloadTests` proves it four ways, and nothing outside the test project names the files — no engine reads them yet. `docs/guide/play-rules.md` is the guide.

**Slice (c) landed the same day** — Chapter 5 "Resolve and Adversity" (pp.83–85; the corpus carries no p.86) as `data/rules/play/resolve.json`, twenty-eight entries under `CanonicalResolveRules.cs` and the same reflection walk. Every spend carries an explicit `currency` and a `who`, and a test holds Resolve to the Hero and Adversity to the GM by the currency rather than by an id. The p.85 example (four Heroes, Challenge Level 2 → 8 Adversity) resolves through the JSON; the starting-Resolve table is read-compared against `DerivedStatsCalculator.CalculateResolve` at three ranks; the eleven Powers p.83 exempts are cross-checked against `powers.json`'s `affects_resolve` — ten as exemptions and the eleventh, Expertise, per nomination, because the page exempts it only "except for combat skills". **That carve-out was a recorded engine gap for a day and is fixed, on the owner's ruling of 2026-09-06 (book, then JSON, then engine):** a combat skill is an Expertise nominated to one of the four Abilities the Attack and Defense table on Ch.4 p.75 uses to attack or defend — Might, Agility, Toughness, Willpower — never a Talent (Scáthach prints Expertise (Academics: Strategy and Tactics) 12d with Resolve 5, which only holds if it does not count; the engine reproduces both that 5 and the 3 it would be), and never a Power (Ch.2 p.28: a specialisation falls under an Ability or Talent; `EXPERTISE_NOMINATION_NOT_A_TRAIT` reports one). `powers.json`'s `expertise` carries `affects_resolve_when_nominated`, `DerivedStatsCalculator.ResolveAffectedBySelection` reads it, and none of the twenty published Heroes or the twenty-eight Pinnacle City sheets moved. The book never defines the phrase — it occurs once in the extracted corpus — so the reading is the repository's, errs towards counting (less Resolve for a Hero), and leaves p.83's GM's-final-say as the release valve; `docs/guide/rules-engine.md` carries the derivation and the rival reading. Two figures that first arrived as facts were demoted on review — the par share rate (inferred from the printed 2-for-1, unprinted itself) and two Ch.4 initiative values that belong to slice (b).

**Slice (b) landed on 2026-09-06** — Chapter 4 "Combat" (pp.73–81; the corpus carries no p.82) as `combat.json` (fifty-one entries: the page, Edge order and its ties, seizing the initiative and the GM's alternative that slice (c) deferred here, multiple actions, ranges, throwing, movement, chases, the five-row attack and defence matrix, cover, size, visibility, damage, Health, healing, special effects, grappling, stunts, Threat ranks, Minions on both sides, and the nine special cases) and `gritty.json` (the ten Gritty settings and their preamble), under `CanonicalCombatRules.cs` and `CanonicalGrittyRules.cs` and the same reflection walk. Edge and Health are read-compared against `DerivedStatsCalculator`, with the expected value built from the file's own formula string. The book's worked fights are the fixtures — p.74's movement and chase, p.76's special effect and break-free, p.79's fatal damage, p.81's Example of Combat step by step — each resolving through the JSON. **The extraction defect was fixed rather than worked around**: `EXAMPLE OF COMBAT` was a full-width title set inside the left column, so the extractor filed it ahead of the sidebar's `WOUND PENALTIES` and merged the two; `PageReader` now defers a full-width block's title to the end of its band, which also repaired eight Ch.7 sections `SMASHING` had been qualifying since the extraction, and a second fix keeps a full-width block's paragraph breaks. All ten chapters regenerate byte-identical from the PDF (1,525 sections now, from 1,523), the bake in `worker/corpus.js` matches, and `docs/guide/rulebook-corpus.md` records the one case the deferral cannot decide. Review demoted two figures that had arrived as facts — Fatal Damage's rescue point, which the printed word puts *below* the threshold while the printed example puts it above (recorded as printed, the example's reading as an `interpretation`), and Slow Healing's lowest band, whose hourly rate the page never prints — and four hedges the first pass had flattened. Wound Penalties is gritty, which answers one of the questions below. **(d) landed on 2026-09-06 — see the pull request that carried it** — `play/ProwlersAndParagons.Play.csproj` beside `engine/`, referencing it and never the reverse (guarded at csproj and source level, and `PresentationFlagsTests`, the no-filesystem and no-network scans, and the `CostCalculator`/`CharacterValidator` ban all extend to it): `PlayRulesRepository` reads the five play files strictly, `IDiceSource` returns faces, `SuccessCounter` holds the whole dice model with no literal in it, `Combatant` is immutable and only a Hero can hold Resolve, `CombatantFactory.From` is the one place a sheet is read and the caller supplies `Kind` and `Side`, and `Encounter.Step` is pure over an immutable state with a ledger that names the rule and its page on every line. Every intent flag is applied or refused on the ledger; every table switch is applied or announced at `Begin` as not yet; `docs/guide/play-engine.md` tabulates the seventeen readings the data could not answer and two guards hold its not-yet lists to the code. The book's eight worked examples replay through the data — p.67, p.74 twice, p.76 twice, p.79, the whole of p.81, p.85 — and a broken twin substitutes one line of a copy of `combat.json` (the special effect's rounding), throwing unless it matches exactly once, and requires both p.76 replays to say `FAIL` while the other six pass. The adversarial review found eleven defects, seven of them ledger lines announcing an effect the state never received (defeat by special effect, the dying clock, area defence halving, all-out's guard clause, charge's impact, Tough Minions' count, a grab stored as a hold); all are fixed with a fixture each, and the round dropped the engine's one silent design reading — sides are a field now, so Heroes can fight Heroes as p.73 says they may. Still not modelled, and listed as such: keeping hold, knockback, luring, team attacks, three of the four Adversity spends, the cover/size/visibility modifiers, the item a full grab wins, and five Gritty switches (Close Range, The Drop, Friendly Fire, Hard Targets, Slow Healing, the raised Gear Limit). Verified by the orchestrator: the p.76 replays go red when the rounding is hard-coded, and the defeat-by-effect line goes red when its state change is removed. **(e) landed on 2026-09-06 — see the pull request that carried it** — `mcp-play/ProwlersAndParagons.McpPlay.csproj`, wire name `prowlers-and-paragons-play`, registered in `.mcp.json` beside the first server and run the same way (`dotnet exec` on a published dll, now under `mcp-play-server/`); `RulesLocation` and `CommandLine` moved by rename into `mcp-shared/` so neither server references the other. Four tools: `combat_guide` serves `mcp-play/PLAY-POLICY.md` verbatim, whose rule is the mirror of the creation policy — the engine resolves, the model narrates — and whose not-yet lists are held to the engine's by a test; `start_encounter` takes character JSON through the strict reader (a refused sheet, an unknown tier, a fight with one side, a Minion group of none — each a refusal by name, never a crash); `take_turn` takes one intent and answers the ledger lines it added and the public state, one fight's turns serialised behind that fight's own gate; `run_encounters` runs N seeded fights under a named policy and answers win rates, pages and what was spent **only in the same object as its N, its seeds, its policy and the whole table echoed**, refusing fewer than thirty or more than five thousand. Standard output carries the protocol and nothing else, held by the same source scan and driven runtime scan the first server has, over both launch paths; every problem code the server can emit is driven by one case and the set is pinned against the source. A thousand seeded runs of the p.81 fight cost under three seconds cold. Verified by the orchestrator: a one-run "measurement" and a non-blocking gate each go red; (f) the first balance measurement, reported only with its N, seed, policy and table settings. **The first balance measurement — run 2026-09-07, corrected 2026-09-08, and its figures are the second set below.** The entry's own question, "whether a Standard-tier Lynchpin is survivable for four 100-point Heroes", answered with the party the data has: the four Low Level Pinnacle City Heroes (Cho-won Jung, Dr. Felix Crane, Eleanor Greer, Emir Hughes) on one side, one Villain on the other, through `run_encounters` on the published server over stdio exactly as `.mcp.json` launches it. Every run: N = 1000, seeds 20260908 to 20261907 consecutively (the echo's `seeds` block), policy `attack_the_weakest`, targeting `weakest`, page cap 20, Challenge Level 0, opening range close, visibility clear, the table from the call because no sheet carried one, every switch echoed, `not_yet_applied` empty. **The first run's figures were wrong, and the first report of the styles slice is what showed it**: the attack-form derivation took "every Trait p.75's table does not name" off a dictionary that holds Talents and passive Powers, so Eleanor Greer fought Schism with Academics and Schism answered with Armor. The derivation now reads p.75's attacking Abilities and Chapter 2's `Attack` category (fifteen Powers; the three that carry only an `attack` tag — Spinning, Polymorph, Elemental Control — attack with the Ability the table gives everybody until the owner wants `engine/` to widen the reading), and the runs were repeated on the same seeds:

| fight | table | heroes win | villain wins | draws | mean pages | Resolve spent (heroes, mean) | Adversity spent |
|---|---|---|---|---|---|---|---|
| four vs **Lynchpin** (Standard) | owner's: `gm_alternative_to_seizing_initiative` on, no Gritty | **1.000** | 0.000 | 0 | 2.50 | 1.47 | 0 |
| four vs **Schism** (Standard) | owner's | **0.351** | 0.649 | 0 | 5.94 | 1.18 | 0 |
| four vs **Lynchpin** | the book's baseline (alternative off) | 1.000 | 0.000 | 0 | 2.50 | 1.47 | 0 |

Attack forms as fought: Cho-won Jung Blast, the other three Agility, Lynchpin Agility, Schism Shockwave. Per character against Schism: defeat rate Cho-won Jung 0.666, Dr. Felix Crane 0.85, Eleanor Greer 0.971, Emir Hughes 0.649, Schism 0.351; against Lynchpin only Eleanor Greer ever falls (0.069). (The withdrawn first set, for the record and nothing else: Lynchpin 0.997 in 2.28 pages, Schism 0.485 in 5.53.) **The owner's house rule still moved nothing** — runs 1 and 3 are identical — and the echo says why: `mean_adversity_spent 0`; this policy never buys a seize. The styles built on 2026-09-08 do, and the matrix below is the same party against Schism under each, 100 fights a cell, base seed 20260908, targeting `weakest`, the owner's table, `unfair` at the owner's line of half or worse:

| matchup | mano a mano | standard | min-max | reckless |
|---|---|---|---|---|
| the party | 0.28 unfair | 0.19 unfair | 0.47 unfair | 0.66 |
| Cho-won Jung alone | 0.02 unfair | 0.08 unfair | 0.04 unfair | 0.09 unfair |
| Dr. Felix Crane alone | 0.00 unfair | 0.00 unfair | 0.00 unfair | 0.00 unfair |
| Eleanor Greer alone | 0.00 unfair | 0.00 unfair | 0.00 unfair | 0.00 unfair |
| Emir Hughes alone | 0.00 unfair | 0.00 unfair | 0.01 unfair | 0.00 unfair |

Two thousand fights in a quarter of a second. What the table surfaces and the tool does not conclude: this party is on the wrong side of the owner's line against Schism under three styles of four, `standard` is worse for them than fighting on the sheets alone because Schism out-Edges everyone and the GM's pool answers more often than four Heroes' Resolve does, and no Hero survives Schism alone under any style. Verified by the orchestrator: every figure above was produced by the orchestrator's own driver, and the Schism run reads 0.491 on seed 1 under the old reading and moves again under the new.

**Eight questions for the owner before (d):** the dice model as printed (recommend yes); Monte Carlo rather than expected value (recommend yes — Resolve is spent after the roll and 6s explode); **where the twenty-eight Pinnacle City sheets are — answered: in the production D1 under the tabletop account, and one `wrangler --cwd d1 d1 execute prowlers-and-paragons --remote --json` query pulls them; on 2026-09-06 all twenty-eight read and priced legal through `build --from-dir` in one process, into the gitignored `characters/pinnacle-city/`, so (f) is not blocked**; the default table (recommend the book's baseline, every gritty rule off); what decides an NPC's action (an `IPolicy`, named in every report); whether `play/` may read `IsVillain` (recommend no — the caller sets `Combatant.Kind`); whether Wound Penalties is core or gritty; and whether `docs/RULEBOOK-COVERAGE.md`'s Ch.3–5 rows move (slice (a) moved Ch.3's).

### 15. The Trait Cap is the tier's, and a campaign may want a tighter one

`CharacterValidator` takes the Trait Cap from the chosen tier — 12d at Standard, 8d at Street Level.
**A campaign can impose a tighter ceiling that no tier expresses.** The Pinnacle City setting caps a
non-superhuman NPC at **6d — peak human, with 3d an average adult** — which is a house rule the tool
cannot see, so a sheet breaking it still validates `ok: true`.

Until this item closed it was audited by hand — three times across twenty-eight sheets in one
session, and it held every time, which was a property of that session and not of the tool.

**The obvious shape is a `--trait-cap` override on `build`, or a field on the character file,
reported under its own issue code so the repair is mechanical.** One thing to settle before
building it, because it is not cosmetic: **the Trait Cap is also what Resolve is computed from** —
`DerivedStatsCalculator` takes it off the gap between the cap and the highest relevant rank. So an
override changes derived stats, and whether a *house* cap should move Resolve, or only gate
validation while the tier's cap keeps doing the arithmetic, is the actual design question. The flag
is the easy half.

**Answered by the owner, 2026-09-05: the house cap moves Resolve. Substitute it, do not merely gate
on it.** The rules tie the two together, and the trade is the player's to make — spend the room
under the cap on power, or leave it unspent and take the Resolve.

The arithmetic is why there was no honest alternative. `DerivedStatsCalculator.CalculateResolve`
computes `baseResolve = max(0, (traitCap - highestRelevantRank) * 2)`, so **the cap *is* the datum
Resolve is measured from**. Gate on a house cap of 6d while the tier's 12d keeps doing the
arithmetic, and a character sitting at 4d is paid `(12-4)x2 = 16` Resolve for a restraint the
campaign imposed on them rather than one they chose; substituting gives `(6-4)x2 = 4`, and staying
low becomes a decision with a price. So the substitution happens at the one read of
`tier.TraitCapRank` in that method.

**Two consequences to carry into the slice, neither a blocker.** A tighter cap lowers the Resolve
*ceiling* too — 24 at a 12d cap, 12 at 6d — which is what "tied to" means in the other direction
and will read as a nerf the first time somebody sees it. And it is **noise on a Villain**: only
Heroes have Resolve, so on the NPC sheets that surfaced this the house cap is doing validation work
and the Resolve half is a figure nobody should quote. Both halves still land; only one is visible
per kind of character.

**Built — see the pull request that closed this item.** `CharacterSheet.TraitCapRank` is the cap a
character is built to, null for the tier's, and `DerivedStatsCalculator.EffectiveTraitCap` is the
one answer every reader takes — Resolve, the validator, the budget strip, the printed sheet's meta
line, the Resolve breakdown, the replay verdict, the terminal wizard, `build`, the MCP report and
the JSON export's top-level `trait_cap`; `TraitCapReadTests` scans for any surface still reading
the tier's. A cap above the tier's is `TRAIT_CAP_ABOVE_TIER`, below 1d `TRAIT_CAP_BELOW_MINIMUM`,
and both are used as written. `build --trait-cap N` overrides every character of a run and is never
written back; reports carry `trait_cap` beside `tier_trait_cap`. Joining a campaign copies its cap
into an empty field, says exactly what it took, and `CampaignJoin.Inspect` — which nothing in the
application had ever called — is now drawn on `/campaign`, so a character that disagrees with its
game is told. The GM's approval diff gains a `Trait Cap 12d → 6d` row, and a rank row under a cap
tighter than its package floor no longer throws. `StoredCharacter.CurrentVersion` is untouched.
Verified by the orchestrator: a 7d Intellect at Standard reports Resolve 10, and 0 under
`--trait-cap 6` with `TRAIT_ABOVE_CAP`; `CalculateResolve` mutated back to the tier's cap went red
on `AHouseTraitCapMovesResolve`.

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

**The browser half of this finding is closed.**
The same twenty-eight NPCs are what broke the character manager, and a roster page that can be
filtered, grouped by game and read at a glance is what came of it. **Nothing above is affected**:
every bullet here is about `cli/` and `sheets/` — one file per `--from`, timestamped export names,
no cross-sheet question — and the browser cannot answer any of them.

**All four bullets are closed — see the pull request that closed this item.** `--from` repeats and
`--from-dir` takes every `*.json` in a directory (case-insensitively on every platform); one input
keeps today's report byte for byte, more than one is still exactly one JSON document — `characters[]`
with an absolute `source` each, `exit_code` the worst of them, an unreadable file one exit-2 report
inside the list rather than the end of the run. `--overwrite` names exports after the character
alone; two characters sharing a safe name each get a numbered pair **and** an `EXPORT_NAME_COLLISION`
warning, and the same run twice replaces the same files. `roster.spending` is `CostCalculator`'s own
six addends and the Perks that make up one of them, `roster.perks_by_id` counts holders and units
and prices nothing, and `--traits-above N` lists every Trait over the rank with the Trait a Power's
baseline is read from (`DerivedStatsCalculator.BaselineTraitIds`, a new pure method) — a Power the
engine cannot rank is a row with `rank: null`, not an absence. `--no-build` is in `--help`, the
skill, `CLAUDE.md`, `README.md` and the guide, with tests pinning the flag table in both directions.
**Not built: the monotonic-ladder question**, which is a question about variants of one character
and waits on item 21. Verified by the orchestrator: three fixtures driven for real, exit 2 on the
broken one; two colliding names under `--overwrite` gave two pairs on disk, twice; the roster exit
code mutated to "last character's" went red on three theory cases.

### 21. Variants of one character are a naming convention doing a structure's job

**Answered 2026-09-10: build it. Slice one landed 2026-09-11.** What landed: `CharacterSheet.Variant`,
null on every older sheet so every older export is byte-identical, three kinds as string constants
(`later`, `as_seen_by`, `alternate_form`), two validator codes for a malformed link
(`VARIANT_WITHOUT_ROOT`, `UNKNOWN_VARIANT_KIND`) and one browser-side finding for a root this
account does not hold (`VARIANT_ROOT_NOT_HELD`, a Warning like `UNKNOWN_CAMPAIGN`); the roster's
index gains `variant_of` and `variant_kind` (migration `0010`, stored and listed by a server that
still parses no payload); `CharacterVariants.Group` draws the family over item 12's grouping
component on the roster and in the banner's switcher; "Version of…" on a roster row makes the link
and refuses a self-link or a cycle. **Two limits, stated:** the tree is one level deep — siblings
of one root, which is every example the owner gave; a version of a version draws as its own
orphan-shaped row and a test pins that — and **the rules engine is blind to the link by a guard**,
so an alternate form is costed exactly as any character until slice two decides how Chapter 2's
shared Resolve and paid-power-level budget reach a sheet.

**The question that was open — Alternate Form — was answered by taking the recommendation**: it is a
kind of variant now. Slice two gave it its rules — see the list entry and the "Alternate forms: the one rule that needs two sheets" section of `docs/guide/rules-engine.md`, where the one deferred sentence of p.21 is recorded.

- **Open: should the Alternate Form Power use the same mechanism?** The owner raised it as a
  question, not a ruling — they have not read that Power. Chapter 2's `alternate_form` says the
  form is *"built as a separate character with its own Hero Point budget"*, both forms pay for the
  Power, and they share one Resolve pool. Today nothing in `engine/`, `sheets/`, `web/` or `cli/`
  reads that Power beyond pricing it — a player builds two sheets and puts the relationship in the
  names, which is the same naming convention this entry is about. If it is folded in, the
  relationship has a third kind beside *later version* and *as another audience sees them*, and
  that kind carries rules the other two do not (shared Resolve, budget set by the paid power level).
  Decide before designing, because a link that carries rules and a link that carries none are
  different shapes.
- **It must fit every character already in the app.** No migration that asks the owner to rebuild
  a roster; existing sheets stay valid with no relationship, and one is attached afterwards. That
  rules out making the link a required field, and it rules out anything that changes an existing
  export's bytes when no relationship has been declared.

The paragraph that follows, and the recommendation it carried, is kept as the reasoning the answer
was given against; the "wait and see" half of it is superseded.

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
about what a character is. It has never had one. **The tier column arrived
that way already**, on 2026-09-01: `d1/migrations/0008_character_index_fields.sql` adds `kind`,
`tier_id` and `spent`, every one written by the client, and `putCharacter` binds all three without
the server parsing a word. That question is settled — do not re-open it as part of this item.

**Built — see the pull request that closed this item.** `GET /api/admin/accounts` lists the players
in the caller's own campaigns — never the caller, never every account — with what each holds and
their cap; `PUT /api/admin/accounts/{email}/character-limit` sets it in one scoped `UPDATE …
RETURNING` (0–500, and `AccountsContractTests` pins the razor's `max` to the server's constant);
`GET …/{email}/characters` answers `label`, `updatedAt` and the three client-written index columns
and never the payload. The key is the address, which the administrator already reads off the
invitation list one panel up, so it is not a new disclosure; `routePattern` files the whole prefix
as one row. A failed read says the players could not be read rather than that nobody has joined,
Save is dead until the box holds a different whole number, a draft in one row survives saving
another, and a slow answer for one player's sheets cannot land under another's name. Verified by
the orchestrator: the admin gate, the self-cap exclusion on the `UPDATE`, the conditional
`aria-controls` and the sheets in-flight guard each went red under mutation.

### 20. xunit.v3 4.0.0 is a test-platform migration, and it is measured but not done

**Done 2026-09-11.** The recipe below was re-measured and held: xunit.v3 4.0.0, `global.json` opts
the SDK into Microsoft.Testing.Platform, `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`
are gone. `count-tests.sh` runs each .NET project on its own because MTP prints one combined
`total:` for a solution run. **One trap found on the way and written into the script and the guide:
`--nologo` and `-v q` are forwarded to the MTP test host, which reports "Zero tests ran" and exits
without running anything** — a green-looking no-op of exactly the shape this repository's guard
faults take. Do not pass them to `dotnet test` any more. The crash trap the guide warned about is
closed for real: a deliberate stack overflow prints `Zero tests ran`, `error: 1` and a non-zero exit,
not `Passed!`. The paragraphs below are kept as the record of what the migration was measured
against.

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

### 22. The Current state table is where this file actually conflicts

**Measured rather than assumed, on 2026-09-05.** Of the 201 commits that have touched this file,
**122 touch the 23-line `Current state` table** — 61% of all churn on the file, concentrated in
about 1.5% of its lines. The `Remaining work` preamble, which looked like the obvious culprit,
accounts for 15.

```bash
git log --oneline -L 11,33:PROGRESS.md | grep -c '^[0-9a-f]\{7\} '   # the table
git log --oneline -L 34,86:PROGRESS.md | grep -c '^[0-9a-f]\{7\} '   # the preamble
```

**That matters because it says what the fix is not.** Splitting `Remaining work` into one file per
item — the move that worked for the completed-work archive — would be a large restructure aimed at
the 7%, and it would cost the property that makes the section work: a reader needs every open item
*in one place* to notice that two of them collide, which is exactly what the paragraph above the
list warns about. Nobody ever needed to read all the completed slices together, which is why the
same fix does not transfer. It would also sit badly against `CLAUDE.md`'s "do not reintroduce a
second list".

**The fix is the one the Tests row already demonstrates.** That row used to carry five figures in
prose, went wrong four separate ways, and now names `./scripts/count-tests.sh` instead. The rule it
implies: **a cell whose content is a measured figure should name the command that measures it,
rather than quoting the answer.** `Powers: 141 entries` is fine — it moves when the data moves, and
that is the point. These are not:

- **Static analysis** — a Qodana count that has been 2, 5, 2, 3, 37 and 23. The cell already
  contains the instruction *"Do not name this commit's own sha here"*, because a sha put there went
  stale within the hour.
- **Hosting** and **Accounts** — live deployment and migration state, which changes when somebody
  deploys rather than when somebody edits this file. Both carry long narrative reconstructions of
  which migration was applied when.

**What guards it.** A test over the `Current state` table that fails on a cell carrying a bare
measured figure with no command beside it that would reproduce it, with a positive control so a
scan matching no cells fails rather than passing vacuously — the shape `RepositoryGuideTests` and
`ProgressArchiveTests` already use. Break it by pasting a Qodana count back into the Static
analysis cell and watching it go red.

**Churn is not the same as conflict, and both were measured, against different questions.** The
figures above count *how often a region changes*. A separate experiment on 2026-09-05 ran real
three-way merges in a throwaway clone and found that **two branches editing two different `###`
items merge clean today** — even adjacent ones, even when one deletes its whole block. What
conflicted there were two shared anchors: the triage preamble that used to name every open item in
wrapped prose, so any two closures collided inside one bullet, and the `## Completed work`
boundary that every new item is appended at.

**The preamble half is fixed** — it is now the one-line-per-item checklist above, so two closures
touch two different lines. The append boundary is not, and this entry sits on it. Neither
experiment tested the case the churn figures point at, which is two branches both editing the
`Current state` table; at 61% of all commits that is the likely collision rather than a
hypothetical one, and it stays the substance of this item.

**One concrete defect found alongside, and it is not contention: `### 9.` is used twice** — line
1091 (durable telemetry, deferred) and line 1205 (visual regression, closed). Two headings with one
number means an ambiguous anchor, and this file is full of anchor links. There are also references
of the form `PROGRESS.md item N` in the code — 21 of them under `--include='*.cs' --include='*.razor'
--include='*.js' --include='*.sh'` — with nothing guarding that any of them resolves. A dead pointer
is worse than no pointer; the guard this item proposes should cover them too.

**What moved, and it is four cells.** Each keeps the one sentence that says what the row *is* and
gives up the figure:

- **Tests** — already the model, and it had one measured figure left: the e2e driver's check
  counts, which item 23's audit had *already* caught being wrong once. Gone; the row names
  `docs/guide/testing.md`, where that account is kept up, and says the figure has been wrong here
  before.
- **Hosting** — the long reconstruction of which migration was applied on which run is gone. What
  the deploy last did moves when somebody deploys, not when somebody edits this file. The
  workflow's own runs are the record; the mechanism, the two failures that built it and what each
  token permission was proved by are in `docs/guide/hosting.md`, which already carried all of it.
- **Accounts** — the migration count and the applied/pending state are gone, replaced by the two
  commands that answer them. The cell had **seven** where there are eight, which is one of the ten
  drifts listed in item 23 and the clearest possible demonstration of the rule.
- **Static analysis** — the Qodana figure and the two commit shas are gone. The two-readings-in-one-
  day account moved into `docs/guide/testing.md` beside the two rots already recorded there, so
  that file now carries all four; the cell keeps the standing instruction not to name a sha, which
  is the one thing in it that was never a measurement.

**What stayed, and why.** `Powers: 141 entries`, the 106 Pros/Cons, the twelve gear features —
these move when the *data* moves, which is the point of the row and the whole reason the table is
here. `Rulebook coverage`, `Front ends`, `Wizard`, `Printed sheet`, `Licence` and `Known-wrong
data` are decisions or descriptions of shape, not readings: nothing about them is measured
somewhere else and transcribed here. `Zero warnings at CI strictness` stayed too, and the
distinction is worth naming — it is a standard the build enforces on every run, not a figure
somebody took once, and the difference is whether anything fails when it stops being true.

**The guard is `ProgressCurrentStateTests`**, and it refuses three shapes in that table: a counted
figure over tests, migrations, sections or checks; a commit sha; and a state described as pending.
It carries a positive control on the Tests row's pointer — the table has to be there, the `Tests`
row has to name `./scripts/count-tests.sh`, and that script has to exist — so a scan that matched
no cells fails rather than passing vacuously; and controls in both directions on the patterns
themselves, which are watched to fire on the four cell texts that produced this item and *not* to
fire on the four figures the table is meant to keep. **What it cannot do** is bounded by the same
thing every scan here is: it matches shapes, so a count spelled a way it does not know, or a stale
state described without a number, walks straight through. Nothing checks a cell against reality —
that is what the pointer in the cell is for, and why the cell has to carry one.

**The `### 9.` half is done**: the closed one is now 25, and `ProgressPointerTests` fails the build
on the next repeated number. **The 21 `PROGRESS.md item N` comments in the source are not**, and
that is deliberate rather than overlooked — resolving one means reading an item to see whether the
comment still means it, which is the judgement half of item 23's audit and not something a scan
settles. What the number guard buys is that none of the 21 can become ambiguous again.

**Done on 2026-09-06, last as planned** — four rows (Tests, Hosting, Accounts, Static analysis) gave up their figures for pointers, the rest stayed because they are decisions rather than readings, and `ProgressCurrentStateTests` refuses a counted figure, a sha or a pending state in the table with the Tests row's pointer as its positive control; verified by the orchestrator, who watched a restored Qodana count and a restored sha each go red. Nothing was broken; this was contention, and it only bit
when several branches were open at once. The cheaper half of the answer is a process rule rather
than a restructure and is already in `CLAUDE.md`: the orchestrator writes this file, not the agents.

### 23. This file's own claims went stale in sixteen places

**Found by auditing it against the code on 2026-09-05, entry by entry** — all nine `Current state`
rows and all 24 anchors below. The audit exists because a stale claim here is inherited by every
agent at once: `PROGRESS.md` is the first thing `CLAUDE.md` sends anybody to, and one of these
findings had already sent a reader to build something that shipped three days earlier.

**Five were fixed on the spot** because they would have misdirected somebody immediately: the
headline "a visitor with no account can watch a real conversation build one" (the replay has been
`/admin/portfolio/replay` inside `<AdminOnly>` since `9e65abc`), item 19's "if a tier column is
wanted later" (it shipped as `0008_character_index_fields.sql`), the Tests row claiming six e2e
checks "through either of two drivers" when the default `node` driver has five, `count-tests.sh`
being unable to run three of the five suites, and the maintenance step that still demanded a
slice write-up `CLAUDE.md` forbids.

**What is left is a sweep, which is why it is an item rather than a footnote:**

- **Twenty-one dead pointers.** Sixteen bare `see [the archive](docs/progress/)` links and five
  `see the completed entry at the top of this file` references. `88e8a1b` deleted the 6,723-line
  file holding all 92 pre-split entries and never repointed this file; `## Completed work` is a
  pointer rather than a place. The four *named* archive links still resolve. This is the repository's
  own "a dead pointer is worse than no pointer" standard, failing in its index. The fix is
  per-site judgement — inline the sentence that mattered, or drop the pointer and let the claim
  stand alone — not a find-and-replace.
- **Ten smaller factual drifts**, each with evidence in the audit: seven D1 migrations recorded
  where there are eight; Chapter 9 named "Creating Villains" when the book calls it "Superhero
  Gaming"; the visual check covering eight pages and recorded as seven; a `wrangler pages … tail`
  command naming `prowlers-and-paragons` when the project is `prowlers-and-paragons-chargen`, which
  a reader would copy and watch fail; "this Windows machine" describing a Mac; `CLAUDE.md` measured
  at 290 lines; item 7's token side recorded as costed-but-unimplemented when `acbc6a6` implemented
  it, still quoting this file at 5,069 lines against an actual 1,528.
- **`### 9.` is used twice** — see item 22, which carries this along with the 21 code comments
  referencing `PROGRESS.md item N` that nothing guards.

**The guard is the point, not the sweep.** Fixing sixteen claims once buys nothing durable; this
file has gone stale before and will again. `RepositoryGuideTests` already proves every path
`CLAUDE.md` names exists — the same shape applied here would hold every file path, every migration
number and every `docs/progress/` link in this file to resolving. Claims about *behaviour* cannot
be guarded that way and will still need an audit; say so rather than implying the test covers them.

**The guard is `ProgressPointerTests`, and it is six checks over this file's own text.**

- **Every markdown link resolves** — a path link to a path that is there, an anchor to a heading
  in this file. GitHub's slug rule is spelled out rather than approximated and pinned against
  three anchors that work today, because an em dash is dropped like any other punctuation and the
  spaces either side of it survive: half the anchors here carry a doubled hyphen for that reason,
  and a slug function that tidied hyphen runs would call every one of them dead.
- **Every `docs/progress/` link names an entry rather than the directory.** This is the check a
  path-existence test could never have made, and it is the audit's largest finding: the directory
  exists, so all sixteen bare links resolved perfectly while pointing at nothing in particular.
  The one exception is the signpost under `## Completed work`, identified by where it is rather
  than how it is written, since naming the directory is that pointer's whole job.
- **Nothing names `the completed entry` without saying which file.** There were six, not five —
  the sixth was wrapped across two lines, which is why a line-oriented grep had found three.
- **Every test this file names in backticks exists** in one of the two `dotnet test` projects,
  with a control on the search itself: a name that has never existed must come back missing.
- **The file names no commit sha except the ones an allow-list carries**, each with the claim it
  is for — and every sha on that list must still be in the file, so the list cannot become the
  next place things rot.
- **No two entries share an item number**, which is the `### 9.` defect generalised. GitHub's
  anchors hid it: two headings with different titles make different slugs, so every *link*
  resolved and only the *number* was ambiguous — and the number is what prose and the 21 code
  comments citing this file use.

**What it cannot do, which is the half worth reading.** Every check above is about a pointer, so
**a claim with no link is invisible to all of them.** The ten drifts listed above are exactly that
shape — seven migrations recorded where there are eight, a chapter named wrong, a page count one
out, `CLAUDE.md` measured at 290 lines — and not one of them names a file, an anchor or a test.
No scan of this file's text has an opinion about any of them; they still need somebody to read it
against the code. What the guard buys is that the *mechanical* half never needs auditing again, so
the audit that is left is the half that actually needed judgement in the first place. Item 22's
`ProgressCurrentStateTests` takes the second bite from the other side, refusing the *shapes* of
figure that go stale in the one table that holds most of them — the seven-migrations drift above is
in a cell it now covers. `docs/guide/testing.md` carries both, and says why the sha check is an
allow-list rather than `git cat-file`: CI checks out at depth 1, so a reachability test would fail
the build on facts that are true.

**Twenty-two dead pointers were fixed rather than twenty-one**, per-site as this entry asked and
not by find-and-replace: repointed where a surviving entry carries the argument — the e2e harness
account is `docs/progress/2026-09-02-driving-the-assembled-app.md` — and otherwise dropped, so the
claim stands on its own. The second `### 9.` is now 25.

**Done on 2026-09-06.** `ProgressPointerTests` holds every link and anchor to resolving (GitHub's slug rule written out and pinned against live anchors), every `docs/progress/` link to naming an entry rather than the directory, every backticked `…Tests[.Method]` to existing, every sha to an allow-list, and every item number to being used once; what it cannot see is a claim with no link, and the twenty-one `PROGRESS.md item N` comments in code are judgement rather than a scan and were left. Verified by the orchestrator, who broke an anchor and watched `EveryLinkResolves` name the line.

### 24. A bUnit event is dispatched, not applied, and three palette tests read a render early

**Three CI runs went red on one class, one test at a time, and each was fixed alone — which was the
wrong shape of fix, and this entry exists so the class-wide one is not undone.** bUnit's synchronous
`Input()`, `Click()` and `KeyDown()` post the event and return; only the `…Async` forms come back
once the render they caused has finished. While the renderer is idle the post runs inline and the
difference never shows. The command palette is the one place here where the renderer is *not* idle:
the book's answer lands on a thread-pool continuation and the redraw it raises is queued through
`InvokeAsync`, so a test that dispatched a keystroke and read the DOM on the next line was reading the
markup from before the keystroke — on a slow enough machine. That is a fact about the test harness
and about machine speed, not about Linux and not about the product: every one of the three was traced
to the drive, and the product's own behaviour was proved right by mutation each time.

It slipped in because it passed on the Mac, three times, and it stayed on CI for the better part of a
day because each fix converted the one test that had just failed. **The fix now is the class**:
every drive in `PaletteBookTests`, `BannerTests` and `CommandPaletteTests` that is followed by a read
uses the awaited form, the sensitive reads run under a deliberately busy renderer
(`BusyRenderer`, with an elapsed-time positive control) so the losing order is exercised on every
machine on every run, and `PaletteDispatchTests` reads the three files' source and fails the build on
the next synchronous drive — its doc comment says what a denylist cannot do. Runs `0361de7`,
`621939f` and the sweep are the history; `docs/guide/testing.md` carries the rule.

### 26. A campaign submission carried an empty sheet under a real character's label

**Found by the owner on the GM's own screen, with screenshots, on 2026-09-06**: opening the approved
sheet of a player's character showed an unnamed, empty sheet — every Trait 0d, the campaign's tier
and 10d house cap, Resolve 20 — while the admin panel showed that player holding two fully statted
characters. The production row confirmed it: the approved clone was 379 bytes, exactly the envelope
plus what a join copies in, beside two full clones of 2,248 and 3,567.

**The path, reproduced before it was fixed.** Sign-in and the boot restore both empty the session
with `StartAgain` when the account's character cannot be read, and neither moves the
current-character pointer — correctly, the character is still there. From that state the campaigns
page sent `Session.Sheet` under the pointer's character: a join wrote the campaign's tier onto the
empty sheet, which made it worth keeping, and the autosave then **wrote the empty sheet over the
stored character**; a submit sent it under the row's label; a later re-join restored the label over
the emptiness. `pending_version` 1 and the byte count agree with that sequence exactly.

**What holds now.** A campaign act — join or submit — requires the session to hold the character the
pointer names: an emptied session re-adopts the stored character first, or refuses with a sentence
naming it, and `CampaignJoin.Apply` never runs on a sheet the session did not load for that id.
Label and payload come from one read. A sheet with literally nothing on it — no Ability rank, Talent,
Power, Perk, Flaw, Gear or Name, the tier alone does not count — is refused with a sentence under the
row's own button, and a character made of Powers alone is sent, because refusing it would be
repairing. The autosave refuses to write an empty sheet over an id the account holds a priced
character under, and says so in the save region. The GM's screen and both lists say when a clone or
a waiting snapshot is empty, and Approve stays the GM's. Twenty-four driven tests in
`CampaignSubmissionTests`, each through the page under a real store, asserting on what the server
received. Verified by the orchestrator: the join's re-adopt and the autosave's refusal each went red
under mutation. **Remedy for the live row**: the player resubmits and the GM approves; nothing in the
database is edited by hand.

**Left open here and closed by item 27**: the root invariant — that the session holds the character
the pointer names — is still enforced at the campaigns page and not at sign-in, so a stale pointer
after a failed read is still a state the app can be in; saying so on screen, or re-adopting there, is
a slice of its own.

### 27. The session must hold the character the pointer names

**Found by the review of item 26, and deliberately left open there.** `SignIn.razor` and `Program.cs`
both do `if (await Store.LoadAsync() is { } theirs) Session.Open(…); else Session.StartAgain();` —
so a read that fails (a `404`, a timeout, the site's own `index.html` answering a `200`) empties the
session **without moving the current-character pointer**. The pointer still names the stored
character; the session holds nothing. Item 26 stopped the campaigns page acting on that split
(joining re-adopts or refuses; submitting reads the row's character by id; the autosave will not
write an empty sheet over a stored full one), which closes the one path that reached somebody
else's database. The state itself remains: a signed-in reader whose character could not be read is
looking at an empty builder that the app believes is their character, and the next edit autosaves
under that id.

**What a slice would do.** Either re-adopt on the next successful read (a retry that lands moves
the sheet into the session and says so in `.save-status`), or say on screen that the character
could not be loaded and offer the manager — never leave the two disagreeing in silence. A test
drives the failed read through `SignIn.razor` (not by calling `StartAgain` directly, which is how
item 26's first reproduction missed the join), asserts the split, and asserts it is reported or
healed. `docs/guide/browser.md`'s "Keeping a character while starting another" carries the ordering
rules the fix has to keep.

**Not a defect a player has hit yet as far as the record shows**; recorded so the next campaign-page
change does not rediscover the state it stands on.

**Built 2026-09-07 — see the pull request that carried it.** The fact is recorded by the read and not by a caller: `ApiCharacterStore.UnreadId` is set inside the no-argument `LoadAsync()` when the pointer's character is `Unreachable`, cleared by any successful read of that id or by the pointer moving, and never set for an id this browser minted. The autosave's `WouldWriteOverACharacterNothingRead` sits beside item 26's `WouldEmptyACharacter` and answers the question that one stops asking the moment somebody types a name; `StartAnotherAsync` asks the same question and refuses whole, with a fourth sentence under the button. `MainLayout`'s `.save-status` says the character could not be loaded, offers a retry that re-adopts and says so, and links the manager; it is gated on being signed in and follows the pointer. The review found the Start-another hole, the pointer-scope defect and a false notice on a new account booting offline, and closed all three. `SessionHoldsThePointerTests` drives every path through `SignIn.razor` or a mirror of `Program.cs`'s boot lines, and a source-reading guard holds that mirror to the three lines it copies.

### 28. Two flakes on a docs-only pull request, and what the harness said about them

**Pull request #160 changed `PROGRESS.md` and nothing else, and its build failed twice for two
different reasons, neither of them in its diff.** A third was seen the same day on a laptop under
load. Each is a class, not an instance, and each was swept as one — `docs/guide/testing.md` records
the mechanism of each beside the bUnit dispatch trap.

- **The e2e server died mid-drive and the harness said nothing about why.** Run 34040527190's
  Playwright step: BOOT passed, A11Y waited 45 s for `/build` to render, and every check after it
  reported `net::ERR_CONNECTION_REFUSED` as if it were its own finding — three of them printing a
  raw seeded sign-in token in the URL into a public log. `e2e.sh` printed the driver's verdicts and
  never the server's log tail, and never said whether `server_pid` was still alive. Now
  `capture_server_state` runs before `stop_server` on every failure arm and says alive-or-dead, exit
  status and whether the port is bound, quoting the redacted log only when the server is gone; both
  drivers probe the server once, report `[HARNESS] the server stopped answering` as one named kind,
  print `NOT RUN` for the rest and count only what ran; verdicts are redacted and one line. The cause
  itself was not found — once in sixty runs, a listener gone and staying gone rather than a `workerd`
  respawn — and the two things that would name it next time, the wrapper's exit status and the log's
  last lines, are exactly what is printed now.
- **The in-process MCP server's teardown raced its own read loop.** The rerun failed
  `McpPlayServerTests.EveryProblemCodeIsDrivenOverTheWire` with *Reading is not allowed after reader
  was completed*: both server test classes disposed the transport, which completes the `PipeReader`
  under a live `ReadLineAsync`, and the SDK (`ModelContextProtocol.Core` 2.2.0) propagates anything
  but cancellation out of `RunAsync`. EOF is a clean end in that SDK, so the fix is ordering rather
  than a catch: one shared `InProcessMcpServer` completes the client's writer, waits a bounded time
  for the run to end on its own, and only then disposes; every teardown step is folded into one
  verdict so a harness fault can neither mask a body's failure nor be swallowed by a passing one.
- **A secret scan's five-second regex timeout fired under load — and the load was not the cause.**
  `AccountsContractTests.NoKeyOrTokenIsInTheRepository` threw `RegexMatchTimeoutException` at a load
  average of 176, but the pattern was quadratic in any unbroken run of token characters: a 128 KB
  blob times it out on an idle laptop, and `worker/corpus.js` is 722 KB. Every source-scanning regex
  in both test projects now goes through one `ScanRegex.Build`, which asks for the linear engine and
  refuses to fall back silently; the scan itself covers the whole account server, the Razor tree,
  `scripts/` and `tests/` rather than two directories partially, with planted fixtures per
  alternative so a pattern that matches nothing cannot pass, and the one place the two engines
  disagree (`Group.Captures` on a quantified group, read by nobody) is pinned. The review replaced a
  wall-clock tripwire that was itself a flake at 6.6× margin with a control whose only clock is on
  the side that must fail.

**The second death came on 2026-09-07, and this time the guard caught its shape.** Run
34120157313 on `main`, the Playwright twin `html-lang-dropped`: exit status 1, an empty `✘ [ERROR]`
on stdout, and wrangler pointing at its own debug log under `$HOME` on a runner that no longer
existed. Read out of the pinned 4.127.0's source: the logger writes every level to that file
unconditionally and the error handler sends the message to stdout and the stack to the file — so
the empty line was the message and the cause was in the file nobody had. Every e2e server now
writes its debug log under `.e2e/logs/wrangler/<name>/` through `WRANGLER_LOG_PATH`, a dead server's
report leads with the error block and the `Logs were written to` path, then the newest debug log's
tail, then the request tail, all redacted, and a failed drive uploads the whole directory as a CI
artifact. A ninth kill-tree case holds it, watched red eleven ways. Two facts came out of measuring
rather than reasoning: the pinned wrangler logs a request's pathname and drops its query, so no
server log ever carried a sign-in token and only the planted-token test proves the redactor; and
the artifact step is a workflow, which nothing here can execute — it is proved by the next failing
run, not by this entry. The rerun passed, twice in ~75 runs is the rate, and wrangler 4.128 and
4.129's notes name no relevant fix; the deploy's pin stays.

**What the reviews added, because the first fixes were themselves checks nobody had broken.** The
orchestrator's inverted-aliveness mutation *hung* the kill-tree suite for eleven minutes instead of
turning it red — `wait` on a live child blocks — so `capture_server_state` is bounded and the suite
proves a lying aliveness test fails within seconds. A server that answers by hanging (dead `workerd`
under live wrangler, the documented death mode) was being classified as the check's own failure, and
crashed the Node driver outright; both drivers now bound the probe and call it stopped. A passing
run whose server died after the last check said nothing; it warns. The new verdict lines leaked a
token through the half of the URL that was not wrapped; redaction moved to one choke point per
driver, proved with a planted token. `test-kill-tree.sh` started a hundred marker processes with no
trap and left them all on SIGTERM. Everything above was watched red before it was believed.

### 29. A campaign's table rules, and its Immortality price

**Asked for by the owner on 2026-09-07**: the table's optional rules are per campaign, set as toggles
when a campaign is made, visible to players and GM alike — and a number where the book prints a
range. `powers.json`'s Immortality is the one such Power: 3 HP flat, "in a game where Heroes can
die, GMs should charge more — somewhere between 6 and 12", now carried as `campaign_cost_min` and
`campaign_cost_max` on the entry with a test holding the prose to the figures.

**Built the way the house Trait Cap is (item 15) — see the pull request that carried it.**
`engine/CampaignTable` is one boolean per play table setting, named exactly as `play/`'s
`TableRules` names its switches, plus the Gear Limit rank; a source-reading guard holds the two
name sets together in both directions, since `engine/` may not reference `play/`.
`Campaign.ImmortalityCost` and the table are copied onto the sheet on join and never refreshed
behind anybody's back; `CostCalculator` prices Immortality from the sheet, a price outside the
book's range or on a sheet in no campaign is reported and never repaired, and every screen prices
through the same read, held by `HousePriceReadTests`. Both exports carry the rules — the `.json`
one as `campaign_table` and `immortality_cost`, always written and `null` at no table — and the
stored payload spells them `CampaignTable` and `ImmortalityCost`, which is what the strict reader
and so the encounter server take. The twenty published Heroes moved by exactly the Immortality
figure for the one who carries it (Nano) and by nothing otherwise; all twenty-eight Pinnacle City
sheets re-priced identically.

**The review found** a player shown `UNKNOWN_CAMPAIGN` above their own live game (the server scopes
a campaign's payload to its owner, so a member resolves no campaign), a house-price exemption list
whose count was never checked, a join that took a changed price without saying so, and the Con on
Immortality untested; each is fixed with a fixture. What it left open is item 30.

### 30. A member sees the copy their character carries, not the campaign's live table

**Because the server scopes a campaign's payload to the GM's account, a player's browser cannot read
the campaign at all.** So the read-only list under a member's game is drawn from the character's
own copy, labelled as such, and a setting the GM changes later arrives only when the character
joins again. In the one direction nothing reports: a character that joined before the GM decided
anything carries nothing, and `CampaignJoin.Inspect` — item 15's condition, unchanged — compares
only where both sides have set something, so that character is priced at the book's 3 while the
table charges 12 and no panel says so. Recorded in `docs/guide/browser.md`.

**What a slice would do.** A player-scoped, table-only projection — a route answering the
campaign's `Table` and `ImmortalityCost` and nothing else, authorised by the member's own
`campaign_members` row rather than by ownership; the member's list then shows the live table
beside the copy, and `Inspect` can report the empty-copy direction too. It costs a route, a read
in `worker/db.js` carrying the same player predicate `getMembership` uses, a `KNOWN_ROUTES` entry
and a contract test. Nothing about the character's copy changes: the copy is what is in force for
the character until they join again, and that stays the rule.

**Built 2026-09-07 — see the pull request that carried it.** `GET /api/memberships/{id}/table`
answers `{campaignId, payload}` — the campaign's payload verbatim, the same bytes a join already
hands that player and a strict subset of the join's answer, held so by a test — authorised by the
caller's own `campaign_members` row joined to the campaign under its GM, and `404` in one sentence
for a stranger, the GM of that very row, an id that never existed and a deleted campaign, with the
bodies and the statement count proved indistinguishable. The server parses nothing: lifting the
four fields would have taught it a campaign's shape and failed silently the first time the shape
moved. The member's panel shows the live table beside the copy, says which is which and when the
live one was read, offers a recheck, and draws a row per difference in both directions;
`CampaignJoin.Inspect` reports a character that joined before the game set a cap, a price or a
switch under `CAMPAIGN_HOUSE_RULES_NOT_COPIED`, carrying each figure only where it is the one
missing. The review found the two-GMs-one-code fixture reading an insertion order rather than the
join clause, the live read picking any membership the account holds rather than the character's,
the cap left out of the finding, and no contract guard for a routed address.

### 31. The account autosave lost an edit to its own predecessor

**Found by the e2e harness on 2026-09-07, on a pull request that changed one test file.**
`ACCOUNT_SAVE` opened a second browser as the same account and read the character's name as
"Account Bound H" while the first browser had typed "Account Bound Hero" and said it was saved.
Item 28's new server-state line said the server was alive and bound, so this was the check's own
finding; the rerun passed, so it was a race. Three lines of code made it a real one: `Program.cs`
started one fire-and-forget write per change with nothing ordering it against the one before;
`ApiCharacterStore.SaveAsync` serialised eagerly and carried no version; `worker/characters.js`
wrote unconditionally. An earlier PUT landing after a later one lost the keystrokes between them,
and "Saved" reported the version of whichever write returned last. `docs/guide/browser.md` had
already called the palette's search race "the autosave's defect in a new place".

**Fixed — see the pull request that carried it.** `web/Services/Autosave.cs` is one write open at
a time, the latest edit coalesced into the next, the announced version read immediately before the
write so "Saved" can only ever understate what landed; a write that throws re-pumps a pending edit
before the throw goes where it went, bounded by edits made rather than failures suffered.
`AutosaveOrderTests` reproduces the sighting to the character by holding the first PUT at the wire.
Client serialisation rather than a server version, deliberately: both writes come from one tab and
the newer is the keeper, `CharacterSession.Version` restarts per page load so a server refusing an
older number would refuse a second tab wholesale, and `409` on that route already means the account
is full. The e2e check now waits for the name it typed rather than for any write. The review found
the throw path dropping the coalesced edit, the app's own `Start()` unguarded (deleting it left
every test green while the deployed site wrote nothing — item 10's shape, now a source-reading
guard), and pinned the announced-version order, the mid-write character switch and item 26's
refusals on the coalesced trip.

**Left open, named rather than hidden.** Two tabs editing one character still race at the server,
which is a different defect needing the server-side version this entry argues against in its
current form. And a keystroke landing inside the one JS-interop hop between the pump capturing the
sheet and the store resolving the pointer could in principle write one character's bytes under
another's id; it predates this fix, is unreachable under bUnit's synchronous storage, and closing it
means carrying the captured id through `ICharacterStore` — a design change nobody should land
without a harness that can watch it fail.

### 32. Fold Chapter 6 into the sheet and the fight

**The owner's ask, 2026-09-08**: "I want the game mechanically." Item 3 put the data in; this item
makes it do something. In order of what unblocks the most:

1. **Load and offer.** `gear.json`, `gadgets.json`, `vehicles.json` and `headquarters.json` go on
   `RulesRepository.DataFileNames` with a collection each and a `RulesFileCoverageTests` row, and
   come off `RulesSourceTests`' exemption list — the guard fails if only one happens. The payload
   grows by roughly the four files; item 5 records that as a characteristic. The palette then
   offers the new entries the way it offers Powers — armour, weapons, vehicle and base features,
   environment tables — filtered in the browser, not over the network.
2. **The Gear step picks from the catalogue.** A weapon row brings its bonus and features, an
   armour row grants the Armor Power at Toughness plus its bonus under the Gear Limit (p.87 settles
   that), a shield its +1d; mundane gear stays free and untracked, and `SelectedGear` keeps its
   shape for that. The Item Con question has to be answered first: `GearCost`'s comment says it is
   not credited, `cons.json` prices it at −1 so a host that records it gets the discount, and no
   Pro or Con in the data is marked applicable to gear at all though p.93 names twenty-four.
3. **Vehicles and headquarters on the sheet.** Two new collections beside `Gear`, each holding the
   Perk's Hero Points, the derived second-currency budget, the characteristics (a vehicle's four
   ranks, all bought from zero — `SelectedGear`'s free baseline is the wrong precedent) and the
   selected features with a count or a grade; `CostCalculator` gains four totals, three of them not
   in Hero Points and none folded into `TotalCost()`; the validator reports a budget or a constraint
   (Mecha's Might at least half its Body) and never repairs; the `.txt` and `.json` exports print
   them. **Two questions to settle before code**: both pages let Heroes *pool* points into one
   object and a `CharacterSheet` is one character — pooling is either unrepresentable or
   double-counted across sheets; and Training Facilities' Teamwork behaves like Resolve, which only
   Heroes hold, while p.100 gives headquarters to Villains too — a presentation decision of the
   same kind as Resolve on a Villain.
4. **Gadgets** are a pool that pays out, spent through the ordinary cost rules with the Item Con
   applied and not credited — a fifth calculation that runs the other way.
5. **The fight reads Chapter 7.** `modifier_cover`'s Structure from `smashing_table` or
   `scenery_table`; knockback's solid object likewise; throwing's weight rank from
   `massive_objects_table` now that Ch.2 p.17 settles which column it is. A vehicle is mundane gear
   under the Gear Limit (`vehicular_gear_limit`), so the fight reuses the cap it has.

**The owner answered the design questions on 2026-09-09, and they bind steps 3–5**: a shared
vehicle or headquarters lives *on the campaign*, funded by its members — each sheet records the
Hero Points it put in and the campaign sums the budget, so a `CharacterSheet` holds a contribution
and never the pooled object; a Villain's Teamwork from Training Facilities is treated exactly as
Resolve is — computed, never quoted, never spent; the Gear Limit's default is honoured always and a
raised one follows Immortality's pattern on the campaign.

**Steps 1 and 2 landed for `gear.json` on 2026-09-10, and the Item Con question is answered in
code.** The file is on `DataFileNames` and off the exemption list, which `RulesSourceTests`,
`RulesFileCoverageTests`, `RulesLoadingTests` and `EquipmentDataTests` hold together — removing it
from either side alone fails five tests. `GearCatalogue` offers every row (armour, shields, the
weapon-table copy, the thirty-six mundane items); the Gear step in the browser and the terminal
picks from it; the ⌘-K palette's "Gear from the book" group offers rows under its own cap of eight
so a word that matches eight Powers cannot push every weapon off the bottom; `OptionList.Query`
seeds the filter the palette lands on, and a reader already on the Gear step is filtered too, which
the first cut missed in exactly the way `/rules` once did. **The Item Con is not credited**:
`GearCost` skips the Con `gear.json` names as `item_con_id`, because p.93 says every piece of gear
carries it and then leaves it out of the Cons it lists as commonly applied — the Con still prints,
and a Power's own Item Con is untouched. **p.93's twenty-four generic Pros and Cons** are marked
`applicable_to: gear` and asserted by id in both directions, since "Area of Effect" is a printed
name two Pros share. **A worn suit's Armor rank** is p.87's order — the wearer's Toughness or their
own Armor Power, whichever is higher, capped at the effective Gear Limit, *then* the suit's bonus —
and `DerivedStatsCalculator.ArmorFromGear` prints it beside the suit rather than buying a Power;
two suits grant the better one. An unknown catalogue id is `UNKNOWN_GEAR_CATALOGUE_ROW`, an Error,
kept rather than dropped, and it no longer silences the Hero Point budget check — the review found
that a misspelled id had bought exactly that silence. The review also found the crowbar's +4 and the
climbing claws' +2 printed bare on every surface when p.91 qualifies both in the same sentence;
`BonusAppliesTo` is now read by all of them. The payload grew by 13 KiB over the wire — measured,
and recorded in item 5's terms rather than as a figure here.

**One question the review raised is left open for the owner, and the code takes reading (a)
meanwhile**: a wearer whose own Armor Power exceeds the Gear Limit gets *less* from a suit than they
have without it — Armor 12d in Plate is `min(12, 6) + 2 = 8d`, and the sheet prints 8 beside a Power
of 12. Reading (a), as built: p.87's cap binds a substituted Power rank too, so a superhuman does
not benefit from mundane armour. Reading (b): p.88 *grants* a rank and never removes one, so the
figure floors at the wearer's own Power. The interpretation is recorded on `gear.json`'s armour
entry, and `ArmourRankTests` pins the 8 so the choice is deliberate and visible.

**Step 5 landed on 2026-09-10: the fight reads Chapter 7.** An attack can name a piece of scenery
for its cover (`cover_scenery`) and the Structure comes off p.107's materials or p.108's scenery
table rather than the caller's word — a row *and* a stated figure together are refused, because
p.107 lets a GM thicken a wall and the caller who has done so states the number. A knockback can
name what the target flies into (`solid_object`, on the Resolve spend and the Adversity one): p.78's
half the original blow, rounded up by p.7, unless the target's best *passive* defence exceeds the
Structure, in which case they smash through unharmed — a tie belongs to the object, and the fixture
now stands on that tie because the orchestrator's `>=` mutation walked straight through the one that
only straddled it. A Massive Objects row has a weight rank and no Structure, so naming one for
either is refused with nothing spent. A thrown or swung object is priced off p.108 with p.87's Gear
Limit on the Trait and p.108's row-rank-plus-six ceiling on the sum, both applied because the page
does not say which replaces which; a massive object's throw reaches as far as p.74's table says off
its weight rank and no further; an object breaks apart after one shot. Chapter 6 is asked before
Chapter 7 when a name matches both. `EntriesNotYetApplied` stays empty. The review found fourteen
things, the shape of most being a claim nothing measured: the Adversity knockback's object was read
by nothing end to end, the published policy did not tie a row's name to its rank, the ledger offered
a knockback sixteen rows it would then refuse, the swung-versus-thrown halves of p.108 were
indistinguishable, and p.80's stray round dropping the obstacle was unasserted in both fields.
`vehicular_gear_limit` is not read and does not need to be — it sits in the character rules, and
a fight caps a named vehicle by p.87 exactly as it caps a sword; `play-engine.md` records that.

**Steps 1, 3 and 4 landed on 2026-09-10: vehicles, headquarters and Gadgets on the sheet.**
`gadgets.json`, `vehicles.json` and `headquarters.json` are on `DataFileNames` and off the
exemption list — five guards fail if only one happens — and `AssetCatalogue` offers their rows; the
palette offers vehicle and base features under a cap of their own, and `Commands.Steps` is now
held to the pages it names in both directions, which nothing did before a seventh entry was added
to it. A sheet owns `Vehicles` (the Perk's Hero Points, Body, Speed, Control and a nullable Weapons
— an unarmed machine prints the page's em dash, not a rank of nothing — all bought from zero, and
features with a count or a grade), `Headquarters`, `Gadgets` (Complexity, Powers, Ability and
Talent ranks), and `CampaignAssets` — a contribution of Hero Points to a named object the campaign
holds, which is the sheet-side half of the owner's pooling answer. `CostCalculator` prices a vehicle
at 25 Vehicle Points per Hero Point of the Perk (p.96), a base at 3 Base Points (p.100), Control at
two a rank and the rest at one with negative Control refunding to a floor of −3, and a Gadget's
pool at twice its Complexity (p.94) spent through the ordinary Power rules with the Item Con
applied and not credited; only the Perks and the contributions reach `TotalCost()`. The six stock
vehicles come out at their printed totals through two independent derivations, the Submersible's
14 via Radar's Sonar Con. The validator reports a budget overspent in any of the three currencies,
Mecha's Might below half its Body, Control above half its Speed — rounded up, as p.7 rounds every
half, which is what makes three of p.97's own machines legal — a Complexity under 3, the 6d
Technology prerequisite, an unknown feature, grade, Power, Ability or Talent id, and the same Perk
recorded twice as a Warning. Training Facilities grant one point of Teamwork however many bases
carry them, and on a Villain it is treated exactly as Resolve is — computed and never quoted, the
owner's ruling, with a guard on the guide that records it. Both exports print the block and four
arrays plus `derived.teamwork`; the browser's seventh step, the terminal's `ChooseAssetsStep`, the
MCP server's and the headless build's spending breakdowns all carry it; the printed one-page sheet
deliberately does not. The review's findings worth keeping: a Gadget's Powers were walked by nothing,
so a misspelled Con inside one took `Validate` out with an exception and a negative unit count priced
a 12d Nullify inside a Complexity-3 pool with no finding at all; two fixtures straddled their
boundary rather than standing on it; both exports threw over mistakes the validator already
reports; a vehicle feature's printed prerequisite (Submersible needs Swimming, p.100) is prose
nothing checks, pinned by a test that fails the day it is. The payload grew by 20 KiB over the wire,
measured.

**Two questions the review left for the owner**: whether `vehicles.json` should gain a structured
prerequisite beside the prose so Submersible → Swimming and Transforming's two-of-four become
checkable; and whether two copies of the same flat feature on one vehicle (two Sensors for 20
Vehicle Points) is a thing to refuse, where p.96 says "any number of features" and prints no rule
either way.

**The campaign-side half landed on 2026-09-10, and item 32 is closed.** A shared vehicle or base
lives inside the campaign's own opaque payload — `Campaign.Assets`, absent when the game owns
nothing so an older payload round-trips byte-identically — and the server never learns one exists:
no route, no column, no parse, and a worker test that writes the table handler as a field-by-field
projection of the seven older fields and requires exactly one test to go red. The GM writes an
object on the campaign's page (name, kind, the four characteristics, features; renaming keeps the
id, so nobody's contribution is orphaned; removal leaves every contribution where it is and says
so). A member reads the game's objects through item 30's scoped live route, not the copy on the
sheet, and the Vehicles and bases step offers each by name, copying id and kind onto the
contribution. The GM's page opens the books: the budget is the members' contributions summed from
approved clones only (a waiting resubmission funds nothing), at 25 Vehicle Points or 3 Base Points
per Hero Point off the data; the spend is priced by `CostCalculator` in the browser; over-budget and
no-price are two sentences that never merge; the contributors are listed largest first. A shared
machine is held to the same printed rules as one a character owns — `CheckSharedAsset` in the
engine, because a screen comparing Control to Speed is a host holding a rule — and a contribution
naming an object the campaign no longer has is `UNKNOWN_CAMPAIGN_ASSET`, browser-side, the way
`UNKNOWN_CAMPAIGN` is. The review's findings worth keeping: an overflow while the GM typed took the
whole page down between two keystrokes; a shared machine was held to none of the sentences its own
panel printed, so Control −20 paid points back in silence; a failed read of the players erased the
game's objects from the screen with Edit and Remove gone with them; every write on that screen
found its object by position, which one-object fixtures could not see; and two doc comments named
tests that did not exist, now swept by `RepositoryGuideTests`.

The verified fixture was every one of the ledger's own tests: a contribution naming another object
funding this one went red in both suites when the id filter was dropped, and the unknown-kind
clause went red in both when inverted.

### 33. Decisions Chapter 6 left to the owner

**Answered 2026-09-10, all twelve, and closed the same day.** Each question is kept with its
ruling beneath it. Rulings 1, 7, 8, 10, 11 and 12 landed in the first pull request; 2, 4 and 5+6
in the second; 3 needed nothing. The **build** markers below are what each ruling *was*, kept so
the reasoning stays beside the question. Two facts from building 5+6 worth knowing: a proposal is
`CampaignAssetContribution.Proposal`, null on every older sheet so every older export is
byte-identical; and the GM's draft editor became `SharedAssetEditor`, one component both pages
use. From building 4: Teamwork from a shared base reads the *live* campaign through item 30's
route and grants only to an approved membership — offline it is no grant and no error.

**Ruling 12 adds a server secret, and the deploy needs it before the GM's inbox will answer.**
`PLAYER_KEY_SECRET` signs the per-campaign player key; the worker refuses the inbox loudly without
it, by design, so a deploy that has not set it breaks every GM's approval page. Set it before
merging to `main`: `openssl rand -base64 32`, then the Pages project's environment variables (and
`.dev.vars` locally — never written by an agent). `docs/ACCOUNTS-SETUP.md` carries it.

1. **Does a suit of armour make a superhuman worse?** A wearer whose own Armor Power exceeds the
   Gear Limit gets less from a suit than they have without it — Armor 12d in Plate prints 8d.
   **Ruling: the book decides, and it does.** The owner's rule of thumb was that the specific rule
   beats the general one, and p.88's specific rule is that armour *"grants you the Armor Power"* and
   that a wearer who already has Armor *"can use it in place of Toughness"* — a grant and an option,
   neither of which takes a rank away. So a suit floors at the wearer's own rank: Armor 12d in Plate
   prints 12d, and the suit contributes nothing. **Build**: `DerivedStatsCalculator.ArmorFromGear`
   takes the max of the wearer's own Power and the capped gear figure, with the p.88 words in the
   guard's message.
2. **A structured prerequisite on vehicle features.** p.100's "only vehicles with Swimming can have
   this feature" is prose nothing checks, so a Submersible with no Swimming validates clean.
   **Ruling: check it.** **Build**: a `requires_features` field beside the prose on `vehicles.json`
   for Submersible and Transforming's two-of-four, read by the validator as a Warning; the test
   pinning the gap flips to asserting the check.
3. **Two copies of one flat feature** on one vehicle — two Sensors for 20 Vehicle Points. p.96 says
   "any number of features" and prints no rule against it. **Ruling: acceptable as built.** Nothing
   to do.
4. **A shared base's Training Facilities grant nobody Teamwork.** **Ruling: every approved member
   of the campaign gets it**, the way an owner gets it from their own base — still one point however
   many bases carry the feature. **Build**: `CalculateTeamwork` reads the campaign's shared bases
   the member has been approved into, browser-side where the sheet is priced against its campaign.
5. **A surplus contribution is unreported**, and
6. **Only the GM may write a shared object.** **Ruling, covering both: the players hold the
   object, and the GM approves it.** The points are the players' own from character creation and
   must never be stranded or spent for them; a player presents their best effort — the vehicle or
   base, built — and the GM accepts, refuses, or suggests changes. That is item 26's submission
   shape, whose plumbing already exists for a character: a membership-side draft, a GM decision, an
   approved clone. **Build**: a shared object becomes a submission a member authors rather than a
   row the GM types; the GM's page reviews it with the same three answers; contributions attach to
   the approved object. The surplus question dissolves — a player who over-funds their own proposal
   sees the figure on their own screen before submitting, and the validator says so as a Warning.
   The GM-authored path stays for a GM who wants to hand the party something.
   **Design accepted 2026-09-10, queued.** The proposal rides the character submission: a
   contribution gains an optional `Proposal` — the full `CampaignAsset` build — null on every
   existing sheet so every existing export is byte-identical; the proposer mints the id and
   adoption keeps it, so no contribution is re-pointed and no orphan appears; the GM's pending diff
   shows the proposal as a row with **Adopt**, **Adopt with changes** (today's draft editor,
   pre-filled) and **Refuse** (the existing reject); the same id already in the campaign is an
   amendment shown as a diff; `CAMPAIGN_ASSET_SURPLUS` is a Warning naming the unspent points. Two
   questions the owner answered by taking the recommendation: Adopt and Approve are one click (a GM
   who wants the object and not the sheet adds it by hand and rejects), and a proposal spends the
   character's Hero Points the moment it is written, before adoption. No new route, no migration,
   no server knowledge — a proposals table was considered and rejected as a second submission
   channel nobody asked for.
7. **A budget that overflows.** A contribution of a hundred million Hero Points is legal in an
   unlimited-budget game and the summed budget throws. **Ruling: cap a contribution at 10,000 HP**
   — the owner's words: a game would never exceed that on a Hero. **Build**: refuse above it with
   the figure in the message, on the engine's `CheckSharedAsset` and on the screen.
8. **A contribution whose kind disagrees with the object's** is priced at the object's currency in
   silence. **Ruling: report it.** **Build**: `CAMPAIGN_ASSET_KIND_MISMATCH`, Error, never repaired.
9. **A member who leaves takes their contribution out of the budget**, and nothing says the object
   lost the funding. **Ruling: say so.** **Build**: the GM page's over-budget sentence names the
   contributor whose approval is gone, and the object panel says the budget fell and by how much.
10. **The editor is one slot**: "Add", or "Edit" on another row, replaces an open draft silently.
    **Ruling: refuse until the draft is saved or cancelled.** **Build**: the second control is
    disabled while a draft is open, with the reason beside it.
11. **An older build's settings save drops every shared object**, the table rules and the
    Immortality price, because it reads the payload leniently and writes its own `Campaign` back.
    **Ruling: refuse the stale save, and explain it.** The owner's condition is the explanation:
    losing control unexpectedly is what users hate, so the refusal must say *why* in the user's
    terms — this tab is running an older version of the site and saving would erase settings it
    cannot see; reload to continue. **Build**: the server compares the fields the stored payload
    carries against the ones the save carries and answers `409` with that sentence; the browser
    shows it verbatim and offers the reload. No silent merge — a merge would hide the same loss one
    field at a time.
12. **Two characters from one account in one game** show as two contributors with no hint they
    are one player. **Ruling: a tree under the player** — the player once, their characters beneath,
    the owner's example being a character held as two sheets (Gemini). **Build**: the contributor
    list groups by account, the account's label as the parent row and each character's contribution
    as a child, largest player first. This is also the shape item 21's variants will want to be
    drawn in, so build the grouping once.
    **Design accepted 2026-09-10, queued.** The server tells a GM nothing about which account a
    membership belongs to, and keeps not doing so: inbox rows gain a `playerKey`, an HMAC of the
    campaign id and the player's account id under a server secret — stable within one campaign,
    meaningless across campaigns, naming nobody — and the browser groups the ledger by it. A
    single-character player renders flat, so the common case is unchanged. The parent row's label
    is the owner's pick of the recommendation: *"One player, 2 characters"*, which is true and
    invents nothing; a display name for an account would be item 19's admin page, not this.

### 34. A warning cannot be dismissed

**Asked for by the owner 2026-09-10, design accepted and built the same day** — see the pull request
that carried it; the Error guard and the display filter each went red under the orchestrator's
own mutation. Every finding a row
prints stays printed until the sheet changes, and a Warning the reader has read and decided to
live with is noise on every later glance.

- **Key**: `(Code, SubjectKind, SubjectId, OwnerId)` — the four fields `SheetFindings` already
  routes on. Not the message (prose moves) and not `Value`/`Limit` (a dismissal that silently
  un-dismisses when a number moves is a control that appears to be broken).
- **Where it lives: this browser**, under `ppStore` beside the theme choice, keyed by character
  id — the owner's pick, made knowing the alternative. On the sheet it would travel into the JSON
  export, the MCP skill's input and the campaign clone, where the GM would read "the player waved
  this away" — a social feature nobody asked for — and it would be one more field every
  rules-adjacent reader has to be told to ignore. A browser that refuses storage means nothing is
  dismissed, the same fallback `SavedCharacters` makes.
- **Errors stay undismissable.** `IsValid` is "no Error"; a dismissable Error is a way of making an
  illegal character look legal, against the settled rule that an illegal character is reported,
  never repaired. The control is drawn only on a Warning and the filter asserts on severity rather
  than trusting the key.
- **The engine is never told.** The filter sits where `RowFinding` is fed and in `Review.Issues`,
  the precedent `Review.razor` already sets for `HP_BUDGET_EXCEEDED` in Villain mode; `Validate()`,
  `IsValid` and the JSON report are unchanged.
- **Undo**: the review panel says "N warnings dismissed · Show them"; keys the engine no longer
  produces are pruned on write.

### 35. The roster is reachable from nowhere but itself

**Reported by the owner 2026-09-10: invited players "do not see the character select area".**
The investigation found no gate — not role, not cap, not viewport, not hosting — and one gap: the
character manager and the banner's switcher render only under `/build`, nothing on the front page,
`/rules`, `/campaign` or the ⌘-K palette links to `/build/characters`, and a player holding one
character sees no list at all because the list draws only *other* characters. Two of the five
ranked causes are that shape; the rest are "nothing built yet", a failed or signed-out read (the
panel then says "in this browser only"), and a cap of 0 or 1 set on `/admin`.

**The owner's answer: a Characters tab in the banner beside Build, Run and Rules, and a
"Your characters" entry in the palette.** Built the same day, see the pull request that carried
it: `Areas` gains the roster as its own area at `/characters` (the old `/build/characters` redirects,
so a bookmark still lands), the banner gains one `NavLink` — the cost `MainLayout.razor`'s own note
says a new avenue has — and `Commands` offers the roster by name once something is typed. A player
with one character was already shown it, named, under "Open now"; a regression test now holds that,
since nothing had. **The four shell goldens draw the banner and must be regenerated on the CI
runner** (`gh workflow run visual-goldens.yml --ref <branch>`), never locally — the pixel
comparator will be red until they are.

### 36. Four validator checks still skip a Gadget's Powers

**Found 2026-09-29, by the follow-up to the named-immunities pull request, and deliberately not
fixed in it.** `CheckUnitNames` walked `sheet.SelectedPowers` alone, so an Immunity bought inside a
Gadget was never checked for its names — the third time this shape has shipped, after
`EveryModifier` and `CheckModifiers` (item 32). It now walks `sheet.Gadgets` through the same
method as the character's own Powers. Four more checks have the same gap, each confirmed by a
throwaway probe against the shipped rules:

- `CheckDuplicatePowers` — the same non-repeatable Power twice in one Gadget is priced twice and
  draws no `DUPLICATE_POWER`.
- `CheckPowerCosts` — a Gadget Power driven to its cost floor by Cons draws no
  `POWER_COST_AT_MINIMUM`, so a further Con buys nothing and nobody is told.
- `CheckSources` — a rankless Gadget Power with no Source draws no
  `RANKLESS_POWER_WITHOUT_SOURCE`, so it has no default rank against other Powers.
- `CheckUnverifiedPowers` — latent: no Power carries `needs_review` or an unverified
  description today, so there is nothing for it to miss yet.

**Closed 2026-09-30 with the better fix** (see the pull request that carried it): one enumeration
of every `SelectedPower` a sheet pays for, with its subject — `EveryPaidPower` in
`CharacterValidator` — that the four checks above, `CheckUnitNames` and `CheckPowerRanks` (a fifth
instance of the same shape, found while building the guard and not named here) all walk. A
Gadget's finding carries the Gadget as its subject and routes to the Gadget's row. The review found
the duplicate pool keyed by a Gadget's display name, so two Gadgets named alike merged their pools
and a Power bought once in each read as a repeat; it is keyed by the Gadget instance now.
`GadgetPowerWalkReadTests` holds every remaining direct walk of `sheet.SelectedPowers` to a
one-line reason and an exact count, scans the whole file rather than a line at a time, and refuses
a filter chained straight onto the enumeration — each of those three closing a way past it the
review walked through.

**One residual, recorded rather than fixed here**: `CheckQuantities`' `PER_UNIT_WITHOUT_UNITS`
clause still walks the character's own Powers alone, so a per-unit Power bought at zero units
inside a Gadget is not reported. The guard's allow-list names it as the gap it is; it is not one of
the four this entry was written about, and it is a few lines on `EveryPaidPower` when somebody
wants it.

### 37. Joining a campaign is one character at a time, and each join re-reads the page

**The owner's report of 2026-09-30: adding several sheets to a campaign is painfully slow.** Two
causes, measured by reading the page rather than timing it:

- **The join box only joins the character on screen.** For each further sheet a player goes to
  the character manager, opens it (a read and a pointer move), comes back to `/campaign`,
  re-enters the code — the box empties after every join — and presses Join. About six clicks and
  a paste per character.
- **Every join then re-reads the whole page, serially.** `Refresh` awaits availability, the
  campaigns list, the memberships, the inbox, the pointer, one `ReadAsync` per membership with
  anything sent (`EmptySubmissions.AmongAsync`), and then the campaign resolve — roughly 7 + N
  round trips one after another, so the third character costs more than the first.

**Proposed, and pitched to the owner with a specimen; not built.**

- **Who joins** becomes a checkbox list of the account's characters under the code box, the
  on-screen one pre-ticked and rows already in that game shown disabled, under one button —
  *Join with 2 characters*. Each is joined in turn by the existing route. A character not on
  screen is read by id (`ReadAsync`, never `OpenAsync`), run through `CampaignJoin.Apply`, and
  written back **by id** through the `RestoreAsync` path, which never moves the pointer and
  answers whether the write landed; the on-screen one keeps today's session path, because a
  stored read straight after an edit can lag the sheet on screen (item 31).
- **The code stays in the box** after a join.
- **`Refresh` runs its independent reads under `Task.WhenAll`**, the per-membership reads
  included.

**Faults said per row, nothing repaired**: a tier that disagrees (nothing written), a read that
fails, an account-cap refusal on the write-back, an empty sheet skipped with its own sentence.

**Built — see the pull request that closed this item; the owner took both recommendations.** The
checkbox list under the code box, one button, each ticked character joined in turn: the on-screen
one by the session path it always took, a character not on screen read by id, run through
`CampaignJoin.Apply` and written back by id through `RestoreAsync`, which answers whether the write
landed. A tier that disagrees writes nothing and says so; a read that fails says which of its two
failures it was; a refused write-back is said; an empty sheet is skipped with its own sentence; a
character already in a game is shown disabled. The code stays in the box. `Refresh` and
`EmptySubmissions.AmongAsync` run their reads under `Task.WhenAll`. **A second press while the
first is in flight does nothing** — a no-context review found the by-id write-back could run twice
over one character from a double-click, the lost-update shape `Autosave` exists to stop — so
`Join` holds an in-flight flag and the button is dead on it; the orchestrator removed the flag and
watched `ASecondCallWhileTheFirstIsInFlightChangesNothing` go red. The agent's first
tier-disagreement mutation was null — it wrote back byte-identical content — and the test gained an
`UpdatedAt` control before the same mutation went red, which is the shape `CLAUDE.md` warns of.

### 38. A Villain approved into a campaign becomes the GM's, and the player's nemesis

**The owner's idea of 2026-09-30, and all four of its questions answered on 2026-10-01.** Today a
campaign holds a *clone* and the player keeps their character whatever kind it is. For a Villain
that changes: when one is approved, **the sheet stops being the player's and becomes the campaign
owner's**, and the campaign screen shows it back to the player as the nemesis they made. It sits
well with two settled rules: Villains are GM material (only Heroes have Resolve; the GM spends
Adversity on any NPC), and a Villain's Flaws are the players' handles.

**The owner's four rulings, in their words where they gave them:**

1. **The player sees the name and an effect, not the sheet.** *"They can see the name and a cool
   visual effect that it's their nemesis. Like evil eyes and obscuring or something simpler. Just
   give them something they can see and go 'holy crap.'"* So no read-only sheet: the stats are the
   GM's now and the point is that the player does not know them.
2. **It cannot be handed back.** *"Once transferred it's the GM's / campaign owner's now."* No
   undo, no return route, no admin repair.
3. **It counts against the receiving account's character cap**, the same `character_limit`
   `worker/characters.js` refuses at 409. A Villain is not exempt as campaign material.
4. **It happens on approval, and the player is warned before sending.** No separate *Take as
   nemesis* control for the GM; approving a Villain is the handover.

**What a slice has to build.**

- **Server (`worker/`).** `approveSubmission` in `worker/db.js` is one compare-and-swap `UPDATE`
  on `campaign_members`; for a payload whose `IsVillain` is true the approval must also move the
  player's `characters` row to `gm_user_id` — or write the approved payload as a fresh row on the
  GM's account and delete the player's, whichever keeps the move atomic in D1 — **inside the GM's
  cap, checked in the same statement batch**, so a full account refuses the approval rather than
  half-doing it. The membership row records that it was handed over, so neither side reads it as
  an ordinary approval afterwards. **The server never parses a character today** (see
  [`docs/guide/accounts-server.md`](docs/guide/accounts-server.md)): whether a payload is a
  Villain has to come from somewhere it can read without parsing — the `kind` index field
  `characters.js` already stores is the obvious source, and the submission may need to carry it.
  Read that guide before choosing; it is the decision in this slice most likely to be got wrong.
- **A full cap is said, to the GM, on the approval screen**: the approval is refused whole, the
  snapshot stays waiting, and the sentence names the cap — the same answer the roster gives a
  full account. Recommended, not ruled: ask the owner only if a reviewer disagrees.
- **The player's roster.** The row does not vanish silently: it reads that it was given to the
  campaign as a nemesis and opens nothing — the rule an unreadable row already follows, that a
  reader is told which state a thing is in. It no longer counts against the player's cap, since
  it is not theirs.
- **The campaign screen (`/campaign`).** A *Your nemesis* block for the player with the Villain's
  name and an effect worth a double take — the owner offered evil eyes and an obscuring veil, or
  something simpler. **It is drawn in the villain palette's tokens and named no colour**, honours
  `prefers-reduced-motion` (the three duration tokens already collapse every animation), and is
  held by a twin harness if it animates, per `CLAUDE.md`. Pitch a specimen before building it,
  as the owner's working rule asks.
- **The GM's side.** The transferred sheet is an ordinary character on the GM's roster and opens
  in the builder like any other. The campaign roster still lists the membership, marked as the
  nemesis, so the GM can see who made it.
- **The warning.** Send for approval on a Villain says, before anything goes, that an approved
  Villain becomes the GM's for good and the player will see only its name — and needs a second
  press, the way Leave and Remove already do. A rejection leaves everything exactly as it was.
- **Nothing on the engine side moves.** `IsVillain` stays a presentation flag no rules code reads
  (`PresentationFlagsTests`); this is storage and screens only.

### 39. Fourteen Pros and Cons ask the player to define something, and there is nowhere to write it

**The owner's ask of 2026-09-30: find every place the rules ask the player to describe something,
and make sure they have somewhere sensible to do it.** Counted from `narrative_constraint` in
`data/rules/`, forty entries ask — and the answer splits cleanly:

- **19 Flaws and 7 Perks have a box.** `SelectedFlaw` and `SelectedPerk` carry `NarrativeDetail`,
  the tab labels the box with the rule's own sentence, Add is dead until it is filled, and the
  sheet, both exports, the CLI and the diff all carry it.
- **11 Cons and 3 Pros have nothing.** `SelectedProCon` is an id, a grade and a unit count and no
  text — `docs/guide/rules-engine.md` records the `(Item: armor)` half of this. `ProConPicker`
  never prints the sentence either: its caveat is the description plus the applicability note,
  so a player taking Conditional, Limited, Side Effect, Signature, Exclusive, Charges, Delayed,
  Resource, Triggered, Item, Blocked or Concentration's kin is never told the book wants the
  condition written down and has no box for it. Immortality's own *Vulnerable* — "describe how"
  — is the same gap on a Power's own Con.
- **Two Powers ask in their prose and not in a field**: Expertise is "a specialisation you name"
  (the Trait is nominated; the name is not), and Animation has the player pick one Trait to sit
  at full rank. Neither is modelled.

**Built — see the pull request that closed this item; the owner asked for Expertise and
Animation in the same slice.** `SelectedProCon.Detail` and `SelectedPower.Detail` (`string?`,
null when absent so every stored sheet round-trips byte for byte and
`StoredCharacter.CurrentVersion` stays 1). The picker's confirm panel and the Power editor ask in
the entry's own words and Add waits, exactly as the Flaws tab does; the CLI prompts the same
question. The sheet prints `Conditional (Often Works) — only under an open sky` and
`Expertise: Firearms`; the Ability source line prints `(Item: plate armour)`, closing the gap
`rules-engine.md` recorded; both exports, the MCP option listings and `power_detail`, the skill's
and the question policy's JSON shape, and the diff (as a part) carry the words. Three entries
gained a `narrative_constraint` in the data: Expertise, Animation, and Immortality's own
Vulnerable. Nothing is validated: a missing answer is a box the editor will not let past, not a
finding — the same rule the Flaws follow.

### 40. A Power's own Pros and Cons read like the generic ones

**The owner's report of 2026-09-30**: Immortality's own *Vulnerable* beside the *Vulnerability*
Flaw is confusing — the same word for a Con printed inside one Power's entry and a Flaw anybody
can take. Today `ProConPicker` marks a Power's own option with a *this Power* tag in both the
offered and the chosen list, and nothing else distinguishes them: the sheet, the exports and the
diff print the name alone, and the picker lists both kinds in one run.

**Where it bit, in the owner's words: on the sheet.** He read *Vulnerable* there, went to see how
the Con worked and found no tooltip, then looked *Vulnerability* up first through `Ctrl`/`⌘`+`K`
and was handed a different rule. So the fix is on the sheet and in the palette, and the picker —
which already tags a Power's own options *this Power* — is left alone.

**Built — see the pull request that closed this item.** Every Pro and Con on a Power's line is a
`Term`; a Power's own opens *"Immortality's own Con."* before its text, keyed on the Power so a
generic option of the same spelling cannot share its sentence. The palette gains a fifth group,
**Rules terms** — generic Pros and Cons, Perks, Flaws and every Power's own option — each row
saying its kind before its description, so the two *Vulnerab…* rows sit together labelled Con and
Flaw; choosing one goes where it is bought and adds nothing. The picker's two-group layout was
pitched and not built: `OptionList` filters rows and would not filter a heading, and the chip
already answers the question there.

### 41. Two play tests fail on every Windows checkout

**Found 2026-10-01, while running the full suite for items 39 and 40; recorded, not fixed.** On a
Windows checkout `McpPlayServerTests.EveryProvenanceNameThePolicyPrintsIsOneThisServerEchoes` and
`PlayEngineStepTests.TheOrderOfActionComesFromTheLadderAndPutsMinionsAfterAllOfIt` fail on every
branch, `main` included, and both pass in CI. The cause, read from the failures:

- `.gitattributes` checks `*.md` and `*.json` out with the platform's line endings, so
  `mcp-play/PLAY-POLICY.md` and `data/rules/play/combat.json` are CRLF on Windows.
- The provenance test finds the end of a paragraph with `IndexOf("\n\n")`, which a CRLF
  file never contains.
- The twin's `SubstitutedPlayRules.WithDefect` searches the JSON for a line written with `\n`
  between its two halves, finds it zero times, and throws — correctly, by its own rule.

**The fix is a choice between two places**: normalise to LF where those two tests read the text,
or pin both files `eol=lf` in `.gitattributes`. The second is one line and fixes every future
reader too; check that nothing on the Windows side depends on CRLF in them first. Either way, a
local full run that is red for a reason unrelated to the change is how a real red gets waved
through, which is why it is worth an item.

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

1. **Tick its box in the list above — and write no account of it here.** The pull request is the
   account; `CLAUDE.md` says so in as many words, and `docs/progress/` is **closed**, not a
   directory to add to. This step used to require a `docs/progress/YYYY-MM-DD-a-short-slug.md`
   write-up and it no longer does: the owner's position is that these narrate a session rather than
   a change, and the reasoning worth keeping belongs in a doc comment beside the guard it explains
   or in the message the test prints. **The tick is not a formality** — see what it requires, above;
   under an orchestrator it is the orchestrator's to make and never the author's.
2. Update **Current state** if the headline numbers moved (entry counts, coverage). **The test
   figures are not among them any more** — that row names `./scripts/count-tests.sh` instead,
   because a number recorded in prose here went wrong four times and the script cannot.
3. If the work revealed new gaps, add them to **Remaining** rather than leaving them in a commit
   message.
4. Link the PR. It carries what changed and why, so nothing here has to repeat it.

If a task turns out to be partly blocked, say so explicitly in the item and name the blocker. An item that quietly narrows its own scope is worse than one that stays open.
