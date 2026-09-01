-- A campaign holds a clone of a character, and a player's edits arrive as an approval request.
--
-- **Fork and pull request, for characters.** A player builds freely in their own rows and needs
-- nobody's permission to do it; when they want the change to count at the table they *send it for
-- approval*, which writes a snapshot into the campaign. The GM sees a diff and accepts or rejects
-- the whole snapshot. Accepting replaces the campaign's clone. Both sides keep a copy, and neither
-- side's copy is ever the other's row.
--
-- **The clones are NOT in `characters`, and that is the whole reason this table exists.** The cap
-- in `db.putCharacter` is `SELECT COUNT(*) FROM characters WHERE user_id = ?`, so a clone stored
-- there would count against the GM's own `character_limit` — a GM with six players would hit their
-- five-character cap before building a single NPC. A clone is not one of the GM's characters; it is
-- the campaign's record of somebody else's.
--
-- **The server still does not know what a character is.** `approved_payload` and
-- `pending_payload` are the same opaque strings `characters.payload` holds, written verbatim and
-- handed back verbatim. Nothing here parses one, and nothing here compares two: the diff the GM
-- reads is computed in the browser, by the engine, which is the authority on what a character
-- costs and whether it is legal.
--
-- **Two owners on one row, so that every query stays scoped to whoever is asking.** `gm_user_id`
-- owns the campaign; `player_user_id` owns the character. The GM's reads are
-- `WHERE gm_user_id = ?`, the player's are `WHERE player_user_id = ?`, and no statement in
-- `worker/db.js` lets either name a row belonging to somebody else. That is what keeps the
-- promise in `docs/CHARACTERS-API.md` — the payload the GM reads is one the player deliberately
-- sent them, not one this server went and fetched out of their account.
--
-- **`id` is minted here rather than by a client, and it is the only id in this system that is.**
-- Every other id is the client's (`c_`, `g_`) so a PUT is idempotent. A membership is not created
-- by a PUT to a known address: it is created by redeeming a join code, and the server is the only
-- party that can see both accounts at that moment. Minting it here is also what keeps
-- `player_user_id` off the wire — the GM approves `m_…`, and never learns an account id.

-- `m_` plus 22 URL-safe characters, the `c_`/`g_` shape with a third letter, so none of the three
-- can be passed where another is meant.
CREATE TABLE campaign_members (
    id               TEXT    NOT NULL PRIMARY KEY,

    -- Which campaign, and whose. `gm_user_id` is denormalised out of `campaigns` on purpose: the
    -- GM's list is `WHERE gm_user_id = ?`, and a join to `campaigns` to discover the owner would
    -- be a query whose scoping depended on another table's row still being there. Deleting a
    -- campaign must not silently widen who can read a membership.
    campaign_id      TEXT    NOT NULL,
    gm_user_id       TEXT    NOT NULL REFERENCES users (id) ON DELETE CASCADE,

    -- Whose character, and which one. `character_id` is the player's own `c_…` — a string to
    -- store and return, never joined to `characters`: the player may build in one browser and
    -- have no account row for that character at all.
    player_user_id   TEXT    NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    character_id     TEXT    NOT NULL,

    -- Client-supplied, opaque, exactly as `characters.label` is. The server cannot read a name
    -- out of a payload it never parses.
    label            TEXT    NOT NULL,

    -- The campaign's clone: the last snapshot the GM accepted. NULL until the first approval,
    -- which is an ordinary state — a player can join and be waiting on their first decision.
    approved_payload TEXT,
    approved_at      INTEGER,

    -- The snapshot waiting for a decision, or NULL when there is none. **One slot, and
    -- resubmitting overwrites it.** There is deliberately no history: a second row per submission
    -- would make this a version-control system for characters, and the owner asked for a decision
    -- queue.
    pending_payload  TEXT,
    pending_at       INTEGER,

    -- **The compare-and-swap token, and it is the whole of one real defect's fix.** Without it:
    -- the GM reads snapshot A, the player resubmits B, the GM clicks Approve, and B is approved
    -- unseen. So a submission increments this, Approve and Reject send back the number they were
    -- shown, and the UPDATE's `WHERE` carries it — a mismatch matches no row and is refused.
    --
    -- **Monotonic, and never reset when the pending slot is cleared.** Resetting it would let a
    -- resubmission after an approval reuse a number the GM might still be holding on screen,
    -- which is the same defect with an extra step.
    pending_version  INTEGER NOT NULL DEFAULT 0,

    joined_at        INTEGER NOT NULL
);

-- One membership per character per campaign. `player_user_id` is in the key as well as
-- `character_id`, so one account cannot squat on an id belonging to another and block a join it
-- has no business knowing about.
--
-- **`gm_user_id` leads the key, and leaving it out was a real hole rather than a tidiness point.**
-- `campaigns` is `PRIMARY KEY (user_id, id)`, so a `g_…` is unique *per account* and two GMs may
-- hold the same one — a `g_…` is handed to every member of a campaign in the join response and
-- travels in an exported character's `CampaignId`, so knowing one takes no work. Without this
-- column the two campaigns share a key here: a player joining the second one conflicts with their
-- row in the first, `ON CONFLICT … DO UPDATE` hands back *that* row, and every snapshot they send
-- afterwards is delivered to a GM they never joined — while the GM whose code they redeemed sees
-- an empty inbox. The whole "two owners on one row" promise above is this column being in the key.
CREATE UNIQUE INDEX campaign_members_one_per_character
    ON campaign_members (gm_user_id, campaign_id, player_user_id, character_id);

-- The GM's read: every membership of a campaign they own.
CREATE INDEX campaign_members_by_gm ON campaign_members (gm_user_id, campaign_id);

-- The player's read: every membership of a character they own.
CREATE INDEX campaign_members_by_player ON campaign_members (player_user_id, character_id);

-- The join code: a rotatable shared secret that is how somebody joins a campaign.
--
-- **It cannot live inside `payload`.** Redeeming a code means finding the campaign it belongs to,
-- which is a query — and the payload is the one thing in this system no query may look inside. So
-- the code is a column, and it is the only field of a campaign the server can read.
--
-- **It is a capability, not an identifier**, which is why it is separate from `id` and why it can
-- be replaced. An id leaked is leaked for ever; a code leaked is replaced with one POST, and every
-- existing membership survives because membership does not re-check the code after it is redeemed.
--
-- **NULL is allowed, and means a campaign nobody can join yet.** Every campaign written through
-- `db.putCampaign` gets one on insert, but an ALTER cannot invent randomness for rows that already
-- exist — a campaign made before this migration has no code until it is next written or rotated,
-- which is a state the screen reports rather than a state that breaks anything. A `NOT NULL
-- DEFAULT ''` would be worse: every such campaign would share one code, and a unique index over
-- them would refuse the second row.
--
-- **The uniqueness is enforced here rather than checked before the insert.** A read that found no
-- campaign with a code, followed by a write that trusted it, is the race every other statement in
-- `worker/db.js` is written to avoid; the index is the check, and `db.rotateJoinCode` retries on
-- conflict. SQLite treats NULLs as distinct in a unique index, so any number of code-less
-- campaigns coexist.
ALTER TABLE campaigns ADD COLUMN join_code TEXT;

CREATE UNIQUE INDEX campaigns_by_join_code ON campaigns (join_code);
