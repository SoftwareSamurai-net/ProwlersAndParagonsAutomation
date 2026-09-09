using System.Security.Cryptography;
using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// One row a campaign list can draw. The campaign itself is fetched separately, exactly as
/// <see cref="SavedCharacterSummary"/> keeps a character's payload out of its list.
/// </summary>
/// <param name="Id"><c>g_</c> followed by 22 URL-safe characters.</param>
/// <param name="Label">What the GM called the game. Opaque here and on the server alike.</param>
/// <param name="UpdatedAt">Unix milliseconds, for "most recently touched first" and nothing else.</param>
/// <param name="JoinCode">
/// The shared secret somebody joins with, or null for a campaign that has never been written
/// since join codes existed.
///
/// <para><b>The one field of a campaign the server can read</b>, and it is in the list because the
/// GM has to be able to read it out to somebody. It is not inside the payload and cannot be:
/// redeeming a code means finding the campaign it belongs to, which is a query, and the payload is
/// the one thing no query looks inside.</para>
///
/// <para><b>Stored and sent without its hyphen</b>, because that is the form the server compares
/// against — <c>normaliseJoinCode</c> takes the punctuation out on the way in, so a player who
/// types the code without it, or in lower case, still gets in. The hyphen is presentation, and it
/// is put back by <see cref="Spoken"/> rather than carried on the wire.</para>
/// </param>
public sealed record SavedCampaignSummary(
    string Id, string Label, long UpdatedAt, string? JoinCode = null)
{
    /// <summary>
    /// The code as it is read out at a table: <c>XXXXX-XXXXX</c>, or null when there is none yet.
    ///
    /// <para><b>The hyphen is put back here and nowhere else.</b> It was meant to travel with the
    /// code and did not: the server stores the normalised ten symbols, the list answers those, and
    /// the screen printed them — so a code minted as <c>Q4TWX-NPRKM</c> was only ever shown as
    /// <c>Q4TWXNPRKM</c>. Ten unbroken characters is what somebody misreads over a phone, which is
    /// the whole reason the hyphen exists.</para>
    ///
    /// <para>Anything that is not the expected ten symbols is handed back untouched rather than
    /// cut in half — a code from an older or newer minter is a thing to show, not a thing for a
    /// formatter to have an opinion about.</para>
    /// </summary>
    public string? Spoken =>
        JoinCode is { Length: 10 } code ? $"{code[..5]}-{code[5..]}" : JoinCode;
}

/// <summary>
/// What a stored campaign actually is, and the two things a host needs to mint one.
///
/// <para><b>One envelope, for the same reason <see cref="StoredCharacter"/> has one.</b> A
/// campaign is written down by the browser and read back by whatever browser signs in next, and if
/// each end owned its own envelope a version bump would land in one of them and not the other.
/// There is one writer and one reader, here.</para>
///
/// <para><b>Nothing here throws.</b> A hand-edited payload, one from a later build, and an HTML
/// error page arriving where JSON was expected are the same case to a caller: there is no campaign
/// here, carry on.</para>
///
/// <para><b>The version is its own, and starts at 1.</b> It is deliberately not shared with the
/// character envelope: bumping one must never discard the other, and
/// <see cref="StoredCharacter"/>'s reader discards a version mismatch in silence.</para>
///
/// <para><b>There is no local campaign store any more, and the removal is the decision rather than
/// a tidy-up.</b> <c>SavedCampaigns</c> kept campaigns under <c>pp.campaign.v1</c> in this browser,
/// beside the characters — written before there was a screen, and reachable only through
/// <see cref="AccountCampaignStore"/>, which used it for anybody not signed in. A campaign exists
/// so that two accounts can hand a snapshot between them: one kept in a single browser can never
/// receive a submission, hold a clone, or be joined by the code it would advertise. So campaigns
/// are account-only, and the two static helpers that class carried live here, where the envelope
/// is. **Nothing was lost by deleting it**: no screen had ever created a campaign, so no visitor
/// could be holding one under that key.</para>
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
    /// <c>g_</c> plus 22 URL-safe characters — 16 random bytes, base64url without padding.
    ///
    /// <para><b>Mirrors <see cref="SavedCharacters.NewId"/> exactly except for the letter</b>, so
    /// the server can validate a campaign id with the pattern it already validates a character id
    /// with, and neither can be passed where the other is meant.</para>
    /// </summary>
    public static string NewId()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var text = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return $"g_{text}";
    }

    /// <summary>
    /// <c>a_</c> plus 22 URL-safe characters, for one of the campaign's shared vehicles or bases.
    ///
    /// <para><b>Its own letter, and it is minted rather than derived from the name</b>, because
    /// the name is what a reader sees and is re-typed the first time somebody dislikes it — while
    /// every member's sheet is holding this id as the record of what they paid for. A key that
    /// moved when the object was renamed would orphan five contributions at once.</para>
    ///
    /// <para><b>It never reaches the server as a key</b>, unlike <c>c_</c> and <c>g_</c>: an
    /// object lives inside the campaign's payload, which the server stores as an opaque string and
    /// never parses. The shape mirrors the other two anyway, so anybody meeting one in a payload
    /// can see what kind of thing it is.</para>
    /// </summary>
    public static string NewAssetId()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var text = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return $"a_{text}";
    }

    /// <summary>
    /// The name to list a campaign under. Trimmed, and never empty — an unnamed game is an
    /// ordinary state and a blank row reads as broken rather than as unnamed. The same rule
    /// <see cref="SavedCharacters.LabelFor"/> applies to a character, and the server's own default
    /// matches it.
    /// </summary>
    public static string LabelFor(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        return string.IsNullOrWhiteSpace(campaign.Name) ? "Unnamed campaign" : campaign.Name.Trim();
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
