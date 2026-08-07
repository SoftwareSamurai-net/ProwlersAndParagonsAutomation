namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Two finished characters, for previewing a sheet without building one first.
///
/// <para>They exist because an empty sheet shows nothing worth looking at: no Source
/// headings, no Pros and Cons, no gear line, every derived stat zero. These fill every
/// section a printed sheet has, so a layout or styling change can be judged against
/// something realistic.</para>
///
/// <para><b>They are this project's own characters, not the rulebook's.</b> The published
/// Heroes in Ch.8 are transcribed in the test suite, where they verify the engine against
/// numbers the authors printed; those stay there. Shipping them in the app would be
/// redistributing the authors' content, which is the line this repository draws
/// everywhere else.</para>
///
/// <para>There is no rules logic here — it builds a <see cref="CharacterSheet"/> and
/// nothing more, the same way a player would. What it costs and whether it is legal are
/// <see cref="CostCalculator"/>'s and <see cref="CharacterValidator"/>'s answers, and a
/// test holds both to that.</para>
/// </summary>
public static class SampleCharacters
{
    /// <summary>The tier both samples are built at, and so the budget they fit inside.</summary>
    public const string TierId = "standard";

    /// <summary>
    /// Ninth Precinct — a powered-armour detective. Tech Source throughout, so the sheet
    /// prints a single <c>TECH POWERS</c> group, and the armour carries the Item Con the
    /// rulebook expects of equipment-derived Powers.
    /// </summary>
    public static CharacterSheet Hero()
    {
        var sheet = new CharacterSheet
        {
            SelectedTierId    = TierId,
            SelectedPackageId = "hero_package",
            Name              = "Ninth Precinct",
            Appearance        = "Riot armour rebuilt in a police garage: matte navy plate over a "
                              + "patched underlayer, a badge number stencilled on the shoulder, and a "
                              + "helmet visor that never quite stops flickering.",
            Motivation        = "Someone has to answer the calls the department writes off, and she "
                              + "still believes the paperwork matters.",
            Quote             = "Detective Reyes. I have a warrant and a very bad afternoon."
        };

        sheet.Connections.Add("Her old partner, still on the force and still covering for her");
        sheet.Connections.Add("The night mechanic who keeps the armour running, and asks no questions");

        Set(sheet.AbilityRanks, ("agility", 6), ("intellect", 9), ("might", 8),
                                ("perception", 9), ("toughness", 8), ("willpower", 7));

        Set(sheet.TalentRanks, ("investigation", 7), ("streetwise", 6),
                               ("technology", 6), ("professional", 4));

        // Bought through the armour, which is what the Item Con records. Ch.6: gear-derived
        // Traits carry it, and CostCalculator discounts the ranks a package does not cover.
        sheet.AbilityModifiers["toughness"] = [new SelectedProCon("item")];
        sheet.AbilityModifiers["might"]     = [new SelectedProCon("item")];

        sheet.SelectedPowers.AddRange(
        [
            // Baseline from half Toughness, so the sheet shows a free rank stacking with
            // bought ones rather than a flat number: ⌈8/2⌉ = 4 free, 4 bought, 8 effective.
            new SelectedPower("armor", 4) { SourceId = "tech" },

            // Danger Sense's baseline *equals* Perception rather than half it, so only 2
            // ranks are bought on top of 9 — and it replaces Perception in the Edge sum
            // rather than adding to it, which is the sheet's most instructive number.
            new SelectedPower("danger_sense", 2) { SourceId = "tech" },
            // Two options of one Power. Ch.2 is explicit that Super Senses stays a single
            // Power however many you take, so CostCalculator sums the group and applies
            // the floor once — worth having on a sample, since it is the rule most easily
            // broken by a change to how Powers are priced.
            new SelectedPower("super_senses_thermal_vision", 0) { SourceId = "tech" },
            new SelectedPower("super_senses_radio_hearing", 0) { SourceId = "tech" },

            // Priced at 1 HP per 2 ranks, the only rate below 1, so the sheet shows a
            // half-rate line rounding up. Its baseline equals Toughness, so 3 bought on
            // top of 8 lands at 11 — one under the cap, like Danger Sense above.
            new SelectedPower("resistance", 3) { SourceId = "tech" },

            new SelectedPower("communications", 0) { SourceId = "tech" },
            new SelectedPower("stun", 6) { SourceId = "tech" },
        ]);

        sheet.Perks.Add(new SelectedPerk("contacts", 2, "Precinct dispatch, and a tow-yard clerk"));

        sheet.Flaws.Add(new SelectedFlaw("secret_identity",
            "The department believes the armour was destroyed in the evidence-locker fire."));
        sheet.Flaws.Add(new SelectedFlaw("enemy",
            "The contractor who built the armour wants it back, intact or otherwise."));

        // A customised item, so the gear line prints something other than a bare name.
        sheet.Gear.Add(new SelectedGear("Evidence kit"));
        sheet.Gear.Add(new SelectedGear("Breaching maul")
        {
            Features = [new SelectedGearFeature("powerful", "powerful")]
        });

        return sheet;
    }

    /// <summary>
    /// The Quiet Hour — a Magic-Source antagonist. Villains are built by exactly the same
    /// rules (Ch.9), so this is a legal Standard-tier character; the front end simply
    /// stops showing a budget for one.
    /// </summary>
    public static CharacterSheet Villain()
    {
        var sheet = new CharacterSheet
        {
            SelectedTierId    = TierId,
            SelectedPackageId = "hero_package",
            Name              = "The Quiet Hour",
            Appearance        = "A stopped-clock face under a grey hood, and hands that are always "
                              + "a half-second ahead of where you last saw them.",
            Motivation        = "Every city keeps one hour it would rather forget. He is collecting them.",
            Quote             = "You will not remember disagreeing with me."
        };

        sheet.Connections.Add("A pawnbroker who fences hours the way others fence jewellery");

        Set(sheet.AbilityRanks, ("agility", 8), ("intellect", 9), ("might", 5),
                                ("perception", 9), ("toughness", 7), ("willpower", 11));

        Set(sheet.TalentRanks, ("covert", 8), ("charm", 7), ("academics", 6));

        sheet.SelectedPowers.AddRange(
        [
            // Cheaper for being unreliable, which is what a Con is for — and the sheet
            // prints the Con beside it.
            new SelectedPower("mind_control", 8,
                [], [new SelectedProCon("unreliable")]) { SourceId = "magic" },

            // Rankless and flat-priced, so no ranks are bought: the sheet prints
            // "No rank" against it, which is worth seeing beside a ranked Power.
            new SelectedPower("invisibility", 0) { SourceId = "magic" },

            new SelectedPower("teleportation", 7) { SourceId = "magic" },

            // No Source recorded, so the sheet prints a plain POWERS group after the
            // Magic one — deliberately, to show that a Power is never dropped for want of
            // a Source. The validator warns instead.
            new SelectedPower("lightning_reflexes", 0),
        ]);

        sheet.Flaws.Add(new SelectedFlaw("quirk",
            "Cannot act until a clock somewhere in earshot finishes striking."));
        sheet.Flaws.Add(new SelectedFlaw("wanted",
            "Three cities want him for thefts nobody can quite describe."));

        sheet.Gear.Add(new SelectedGear("Pocket watch, stopped"));

        return sheet;
    }

    private static void Set(Dictionary<string, int> ranks, params (string Id, int Rank)[] values)
    {
        foreach (var (id, rank) in values) ranks[id] = rank;
    }
}
