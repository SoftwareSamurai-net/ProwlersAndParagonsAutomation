-- The character list, and the cap that makes "list" mean something.
--
-- Two changes, bundled because they arrived from the same requirement: an account holds many
-- characters now, not one, and an account can be told to hold more than the ordinary five. See
-- docs/CHARACTERS-API.md for the contract this schema serves.
--
-- SQLite cannot add a column to a primary key, so `characters` is rebuilt rather than altered:
-- a new table with the shape it should always have had, every existing row copied across, the
-- old table dropped, the new one renamed into its place. Nothing here is destructive on its own
-- terms — a row that existed before this migration runs still exists after it, under a
-- generated id and a label that says exactly what it is: a character with no name of its own
-- yet, not a character that has lost one.

ALTER TABLE users ADD COLUMN character_limit INTEGER NOT NULL DEFAULT 5;

-- `c_` plus 22 URL-safe characters is the id shape the contract defines — the same shape
-- `crypto.js` mints for a user id, minted client-side from here on. A migration has no access
-- to that code and SQLite has no base64 built in, so an existing row is given a lower-case hex
-- id instead: still 22 characters, still drawn only from the URL-safe alphabet the server
-- validates against, just from a narrower part of it.
CREATE TABLE characters_new (
    user_id    TEXT    NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    id         TEXT    NOT NULL,
    label      TEXT    NOT NULL,
    -- Exactly what the browser's local storage holds: the inputs, versioned, as written by
    -- CharacterStore. Opaque here, same as before this migration.
    payload    TEXT    NOT NULL,
    updated_at INTEGER NOT NULL,
    PRIMARY KEY (user_id, id)
);

INSERT INTO characters_new (user_id, id, label, payload, updated_at)
SELECT user_id, 'c_' || lower(hex(randomblob(11))), 'Unnamed character', payload, updated_at
FROM characters;

DROP TABLE characters;

ALTER TABLE characters_new RENAME TO characters;
