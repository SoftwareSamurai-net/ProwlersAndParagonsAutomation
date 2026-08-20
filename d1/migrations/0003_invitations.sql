-- Who is allowed to ask for a sign-in link at all.
--
-- Until this table existed, anybody who could load the page could cause this site to send mail
-- to any address they typed. That is the ordinary shape for a public sign-up and it is not what
-- this site is: the account is what puts the rulebook's own text on screen, and the right to
-- read that belongs to the owner and to people he has named.
--
-- **The list is addresses, not accounts.** A row here is permission to become one, and it stays
-- after the account exists — so taking the row away is what withdraws the permission, and the
-- account it made is still there to be handed back. That also means the first row cannot come
-- from here: nobody can reach the page that manages this table without an account, and no
-- account can exist without a row. The bootstrap is `ADMIN_EMAIL`, an environment variable read
-- by the server and never stored — see docs/ACCOUNTS-SETUP.md.
--
-- **Nothing is seeded.** A committed address would be the repository owner's own, on every fork
-- and every clone, silently making him the administrator of somebody else's deployment. A
-- deployment with no `ADMIN_EMAIL` set therefore allows nobody, which is the safe direction:
-- a site that signs nobody in is visibly broken, and one that lets a stranger in is not.

CREATE TABLE invitations (
    -- Opaque, and not the address, for the same reason `users.id` is not: this id travels in a
    -- URL when one is withdrawn, and an address in a URL is an address in every log between
    -- here and the browser.
    id           TEXT    PRIMARY KEY,
    email        TEXT    NOT NULL UNIQUE,
    -- Whether signing in on this address makes an administrator. On the invitation rather than
    -- on `users`, so the list is the single answer to both questions it is asked — may this
    -- address sign in, and may it manage the list — and there is no second copy to fall out of
    -- step when a row changes.
    grants_admin INTEGER NOT NULL DEFAULT 0,
    -- Who added it. Null for a row whose author's account has since been removed, which is why
    -- this is SET NULL rather than CASCADE: deleting an account must not delete the permissions
    -- that account granted to other people.
    invited_by   TEXT             REFERENCES users (id) ON DELETE SET NULL,
    created_at   INTEGER NOT NULL
);

CREATE INDEX idx_invitations_email ON invitations (email);
