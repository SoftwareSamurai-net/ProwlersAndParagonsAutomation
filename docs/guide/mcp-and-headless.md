# The two assisted-creation surfaces

Read before touching `mcp/` or `cli/Headless/`: the question policy, the six tools, the standard-output discipline, and the `build --from` contract.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## The headless build command

`dotnet run -- build --from character.json` costs and validates a character, writes both exports and exits **0** (legal), **1** (breaks a rule) or **2** (unreadable input or bad arguments). It exists so a model can propose a character during play and have the engine decide whether it is legal. The skill that teaches that loop is `.claude/skills/prowlers-and-paragons-character/SKILL.md`.

- **Nothing in `cli/Headless/` computes a Hero Point.** The model proposes and the engine decides; inverted, this is a random number generator with good prose. It also **reports and never repairs** — auto-clamping a rank would be the tool making a design decision about somebody's character, and would hide from a player that their concept did not fit.
- **The input is the `CharacterSheet` shape, not the JSON export.** The export is a report and reading it back would rebuild a character from its own conclusions. Reading and writing that shape is `engine/CharacterSheetJson`, shared with the browser's local storage so the two cannot drift.
- **`Read(json, strict: true)` refuses a field name that is not part of a character; `CharacterStore` deliberately does not.** A misspelled `AbilityRanks` in a submitted file drops every ability and produces a cheaper, legal character nobody notices is wrong. Restored storage wants the opposite: a field removed in a later build should cost a field, not the character.
- **Standard output is exactly one JSON report for each of the three exits**, `--help` excepted. Anything about the run itself goes to stderr, so a caller parses stdout whole. There is a test; do not print a warning above the report. `CommandLine` handles the arguments `BuildCommand` never sees, and reports them the same way — it existed as six lines of top-level statements in `Program.cs`, none of them covered, and one answered a misspelled verb with exit 2 and an empty stdout.
- **Every engine call in `Judge` is guarded, and a figure the engine cannot supply comes back null rather than 0.** Reporting 0 for a character that cannot be priced is a lie a caller would act on. `Validate` is guarded too, and separately, because its failure leaves no findings to report at all — it was the one bare call here, under a comment claiming otherwise, and ten shapes of hand-written character came out as a stack trace through it.
- **`CharacterValidator` reports rather than throws, and `CheckHpBudget` carries a `catch` to keep that true.** The specific checks around it — unknown Pro, Con, Perk, nominated Trait, and variant or grade keys that are present but wrong — are what a repair loop acts on; the `catch` only promises the validator answers. Do not delete either for the other: the set of unpriceable shapes grows with every field added, and a validator that throws costs the caller every other finding.
- **A negative quantity is refused by the validator, and `TotalCost`'s arithmetic is `checked`. Nothing floors the total itself.** A negative `Units` on a per-unit Perk paid the character Hero Points and reported an over-budget character legal at exit 0; a very large one wrapped to a negative total and did the same. What stops both now is `NEGATIVE_UNITS` and `checked`, so the verdict is right — but a report for such a character still quotes a negative spend, because the figures are the engine's and the engine was asked to price nonsense. Do not read this bullet as a floor in `CostCalculator`: there is none, and an earlier version of this sentence said there was. Six fields carry a quantity — ability rank, talent rank, a Power's purchased ranks, a Power's `Units`, a Perk's `Units`, and a Pro or Con's `Units` — and the last was the one missed first time. `checked` on the total alone is not enough: the per-unit multiplications underneath it are where the wrap happens.
- **The same Pro or Con twice is refused — unless the rulebook says to buy it again.** Three options say so in their own entries: Also X on Energy Absorption ("each time you select this Pro") and on Energy Form, and the generic Affect Inanimate ("You can apply this Pro multiple times"). That is `repeatable` on the option, never a list of ids in the validator, and it is **two of the seven Also X entries** — the other five are priced per unit, where the quantity is the mechanism and a second copy really would charge twice for one thing. Missing this made **Blastwave illegal**: his six energy types are five copies, the calculator charged all five and landed him on his printed 125, and the validator returned four errors on the same sheet. Every cost floors at zero, so three Burnouts cancelled a 12d Ability exactly — six Abilities at the Trait Cap for 0 HP, reported legal with an empty issue list. A duplicate flaw is refused too (it paid Resolve twice for one drawback); a duplicate **Power** is only a warning, because the rulebook does not forbid it and two Blasts with different Pros is a shape a player might want — what is wrong is that the budget charges for both while the sheet shows the first.
- **`--from` refuses Windows device names.** `File.ReadAllText("CON")` opens the console and blocks for ever: no output, no exit code, no end. `NUL` and `PRN` fail politely and `CON`, `COM1` and `CONIN$` do not, so the whole reserved set is refused rather than the three that were caught.
- **A report must not blame the caller for a fault here.** A duplicate id in a rules file throws the same exception type as a bad character; `CHARACTER_UNUSABLE` used to say "a null where an id belongs, most likely" and send a repair loop after its own file for ever. It no longer guesses, and `ENGINE_COULD_NOT_ANSWER` exists for the other half: a figure the engine cannot supply with no error beside it is this program's fault, and saying so is what stops a caller looping on a legal character.
- `ValidationIssue` carries `SubjectKind`, `SubjectId`, `OwnerId`, `Value`, `Limit` and `Options` beside the sentence, because a repair loop would otherwise have to parse the message back into the facts it was built from. All six are optional; the messages are unchanged and still held to `ValidationMessageTests`.
- **`UNKNOWN_TIER`, `UNKNOWN_PACKAGE`, `UNKNOWN_ABILITY` and `UNKNOWN_TALENT` exist because the wizard picks from a list and a submitted file does not.** An unknown tier used to skip *both* the budget and the Trait Cap checks, so a 99d Ability reported clean. Do not remove them on the grounds that no front end can produce one.
- `SkillDocumentationTests` feeds the skill's own example through the strict reader and validates it. It is documentation of a schema, which rots silently; it caught two errors in its first run. **It must ask `BuildCommand.SubjectKindName` for the wire names rather than converting the enum itself** — it did convert them itself, and so agreed with a broken copy.
- **`ValidationIssueStructureTests` checks its own case list against the validator's source**, so every code the validator can construct is provoked by some sheet. Without it the structural invariants were worth only what somebody remembered to add: two codes shipped uncovered in the change that introduced the invariants, and one invariant would have failed had they been listed. Three codes are exempt by name with a reason; do not add a fourth without one.


