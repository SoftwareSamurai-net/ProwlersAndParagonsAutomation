using ProwlersAndParagonsAutomation.Engine;

// Resolve the data path relative to the project root.
// AppContext.BaseDirectory points to bin/Debug/netX.0/ at runtime,
// so we walk up to the repo root where data/ lives.
var projectRoot = FindProjectRoot(AppContext.BaseDirectory);
var rules = RulesRepository.FromBasePath(projectRoot);

Console.WriteLine("=== Prowlers & Paragons — Engine Smoke Test ===");
Console.WriteLine();

// Print loaded counts
Console.WriteLine($"Tiers loaded:         {rules.Tiers.Count}");
Console.WriteLine($"Abilities loaded:     {rules.Abilities.Count}");
Console.WriteLine($"Talents loaded:       {rules.Talents.Count}");
Console.WriteLine($"Powers loaded:        {rules.Powers.Count}");
Console.WriteLine($"Pros loaded:          {rules.Pros.Count}");
Console.WriteLine($"Cons loaded:          {rules.Cons.Count}");
Console.WriteLine();

// Quick validation: build a minimal Standard-tier character
var costs  = new CostCalculator(rules);
var derived = new DerivedStatsCalculator(rules);
var validator = new CharacterValidator(rules, costs, derived);

var sheet = new CharacterSheet
{
    SelectedTierId = "standard",
};

// Set abilities to ordinary-human baseline (2d each)
foreach (var ability in rules.Abilities)
    sheet.AbilityRanks[ability.Id] = 2;

// Add one flaw (required minimum)
sheet.Flaws.Add("example_flaw");

// Add Strike (3 purchased ranks on top of Might baseline)
sheet.SelectedPowers.Add(new SelectedPower("strike", purchasedRanks: 3));

var result = validator.Validate(sheet);

Console.WriteLine("=== Sample character validation ===");
Console.WriteLine($"Tier:           {rules.GetTier(sheet.SelectedTierId!)!.Name}");
Console.WriteLine($"HP budget:      {rules.GetTier(sheet.SelectedTierId!)!.HeroPoints}");
Console.WriteLine($"HP spent:       {costs.TotalCost(sheet)}");
Console.WriteLine($"Edge:           {derived.CalculateEdge(sheet)}");
Console.WriteLine($"Health:         {derived.CalculateHealth(sheet)}");
Console.WriteLine();
Console.WriteLine($"Valid:  {result.IsValid}");

foreach (var issue in result.Issues)
    Console.WriteLine($"  [{issue.Severity}] {issue.Code}: {issue.Message}");

Console.WriteLine();
Console.WriteLine("Engine ready. CLI layer next.");

// ── Helpers ──────────────────────────────────────────────────────────────

static string FindProjectRoot(string startPath)
{
    var dir = new DirectoryInfo(startPath);
    while (dir is not null)
    {
        if (dir.GetFiles("*.sln").Length > 0)
            return dir.FullName;
        dir = dir.Parent;
    }
    // Fallback: use the starting path
    return startPath;
}
