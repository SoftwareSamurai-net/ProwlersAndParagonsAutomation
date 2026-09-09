using ProwlersAndParagonsAutomation.Cli.Steps;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The terminal wizard's Gear step, as far as a test can reach it.</b>
///
/// <para><b>What this can and cannot see, said plainly.</b> The wizard is the oldest front end and
/// the only one with no harness — every step drives <c>AnsiConsole</c> prompts directly, so the
/// menu, the two selection prompts and what a keypress does are not reachable from here, exactly as
/// they are not for the other five steps. What <em>is</em> reachable is the line each catalogue row
/// is offered under, which is where this step makes a decision rather than plumbing one: a row's
/// printed columns are on the page, and the Armor rank a suit would grant this wearer is not.</para>
///
/// <para>So this is a check on the one thing worth checking here, and it is not a claim that the
/// step is covered. <see cref="GearCataloguePickerTests"/> in the browser suite drives the
/// equivalent surface end to end.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class ChooseGearStepTests
{
    private readonly RulesFixture _f;

    public ChooseGearStepTests(RulesFixture fixture) => _f = fixture;

    private string Label(string rowId, CharacterSheet sheet) =>
        ChooseGearStep.CatalogueLabel(_f.Rules.Catalogue.Find(rowId)!, sheet, _f.Derived, _f.Rules);

    /// <summary>
    /// <b>An armour row is offered with the rank it would grant <em>this</em> character</b>, which
    /// is the figure neither page prints: p.88 gives Toughness plus the suit's bonus and p.87 caps
    /// the Toughness half at the Gear Limit. Which suit is worth taking depends on the wearer, so
    /// a line that showed only the printed bonus would leave the choice unmade.
    /// </summary>
    [Fact]
    public void AnArmourRowIsOfferedWithTheRankItWouldGrantThisWearer()
    {
        var strong = _f.LegalSheet();
        strong.AbilityRanks["toughness"] = 10;

        var line = Label(GearCatalogue.ArmorPrefix + "ancient_plate", strong);

        Assert.Equal("Plate — +2 · Rigid · grants Armor 8d", line);

        // A weaker wearer gets a different figure off the same row, which is what says the rank is
        // computed for the character rather than printed off the table.
        var ordinary = _f.LegalSheet();
        ordinary.AbilityRanks["toughness"] = 3;

        Assert.Equal("Plate — +2 · Rigid · grants Armor 5d", Label(GearCatalogue.ArmorPrefix + "ancient_plate", ordinary));
    }

    /// <summary>
    /// <b>A weapon row is offered with its printed columns and no rank</b>, because a weapon grants
    /// no Power and inventing a figure for it would be a rule this project made up.
    /// </summary>
    [Fact]
    public void AWeaponRowIsOfferedWithItsPrintedColumnsAlone()
    {
        var sheet = _f.LegalSheet();

        Assert.Equal("Battle Axe — +3 · Two-Handed", Label(GearCatalogue.WeaponPrefix + "battle_axe", sheet));

        // The subdual mark is part of the bonus column, and the Baton carries it.
        Assert.Equal("Baton — +1 (s) · Thrown", Label(GearCatalogue.WeaponPrefix + "baton", sheet));

        Assert.DoesNotContain("Armor", Label(GearCatalogue.WeaponPrefix + "battle_axe", sheet), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A shield says what its die is for.</b> The weapons table prints only the half you get by
    /// swinging it; p.88's rule is the half it is mostly carried for.
    /// </summary>
    [Fact]
    public void AShieldRowSaysWhatTheDefensiveDieIsFor()
    {
        var line = Label(GearCatalogue.WeaponPrefix + "shield", _f.LegalSheet());

        Assert.Equal("Shield — +1 (s) · Shield · +1d to every defence in the off hand", line);
    }

    /// <summary>
    /// <b>An item with nothing printed beside it is offered as its bare name.</b> Most of p.91's
    /// thirty-six are props, and a trailing dash before nothing is furniture.
    /// </summary>
    [Fact]
    public void AnItemWithNoPrintedFigureIsOfferedAsItsNameAlone()
    {
        var sheet = _f.LegalSheet();

        Assert.Equal("Polyhedral Dice", Label(GearCatalogue.ItemPrefix + "polyhedral_dice", sheet));

        // And one that does print a figure carries it — with what the page says the figure is
        // for, because a bare "+4" beside a Battle Axe's "+3" offers the crowbar as the better
        // weapon, which is not a thing p.91 grants.
        Assert.Equal(
            "Crowbar — +4 to Might rolls made to force things open or apart",
            Label(GearCatalogue.ItemPrefix + "crowbar", sheet));
    }
}
