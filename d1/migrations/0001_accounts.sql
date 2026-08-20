-- The whole account system's storage, and deliberately five small tables.
--
-- Two things are stored as a hash and never in the clear: the magic-link token and the
-- session secret. A dump of this database therefore lets nobody sign in as anybody — it is
-- the difference between leaking a list of email addresses and leaking a set of keys.
--
-- The character is stored as an opaque string. Nothing here knows what a Hero Point is, and
-- nothing here may learn: the engine is the authority on a character and it runs in the
-- browser. The server's whole job is to hand the same bytes back to the same person.

CREATE TABLE users (
    -- Opaque, and not the email. An id that is an address puts the address into every log
    -- line and every foreign key, and there is no way to take it back out later.
    id           TEXT    PRIMARY KEY,
    email        TEXT    NOT NULL UNIQUE,
    display_name TEXT,
    created_at   INTEGER NOT NULL
);

-- A magic link in flight. Rows are short-lived by design: a token is good for one use and
-- fifteen minutes, whichever comes first.
CREATE TABLE login_tokens (
    -- SHA-256 of the token, hex. The token itself exists only in the email.
    token_hash TEXT    PRIMARY KEY,
    email      TEXT    NOT NULL,
    expires_at INTEGER NOT NULL,
    -- Set on first use. A forwarded email is then a dead link rather than a permanent key.
    used_at    INTEGER
);

CREATE INDEX idx_login_tokens_expires ON login_tokens (expires_at);

CREATE TABLE sessions (
    -- SHA-256 of the cookie's secret, hex. Same reasoning as the token above.
    id_hash    TEXT    PRIMARY KEY,
    user_id    TEXT    NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    expires_at INTEGER NOT NULL,
    created_at INTEGER NOT NULL
);

CREATE INDEX idx_sessions_user    ON sessions (user_id);
CREATE INDEX idx_sessions_expires ON sessions (expires_at);

-- One character per account, on purpose.
--
-- A list is a different interface and a different set of screens, and it is much easier to
-- get right once one character round-trips. `user_id` being the primary key is what makes
-- that a fact about the storage rather than a convention somebody has to remember.
CREATE TABLE characters (
    user_id    TEXT    PRIMARY KEY REFERENCES users (id) ON DELETE CASCADE,
    -- Exactly what the browser's local storage holds: the inputs, versioned, as written by
    -- CharacterStore. Opaque here. Parsing it would mean a second place that knows the shape
    -- of a character, and the two would drift.
    payload    TEXT    NOT NULL,
    updated_at INTEGER NOT NULL
);

-- Rate limiting, so an address cannot be mailed a link every second by anybody who knows it.
-- Keyed `scope:value` — one table rather than one per scope, because the counting is identical
-- and the scopes are decided in code.
CREATE TABLE login_attempts (
    key          TEXT    PRIMARY KEY,
    count        INTEGER NOT NULL,
    window_start INTEGER NOT NULL
);
