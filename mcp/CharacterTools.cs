using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Mcp;

/// <summary>
/// The six tools, and the reasoning behind there being six.
///
/// <para><b>They were chosen by asking what a conversation needs, not by mirroring the
/// engine.</b> Someone describes a character out loud; the assistant has to find out which
/// Powers could realise what they described, know what the choices are, ask the two or three
/// questions that change the build, hand a whole character to the engine, and show them the
/// sheet. That is what is here, and nothing else is.</para>
///
/// <para><b>Costing and validating are one tool, deliberately.</b> The obvious surface is
/// <c>cost_character</c> beside <c>validate_character</c>, which is the engine's API rather
/// than the conversation's: no turn of a conversation wants a price without knowing whether
/// the thing priced is allowed, and a separate costing tool is an invitation to quote a
/// number for a character that breaks a rule. <see cref="CheckCharacter"/> answers both, and
/// it is the only place in this program the word "legal" is decided.</para>
///
/// <para><b>The small lists are one tool too.</b> Tiers, packages, abilities, talents,
/// sources, perks, flaws, pros, cons and gear features are ten catalogues of a dozen entries
/// each; ten tools for them would crowd out the six that matter in every client's tool list.
/// Powers are the exception — 141 entries, searched rather than listed, which is a different
/// job and gets its own two tools.</para>
///
/// <para><b>Nothing here computes a Hero Point</b> and nothing decides legality. Every figure
/// comes back from <see cref="Judgement"/>, which asks the engine. See QUESTION-POLICY.md,
/// which is the payload of <see cref="CreationGuide"/> and the place the question policy is
/// written down.</para>
/// </summary>
public sealed class CharacterTools
{
    private readonly RulesRepository _rules;
    private readonly CostCalculator _costs;
    private readonly DerivedStatsCalculator _derived;
    private readonly CharacterValidator _validator;
    private readonly ProConApplicability _applicability;
    private readonly Judgement _judgement;
    private readonly Func<DateTime> _now;
    private readonly Func<string> _guide;

    /// <summary>
    /// The engine, a clock, and the guide. <c>now</c> is the time a printed sheet is stamped
    /// with, and it is injectable so a test can assert on a whole sheet without matching a clock.
    ///
    /// <para><c>guide</c> is injectable for the same kind of reason and a sharper one: the
    /// question policy is an <em>embedded resource</em>, so the way it goes missing is a csproj
    /// edit, and there is no way to un-embed it from an assembly that is already loaded. Reading
    /// it in <see cref="ReadEverything"/> is what makes that a refusal at startup rather than a
    /// conversation opening with an empty document — and with the read hard-wired, the only test
    /// available for that was a grep of this method's own source, which a <c>nameof</c> or a
    /// comment satisfies without reading anything. Handed in, the claim can be driven.</para>
    /// </summary>
    public CharacterTools(
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived,
        CharacterValidator validator,
        Func<DateTime>? now = null,
        Func<string>? guide = null)
    {
        _rules         = rules;
        _costs         = costs;
        _derived       = derived;
        _validator     = validator;
        _applicability = new ProConApplicability(rules);
        _judgement     = new Judgement(rules, costs, derived, validator);
        _now           = now ?? (() => DateTime.Now);
        _guide         = guide ?? (() => QuestionPolicy.Text);
    }

    /// <summary>
    /// Reads everything the tools will need, so that a bad or partial rules directory is a
    /// refusal at startup rather than an error on every question.
    ///
    /// <para><b>It has to touch every catalogue, not one.</b> A repository loads each file
    /// lazily on first use, so warming <c>tiers</c> alone let a directory holding nothing but
    /// <c>tiers.json</c> start cleanly and then throw out of five of the six tools — the exact
    /// failure this is here to prevent, passing its own check. The guide is read here too: it
    /// is an embedded resource, and the way it goes missing is a csproj edit that no test in
    /// the world would connect to a conversation starting with an empty document.</para>
    /// </summary>
    /// <exception cref="Exception">Whatever reading the rules threw. The caller reports it and
    /// exits; there is nothing this class can do about it.</exception>
    public void ReadEverything()
    {
        foreach (var category in Categories) _ = ListOptions(category);

        _ = _rules.Powers.Count;
        _ = _rules.CreationRules.TraitRankLimits.Minimum;

        // Chapter 6's equipment, which no catalogue above reaches: a character submitted to this
        // server can name an armour or weapon row, and the validator resolves it. A file the tools
        // reach only through a submitted character is exactly the one a partial rules directory
        // would take down mid-conversation rather than at startup.
        _ = _rules.Catalogue.Rows.Count;

        _ = _guide().Length;
    }

