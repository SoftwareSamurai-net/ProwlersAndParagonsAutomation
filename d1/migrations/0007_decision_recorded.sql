-- A decision is a fact, and until now this table kept only its consequence.
--
-- **Approving and rejecting were indistinguishable to the player, and that is the defect.**
-- Approve moves `pending_payload` into `approved_payload`; Reject clears the pending slot and
-- leaves the clone exactly where it was. Both then leave a row whose only readable difference is
-- one the player cannot see: the standing on their own screen is derived from the two slots, so a
-- resubmission that was turned down reverts to the identical sentence it showed before they sent
-- it — *Approved for Nightfall*, or *Not submitted*. A player who submitted a change and came back
-- an hour later could not tell that the GM had decided at all, let alone which way.
--
-- **One slot, overwritten, and deliberately not a history.** `0006` records why: approval history
-- and rollback are out of this design, because what the owner asked for is a decision queue and a
-- row per submission is a version-control system for characters. The same reasoning applies here —
-- this is the *last* decision, not a log of them.
--
-- **Nothing clears it, and nothing needs to.** A new submission fills `pending_payload`, which
-- shadows this in the standing the browser derives; the next decision overwrites it. A row that
-- was left and rejoined is a new row with both columns NULL, which is the correct answer for a
-- membership nobody has decided anything about yet.

-- 'approved' or 'rejected', and NULL for a membership that has never been decided — which is an
-- ordinary state, not a missing value: a player can join and be waiting on their first decision.
--
-- **A TEXT column rather than a boolean**, because the third state is the common one at the start
-- of every membership and `NULL` on a boolean would be a third state wearing a two-state type.
ALTER TABLE campaign_members ADD COLUMN decision TEXT;

-- When that decision was made, in Unix milliseconds.
--
-- **Not the same as `approved_at`**, which is when the clone was accepted and does not move when a
-- snapshot is turned down. This one moves on either decision, which is what makes it the answer to
-- "when did I last hear back".
--
-- **Stored and deliberately not on the wire yet.** `PROGRESS.md` records an open item — a GM
-- cannot be told a submission's age, and nothing prints one — and `AccountsContractTests` holds
-- the server to sending nothing the browser binds nothing to. So the fact is recorded here, where
-- it is free and where inventing it later would be impossible, and it stays off the wire until
-- something draws it. A column that costs nothing is not the same as a field that rots.
ALTER TABLE campaign_members ADD COLUMN decided_at INTEGER;
