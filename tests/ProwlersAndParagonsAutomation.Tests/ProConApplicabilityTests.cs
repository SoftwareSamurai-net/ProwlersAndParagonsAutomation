using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Which generic Pros and Cons may be applied to a Power.
///
/// <para>The rulebook states this inside each option's own entry, never as a list on the
/// Power ("This Pro applies to Zone Powers", "applies to Powers that only affect you"). The
/// constraints below are transcribed from Ch.2 pp.48–53 — if one of these fails, check the
/// page, do not edit the expectation to match the code.</para>
///
/// <para>Only the constraints the rulebook prints for every Power are enforced: its Range
/// (Self, Touch, Ranged, Zone, Special — p.19) and its Rank type. The rest travel as a
/// caveat on the option; see <see cref="AnUncheckableConstraintIsCarriedAsACaveat"/>.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class ProConApplicabilityTests
{
    private readonly RulesFixture _f;
    private readonly ProConApplicability _sut;

    public ProConApplicabilityTests(RulesFixture fixture)
    {
        _f   = fixture;
        _sut = new ProConApplicability(fixture.Rules);
    }

    private PowerModel Power(string id) => _f.Rules.GetPower(id)!;

    // ── The constraints, as the rulebook states them ─────────────────────────

    /// <summary>
    /// Every range constraint the generic entries state, with the Ranges each permits.
    /// Ch.2 p.19 defines the five: Self affects you, Touch reaches Close Range, Ranged
    /// reaches Distant Range, Zone is the area option, Special varies.
    /// </summary>
    public static TheoryData<string, string, string[]> RangeConstraints() => new()
    {
        // id,             kind,  permitted base Ranges
        { "close",         "pro", ["touch"] },                    // "+2 Pro when applied to Touch Powers"
        { "close",         "con", ["ranged"] },                   // "-2 Con when applied to Ranged Powers"
        { "ranged",        "pro", ["touch", "zone"] },            // priced from Touch; Zone addressed explicitly
        { "touch",         "con", ["ranged"] },                   // "-4 Con ... Powers used at Distant Range"
        { "line_of_sight", "pro", ["touch", "ranged", "zone"] },  // priced from Touch/Distant; Zone addressed
        { "expansive",     "pro", ["zone"] },                     // "This Pro applies to Zone Powers"
        { "imbue",         "pro", ["self"] },                     // "Powers that only affect you"
        { "area_burst",    "pro", ["ranged"] },                   // "These Pros apply to Ranged Powers"
        { "zone_nova",     "pro", ["ranged", "touch"] },          // "Ranged Powers and Touch Powers"
        { "ricochet",      "pro", ["ranged"] },                   // "applies to ranged attack Powers"
    };

    [Theory]
    [MemberData(nameof(RangeConstraints))]
    public void RangeConstraintsMatchTheRulebook(string id, string kind, string[] ranges)
    {
        IGenericProCon option = kind == "pro" ? _f.Rules.GetPro(id)! : _f.Rules.GetCon(id)!;

        Assert.Equal(ranges.Order(), option.AppliesToRanges.Order());
    }

    /// <summary>Degrades is the only entry that constrains on Rank rather than Range.</summary>
    [Fact]
    public void DegradesAppliesToRankedPowersOnly()
    {
        var degrades = _f.Rules.GetCon("degrades")!;

        Assert.Equal<IEnumerable<string>>(["baseline", "power"], degrades.AppliesToRankTypes.Order());

        Assert.True(ProConApplicability.IsApplicable(degrades, Power("blast")));       // Power Rank
        Assert.True(ProConApplicability.IsApplicable(degrades, Power("strike")));      // Baseline Rank
        Assert.False(ProConApplicability.IsApplicable(degrades, Power("adaptation"))); // Default Rank
    }

    /// <summary>
    /// Exactly these entries constrain on something checkable. Everything else states no
    /// Range or Rank condition and so applies to any Power — pinned so an entry cannot
    /// quietly acquire or lose a constraint.
    /// </summary>
    [Fact]
    public void OnlyTheTranscribedEntriesAreConstrained()
    {
        var constrained = _f.Rules.Pros
            .Where(p => p.AppliesToRanges.Count > 0 || p.AppliesToRankTypes.Count > 0)
            .Select(p => $"pro:{p.Id}")
            .Concat(_f.Rules.Cons.Where(c => c.AppliesToRanges.Count > 0 || c.AppliesToRankTypes.Count > 0)
                .Select(c => $"con:{c.Id}"))
            .Order()
            .ToList();

        Assert.Equal<IEnumerable<string>>(
            ["con:close", "con:degrades", "con:touch",
             "pro:area_burst", "pro:close", "pro:expansive", "pro:imbue",
             "pro:line_of_sight", "pro:ranged", "pro:ricochet", "pro:zone_nova"],
            constrained);
    }

    // ── Applying them ────────────────────────────────────────────────────────

    [Fact]
    public void AnOptionWithNoStatedConstraintAppliesToEveryPower()
    {
        // Burnout's entry names no Range or Rank condition at all.
        var burnout = _f.Rules.GetCon("burnout")!;

        Assert.All(_f.Rules.Powers, p =>
            Assert.True(ProConApplicability.IsApplicable(burnout, p), $"burnout rejected on {p.Id}"));
    }

    /// <summary>
    /// The regression this whole change exists for. The Ranged Pro raises a Touch Power to
    /// Distant Range; a Self Power affects only you, so there is nothing to raise. Six
    /// Powers used to offer it anyway because the old hand-written lists said so.
    /// </summary>
    [Theory]
    [InlineData("animal_empathy")]
    [InlineData("boost")]
    [InlineData("communications")]
    [InlineData("leadership")]
    [InlineData("ventriloquism")]
    [InlineData("weakness_detection")]
    public void TheRangedProIsNotOfferedOnASelfPower(string powerId)
    {
        var power = Power(powerId);

        Assert.Equal("self", power.Range);
        Assert.DoesNotContain(_sut.ProsFor(power), p => p.Id == "ranged");
    }

    [Fact]
    public void TheTouchConIsNotOfferedOnASelfPower() =>
        Assert.DoesNotContain(_sut.ConsFor(Power("teleportation")), c => c.Id == "touch");

    [Fact]
    public void ExpansiveIsOfferedOnZonePowersAndNoOthers()
    {
        foreach (var power in _f.Rules.Powers.Where(p => p.Range is not "special"))
        {
            var offered = _sut.ProsFor(power).Any(p => p.Id == "expansive");
            Assert.Equal(power.Range == "zone", offered);
        }
    }

    [Fact]
    public void ImbueIsOfferedOnSelfPowersAndNoOthers()
    {
        foreach (var power in _f.Rules.Powers.Where(p => p.Range is not "special"))
        {
            var offered = _sut.ProsFor(power).Any(p => p.Id == "imbue");
            Assert.Equal(power.Range == "self", offered);
        }
    }

    /// <summary>
    /// A Range of Special means the Power works in a way its own description defines (p.19),
    /// so nothing can be ruled out for it and every option stays available.
    /// </summary>
    [Fact]
    public void ASpecialRangePowerIsOfferedEveryRangeConstrainedOption()
    {
        var special = _f.Rules.Powers.Where(p => p.Range == "special").ToList();
        Assert.NotEmpty(special);

        foreach (var power in special)
        {
            Assert.Equal(_f.Rules.Pros.Count, _sut.ProsFor(power).Count);

            // Degrades still applies on Rank type, which Special range does not excuse.
            var expectedCons = _f.Rules.Cons.Count(c =>
                c.AppliesToRankTypes.Count == 0 || c.AppliesToRankTypes.Contains(power.RankType));
            Assert.Equal(expectedCons, _sut.ConsFor(power).Count);
        }
    }

    // ── Caveats ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The constraints that are not enforced. "Powers that inflict physical or energy
    /// damage" cannot be decided from anything the rulebook prints per Power, and deciding
    /// it here would mean inventing the data the old lists were made of. Ch.2 calls the
    /// list "not intended to cover every possible option" and leaves it under GM approval,
    /// so the condition is carried to the player instead of being guessed at.
    /// </summary>
    [Theory]
    [InlineData("armor_piercing", "pro")]
    [InlineData("penetrating", "pro")]
    [InlineData("ongoing", "pro")]
    [InlineData("carrier_attack", "pro")]
    [InlineData("resisted", "pro")]
    [InlineData("affect_inanimate", "pro")]
    [InlineData("only_inanimate", "con")]
    [InlineData("constant", "con")]
    [InlineData("uncontrolled", "con")]
    [InlineData("sustained", "con")]
    public void AnUncheckableConstraintIsCarriedAsACaveat(string id, string kind)
    {
        IGenericProCon option = kind == "pro" ? _f.Rules.GetPro(id)! : _f.Rules.GetCon(id)!;

        Assert.False(string.IsNullOrWhiteSpace(option.ApplicabilityCaveat));
        Assert.EndsWith(".", option.ApplicabilityCaveat!.TrimEnd(), StringComparison.Ordinal);

        // A caveat is a note to the player, never a silent filter.
        Assert.All(_f.Rules.Powers, p =>
            Assert.True(ProConApplicability.IsApplicable(option, p) ||
                        option.AppliesToRanges.Count > 0 || option.AppliesToRankTypes.Count > 0));
    }

    /// <summary>
    /// <b>What the caveat actually says, which is the whole of what it delivers.</b> The test
    /// above asks whether there is a sentence there; it does not ask whether the sentence is
    /// true. Penetrating's was replaced with "Applies to absolutely any Power at all, no
    /// conditions." — the opposite of the printed constraint — and the suite stayed green.
    ///
    /// <para>That matters more here than for anything else in the data, because the design is
    /// that a caveat is <em>shown to the player instead of being enforced</em>. Everywhere else a
    /// wrong sentence is a wrong sentence beside a mechanism that still works; here the sentence
    /// <em>is</em> the mechanism, so nothing downstream can be wrong in a way anybody notices.
    /// </para>
    ///
    /// <para>See <see cref="CanonicalCaveats"/> for why this is two assertions rather than one:
    /// the caveat is held to a record, and the record is held to the printed page.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Caveats))]
    public void ACaveatSaysWhatTheRulebookPrints(string id)
    {
        var entry = CanonicalCaveats.All.First(e => e.Id == id);

        IGenericProCon option = entry.IsPro ? _f.Rules.GetPro(id)! : _f.Rules.GetCon(id)!;

        Assert.True(entry.Caveat == option.ApplicabilityCaveat,
            $"'{id}' carries a different caveat from the one recorded against its printed "
            + $"constraint (\"{entry.PrintedConstraint}\"). Read Ch.2 pp.48-53 and agree the "
            + $"wording with the page before changing either. It now reads: "
            + $"\"{option.ApplicabilityCaveat}\"");

        // And the record is held to the book rather than to itself, so a caveat and a
        // transcription cannot be edited into agreement about something never printed.
        Assert.Contains(entry.PrintedConstraint, PowersChapterText, StringComparison.Ordinal);
    }

    public static TheoryData<string> Caveats() => [.. CanonicalCaveats.All.Select(e => e.Id)];

    /// <summary>
    /// The caveats are exactly the options that carry one — so an entry cannot acquire a caveat
    /// nobody checked, or lose the one recorded here and leave a line guarding nothing.
    /// </summary>
    [Fact]
    public void EveryCaveatInTheDataIsOneThatWasCheckedAgainstThePage()
    {
        var carried = _f.Rules.Pros.Select(p => $"pro:{p.Id}")
            .Concat(_f.Rules.Cons.Select(c => $"con:{c.Id}"))
            .Zip(_f.Rules.Pros.Select(p => p.ApplicabilityCaveat)
                    .Concat(_f.Rules.Cons.Select(c => c.ApplicabilityCaveat)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Second))
            .Select(x => x.First)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal<IEnumerable<string>>(
            [.. CanonicalCaveats.All
                .Select(e => $"{(e.IsPro ? "pro" : "con")}:{e.Id}")
                .Order(StringComparer.Ordinal)],
            carried);
    }

    // Cached rather than constructed per call: CA1869, which is an error under
    // ContinuousIntegrationBuild and so does not show up in a local `dotnet test`.
    private static readonly System.Text.Json.JsonSerializerOptions SnakeCase =
        new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower };

    /// <summary>
    /// Chapter 2's printed text, which is where every Pro and Con in the game is defined
    /// (pp.48-53). Read through the deserializer rather than off the raw file, so the JSON
    /// escapes the extractor writes — <c>+</c> among them — are the characters the book prints.
    /// </summary>
    /// <remarks>
    /// <b>Declared after <see cref="SnakeCase"/>, and it has to be.</b> Static field initialisers
    /// run in declaration order, so with these two the other way round the options were still
    /// null when the chapter was read — which does not throw, it silently deserialises without
    /// the naming policy and hands back a chapter with no sections. Every caveat then failed on
    /// a sentence the book does contain.
    /// </remarks>
    private static string PowersChapterText { get; } = ReadPowersChapter();

    private static string ReadPowersChapter()
    {
        var path = Path.Combine(RulesFixture.RepoRoot, "data", "rulebook", "ch02-characters.json");

        var chapter = System.Text.Json.JsonSerializer.Deserialize<Chapter>(File.ReadAllText(path), SnakeCase)
            ?? throw new InvalidOperationException($"{path} is not readable as a chapter.");

        // An empty read is the failure mode that matters here: it makes every printed clause
        // look absent from a book that contains all of them, which reads as fifteen wrong
        // transcriptions rather than as one unread file.
        if (chapter.Sections is not { Length: > 0 })
            throw new InvalidOperationException($"{path} read back with no sections at all.");

        return string.Join("\n", chapter.Sections.Select(s => s.Text));
    }

    private sealed record Chapter(Section[] Sections);

    private sealed record Section(string Heading, string Text);

    /// <summary>
    /// Force Field is Self range and its own entry names three generic Pros anyway:
    /// "Apply the Zone Pro to shield large areas, the Ranged Pro to shield things at a
    /// distance, or the Area Pro to shield large areas at a distance" (Ch.2 p.29). T-Kay
    /// (Ch.8 p.143) is printed with Force Field 12d (Zone), so until this held, a Hero in
    /// the rulebook was refused by both editors and by the validator.
    /// </summary>
    [Theory]
    [InlineData("zone_nova")]
    [InlineData("area_burst")]
    [InlineData("ranged")]
    public void ForceFieldOffersTheProsItsOwnEntryNames(string proId)
    {
        var forceField = Power("force_field");

        Assert.Equal("self", forceField.Range);
        Assert.Contains(_sut.ProsFor(forceField), p => p.Id == proId);
    }

    /// <summary>
    /// <b>The mechanism, driven rather than observed.</b> Every other test here reads the
    /// shipped data, so replacing the field with <c>power.Id == "force_field" &amp;&amp; …</c>
    /// hard-coded left all of them green — the JSON field was behaviourally dead as far as the
    /// suite could tell, and the second entry somebody adds later would have done nothing
    /// while the tests said it was fine. These two Powers differ only in the field.
    /// </summary>
    [Fact]
    public void TheExemptionIsReadFromTheDataAndNotFromAPowerId()
    {
        var zone = _f.Rules.Pros.Single(p => p.Id == "zone_nova");

        var plain = new PowerModel { Id = "made_up", Range = "self", RankType = "power" };
        var named = plain with
        {
            ProsAllowedByOwnText = [new ProAllowanceModel { Id = "zone_nova", Reason = "test" }]
        };

        Assert.False(ProConApplicability.IsApplicable(zone, plain));
        Assert.True(ProConApplicability.IsApplicable(zone, named));
    }

    /// <summary>
    /// It widens the Range rule and nothing else. The first version returned early above both
    /// checks, so it exempted the rank-type rule too — and because the method takes the
    /// interface, an id in a field named for Pros would have exempted a Con sharing it.
    /// Neither was reachable with the data as it stands, which is why neither was noticed.
    /// </summary>
    [Fact]
    public void TheExemptionWidensRangeAloneAndReachesNoCon()
    {
        // <b>A synthetic Pro, because the rulebook has no Pro with a rank-type constraint.</b>
        // The first version of this test used Degrades, which is a Con — so it was refused by
        // the Pros-only guard before ordering could matter, and hoisting the exemption back
        // above both checks left all 3397 tests green. Two assertions that were really one.
        var rankTypePro = new ProModel
        {
            Id = "made_up_pro", Name = "Made Up",
            AppliesToRanges     = ["ranged"],
            AppliesToRankTypes  = ["power"]
        };

        var ranklessButNamed = new PowerModel
        {
            Id = "made_up", Range = "self", RankType = "default",
            ProsAllowedByOwnText = [new ProAllowanceModel { Id = "made_up_pro", Reason = "test" }]
        };

        // The Range it names is widened; the rank type it does not name is still refused.
        Assert.False(ProConApplicability.IsApplicable(rankTypePro, ranklessButNamed));
        Assert.True(ProConApplicability.IsApplicable(
            rankTypePro, ranklessButNamed with { RankType = "power" }));

        var degrades = _f.Rules.Cons.Single(c => c.Id == "degrades");
        Assert.NotEmpty(degrades.AppliesToRankTypes);

        // A Con sharing an exempted Pro's id is not carried along by it.
        var conWithAProsId = new ConModel
        {
            Id = "zone_nova", Name = "Not the Pro", AppliesToRanges = ["ranged", "touch"]
        };

        var forceField = Power("force_field");
        Assert.Equal("self", forceField.Range);
        Assert.False(ProConApplicability.IsApplicable(conWithAProsId, forceField));
    }

    /// <summary>
    /// Every id claimed by a Power's own text has to be a Pro the rulebook gives, and every
    /// grade named has to be one that option prices. Neither was checked anywhere: a typo in
    /// either was silently inert, which is the failure shape this repository guards hard
    /// elsewhere. Each entry also has to carry the printed sentence behind it — the standard
    /// for adding one — because a standard nothing checks is documentation.
    /// </summary>
    [Fact]
    public void EveryOwnTextAllowanceResolvesAndCitesItsPrintedText()
    {
        var claimed = _f.Rules.Powers.SelectMany(p => p.ProsAllowedByOwnText.Select(a => (p, a))).ToList();

        Assert.NotEmpty(claimed);

        foreach (var (power, allowance) in claimed)
        {
            var pro = _f.Rules.Pros.SingleOrDefault(x => x.Id == allowance.Id);
            Assert.True(pro is not null,
                $"{power.Id} allows '{allowance.Id}', which is not a Pro the rulebook gives.");

            Assert.False(string.IsNullOrWhiteSpace(allowance.Reason),
                $"{power.Id}'s allowance of '{allowance.Id}' cites no printed text.");

            foreach (var grade in allowance.Grades)
                Assert.True(pro!.CostModifierRange?.ContainsKey(grade) == true,
                    $"{power.Id} names grade '{grade}' for '{allowance.Id}', which does not price it.");
        }
    }

    /// <summary>
    /// The grades of Zone/Nova and Ranged encode a Range, and Force Field is Self — which the
    /// rulebook does not price. Both grades were accepted, 2 Hero Points apart, so T-Kay's
    /// printed character costed two ways depending on which key was typed. The decision is
    /// now in the data and enforced; it was a comment the engine never read.
    /// </summary>
    [Theory]
    [InlineData("zone_nova", "zone_ranged", "zone_touch")]
    [InlineData("ranged", "from_close_range", "from_touch")]
    public void ASelfPowerTakesTheRangedGradesAndNotTheTouchOnes(
        string proId, string offered, string refused)
    {
        var pro   = _f.Rules.Pros.Single(p => p.Id == proId);
        var power = Power("force_field");

        var grades = ProConApplicability.GradesFor(pro, power, pro.CostModifierRange!.Keys);

        Assert.Contains(offered, grades);
        Assert.DoesNotContain(refused, grades);

        // Unnarrowed elsewhere: a Power that reaches the option by its own Range still gets
        // every grade, so this is a narrowing for the exempted Power alone.
        var touchPower = _f.Rules.Powers.First(p => p.Range == "touch");
        Assert.Equal(pro.CostModifierRange.Count,
                     ProConApplicability.GradesFor(pro, touchPower, pro.CostModifierRange.Keys).Count);
    }

    /// <summary>
    /// The exemption is a record of one Power's printed sentence, not a hole in the Range
    /// rule. Every other Self-range Power is still refused all three, and nothing else in
    /// the data claims the exemption.
    /// </summary>
    [Fact]
    public void TheOwnTextExemptionReachesNoOtherPower()
    {
        var claimed = _f.Rules.Powers.Where(p => p.ProsAllowedByOwnText.Count > 0)
                                     .Select(p => p.Id)
                                     .ToList();

        Assert.Equal(["force_field"], claimed);

        // Named separately from the loop below so the failure says which Pro escaped.
        foreach (var proId in new[] { "zone_nova", "area_burst", "ranged" })
        {
            var pro = _f.Rules.Pros.Single(p => p.Id == proId);

            Assert.All(_f.Rules.Powers.Where(p => p.Range == "self" && p.Id != "force_field"),
                p => Assert.False(ProConApplicability.IsApplicable(pro, p),
                    $"{proId} escaped onto {p.Id}, which is Self range and says nothing about it."));
        }
    }

    /// <summary>
    /// Repeatability is the rulebook's word in three places and nowhere else. Also X is
    /// "each time you select this Pro" on Energy Absorption (Ch.2 p.28) and "one additional
    /// type of energy for every 2 extra Hero Points" on Energy Form (p.30); Affect Inanimate
    /// is "You can apply this Pro multiple times" (p.48).
    ///
    /// <para>The other five Also X entries are deliberately absent: they are priced per
    /// unit, so the extra Sources are a quantity on one selection and a second copy really
    /// would charge the same thing twice.</para>
    /// </summary>
    [Fact]
    public void OnlyTheOptionsTheRulebookRepeatsAreRepeatable()
    {
        Assert.Equal(["affect_inanimate"],
            _f.Rules.Pros.Where(p => p.Repeatable).Select(p => p.Id).Order());

        Assert.DoesNotContain(_f.Rules.Cons, c => c.Repeatable);

        var powerSpecific = _f.Rules.Powers
            .SelectMany(p => p.PowerPros.Concat(p.PowerCons)
                              .Where(x => x.Repeatable)
                              .Select(x => $"{p.Id}/{x.Id}"))
            .Order()
            .ToList();

        Assert.Equal(["energy_absorption/also_x", "form_energy/also_x"], powerSpecific);
    }

    /// <summary>
    /// <b>The pickers offer options in rules-file order, and the two editors show them in the
    /// order they are offered.</b> Both methods say so in their own summaries and nothing held
    /// them to it: adding <c>.Reverse()</c> to <c>ProsFor</c> left the whole suite green.
    ///
    /// <para>It is not cosmetic. Ch.2 prints Pros and Cons alphabetically and a player choosing
    /// from a list of fifty is looking one up by name, so a list in some other order is a list
    /// they have to read all of. Asserted as a subsequence of the rules file rather than as a
    /// literal list, because which options a given Power is offered is
    /// <see cref="IsApplicable"/>'s answer and not this test's business.</para>
    /// </summary>
    [Fact]
    public void ThePickersOfferOptionsInRulesFileOrder()
    {
        var pros = _f.Rules.Pros.Select(p => p.Id).ToList();
        var cons = _f.Rules.Cons.Select(c => c.Id).ToList();

        foreach (var power in _f.Rules.Powers)
        {
            var offeredPros = _sut.ProsFor(power).Select(p => p.Id).ToList();
            var offeredCons = _sut.ConsFor(power).Select(c => c.Id).ToList();

            Assert.Equal(pros.Where(offeredPros.Contains), offeredPros);
            Assert.Equal(cons.Where(offeredCons.Contains), offeredCons);
        }

        // Narrow: a file order that happened to be the same as some other order would make the
        // check above vacuous, so at least one Power is offered enough options to tell them apart.
        Assert.True(_sut.ProsFor(Power("blast")).Count > 2);
    }

    /// <summary>
    /// <b>The intersection in <c>GradesFor</c>, driven rather than observed.</b> Its comment says
    /// it stops a rules file "inventing a key", and with the shipped data it cannot: every grade
    /// any allowance records is one the option prices — <see
    /// cref="EveryOwnTextAllowanceResolvesAndCitesItsPrintedText"/> guarantees exactly that — so
    /// replacing the whole thing with <c>return allowance.Grades.ToList();</c> is behaviourally
    /// identical and every test stayed green.
    ///
    /// <para>Which is the point rather than an objection: a guard whose only evidence comes from
    /// data that cannot exercise it is a guard nobody knows the state of. These two Powers differ
    /// only in the grades their allowance names, and one of them names a key the Pro does not
    /// price. The narrower test above — Force Field taking the Ranged grades and not the Touch
    /// ones — covers the case the rulebook has; this covers the case the comment claims.</para>
    /// </summary>
    [Fact]
    public void AGradeARulesFileNamesButTheOptionDoesNotPriceIsNotOffered()
    {
        var zone = _f.Rules.Pros.Single(p => p.Id == "zone_nova");
        var printed = zone.CostModifierRange!.Keys.ToList();

        Assert.DoesNotContain("zone_underwater", printed);

        var honest = new PowerModel
        {
            Id = "made_up", Range = "self", RankType = "power",
            ProsAllowedByOwnText =
                [new ProAllowanceModel { Id = "zone_nova", Reason = "test", Grades = ["zone_ranged"] }]
        };

        var inventing = honest with
        {
            ProsAllowedByOwnText =
            [
                new ProAllowanceModel
                {
                    Id = "zone_nova", Reason = "test",
                    Grades = ["zone_ranged", "zone_underwater"]
                }
            ]
        };

        Assert.Equal<IEnumerable<string>>(
            ["zone_ranged"], ProConApplicability.GradesFor(zone, honest, printed));

        // The invented key is dropped rather than offered, so a caller cannot be told to write
        // back a grade nothing can price.
        Assert.Equal<IEnumerable<string>>(
            ["zone_ranged"], ProConApplicability.GradesFor(zone, inventing, printed));
    }

    [Fact]
    public void NoPowerCarriesAnInventedProOrConList()
    {
        // The old available_pros / available_cons fields are gone from the data. Nothing
        // deserialises them any more, so this guards the JSON rather than the model.
        var json = File.ReadAllText(Path.Combine(RulesFixture.DataPath, "powers.json"));

        Assert.DoesNotContain("available_pros", json, StringComparison.Ordinal);
        Assert.DoesNotContain("available_cons", json, StringComparison.Ordinal);
    }
}
