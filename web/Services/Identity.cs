namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Who the character being built belongs to.
///
/// <para><b>There are no accounts yet, and this exists so that adding them is a swap rather than
/// a rewrite.</b> Everything that stores a character asks for a key, and today every visitor gets
/// the same one — so the app behaves exactly as it did, with the character in this browser and
/// nowhere else. When there is a sign-in, it returns the account's key instead and the storage
/// underneath starts partitioning by it without any caller changing.</para>
///
/// <para><b>It deliberately carries no claims, no token and no expiry.</b> A key and a name is
/// everything the app needs to know; anything more would be this project inventing an
/// authentication model before it has chosen one, and the wrong model is harder to remove than
/// no model.</para>
/// </summary>
/// <param name="Key">
/// What storage partitions by. Stable for one person across visits.
/// </param>
/// <param name="DisplayName">
/// What to call them on screen, or null when nobody has said. Null is the anonymous case.
/// </param>
public sealed record Identity(string Key, string? DisplayName)
{
    /// <summary>
    /// Whether this is somebody the app can tell apart from anybody else.
    ///
    /// <para>Read by presentation only. <b>Nothing about the rules may branch on it</b>, for the
    /// same reason nothing may branch on the Hero/Villain flag: a character is legal or not
    /// regardless of who is holding it.</para>
    /// </summary>
    public bool IsSignedIn => DisplayName is not null;

    /// <summary>
    /// The visitor with no account — everybody, today.
    ///
    /// <para><b>The key is <c>local</c> and must stay that way.</b> The browser store's key is
    /// built from it, and the historical key it has been writing all along is the one this
    /// produces, so existing characters survive the introduction of identities. Renaming it
    /// orphans every saved character silently, which looks exactly like storage being cleared.</para>
    /// </summary>
    public static Identity Anonymous { get; } = new("local", null);
}

/// <summary>
/// Where the current <see cref="Identity"/> comes from.
///
/// <para>Asynchronous because a real one will have to ask somebody — a token endpoint, a cookie,
/// a redirect that has already happened. Making it synchronous now would mean changing every
/// caller later, which is the whole thing this interface exists to avoid.</para>
/// </summary>
public interface IIdentitySource
{
    ValueTask<Identity> CurrentAsync();
}

/// <summary>
/// The stub: everybody is the same anonymous visitor, and their character lives in this browser.
///
/// <para><b>This is the honest state of the app rather than a placeholder pretending otherwise.</b>
/// There is no sign-in, so there is nobody to be, and the site is a static deploy with no server
/// to ask. It answers immediately and never fails.</para>
/// </summary>
public sealed class LocalIdentity : IIdentitySource
{
    public ValueTask<Identity> CurrentAsync() => ValueTask.FromResult(Identity.Anonymous);
}
