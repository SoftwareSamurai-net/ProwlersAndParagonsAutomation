// The owner's read of what has gone wrong, over the wire.
//
// **Gated by the same question as the invitation list, not a second one.** `index.js` asks
// `invitations.isAdministrator` before either address is reached, so this module does not
// re-decide who may see it — inventing a second check here is exactly the shape of bug where the
// two eventually disagree. See the note in `index.js` for why that check answers an ordinary
// account the same 404 an unrouted address gets.
//
// **Read-only.** There is no route here that deletes or clears a row, because the table needs
// none — see the comment on `error_log` in `d1/migrations/0004_error_log.sql`. If a clear is
// ever wanted, that is a new decision to make, not a gap to quietly close.

import * as db from './db.js';
import { json } from './http.js';

/** Every recorded failure, in the shape the panel reads. */
export async function list(request, env, deps, user) {
    const rows = await db.listErrorLog(env.DB);

    return json({
        rows: rows.map(row => ({
            category: row.category,
            route: row.route,
            kind: row.kind,
            detail: row.detail,
            occurrences: row.occurrences,
            firstAt: row.first_at,
            lastAt: row.last_at,
            reference: row.reference,
        })),
    });
}