    // ── The guide ─────────────────────────────────────────────────────────

    /// <summary>
    /// The question policy, verbatim from the file. There is one copy of it: a paraphrase in
    /// a string literal here would drift from the document the next person reads, and the
    /// drift would be invisible to everybody.
    /// </summary>
    [Description(
        "How to build a character from somebody's description: which two or three questions "
        + "are worth asking, what to decide silently, how to report back, and the JSON shape "
        + "the other tools take. Read this before proposing a character.")]
    public string CreationGuide() => _guide();

    // ── The catalogues ────────────────────────────────────────────────────

    /// <summary>Everything that is a short list rather than a search.</summary>
    public static IReadOnlyList<string> Categories { get; } =
    [
        "tiers", "packages", "abilities", "talents", "sources",
        "perks", "flaws", "pros", "cons", "gear_features"
    ];

    [Description(
        "The choices a character is built from, with their ids and the numbers that matter. "
        + "Categories: tiers, packages, abilities, talents, sources, perks, flaws, pros, "
        + "cons, gear_features. Ask for tiers before settling one — the tier sets the budget "
        + "and the Trait Cap.")]
    public string ListOptions(
        [Description("One of: tiers, packages, abilities, talents, sources, perks, flaws, pros, cons, gear_features.")]
        string? category)
    {
        // <b>Nullable because a client really can send null</b>, whatever the schema says —
        // `{"category": null}` arrives here as one, and the argument is declared for what can
        // actually arrive rather than for what a well-behaved caller would send. Declared
        // non-null, this guard read as dead code to an inspection and deleting it would have
        // turned a refusal into an exception across the transport.
        var wanted = (category ?? "").Trim().ToLowerInvariant();

        JsonArray entries;
        switch (wanted)
        {
            case "tiers":
                entries = [.. _rules.Tiers.Select(t => new JsonObject
                {
                    ["id"] = t.Id, ["name"] = t.Name,
                    ["hero_points"] = t.HeroPoints, ["trait_cap"] = t.TraitCapRank,
                    ["description"] = t.Description, ["notes"] = t.Notes
                })];
                break;

            case "packages":
                entries = [.. _rules.CreationRules.OptionalPackages.Select(p => new JsonObject
                {
                    ["id"] = p.Id, ["name"] = p.Name, ["hero_points"] = p.Cost,
                    ["grants_every_ability"] = p.AbilitiesRank,
                    ["grants_every_talent"]  = p.TalentsRank,
                    ["description"] = p.Description
                })];
                break;

            case "abilities":
                entries = [.. _rules.Abilities.Select(a => new JsonObject
                {
                    ["id"] = a.Id, ["name"] = a.Name,
                    ["hero_points_per_rank"] = a.CostPerRank,
                    ["ordinary_human_rank"]  = a.OrdinaryHumanRank,
                    ["description"] = a.Description
                })];
                break;

            case "talents":
                entries = [.. _rules.Talents.Select(t => new JsonObject
                {
                    ["id"] = t.Id, ["name"] = t.Name,
                    ["hero_points_per_rank"] = t.CostPerRank,
                    ["ordinary_human_rank"]  = t.OrdinaryHumanRank,
                    ["linked_ability"] = t.LinkedAbility,
                    ["description"] = t.Description
                })];
                break;

            case "sources":
                entries = [.. _rules.Sources.Select(s => new JsonObject
                {
                    ["id"] = s.Id, ["name"] = s.Name,
                    ["default_rank_ability"] = s.DefaultRankAbility,
                    ["description"] = s.Description
                })];
                break;

            case "perks":
                entries = [.. _rules.Perks.Select(p => new JsonObject
                {
                    ["id"] = p.Id, ["name"] = p.Name, ["cost_type"] = p.CostType,
                    ["hero_points"] = p.Cost, ["hero_points_per_unit"] = p.CostPerUnit,
                    ["unit"] = p.UnitLabel, ["description"] = p.Description,
                    ["narrative_constraint"] = p.NarrativeConstraint
                })];
                break;

            case "flaws":
                entries = [.. _rules.Flaws.Select(f => new JsonObject
                {
                    ["id"] = f.Id, ["name"] = f.Name, ["flaw_type"] = f.FlawType,
                    ["description"] = f.Description,
                    ["narrative_constraint"] = f.NarrativeConstraint
                })];
                break;

            case "pros":
                entries = [.. _rules.Pros.Select(p => ProCon(p, p.CostModifier, p.CostModifierRange))];
                break;

            case "cons":
                entries = [.. _rules.Cons.Select(c => ProCon(c, c.CostModifier, c.CostModifierRange))];
                break;

            case "gear_features":
                entries = [.. _rules.GearFeatures.Select(g => new JsonObject
                {
                    ["id"] = g.Id, ["name"] = g.Name, ["cost_type"] = g.CostType,
                    ["hero_points"] = g.Cost,
                    ["grades"] = Numbers(g.CostRange),
                    ["applies_to"] = g.AppliesTo, ["description"] = g.Description
                })];
                break;

            default:
                return Write(Judgement.Problem("NO_SUCH_CATEGORY",
                    $"There is no category '{category}'. The categories are: "
                    + string.Join(", ", Categories) + "."));
        }

        var report = new JsonObject
        {
            ["ok"] = true,
            ["category"] = wanted,
            ["entries"] = entries
        };

        if (wanted == "flaws")
        {
            var flawRules = _rules.CreationRules.FlawRules;
            report["at_creation"] = new JsonObject
            {
                ["minimum"] = flawRules.MinAtCreation,
                ["maximum"] = flawRules.MaxAtCreation
            };
        }

        if (wanted is "pros" or "cons")
            report["note"] = "These are the generic options. A Power may also have Pros and "
                           + "Cons printed in its own entry — power_detail lists both, and "
                           + "only the ones that Power may legally take.";

        return Write(report);
    }

