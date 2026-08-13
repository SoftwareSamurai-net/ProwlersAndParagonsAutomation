using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Mcp;

/// <summary>
/// What the engine says about a character, written down.
///
/// <para><b>Nothing here computes a Hero Point.</b> Every figure in the report is the return
/// value of a <see cref="CostCalculator"/>, <see cref="DerivedStatsCalculator"/> or
/// <see cref="CharacterValidator"/> call, and the word "legal" appears only where
/// <see cref="ValidationResult.IsValid"/> put it. Inverted — a model proposing a character
/// and this agreeing with it — the whole tool is a random number generator with good
/// prose.</para>
///
/// <para><b>Every engine call is guarded, and a figure the engine cannot supply comes back
/// null rather than 0.</b> A character submitted by a caller can be one the engine cannot
/// answer for at all: a variable-cost Power with no variant chosen has no cost, and asking
/// for one throws rather than guessing. Reporting 0 for a character that cannot be priced is
/// a lie a caller would act on. The validator reports the same thing as an issue, which is
/// what the caller repairs from.</para>
///
/// <para>This is deliberately not shared with <c>cli/Headless/BuildCommand</c>, which does
/// the same guarding around the same three calls. What is shared is the part that matters —
/// the engine — and the two reports are different documents: one names the files it wrote,
/// this one carries a spending breakdown a conversation needs and no paths at all, because
/// this program writes nothing.</para>
/// </summary>
public sealed class Judgement
{
    private readonly RulesRepository _rules;
    private readonly CostCalculator _costs;
    private readonly DerivedStatsCalculator _derived;
    private readonly CharacterValidator _validator;

    public Judgement(
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived,
        CharacterValidator validator)
    {
        _rules     = rules;
        _costs     = costs;
        _derived   = derived;
        _validator = validator;
    }

    /// <summary>
    /// Costs and validates the character, or says why it could not be checked at all.
    /// </summary>
    public JsonObject Judge(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        // The validator is the one call whose failure leaves nothing to report — without it
        // there are no findings, so there is no report to put them in. It is guarded
        // separately for that reason, and it does not guess whose fault it was: a duplicate id
        // in one of this program's own rules files throws the same kind of exception as a bad
        // character, and a caller told to fix the character would edit its own file for ever.
        ValidationResult validation;
        try
        {
            validation = _validator.Validate(sheet);
        }
        catch (Exception e) when (IsUnanswerable(e))
        {
            return Problem(
                "CHARACTER_UNUSABLE",
                "This character could not be checked against the rules at all. It may be the "
                + "character, and it may be a fault in this program or in its copy of the "
                + $"rules. The engine said: {e.Message}");
        }

        var tier  = sheet.SelectedTierId is null ? null : _rules.GetTier(sheet.SelectedTierId);
        var spent = Answer(() => _costs.TotalCost(sheet));

        var report = new JsonObject
        {
            ["ok"]      = validation.IsValid,
            ["verdict"] = validation.IsValid ? "legal" : "breaks_a_rule",
            ["character"] = new JsonObject
            {
                ["name"]    = string.IsNullOrWhiteSpace(sheet.Name) ? null : sheet.Name,
                ["tier"]    = sheet.SelectedTierId,
                ["package"] = sheet.SelectedPackageId
            },
            ["hero_points"] = new JsonObject
            {
                ["spent"]     = spent,
                ["budget"]    = tier?.HeroPoints,
                ["remaining"] = spent is null || tier is null ? null : tier.HeroPoints - spent
            },
            ["trait_cap"] = tier?.TraitCapRank,
            ["derived"]   = new JsonObject
            {
                ["edge"]    = Answer(() => _derived.CalculateEdge(sheet)),
                ["health"]  = Answer(() => _derived.CalculateHealth(sheet)),
                ["resolve"] = Answer(() => _derived.CalculateResolve(sheet))
            },
            ["spending"] = Spending(sheet),
            ["issues"]   = Issues(validation)
        };

        // A figure the engine could not supply, with nothing to fix beside it, is a fault
        // here rather than in the character — and a caller told to "repair the errors and the
        // figures appear" would loop for ever on a character that breaks no rule.
        if (spent is null && validation.IsValid)
        {
            report["ok"]      = false;
            report["verdict"] = "engine_could_not_answer";
            report["issues"]!.AsArray().Add(new JsonObject
            {
                ["severity"] = "error",
                ["code"]     = "ENGINE_COULD_NOT_ANSWER",
                ["message"]  = "This character broke no rule, and its Hero Point total still "
                             + "could not be worked out. That is a fault in this program rather "
                             + "than in the character."
            });
        }

        return report;
    }

