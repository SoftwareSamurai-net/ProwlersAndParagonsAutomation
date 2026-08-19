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

        var asked = Regex.Matches(BrowserSource(), @"""(api/[a-z/]+)(?:\?[^""]*)?""",
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
    /// The fields on the wire are spelled the same on both sides.
    ///
    /// <para>An identity is a key and a name; a sign-in request carries an address; a
    /// verification carries a token; an entry is asked for by name. Each is one word, and each
    /// is a word that would fail silently: <c>ReadFromJsonAsync</c> answers null for a property
    /// it cannot find, which the client reads as "nobody is signed in".</para>
    /// </summary>
    [Theory]
    [InlineData("key")]
    [InlineData("displayName")]
    [InlineData("email")]
    [InlineData("token")]
    [InlineData("name")]
    public void EveryFieldOnTheWireIsSpelledTheSameOnBothSides(string field)
    {
        Assert.Contains(field, ServerSource(), StringComparison.Ordinal);
        Assert.Contains(field, BrowserSource(), StringComparison.Ordinal);
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
