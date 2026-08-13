# Progress

The single source of truth for what is done and what is left in this project.

**This file must be updated as part of any task that changes what is done or what remains.** Not afterwards, not in a follow-up — in the same change, so the record and the code land together. Previously this information lived in two places (the README roadmap and a gaps list in `CLAUDE.md`) and drifted out of step with reality; both now point here instead.

Keep it honest. A half-finished item stays open with a note on what is missing. "Done" means done and verified, not written.

---

## Current state

| | |
|---|---|
| Rulebook coverage | Everything character creation needs. Chapters 1–2 fully extracted and verified, plus Ch.6's custom gear and Ch.7's toxin Pros/Cons. Chapters 3, 4, 5 and 7 are play rules, 8 is the pre-built characters (transcribed in the tests) and 9 builds Villains by the Hero rules — see item 3 |
| Powers | 141 entries, all mechanically verified against Ch.2 pp.21–48 |
| Power-specific Pros/Cons | 106 entries across 62 Powers, verified |
| Custom gear features | 12 entries, verified against Ch.6 p.93 |
| Other rules data | Tiers, abilities, talents, pros, cons, perks, flaws, sources — all verified, nothing flagged |
| Tests | 3345 across two projects — 3227 on the engine, 118 rendering components with bUnit — run in CI at the same strictness as the build |
| Wizard | All six creation steps working, with back-navigation and `.txt` + `.json` export |
| Front ends | Two interactive, plus two for a machine — the terminal wizard, a Blazor WebAssembly app, `build --from`, and an MCP server somebody can connect to their own Claude. All on the same engine assembly |
| Hosting | **Live** at [prowlers-and-paragons-chargen.pages.dev](https://prowlers-and-paragons-chargen.pages.dev), deployed from `master` by GitHub Actions; `pp.softwaresamurai.net` not yet attached |
| Printed sheet | One A4 page on the published Hero Sheet's layout; Hero and Villain ink on white paper — see the completed item below |
| Static analysis | Zero warnings at CI strictness; a whole-tree Qodana scan reports zero |
| Known-wrong data | None outstanding |
| Licence | MIT, in `LICENSE`. Covers this repository only — the game system is © LakeSide Games and no rulebook text is here |

The engine reproduces the printed Edge, Health and Resolve of all 20 pre-built Heroes in Chapter 8, and rebuilds **16 of the 20 to exactly their 125 Hero Point budget**. The remaining four are all 1 HP out, each for a recorded reason — see [Close the last four Heroes](#1-close-the-last-four-heroes).

---

## Remaining work

Roughly in the order that unblocks the most. **Nothing here is a defect** — the tool creates, prices, validates, prints and exports characters through four front ends, and a visitor with no account can watch a real conversation build one. What is left is four Heroes a Hero Point out, some polish on the printed sheet, one sub-tool nobody has needed, a Power search that orders ties by name, and a payload size.

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

The two ambiguous grades (`Side Effect: collateral damage`, `Limited: only for Telekinesis`) remain guesses that could be revisited, but do not tune them just to force a zero — that is fitting the model to the answer.

**Revisited, and deliberately not closed.** The arithmetic was worked out and it is a trap:

- **T-Kay closes exactly** if `Limited: only for Telekinesis` is read as *somewhat limited* (−1) rather than *significantly limited* (−2). It sits on Lightning Reflexes, a flat 3 HP Power, so the grade is worth 1 HP after the floor — precisely his −1. **That is the tuning this item forbids.** The sheet prints no grade; "only for Telekinesis" reads at least as much like the harsher grade as the milder one, and the only thing recommending the milder one is that it makes the number come out. Deciding it needs the Power's entry in the book, not this file.
- **Vigilant cannot be closed by his gear.** His Jo Sticks are *Upgraded*, worth +2, and he is 1 HP under: transcribing the feature moves him to +1 rather than to 0. Adding it would make the transcription more faithful and the residual no smaller, so it is left recorded rather than half-applied.
- **Herald (Airmid) at +2, Herald (Scathach) at +1 and Shadow at +1** have no candidate in the data at all. Scathach's Strike carrying four Pros and Cons at once remains the most likely place for a variant reading to be wrong.

**The book was then opened, and it settled two of the three questions above.** `docs/` holds both PDFs — they are gitignored, so they are in the main working directory and **not in a worktree's `docs/`**, which is how they were missed at first.

- **T-Kay's grade is a judgement call by the rulebook's own words.** The Limited entry (Ch.2) reads: −1 "if the Power is somewhat limited", −2 "if it's significantly limited", −4 "if it's severely limited", and then *"Use this Con as a catch-all when nothing else seems appropriate."* There is no rule mapping "only for Telekinesis" onto a grade, so the milder reading has nothing recommending it except that it produces a zero. **Left as recorded.**
- **Vigilant's Upgraded is confirmed printed** — his Gear box reads `2 Jo Sticks: 10d (s) Melee (Upgraded)`, and he has Two-Fisted, so the pair is customised for one price of 2 HP. He is 1 HP under, so transcribing it lands him on +1. It closes nothing and is left recorded rather than half-applied.
- **Herald (Airmid) is closed.** The lead was her package: she was recorded on the Superhero Package while her sheet prints nine of twelve Talents at 2d, and a package's granted ranks are a floor. Following it found the actual fault — **her sheet prints two Expertise Powers, "Expertise (Medicine: Ancient Remedies) 12d" and "Expertise (Science: Botany) 12d", and only the first was transcribed.** Expertise costs half a Hero Point per rank and takes its baseline from the nominated Trait, so 12d over Science 2d is ten purchased ranks and **exactly 5 HP** — which is what the wrong package was absorbing. With the second Expertise transcribed and the package corrected to the one her printed Talents allow, she rebuilds to 125 to the point. Both halves are forced by the printed page.
- **Scathach's transcription is verified faithful to the printed sheet** — every Ability, all twelve Talents, all eleven Powers, both her Edge/Health/Resolve and her Determination, and all four modifiers on Strike. The rulebook gives Strike two different deflection Pros, `Deflect` (+4, physical *and* energy) and `Deflect Missiles` (+2, physical only); her sheet prints the plain one and the data uses +4, which is right. So her +1 is in the pricing model, not in the data — which is a narrowing rather than an answer.
- **Shadow** still has no candidate at all.

**A second, independent argument for every package attribution now exists**, and it is what caught Airmid. The inference had rested entirely on which package lands the rebuild on 125 — an argument from a total, and totals can agree for the wrong reasons. A package's granted ranks are a *floor*, so a package is impossible if the sheet prints a Trait below it, whatever the total says. `PrebuiltHeroTests.NoHeroPrintsATraitBelowWhatItsPackageGrants` checks all twenty against that, and it matters most for the five whose totals do not land on 125 — exactly where the totals argument is weakest.

**What reading the book did find is that every one of the twenty page citations was ten pages out.** Chapter 8 runs from printed 127 to 146 and the transcription recorded 137 to 156 — the offset applied twice. This is the error `CLAUDE.md` already warns about ("was ten pages out in the chapter it was offered for"); the note was corrected and the transcription was not, because nothing read those numbers. `PrebuiltHeroTests.EveryHeroIsCitedInsideChapterEight` now does.

Five residuals inside a 2 HP bound, each with a recorded reason, remains a more honest state than five zeroes.

One thing genuinely cannot be modelled as things stand: Eidolon's `Omni-Power (Mind Link)` applies Telepathy's Pro to a *mimicked* Power. Pros are stored per Power, so there is nowhere for it to live. Eidolon reconciles anyway, so it costs nothing today.

### 1b. Semantic pro/con constraints are still unenforced

The invented per-Power lists are gone — see the completed item below. What is left is the half of the constraints that cannot be checked against anything the rulebook prints per Power: "Powers that inflict physical or energy damage", "Powers that can be activated and deactivated at will", "attack Powers", "Powers that last or can be maintained". These are shown to the player as a caveat on the option and left to the GM, which is how Ch.2 frames the list.

Enforcing them would need roughly seven booleans on each of the 141 Powers — about a thousand fresh judgements against the book. That is worth doing only if something downstream actually needs it, and the obvious candidate was assisted creation, where a model proposing a character benefits from the engine ruling out illegal combinations.

**Assisted creation has now shipped without them, and did not need them** — see the completed item below. A caveat is shown to whoever is proposing and left to the GM, which is what Ch.2 says it is. So this stays open with no consumer asking for it, and the caveat remains honest where the guess would not be.

### 2. What the sheet still cannot say

Found by an adversarial audit during the sheet-polish slice; real, and out of scope for it.

**A printed page in the middle of a sheet is anonymous.** Much less pressing now the sheet is one page for an ordinary character, but a Powers-heavy one still runs over. The name is on page one and in a colophon on the last; every page between them relies on the browser's own print header, which the user can switch off — and unticking it is exactly what the review step now tells them to do, because that header is also where the web address comes from. CSS has no portable answer: `position: fixed` renders once at the top of page two in Chrome, and Chrome supports neither `@page` margin boxes nor `counter(page)`. The only mechanism that genuinely repeats per page is a table `<thead>`, which would mean rebuilding the sheet as one table.

Smaller, from the same audits: the GM review step lists findings with no route back to the step that caused them.

**The 0d half of this item turned out to be a rules gap rather than a UI wrinkle, and is closed.** It was recorded as "a fresh sheet starts every Ability at 0d although the editor's floor is 1d without anything objecting". The floor was right and nearly everything else was wrong: Ch.2 states, once for Abilities (p.17) and again for Talents (p.18), that **no rank can be lower than 1d** and that ordinary people have 2d in every one — so a character has all eighteen Traits, 0d is not a low rank but a Trait nobody can be without, and the Talents editor's floor of 0d contradicted the book outright.

It was enforced nowhere, and it costs Hero Points: without a package a character pays for all eighteen at 1d, which is 18 HP before anything interesting. **The packages corroborate it** — the Civilian Package is 35 HP for 2d in all eighteen, which is 36 points of ranks, exactly the "small discount" the rulebook calls a package. All twenty published Heroes take a package, so every one of their Traits sits at or above its floor, which is why rebuilding them never caught this.

Now `TRAIT_BELOW_MINIMUM`, with `TRAIT_BELOW_PACKAGE` beside it for the other floor — a package's granted ranks cannot be lowered, which is the rule that proved Airmid's attribution impossible and was a test over the published Heroes before it was a check here. Both samples had to gain their missing Talents; both were illegal characters shipped as examples.

### 3. Remaining rulebook chapters — mostly not this tool's business

**This item used to say chapters 3–9 were "not extracted" and rank them "by usefulness to the wizard", with Combat third. That was wrong, and it made a finished job look unfinished.** Read against the table of contents, everything a *character generator* needs is extracted:

| Chapter | What is in it | Relevant to creating a character? |
|---|---|---|
| 3, Action (p.67) | Challenge rolls, assisting, contests | No — play |
| 4, Combat (p.73) | Combat, stunts, minions, gritty rules | No — play |
| 5, Resolve and Adversity (p.83) | Earning and **spending** Resolve | No — play. The Resolve a character *starts with* is Ch.2 p.60, extracted and implemented |
| 6, Equipment (p.87) | Gear limits, armour, weapons, **custom gear (p.92)**, gadgets, vehicles, headquarters | Custom gear features: **extracted**. Mundane gear is free and untracked. See below for the one gap |
| 7, Environment (p.105) | Disasters, falling, lifting, **toxins (p.108)** | No — play. The three toxin Pros/Cons are extracted |
| 8, Friends and Foes (p.111) | The pre-built Heroes and Villains | Transcribed in the test suite, where they verify the engine |
| 9, Creating Villains (p.167) | Villain guidance, GM tips | No mechanics to extract — Ch.9 builds Villains by the Hero rules, which is why the mode is presentation only |

**The one genuine gap is Ch.6's vehicles and headquarters (pp.94–104).** `unique_vehicle` and `headquarters` are Perks priced per unit — a Hero Point buys 25 Vehicle Points — and what those points buy is not modelled, so the perk is a cost and a free-text note. That is a sub-tool of its own (spend a vehicle's points on a vehicle), not a chapter to extract, and nothing else needs it.

### 4. `search_powers` ranks ties alphabetically

The MCP server's Power search is a word match, and when several Powers match the same words it
puts them in name order under a caution calling them "the closest entries". **"Walks through
walls" is the case to reproduce**: twenty-one Powers score two points each, every one of them on
the filler word "through" — Phasing among them, at position eleven, where a caller asking for
eight rows never sees it. "He shoots fire from his hands" is the same weakness the other way
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

## Completed work

Newest first. Link the PR so the reasoning stays findable.

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

### Conversational creation, half of it: the MCP server, and the questions worth asking — [#39](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/39)

`mcp/` is a stdio MCP server wrapping the same engine, so somebody can connect their own Claude,
describe a character out loud, and get a legal costed one back. It handles no credentials and
holds no key — the conversation happens in the client they already pay for. The setup a stranger
needs is in the README; the dependency arrows hold at compile time, because `mcp/` references
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
  matching answered "she bakes bread in the city" with **Elasticity**; matching is word by word
  with a shared-prefix rule now, because a match like that is worse than none — nothing in it
  looks wrong.

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
- **A mistyped `PROWLERS_RULES_DIR` fell through to the shipped copy**, silently. The README's
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
  refusal had landed on the argument and not on the variable, which is the one the README tells
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

`pp.softwaresamurai.net`, deployed by GitHub Actions on every push to `master` that touches the app, the engine, the rules or the deploy itself. Direct upload rather than Cloudflare's Git integration, so there is one deploy path rather than two that can disagree. Setup and the token scoping are in the README.

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
