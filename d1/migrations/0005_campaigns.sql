-- Campaigns, beside characters, and a column on `characters` that says which one a character is in.
--
-- **The server still does not know what a character is, and this migration does not change
-- that.** A campaign row has exactly the five columns a character row has and the same meaning
-- for each: who it belongs to, an id the client picked, a label the client supplied, an opaque
-- payload, and when it was last touched. Nothing here understands a tier, a Trait Cap or a
-- budget — those are inside `payload`, which no query in this system ever parses.
--
-- **`characters.campaign_id` is a duplicate of something inside the payload, deliberately.** The
-- server cannot derive it, because deriving it would mean reading the payload; so the client
-- sends it alongside, exactly as it sends `label`. It is a string to store and return, never a
-- key to join on and never a value to validate. There is deliberately no foreign key: a campaign
-- can be deleted while characters still name it, and those characters are *reported* as naming a
-- campaign that is not here rather than being quietly edited — the same rule an unknown tier
-- follows. A `REFERENCES` clause here would turn that reported state into either a refused
-- delete or a silent cascade, both of which decide something on the player's behalf.
--
-- **No table rebuild, unlike 0002.** `campaign_id` is not part of a primary key, so SQLite can
-- add it in place. An existing row gets NULL, which is exactly right: a character written before
-- campaigns existed belongs to no campaign.

ALTER TABLE characters ADD COLUMN campaign_id TEXT;

-- `g_` plus 22 URL-safe characters, mirroring the `c_` shape of a character id, so that neither
-- can be passed where the other is meant and the server can validate both with one pattern.
CREATE TABLE campaigns (
    user_id    TEXT    NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    id         TEXT    NOT NULL,
    label      TEXT    NOT NULL,
    -- Exactly what the browser's local storage holds: a version and the campaign's own fields,
    -- as written by StoredCampaign. Opaque here, same as a character's payload.
    payload    TEXT    NOT NULL,
    updated_at INTEGER NOT NULL,
    PRIMARY KEY (user_id, id)
);