    /// <summary>
    /// Where the Hero Points went, so a conversation about an unaffordable character can name
    /// the expensive part instead of guessing at it.
    ///
    /// <para>The per-Power figures are each Power priced <b>on its own</b>, and they do not
    /// have to add up to <c>powers</c>: Super Senses is one Power in the rulebook whose
    /// sixteen options are stored separately, so the group is costed once, with one floor.
    /// The total is the engine's and the parts are indicative, which is the honest way round
    /// — the alternative is a total this program added up itself.</para>
    /// </summary>
    private JsonObject Spending(CharacterSheet sheet)
    {
        var byPower = new JsonArray();

        foreach (var selection in sheet.SelectedPowers)
        {
            byPower.Add(new JsonObject
            {
                ["power_id"]       = selection.PowerId,
                ["name"]           = selection.PowerId is null
                                        ? null
                                        : _rules.GetPower(selection.PowerId)?.Name,
                ["effective_rank"] = Answer(() => _derived.GetEffectiveRank(selection, sheet)),
                ["hero_points"]    = Answer(() => _costs.PowerCost(selection))
            });
        }

        var byPerk = new JsonArray();

        foreach (var perk in sheet.Perks)
        {
            byPerk.Add(new JsonObject
            {
                ["perk_id"]     = perk.PerkId,
                ["name"]        = perk.PerkId is null ? null : _rules.GetPerk(perk.PerkId)?.Name,
                ["units"]       = perk.Units,
                ["hero_points"] = Answer(() => _costs.PerkCost(perk))
            });
        }

        return new JsonObject
        {
            ["abilities"] = Answer(() => _costs.AbilityCost(sheet)),
            ["talents"]   = Answer(() => _costs.TalentCost(sheet)),
            ["package"]   = Answer(() => _costs.PackageCost(sheet)),
            ["powers"]    = Answer(() => _costs.TotalPowersCost(sheet)),
            ["perks"]     = Answer(() => _costs.TotalPerksCost(sheet)),
            ["gear"]      = Answer(() => _costs.TotalGearCost(sheet)),
            ["by_power"]  = byPower,
            ["by_perk"]   = byPerk,
            ["note"]      = "Each Power is priced on its own here. Super Senses is one Power "
                          + "whose options are stored separately, so its options are costed as "
                          + "a group and the parts need not add up to the total."
        };
    }

    private static JsonArray Issues(ValidationResult validation)
    {
        var array = new JsonArray();

        foreach (var issue in validation.Issues)
        {
            var node = new JsonObject
            {
                ["severity"] = issue.Severity == ValidationSeverity.Error ? "error" : "warning",
                ["code"]     = issue.Code,
                ["message"]  = issue.Message
            };

            // Absent rather than null: an issue with nothing to locate is not an issue with an
            // empty subject, and a reader should not have to tell those apart.
            if (issue.SubjectKind != ValidationSubject.None)
                node["subject_kind"] = SubjectKindName(issue.SubjectKind);
            if (issue.SubjectId is not null) node["subject_id"] = issue.SubjectId;
            if (issue.OwnerId is not null) node["owner_id"] = issue.OwnerId;
            if (issue.Value is not null) node["value"] = issue.Value;
            if (issue.Limit is not null) node["limit"] = issue.Limit;
            if (issue.Options.Count > 0)
                node["options"] = new JsonArray([.. issue.Options.Select(o => JsonValue.Create(o))]);

            array.Add(node);
        }

        return array;
    }

    /// <summary>
    /// A subject kind as it goes over the wire: <c>GearFeature</c> to <c>gear_feature</c>, so
    /// a caller reads the same names here as from the <c>build</c> command and the skill.
    /// </summary>
    public static string SubjectKindName(ValidationSubject kind) =>
        string.Concat(kind.ToString().Select((c, i) =>
            char.IsUpper(c) && i > 0
                ? "_" + char.ToLowerInvariant(c)
                : char.ToLowerInvariant(c).ToString()));

    /// <summary>
    /// A refusal, in the shape every tool here answers with. <c>ok: false</c> and a problem
    /// with a code and a sentence — never an exception across the transport, where the
    /// message arrives as a protocol error a model has no way to act on.
    /// </summary>
    public static JsonObject Problem(string code, string message) => new()
    {
        ["ok"]      = false,
        ["problem"] = new JsonObject
        {
            ["code"]    = code,
            ["message"] = message
        }
    };

    /// <summary>
    /// A figure the engine can answer, or null where it cannot. The exceptions listed are what
    /// a half-finished or hand-written character produces — <see cref="InvalidOperationException"/>
    /// for a selection with no cost yet, and the rest for ids and numbers a caller invented.
    /// All are already reported as issues by the validator, so swallowing one here loses
    /// nothing and keeps a single bad figure from costing the caller the whole report.
    /// </summary>
    private static int? Answer(Func<int> figure)
    {
        try { return figure(); }
        catch (Exception e) when (IsUnanswerable(e)) { return null; }
    }

    public static bool IsUnanswerable(Exception e) =>
        e is InvalidOperationException or KeyNotFoundException or ArgumentException
          or NullReferenceException or FormatException or OverflowException;
}
