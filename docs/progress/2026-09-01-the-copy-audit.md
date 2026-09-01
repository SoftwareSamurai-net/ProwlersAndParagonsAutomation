# The copy audit: the app's prose is mostly sound, and three sentences were written out twice

**Asked for after the handout's title was rejected as evocative rather than instructive** — the
question was whether the same fault was elsewhere. 61 prose blocks of 55+ literal characters across
26 pages and components, read as rendered text.

**The answer is mostly no, and that is the finding rather than a let-off.** Most of what reads like
explanation is doing definitional work a player needs — *"A Condition is always in effect; a Plot
Hook is something the GM can reach for"*, *"A package buys the ranks it grants, at a discount"* —
and the rulebook citations stay, because the guides already sanction those on screen and the
`(Ch.6)` in `Gear.razor` is one. One sentence was the app arguing its own case: `ReplayVerdict`'s
*"Ch.9 builds a Villain by exactly the Hero rules, **so the budget finding stands**"*. The citation
kept, the clause about its own output gone.

**Three sentences were written out twice, which is the real defect and is not about style.**

| Said twice | Now |
|---|---|
| The print hint, on both pages that print a sheet | `PrintOnePageHint` |
| The recordings standfirst, on both pages that list recordings | `RecordingsIntro` |
| All three refusal branches of a gated page, in `AdminOnly` and again in `Admin` | `GateRefusal` |

- **`Admin` could not simply be wrapped in `AdminOnly`**, which is why the copy was pasted rather
  than shared: it needs the request state for its own error-row fetch as well as for the gate. So
  `GateRefusal` takes the state as a parameter and owns the three panels, and neither page's
  "Looking…" line moves — those two differ on purpose, because each names what it is waiting for.
- **One pair was not byte-identical.** One page emphasised *"The figures are not recorded"* and the
  other did not, so a comparison of source would have called them different while a reader could
  not tell them apart. The guard therefore compares *rendered* text, and the shared component keeps
  the emphasis: a phrase either matters or it does not.
- **The reassurance stays.** The audit called *"Nothing is wrong — there is simply nothing here for
  this account"* an answer to an unasked question, and that was wrong: on a screen that has just
  told somebody a page is not for them, *did I break something* is a question they are asking. It
  is recorded here because reversing a finding quietly is how a list of findings stops being worth
  reading.
- **What is left is a house phrase rather than a duplication.** *"Nothing is wrong"* opens six more
  strings — four in `SignIn`, one in `CampaignApproval`, one in `ReplayConversation` — all different
  sentences. Nothing drifts, so nothing is broken; noted in case the tic is not wanted.

**`NoTwoPagesCarryTheSameSentence` holds it**, at 55 characters because short strings repeat
legitimately and a threshold that fires on a button label is one that gets weakened. Broken twice:
pasting the print hint back names both files, and making the extractor keep nothing trips the
positive control — *"Only 0 prose blocks were found, so this scan is not reading the pages"* —
which is the failure a duplicate-finder has no other way to report.
