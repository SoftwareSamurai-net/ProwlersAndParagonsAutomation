-- The owner's half of a failure, and it is one small table on purpose.
--
-- This server's only log was a live tail: close it and the error is gone, so any failure nobody
-- happened to be watching for was unrecoverable. This is the durable half. The visitor's half is
-- a category and a reference in the 500 body — see `worker/errors.js`.
--
-- **There is no admin endpoint and there is not going to be one.** `Identity` carries a key and a
-- name and no role — there is a test asserting the wire identity holds nothing else — so "am I an
-- admin" is not a question the client can ask, and inventing a role to answer it is a far larger
-- change than this needs. The precedent is `users.character_limit`, raised by hand in SQL on the
-- reasoning that a cap you can raise on yourself is not one. This is read the same way:
--
--   wrangler d1 execute prowlers-and-paragons --remote \
--     --command "SELECT * FROM error_log ORDER BY last_at DESC"
--
-- **One row per (category, route), counted rather than appended.** A failing dependency throws on
-- every request, and a log with a row per occurrence turns one outage into a full database. The
-- key is the pair, so the table is bounded by construction: four categories times the handful of
-- route patterns `routePattern` can return, and nothing a caller sends can add a row. `/api/
-- characters/{id}` is stored as that pattern and never as the id, which is both what bounds it
-- and what keeps a caller-chosen string out of here.
--
-- **`occurrences` is the record of what was dropped.** Only the most recent failure's `kind`,
-- `detail` and `reference` are kept; the rest are folded into the count. A silently truncated log
-- reads as a quiet period, so the count is what stops this one doing that — a row saying 4,000
-- occurrences since `first_at` is the outage, and the one message beside it is a sample of it. A
-- reference quoted by a visitor that does not match the row is one of the occurrences folded in.

CREATE TABLE error_log (
    -- One of the four in `CATEGORIES`. Never a free-text description of what went wrong: the
    -- closed set is what stops this table becoming a description of the internals by enumeration.
    category    TEXT    NOT NULL,
    -- A route *pattern* from a fixed list, or 'other'. Never the path that arrived.
    route       TEXT    NOT NULL,
    -- The exception's type — 'Error', 'TypeError'. Always safe to keep, unlike its message.
    kind        TEXT    NOT NULL,
    -- The message, with addresses and long random strings taken out and capped in length by
    -- `redact`. Redaction does not make this table safe to publish; it makes it safe to read
    -- aloud, which is the thing that actually happens to an error log.
    detail      TEXT,
    -- The six characters the most recent visitor was told to quote.
    reference   TEXT    NOT NULL,
    -- How many failures this row stands for, including the ones whose detail was dropped.
    occurrences INTEGER NOT NULL,
    -- When this run of failures started. Reset with the count when a row goes stale, so an
    -- outage last month and one this morning are not reported as one continuous fault.
    first_at    INTEGER NOT NULL,
    last_at     INTEGER NOT NULL,
    PRIMARY KEY (category, route)
);

-- Pruning reads this: rows nobody has written to inside the retention window are swept away
-- beside the expired tokens and sessions, in `sweepExpired`.
CREATE INDEX idx_error_log_last ON error_log (last_at);
