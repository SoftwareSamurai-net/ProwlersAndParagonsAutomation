using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
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

await builder.Build().RunAsync();
