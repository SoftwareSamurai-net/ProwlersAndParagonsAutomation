using System.Globalization;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Turns one of Chapter 8's twenty printed Heroes into a <see cref="CharacterSheet"/> the engines
/// can read.
///
/// <para><b>It is shared because two suites want the same twenty characters.</b>
/// <see cref="PrebuiltHeroTests"/> rebuilds them to check the engine reproduces the Edge, Health
/// and Resolve the authors printed; the encounter server's tests put two of them into a fight over
/// the wire, because a fight between characters somebody published is a stronger check than a fight
/// between two sheets written for the test. A private copy in each place would be two
/// transcriptions of one rulebook page, and they would disagree.</para>
///
/// <para>Printed Power ranks are final ranks, so a baseline-rank Power's purchased ranks are the
/// difference between the printed rank and the baseline its Traits provide.</para>
/// </summary>
public static class PrebuiltHeroSheets
{
    /// <summary>
    /// Parses the Pros and Cons a sheet prints for one Power, in the
    /// <c>kind:id[:variant][#units]</c> form <see cref="PrebuiltHeroes.ProsConsByHero"/> uses.
    /// </summary>
    private static (List<SelectedProCon> Pros, List<SelectedProCon> Cons) ProsCons(
        PrebuiltHeroes.Hero hero, string powerId)
    {
        var pros = new List<SelectedProCon>();
        var cons = new List<SelectedProCon>();

        if (!PrebuiltHeroes.ProsConsByHero.TryGetValue($"{hero.Name}|{powerId}", out var entries))
            return (pros, cons);

        foreach (var raw in entries)
        {
            var text  = raw;
            int? units = null;

            var hash = text.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
            {
                units = int.Parse(text[(hash + 1)..], CultureInfo.InvariantCulture);
                text  = text[..hash];
            }

            var parts = text.Split(':');
            Assert.True(parts.Length is 2 or 3, $"Malformed pro/con '{raw}' on {hero.Name}/{powerId}.");

            var choice = new SelectedProCon(parts[1], parts.Length == 3 ? parts[2] : null) { Units = units };
            (parts[0] == "pro" ? pros : cons).Add(choice);
        }

        return (pros, cons);
    }

    /// <summary>
    /// Turns a printed sheet into a CharacterSheet. Printed Power ranks are final ranks,
    /// so a baseline-rank Power's purchased ranks are the difference between the printed
    /// rank and the baseline its Traits provide.
    /// </summary>
    public static CharacterSheet Build(
        RulesRepository rules, DerivedStatsCalculator derived, PrebuiltHeroes.Hero hero) =>
        Build(rules, derived, hero, PrebuiltHeroes.BuildByHero[hero.Name].Package);

    /// <summary>
    /// Overload used only by <see cref="PrebuiltHeroTests.NoOtherPackageLandsAnyOfTheThreeUnclosedHeroesOnExactly125"/>
    /// to rebuild a Hero under a package other than the one recorded against them, so that test can
    /// sweep every package rather than trusting the one <see cref="PrebuiltHeroes.BuildByHero"/>
    /// already picked as closest.
    /// </summary>
    public static CharacterSheet Build(
        RulesRepository rules, DerivedStatsCalculator derived, PrebuiltHeroes.Hero hero, string packageId)
    {
        var sheet = new CharacterSheet
        {
            SelectedTierId    = "standard",
            SelectedPackageId = packageId,
            AbilityRanks =
            {
                ["agility"]    = hero.Agility,
                ["intellect"]  = hero.Intellect,
                ["might"]      = hero.Might,
                ["perception"] = hero.Perception,
                ["toughness"]  = hero.Toughness,
                ["willpower"]  = hero.Willpower
            }
        };

        foreach (var abilityId in sheet.AbilityRanks.Keys.ToList())
        {
            if (!PrebuiltHeroes.AbilityModifiersByHero.TryGetValue($"{hero.Name}|{abilityId}", out var mods))
                continue;

            sheet.AbilityModifiers[abilityId] =
                mods.Select(m => new SelectedProCon(m.Split(':')[1])).ToList();
        }

        // Abilities and Talents the sheet marks with a Source. Absent means the rulebook
        // default — Innate for an Ability, Trained for a Talent — which no sheet prints.
        foreach (var (key, traitIds) in PrebuiltHeroes.TraitSourcesByHero)
        {
            var parts = key.Split('|');
            if (parts[0] != hero.Name) continue;

            foreach (var traitId in traitIds)
            {
                if (rules.GetAbility(traitId) is not null) sheet.AbilitySources[traitId] = parts[1];
                else                                          sheet.TalentSources[traitId]  = parts[1];
            }
        }

        var talents = PrebuiltHeroes.TalentsByHero[hero.Name];
        Assert.Equal(PrebuiltHeroes.TalentIds.Length, talents.Length);
        for (var i = 0; i < talents.Length; i++)
            sheet.TalentRanks[PrebuiltHeroes.TalentIds[i]] = talents[i];

        foreach (var perk in PrebuiltHeroes.PerksByHero[hero.Name])
        {
            Assert.NotNull(rules.GetPerk(perk.Id));
            sheet.Perks.Add(new SelectedPerk(perk.Id, perk.Units));
        }

        foreach (var p in hero.Powers)
        {
            var power = rules.GetPower(p.Id);
            Assert.NotNull(power);

            var (pros, cons) = ProsCons(hero, p.Id);

            // Determination records how much Resolve was bought, not a rank.
            if (p.Id == "determination")
            {
                sheet.SelectedPowers.Add(new SelectedPower(p.Id, 0)
                {
                    Units    = hero.DeterminationResolve,
                    SourceId = PrebuiltHeroes.SourceOf(hero.Name, p.Id)
                });
                continue;
            }

            if (power.MaxRank == 0)
            {
                sheet.SelectedPowers.Add(new SelectedPower(p.Id, 0, pros, cons)
                {
                    BaselineTraitId = p.BaselineTrait,
                    Units           = p.Units,
                    CostVariantKey  = p.CostVariant,
                    SourceId        = PrebuiltHeroes.SourceOf(hero.Name, p.Id)
                });
                continue;
            }

            var probe    = new SelectedPower(p.Id, 0) { BaselineTraitId = p.BaselineTrait };
            var baseline = derived.GetBaselineRank(power, sheet, probe);
            var purchased = Math.Max(0, p.EffectiveRank - baseline);

            sheet.SelectedPowers.Add(new SelectedPower(p.Id, purchased, pros, cons)
            {
                BaselineTraitId = p.BaselineTrait,
                Units           = p.Units,
                CostVariantKey  = p.CostVariant,
                SourceId        = PrebuiltHeroes.SourceOf(hero.Name, p.Id)
            });
        }

        foreach (var flawId in hero.Flaws)
        {
            Assert.NotNull(rules.GetFlaw(flawId));
            sheet.Flaws.Add(new SelectedFlaw(flawId));
        }

        return sheet;
    }
}
