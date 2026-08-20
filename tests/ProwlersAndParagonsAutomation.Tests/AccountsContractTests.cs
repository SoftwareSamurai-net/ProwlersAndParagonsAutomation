using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Accounts, held to the two promises that no compiler and no single-language test can check.
///
/// <para><b>One: the rules never learn who is holding a character.</b> A character is legal or
/// not regardless of whose it is, and the day one <c>if</c> in the validator disagrees, the
/// browser is deciding a rule. That is the same discipline <see cref="PresentationFlagsTests"/>
/// holds the Hero/Villain flag to, and it is worth the same guard for the same reason: every
/// other test in this suite would stay green.</para>
///
/// <para><b>Two: the two halves of the account system are written in different languages and
/// have to agree.</b> The server is JavaScript, because Cloudflare Workers is; the client is
/// C#. Each is tested thoroughly on its own — the server against the real migration in real
/// SQLite, the client against a stub of the server — and both suites would stay green while an
/// address or a field name changed on one side only. Nothing but this reads both.</para>
/// </summary>
public sealed class AccountsContractTests
{
    /// <summary>The projects that decide a cost, a rank, a figure or a verdict.</summary>
    private static readonly string[] RulesProjects = ["engine", "sheets"];

    /// <summary>
    /// Words that would mean rules code had learned about accounts.
    ///
    /// <para>Deliberately broader than the type names: <c>Identity</c> alone would be a
    /// dependency, but so would a bare "signed in" in a comment explaining a branch.</para>
    /// </summary>
    private static readonly string[] AccountWords =
        ["IIdentitySource", "IdentitySource", "Identity.Anonymous", "IsSignedIn",
         "SignedIn", "AccountCharacterStore", "ApiCharacterStore", "RulebookReader"];

