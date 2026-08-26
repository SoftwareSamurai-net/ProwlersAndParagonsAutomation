# s8-search: closing part of the `search_powers` tie-ordering gap

This is the account for the slice that improved `mcp/CharacterTools.cs`'s `search_powers`
scorer, measured against `tests/ProwlersAndParagonsAutomation.Tests/PowerSearchExpectations.cs`
(33 labelled queries) per PROGRESS.md item 4. Branch `s8-search`.

## Method

1. Ran `ReportTheCurrentScore` (via a scratch console harness that calls `CharacterTools`
   directly, since `dotnet test`'s console logger does not surface xunit.v3 diagnostic messages)
   to get the baseline table.
2. Read the failing rows to find the *mechanism*, not just the two named examples — matched
   terms per row, via `search_powers`'s own `matched_terms` field.
3. Formed and tested three hypotheses in isolation, each measured against the full 33-item set
   before deciding whether to keep it.
4. Kept the one that raised the count with no expectation moving from met to unmet; reverted the
   other two and recorded why.
5. Raised the ratchet from 24 to 25.

## Before: baseline table (24/33 met)

```
MET  WANT (top N)  AT   QUERY                                                   EXPECTED               TOP 5 RETURNED
NO   top 8         19   he can shut off everyone's powers in the area at once   nullify                buff, hyper_breath, shockwave, darkness, slick
NO   top 5         -    he moves faster than anyone can follow                  super_speed            aura, running, tracer, animal_mimicry, banish
NO   top 3         -    he shoots fire from his hands                           blast                  elemental_control, two_fisted
NO   top 8         -    he throws a devastating punch that puts his opponent... strike                 animal_empathy, blind_fighting, nullify, stun, super_senses_microscopic_vision
NO   top 3         -    his wounds knit themselves back together over time      regeneration           time_stop, time_travel, possession, precognition, banish
NO   top 3         3    she can fly through the sky                             flight                 blink, darkness, elemental_control, flight, form_gaseous
NO   top 3         18   she can read anyone's mind from across the room         telepathy              mind_blast, mind_control, psi_screen, super_senses_hypersensitive_touch, aura
NO   top 5         -    she lays hands on someone and mends their wounds        healing                life_drain, slay, two_fisted
NO   top 3         10   walks through walls                                     phasing                wall_crawling, super_senses_hypersensitive_touch, blink, constructs, darkness
(24 further rows met, omitted here — unchanged by this change except "flight" and "dispel", see below)
```

Full baseline: 24 of 33 met, matching the number recorded in PROGRESS.md item 4 and in
`PowerSearchEvaluationTests.Baseline` before this change.

## Hypothesis

PROGRESS.md item 4 names two examples but the actual mechanism, read off `matched_terms` on
several failing rows, is broader: the stopword list already exists specifically to strip words
that "carry no information about a Power" (its own comment, of "someone"/"something"/"anything"),
but it stops short of most ordinary prepositions and indefinite pronouns. `Mentions` debug output
for "walks through walls" shows **22 Powers** matching, 20 of them on the single word "through"
alone — a preposition that happens to appear in eighteen unrelated Power descriptions ("pass
**through** solid matter", "run **through** the streets", "see **through** walls" as an X-ray
description, etc.). The same shape recurs across the set: "than" and "anyone" (`he moves faster
than anyone can follow` — 17 of 25 candidates tie on one of those two alone, and Super Speed
itself doesn't even appear, since neither word is in its description at all), "down"/"another"
(`she can shut down another power...`), "over" (`she can jump over a building...`).

None of this is unique to the two examples PROGRESS quotes — it is the same class of word the
stopword list already targets, just incompletely.

## Change kept: widen the stopword list

Added to `Stopwords` in `mcp/CharacterTools.cs`: `through, than, anyone, everyone, over, under,
around, against, down, back, once, another, else, somewhere, before, after, without, across,
toward, towards, upon, near`.

### After (25/33 met)

```
MET  WANT (top N)  AT   QUERY                                                   EXPECTED               TOP 5 RETURNED
NO   top 8         -    he can shut off everyone's powers in the area at once   nullify                buff, darkness, hyper_breath, shockwave, slick
NO   top 5         -    he moves faster than anyone can follow                  super_speed            blink, evasion, precognition, running, super_senses_thermal_vision
NO   top 3         -    he shoots fire from his hands                           blast                  elemental_control, two_fisted
NO   top 8         -    he throws a devastating punch that puts his opponent... strike                 blind_fighting, stun, telekinesis, ventriloquism
NO   top 3         -    his wounds knit themselves back together over time      regeneration           time_stop, time_travel, banish, hard_to_kill, life_drain
NO   top 3         11   she can read anyone's mind from across the room         telepathy              mind_blast, mind_control, psi_screen, super_senses_hypersensitive_touch, flight
NO   top 5         -    she lays hands on someone and mends their wounds        healing                life_drain, slay, two_fisted
NO   top 3         -    walks through walls                                     phasing                wall_crawling, constructs, super_senses_hypersensitive_touch, super_speed
yes  top 3         0    she can fly through the sky                             flight                 flight                                        <- newly met
yes  top 8         0    she can shut down another power that's affecting som...  dispel                 dispel, form_energy, form_gaseous, two_dimensional  <- was already met (pos4), now pos0
(23 further rows, all still met, none newly failing)
```

**One expectation moved from unmet to met**: `"she can fly through the sky"` → Flight, which was
tied for 3rd with three coincidental "through"-only matches (`blink`, `darkness`,
`elemental_control` — all alphabetically ahead of `flight`) and is now the sole match, since
nothing else in the query is a coincidental filler word.

**No expectation moved from met to unmet.** Several already-met rows improved their position
(e.g. `dispel` from 4th to 1st, `strike`'s query lost some noise) without changing pass/fail.

Ran the fuller `dotnet test` suite (`tests/ProwlersAndParagonsAutomation.Tests`,
`tests/ProwlersAndParagons.Web.Tests`) in Release/CI mode: both projects print `Passed!`, 3734 +
482 = 4216 tests, zero `Catastrophic`. One existing test needed updating (see below); everything
else was unaffected.

### Test that needed updating

`McpServerTests.AThinMatchCanBeSeenToBeThin` searched literally `"walks through walls"` to
reproduce PROGRESS.md item 4's own tie-flood on "through". Once "through" is a stopword that
query no longer produces one (its remaining terms, "walks"/"walls", don't tie two dozen Powers
together), so the test's premise stopped holding — not because the property under test (a thin,
alphabetically-ordered tie is reported honestly by `matched_terms` / `more_beyond_these` /
`caution`) became false, but because its example stopped demonstrating it.

Replaced the query with one built on the word already named elsewhere in the same file as
"common enough to overflow the 25-row ceiling" (`ALimitOutsideTheRangeIsBroughtInsideIt`'s
`WideQuery = "rank"`, which appears in 50 of 141 Power descriptions): `"it happens at your rank,
and it is quite a boost"`. "Rank" alone still ties dozens of Powers at 2 points each; "boost" adds
one row that matches by *name* too, which is what keeps the caution in the "closest entries"
branch (the property this test is actually about) rather than the "nothing matched by name"
branch a query with no name match at all would trigger.

## Changes tried and reverted

Both were tested by editing `mcp/CharacterTools.cs`, rebuilding, and re-running the full 33-item
set through the same scratch harness before touching the committed tree — neither was ever
committed.

### 1. Symmetric stemming (stem both sides of a word comparison)

**Hypothesis**: `Mentions` only stems the *query* term, never the rulebook *text* word it is
compared against, so a query for "mind" never finds Telepathy's own "You can read **minds**" —
the description pluralises the word and the query doesn't, and stemming only runs one way.
Confirmed by inspection: `Stem("minds")` correctly yields `"mind"` via the existing `-s` rule, so
comparing `Stem(word) == term` (in addition to the existing `word == term || word == stem`
checks) would catch it.

**Result**: 24/33 (down from 25/33 with the stopword change alone — net **-1**). Fixing "mind" →
"minds" also strengthened a coincidental match: `cloud_minds`'s own *name* contains "Minds"
(plural), which the same symmetric-stemming fix now recognises as matching the query term "mind"
— giving Cloud Minds a **name**-level match (10 points) it didn't have before, on the strength of
the identical bug fix. That pushed `telekinesis` (a query about moving objects with your mind)
from met (position 4, top 5) to unmet (position 5), and left `telepathy` still unmet, ranked
behind both `mind_blast`/`mind_control` (genuine name matches) *and* the newly-strengthened
`cloud_minds`. This is the same failure shape PROGRESS.md item 4 records for the previous
attempt at this problem — fixing one case in isolation moved a case nobody was looking at.
**Reverted.**

### 2. Tie-break by matched-term breadth before raw points

**Hypothesis**: sort ties by `MatchedTerms.Count` (how many of the caller's words matched) before
`Points` (which field they matched in), instead of the other way around, on the theory that a
row matching two of the query's real words should usually beat a row matching one — even a
strong one.

**Result**: 25/33, same as points-first (net **0**) — the composition of the top-5 columns
shifted for several rows (e.g. `animal_control` moved from 1st to 2nd, `telekinesis` moved from
5th to 1st) but no expectation crossed the met/unmet line in either direction. Rejected anyway:
this reorders *every* tie in the tool, not just the ones in this set, and it inverts the
documented field hierarchy ("name match beats id match beats tag beats category beats
description" — `Score`'s own summary) for any pair where a two-word description match and a
one-word name match compete: a single genuine name match (10 points) would now rank *below* two
coincidental description words (4 points). With zero measured benefit on the labelled set and a
real behavioural change to a documented, tested contract, this was reverted rather than kept on
spec.

## The two named cases: still unmet, and why

- **"walks through walls" (wants Phasing, top 3).** Phasing's printed description says "pass
  through solid matter but not force fields" — it never uses the word "wall" or "walls" at all.
  Once "through" is a stopword (removing the flood of 20 unrelated Powers that only shared that
  filler word), Phasing has no remaining word in common with the query and simply isn't returned.
  No change to *matching* can close this gap without inventing vocabulary the rulebook text does
  not carry (e.g. adding a "wall" tag to Phasing's `data/rules/powers.json` entry) — which is a
  change to the data, not the scorer, and out of scope here.
- **"he shoots fire from his hands" (wants Blast, top 3).** Blast's entire rules-relevant text is
  "A damaging ranged attack. Name the type of damage it inflicts when you buy it." — the type of
  damage (fire, cold, force, whatever) is explicitly the *player's* choice at creation time, never
  printed. No word in the query ("shoots", "fire", "hands") appears in Blast's description, tags
  (`attack, ranged, damage, energy`), or name. Same conclusion: unreachable by word-matching
  without adding invented vocabulary to the data.

Both were expected to still fail going in (PROGRESS.md item 4 quotes them as the two examples
that "fail as expected") and both do. This matches the task's own framing: an honest report that
these two remain open, rather than a targeted hack that would only ever prove itself against the
two sentences it was built from.

## `worker/search.js` — not touched, and why

`worker/search.js` is a deliberate *port* of `Mentions` for the `/rules` full-book search, with
its own, differently-tuned stopword list (documented in `CLAUDE.md`: it keeps "power", "powers",
"character" and "super" because those are section headings a reader of the whole book will
actually type, where the Powers search drops them as uninformative). The stopword additions here
are prepositions and indefinite pronouns ("through", "than", "anyone", "over", …) that are
generic English function words, not domain-specific to either corpus — so in principle the same
words are just as uninformative for a full-book search. In practice this change is **not ported**
here, for two reasons:

1. `worker/` is explicitly out of scope for this task except where the port itself is the right
   call, and porting a stopword-list change is a small, low-risk one — but doing it inside this
   slice would touch a file this task's instructions list as off-limits by default, for no
   measured benefit (there is no labelled evaluation set for the rulebook search to measure
   against, so "helps" here is a guess rather than a measurement, which is exactly the standard
   this whole exercise is trying to avoid).
2. The two corpora behave differently under the same word. `CLAUDE.md` already documents that
   "city" reaches real matches in the rulebook prose (twenty-one of them) where it would be pure
   noise in the 141 short Power entries — the same could easily be true of some of the words added
   here (e.g. "before"/"after" are far more likely to appear in genuinely relevant passages of a
   whole rulebook chapter than in a two-sentence Power description). Porting blind, without the
   Mentions-fragment-style positive/negative test pairs `worker/search.test.mjs` already uses to
   pin that corpus's behaviour, risks quietly narrowing a search that currently works.

If this stopword list is ever revisited for the rulebook search, it should be measured against
`worker/search.test.mjs`'s own fixtures the same way this change was measured against
`PowerSearchExpectations`, not copied over on the strength of "it helped over there."

## Mutation checks (discipline, not optional)

Both watched to fail, then restored to the exact committed state (`git status --short` empty,
`git diff --stat` empty) before the suite was re-run green.

### Ratchet: forced the internal row count to 1

Changed `var wanted = Math.Clamp(limit, 1, 25);` to `var wanted = 1;` (the exact mutation
PROGRESS.md item 4 already records reproducing this bug with).

```
search_powers now meets only 17 of 33 labelled expectations, down from the recorded baseline of 25.
```

`TheScoreNeverGetsWorse` failed with that exact count in the message, as expected — confirming
the assertion reads live search output on every run, not a cached number.

### `Mentions` guard: put substring matching back

Replaced the body of `Mentions` with `return text.Contains(term,
StringComparison.OrdinalIgnoreCase);`.

- `TheSentenceAboutABakerMatchesNoPowerAtAll` — **failed** (`found` went from 0 to 1; "she bakes
  bread in the city" matched Plasticity on "city", as PROGRESS.md's own history records).
- `AWordThatOnlyOccursInsideALongerWordIsNotAMatch` — **all 7 cases failed**: `art`→martial_arts,
  `city`→plasticity, `ration`→regeneration, `kinesis`→telekinesis, `formation`→
  transformation_shapeshifting, `generation`→regeneration, `visibility`→invisibility all came
  back as substring matches.

Both restored with `git checkout -- mcp/CharacterTools.cs` (the working tree had no other
uncommitted changes at either point, confirmed with `git status --short` first), and the full
`dotnet test --configuration Release -p:ContinuousIntegrationBuild=true` suite re-run green
afterwards (two `Passed!` lines, 4216 tests, zero `Catastrophic`).

## Result

- `PowerSearchEvaluationTests.Baseline` raised from 24 to 25.
- `mcp/CharacterTools.cs`: 22 words added to the `Stopwords` set, one line of explanatory comment.
- `tests/ProwlersAndParagonsAutomation.Tests/McpServerTests.cs`: `AThinMatchCanBeSeenToBeThin`'s
  query and docstring updated to keep demonstrating the same property against a word that still
  produces the tie it exists to characterise.
- No change to `worker/`, `.editorconfig`, `data/rules/`, or the scoring formula's field-weight
  hierarchy (name > id > tag > category > description) — only which words are allowed to compete
  for those points in the first place.
