namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The flaw_type printed for every one of the 53 Flaws in Prowlers &amp; Paragons Ultimate
/// Edition, Chapter 2 (pp.56-60), transcribed from the rulebook's own prose rather than
/// from data/rules/flaws.json.
///
/// <para><b>This field has teeth.</b> <see cref="Engine.DerivedStatsCalculator.CalculateResolve"/>
/// grants +1 Resolve per Condition or Plot Hook flaw a character carries — so a flaw retyped
/// from "regular" to "condition" (or vice versa) silently changes every character built with
/// it, and nothing before this file pinned 49 of the 53 values to the book. See
/// <see cref="FlawTypeTests"/> for the coverage assertion and the Resolve consequence check.</para>
///
/// <para>The rulebook states the type inside most Flaws' own entries — "This Flaw is a Plot
/// Hook that grants you 1 extra point of Resolve..." or "...is a Condition that grants...".
/// Two, Amnesia and (by the shared Unlucky/Jinx entry) Jinx, are stated by cross-reference to
/// a paired or preceding entry rather than restated in full; those are noted below. Where an
/// entry says neither, the Flaw is <c>regular</c> — the general rule from the FLAWS section
/// intro (p.55): a Hero brings a Flaw into play to earn Resolve, up to once per scene, unless
/// it is called out as one of the two exceptions.</para>
///
/// <para>Five printed headings pair two Flaws under one entry — Compulsion/Severe Compulsion,
/// Heavy/Very Heavy, Reaction/Severe Reaction, Requirement/Severe Requirement, Unlucky/Jinx —
/// and data/rules/flaws.json stores the two halves separately, which is why there are 53
/// entries here for 48 printed headings (RulesDataTests.ThereAreSix53Flaws already covers the
/// count). Each half's type is transcribed from its own sentence in the shared entry.</para>
/// </summary>
public static class CanonicalFlawTypes
{
    public sealed record Entry(string Id, string FlawType, int Page, string Evidence);

