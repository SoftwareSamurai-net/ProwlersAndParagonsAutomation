-- One column on `campaign_members`: which Hero a Villain is the nemesis of.
--
-- **The owner's asks of 2026-10-02 (`PROGRESS.md`, a nemesis keyed to the Hero it hunts):** a
-- Villain sent to a campaign names one of the sender's own Heroes in that game, the GM's table is
-- laid out with each nemesis under its Hero, and the GM may re-key a nemesis that is theirs.
--
--   * `nemesis_of` — the id of another membership in the same campaign: the Hero's. **A membership
--                    id, never a character id**, because it is a row this server already scopes and
--                    can check — same player, same GM, same campaign — without reading either
--                    character. Written with the snapshot by the player, and afterwards only by the
--                    GM once the Villain is theirs. NULL for a Hero and for every row written before
--                    this migration. Not a foreign key, for the reason `characters.campaign_id` is
--                    not one: a Hero leaving the game must not delete or rewrite its nemesis, and
--                    the screen reports a key naming nobody rather than this table refusing it.
--
-- A plain `ALTER TABLE`, for the reason 0007, 0008 and 0011 give.

ALTER TABLE campaign_members ADD COLUMN nemesis_of TEXT;
