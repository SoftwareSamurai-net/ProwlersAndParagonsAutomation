using System.Collections;
using System.Reflection;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The rules come back whole. <b>The failure mode this guards is silence.</b>
///
/// <para>Reflection-based deserialization is opaque to the IL trimmer, which is free to remove a
/// model property nothing appears to read — and the result is not a build error but a rules set
/// that loads with holes in it. That is why the browser build does not trim, and why the payload
/// is what it is; see <c>PROGRESS.md</c> item 4.</para>
///
/// <para><b>This exists because replacing the reflective reader with a source-generated one was
/// tried and abandoned</b>, and the abandoning is the useful part. A
/// <c>JsonSerializerContext</c> over the eleven rules types builds clean, loads every file, and
/// returns <b>null</b> for six collection properties that are declared non-null with an
/// <c>= []</c> initializer: <c>PowerModel.PowerPros</c> and <c>PowerCons</c>, and the four
/// applicability lists on <c>ProModel</c> and <c>ConModel</c>. <c>CostCalculator</c>
/// dereferences the first of those for any Power carrying a Pro.</para>
///
/// <para>It surfaced loudly only because something had just started reading two of the six. The
/// other four would have been a quiet wrong answer — which is the same shape as the trimming
/// hazard the change was meant to remove. So the test stayed and the change did not.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class RulesLoadingTests
{
    private readonly RulesFixture _f;

    public RulesLoadingTests(RulesFixture f) => _f = f;

    /// <summary>
    /// Every collection on every rules model that came back, checked for null.
    ///
    /// <para>A collection property declared with an <c>= []</c> initializer must never be null
    /// after loading: absent in the JSON means empty, and every caller reads <c>.Count</c>
    /// without asking. This is the exact difference that broke the applicability check the day
    /// the reflective reader was replaced — and it broke <em>loudly</em> only because something
    /// had just started reading those two properties. Nothing was reading most of the others.
    /// </para>
    /// </summary>
    [Fact]
    public void NoCollectionOnAnyLoadedRulesModelComesBackNull()
    {
        var models = new List<object>();
        models.AddRange(_f.Rules.Tiers);
        models.AddRange(_f.Rules.Abilities);
        models.AddRange(_f.Rules.Talents);
        models.AddRange(_f.Rules.Powers);
        models.AddRange(_f.Rules.Pros);
        models.AddRange(_f.Rules.Cons);
        models.AddRange(_f.Rules.Flaws);
        models.AddRange(_f.Rules.Perks);
        models.AddRange(_f.Rules.GearFeatures);
        models.AddRange(_f.Rules.Sources);
        models.Add(_f.Rules.CreationRules);

        Assert.NotEmpty(models);

        var nulls = new List<string>();

        foreach (var model in models)
        {
            foreach (var property in model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0) continue;
                if (property.PropertyType == typeof(string)) continue;
                if (!typeof(IEnumerable).IsAssignableFrom(property.PropertyType)) continue;

                // Only the ones the type says cannot be null. A nullable collection means
                // "the rulebook may say nothing here", which is a different statement.
                var nullability = new NullabilityInfoContext().Create(property);
                if (nullability.ReadState != NullabilityState.NotNull) continue;

                if (property.GetValue(model) is null)
                    nulls.Add($"{model.GetType().Name}.{property.Name}");
            }
        }

        Assert.True(nulls.Count == 0,
            "These collections came back null from the rules files, where their declared type "
            + "says they cannot be: " + string.Join(", ", nulls.Distinct()));
    }

    /// <summary>
    /// The rules actually have contents. A trimmed build's failure is an empty rules set rather
    /// than an exception, so the counts are asserted against what the files hold — 141 Powers is
    /// the number the rest of this suite is built on.
    /// </summary>
    [Fact]
    public void TheRulesLoadWithTheirContents()
    {
        Assert.Equal(141, _f.Rules.Powers.Count);
        Assert.Equal(6, _f.Rules.Tiers.Count);
        Assert.Equal(6, _f.Rules.Abilities.Count);
        Assert.Equal(12, _f.Rules.Talents.Count);
        Assert.Equal(6, _f.Rules.Sources.Count);
        Assert.Equal(12, _f.Rules.GearFeatures.Count);
        Assert.NotEmpty(_f.Rules.CreationRules.OptionalPackages);

        // And a property deep inside one, since a whole entry is easier to keep than a field on
        // it: Armor's baseline is half Toughness, and nothing else in the file says so.
        var armor = _f.Rules.GetPower("armor")!;
        Assert.Equal("baseline_half", armor.Prerequisite?.Relationship);
        Assert.Equal("toughness", armor.Prerequisite?.Ability);
    }

    /// <summary>
    /// Every file the repository lists actually loads. <c>DataFileNames</c> is the contract a
    /// self-loading host works from — a browser cannot glob a directory that is not there — and
    /// a file listed but unreadable, or readable but unlisted, is a browser build running on an
    /// incomplete rules set.
    /// </summary>
    [Fact]
    public void EveryRulesFileTheRepositoryListsLoads()
    {
        var exception = Record.Exception(() =>
        {
            _ = _f.Rules.Tiers; _ = _f.Rules.Abilities; _ = _f.Rules.Talents; _ = _f.Rules.Powers;
            _ = _f.Rules.Pros; _ = _f.Rules.Cons; _ = _f.Rules.Flaws; _ = _f.Rules.Perks;
            _ = _f.Rules.GearFeatures; _ = _f.Rules.Sources; _ = _f.Rules.CreationRules;
        });

        Assert.Null(exception);
        Assert.Equal(11, RulesRepository.DataFileNames.Count);
    }
}
