using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web;
using ProwlersAndParagonsAutomation.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddScoped(_ => http);

// Every rules file is fetched before the first render. The engine is synchronous by
// design — see PROGRESS.md on why IRulesSource is not async — so a repository built on a
// half-loaded set would throw FileNotFoundException on whichever collection was touched
// first. Fetching is the host's problem; answering questions about the rules is the
// engine's. RulesRepository.DataFileNames is the contract, because the browser cannot
// glob a directory that is not there.
var files = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (var name in RulesRepository.DataFileNames)
    files[name] = await http.GetStringAsync($"data/rules/{name}");

var rules     = new RulesRepository(new InMemoryRulesSource(files));
var costs     = new CostCalculator(rules);
var derived   = new DerivedStatsCalculator(rules);
var validator = new CharacterValidator(rules, costs, derived);

builder.Services.AddSingleton(rules);
builder.Services.AddSingleton(costs);
builder.Services.AddSingleton(derived);
builder.Services.AddSingleton(validator);
builder.Services.AddSingleton(new ProConApplicability(rules));
builder.Services.AddSingleton(new SourceGrouping(rules));
builder.Services.AddScoped<CharacterSession>();
// Accounts. This is the whole of what changed when the app stopped being anonymous-only: one
// identity source that can answer something other than "nobody", and a store that puts the
// character where it belongs. Nothing else in the app asks who is signed in, and nothing in
// engine/ or sheets/ can — see PresentationFlagsTests.
//
// Both concrete stores are registered as well as the interface, because AccountCharacterStore
// takes them and the sign-in page needs the two operations that are not on ICharacterStore:
// reading the anonymous slot specifically, and copying it up on request.
builder.Services.AddScoped<Accounts>();
builder.Services.AddScoped<Invitations>();
builder.Services.AddScoped<ErrorLog>();
builder.Services.AddScoped<IIdentitySource>(s => s.GetRequiredService<Accounts>());
builder.Services.AddScoped<CharacterStore>();
// The plural store, registered separately from CharacterStore even though CharacterStore
// builds its own instance internally (SavedCharacters is stateless — every method reads
// storage fresh — so there is nothing to share). This registration is for a manager page:
// list, switch, save-as and delete are not on ICharacterStore's single-character shape and
// never will be, so a page that wants them asks for SavedCharacters directly.
builder.Services.AddScoped<SavedCharacters>();
builder.Services.AddScoped<ApiCharacterStore>();
builder.Services.AddScoped<AccountCharacterStore>();
builder.Services.AddScoped<DiscardedCharacter>();

// Campaigns, the same two stores and the same chooser. Registered although no page asks for one
// yet: the storage half of a campaign ships before its screens do, and a service nothing can
// resolve is a service the first page has to discover is missing.
builder.Services.AddScoped<SavedCampaigns>();
builder.Services.AddScoped<ApiCampaignStore>();
builder.Services.AddScoped<AccountCampaignStore>();
builder.Services.AddScoped<ICharacterStore>(s => s.GetRequiredService<AccountCharacterStore>());
builder.Services.AddScoped<RulebookReader>();
builder.Services.AddScoped<CharacterImport>();
builder.Services.AddScoped<Motion>();
builder.Services.AddScoped<Commands>();
builder.Services.AddScoped<Shortcuts>();
builder.Services.AddScoped<Theme>();
builder.Services.AddScoped<ReplayLoader>();
builder.Services.AddScoped<Sliders>();

var host = builder.Build();

// The character is read back before the first render, not after it. Restoring in a
// component's OnAfterRender works and shows the player an empty sheet first, which reads as
// "your character is gone" for as long as it takes to correct itself. Interop is available
// here because this is WebAssembly and there is no prerender to wait for.
//
// Nothing in this block may stop the app starting: a character saved by an older build, or
// storage the browser refuses, both mean "no character", and CharacterStore returns null
// rather than throwing. See its remarks.
//
// This still restores "the current character" and nothing more, unchanged by SavedCharacters
// existing at all: CharacterStore.LoadAsync() asks SavedCharacters which id is open before
// reading, so whichever character a manager page switched to is the one that comes back here.
// A visitor who has never used a manager has never switched anything, so this restores the
// bare pp.character.v1 slot exactly as it always did.
var store = host.Services.GetRequiredService<ICharacterStore>();
var session = host.Services.GetRequiredService<CharacterSession>();

