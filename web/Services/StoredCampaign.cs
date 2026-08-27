using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// What a stored campaign actually is, in local storage and on the wire alike.
///
/// <para><b>One envelope, for the same reason <see cref="StoredCharacter"/> has one.</b> A
/// campaign is kept in two very different places — this browser and an account's server — and if
/// each owned its own envelope a version bump would land in one of them and not the other, so a
/// campaign made on a laptop would read back wrongly on a phone. There is one writer and one
/// reader, here.</para>
///
/// <para><b>Nothing here throws.</b> A hand-edited key, a payload from a later build, and an HTML
/// error page arriving where JSON was expected are the same case to a caller: there is no
/// campaign here, carry on. This is read on paths that run before a render, so an exception is
/// not a lost campaign but an app that does not start.</para>
///
/// <para><b>The version is its own, and starts at 1.</b> It is deliberately not shared with the
/// character envelope: bumping one must never discard the other, and
/// <c>StoredCharacter.Usable</c> discards a version mismatch in silence.</para>
/// </summary>
public static class StoredCampaign
{
    /// <summary>
    /// Bumped when a change to <see cref="Campaign"/> would make an older stored campaign read
    /// back wrongly rather than merely incompletely. A mismatch is discarded in silence.
    /// </summary>
    private const int CurrentVersion = 1;

    /// <summary>
    /// The engine's own options, so a campaign is spelled the way a character is. Nothing about a
    /// campaign needs the sheet reader's repairs; what it needs is one naming policy at both ends.
    /// </summary>
    private static JsonSerializerOptions Options => CharacterSheetJson.Options;

    private sealed record Saved(int Version, Campaign? Campaign);

    /// <summary>The campaign as it is written down.</summary>
    public static string Write(Campaign campaign) =>
        JsonSerializer.Serialize(new Saved(CurrentVersion, campaign), Options);

    /// <summary>A stored payload, or null if there is none this build can trust.</summary>
    public static Campaign? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var saved = JsonSerializer.Deserialize<Saved>(json, Options);

            // `Campaign` is declared non-nullable and comes back null for `{"Version":1}` — the
            // same promise the type system makes and the deserializer does not that
            // `StoredCharacter` documents. An id or a name it cannot be listed under is the same
            // case: there is no campaign here.
            if (saved is not { Version: CurrentVersion, Campaign: { } campaign }) return null;

            return string.IsNullOrWhiteSpace(campaign.Id) ? null : campaign;
        }
        catch (Exception e) when (IsUnreadable(e)) { return null; }
    }

    /// <summary>
    /// Everything that can go wrong reading a payload, named rather than caught bare so the list
    /// is reviewable. Every one means the same thing: there is no stored campaign.
    /// </summary>
    private static bool IsUnreadable(Exception e) =>
        e is JsonException                  // malformed, hand-edited, or an HTML error page
          or NotSupportedException          // a type the serializer cannot handle
          or ArgumentException              // an id or key the payload invented
          or OverflowException;             // a number no build can hold
}
