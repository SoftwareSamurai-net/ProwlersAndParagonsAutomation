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
builder.Services.AddScoped<ICharacterStore>(s => s.GetRequiredService<AccountCharacterStore>());
builder.Services.AddScoped<RulebookReader>();
builder.Services.AddScoped<CharacterImport>();
builder.Services.AddScoped<Motion>();
builder.Services.AddScoped<Commands>();
builder.Services.AddScoped<Shortcuts>();
builder.Services.AddScoped<Theme>();

// The recorded conversations, fetched the same way and for the same reason — a browser
// cannot glob a directory it has no filesystem for, so TranscriptLibrary.FileNames is the
// contract, as RulesRepository.DataFileNames is above.
//
// Unlike the rules, this is allowed to fail: the recordings are a demonstration and the app
// is a character generator, so a demo file that did not arrive must not stop somebody
// building a character. That guarantee lives in ReplayLibrary.LoadAsync and is tested there,
// and the *client* is handed over rather than a fetch: fetching here and passing a delegate
// that cannot fail puts the throwing call back outside the guard with every test still green.
// It was a try/catch here, where nothing could reach it at all — deleting the try left the
// suite green and one 404 took the whole app to a blank page.
builder.Services.AddSingleton(await ReplayLibrary.LoadAsync(http));

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
session.Changed += () => _ = store.SaveAsync(session.Sheet, session.Mode);

await host.RunAsync();
