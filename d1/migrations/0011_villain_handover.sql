-- Two columns on `campaign_members`, so approving a Villain can hand it to the campaign's owner.
--
-- **The owner's rulings of 2026-10-01 (`PROGRESS.md`, the nemesis handover):** an approved
-- Villain stops being the player's and becomes the GM's for good, inside the GM's character cap;
-- the player keeps no sheet and sees only its name. A Hero's approval is unchanged — a clone, and
-- the player keeps their character.
--
--   * `pending_kind`   — what the waiting snapshot is: the same opaque palette word
--                        (`villain` or `hero`) every save already sends beside a character as
--                        `characters.kind` (0008). **Sent with the snapshot and stored with it,
--                        never read out of the payload and never taken from `characters`**:
--                        this server does not parse a character, the player may hold no
--                        account row for the character at all, and the player's own row
--                        describes their sheet *now*, not the snapshot the GM is deciding about.
--                        A player who flips the switch after sending must not change what
--                        approving does. Written by the same statement that writes
--                        `pending_payload`, so the two always describe one snapshot. NULL for a
--                        submission made before this migration, which approves as a Hero does —
--                        the behaviour every such row was sent under.
--   * `handed_over_at` — when an approval moved the Villain to the GM's account, or NULL. Set
--                        once and never cleared: there is no hand-back. It is what tells both
--                        sides this membership is a nemesis rather than an ordinary approval,
--                        what refuses any further submission into it, and what refuses the
--                        player's browser saving the character back under its old id.
--
-- Plain `ALTER TABLE`s, for the reason 0007 and 0008 give: nothing is part of a key, so nothing
-- has to be copied and nothing can be lost copying it.

ALTER TABLE campaign_members ADD COLUMN pending_kind TEXT;
ALTER TABLE campaign_members ADD COLUMN handed_over_at INTEGER;
