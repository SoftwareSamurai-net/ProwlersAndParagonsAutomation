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
    public static JsonSerializerOptions StrictOptions { get; } = new(Options)
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
    public static CharacterSheet Repair(CharacterSheet sheet)
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

        return sheet;
    }

    /// <summary>
    /// A character read from JSON, repaired, or null if the text is not a JSON object at all.
    /// </summary>
    /// <exception cref="JsonException">The text is not well-formed JSON, or a value has the
    /// wrong type for the field it is in. Left to the caller: a browser restoring storage
    /// wants to shrug and start empty, and a command reading a file the user named wants to
    /// say which file and why.</exception>
    public static CharacterSheet? Read(string json) => Read(json, strict: false);

    /// <summary>
    /// <inheritdoc cref="Read(string)"/>
    /// </summary>
    /// <param name="json">The character.</param>
    /// <param name="strict">
    /// True to refuse a property that is not part of a character — see
    /// <see cref="StrictOptions"/> for why that is right for a submitted file and wrong for
    /// restored storage.
    /// </param>
    public static CharacterSheet? Read(string json, bool strict) =>
        JsonSerializer.Deserialize<CharacterSheet>(json, strict ? StrictOptions : Options)
            is { } sheet ? Repair(sheet) : null;

    /// <summary>The character's inputs as JSON, in the shape <see cref="Read"/> accepts.</summary>
    public static string Write(CharacterSheet sheet) =>
        JsonSerializer.Serialize(sheet, Options);
}
