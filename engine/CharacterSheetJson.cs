using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Reads and writes a <see cref="CharacterSheet"/> as JSON — the inputs of a character,
/// which is what a host stores and what a caller submits.
///
/// <para><b>This is not the export.</b> <c>CharacterSheetRenderer.RenderJson</c> produces a
/// report: derived stats, costs and validation findings, all of them answers. Reading one
/// back would mean rebuilding a character out of its own conclusions.</para>
///
/// <para>It lives in the engine because the shape belongs to the engine, and because there
/// are now two callers — the browser's local storage and the headless <c>build</c> command.
/// The subtleties below are worth exactly one copy: they were all found the hard way, by an
/// app that would not start.</para>
/// </summary>
public static class CharacterSheetJson
{
    /// <summary>
    /// Populate rather than replace, because <see cref="CharacterSheet"/> exposes its
    /// collections as get-only properties with initialisers — the shape the engine wants,
    /// and one a deserializer has to be told to fill rather than assign. Without this the
    /// abilities, talents, powers, perks, flaws and gear of every submitted character are
    /// silently dropped and the result is a legal, empty, free character.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // So a caller writing a character by hand can spell a field the way their language
        // does. It does not forgive an underscore — selected_tier_id is still not a field —
        // and nothing here silently renames anything.
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// The same, but a property that is not part of a character is refused rather than
    /// ignored.
    ///
    /// <para><b>This is for a character somebody wrote, rather than one this program saved.</b>
    /// A misspelled <c>AbilityRanks</c> is not a small error: the abilities are silently
    /// dropped and what arrives is a cheaper, legal character that nobody notices is wrong.
    /// A caller submitting a file wants to be told; a browser restoring its own storage
    /// wants the opposite, since a field removed in a later build would otherwise throw away
    /// a character it could still mostly read.</para>
    /// </summary>
    private static JsonSerializerOptions StrictOptions { get; } = new(Options)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>
    /// Repairs the nulls a deserializer will put where the type system says it cannot.
    ///
    /// <para>Every one of these is a property declared non-nullable that comes back null when
    /// its key is absent, and none of them is something the compiler can warn about:</para>
    /// <list type="bullet">
    ///   <item>A <c>SelectedPower</c>'s <c>Pros</c> and <c>Cons</c> — a
    ///     <see cref="NullReferenceException"/> on the next thing that costs the sheet.</item>
    ///   <item>Entries inside the four lists: <c>"Flaws":[null]</c> parses cleanly.</item>
    ///   <item>The four free-text fields, which the text export enumerates.</item>
    /// </list>
    ///
    /// <para><b>This is tidying, not a guard.</b> The nesting goes deeper than any list of
    /// shapes can chase, so a caller still has to be able to survive a payload the engine
    /// cannot answer for — by asking the engine, once, rather than by checking fields.</para>
    /// </summary>
    /// <param name="sheet">The character to repair in place.</param>
    /// <param name="dropIdlessEntries">
    /// <b>What to do with an entry that has no id</b> — <c>{"Flaws":[{}]}</c>, which is
    /// well-formed JSON naming nothing. The two callers want opposite things and both are right.
    ///
    /// <para>Storage drops it: it is junk, it can only have come from a hand-edited or
    /// half-written payload, and losing one entry is not worth losing the character. A submitted
    /// file keeps it, because the validator reports it by name and dropping it silently would
    /// hand back a cheaper character than the one that was sent — the same failure as a
    /// misspelled field name, which is refused for exactly this reason.</para>
    /// </param>
    public static CharacterSheet Repair(CharacterSheet sheet, bool dropIdlessEntries = false)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        for (var i = 0; i < sheet.SelectedPowers.Count; i++)
        {
            var power = sheet.SelectedPowers[i];
            if (power is null || (power.Pros is not null && power.Cons is not null)) continue;

            sheet.SelectedPowers[i] = power with { Pros = power.Pros ?? [], Cons = power.Cons ?? [] };
        }

        sheet.Name       ??= "";
        sheet.Appearance ??= "";
        sheet.Motivation ??= "";
        sheet.Quote      ??= "";

        sheet.SelectedPowers.RemoveAll(p => p is null);
        sheet.Perks.RemoveAll(p => p is null);
        sheet.Flaws.RemoveAll(f => f is null);
        sheet.Gear.RemoveAll(g => g is null);
        sheet.Connections.RemoveAll(c => c is null);
        sheet.Vehicles.RemoveAll(v => v is null);
        sheet.Headquarters.RemoveAll(h => h is null);
        sheet.Gadgets.RemoveAll(g => g is null);
        sheet.CampaignAssets.RemoveAll(c => c is null);

        // A piece of gear's three lists are declared non-null and come back null when the keys
        // are absent, which is a NullReferenceException the next time anything prices it. Absent
        // and empty mean the same thing here — no features, no Pros, no Cons — so this is
        // repaired in both modes rather than reported: there is nothing a caller would want told.
        for (var i = 0; i < sheet.Gear.Count; i++)
        {
            var gear = sheet.Gear[i];
            if (gear.Features is not null && gear.Pros is not null && gear.Cons is not null) continue;

            sheet.Gear[i] = gear with
            {
                Features = gear.Features ?? [],
                Pros     = gear.Pros ?? [],
                Cons     = gear.Cons ?? []
            };
        }

