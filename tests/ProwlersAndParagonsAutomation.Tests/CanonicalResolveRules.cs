namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Chapter 5, Resolve and Adversity (pp.83-86), transcribed from the rulebook. This is the
/// reference <c>data/rules/play/resolve.json</c> is checked against, so a data edit that
/// contradicts the book fails a test rather than quietly changing what a point buys.
///
/// <para><b>Same standing as <see cref="CanonicalPowers"/> and
/// <see cref="CanonicalChallengeRules"/>: this file is the rulebook.</b> Do not "fix" a failing
/// test by editing a number here. Open the page named beside the value and fix whichever side is
/// actually wrong.</para>
///
/// <para><b>Every value carries the sentence it came out of</b>, for the reason
/// <see cref="CanonicalChallengeRules"/> gives at length: an adversarial pass over the Chapter 3
/// slice found some twenty booleans and numbers that deserialized and were compared to nothing,
/// and a value with no quote beside it is a claim nobody can check.</para>
///
/// <para><b>What must never be here is a reading.</b> Which of the six ways of earning a
/// simulator could apply on its own is this repository's distinction and not the book's; it lives
/// in the data under <c>interpretation</c> and is derived by
/// <see cref="PlayRulesDataTests.TheEarningsASimulatorCouldApplyAreExactlyTheOnesWithAStatedTrigger"/>.</para>
/// </summary>
public static class CanonicalResolveRules
{
    /// <summary>One printed row of the Resolve table: how far below the Trait Cap, and what that pays.</summary>
    public sealed record StartingResolveRow(int RanksBelowTraitCap, int Resolve);

    /// <summary>One row of the Challenge Level guidance, p.85.</summary>
    public sealed record ChallengeLevelGuidance(int Level, string UsedFor);

    // ── The chapter, and who holds which pool ────────────────────────────────

    /// <summary>The printed pages this chapter occupies. p.86 carries no chapter text.</summary>
    public const int FirstPage = 83;
    public const int LastPage = 86;

    /// <summary>
    /// <b>The settled rule, and it is printed in this chapter twice.</b> "Because only Heroes have
    /// Resolve, one of the players must spend Resolve any time a friendly Extra uses a Power that
    /// requires it" (p.84), and "As the GM, you have something even better, you have Adversity"
    /// (p.85). Every spend in the file is keyed to one of these two, so
    /// <c>CLAUDE.md</c>'s "only Heroes have Resolve; the GM gets Adversity" is data rather than
    /// prose.
    /// </summary>
    public const string ResolveIsSpentBy = "hero";
    public const string AdversityIsSpentBy = "gm";

    /// <summary>
    /// The two pools the chapter names, as the <c>currency</c> each spend declares. <b>Every spend
    /// says which pool it draws on, and that is what keys it to a side of the screen</b> — the id
    /// prefix does not, and three spends had no printed cost for a cost-shaped field to key them by
    /// either: the combat spends defer to Ch.4, the Powers one charges whatever the Power asks, and
    /// the GM's mirror charges whatever it is imitating.
    /// </summary>
    public const string ResolveCurrency = "resolve";
    public const string AdversityCurrency = "adversity";

