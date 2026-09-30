namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// A strict, display-only reading of <c>data/rules/play/resolve.json</c> — the one play rules
/// file <c>web/</c> is permitted to read at all, and only for this. See
/// <see cref="ResolveReferenceReader"/> and <c>docs/guide/play-rules.md</c>.
///
/// <para><b>Every property here is modelled, not extracted.</b> The reader deserializes with
/// <c>JsonUnmappedMemberHandling.Disallow</c>, so a field the file gains that none of these
/// records name throws rather than silently vanishing — the whole point of a display-only
/// reader for data nothing else in this application is allowed to hold an opinion about. There
/// is deliberately no <c>JsonExtensionData</c> catch-all anywhere below: that would let the file
/// drift out from under these types with nobody told.</para>
///
/// <para>None of this computes anything. A cost, a rank or a formula that this file states is
/// printed on the page as the file's own words; nothing here adds it to a character or resolves
/// an action. That is <c>play/</c>'s job, on a copy of this same file it reads for itself.</para>
/// </summary>
public sealed record ResolveDocument
{
    public required ResolveHeader Header { get; init; }
    public required IReadOnlyList<ResolveEntry> Entries { get; init; }
}

public sealed record ResolveHeader
{
    public string WhatThisIs { get; init; } = "";
    public string PlacementNote { get; init; } = "";
    public string NotLogic { get; init; } = "";
    public string DeliberatelyOmitted { get; init; } = "";
    public IReadOnlyList<string> VerifiedFieldsClosedList { get; init; } = [];
    public string SourceRef { get; init; } = "";
}

/// <summary>
/// This repository's own reading, kept out of the transcribed fields on purpose — see
/// <c>docs/guide/play-rules.md</c>'s "a reading of the page is not a transcription of it". The
/// two extra fields are each carried by exactly one entry today; both are nullable because
/// nothing requires an entry with an interpretation to carry either.
/// </summary>
public sealed record ResolveInterpretation
{
    public string WhatThisIs { get; init; } = "";
    public IReadOnlyList<string>? MechanisableEntryIds { get; init; }
    public int? InferredCostPerPointShared { get; init; }
}

public sealed record ResolveTableRow
{
    public int RanksBelowTraitCap { get; init; }
    public int Resolve { get; init; }
}

public sealed record StartingResolve
{
    public string GrantedAt { get; init; } = "";
    public bool IssueIsAGameSession { get; init; }
    public string DependsOn { get; init; } = "";
    public string MeasuredFrom { get; init; } = "";
    public int AtTraitCap { get; init; }
    public int ResolvePerRankBelowCap { get; init; }
    public string Formula { get; init; } = "";
    public IReadOnlyList<ResolveTableRow> TableRows { get; init; } = [];
    public bool TableContinuesBeyondThePrintedRows { get; init; }
}

public sealed record ResolveExceptionsData
{
    public bool TalentsCount { get; init; }
    public string Criterion { get; init; } = "";
    public IReadOnlyList<string> NamedPowers { get; init; } = [];
    public string ExpertiseQualifier { get; init; } = "";
    public bool NamedListIsQualifiedAsUsual { get; init; }
    public bool GmHasFinalSay { get; init; }
}

public sealed record MaximumPossibleRank
{
    public string AppliesWhen { get; init; } = "";
    public IReadOnlyList<string> PowersNamed { get; init; } = [];
    public string RankUsed { get; init; } = "";
}

public sealed record ResolveCarryover
{
    public bool CarriesOverBetweenIssues { get; init; }
    public bool UnspentIsLostAtIssueEnd { get; init; }
    public bool GmMayAllowCarryover { get; init; }
    public bool GmCarryoverShouldBeRare { get; init; }
}

public sealed record EarningOverview
{
    public bool GmMayAwardWheneverTheySeeFit { get; init; }
    public bool ListedWaysAreExamplesNotAClosedList { get; init; }
}