    /// <summary>
    /// One generic option. <paramref name="power"/> is the Power it is being offered for, when
    /// there is one — <c>list_options</c> has no Power and passes null.
    ///
    /// <para>It is needed for two things a Power changes about the answer, and without them
    /// the document contradicted itself: Force Field is <c>"range": "self"</c> and legitimately
    /// offers the Ranged Pro, whose <c>applies_to_ranges</c> reads touch and zone — which a
    /// model reading its own tool output cannot tell from a bug. Now the row says the Power's
    /// own text is what allows it, and quotes the sentence.</para>
    /// </summary>
    private static JsonObject ProCon(
        IGenericProCon option, int? costModifier, IReadOnlyDictionary<string, int>? range,
        PowerModel? power = null)
    {
        var allowance = power?.ProsAllowedByOwnText
            .FirstOrDefault(a => option is ProModel &&
                                 string.Equals(a.Id, option.Id, StringComparison.Ordinal));

        // The grades this Power may actually pick, which is not always every grade printed:
        // those of Zone/Nova and Ranged encode a Range, and a Power reaching them through its
        // own text has one the rulebook does not price.
        var grades = range?
            .Where(kv => ProConApplicability.GradesFor(option, power, range.Keys)
                                            .Contains(kv.Key, StringComparer.Ordinal))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return new()
        {
            ["id"] = option.Id, ["name"] = option.Name,
            ["hero_points"] = costModifier,
            ["grades"] = Numbers(grades),
            ["applies_to_ranges"] = Strings(option.AppliesToRanges),
            ["applies_to_rank_types"] = Strings(option.AppliesToRankTypes),
            ["repeatable"] = option.Repeatable,
            ["allowed_by_this_power_text"] = allowance?.Reason,
            ["caveat"] = option.ApplicabilityCaveat
        };
    }

    // ── Powers ────────────────────────────────────────────────────────────

