// Written from the shipped data/rules/play JSON. Every key of every
// entry in the file this covers has a property here, because PlayRulesFileCoverageTests
// deserializes the file with JsonUnmappedMemberHandling.Disallow: a field nothing reads is a
// field nothing can hold to the rulebook, which is the failure creation_rules.json shipped.
//
// Prose, `ambiguity` and `interpretation` are modelled too. They are part of the file, and a
// reader of an entry needs to see the ambiguity beside the number. What the engine may *consume*
// is narrower — see docs/guide/play-engine.md — and where it consumes an interpretation it says
// so in a doc comment naming the entry.

namespace ProwlersAndParagonsAutomation.Play.Rules.Models;

public sealed record MetaSubOneDieModel(
    int DiceRolled,
    IReadOnlyList<int> CountingFaces,
    int SuccessesWhenHit,
    string Otherwise
);

public sealed record MetaAutomaticSuccessesModel(
    int DicePerSuccess,
    bool GmMayVeto
);

public sealed record MetaExceptionModel(
    string Name,
    string Direction,
    string WhatItGoverns,
    string Reference,
    string Note
);

public sealed record MetaRoundingModel(
    string Direction,
    string Scope,
    IReadOnlyList<string> Examples,
    IReadOnlyList<MetaExceptionModel> Exceptions
);

public sealed record PlayMetaEntry(
    string Id,
    string Name,
    string Kind,
    int? DieSides,
    string? PoolFormula,
    string Description,
    IReadOnlyList<string> VerifiedFields,
    string SourceRef,
    IReadOnlyDictionary<string, int>? SuccessMap,
    IReadOnlyList<string>? CorroboratedBy,
    MetaSubOneDieModel? SubOneDie,
    string? Ambiguity,
    MetaAutomaticSuccessesModel? AutomaticSuccesses,
    string? NetSuccessFormula,
    MetaRoundingModel? Rounding
);