    /// <summary>
    /// Which pool is whose, from the chapter's own two sentences — p.84 "Because only Heroes have
    /// Resolve…" and p.85 "As the GM, you have something even better, you have Adversity". A spend
    /// whose <c>who</c> and <c>currency</c> disagree contradicts one of them, and
    /// <see cref="PlayRulesDataTests.EveryResolveSpendIsTheHerosAndEveryAdversitySpendIsTheGms"/>
    /// says which.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PoolHolders =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ResolveCurrency] = ResolveIsSpentBy,
            [AdversityCurrency] = AdversityIsSpentBy
        };

    // ── The Resolve table, p.83 ──────────────────────────────────────────────

    public const int StartingResolvePage = 83;

    /// <summary>
    /// "You begin every issue—again, that's every game session—with some amount of Resolve. Your
    /// starting Resolve depends on your highest ranked Ability or Power (subject to the exceptions
    /// below): the lower the rank, the higher your Resolve."
    /// </summary>
    public const string StartingResolveGrantedAt = "the start of every issue";
    public const bool IssueIsAGameSession = true;
    public const string StartingResolveDependsOn =
        "the character's highest ranked Ability or Power, subject to the exceptions";
    public const string StartingResolveMeasuredFrom = "trait_cap";

    /// <summary>
    /// The printed table: "Trait Cap 0 · Trait Cap −1d 2 · Trait Cap −2d 4 · Trait Cap −3d 6 ·
    /// Etc. Etc." Four rows and an explicit continuation, so the ladder has no printed end — the
    /// same open-ended shape the Thresholds table gives Godlike's "12 or more".
    /// </summary>
    public const int StartingResolveAtTraitCap = 0;
    public const int StartingResolvePerRankBelowCap = 2;
    public const bool StartingResolveTableContinues = true;

    public static readonly IReadOnlyList<StartingResolveRow> StartingResolveTable =
    [
        new(0, 0),
        new(1, 2),
        new(2, 4),
        new(3, 6)
    ];

    /// <summary>
    /// This project's spelling of the printed table as arithmetic, which is what
    /// <see cref="Engine.DerivedStatsCalculator.CalculateResolve"/> already implements — and the
    /// two are held to the same answer by
    /// <see cref="PlayRulesDataTests.TheEngineComputesTheTableThisFileRecords"/> rather than by
    /// anybody's reading of either.
    /// </summary>
    public const string StartingResolveFormula = "resolve = (trait_cap - highest_relevant_rank) * 2";

    // ── Exceptions, p.83 ─────────────────────────────────────────────────────

    public const int ExceptionsPage = 83;

    /// <summary>
    /// "Talents do not affect Resolve. … Likewise, Powers that cannot be used for attack, defense,
    /// or to affect other characters or objects do not affect Resolve. For example, Powers like
    /// Danger Sense, Detection, Expertise (except for combat skills), Flight, Leaping, Running,
    /// Super Senses, Swimming, Swinging, Teleportation, and Tunneling do not usually affect
    /// Resolve. Nevertheless, the GM has the final say when determining which Powers affect
    /// Resolve."
    ///
    /// <para><b>The list is illustrative twice over</b> — "For example" opens it and "usually"
    /// qualifies it — so <see cref="NamedResolveExemptPowers"/> is a set of instances of
    /// <see cref="ResolveExemptCriterion"/>, never the criterion itself.</para>
    /// </summary>
    public const bool TalentsCountTowardsResolve = false;
    public const string ResolveExemptCriterion =
        "a Power that cannot be used for attack, defense, or to affect other characters or objects";
    public const string ExpertiseQualifier = "except for combat skills";
    public const bool NamedResolveExemptionsAreQualifiedAsUsual = true;
    public const bool GmHasFinalSayOnResolveExemptions = true;

    public static readonly IReadOnlyList<string> NamedResolveExemptPowers =
    [
        "Danger Sense",
        "Detection",
        "Expertise",
        "Flight",
        "Leaping",
        "Running",
        "Super Senses",
        "Swimming",
        "Swinging",
        "Teleportation",
        "Tunneling"
    ];

    /// <summary>
    /// "If you can increase the rank of an Ability or Power through other Powers like Boost or
    /// Shapeshifting, use your maximum possible rank when checking this table."
    /// </summary>
    public const string MaximumRankAppliesWhen = "another Power can raise the rank of an Ability or Power";
    public const string RankUsedWhenAPowerCanRaiseIt = "maximum possible rank";

    public static readonly IReadOnlyList<string> RankRaisingPowersNamed = ["Boost", "Shapeshifting"];

    // ── Carryover, p.83 ──────────────────────────────────────────────────────

    public const int CarryoverPage = 83;

    /// <summary>
    /// "Resolve doesn't carry over between issues. When an issue ends, unspent Resolve is lost.
    /// This is to encourage players to spend Resolve. However, GMs can occasionally allow Resolve
    /// to carry over between issues. … This should be done sparingly, however, as it encourages
    /// hoarding."
    /// </summary>
    public const bool ResolveCarriesOverBetweenIssues = false;
    public const bool UnspentResolveIsLostAtIssueEnd = true;
    public const bool GmMayAllowResolveCarryover = true;
    public const bool ResolveCarryoverShouldBeRare = true;

    // ── Earning Resolve, pp.83-84 ────────────────────────────────────────────

    public const int EarningResolvePage = 83;

    /// <summary>
    /// "Although some of the more common ways are set forth below, what these examples really
    /// amount to is roleplaying an interesting superhero… Thus, GMs should feel free to award
    /// Resolve whenever they see fit."
    /// </summary>
    public const bool GmMayAwardResolveWheneverTheySeeFit = true;
    public const bool ListedWaysToEarnAreExamples = true;

    /// <summary>
    /// "you earn 1 Resolve any time you suffer a significant defeat, failure, or setback, which
    /// may or may not involve combat. You can only earn this extra Resolve once per battle, even
    /// if you get defeated multiple times in the same encounter."
    /// </summary>
    public const int DefeatAward = 1;
    public const string DefeatTrigger = "a significant defeat, failure or setback";
    public const bool DefeatMayBeOutsideCombat = true;
    public const int DefeatLimitPerBattle = 1;

    /// <summary>
    /// "you earn 1 Resolve whenever you bring one of your Flaws into play in a way that causes
    /// problems for you or your allies. Of course, you can only do this when it makes sense within
    /// the context of the game. … Note that certain Flaws work differently, granting you 1 extra
    /// point of Resolve per issue instead."
    /// </summary>
    public const int FlawAward = 1;
    public const string FlawTrigger =
        "bringing one of your own Flaws into play so that it causes trouble for you or for your allies";
    public const bool FlawMustFitTheSituation = true;
    public const int SomeFlawsAwardPerIssueInstead = 1;

    /// <summary>
    /// "You can earn Resolve by creating and playing out an interlude. Like subplots and
    /// flashbacks, interludes are short scenes that involve the Heroes but aren't directly related
    /// to the current story of the game. For more information about what interludes are and how
    /// they work, see Chapter 9."
    ///
    /// <para><b>No amount is printed</b>, which is why the entry records the absence rather than
    /// the one point every other row in the section states.</para>
    /// </summary>
    public const bool InterludeAwardIsStated = false;
    public const string InterludeTrigger = "creating and playing out an interlude";
    public const string InterludeIs = "a short scene involving the Heroes but set aside from the current story";
    public const string InterludeDetailChapter = "Ultimate Edition, Ch.9 Superhero Gaming";

    /// <summary>
    /// "You earn 1 Resolve whenever you do something unwise, out of character, or detrimental to
    /// yourself or your allies because of your motivation. … However, this is not meant to
    /// encourage impulsiveness, stupidity, or intentionally derailing a game."
    /// </summary>
    public const int MotivationAward = 1;
    public const string MotivationTrigger =
        "acting unwisely, out of character, or to your own or your allies' cost because of your motivation";
    public const bool MotivationIsNotAnInvitationToDerailTheGame = true;

    /// <summary>
    /// "You earn 1 Resolve whenever the GM feels you've done something exceptionally clever,
    /// dramatic, funny, heroic, or just plain cool. Great roleplaying, brilliant comedy, clever
    /// problem solving, and strategic thinking could all merit this reward, as could taking the
    /// time to help a new player."
    /// </summary>
    public const int RoleplayingAward = 1;
    public const string RoleplayingTrigger =
        "play the GM judges exceptionally clever, dramatic, funny, heroic or cool";
    public const bool RoleplayingIsGmJudged = true;

    public static readonly IReadOnlyList<string> RoleplayingExamples =
    [
        "great roleplaying",
        "brilliant comedy",
        "clever problem solving",
        "strategic thinking",
        "helping a new player"
    ];

    /// <summary>
    /// "If you find yourself in a pinch and out of Resolve, you can dig deep and push yourself.
    /// This grants you 1 point of Resolve you have to use immediately (on that same page), but
    /// once you do, you pass out from the strain of whatever you were doing and you remain that
    /// way for the rest of the scene."
    /// </summary>
    public const int SacrificeAward = 1;
    public const string SacrificeAvailableWhen = "you are out of Resolve and in a pinch";
    public const bool SacrificeMustBeSpentOnTheSamePage = true;
    public const bool SacrificeThenUnconscious = true;
    public const string SacrificeUnconsciousUntil = "the end of the scene";

    // ── Spending Resolve, p.84 ───────────────────────────────────────────────

    public const int SpendingResolvePage = 84;

    /// <summary>
    /// "As the narrative currency of the game, Resolve can be spent in a few different ways. The
    /// basic uses for Resolve are set forth below. As with everything else, GMs should feel free
    /// to expand how Resolve can be used in their games."
    /// </summary>
    public const bool GmMayExpandResolveUses = true;
    public const bool ListedResolveUsesAreTheBasicOnes = true;

    /// <summary>
    /// "You can share Resolve with your allies, giving them as many points as you wish. You must
    /// describe how you're assisting a character when you give them Resolve. This purely for the
    /// sake of the narrative: your actions have no mechanical effect other than letting you share
    /// Resolve. If you can't actually assist your ally (because you're unconscious, not in the
    /// scene, otherwise occupied, etc.), you have to spend 2 points of Resolve for every point you
    /// want to share, and you have to narrate a brief flashback or memory…"
    ///
    /// <para><b>Read that quote for a par rate and it is not there, which is why there is no
    /// constant for one.</b> The only rate the page prices is the penalty — two points for every
    /// point shared, charged to a giver who could not actually be assisting — and "as many points
    /// as you wish" says how many may move, never what each costs. <c>SharePointCost = 1</c> stood
    /// here with this quote beside it, which made a reading look like a transcription and put it
    /// under the "do not edit this to match the code" notice. One-for-one is now an
    /// <c>interpretation</c> on the entry, derived from the printed penalty by
    /// <see cref="PlayRulesDataTests.TheParShareRateIsInferredFromThePrintedPenaltyRate"/>.</para>
    /// </summary>
    public const bool OrdinaryShareRateIsPrinted = false;
    public const int SharePointCostWhenUnableToAssist = 2;
    public const string SharePointsLimit = "as many as you wish";
    public const bool ShareMustNarrateTheAssistance = true;
    public const bool ShareNarrationHasNoMechanicalEffect = true;
    public const bool ShareFlashbackRequiredWhenUnable = true;

    public static readonly IReadOnlyList<string> UnableToAssistExamples =
        ["unconscious", "not in the scene", "otherwise occupied"];

    /// <summary>
    /// "You can spend Resolve to add extra dice to your challenge rolls on a one-for-one basis.
    /// You can spend as much Resolve as you wish, and you can decide whether to do so after
    /// rolling the dice."
    /// </summary>
    public const int ChallengeRollDiceCost = 1;
    public const int ChallengeRollDiceGained = 1;
    public const bool ChallengeRollDiceSpendIsUnlimited = true;
    public const bool ChallengeRollDiceDecidedAfterTheRoll = true;

    /// <summary>
    /// "You can also spend 1 Resolve to reroll a challenge roll completely. If you've already
    /// spent Resolve to add extra dice to the roll, you can reroll those extra dice as well."
    /// </summary>
    public const int RerollChallengeRollCost = 1;
    public const string RerollChallengeRollCovers = "the whole challenge roll";
    public const bool RerollIncludesDiceBoughtWithResolve = true;

    /// <summary>
    /// "Last, you can spend 1 Resolve to reroll any other type of roll, such as the one made to
    /// determine if your Unreliable Power works."
    /// </summary>
    public const int RerollAnyOtherRollCost = 1;
    public const string RerollAnyOtherRollAppliesTo = "any roll that is not a challenge roll";
    public const string RerollAnyOtherRollExample = "the roll that decides whether an Unreliable Power works";

    /// <summary>
    /// "There are several ways to spend Resolve in combat. They include (a) seizing the initiative
    /// to act first (or if the GM prefers, to double your Edge) on every page of the action,
    /// (b) recovering from a defeat or a special effect, (c) knocking a target backwards,
    /// (d) luring an attacker into hitting something or someone other than you, (e) enhancing your
    /// team attack by making your dice explode, and (f) reducing the impact or lethality of
    /// certain Gritty Combat Rules. For more on this, see Chapter 4, Combat."
    ///
    /// <para><b>Recorded as references, not transcribed.</b> Each of the six is specified in
    /// Chapter 4; copying it here would create a second transcription to disagree with the
    /// first.</para>
    ///
    /// <para><b>Including the parenthesis, which is why there is no constant for it.</b> The
    /// entry once carried the GM's alternative and its duration as fact fields, on the reading
    /// that Chapter 5's parenthesis was the only printing of them. It is not: <b>Ch.4 p.73,
    /// SEIZING INITIATIVE, prints the whole rule</b> — "You can spend 1 Resolve to jump ahead of
    /// everyone else in combat. From that point on, you act first on every page of the action. …
    /// Optionally, rather than allowing characters to automatically act first, GMs may instead
    /// have this double a character's effective Edge…" — so the values belong to Chapter 4's
    /// slice and a Chapter 5 copy of them would be exactly the second transcription this entry
    /// exists to avoid. <see cref="PlayRulesDataTests.AnEntryThatDefersToAnotherChapterCarriesReferencesAndNothingElse"/>
    /// now refuses any fact field beyond the reference list on an entry that says
    /// <c>transcribed_here: false</c>.</para>
    /// </summary>
    public const bool CombatSpendsAreTranscribedHere = false;
    public const string CombatSpendsDetailChapter = "Ultimate Edition, Ch.4 Combat";

    public static readonly IReadOnlyList<string> CombatSpendRefs =
    [
        "seize_initiative",
        "recover_from_a_defeat_or_special_effect",
        "knock_a_target_backwards",
        "lure_an_attacker_into_hitting_something_else",
        "make_a_team_attacks_dice_explode",
        "reduce_the_impact_of_a_gritty_combat_rule"
    ];

    /// <summary>
    /// "You can spend 1 Resolve to make up some minor detail about the game world, usually a lucky
    /// break or incredible coincidence that works in your favor. For example, if you get knocked
    /// off a rooftop, you can use this to say you land on a pile of discarded mattresses. This is
    /// always subject to the GM's approval."
    /// </summary>
    public const int LuckyBreakCost = 1;
    public const string LuckyBreakInvents =
        "a minor detail of the game world, usually a coincidence in the spender's favour";
    public const bool LuckyBreakSubjectToGmApproval = true;
    public const string LuckyBreakExample = "landing on discarded mattresses after being knocked off a roof";

    /// <summary>
    /// "You can spend 1 Resolve to use one of your Abilities or Powers in a way that imitates
    /// another Power. If the stunt is even remotely reasonable, the imitated Power has the same
    /// rank as the Ability or Power used to imitate it. To be clear, this doesn't grant you a new
    /// Power."
    /// </summary>
    public const int PowerStuntCost = 1;
    public const string PowerStuntUses =
        "an Ability or Power the character already has, in a way that imitates a different Power";
    public const string PowerStuntRankSource = "the Ability or Power doing the imitating";
    public const bool PowerStuntRequiresRemotelyReasonable = true;
    public const bool PowerStuntGrantsANewPower = false;

    /// <summary>
    /// "you may have to spend Resolve to use certain Powers. Because only Heroes have Resolve, one
    /// of the players must spend Resolve any time a friendly Extra uses a Power that requires it.
    /// If none of the players are willing to spend the Resolve, the Extra can't or won't use the
    /// Power at that time. Mind you, this doesn't affect how these characters use their Powers
    /// when they aren't around the Heroes."
    /// </summary>
    public const bool SomePowersRequireResolve = true;
    public const bool OnlyHeroesHaveResolve = true;
    public const bool PlayerMustSpendForAFriendlyExtra = true;
    public const bool ExtraCannotUseThePowerIfNobodySpends = true;
    public const bool PowerResolveRuleAppliesOnlyWhileTheExtraIsWithTheHeroes = true;

    // ── The reroll floor, p.85 ───────────────────────────────────────────────

    public const int RerollFloorPage = 85;

    /// <summary>
    /// The Super Tip beside the Adversity rules: "Spending Resolve should never make things worse.
    /// If a player spends Resolve to reroll a roll, let them keep the first roll if the second
    /// turns out to be worse than the original."
    /// </summary>
    public const bool SpendingShouldNeverMakeThingsWorse = true;
    public const bool KeepTheFirstRollIfTheRerollIsWorse = true;
    public const string RerollFloorAppliesTo = "a reroll bought with Resolve";

    // ── Adversity, p.85 ──────────────────────────────────────────────────────

    public const int AdversityPage = 85;

    /// <summary>
    /// "The GM begins every issue with 1 point of Adversity for every Hero in the game, and never
    /// carries Adversity over between issues. … GMs should, of course, feel free to add to this
    /// list as they see fit, but they shouldn't earn Adversity as easily as Heroes earn Resolve.
    /// Adversity is supposed to be more of a fixed resource than Resolve."
    ///
    /// <para>Chapter 1's summary prints the rate a second time on p.11 — "GMs begin every issue
    /// with 1 point of Adversity per Hero" — which is the only independent printing of any figure
    /// in this chapter, and <c>corroborated_by</c> cites it.</para>
    /// </summary>
    public const int AdversityPerHeroPerIssue = 1;
    public const string AdversityHeldBy = "gm";
    public const bool AdversityCarriesOverBetweenIssues = false;
    public const bool AdversityIsMoreOfAFixedResource = true;
    public const bool GmMayAddWaysToEarnAdversity = true;
    public const bool AdversityShouldNotBeAsEasyToEarnAsResolve = true;

    /// <summary>The page of Chapter 1's summary that reprints the Adversity rate.</summary>
    public const int ChapterOneSummaryPage = 11;

    /// <summary>
    /// "One way to support this is to give these scenes a Challenge Level, usually from 1 to 3. …
    /// Multiply a scene's Challenge Level by the number of Heroes in the game to determine how much
    /// Adversity to award the GM. … GMs can use this extra Adversity however they wish, spending it
    /// right away or saving it to torment the Heroes later that issue. Only a handful of scenes in
    /// any story should have a Challenge Level."
    ///
    /// <para><b>The award is recorded as its operands rather than as a sentence</b>, so the worked
    /// example on the same page can be resolved through the data: drop a factor and the product
    /// stops being 8.</para>
    /// </summary>
    public const string ChallengeLevelOperation = "product";
    public const string ChallengeLevelAwardedAt = "the start of the scene";
    public const int ChallengeLevelTypicalMin = 1;
    public const int ChallengeLevelTypicalMax = 3;
    public const bool ChallengeLevelThreeMayBeExceeded = true;
    public const bool OnlyAHandfulOfScenesHaveAChallengeLevel = true;
    public const bool ChallengeLevelAdversityMayBeSavedForLater = true;

    public static readonly IReadOnlyList<string> ChallengeLevelAwardFactors = ["challenge_level", "hero_count"];

    /// <summary>
    /// "Most of these scenes will be Challenge Level 1, which is suitable for high-stakes action
    /// scenes and ordinary fight scenes involving super-powered enemies. Challenge Level 2 … is
    /// best used for pivotal action scenes or climactic fight scenes against especially powerful or
    /// recurring Villains. Challenge Level 3 (or greater) should be reserved for battles against
    /// nigh-unstoppable foes and other truly epic scenes…"
    /// </summary>
    public static readonly IReadOnlyList<ChallengeLevelGuidance> ChallengeLevelGuidanceRows =
    [
        new(1, "high-stakes action scenes and ordinary fights against super-powered enemies"),
        new(2, "pivotal action scenes and climactic fights against especially powerful or recurring Villains"),
        new(3, "battles against nigh-unstoppable foes and other truly epic scenes")
    ];

    /// <summary>
    /// "Whenever a Hero performs a cowardly, selfish, treacherous, or otherwise unheroic action,
    /// the GM immediately earns 1 Adversity. The same applies whenever Heroes do anything contrary
    /// to their motivation. This occurs even if they've been coerced, forced, or tricked into
    /// taking such actions."
    /// </summary>
    public const int UnheroicActionAward = 1;
    public const bool UnheroicActionAwardIsImmediate = true;
    public const bool UnheroicActionAlsoWhenContraryToMotivation = true;
    public const bool UnheroicActionAppliesEvenIfCoerced = true;

    public static readonly IReadOnlyList<string> UnheroicActionTriggers =
        ["cowardly", "selfish", "treacherous", "otherwise unheroic"];

    public static readonly IReadOnlyList<string> UnheroicActionCoercionForms =
        ["coerced", "forced", "tricked"];

    /// <summary>
    /// "you can use Adversity to do anything players can do with Resolve, and more. If the rules
    /// say you can use Resolve to do something, you can use Adversity to do the same thing. Plus,
    /// you can spend Adversity on behalf of any NPC whether they're Villains, Foes, Minions, or
    /// Extras. You don't have to worry about sharing because all these characters have access to
    /// the same pool of points. There are also three things you can do with Adversity that Heroes
    /// can't do with Resolve."
    /// </summary>
    public const bool AdversityCanDoAnythingResolveCan = true;
    public const bool AdversityMayBeSpentOnAnyNpc = true;
    public const bool AllNpcsShareOneAdversityPool = true;
    public const int AdversityExclusiveSpendCount = 3;

    public static readonly IReadOnlyList<string> NpcKinds = ["Villains", "Foes", "Minions", "Extras"];

    /// <summary>
    /// "For these characters, Flaws operate more like actual weaknesses, coming into play and
    /// causing problems whenever the opportunity presents itself… GMs can spend 1 Adversity to
    /// prevent a Flaw from getting the better or a Villain, Foe, or Extra for the rest of a scene,
    /// but no character can benefit from this more than once per issue."
    ///
    /// <para><b>Minions are not in the printed list</b>, and the paragraph before it enumerates
    /// four kinds of NPC. Recorded as three, with the gap in <c>ambiguity</c>: a fact field says
    /// the page states the thing, and the page states three.</para>
    /// </summary>
    public const int SuppressFlawCost = 1;
    public const string SuppressFlawPrevents = "a Flaw getting the better of the character";
    public const string SuppressFlawDuration = "the rest of the scene";
    public const int SuppressFlawLimitPerCharacterPerIssue = 1;
    public const bool NpcFlawsBiteWhenTheOpportunityArises = true;
    public const bool NpcsCannotChooseWhenTheirFlawsBite = true;

    public static readonly IReadOnlyList<string> SuppressFlawEligible = ["Villain", "Foe", "Extra"];

    /// <summary>
    /// "You can spend 1 Adversity to throw a misfortune at the Heroes. Misfortunes are random
    /// problems, obstacles, and instances of bad luck that make the Heroes' lives more difficult.
    /// They include things like having a Hero's weapon malfunction or run out of ammo, having a
    /// stray attack cause collateral damage that endangers civilians, having a Hero lose their mask
    /// in a fight… Misfortunes should always be challenges and complications that make things more
    /// interesting and fun. Don't use them as heavy-handed plot devices… or to punish the Heroes
    /// for no reason."
    /// </summary>
    public const int MisfortuneCost = 1;
    public const string MisfortuneIs = "a random problem, obstacle or piece of bad luck aimed at the Heroes";
    public const bool MisfortuneMustBeAChallengeNotAPunishment = true;
    public const bool MisfortuneMustNotBeAPlotDevice = true;

    public static readonly IReadOnlyList<string> MisfortuneExamples =
    [
        "a weapon malfunctions or runs out of ammunition",
        "a stray attack endangers civilians",
        "a Hero loses their mask in a fight"
    ];

    /// <summary>
    /// "Once per story, you can spend 1 Adversity to have a Villain automatically perform an act of
    /// villainy, meaning anything necessary to advance the story. This can be used to have Villains
    /// do things like throw switches, grab hostages, or escape so they can face the Heroes another
    /// time. Use this tool sparingly. … Villainy applies only to Villains; Foes and Minions lack
    /// what it takes to use Adversity in this manner."
    /// </summary>
    public const int VillainyCost = 1;
    public const int VillainyLimitPerStory = 1;
    public const bool VillainyIsAutomatic = true;
    public const string VillainyEffect = "a Villain does whatever the story needs them to do";
    public const bool VillainyUseSparingly = true;

    public static readonly IReadOnlyList<string> VillainyExamples =
        ["throwing a switch", "grabbing hostages", "escaping to face the Heroes another time"];

    public static readonly IReadOnlyList<string> VillainyEligible = ["Villain"];
    public static readonly IReadOnlyList<string> VillainyExcluded = ["Foe", "Minion"];

    // ── The worked example printed under Challenge Level, p.85 ───────────────

    /// <summary>
    /// "For example, in a game with 4 Heroes, a Challenge Level 2 scene drops 8 Adversity into the
    /// GM's evil clutches."
    ///
    /// <para><b>This is the fixture this file exists to support.</b> Everything else here compares
    /// one transcription to another and two transcriptions can agree and both be wrong; a case the
    /// authors worked through in print cannot. It is resolved through the shipped JSON's own
    /// factors and operation, so a lost factor gives 2 and a sum gives 6.</para>
    /// </summary>
    public static class AdversityExample
    {
        public const int Page = 85;
        public const int Heroes = 4;
        public const int ChallengeLevel = 2;
        public const int Adversity = 8;
    }
}
