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

// The catch is deliberately total, and it is a backstop rather than the strategy:
// CharacterStore already rejects anything it cannot use. But this runs before the first
// render, so anything escaping here is not a lost character — it is a blank page, and a
// blank page caused by something the app wrote itself is the worst outcome available.
try
{
    if (await store.LoadAsync() is { } saved)
    {
        session.Restore(saved.Sheet, saved.Mode);
        await host.Services.GetRequiredService<IJSRuntime>()
            .InvokeVoidAsync("ppSetMode", saved.Mode == SheetMode.Hero ? "hero" : "villain");
    }
}
#pragma warning disable CA1031 // see above: starting empty always beats not starting
catch (Exception)
{
    session.StartAgain();
}
#pragma warning restore CA1031

// Every change writes through. The sheet is small and localStorage is synchronous and
// fast, so there is nothing to gain by batching — and a debounce is one more way to lose
// the last edit before a refresh, which is the thing this exists to prevent.
session.Changed += () => _ = store.SaveAsync(session.Sheet, session.Mode);

await host.RunAsync();