/// <summary>
/// The six shapes of "how a Hero earns a point", under one type because the JSON key
/// (<c>earning</c>) is one key across all six entries. Every field is nullable: which ones an
/// entry carries is exactly what tells the six apart, and none of that is guessed here.
/// </summary>
public sealed record Earning
{
    public int? AwardResolve { get; init; }
    public string? Trigger { get; init; }
    public bool? MayBeOutsideCombat { get; init; }
    public int? LimitPerBattle { get; init; }
    public bool? MustFitTheSituation { get; init; }
    public int? SomeFlawsAwardPerIssueInstead { get; init; }
    public bool? AwardStated { get; init; }
    public string? InterludeIs { get; init; }
    public string? DetailChapter { get; init; }
    public bool? NotAnInvitationToDerailTheGame { get; init; }
    public bool? GmJudged { get; init; }
    public IReadOnlyList<string>? ExamplesGiven { get; init; }
    public string? AvailableWhen { get; init; }
    public bool? MustBeSpentOnTheSamePage { get; init; }
    public bool? ThenUnconscious { get; init; }
    public string? UnconsciousUntil { get; init; }
}

public sealed record SpendingOverview
{
    public bool GmMayExpandTheUses { get; init; }
    public bool ListedUsesAreTheBasicOnes { get; init; }
}

/// <summary>
/// Every shape of "what a point buys", Hero and GM alike, under one type for the same reason
/// <see cref="Earning"/> is: one JSON key, twelve differently-shaped entries. Nullable
/// throughout, on purpose.
/// </summary>
public sealed record Spend
{
    public string Currency { get; init; } = "";

    // spend_assisting_allies
    public int? CostPerPointSharedWhenUnableToAssist { get; init; }
    public string? PointsSharedLimit { get; init; }
    public bool? MustNarrateTheAssistance { get; init; }
    public bool? NarrationHasNoMechanicalEffect { get; init; }
    public IReadOnlyList<string>? UnableToAssistExamples { get; init; }
    public bool? FlashbackRequiredWhenUnable { get; init; }

    // spend_challenge_roll_dice / spend_reroll_challenge_roll / spend_lucky_break / spend_power_stunt
    public int? CostResolve { get; init; }
    public int? DiceGained { get; init; }
    public bool? Unlimited { get; init; }
    public bool? DecidedAfterTheRoll { get; init; }
    public string? Rerolls { get; init; }
    public bool? IncludesDiceBoughtWithResolve { get; init; }

    // spend_reroll_other_roll
    public string? AppliesTo { get; init; }
    public string? ExampleGiven { get; init; }

    // spend_combat
    public bool? TranscribedHere { get; init; }
    public string? DetailChapter { get; init; }
    public IReadOnlyList<string>? CombatSpendRefs { get; init; }

    // spend_lucky_break
    public string? Invents { get; init; }
    public bool? SubjectToGmApproval { get; init; }

    // spend_power_stunt
    public string? Uses { get; init; }
    public string? ImitatedPowerRankSource { get; init; }
    public bool? RequiresRemotelyReasonable { get; init; }
    public bool? GrantsANewPower { get; init; }

    // spend_using_powers
    public bool? SomePowersRequireResolve { get; init; }
    public bool? OnlyHeroesHaveResolve { get; init; }
    public bool? PlayerMustSpendForAFriendlyExtra { get; init; }
    public bool? ExtraCannotUseThePowerIfNobodySpends { get; init; }
    public bool? AppliesOnlyWhileTheExtraIsWithTheHeroes { get; init; }

    // adversity_spend_anything_resolve_can
    public bool? CanDoAnythingResolveCan { get; init; }
    public bool? MayBeSpentOnAnyNpc { get; init; }
    public IReadOnlyList<string>? NpcKinds { get; init; }
    public bool? AllNpcsShareOnePool { get; init; }
    public int? ExclusiveSpendsCount { get; init; }