## The MCP server

`mcp/` is a stdio MCP server wrapping the same engine, so somebody can describe a character to
their own Claude and get a legal costed one back. It handles no credentials and holds no key —
the conversation happens in the client the user already pays for, and this program only answers
questions about the rules. It does not replace `build --from`; both call the same engine.

- **The question policy is the deliverable, not the transport.** Which two or three questions
  are worth asking is the whole design problem: ask none and you build somebody else's
  character, ask ten and this is a questionnaire wrapped around a wizard that already exists.
  It is written down in **`mcp/QUESTION-POLICY.md`**, which is *embedded in the assembly and
  served verbatim* as the `creation_guide` tool — one copy, so the document the next person
  reads and the document the assistant is taught cannot drift. `McpQuestionPolicyTests` holds
  it to the same standard as the skill: its example character goes through the strict reader
  and the validator, and the four questions are asserted by name.
- **A served document goes stale silently, and three sentences in this one had.** It claimed a
  Villain has no Hero Point budget (retired when `UnlimitedBudget` became an independent toggle —
  and `CLAUDE.md` already said so), that there is no Villain flag to set (`IsVillain` is a real
  field), and said nothing about Resolve at all. Every conversation the server has starts from
  this file, so a wrong sentence here is wrong everywhere at once and nothing compiles it. **When
  a decision changes in `CLAUDE.md`, grep this document for it.** The skill is the same pair and
  drifts the same way — both now carry the optimise-first default and the Villain Resolve rule,
  and both have tests naming the retired claims so they cannot come back.