// **The copy an account character leaves behind is cleared here as well as on the sign-out
// button, and this is the half that matters.** Signing out by pressing the button was the only
// thing that cleared it, so closing the tab or letting the session expire — which is how somebody
// actually leaves a shared machine — left the last account character they opened readable by the
// next visitor, under no account at all. That is precisely the leak the clear exists to close, and
// it was open in the ordinary case. Demonstrated by adversarial review, not by any test.
//
// Asking here works because this runs before the first render and the server is the authority on
// whether the cookie is still good: an expired or revoked session answers "anonymous", and that is
// the transition nothing else was watching. It removes only `AccountCopyId`, so a visitor who never
// signed in loses nothing — see AccountCharacterStore.ClearAnonymousAsync.
try
{
    if (!(await host.Services.GetRequiredService<IIdentitySource>().CurrentAsync()).IsSignedIn)
        await host.Services.GetRequiredService<AccountCharacterStore>().ClearAnonymousAsync();
}
catch
{
    // Same rule as the two catches below: nothing in this block may stop the app starting. A
    // leftover copy is worth less than a blank page.
}

// Both catches are deliberately total, and they are a backstop rather than the strategy:
// CharacterStore already rejects anything the engine cannot answer questions about. But this
// runs before the first render, so anything escaping here is not a lost character — it is a
// blank page, and a blank page caused by something the app wrote itself is the worst outcome
// available.
//
// They are two catches rather than one because the two halves fail differently. Reading the
// character back can fail in a way that means "there is no character"; setting the palette
// cannot. One catch around both threw away a character that had restored perfectly, because
// a JS call about its colours did not answer.
// No initialiser: both branches below assign it, and one that looked like a safe default
// would only hide it if some later edit stopped doing so.
(CharacterSheet Sheet, SheetMode Mode)? saved;

#pragma warning disable CA1031 // see above: starting empty always beats not starting
try
{
    saved = await store.LoadAsync();
    if (saved is { } restored) session.Restore(restored.Sheet, restored.Mode);
}
catch (Exception)
{
    session.StartAgain();

    // And forget it, rather than leaving it to be re-read and re-fail on every future visit.
    // A character that stops the app has to be removable from inside the app.
    try { await store.ClearAsync(); } catch (Exception) { /* nothing left to try */ }
    saved = null;
}

if (saved is { } withMode)
{
    // The palette is presentation. Failing to set it is a Hero-coloured Villain, which is
    // a great deal better than no character.
    try
    {
        await host.Services.GetRequiredService<IJSRuntime>()
            .InvokeVoidAsync("ppSetMode", withMode.Mode == SheetMode.Hero ? "hero" : "villain");
    }
    catch (Exception) { /* the character is already restored; the colours can wait */ }
}
#pragma warning restore CA1031

// Every change writes through. The sheet is small and localStorage is synchronous and
// fast, so there is nothing to gain by batching — and a debounce is one more way to lose
// the last edit before a refresh, which is the thing this exists to prevent.
//
// **The "Saved" the shell shows is this await returning, and nothing more.** SaveAsync
// never throws — see ICharacterStore's own doc comment — so there is no failure path to
// invent a message for, and no "saving…" state either: nobody out here knows how long a
// write takes, only that it finished.
//
// **The version is captured before the await, not read again after it.** Two saves can be
// in flight together and finish in either order, so a version read from `session` once this
// one's write returns could belong to an edit made while this write was still going — session
// itself weighs the two by number rather than by which callback happened to run last.
session.Changed += () => _ = SaveThenAnnounce(session.Version);

async Task SaveThenAnnounce(int version)
{
    await store.SaveAsync(session.Sheet, session.Mode);
    session.NotifySaved(version);
}

await host.RunAsync();
