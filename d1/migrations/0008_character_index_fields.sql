-- Three more columns on `characters`, so a list of thirty can say what each one is.
--
-- **The same bargain `label` and `campaign_id` already struck, and for the same reason.** A row in
-- the manager may not cost a payload read: `SavedCharacters` keeps an index precisely so that
-- drawing a list does not deserialize, cost and validate every character in it, and at
-- twenty-nine characters that is the difference between a list and twenty-nine fetches. So
-- anything a row prints has to travel *beside* the payload, supplied by the client, because the
-- server cannot read it out — it never parses a character and this migration does not change
-- that.
--
-- **What they are:**
--
--   * `kind`     — which palette the character is built in. Two words in practice; an opaque,
--                  length-bounded string here, exactly like `label`.
--   * `tier_id`  — the tier the character is built to. An id in `data/rules/tiers.json`, which is
--                  a file this server has never read and must not start reading. It is not a key
--                  in any table here, so there is nothing to join it to and no reference to check.
--   * `spent`    — Hero Points, as the engine priced them, or NULL.
--
-- **NULL on `spent` is a real answer and not a missing one.** The engine refuses to price an
-- incomplete selection rather than guessing at it — a variable-cost Power with no variant chosen
-- throws — so the honest thing for the client to send is nothing, and the honest thing for a row
-- to draw is a name and no figure. That is the same rule the front door follows. A column that
-- defaulted to 0 would turn "we do not know" into "this character costs nothing".
--
-- **And NULL is what every existing row gets, which is also right.** A character written before
-- this migration has no index entry for these fields, and it acquires one the next time it is
-- saved — which is every autosave. Nothing backfills, because backfilling would mean parsing
-- twenty-nine payloads on the server, which is the thing this column set exists to avoid.
--
-- **No table rebuild.** None of the three is part of a primary key, so SQLite adds them in place —
-- the same reason 0005 could add `campaign_id` without the rebuild 0002 needed.

ALTER TABLE characters ADD COLUMN kind TEXT;
ALTER TABLE characters ADD COLUMN tier_id TEXT;
ALTER TABLE characters ADD COLUMN spent INTEGER;
