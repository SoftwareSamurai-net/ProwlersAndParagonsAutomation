namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>Who said a line in a recorded conversation.</summary>
public enum TranscriptSpeaker
{
    /// <summary>The person describing a character in ordinary words.</summary>
    Person,

    /// <summary>The assistant they were describing it to.</summary>
    Assistant
}

/// <summary>
/// One line of a recorded conversation, and — when the assistant submitted a character at
/// that point — the character it submitted.
///
/// <para><b>A turn carries a character, never an answer about one.</b> That is the whole
/// discipline of this file. The recorded prose says what was said; every Hero Point figure,
/// every derived stat and the word "legal" come from running <see cref="Character"/> through
/// the engine wherever the transcript is being shown. A draft that was over budget is stored
/// as the draft, not as the number it came back with — so if the rules change under it, the
/// replay reports the new answer instead of quietly repeating the old one.</para>
/// </summary>
public sealed record TranscriptTurn
{
    /// <summary>Who spoke.</summary>
    public TranscriptSpeaker Speaker { get; init; }

    /// <summary>What they said.</summary>
    public string Text { get; init; } = "";

    /// <summary>
    /// The character the assistant put to the engine at this point in the conversation, or
    /// null for a turn that was only talk.
    ///
    /// <para>Several turns of one transcript may carry one: a first draft that did not fit,
    /// then the character that was settled on. Re-costing each in order is what shows the
    /// judgement happening rather than being reported.</para>
    /// </summary>
    public CharacterSheet? Character { get; init; }
}

/// <summary>
/// A conversation that actually happened, kept so it can be replayed to somebody who has no
/// way to hold one of their own.
///
/// <para>The transcripts were produced by driving the real server with the real rules, not
/// written as dialogue, and every character in one is a character the engine priced. What is
/// recorded is the <em>inputs</em> — the same shape <see cref="CharacterSheetJson"/> reads
/// and the browser stores — so nothing here can disagree with the engine about a number,
/// because nothing here holds one.</para>
/// </summary>
public sealed record Transcript
{
    /// <summary>The id in the address of the page that plays it. Lower case, hyphenated.</summary>
    public string Id { get; init; } = "";

    /// <summary>A short name for the conversation, for the list of them.</summary>
    public string Title { get; init; } = "";

    /// <summary>One sentence on what this conversation shows that the others do not.</summary>
    public string Blurb { get; init; } = "";

    /// <summary>
    /// Whether the character was built as a Villain.
    ///
    /// <para>It is a property of the <em>conversation</em> and not of the character, which is
    /// why it sits here and not on <see cref="CharacterSheet"/>. Ch.9 builds Villains by
    /// exactly the Hero rules and the engine is never told which it is looking at; all this
    /// does is set the palette the replay wears, and say that a budget finding is the GM's
    /// call rather than a rule broken.</para>
    /// </summary>
    public bool Villain { get; init; }

    /// <summary>The conversation, in order.</summary>
    public IReadOnlyList<TranscriptTurn> Turns { get; init; } = [];

    /// <summary>
    /// The character the conversation arrived at — the last one submitted — or null if it
    /// never got that far.
    /// </summary>
    public CharacterSheet? FinalCharacter =>
        Turns.LastOrDefault(t => t.Character is not null)?.Character;
}