- **The four that change the build are tier, one-Power-or-several, what the character is
  deliberately ordinary at, and Source** — and the second is the one a model is most tempted
  to answer silently. Everything else is decided and *shown*. The reasoning for each is in the
  document; do not re-derive it from the tool descriptions.
- **Six tools, chosen by what a conversation needs rather than by mirroring the engine.**
  `cost_character` beside `validate_character` is the engine's API: no turn of a conversation
  wants a price without knowing whether the thing priced is allowed, and a separate costing
  tool is an invitation to quote a number for a character that breaks a rule. So
  `check_character` answers both and is the only place the word "legal" is decided. The ten
  catalogues are one `list_options` for the same reason in reverse — ten tools for a dozen
  entries each would crowd out the ones that matter. Powers get two tools of their own because
  141 entries are searched rather than listed.
- **Standard output carries the protocol and nothing else.** Everything said to a human goes to
  standard error. `McpStdioTests` checks this twice, and needs both: it reads the source for
  `Console.` followed by anything but `Error` (not for `Console.WriteLine`, because
  `Console.Out.Write` and `OpenStandardOutput` are the same mistake in other spellings), **and
  it runs the built program and requires every line on that stream to be a JSON-RPC message.**
  The source half exists because **a stray line does not necessarily break a client** — the
  first runtime test drove the binary through the SDK's own client and asserted the session
  worked, and a real stray line left it perfectly happy, because the client skips what it cannot
  parse. Do not replace either with the other. **The runtime half now
  covers both launch paths**, because there are two: the published binary, and `dotnet run` from a
  checkout, which is what the repository's own `.mcp.json` uses.
- **"`dotnet run` writes MSBuild's progress to standard output" was the reason this guide gave for
  publishing, and on the pinned .NET 10 SDK it is false.** Measured through a forced full NuGet
  restore and a recompile, that launch put 4,448 bytes on standard output and every one was
  protocol. The claim is not merely stale — `.mcp.json` depends on it being false, which is why
  `TheBuildToolLaunchSpeaksNothingButTheProtocol` now holds it rather than a sentence doing so.
  Publishing is still right for a client that is **not working inside a checkout**, because
  `dotnet run` needs the checkout; that is a different reason and the one `docs/MCP-SETUP.md` now
  gives.
- **The two halves are complementary only as far as the runtime half is driven, and this note
  used to claim more than that.** It said the runtime test existed to catch "a spelling split
  across two lines". It did not: it sent `initialize`, `notifications/initialized` and
  `tools/list` and stopped, so it never entered a tool body. `Console` and `.WriteLine(…)` on
  two lines inside `SearchPowers` contains the token `Console.` on neither line, and reached a
  real client's stdout as message two with **both guards green**. The same write in
  `ListOptions` was caught, and only because `ReadEverything` calls it at startup — that is how
  narrow the cover was. The runtime test now calls all six tools and requires each answer to
  carry something only the far end of that body produces, since a call answered "unknown tool"
  would otherwise satisfy it while running no code at all. **The fix for a hole in the source
  scan is another driven path, never another regex**: the spelling after a multi-line one is a
  helper in another file, or a library.
- **The rules are found beside the binary, then upwards — never by walking up for a `.sln`.**
  That is the CLI's answer and it is wrong here: a client launches the published program from a
  directory of its own choosing and there may be no repository on the machine. `PROWLERS_RULES_DIR`
  and a first argument override it, and `RulesLocation.Find` **refuses** rather than guessing,
  because a repository built for a directory that is not there gets as far as a connected
  session and then answers every question with an error. **A directory the user named and that
  is not there is a refusal too, not a candidate that failed** — it used to fall through to the
  shipped copy, so a typo in the variable the setup guide tells a stuck user to set produced a
  working server on somebody else's rules and no message at all.
