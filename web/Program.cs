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
builder.Services.AddScoped<CharacterStore>();

// The recorded conversations, fetched the same way and for the same reason — a browser
// cannot glob a directory it has no filesystem for, so TranscriptLibrary.FileNames is the
// contract, as RulesRepository.DataFileNames is above.
//
// Unlike the rules, this block is allowed to fail. The recordings are a demonstration for
// somebody who has no way to hold the conversation themselves; the app is a character
// generator, and refusing to start it because a demo file did not arrive would be the wrong
// trade in every direction. What is not allowed is an empty list that looks deliberate, so
// the reason travels with the library and the replay pages print it.
ReplayLibrary replays;

#pragma warning disable CA1031 // any failure here means "no recordings", never "no app"
try
{
    var transcripts = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var name in TranscriptLibrary.FileNames)
        transcripts[name] = await http.GetStringAsync($"data/transcripts/{name}");

    replays = new ReplayLibrary(TranscriptLibrary.ReadAll(transcripts));
}
catch (Exception e)
{
    replays = new ReplayLibrary([], e.Message);
}
#pragma warning restore CA1031

builder.Services.AddSingleton(replays);

var host = builder.Build();

// The character is read back before the first render, not after it. Restoring in a
// component's OnAfterRender works and shows the player an empty sheet first, which reads as
// "your character is gone" for as long as it takes to correct itself. Interop is available
// here because this is WebAssembly and there is no prerender to wait for.
//
// Nothing in this block may stop the app starting: a character saved by an older build, or
// storage the browser refuses, both mean "no character", and CharacterStore returns null
// rather than throwing. See its remarks.
var store = host.Services.GetRequiredService<CharacterStore>();
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
