using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Loads the real <c>data/rules/play</c> JSON once for the whole run, through the same
/// <see cref="IRulesSource"/> a host would use.
///
/// <para>Same reasoning as <see cref="RulesFixture"/>: the suite asserts against the shipped data
/// deliberately, because its job is to catch a rules file drifting away from the book — and, here,
/// to catch the engine drifting away from the file.</para>
/// </summary>
public sealed class PlayFixture
{
    /// <summary>The shipped play rules.</summary>
    public PlayRulesRepository Play { get; } = new(new FileSystemRulesSource(DataPath));

    /// <summary>The <c>data/rules/play</c> directory.</summary>
    public static string DataPath => Path.Combine(RulesFixture.RepoRoot, "data", "rules", "play");
}

/// <summary>
/// Shares one <see cref="PlayFixture"/> across the play engine's test classes, so the five files
/// are parsed once per run. Not named *Collection: CA1711 reserves that suffix.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SharedPlayRules : ICollectionFixture<PlayFixture>
{
    public const string Name = "play rules";
}
