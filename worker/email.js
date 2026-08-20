// One reading of what an email address is, for the two places that must agree about it.
//
// **They are a gate and a key to the same lock.** `invitations` stores the address somebody is
// allowed to use and `auth` normalises the address somebody typed, and the allow-list is a
// lookup of the second against the first. Two normalisers that disagree by one character — a
// trimmed space, a capital letter — is an invitation that exists and never matches, which reads
// from the outside as an address that was allowed and still cannot sign in. So there is one
// function, imported by both, and a test that nothing else spells it out again.

/**
 * An address, normalised, or null.
 *
 * <p><b>Deliberately permissive.</b> The address is validated by mailing it — anything stricter
 * than "one @, something either side, no spaces" starts rejecting addresses that work, and the
 * flow already proves deliverability in a way no pattern can. Lower-casing is what makes the
 * allow-list a lookup rather than a scan, and it is safe for the domain in every case and for
 * the local part in every case anybody has in practice.</p>
 */
export function normaliseEmail(value) {
    if (typeof value !== 'string') return null;

    const email = value.trim().toLowerCase();
    if (email.length < 3 || email.length > 254) return null;
    if (/\s/.test(email)) return null;

    const at = email.indexOf('@');

    return at > 0 && at === email.lastIndexOf('@') && at < email.length - 1 ? email : null;
}
