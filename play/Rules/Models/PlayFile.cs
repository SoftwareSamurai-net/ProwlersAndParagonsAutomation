namespace ProwlersAndParagonsAutomation.Play.Rules.Models;

/// <summary>
/// The header every <c>data/rules/play</c> file opens with: what the file is, why it sits in a
/// subdirectory, the closed <c>verified_fields</c> vocabulary, and what was deliberately left out.
///
/// <para>None of it is a mechanic and none of it is consumed by <c>Encounter</c>. It is
/// modelled because the strict coverage test reads the whole file, and because
/// <c>PlayRulesDataTests</c> holds the header to saying these things.</para>
/// </summary>
public sealed record PlayFileHeader(
    string WhatThisIs,
    string PlacementNote,
    string NotLogic,
    string? DeliberatelyOmitted,
    IReadOnlyList<string> VerifiedFieldsClosedList,
    string SourceRef,
    int? SpecialCasesCount);

/// <summary>One <c>data/rules/play</c> file: its header and its entries.</summary>
public sealed record PlayFile<TEntry>(PlayFileHeader Header, IReadOnlyList<TEntry> Entries);