    public static readonly IReadOnlyList<Entry> All =
    [
        new("absentminded", "regular", 56, "No Plot Hook or Condition sentence; ordinary Resolve rule applies."),
        new("alter_ego", "regular", 56, "No Plot Hook or Condition sentence."),
        new("amnesia", "plot_hook_and_condition", 56, "\"This Flaw is both a Plot Hook and a Condition, so it grants you 1 extra point of Resolve at the start of every issue.\""),
        new("aversion_fear", "regular", 56, "No Plot Hook or Condition sentence."),
        new("beast", "regular", 56, "No Plot Hook or Condition sentence."),
        new("blind_deaf", "condition", 56, "\"This Flaw is a Condition that grants you 1 extra point of Resolve at the start of every issue.\""),
        new("broke", "regular", 56, "No Plot Hook or Condition sentence."),
        new("clumsy", "regular", 56, "No Plot Hook or Condition sentence."),
        new("code", "regular", 57, "No Plot Hook or Condition sentence."),
        new("color_blind", "regular", 57, "No Plot Hook or Condition sentence."),
        new("compulsion", "regular", 57, "\"You earn a point of Resolve any time this works to your detriment.\" — the base entry; only the Severe variant is a Condition."),
        new("severe_compulsion", "condition", 57, "\"...then you have a Severe Compulsion, which is a Condition that grants you 1 extra point of Resolve at the start of every issue.\""),
        new("creepy", "regular", 57, "No Plot Hook or Condition sentence."),
        new("curse", "regular", 57, "No Plot Hook or Condition sentence."),
        new("decorum", "regular", 57, "No Plot Hook or Condition sentence."),
        new("disabled", "condition", 57, "\"This Flaw is a Condition that grants you 1 extra point of Resolve at the start of every issue.\""),
        new("emotionless", "regular", 57, "No Plot Hook or Condition sentence."),
        new("enemy", "plot_hook", 57, "\"This Flaw is a Plot Hook that grants you 1 extra point of Resolve at the start of every issue.\""),
        new("finite_power", "regular", 57, "No Plot Hook or Condition sentence."),
        new("flashbacks_guilt", "regular", 58, "No Plot Hook or Condition sentence."),
        new("frenzy", "regular", 58, "No Plot Hook or Condition sentence."),
        new("frightening", "regular", 58, "No Plot Hook or Condition sentence."),
        new("heavy", "regular", 58, "\"You earn a point of Resolve whenever your weight becomes an issue.\" — the base entry (5d-6d weight rank); only Very Heavy (7d+) is a Condition."),
        new("very_heavy", "condition", 58, "\"At this level, you have a Condition that grants you 1 extra point of Resolve at the start of every issue.\""),
        new("hidden_agenda", "regular", 58, "No Plot Hook or Condition sentence."),
        new("illiterate", "regular", 58, "No Plot Hook or Condition sentence."),
        new("impaired_sense", "regular", 58, "No Plot Hook or Condition sentence."),
        new("insane", "regular", 58, "No Plot Hook or Condition sentence."),
        new("light_sensitive", "regular", 58, "No Plot Hook or Condition sentence."),
        new("mute", "regular", 58, "No Plot Hook or Condition sentence."),
        new("night_blind", "regular", 59, "No Plot Hook or Condition sentence."),
        new("nocturnal", "regular", 59, "No Plot Hook or Condition sentence."),
        new("notoriety", "regular", 59, "No Plot Hook or Condition sentence."),
        new("obligation", "plot_hook", 59, "\"This Flaw is a Plot Hook that grants you 1 extra point of Resolve at the start of every issue.\""),
        new("outsider", "regular", 59, "No Plot Hook or Condition sentence."),
        new("power_limits", "regular", 59, "No Plot Hook or Condition sentence — \"this Flaw lets you earn 1 point of Resolve whenever...\"."),
        new("quirk", "regular", 59, "No Plot Hook or Condition sentence."),
        new("reaction", "regular", 59, "\"...you earn a point of Resolve whenever you suffer a reaction...\" — the base entry; only the Severe variant is a Condition."),
        new("severe_reaction", "condition", 59, "\"...then you have a Severe Reaction, which is a Condition that grants you 1 extra point of Resolve at the start of every issue.\""),
        new("relationship", "plot_hook", 59, "\"This common Flaw is a Plot Hook that grants you 1 extra point of Resolve at the start of every issue.\""),
        new("repair", "condition", 59, "\"This Flaw is a Condition that grants you 1 extra point of Resolve at the start of every issue.\""),
        new("requirement", "regular", 59, "\"You earn a point of Resolve whenever you do something selfish...\" — the base entry; only the Severe variant is a Condition."),
        new("severe_requirement", "condition", 59, "\"...then you have a Severe Requirement, which is a Condition that grants you 1 extra point of Resolve at the start of every issue.\""),
        new("restriction", "regular", 59, "No Plot Hook or Condition sentence."),
        new("secret", "regular", 59, "No Plot Hook or Condition sentence."),
        new("secret_identity", "regular", 60, "No Plot Hook or Condition sentence — the entry is a one-line cross-reference to Secret."),
        new("slow", "regular", 60, "No Plot Hook or Condition sentence."),
        new("unlucky", "condition", 60, "\"This Flaw is a Condition that grants you 1 extra point of Resolve at the start of every issue.\" — stated once for the shared Unlucky/Jinx entry."),
        new("jinx", "condition", 60, "\"Jinx works the same way [as Unlucky]...\" under the same Condition sentence as Unlucky, in the shared Unlucky/Jinx entry."),
        new("unusual_looks", "regular", 60, "No Plot Hook or Condition sentence."),
        new("unusual_shape", "regular", 60, "No Plot Hook or Condition sentence."),
        new("vulnerability", "condition", 60, "\"This Flaw is a Condition that grants you 1 extra point of Resolve at the start of every issue.\""),
        new("wanted", "plot_hook", 60, "\"This Flaw is a Plot Hook that grants you 1 extra point of Resolve at the start of every issue.\""),
    ];
}