    [Description(
        "Which Powers could realise a described effect — search by what it does, in the "
        + "describer's own words ('turns invisible', 'punches through time'). Returns the "
        + "closest entries in the rulebook, which may be nothing that fits: there are 141 "
        + "Powers and the book does not have everything. Never invent a Power id.")]
    public string SearchPowers(
        [Description("What the effect does, in ordinary words.")] string? query,
        [Description("How many matches to return. Defaults to 8; anything outside 1 to 25 is "
                     + "brought inside it.")]
        int limit = 8)
    {
        var terms = Terms(query ?? "");

        if (terms.Count == 0)
            return Write(Judgement.Problem("EMPTY_QUERY",
                "Search for what the effect does, in ordinary words — 'turns invisible', "
                + "'hits very hard', 'reads minds'."));

        var wanted = Math.Clamp(limit, 1, 25);

        // Scored whole, then cut. <b>Deciding anything from the cut list is a lie about the
        // rulebook rather than about the search</b>: with limit 1, a Power that matched by
        // name at position 2 vanished and the answer then said nothing matched by name at
        // all — which the guide tells the assistant to act on by declining to build.
        var scored = _rules.Powers
            .Select(p => (Power: p, Score: Score(p, query!, terms)))
            .Where(m => m.Score.Points > 0)
            .OrderByDescending(m => m.Score.Points)
            .ThenByDescending(m => m.Score.MatchedTerms.Count)
            .ThenBy(m => m.Power.Name, StringComparer.Ordinal)
            .ToList();

        var matches = scored.Take(wanted).ToList();

        var entries = new JsonArray();

        foreach (var (power, score) in matches)
        {
            entries.Add(new JsonObject
            {
                ["id"] = power.Id,
                ["name"] = power.Name,
                ["category"] = power.Category,
                ["stat_line"] = PowerFormatter.StatLine(power),
                ["description"] = power.Description,
                ["matched_on"] = Strings(score.MatchedOn),

                // The actual words, because "matched on its description" does not say whether
                // the word was "invisible" or "through". A row here on one common word is a
                // coincidence, and a reader can only see that if the word is shown.
                ["matched_terms"] = Strings(score.MatchedTerms)
            });
        }

        // <b>How much a match is worth, said out loud.</b> A search that returns its five best
        // rows reads as five answers whatever the caution says, and the description naming
        // something the rulebook does not have is exactly the one a model will build anyway.
        //
        // <para>What this can honestly report is <em>how</em> the rows matched, not whether any
        // of them fits. The first version of this note went further and said a
        // description-only match "usually means the rulebook has no Power for this" — which
        // told an assistant that asked for a flying character that there is no Power for
        // flight. "Fly" is not a prefix of "Flight", so Flight matches that description on the
        // word inside its own entry, and the flag was right while the advice was wrong.</para>
        var nothingMatchedByName = scored.Count == 0 ||
            scored.TrueForAll(m => !m.Score.MatchedOn.Contains("name")
                                && !m.Score.MatchedOn.Contains("id")
                                && !m.Score.MatchedOn.Contains("tag"));

        return Write(new JsonObject
        {
            ["ok"] = true,
            ["query"] = query,
            ["searched"] = _rules.Powers.Count,
            ["found"] = scored.Count,
            ["matches"] = entries,
            ["nothing_matched_by_name"] = nothingMatchedByName,
            ["more_beyond_these"] = scored.Count > matches.Count,

            // Three different things to say, and saying the wrong one is how a model is told
            // to name "the nearest" out of a list with nothing in it.
            ["caution"] = scored.Count switch
            {
                0 => "Nothing matched at all — not by name and not by a word in any "
                   + "description. Say that the rulebook has no Power for this rather than "
                   + "naming one, and consider whether the effect is an Ability rank, an "
                   + "Expertise, a Perk, or narrative colour that costs nothing.",

                _ when nothingMatchedByName =>
                     "Nothing matched by name, id or tag: every entry above matched on a word "
                   + "inside its description. That cuts both ways — it is how Flight answers "
                   + "\"he can fly\", and it is also what a Power that has nothing to do with "
                   + "the description looks like. Read each one and say which, if any, does "
                   + "what was asked; if none does, say so rather than picking the top row.",

                _ => "These are the closest entries, not a promise that one of them does what "
                   + "was described. Read matched_terms on each: a row that matched one common "
                   + "word is a coincidence, and rows that matched the same words are in no "
                   + "meaningful order. If more_beyond_these is true there are others — search "
                   + "a more distinctive word, or raise limit. If none of them does it, say so, "
                   + "and consider whether the effect is an Ability rank, an Expertise, a Perk "
                   + "or narrative colour rather than a Power. A Power id that is not in this "
                   + "list does not exist."
            }
        });
    }