        // The same non-null-declared-comes-back-null trap the gear lists fall into, on the four
        // Chapter 6 collections. Absent and empty mean the same thing on every one of them, so
        // this is repaired in both modes rather than reported: there is nothing a caller would
        // want told about a vehicle with no features.
        for (var i = 0; i < sheet.Vehicles.Count; i++)
            if (sheet.Vehicles[i].Features is null)
                sheet.Vehicles[i] = sheet.Vehicles[i] with { Features = [] };

        for (var i = 0; i < sheet.Headquarters.Count; i++)
            if (sheet.Headquarters[i].Features is null)
                sheet.Headquarters[i] = sheet.Headquarters[i] with { Features = [] };

        for (var i = 0; i < sheet.Gadgets.Count; i++)
        {
            var gadget = sheet.Gadgets[i];
            if (gadget.Powers is not null && gadget.AbilityRanks is not null
                                          && gadget.TalentRanks is not null) continue;

            sheet.Gadgets[i] = gadget with
            {
                Powers       = gadget.Powers ?? [],
                AbilityRanks = gadget.AbilityRanks ?? new Dictionary<string, int>(),
                TalentRanks  = gadget.TalentRanks ?? new Dictionary<string, int>()
            };
        }

        // A Gadget's Powers are SelectedPowers and fall into the same Pros/Cons trap the
        // character's own do — a NullReferenceException the next thing that prices the Gadget.
        for (var i = 0; i < sheet.Gadgets.Count; i++)
        {
            var gadget = sheet.Gadgets[i];
            if (gadget.Powers.All(p => p is null || (p.Pros is not null && p.Cons is not null)))
                continue;

            sheet.Gadgets[i] = gadget with
            {
                Powers = [.. gadget.Powers.Where(p => p is not null)
                                          .Select(p => p with { Pros = p.Pros ?? [], Cons = p.Cons ?? [] })]
            };
        }

        for (var i = 0; i < sheet.CampaignAssets.Count; i++)
            if (sheet.CampaignAssets[i].Kind is null)
                sheet.CampaignAssets[i] = sheet.CampaignAssets[i] with { Kind = "" };

        if (dropIdlessEntries)
        {
            sheet.SelectedPowers.RemoveAll(p => p.PowerId is null);
            sheet.Perks.RemoveAll(p => p.PerkId is null);
            sheet.Flaws.RemoveAll(f => f.FlawId is null);
            sheet.Gear.RemoveAll(g => g.Name is null);
            sheet.Vehicles.RemoveAll(v => v.Name is null);
            sheet.Headquarters.RemoveAll(h => h.Name is null);
            sheet.Gadgets.RemoveAll(g => g.Name is null);
            sheet.CampaignAssets.RemoveAll(c => c.AssetId is null);

            // And the entries one level in, which are the same thing in a nested list: a Pro
            // that is null, a gear feature that is null. The validator reports these on the
            // submit path; here they are junk between a character and being restored at all.
            for (var i = 0; i < sheet.SelectedPowers.Count; i++)
            {
                var power = sheet.SelectedPowers[i];
                sheet.SelectedPowers[i] = power with
                {
                    Pros = [.. power.Pros.Where(p => p?.Id is not null)],
                    Cons = [.. power.Cons.Where(c => c?.Id is not null)]
                };
            }

            for (var i = 0; i < sheet.Gear.Count; i++)
            {
                var gear = sheet.Gear[i];
                sheet.Gear[i] = gear with
                {
                    Features = [.. gear.Features.Where(f => f?.FeatureId is not null)],
                    Pros     = [.. gear.Pros.Where(p => p?.Id is not null)],
                    Cons     = [.. gear.Cons.Where(c => c?.Id is not null)]
                };
            }

            foreach (var abilityId in sheet.AbilityModifiers.Keys.ToList())
                sheet.AbilityModifiers[abilityId] =
                    [.. (sheet.AbilityModifiers[abilityId] ?? []).Where(m => m?.Id is not null)];
        }

        return sheet;
    }

    /// <summary>
    /// A character read from JSON, repaired, or null if the text is not a JSON object at all.
    /// </summary>
    /// <exception cref="JsonException">The text is not well-formed JSON, or a value has the
    /// wrong type for the field it is in. Left to the caller: a browser restoring storage
    /// wants to shrug and start empty, and a command reading a file the user named wants to
    /// say which file and why.</exception>
    /// <param name="json">The character.</param>
    /// <param name="strict">
    /// True to refuse a property that is not part of a character. <b>There is no default</b>,
    /// because the two callers want opposite answers and neither is the obvious one: a
    /// submitted file wants to hear about a misspelled field, and a browser restoring its own
    /// storage would rather keep a character that has lost one.
    /// </param>
    public static CharacterSheet? Read(string json, bool strict) =>
        JsonSerializer.Deserialize<CharacterSheet>(json, strict ? StrictOptions : Options)
            is { } sheet ? Repair(sheet, dropIdlessEntries: !strict) : null;

    /// <summary>The character's inputs as JSON, in the shape <see cref="Read"/> accepts.</summary>
    public static string Write(CharacterSheet sheet) =>
        JsonSerializer.Serialize(sheet, Options);
}
