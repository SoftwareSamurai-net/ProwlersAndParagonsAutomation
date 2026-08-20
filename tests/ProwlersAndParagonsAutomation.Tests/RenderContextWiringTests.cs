using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>Every service the app registers is registered in the test context too.</b>
///
/// <para><c>RenderContext</c>'s own summary claims it is "wired the way <c>web/Program.cs</c> wires
/// the real app: the same services". Nothing enforced that, and in one afternoon it was false twice
/// — <c>SavedCharacters</c> and then <c>CharacterImport</c> were each added to <c>Program.cs</c> and
/// not to the context.</para>
///
/// <para><b>What makes it worth a guard is how it fails.</b> A service missing from the context is
/// not one failing test about that service: it is every render test in the project failing at once
/// on <c>Unable to resolve service</c>, because the thing that cannot be constructed is a
/// constructor argument of something the shell needs. Three hundred and sixty red tests do say
/// something is wrong, but they say it about everything, and the actual cause is one missing line
/// in a file nobody was editing.</para>
///
/// <para>Read from source rather than by running the two, because this project may not reference
/// <c>web/</c> from here — the same reason <c>WebPresentationTests</c> reads that source too.</para>
/// </summary>
public sealed class RenderContextWiringTests
{
    /// <summary>
    /// Services the context deliberately does not take from the app, each with its reason.
    ///
    /// <para><b>Empty, and that is a claim rather than an oversight.</b> The first version of this
    /// list named eight — the rules repository, the calculators, the replay library, the HTTP client
    /// — on the reasoning that the context builds those itself from files on disk. It does, but they
    /// are registered in <c>Program.cs</c> <em>non-generically</em> (<c>AddSingleton(rules)</c>,
    /// <c>AddScoped(_ =&gt; http)</c>), so the scan below never sees them and they could not have
    /// been reported missing. The positive control caught the list being wrong, which is the only
    /// reason it exists.</para>
    ///
    /// <para><b>So the comparison covers the generic registrations only</b>, and that is the right
    /// scope rather than a shortcut: an <c>AddSingleton(instance)</c> is the context's own business,
    /// built from real data by design, while <c>AddScoped&lt;Foo&gt;()</c> is a service the
    /// container constructs — and the failure this guards against is one of those appearing in the
    /// app and not the context.</para>
    /// </summary>
    private static readonly Dictionary<string, string> Deliberate = new(StringComparer.Ordinal);

    [Fact]
    public void TheTestContextRegistersEveryServiceTheAppDoes()
    {
        var missing = InApp().Where(s => !InContext().Contains(s) && !Deliberate.ContainsKey(s)).ToList();

        Assert.True(missing.Count == 0,
            "web/Program.cs registers these and tests/ProwlersAndParagons.Web.Tests/RenderContext.cs "
            + "does not, so every render test in that project will fail at once on \"Unable to "
            + "resolve service\" as soon as anything the shell needs takes one as an argument: "
            + string.Join(", ", missing)
            + ". Add it to RenderContext, or name it in this test's exemption list with a reason.");
    }

    /// <summary>
    /// The positive control, and it is not optional: a scan that found nothing in either file would
    /// report perfect agreement between two empty sets.
    /// </summary>
    [Fact]
    public void TheGuardIsLookingAtSomething()
    {
        Assert.True(InApp().Count > 8, $"only {InApp().Count} services found in Program.cs");
        Assert.True(InContext().Count > 8, $"only {InContext().Count} found in RenderContext.cs");

        // And every exemption names something the app really registers, so a service that has been
        // renamed away cannot go on excusing itself for ever.
        var stale = Deliberate.Keys.Where(s => !InApp().Contains(s)).ToList();

        Assert.True(stale.Count == 0,
            "these are exempted and web/Program.cs no longer registers them: " + string.Join(", ", stale));
    }

    /// <summary>
    /// Every type named in an <c>AddScoped&lt;…&gt;</c>/<c>AddSingleton&lt;…&gt;</c> on either side,
    /// plus the ones registered by factory as <c>AddScoped&lt;IFace&gt;(s =&gt; …)</c> — the interface
    /// <em>and</em> the concrete type both count, because either can be the one a constructor asks for.
    /// </summary>
    private static HashSet<string> Registered(string source) =>
        [.. Regex.Matches(source, "Add(?:Scoped|Singleton|Transient)<([^>(]+)>",
                RegexOptions.None, TimeSpan.FromSeconds(5))
            .SelectMany(m => m.Groups[1].Value.Split(','))
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)];

    private static HashSet<string> InApp() =>
        Registered(File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "web", "Program.cs")));

    private static HashSet<string> InContext() =>
        Registered(File.ReadAllText(Path.Combine(
            RulesFixture.RepoRoot, "tests", "ProwlersAndParagons.Web.Tests", "RenderContext.cs")));
}