    // adversity_spend_suppress_flaw
    public int? CostAdversity { get; init; }
    public string? Prevents { get; init; }
    public string? Duration { get; init; }
    public IReadOnlyList<string>? EligibleCharacters { get; init; }
    public int? LimitPerCharacterPerIssue { get; init; }
    public bool? NpcFlawsBiteWhenTheOpportunityArises { get; init; }
    public bool? NpcsCannotChooseWhenTheirFlawsBite { get; init; }

    // adversity_spend_misfortune
    public string? WhatItIs { get; init; }
    public IReadOnlyList<string>? ExamplesGiven { get; init; }
    public bool? MustBeAChallengeNotAPunishment { get; init; }
    public bool? MustNotBeAPlotDevice { get; init; }

    // adversity_spend_villainy
    public int? LimitPerStory { get; init; }
    public bool? Automatic { get; init; }
    public string? Effect { get; init; }
    public IReadOnlyList<string>? ExcludedCharacters { get; init; }
    public bool? UseSparingly { get; init; }
}

public sealed record RerollFloor
{
    public bool SpendingShouldNeverMakeThingsWorse { get; init; }
    public bool KeepTheFirstRollIfTheRerollIsWorse { get; init; }
    public string AppliesTo { get; init; } = "";
}

public sealed record AdversityPool
{
    public int PointsPerHeroPerIssue { get; init; }
    public string HeldBy { get; init; } = "";
    public bool CarriesOverBetweenIssues { get; init; }
    public bool IsMoreOfAFixedResourceThanResolve { get; init; }
    public bool GmMayAddWaysToEarn { get; init; }
    public bool ShouldNotBeAsEasyToEarnAsResolve { get; init; }
}

public sealed record ChallengeLevelGuidance
{
    public int Level { get; init; }
    public string UsedFor { get; init; } = "";
}

public sealed record ChallengeLevel
{
    public IReadOnlyList<string> AwardFactors { get; init; } = [];
    public string AwardOperation { get; init; } = "";
    public string AwardedAt { get; init; } = "";
    public int TypicalLevelMin { get; init; }
    public int TypicalLevelMax { get; init; }
    public bool LevelThreeMayBeExceeded { get; init; }
    public bool OnlyAHandfulOfScenesPerStory { get; init; }
    public bool MayBeSavedForLaterInTheIssue { get; init; }
    public IReadOnlyList<ChallengeLevelGuidance> LevelGuidance { get; init; } = [];
}

public sealed record UnheroicAction
{
    public int AwardAdversity { get; init; }
    public bool AwardedImmediately { get; init; }
    public IReadOnlyList<string> Triggers { get; init; } = [];
    public bool AlsoWhenContraryToMotivation { get; init; }
    public bool AppliesEvenIfCoerced { get; init; }
    public IReadOnlyList<string> CoercionForms { get; init; } = [];
}

/// <summary>
/// One entry. The common fields are always there; every kind-specific object below is null on
/// every entry that does not carry that JSON key — which is most of them, since each entry has
/// exactly one.
/// </summary>
public sealed record ResolveEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string PrintedUnder { get; init; } = "";
    public string? Who { get; init; }
    public string Description { get; init; } = "";
    public string Summary { get; init; } = "";
    public IReadOnlyList<string> VerifiedFields { get; init; } = [];
    public string SourceRef { get; init; } = "";
    public IReadOnlyList<string>? CorroboratedBy { get; init; }
    public string? Ambiguity { get; init; }
    public ResolveInterpretation? Interpretation { get; init; }

    public StartingResolve? StartingResolve { get; init; }
    public ResolveExceptionsData? Exceptions { get; init; }
    public MaximumPossibleRank? MaximumPossibleRank { get; init; }
    public ResolveCarryover? Carryover { get; init; }
    public EarningOverview? EarningOverview { get; init; }
    public Earning? Earning { get; init; }
    public SpendingOverview? SpendingOverview { get; init; }
    public Spend? Spend { get; init; }
    public RerollFloor? RerollFloor { get; init; }
    public AdversityPool? Adversity { get; init; }
    public ChallengeLevel? ChallengeLevel { get; init; }
    public UnheroicAction? UnheroicAction { get; init; }
}
