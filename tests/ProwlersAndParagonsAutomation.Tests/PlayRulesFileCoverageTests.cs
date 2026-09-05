using System.Text.Json;
using System.Text.Json.Serialization;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Play.Rules;
using ProwlersAndParagonsAutomation.Play.Rules.Models;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>Every field in every play rules file is read by some model in <c>play/</c>.</b>
///
/// <para>The same check <see cref="RulesFileCoverageTests"/> makes for the character rules, for the
/// same reason and against the same failure: <c>creation_rules.json</c> carried nine top-level keys
/// while its model declared four, and the five that deserialized into nothing included a block of
/// formulas that had rotted away from the engine while reading as a source of truth. <b>Unread data
/// is worse than missing data</b> — missing data is a gap somebody can see.</para>
///
/// <para><b>The strictness lives here and not in <see cref="PlayRulesRepository"/>.</b> That
/// repository ignores an unmapped field at runtime, so a data edit is a failing test rather than a
/// broken host; this re-reads the same five files with
/// <see cref="JsonUnmappedMemberHandling.Disallow"/> and fails naming the field.</para>
///
/// <para><b>It is a second reader of these files and that is deliberate.</b>
/// <see cref="PlayRulesDataTests"/> deserializes them into test-local records to compare every fact
/// field against <c>CanonicalCombatRules</c> and its siblings — the transcription. These models are
/// what the engine consumes. A field added to the data has to be added to both, which is the point:
/// a field the engine does not read cannot quietly become a rule the engine applies.</para>
/// </summary>
public sealed class PlayRulesFileCoverageTests
{
    private static string PlayDataPath =>
        Path.Combine(RulesFixture.RepoRoot, "data", "rules", "play");

    /// <summary>The repository's own options, plus the refusal it must not make at runtime.</summary>
    private static JsonSerializerOptions Strict() => new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        UnmappedMemberHandling      = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>Which model each play rules file is read into by <see cref="PlayRulesRepository"/>.</summary>
    private static readonly (string File, Type Model)[] Coverage =
    [
        (PlayRulesRepository.PlayMetaFile,  typeof(PlayFile<PlayMetaEntry>)),
        (PlayRulesRepository.ChallengeFile, typeof(PlayFile<ChallengeEntry>)),
        (PlayRulesRepository.CombatFile,    typeof(PlayFile<CombatEntry>)),
        (PlayRulesRepository.GrittyFile,    typeof(PlayFile<GrittyEntry>)),
        (PlayRulesRepository.ResolveFile,   typeof(PlayFile<ResolveEntry>))
    ];

    public static TheoryData<string, Type> FilesAndModels()
    {
        var data = new TheoryData<string, Type>();
        foreach (var (file, model) in Coverage) data.Add(file, model);
        return data;
    }

    [Theory]
    [MemberData(nameof(FilesAndModels))]
    public void EveryFieldInAPlayRulesFileIsReadBySomeModel(string fileName, Type model)
    {
        var json = File.ReadAllText(Path.Combine(PlayDataPath, fileName));

        var ex = Record.Exception(() => JsonSerializer.Deserialize(json, model, Strict()));

        Assert.True(ex is null,
            $"{fileName} carries a field no model in play/ reads, so the engine cannot apply it "
            + $"and nothing holds it to the rulebook: {ex?.Message}");
    }

    /// <summary>
    /// The list above has to be the list the repository actually loads, or a new play rules file
    /// arrives uncovered and this whole class says nothing about it.
    /// </summary>
    [Fact]
    public void EveryFileTheRepositoryLoadsIsCoveredHere()
    {
        Assert.Equal(
            PlayRulesRepository.DataFileNames.Order(StringComparer.Ordinal),
            Coverage.Select(c => c.File).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <b>The repository really does load all five, through the seam it says it does.</b> The
    /// coverage theory above deserializes files by hand; it would pass unchanged if
    /// <see cref="PlayRulesRepository"/> had stopped reading one of them, or had never been wired
    /// to <see cref="IRulesSource"/> at all. This drives the repository itself, over the shipped
    /// data, and requires each file to yield a header and entries.
    /// </summary>
    [Fact]
    public void TheRepositoryReadsAllFiveFilesThroughIRulesSource()
    {
        var play = new PlayRulesRepository(new FileSystemRulesSource(PlayDataPath));

        Assert.Equal(6, play.Meta.Entries.Count);
        Assert.Equal(15, play.Challenge.Entries.Count);
        Assert.Equal(51, play.Combat.Entries.Count);
        Assert.Equal(11, play.Gritty.Entries.Count);
        Assert.Equal(28, play.Resolve.Entries.Count);

        foreach (var header in new[]
                 {
                     play.Meta.Header, play.Challenge.Header, play.Combat.Header,
                     play.Gritty.Header, play.Resolve.Header
                 })
        {
            Assert.False(string.IsNullOrWhiteSpace(header.WhatThisIs));
            Assert.False(string.IsNullOrWhiteSpace(header.SourceRef));
        }

        // The lookup is a throw rather than a null, so a rule that has been renamed away cannot
        // resolve to nothing and leave the engine computing a plausible figure with no rule
        // behind it. Both directions are checked: a real id resolves, an invented one throws.
        Assert.Equal("damage", play.GetCombat("damage").Id);
        Assert.Throws<KeyNotFoundException>(() => play.GetCombat("no_such_entry"));
    }

    /// <summary>
    /// <b>The two file lists never meet.</b> A play file on
    /// <see cref="RulesRepository.DataFileNames"/> would be fetched by every browser at boot —
    /// which is the exposure <c>PlayPayloadTests</c> exists to prevent, reached by the one route
    /// that does not go through a csproj glob.
    /// </summary>
    [Fact]
    public void NeitherFileListHoldsTheOthersFiles()
    {
        Assert.True(RulesRepository.DataFileNames.Count >= 11);
        Assert.Equal(5, PlayRulesRepository.DataFileNames.Count);

        Assert.Empty(PlayRulesRepository.DataFileNames.Intersect(RulesRepository.DataFileNames, StringComparer.Ordinal));
    }
}