    [Fact]
    public void NoRulesCodeKnowsWhoIsSignedIn()
    {
        var offenders = new List<string>();

        foreach (var file in RulesSources())
        {
            var text = File.ReadAllText(file);

            offenders.AddRange(
                AccountWords.Where(word => text.Contains(word, StringComparison.Ordinal))
                            .Select(word => $"{Path.GetFileName(file)} names {word}"));
        }

        Assert.True(offenders.Count == 0,
            "These decide costs, ranks and verdicts, and must not be able to see who is holding "
            + "the character — a rule that branches on an account is the browser deciding a "
            + "rule:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// The positive control. A scan for eight names that no longer exist passes on everything.
    /// </summary>
    [Fact]
    public void TheGuardIsLookingAtSomething()
    {
        var files = RulesSources().ToList();
        Assert.True(files.Count > 10, $"only {files.Count} rules source files found");

        // Every word scanned for is a word the browser really does use. One that has been
        // renamed away is a word the scan above can never find, in any file.
        var browser = string.Concat(
            Directory.EnumerateFiles(Path.Combine(RulesFixture.RepoRoot, "web"), "*.cs",
                    SearchOption.AllDirectories)
                .Where(NotBuildOutput)
                .Select(File.ReadAllText));

        Assert.All(AccountWords, word =>
            Assert.True(browser.Contains(word, StringComparison.Ordinal),
                $"nothing under web/ contains \"{word}\", so the guard is scanning for a name "
                + "that does not exist and would pass whatever the rules code did."));
    }

    /// <summary>
    /// Nothing under <c>engine/</c> or <c>sheets/</c> so much as makes an HTTP call.
    ///
    /// <para>The stronger form of the rule above, and the one that would catch an account
    /// arriving by a name this file does not know. The engine has no filesystem access by
    /// design; it has no network either, and both are properties a host provides rather than
    /// something rules code reaches for.</para>
    /// </summary>
    [Fact]
    public void TheEngineHasNoNetwork()
    {
        var offenders = RulesSources()
            .Where(f => Regex.IsMatch(File.ReadAllText(f),
                @"\bHttpClient\b|\bHttpRequestMessage\b|System\.Net\.Http",
                RegexOptions.None, TimeSpan.FromSeconds(5)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0,
            "The engine reads through IRulesSource and reaches for nothing else. These make an "
            + "HTTP call: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// Every address the browser asks for is one the server answers.
    ///
    /// <para><b>Both suites are green while these disagree.</b> The server's tests drive the
    /// server and the browser's tests drive a stub of it, so a renamed route breaks only the
    /// deployed site — where it appears as an app that starts, works, and quietly signs nobody
    /// in. This is the only thing that reads both sides.</para>
    /// </summary>
    [Fact]
    public void EveryAddressTheBrowserAsksForIsOneTheServerAnswers()
    {
        var routed = ServerSource();

        // **The character class was `[a-z/]` and a reviewer walked through it.** Renaming a route
        // to `api/auth/verify-token` made the pattern fail to match the literal at all, so the
        // address was silently dropped from the list and the test passed while the browser called
        // something the server does not route — the exact drift this test exists for. A pattern
        // that answers "not an address" when it means "I cannot read this" is worse than none.
        var asked = Regex.Matches(BrowserSource(), @"""(api/[A-Za-z0-9/_.-]+)(?:\?[^""]*)?""",
                RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => "/" + m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(asked.Count >= 5,
            $"only {asked.Count} addresses found in the browser's source; the pattern has stopped "
            + "matching and this test is asserting nothing.");

        var unanswered = asked
            .Where(address => !routed.Contains($"'{address}'", StringComparison.Ordinal))
            .ToList();

        Assert.True(unanswered.Count == 0,
            "The browser asks for these and worker/index.js routes none of them: "
            + string.Join(", ", unanswered));
    }

    /// <summary>
    /// The identity on the wire is spelled the same at both ends.
    ///
    /// <para><b>This was a <c>Contains</c> first, and a mutation walked straight through it.</b>
    /// Renaming the server's <c>displayName</c> to <c>display_name</c> left the guard green,
    /// because the word still occurred elsewhere in the server's own source — as a parameter
    /// name in <c>db.js</c>. Searching concatenated files for a word says nothing about where
    /// the word is. So both ends are read structurally: the keys of the object the server
    /// actually returns, and the names the client actually binds.</para>
    ///
    /// <para>The failure it guards against is silent in the worst way:
    /// <c>ReadFromJsonAsync</c> answers null for a property it cannot find, and the client reads
    /// null as "nobody is signed in". A working sign-in that signs nobody in.</para>
    /// </summary>
    [Fact]
    public void TheIdentityOnTheWireIsSpelledTheSameAtBothEnds()
    {
        // The server: the object literal `identityOf` returns, and only that one.
        var returned = Regex.Match(ServerSource(),
            @"function identityOf\([^)]*\)\s*\{\s*return\s*\{(?<body>[^}]*)\}",
            RegexOptions.None, TimeSpan.FromSeconds(5));

        Assert.True(returned.Success,
            "worker/auth.js no longer has an identityOf returning an object literal, so this "
            + "test cannot see what the server sends and would pass whatever it sent.");

        var sends = Regex.Matches(returned.Groups["body"].Value, @"(\w+)\s*:",
                RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups[1].Value)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // The client: the names it binds, off the record it deserializes into. Read from the
        // source rather than by reflection, because this project may not reference web/ — the
        // same reason WebPresentationTests reads that source too.
        var record = Regex.Match(File.ReadAllText(
                Path.Combine(RulesFixture.RepoRoot, "web", "Services", "Accounts.cs")),
            @"record Wired\((?<body>[^;]*)\);",
            RegexOptions.Singleline, TimeSpan.FromSeconds(5));

        Assert.True(record.Success,
            "Accounts.cs no longer declares a Wired record, so there is nothing to compare the "
            + "server's answer against and this test would pass whatever the server sent.");

        var reads = Regex.Matches(record.Groups["body"].Value, @"JsonPropertyName\(""(\w+)""\)",
                RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups[1].Value)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.True(reads.Length == 2,
            "the client binds " + reads.Length + " fields on an identity: " + string.Join(", ", reads));

        Assert.True(sends.Length == 2,
            "the server sends " + sends.Length + " fields on an identity: " + string.Join(", ", sends));

        Assert.Equal(reads, sends);
    }

    /// <summary>
    /// Every field the browser sends is a field the server reads.
    ///
    /// <para>Read off both sides rather than listed, for the reason above. The client's fields
    /// are the properties of the anonymous objects it posts and the keys it puts in a query
    /// string; the server's are what it pulls out of a parsed body or a search parameter.</para>
    /// </summary>
    [Fact]
    public void EveryFieldTheBrowserSendsIsOneTheServerReads()
    {
        var browser = BrowserSource();

        var posted = Regex.Matches(browser, @"PostAsJsonAsync\(""[^""]+"",\s*new\s*\{\s*(\w+)",
                RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups[1].Value);

        var queried = Regex.Matches(browser, @"""api/[A-Za-z0-9/_.-]+\?(\w+)=",
                RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups[1].Value);

        var sends = posted.Concat(queried).Distinct(StringComparer.Ordinal).ToList();

        Assert.True(sends.Count >= 3,
            $"only {sends.Count} sent fields found in the browser's source; the patterns have "
            + "stopped matching and this test is asserting nothing.");

        var server = ServerSource();

        var unread = sends
            .Where(field => !server.Contains($"value?.{field}", StringComparison.Ordinal)
                         && !server.Contains($"searchParams.get('{field}')", StringComparison.Ordinal))
            .ToList();

        Assert.True(unread.Count == 0,
            "The browser sends these and the server reads none of them: " + string.Join(", ", unread));
    }

    /// <summary>
    /// The book is not in the browser payload, checked from this side of the repository too.
    ///
    /// <para><c>web/</c>'s csproj stages <c>data/rules</c> and <c>data/transcripts</c> into
    /// <c>wwwroot</c> and nothing else. Adding <c>data/rulebook</c> there is one
    /// <c>ItemGroup</c>, it would look exactly like the two above it, and it would put the
    /// publisher's prose on the open web with no sign-in in front of it.</para>
    /// </summary>
    [Fact]
    public void TheRulebookIsNotStagedIntoTheSite()
    {
        var csproj = File.ReadAllText(
            Path.Combine(RulesFixture.RepoRoot, "web", "ProwlersAndParagons.Web.csproj"));

        Assert.DoesNotContain("data\\rulebook", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data/rulebook", csproj, StringComparison.OrdinalIgnoreCase);

        // The positive control: it does stage the two that are meant to be public.
        Assert.Contains("data\\rules", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data\\transcripts", csproj, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// No secret is in the repository, and the one place a key is named is a name rather than a
    /// value.
    ///
    /// <para>This project has handled no credentials at all until now, and an account system is
    /// the first thing that could quietly change that. The mail provider's key lives in a
    /// Cloudflare secret; what is in the source is the <em>name</em> of the environment variable
    /// it arrives in.</para>
    /// </summary>
    [Fact]
    public void NoKeyOrTokenIsInTheRepository()
    {
        var suspicious = new Regex(
            @"(re_[A-Za-z0-9_]{16,})|(sk_live_[A-Za-z0-9]+)|([A-Za-z0-9_\-]{24,}\.[A-Za-z0-9_\-]{16,}\.[A-Za-z0-9_\-]{16,})",
            RegexOptions.None, TimeSpan.FromSeconds(5));

        var offenders = ServerFiles()
            .Concat(Directory.EnumerateFiles(Path.Combine(RulesFixture.RepoRoot, "web"), "*.cs",
                SearchOption.AllDirectories).Where(NotBuildOutput))
            .Where(f => suspicious.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Something that looks like a live credential is committed in: "
            + string.Join(", ", offenders));

        // The positive control: the key is reached for by name, so the scan is over code that
        // really does handle one.
        Assert.Contains("RESEND_API_KEY", ServerSource(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The setup document names every binding and secret the server actually reads.
    ///
    /// <para><b>Documentation of a configuration rots silently, and this one cannot be tried
    /// out.</b> Every step in it needs the Cloudflare account's own credentials, so nobody
    /// working in this repository can discover that a name has changed — and the failure it
    /// produces is a site that deploys, works, and signs nobody in.</para>
    ///
    /// <para>The names are taken from the server rather than listed, so a new one has to be
    /// documented on the day it is added. <c>McpSetupDocumentationTests</c> exists for the same
    /// reason and caught two errors on its first run.</para>
    /// </summary>
    [Fact]
    public void TheSetupDocumentNamesEverythingTheServerReadsFromItsEnvironment()
    {
        var doc = File.ReadAllText(
            Path.Combine(RulesFixture.RepoRoot, "docs", "ACCOUNTS-SETUP.md"));

        // Everything reached for as env.SOMETHING: the D1 binding and the three settings.
        var needed = Regex.Matches(ServerSource(), @"env\.([A-Z][A-Z0-9_]*)",
                RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(needed.Count >= 4,
            $"only {needed.Count} environment names found in the server ({string.Join(", ", needed)}); "
            + "the pattern has stopped matching and this test is asserting nothing.");

        var undocumented = needed
            .Where(name => !doc.Contains(name, StringComparison.Ordinal))
            .ToList();

        Assert.True(undocumented.Count == 0,
            "The server reads these from its environment and docs/ACCOUNTS-SETUP.md never names "
            + "them, so nobody setting the site up would know to create them: "
            + string.Join(", ", undocumented));

        // And the migration command it gives really names the config that exists.
        Assert.Contains("--cwd d1", doc, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(RulesFixture.RepoRoot, "d1", "wrangler.toml")),
            "the document tells somebody to run wrangler against d1/wrangler.toml and it is not there.");
    }

    /// <summary>
    /// The D1 binding is spelled the same in the server, the migration config and the document.
    ///
    /// <para>Three places, and getting it wrong in any one of them answers every request with a
    /// 500 while the deploy reports success.</para>
    /// </summary>
    [Fact]
    public void TheDatabaseBindingIsSpelledTheSameInAllThreePlaces()
    {
        var toml = File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "d1", "wrangler.toml"));

        var bound = Regex.Match(toml, @"^\s*binding\s*=\s*""(\w+)""",
            RegexOptions.Multiline, TimeSpan.FromSeconds(5));

        Assert.True(bound.Success, "d1/wrangler.toml declares no binding name.");

        var name = bound.Groups[1].Value;

        Assert.Contains($"env.{name}", ServerSource(), StringComparison.Ordinal);
        Assert.Contains($"`{name}`",
            File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "docs", "ACCOUNTS-SETUP.md")),
            StringComparison.Ordinal);
    }

    private static string ServerSource() => string.Concat(ServerFiles().Select(File.ReadAllText));

    private static IEnumerable<string> ServerFiles() =>
        Directory.EnumerateFiles(Path.Combine(RulesFixture.RepoRoot, "worker"), "*.js")
            .OrderBy(f => f, StringComparer.Ordinal);

    private static string BrowserSource() => string.Concat(
        Directory.EnumerateFiles(Path.Combine(RulesFixture.RepoRoot, "web"), "*.cs",
                SearchOption.AllDirectories)
            .Where(NotBuildOutput)
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static IEnumerable<string> RulesSources() =>
        RulesProjects
            .Select(p => Path.Combine(RulesFixture.RepoRoot, p))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(NotBuildOutput)
            .OrderBy(f => f, StringComparer.Ordinal);

    private static bool NotBuildOutput(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
