using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Item 21's family findings, surfaced on the roster and the banner switcher — the same shape
/// <see cref="CharacterVariants.RootNotHeldCode"/> already prints beside a row.
///
/// <para><b>The one rule this class holds is "call the engine", never a rule of the engine's
/// own.</b> <see cref="AlternateForms"/> takes a roster of full sheets and answers one
/// <see cref="AlternateFormFamily"/> per root; this class is the part of that a browser has to do
/// that the switcher and the roster would otherwise duplicate — loading each member's own sheet
/// by id (the engine sees only what it is handed) and turning the family's findings into notes
/// keyed by the member they are about, ready to print beside that row exactly as
/// <c>VARIANT_ROOT_NOT_HELD</c> already is.</para>
///
/// <para><b>Only <c>alternate_form</c> children carry a rule.</b> <c>later</c> and <c>as_seen_by</c>
/// are unaffected — nothing here loads a sheet, or calls the engine, for a family that has none —
/// so an account with no alternate forms costs this exactly nothing beyond the group it already
/// built for <see cref="CharacterVariants.Group"/>.</para>
///
/// <para><b>A member that fails to load is said, not guessed past.</b> The engine needs every
/// member's sheet to answer honestly about the set; a family missing one is reported as
/// unreadable on that one row, and the rest of the family's checks are skipped rather than run
/// against a roster this class cannot vouch for.</para>
/// </summary>
public static class AlternateFormRosterNotes
{
    public sealed record Note(string Text, bool Warning);

    public sealed record Result(
        IReadOnlyDictionary<string, IReadOnlyList<Note>> ByMemberId,
        IReadOnlyDictionary<string, string> SharedResolveByRootId)
    {
        public static readonly Result Empty = new(
            new Dictionary<string, IReadOnlyList<Note>>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    /// <param name="families">
    /// Every family <see cref="CharacterVariants.Group"/> found, of any kind — this walks past
    /// every one that is not an <c>alternate_form</c> family.
    /// </param>
    /// <param name="forms">
    /// <see cref="AlternateFormEngine"/>, the indirection over the engine's own family
    /// calculator that keeps <c>AlternateForms</c> — a name <c>WebPresentationTests</c> bans
    /// from markup — off an <c>@inject</c> line.
    /// </param>
    /// <param name="load">
    /// Reads one member's full sheet by id — <c>AccountCharacterStore.ReadAsync</c>, or the
    /// equivalent on whichever store abstraction is asking — and answers null when this build
    /// cannot read it, which <see cref="Result.ByMemberId"/> then says on that row rather than
    /// silently dropping it from the family.
    /// </param>
    /// <param name="companions">
    /// <see cref="CompanionEngine"/>, for Pet/Sidekick families (Ch.2 p.55) — the second rule that
    /// needs two sheets, surfaced the same way: each finding beside the member it is about, and the
    /// owner's Resolve counted with the companions on the owner's row. Null leaves them out.
    /// </param>
    public static async Task<Result> BuildAsync(
        IReadOnlyList<VariantFamily> families,
        AlternateFormEngine forms,
        Func<string, Task<CharacterSheet?>> load,
        CompanionEngine? companions = null)
    {
        ArgumentNullException.ThrowIfNull(families);
        ArgumentNullException.ThrowIfNull(forms);
        ArgumentNullException.ThrowIfNull(load);

        var byMember = new Dictionary<string, List<Note>>(StringComparer.Ordinal);
        var pools    = new Dictionary<string, string>(StringComparer.Ordinal);

        void Add(string id, string text, bool warning)
        {
            if (!byMember.TryGetValue(id, out var list))
                byMember[id] = list = [];
            list.Add(new Note(text, warning));
        }

        if (companions is not null)
            await CompanionNotesAsync(families, companions, load, Add, pools);

        foreach (var family in families)
        {
            if (family.Root is not { } root) continue;

            var members = family.Children
                .Where(c => c.Variant is { Kind: CharacterVariant.AlternateForm })
                .ToList();
            if (members.Count == 0) continue;

            var roster = new List<RosterEntry>();
            var readable = true;

            foreach (var id in new[] { root.Id }.Concat(members.Select(m => m.Id)))
            {
                var sheet = await load(id);
                if (sheet is null)
                {
                    Add(id, "This character could not be loaded, so its Alternate Form findings "
                            + "cannot be checked.", warning: true);
                    readable = false;
                    continue;
                }

                roster.Add(new RosterEntry(id, sheet));
            }

            // Not this class's roster to guess about: a member missing means the engine cannot
            // honestly say what the set breaks, so nothing else here is reported for this family.
            if (!readable) continue;

            AlternateFormFamily? engineFamily;
            try
            {
                engineFamily = forms.Families(roster)
                    .SingleOrDefault(f => string.Equals(f.RootId, root.Id, StringComparison.Ordinal));
            }
            catch (ArgumentException)
            {
                // Two rows reducing to one id — the engine's own refusal. Nothing to add past it.
                continue;
            }

            if (engineFamily is null) continue;

            foreach (var issue in engineFamily.Issues)
                if (issue.SubjectId is { Length: > 0 } subject)
                    Add(subject, issue.Message, issue.Severity == ValidationSeverity.Warning);

            if (engineFamily.SharedResolve is { } pool)
                pools[root.Id] = pools.TryGetValue(root.Id, out var said)
                    ? $"Shared Resolve pool: {pool} · {said}"
                    : $"Shared Resolve pool: {pool}";
        }

        return new Result(
            byMember.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<Note>)kv.Value, StringComparer.Ordinal),
            pools);
    }

    /// <summary>
    /// The Pet/Sidekick half: every owner with a companion in the list, judged by the engine with
    /// every companion's own sheet, and the owner's Resolve counted with them. A family with a
    /// member that cannot be read is said on that row and judged no further, as above.
    /// </summary>
    private static async Task CompanionNotesAsync(
        IReadOnlyList<VariantFamily> families,
        CompanionEngine companions,
        Func<string, Task<CharacterSheet?>> load,
        Action<string, string, bool> add,
        Dictionary<string, string> pools)
    {
        foreach (var family in families)
        {
            if (family.Root is not { } root) continue;

            var members = family.Children
                .Where(c => c.Variant is { Kind: CharacterVariant.Companion })
                .ToList();
            if (members.Count == 0) continue;

            var roster = new List<RosterEntry>();
            var readable = true;

            foreach (var id in new[] { root.Id }.Concat(members.Select(m => m.Id)))
            {
                if (await load(id) is { } sheet) { roster.Add(new RosterEntry(id, sheet)); continue; }

                add(id, "This character could not be loaded, so its companion findings cannot be checked.", true);
                readable = false;
            }

            if (!readable) continue;

            CompanionFamily? answered;
            try
            {
                answered = companions.Families(roster)
                    .SingleOrDefault(f => string.Equals(f.OwnerId, root.Id, StringComparison.Ordinal));
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (answered is null) continue;

            foreach (var issue in answered.Issues)
                if (issue.SubjectId is { Length: > 0 } subject)
                    add(subject, issue.Message, issue.Severity == ValidationSeverity.Warning);

            if (answered is { Owner: { } owner, OwnerResolve: { } with })
            {
                var names = answered.Members.Select(m => string.IsNullOrWhiteSpace(m.Sheet.Name) ? m.Id : m.Sheet.Name).ToList();
                var joined = names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
                pools[root.Id] = $"{PoolName.For(owner.Sheet)} {with} with {joined}";
            }
        }
    }
}