    [Description(
        "One Power in full: what it costs, what rank it starts at, what it does, and the Pros "
        + "and Cons it may legally take — both the generic ones the rulebook allows on a "
        + "Power of this Range and rank type, and any printed in the Power's own entry.")]
    public string PowerDetail(
        [Description("The Power's id — the \"id\" field of a search_powers match.")] string? powerId)
    {
        // Folded, because "Blast" is what a person reads off the page and every id in the
        // rules files is lower case. Refusing it would be a round trip spent on nothing —
        // and it is still an exact id, not a search: "blast power" remains no such Power.
        var id = (powerId ?? "").Trim().ToLowerInvariant();

        if (_rules.GetPower(id) is not { } power)
        {
            var terms = Terms(id.Replace('_', ' '));

            var near = _rules.Powers
                .Where(p => terms.Any(t =>
                    p.Id.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Select(p => p.Id)
                .Take(8)
                .ToList();

            var problem = Judgement.Problem("NO_SUCH_POWER",
                $"There is no Power with the id '{powerId}'. Search for the effect with "
                + "search_powers rather than guessing an id from a name — several do not match.");

            if (near.Count > 0)
                problem["problem"]!.AsObject()["did_you_mean"] = Strings(near);

            return Write(problem);
        }

        var report = new JsonObject
        {
            ["ok"] = true,
            ["id"] = power.Id,
            ["name"] = power.Name,
            ["category"] = power.Category,
            ["stat_line"] = PowerFormatter.StatLine(power),
            ["range"] = power.Range,
            ["rank_type"] = power.RankType,
            ["cost_type"] = power.CostType,
            ["cost_variants"] = Numbers(power.CostVariants),
            ["max_rank"] = power.MaxRank,
            ["ranks_purchasable"] = power.MaxRank != 0,
            ["unit"] = power.CostUnitLabel,
            ["description"] = power.Description,
            ["source_ref"] = power.SourceRef,
            ["pros"] = new JsonObject
            {
                ["generic"] = new JsonArray([.. _applicability.ProsFor(power)
                    .Select(p => ProCon(p, p.CostModifier, p.CostModifierRange, power))]),
                ["own"] = new JsonArray([.. power.PowerPros.Select(OwnProCon)])
            },
            ["cons"] = new JsonObject
            {
                ["generic"] = new JsonArray([.. _applicability.ConsFor(power)
                    .Select(c => ProCon(c, c.CostModifier, c.CostModifierRange, power))]),
                ["own"] = new JsonArray([.. power.PowerCons.Select(OwnProCon)])
            }
        };

        if (power.Prerequisite is { } prerequisite)
            report["baseline"] = new JsonObject
            {
                ["relationship"] = prerequisite.Relationship,
                ["ability"] = prerequisite.Ability,
                ["powers"] = Strings(prerequisite.Powers),
                ["fixed_value"] = prerequisite.FixedValue,
                ["description"] = prerequisite.Description,
                ["note"] = "Purchased ranks stack on top of this baseline, and the Trait Cap "
                         + "applies to the total."
            };

        return Write(report);
    }

    private static JsonObject OwnProCon(PowerProConModel option) => new()
    {
        ["id"] = option.Id,
        ["name"] = option.Name,
        ["cost_type"] = option.CostType,
        ["hero_points"] = option.CostModifier,
        ["hero_points_per_rank"] = option.CostPerRank,
        ["hero_points_per_unit"] = option.CostPerUnit,
        ["unit"] = option.CostUnitLabel,
        ["grades"] = Numbers(option.CostModifierRange),
        ["rank_grades"] = Numbers(option.CostPerRankRange),
        ["needs_variant"] = option.NeedsVariant,
        // Beside needs_variant for the same reason it is there: it is a fact about how to
        // shape the selection, and a model that has to infer it from the English "per
        // purchase" will either under-buy in silence or be refused for something the
        // rulebook permits. Blastwave's six energy types are five copies of one Pro.
        ["repeatable"] = option.Repeatable,
        ["description"] = option.Description
    };

    // ── The judge ─────────────────────────────────────────────────────────

    [Description(
        "THE JUDGE. Costs and validates a whole character against the rulebook and reports "
        + "what it spent on what. This is the only source of a Hero Point figure or of the "
        + "word 'legal' — never state a cost or call a character legal without it. It reports "
        + "and never repairs: an over-budget or illegal character comes back with every issue "
        + "and hero_points.remaining negative by exactly the overspend, and what to give up is "
        + "the player's decision to make.")]
    public string CheckCharacter(
        [Description("The character's inputs, as the JSON object described by creation_guide.")]
        JsonElement character)
    {
        if (!TryReadCharacter(character, out var read, out var problem))
            return Write(problem);

        return Write(_judgement.Judge(read));
    }

    [Description(
        "The printed character sheet, as text — the thing a person actually reads. Offer it "
        + "once the character is settled. A character the engine cannot price has no sheet.")]
    public string CharacterSheetText(
        [Description("The character's inputs, as the JSON object described by creation_guide.")]
        JsonElement character)
    {
        if (!TryReadCharacter(character, out var sheet, out var problem))
            return Write(problem);

        try
        {
            var validation = _validator.Validate(sheet);

            return CharacterSheetRenderer.RenderText(
                sheet, _rules, _costs, _derived, validation, _now());
        }
        catch (Exception e) when (Judgement.IsUnanswerable(e))
        {
            // A sheet prints costs, so a character the engine cannot price has no sheet to
            // print. That is the same finding check_character reports as an issue, and the
            // caller is sent there rather than being handed a stack trace.
            return Write(Judgement.Problem("NO_SHEET_TO_PRINT",
                "This character could not be priced, so there is no sheet to print. Run "
                + "check_character for the issues to fix."));
        }
    }

    // ── Reading a character ───────────────────────────────────────────────

    /// <summary>
    /// The character as the engine's own shape, or null if it is not one.
    ///
    /// <para><b>Read strictly</b>, exactly as the <c>build</c> command reads a submitted file:
    /// a field name that is not part of a character is refused rather than ignored. A
    /// misspelled <c>AbilityRanks</c> would otherwise drop every Ability and produce a
    /// cheaper, legal character nobody notices is wrong — which is the failure this whole
    /// surface exists to prevent, arriving through the front door.</para>
    ///
    /// <para>A client that sends the character as a JSON <em>string</em> rather than an object
    /// is accommodated. Both are the same character, the schema cannot stop either, and
    /// refusing one on a technicality would read to the person as the tool being broken.</para>
    /// </summary>
    private static bool TryReadCharacter(
        JsonElement character, out CharacterSheet sheet, out JsonObject problem)
    {
        sheet = new CharacterSheet();
        problem = new JsonObject();

        var text = character.ValueKind switch
        {
            JsonValueKind.String    => character.GetString() ?? "",
            JsonValueKind.Undefined => "",
            _                       => character.GetRawText()
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            problem = Judgement.Problem("NO_CHARACTER",
                "No character arrived. Pass the character's inputs as a JSON object — "
                + "creation_guide describes the shape.");
            return false;
        }

        try
        {
            // A JSON null — the literal text "null" — deserializes to a null sheet rather
            // than throwing, and would otherwise arrive as a legal empty character.
            if (CharacterSheetJson.Read(text, strict: true) is not { } read)
            {
                problem = Judgement.Problem("NO_CHARACTER", "That holds no character.");
                return false;
            }

            sheet = read;
            return true;
        }
        // The message does not repeat the exception's own words, which name the C# type that
        // failed to build — this program talking about itself to somebody holding a rulebook.
        // The path and the position are the useful half and are kept.
        catch (JsonException e)
        {
            var where = e.Path is { } path ? $" at {path}" : "";
            var line  = e.LineNumber is { } n ? $", line {n + 1}" : "";

            // <b>Which of the two it was, rather than one guess for both.</b> Reading the same
            // text leniently ignores unknown field names and nothing else, so if that succeeds
            // the field name was the problem and if it fails the value was. The message used
            // to say "check the spelling" either way — and `"might": "8d"` is a correctly
            // named field holding the rank written the way the rulebook writes it, which is
            // the likeliest first mistake there is. A caller was sent hunting for a
            // misspelling that did not exist.
            var fieldName = true;

            try { _ = CharacterSheetJson.Read(text, strict: false); }
            catch (Exception second) when (second is JsonException or InvalidOperationException)
            {
                fieldName = false;
            }

            problem = Judgement.Problem("CHARACTER_UNREADABLE",
                $"That is not a character in the shape these tools take{where}{line}. "
                + (fieldName
                    ? "A field name that is not part of a character is refused rather than "
                      + "ignored, so check the spelling against creation_guide."
                    : "The field name is one a character has and the value in it is the wrong "
                      + "kind of thing — a rank is a number, so 8 rather than \"8d\"."));
            return false;
        }
        // InvalidOperationException, not JsonException, is what the deserializer throws when
        // asked to put a null into one of the get-only collections — "AbilityRanks": null is
        // well-formed JSON that any hand-written character might carry.
        catch (InvalidOperationException)
        {
            problem = Judgement.Problem("CHARACTER_UNREADABLE",
                "That is not a character in the shape these tools take: one of its lists is "
                + "null. Leave a section out rather than setting it to null.");
            return false;
        }
    }

    // ── Searching ─────────────────────────────────────────────────────────

    private readonly record struct Match(
        int Points, IReadOnlyList<string> MatchedOn, IReadOnlyList<string> MatchedTerms);

    /// <summary>
    /// How well a Power answers a described effect. Deliberately dull: a name match beats an
    /// id match beats a tag beats a description, and the reasons come back with the result so
    /// a reader can see that "closest" meant "the word appears in its description" rather
    /// than anything cleverer. Nothing here decides that a Power <em>fits</em>.
    ///
    /// <para><b>It is not a relevance engine and the answer says so.</b> "Walks through walls"
    /// puts twenty-one Powers on two points each — every one of them matching only the filler
    /// word "through", Phasing among them — and which eight a caller sees is then alphabetical.
    /// Weighting a word by how much of the rulebook uses it was tried: it fixed that query and
    /// broke "reads minds", which dropped Telepathy out of the first three because four Powers
    /// carry "mind" in their names. Tuning it properly is a piece of work with its own
    /// evidence, so what ships instead is the truth about each row — which words it matched,
    /// and that rows matching the same words are in no order worth reading.</para>
    /// </summary>
    private static Match Score(PowerModel power, string query, IReadOnlyList<string> terms)
    {
        var points = 0;
        var matchedOn = new List<string>();
        var matchedTerms = new List<string>();

        if (string.Equals(power.Name, query.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(power.Id, query.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            points += 100;
            matchedOn.Add("name");
        }

        foreach (var term in terms)
        {
            var matched = false;

            if (Mentions(power.Name, term))
            {
                points += 10;
                matched = true;
                if (!matchedOn.Contains("name")) matchedOn.Add("name");
            }
            else if (Mentions(power.Id, term))
            {
                points += 8;
                matched = true;
                if (!matchedOn.Contains("id")) matchedOn.Add("id");
            }

            if (power.Tags.Any(t => Mentions(t, term)))
            {
                points += 6;
                matched = true;
                if (!matchedOn.Contains("tag")) matchedOn.Add("tag");
            }

            if (Mentions(power.Category, term))
            {
                points += 4;
                matched = true;
                if (!matchedOn.Contains("category")) matchedOn.Add("category");
            }

            if (Mentions(power.Description, term))
            {
                points += 2;
                matched = true;
                if (!matchedOn.Contains("description")) matchedOn.Add("description");
            }

            if (matched) matchedTerms.Add(term);
        }

        return new Match(points, matchedOn, matchedTerms);
    }

    /// <summary>
    /// Whether a piece of text uses a word, allowing for the endings English puts on it —
    /// "regenerates" finds Regeneration, "invisible" finds Invisibility, "flying" finds a
    /// description that says "you can fly".
    ///
    /// <para><b>Word by word rather than by substring</b>, which is not a refinement: a
    /// substring search matched "she bakes bread in the city" to <em>Plasticity</em>, and a
    /// match like that is worse than no match, because it arrives looking exactly like a real
    /// one and there is nothing in it a reader can see is wrong. (This said <em>Elasticity</em>,
    /// which is not a Power in this rulebook.)</para>
    ///
    /// <para><b>One line puts the substring search back, and nothing stopped it for a whole
    /// slice.</b> Every search test was a positive assertion or a negative on a query whose
    /// words happen not to be substrings of anything, so this method's whole reason for existing
    /// was unasserted. It is pinned now by fragments that occur inside a Power's name and nowhere
    /// in the rules files as a word; the baker's sentence above is one of them and has to stay at
    /// <c>found: 0</c>.</para>
    /// </summary>
    private static bool Mentions(string text, string term)
    {
        var stem = Stem(term);

        foreach (var word in text.ToLowerInvariant()
                     .Split(NotAWord, StringSplitOptions.RemoveEmptyEntries))
        {
            if (word == term || word == stem) return true;

            // A shared prefix long enough to be the same word with a different ending, rather
            // than two words that happen to start alike: "invisible" and "invisibility" share
            // seven letters, "bread" and "breath" share four.
            //
            // <b>It does let a coincidence through</b> — "animals" reaches Animation, which
            // shares five — and requiring the leftovers to be short instead was tried and is
            // worse: the leftovers of animal/animation are "l" and "tion", and those of
            // invisible/invisibility are "le" and "ility", so any rule that refuses the first
            // refuses the second. A search that misses Invisibility for "invisible" is a worse
            // tool than one that offers Animation for "animals", and `matched_terms` shows the
            // reader which word did it.
            var shared = SharedPrefixLength(word, term);
            if (shared >= 5 && shared >= Math.Min(word.Length, term.Length) - 3) return true;
        }

        return false;
    }

    private static int SharedPrefixLength(string a, string b)
    {
        var length = 0;
        while (length < a.Length && length < b.Length && a[length] == b[length]) length++;
        return length;
    }

    /// <summary>
    /// The words worth searching on. Stripped of punctuation and of the words that are in
    /// every sentence — without that, "he can turn invisible" matches every Power whose
    /// description contains "can".
    /// </summary>
    private static IReadOnlyList<string> Terms(string query) =>
        [.. query
            .ToLowerInvariant()
            .Split(NotAWord, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !Stopwords.Contains(w))
            .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// A word with the commonest English ending taken off, when what is left is still a word
    /// worth matching on. Deliberately crude — anything cleverer would be a stemmer, and a
    /// stemmer that gets one word wrong is harder to explain than a substring search that
    /// misses one.
    /// </summary>
    private static string Stem(string word)
    {
        foreach (var ending in Endings)
        {
            if (word.Length - ending.Length >= 3 && word.EndsWith(ending, StringComparison.Ordinal))
                return word[..^ending.Length];
        }

        return word;
    }

    private static readonly string[] Endings = ["ing", "es", "ed", "s"];

    /// <summary>
    /// What separates one word from the next, in a query and in the rules text alike. The
    /// underscore is here because a Power's id is words joined by one, and the em dash because
    /// several names carry one — "Super Senses — Thermal Vision".
    /// </summary>
    private static readonly char[] NotAWord =
    [
        ' ', '\t', '\n', '\r', ',', '.', ';', ':', '!', '?', '"', '\'',
        '(', ')', '[', ']', '/', '\\', '-', '_', '—', '–', '·'
    ];

    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "the", "and", "but", "can", "for", "his", "her", "its", "their", "them", "they",
        "she", "him", "who", "that", "this", "with", "from", "into", "onto", "out", "off",
        "are", "was", "were", "been", "has", "have", "had", "does", "did", "you", "your",
        "any", "all", "one", "two", "not", "when", "what", "how", "why", "where", "which",
        "able", "very", "just", "like", "also", "make", "makes", "made", "get", "gets",
        "power", "powers", "character", "something", "someone", "anything",

        // "super" is in seventeen Power names — Super Speed and the sixteen Super Senses
        // options — so "super strong" answered with four of them and neither Might nor
        // Strike. It carries no information in this rulebook, which is about supers.
        "super",

        // The rest of the prepositions and indefinite pronouns already in this list —
        // "through" is the one PROGRESS.md item 4 names by name: "walks through walls"
        // put twenty Powers on the same two points for matching nothing but this word,
        // eighteen of them entries whose only connection to walking through a wall is
        // that their own description happens to use "through" for something else
        // entirely. The rest were found the same way "someone"/"something"/"anything"
        // already were — a query built to reproduce a real sentence turns out to be full
        // of connective words that are in every third Power's description and say
        // nothing about what that Power does.
        "through", "than", "anyone", "everyone", "over", "under", "around", "against",
        "down", "back", "once", "another", "else", "somewhere", "before", "after",
        "without", "across", "toward", "towards", "upon", "near"
    };

    // ── JSON helpers ──────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions Formatting = new() { WriteIndented = true };

    private static string Write(JsonNode report) => report.ToJsonString(Formatting);

    private static JsonArray Strings(IEnumerable<string> values) =>
        [.. values.Select(v => JsonValue.Create(v))];

    private static JsonObject? Numbers<T>(IReadOnlyDictionary<string, T>? values)
        where T : struct, IConvertible
    {
        if (values is null) return null;

        var node = new JsonObject();

        foreach (var (key, value) in values)
            node[key] = JsonValue.Create(Convert.ToDouble(value, CultureInfo.InvariantCulture));

        return node;
    }
}