- **A test for any of this has to run the program, not the method it calls.** The startup check
  and the two refusals all had unit tests that passed while `Program.cs` was mutated back to the
  bug — a method nobody calls is not a check. Three tests start the built binary and read its
  exit code, and one of them bounds its own wait, because "the server started anyway" is the
  failure being looked for and a bare wait turns catching it into a run that never ends.
- **The startup check reads every rules file, and `ReadEverything` is what makes that true.**
  A repository loads each file lazily, so warming the tiers alone let a directory holding
  nothing but `tiers.json` start cleanly and then throw out of five of the six tools — the
  exact failure the check exists to prevent, passing its own check. The embedded guide is read
  there too, so the way it goes missing (a csproj edit) is a startup failure rather than a
  conversation that begins with an empty document.
- **`Judgement` guards every engine call and duplicates `BuildCommand`'s guarding on purpose.**
  What is shared is the part that matters — the engine — and the two reports are different
  documents: one names the files it wrote, this one carries a spending breakdown and no paths,
  because this program writes nothing. A figure the engine cannot supply comes back **null**,
  never 0.
- **The per-Power figures need not add up to the powers total, and the report says so.** Super
  Senses is one Power whose options are stored separately, so the group is costed once with one
  floor. The total is the engine's and the parts are indicative — the alternative is a total
  this program added up itself.
- **`search_powers` is deliberately dull**, and its most valuable field is `nothing_matched_by_name`.
  A search that always returns its five best rows reads as five answers however carefully the
  caution is worded, and the description naming something the rulebook does not have is exactly
  the one a model will build anyway. Matching is word by word with a shared-prefix rule, not by
  substring: substring matching answered "she bakes bread in the city" with **Plasticity**, and
  a match like that is worse than none because nothing in it looks wrong. (This note and
  `Mentions`' own summary both said *Elasticity*, which is not a Power in this rulebook —
  a reproduction searches for an entry that is not there.)
- **`Mentions` had no test at all until slice A1, and one line put the substring search back.**
  Every search test was either a positive assertion or a negative on a query whose words happen
  not to be substrings of anything, so the property the method exists for was unpinned. What
  holds it now is four fragments that occur inside a Power's name and nowhere in the rules files
  as a word — `city`/Plasticity, `ration`/Regeneration, `art`/Martial Arts,
  `kinesis`/Telekinesis — each with the positive control beside it, so the guard cannot be
  satisfied by a search that has stopped working. **Any change to matching has to keep the
  baker's sentence at `found: 0`.**
- **That flag says how the rows matched and never what to conclude**, and the first version got
  this exactly wrong. It attached "usually means the rulebook has no Power for this" — so
  "he can fly" returned Flight and then told the assistant there is no Power for flight, because
  "fly" is not a prefix of "Flight" and the entry matched on the word inside its own
  description. The three cases are told apart in `caution`, and `found: 0` is the only one that
  means the rulebook has nothing. **It is also computed over the whole result and then
  truncated**: computed after `Take(limit)`, a caller asking for one match turned a name match
  at position two into "nothing matched by name at all".
- **No server-side elicitation.** MCP can ask the user a question from the server; the question
  policy deliberately does not use it. The conversation is the client's, and a question asked
  through a schema-shaped dialogue is the questionnaire this design exists to avoid.
- Three traps that cost time here: the embedded resource name comes from **`RootNamespace`**
  (`ProwlersAndParagonsAutomation.Mcp`) and not from `AssemblyName` (`ProwlersAndParagons.Mcp`),
  which differ in this repository; `CallToolResult.IsError` is a **`bool?`** whose null means
  success, so `Assert.False` on it fails every passing call; and the root `.csproj` globs from
  the repository root, so `mcp/` needed its own `<Compile Remove>` like every other sibling.
- The tests live in `tests/ProwlersAndParagonsAutomation.Tests` beside `HeadlessBuildTests`,
  driven over a real pair of pipes with the SDK's own client. Only `web/` has a test project of
  its own, because rendering components needs one.


