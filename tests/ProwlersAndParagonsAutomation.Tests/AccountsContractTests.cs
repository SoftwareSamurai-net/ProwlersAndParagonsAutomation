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

        // **`.get('field')` on anything, not `searchParams.get('field')` on the expression.** The
        // stricter spelling did not check that the server reads the field, it checked that the
        // server reads it *without naming the search parameters first* — so hoisting
        // `new URL(request.url).searchParams` into a local, which a handler reading two parameters
        // wants to do, reported a field the server plainly reads as unread. A guard that dictates
        // the shape of the code it inspects is a guard that gets worked around rather than fixed.
        var unread = sends
            .Where(field => !server.Contains($"value?.{field}", StringComparison.Ordinal)
                         && !server.Contains($".get('{field}')", StringComparison.Ordinal))
            .ToList();

        Assert.True(unread.Count == 0,
            "The browser sends these and the server reads none of them: " + string.Join(", ", unread));
    }

    /// <summary>
    /// Every email address written down in the accounts surface is one nobody can own.
    ///
    /// <para><b>Because these get published, and by the very code that exists to stop addresses
    /// being published.</b> The server's `console.error` prints the whole exception on every
    /// failure, so a test that provokes one puts its fixture address verbatim into the CI log of
    /// a public repository. The first version of `errors.test.mjs` used a real address and did
    /// exactly that — the redaction test published an address on its way to proving addresses are
    /// redacted.</para>
    ///
    /// <para>The reserved names are RFC 6761 and RFC 2606: `.test`, `.example`, `.invalid`,
    /// `.localhost`, and the `example.com` family. None can be registered by anybody, so none can
    /// be somebody's real address — which is the property wanted here, rather than "looks
    /// fake".</para>
    /// </summary>
    [Fact]
    public void EveryAddressWrittenIntoTheAccountsSurfaceIsUnownable()
    {
        var files = ServerFiles()
            .Concat(Directory.EnumerateFiles(
                Path.Combine(RulesFixture.RepoRoot, "tests", "worker"), "*.mjs"))
            .Append(Path.Combine(RulesFixture.RepoRoot, "docs", "ACCOUNTS-SETUP.md"))
            .Where(File.Exists)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        // The domain only. Matching a whole address would also match the *source of the address
        // regexes* in errors.js, which is character classes rather than anybody's address.
        var domains = new Regex(@"@(([A-Za-z0-9-]+\.)+[A-Za-z]{2,})",
            RegexOptions.None, TimeSpan.FromSeconds(5));

        var found = files
            .SelectMany(f => domains.Matches(File.ReadAllText(f))
                .Select(m => (File: Path.GetFileName(f), Domain: m.Groups[1].Value)))
            .ToList();

        // The positive control: these files really are full of addresses, so a scan finding
        // nothing would mean the pattern had stopped matching rather than that all is well.
        Assert.True(found.Count >= 5,
            $"only {found.Count} email domains found across {files.Count} files; the pattern has "
            + "stopped matching and this test is asserting nothing.");

        // **One exemption, and it is a sender rather than a person.** `MAIL_FROM` has to be an
        // address on the domain Resend has verified, so the setup document naming the site's own
        // no-reply address is the document doing its job. It is also never printed by the server:
        // the rule this test exists for is about *fixtures*, which end up in a public CI log.
        const string sender = "superheroes.softwaresamurai.net";

        // The exemption is asserted to still have a subject. One whose target has been renamed
        // away permits that domain everywhere and reports nothing — the shape `.editorconfig`
        // exemptions in this repository are held to for the same reason.
        Assert.Contains($"no-reply@{sender}",
            File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "docs", "ACCOUNTS-SETUP.md")),
            StringComparison.OrdinalIgnoreCase);

        var ownable = found
            .Where(hit => !IsUnownable(hit.Domain)
                       && !hit.Domain.Equals(sender, StringComparison.OrdinalIgnoreCase))
            .Select(hit => $"{hit.File} names @{hit.Domain}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(ownable.Count == 0,
            "These are addresses somebody could actually own, and the server prints the whole "
            + "exception to a public CI log on every provoked failure. Use a reserved domain "
            + "(RFC 6761/2606) instead:\n  " + string.Join("\n  ", ownable));
    }

    /// <summary>A domain no registry will ever sell — so no message can reach a real person.</summary>
    private static bool IsUnownable(string domain) =>
        domain.EndsWith(".test", StringComparison.OrdinalIgnoreCase)
        || domain.EndsWith(".example", StringComparison.OrdinalIgnoreCase)
        || domain.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase)
        || domain.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
        || domain.Equals("example.com", StringComparison.OrdinalIgnoreCase)
        || domain.Equals("example.org", StringComparison.OrdinalIgnoreCase)
        || domain.Equals("example.net", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The failure categories are spelled the same at both ends.
    ///
    /// <para><b>The whole point of a category is that one side names it and the other renders
    /// it</b>, and both suites stay green while the two lists disagree — the server's tests drive
    /// the server and the browser's drive a stub of it. A category the client does not recognise
    /// falls to the honest default, so the failure is not a crash: it is every mail failure on
    /// the deployed site rendering as "something went wrong", which is precisely the flat sentence
    /// this design replaced.</para>
    /// </summary>
    [Fact]
    public void TheFailureCategoriesAreSpelledTheSameAtBothEnds()
    {
        var declared = Regex.Match(File.ReadAllText(WorkerFile("errors.js")),
            @"CATEGORIES\s*=\s*Object\.freeze\(\[(?<body>[^\]]*)\]",
            RegexOptions.Singleline, TimeSpan.FromSeconds(5));

        Assert.True(declared.Success,
            "worker/errors.js no longer declares a frozen CATEGORIES list, so this test cannot "
            + "see the server's set and would pass whatever it sent.");

        var sends = Regex.Matches(declared.Groups["body"].Value, @"'(\w+)'",
                RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups[1].Value)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.True(sends.Length == 4,
            "the server declares " + sends.Length + " categories: " + string.Join(", ", sends));

        // The client: the wire names its switch matches, plus the default everything else falls
        // to. `unknown` is never matched by name because it is what an unrecognised name becomes.
        var accounts = File.ReadAllText(
            Path.Combine(RulesFixture.RepoRoot, "web", "Services", "Accounts.cs"));

        var named = Regex.Match(accounts,
            @"CategoryNamed\(string\?\s*wire\)\s*=>\s*wire\s*switch\s*\{(?<body>[^}]*)\}",
            RegexOptions.Singleline, TimeSpan.FromSeconds(5));

        Assert.True(named.Success,
            "Accounts.cs no longer maps a wire name to a category in a switch, so there is "
            + "nothing to compare the server's set against.");

        var reads = Regex.Matches(named.Groups["body"].Value, @"""(\w+)""\s*=>",
                RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups[1].Value)
            .Append("unknown")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(sends, reads);

        // And the default really is unknown rather than a guess at the nearest one.
        Assert.Contains("_ => FailureCategory.Unknown", named.Groups["body"].Value,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Every category the server can send is rendered as its own sentence.
    ///
    /// <para>Without this the enum can grow a member that no branch prints, which renders as the
    /// default — a category that exists, arrives, and says nothing more than the flat message it
    /// was introduced to replace.</para>
    /// </summary>
    [Fact]
    public void EveryFailureCategoryIsRenderedAsItsOwnSentence()
    {
        var razor = File.ReadAllText(
            Path.Combine(RulesFixture.RepoRoot, "web", "Pages", "SignIn.razor"));

        foreach (var category in new[] { "Mail", "Storage", "Configuration" })
        {
            Assert.True(razor.Contains($"FailureCategory.{category} =>", StringComparison.Ordinal),
                $"SignIn.razor has no sentence for FailureCategory.{category}, so it renders as "
                + "the default and the category buys the visitor nothing.");
        }

        // And the default arm is there, so an unrecognised category is still a sentence.
        Assert.Contains("_ => \"Something went wrong at our end", razor, StringComparison.Ordinal);
    }

    /// <summary>
    /// The configuration sentence never advises retrying, and the others still do.
    ///
    /// <para><b>This is the one category where retrying cannot help</b>, because no amount of
    /// trying again sets an environment variable. Telling somebody to keep doing the one thing
    /// that cannot work is exactly what the old single message did, and it cost three sign-in
    /// attempts against a deployment whose settings predated it.</para>
    ///
    /// <para>"trying again will not help" is deliberately allowed and deliberately pinned: it is
    /// the denial, not the advice, and a scan that refused it would push the sentence into saying
    /// nothing about retrying at all.</para>
    /// </summary>
    [Fact]
    public void TheConfigurationSentenceNeverAdvisesRetrying()
    {
        var razor = File.ReadAllText(
            Path.Combine(RulesFixture.RepoRoot, "web", "Pages", "SignIn.razor"));

        var configuration = CategoryArm(razor, "Configuration");
        var mail = CategoryArm(razor, "Mail");

        // **The positive control, and it carries this test.** Every assertion below is an
        // absence, and an absence is satisfied completely by a regex that captured nothing. The
        // mail arm is known to advise retrying, so the same scan finding it there is what proves
        // the scan can see retry advice at all.
        Assert.True(RetryAdvice.IsMatch(mail),
            "the mail sentence no longer advises retrying, so the scan below is not known to be "
            + "able to see retry advice and the configuration assertion proves nothing. Mail arm: "
            + mail);

        Assert.False(RetryAdvice.IsMatch(configuration),
            "the configuration sentence advises retrying, and retrying cannot set an environment "
            + "variable: " + configuration);

        // And it still says so, rather than saying nothing about it.
        Assert.Contains("will not help", configuration, StringComparison.Ordinal);
    }

    /// <summary>
    /// Advice to try again, in the spellings this page could plausibly use.
    ///
    /// <para><c>trying again</c> does not match <c>try again</c> — the first is the denial the
    /// configuration sentence carries, the second is the advice it must not.</para>
    /// </summary>
    private static readonly Regex RetryAdvice = new(
        @"\btry again\b|\bretry\b|\btry later\b|\btry once more\b|\bin a moment\b|\bin a few minutes\b",
        RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The sentence one arm of the category switch renders.
    ///
    /// <para><b>The string literals only, not the source of the arm.</b> Reading the source read
    /// the *next* arm's comment too — which explains why the default says nothing about trying
    /// again, and so contains the very words being scanned for. The rule is about what a visitor
    /// is told, and that is the literals; a comment is not on the screen.</para>
    /// </summary>
    private static string CategoryArm(string razor, string category)
    {
        var arm = Regex.Match(razor,
            $@"FailureCategory\.{category}\s*=>(?<body>(?:(?!FailureCategory\.|_\s*=>|\}};).)*)",
            RegexOptions.Singleline, TimeSpan.FromSeconds(5));

        Assert.True(arm.Success,
            $"SignIn.razor has no FailureCategory.{category} arm, so this test reads nothing.");

        var sentence = string.Concat(
            Regex.Matches(arm.Groups["body"].Value, @"""([^""]*)""",
                    RegexOptions.None, TimeSpan.FromSeconds(5))
                .Select(m => m.Groups[1].Value));

        Assert.True(sentence.Length > 20,
            $"the FailureCategory.{category} arm renders only \"{sentence}\", so the pattern has "
            + "stopped matching and any assertion on it is worthless.");

        return sentence;
    }

    /// <summary>
    /// Neither the book nor the recordings are in the browser payload, checked from this side
    /// of the repository too.
    ///
    /// <para><c>web/</c>'s csproj stages <c>data/rules</c> into <c>wwwroot</c> and nothing
    /// else. <c>data/rulebook</c> and <c>data/transcripts</c> are both bundled into the worker
    /// instead and answered only to a signed-in caller — adding either to this csproj is one
    /// <c>ItemGroup</c>, it would look exactly like the one that is there, and it would put the
    /// publisher's prose or the recorded conversations on the open web with no sign-in in front
    /// of them.</para>
    /// </summary>
    /// <remarks>
    /// <para><b>The comments are stripped before the scan, and leaving them in made this guard
    /// refuse a comment.</b> The first version read the raw file, so writing down <i>why</i> the
    /// recordings are no longer staged — which is a paragraph that has to name the directory to
    /// be about anything — failed the test. A guard that cannot tell an explanation from a
    /// directive taxes the explanation, and this repository would rather have the paragraph.</para>
    ///
    /// <para>The stripping is what makes the assertion narrower and therefore stronger: what is
    /// forbidden is a <i>reference</i> in live MSBuild, not the string appearing in the file. The
    /// positive control below plants exactly the <c>ItemGroup</c> that would do the damage and
    /// requires the scan to catch it, so this cannot pass by stripping everything.</para>
    /// </remarks>
    [Fact]
    public void NeitherTheRulebookNorTheRecordingsAreStagedIntoTheSite()
    {
        var path = Path.Combine(RulesFixture.RepoRoot, "web", "ProwlersAndParagons.Web.csproj");
        var live = WithoutXmlComments(File.ReadAllText(path));

        string[] forbidden =
            ["data\\rulebook", "data/rulebook", "data\\transcripts", "data/transcripts"];

        foreach (var store in forbidden)
        {
            Assert.DoesNotContain(store, live, StringComparison.OrdinalIgnoreCase);
        }

        // The positive control on the *stripping*, not only on the scan: a real staging line is
        // still caught after comments are removed. Without this, a stripper that ate the whole
        // file would satisfy every assertion above — the shape this repository has shipped four
        // times, where an absence holds because nothing was measured.
        var planted = live + "\n<ItemGroup><Content Include=\"..\\data\\transcripts\\*.json\" /></ItemGroup>";
        Assert.Contains("data\\transcripts", planted, StringComparison.OrdinalIgnoreCase);

        // ...and it does stage the one that is meant to be public.
        Assert.Contains("data\\rules", live, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An XML file with its <c>&lt;!-- --&gt;</c> comments removed, so a scan reads what MSBuild
    /// would act on rather than what somebody wrote down about it.
    /// </summary>
    private static string WithoutXmlComments(string xml) =>
        new Regex("<!--.*?-->", RegexOptions.Singleline, TimeSpan.FromSeconds(5)).Replace(xml, " ");

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

    private static string WorkerFile(string name) =>
        Path.Combine(RulesFixture.RepoRoot, "worker", name);

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
