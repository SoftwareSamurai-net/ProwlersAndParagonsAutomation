using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// What an account character may and may not do to the browser's own characters.
///
/// <para><b>Every test here exists because an adversarial review demonstrated a defect.</b> The
/// first version of the copy-down wrote through the anonymous <em>current</em> pointer and cleared
/// the same way, which destroyed the reader's own work in two distinct ways and left the leak it
/// was built to close open in the ordinary case. Two independent reviews, given the diff and told
/// nothing else, found the same three faults with running evidence.</para>
///
/// <para><b>These assert on the storage calls, not on storage.</b> bUnit answers null to every
/// interop read, so a test that saved a character and read it back would be testing a simulation
/// that does not exist — three attempts to write these that way failed on their own preconditions.
/// What differs between the defect and the fix is precisely <em>which key is written and which key
/// is cleared</em>, and that is observable exactly. It is also the narrowest honest claim available
/// here, and the reason <c>PROGRESS.md</c> item 10 proposes a harness that could make a wider
/// one.</para>
///
/// <para><b>The anonymous prefix is bare</b> — <c>pp.character.v1</c>, with no identity segment,
/// which <c>SavedCharacters.PrefixFor</c> decides. Getting that wrong is what made the first draft
/// of this file plant a pointer nothing ever read.</para>
/// </summary>
public sealed class AnonymousSlotTests
{
    private const string Prefix = "pp.character.v1";
    private const string CurrentKey = $"{Prefix}.current";

    private static void AnonymousIsHolding(RenderContext ctx, string id) =>
        ctx.JSInterop.Setup<string?>("ppStore.load", CurrentKey).SetResult(id);

    /// <summary>Every key <c>ppStore.&lt;call&gt;</c> was handed, in order.</summary>
    private static List<string?> KeysPassedTo(RenderContext ctx, string call) =>
        ctx.JSInterop.Invocations
            .Where(i => i.Identifier == call)
            .Select(i => i.Arguments.Count > 0 ? i.Arguments[0] as string : null)
            .ToList();

    /// <summary>
    /// <b>A named local character is never written over.</b> The copy goes to a reserved slot.
    ///
    /// <para>The review's third finding: with two named anonymous characters and one of them open,
    /// a signed-in reader opening an account character wrote the account's data straight over the
    /// open one, which kept its own label. This asserts the reserved key is written and the local
    /// one is not.</para>
    /// </summary>
    [Fact]
    public async Task OpeningAnAccountCharacterDoesNotWriteOverANamedLocalOne()
    {
        await using var ctx = new RenderContext();
        AnonymousIsHolding(ctx, "local-one");

        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        var theirs = SampleCharacters.Villain();
        var id = SavedCharacters.NewId();
        await account.SaveAsync(id, theirs.Name, theirs, SheetMode.Villain);

        // The positive control: the open really happened, or nothing below was exercised at all.
        Assert.NotNull(await ctx.Services.GetRequiredService<AccountCharacterStore>().OpenAsync(id));

        var written = KeysPassedTo(ctx, "ppStore.save");

        Assert.Contains($"{Prefix}.{SavedCharacters.AccountCopyId}", written);
        Assert.DoesNotContain($"{Prefix}.local-one", written);
    }

    /// <summary>
    /// <b>Signing out without ever opening an account character loses nothing.</b>
    ///
    /// <para>The review's first and most serious finding: the clear was unconditional, so a draft
    /// built before signing in was destroyed by a later sign-out even though the account feature
    /// had never touched it — silently, and with no undo.</para>
    /// </summary>
    [Fact]
    public async Task SigningOutLeavesADraftTheAccountNeverTouched()
    {
        await using var ctx = new RenderContext();
        AnonymousIsHolding(ctx, "local-draft");

        await ctx.Services.GetRequiredService<AccountCharacterStore>().ClearAnonymousAsync();

        Assert.DoesNotContain($"{Prefix}.local-draft", KeysPassedTo(ctx, "ppStore.clear"));
    }

    /// <summary>
    /// <b>The copy itself is still removed.</b> The counterpart to the two above: refusing to
    /// delete the reader's own work must not become refusing to delete anything, which would put
    /// the leak straight back.
    /// </summary>
    [Fact]
    public async Task TheCopyItselfIsStillCleared()
    {
        await using var ctx = new RenderContext();
        AnonymousIsHolding(ctx, SavedCharacters.AccountCopyId);

        await ctx.Services.GetRequiredService<AccountCharacterStore>().ClearAnonymousAsync();

        Assert.Contains($"{Prefix}.{SavedCharacters.AccountCopyId}", KeysPassedTo(ctx, "ppStore.clear"));
    }
}
