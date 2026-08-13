using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Reads the recorded conversations in <c>data/transcripts</c>.
///
/// <para>Two decisions here are worth the words. The first is that the character inside a
/// transcript is read by <see cref="CharacterSheetJson"/> <b>strictly</b> — a field name that
/// is no longer part of a character is refused rather than ignored. These files are data that
/// nothing recompiles, so the way they rot is a rename: read leniently, a transcript whose
/// <c>AbilityRanks</c> had been renamed would replay a cheaper character with its abilities
/// silently gone, which is the failure the strict reader exists for on every other submit
/// path. The second is that the whole file is written in the character's own field names
/// rather than the <c>snake_case</c> of <c>data/rules</c>: a transcript is mostly a character,
/// and one convention per file beats two.</para>
/// </summary>
public static class TranscriptLibrary
{
    /// <summary>
    /// Every transcript file, named as it appears in <c>data/transcripts</c>.
    ///
    /// <para>The same contract as <c>RulesRepository.DataFileNames</c> and for the same
    /// reason: a browser cannot glob a directory it has no filesystem for, so a host that
    /// loads these itself has to be told what to fetch. A test checks this list against the
    /// directory, because a file added and not listed is a transcript that exists everywhere
    /// except in the app.</para>
    /// </summary>
    public static IReadOnlyList<string> FileNames { get; } =
    [
        "vera-nunn.json",
        "chrono-jab.json",
        "sheet-lightning.json",
        "the-conductor.json"
    ];

    private static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,

        // A transcript field that is not part of a transcript is a mistake in a file nothing
        // compiles. Refusing it turns that into a failure at load, which is where it can be
        // seen, rather than a section quietly missing from a recording.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>
    /// The transcripts, in <see cref="FileNames"/> order, from a set of file contents keyed by
    /// file name. The engine never opens a file; whoever has one hands over the text.
    /// </summary>
    /// <exception cref="InvalidOperationException">A named file is missing, or one of them is
    /// not a transcript this build can read.</exception>
    public static IReadOnlyList<Transcript> ReadAll(IReadOnlyDictionary<string, string> filesByName)
    {
        ArgumentNullException.ThrowIfNull(filesByName);

        return [.. FileNames.Select(name =>
            filesByName.TryGetValue(name, out var json)
                ? Read(json, name)
                : throw new InvalidOperationException($"The transcript '{name}' was not supplied."))];
    }

    /// <summary>
    /// One transcript. Private because <see cref="ReadAll"/> is the only way in: the file
    /// names are this class's business, and a caller that could read one file by name would
    /// be a second place that knows what is in the directory.
    /// </summary>
    /// <param name="json">The file's contents.</param>
    /// <param name="fileName">Named in any error, so a failure says which file to open.</param>
    /// <exception cref="InvalidOperationException">The text is not a transcript this build can
    /// read — malformed, missing a required part, or naming a field a character no longer has.
    /// </exception>
    private static Transcript Read(string json, string fileName)
    {
        Envelope envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<Envelope>(json, Options)
                       ?? throw new InvalidOperationException($"'{fileName}' is empty.");
        }
        catch (JsonException e)
        {
            throw new InvalidOperationException($"'{fileName}' is not a transcript: {e.Message}", e);
        }

        if (string.IsNullOrWhiteSpace(envelope.Id))
            throw new InvalidOperationException($"'{fileName}' has no id.");

        return new Transcript
        {
            Id = envelope.Id,
            Title = envelope.Title ?? "",
            Blurb = envelope.Blurb ?? "",
            Villain = envelope.Villain,
            Turns = [.. (envelope.Turns ?? []).Select((turn, i) => ReadTurn(turn, fileName, i))]
        };
    }

    private static TranscriptTurn ReadTurn(TurnEnvelope turn, string fileName, int index)
    {
        var where = $"'{fileName}' turn {index + 1}";

        if (turn.Text is null)
            throw new InvalidOperationException($"{where} has no text.");

        // Spelled out rather than left to a string-enum converter, which would read a missing
        // speaker as the first member of the enum. A turn attributed to the wrong side is the
        // one error in this file that produces something plausible: the recording still plays,
        // with the assistant apparently describing the character to itself.
        var speaker = turn.Speaker switch
        {
            "person" => TranscriptSpeaker.Person,
            "assistant" => TranscriptSpeaker.Assistant,
            null => throw new InvalidOperationException($"{where} does not say who spoke."),
            _ => throw new InvalidOperationException(
                $"{where} is attributed to '{turn.Speaker}', which is neither 'person' nor 'assistant'.")
        };

        return new TranscriptTurn
        {
            Speaker = speaker,
            Text = turn.Text,
            Character = turn.Character is { } element ? ReadCharacter(element, where) : null
        };
    }

    /// <summary>
    /// The character, through the same strict reader a submitted file goes through. Its
    /// <see cref="JsonException"/> is turned into something that names the transcript: the
    /// message a deserializer gives names a path inside a document, and on its own that is
    /// not enough to find which of four files to open.
    /// </summary>
    private static CharacterSheet ReadCharacter(JsonElement element, string where)
    {
        try
        {
            return CharacterSheetJson.Read(element.GetRawText(), strict: true)
                   ?? throw new InvalidOperationException($"{where} has an empty character.");
        }
        catch (JsonException e)
        {
            throw new InvalidOperationException($"{where} is not a character: {e.Message}", e);
        }
    }

    // The shape on disk. Separate from Transcript because the character is held as raw JSON
    // here and handed to CharacterSheetJson, rather than being deserialized by the options
    // above — which know nothing about the get-only collections a CharacterSheet exposes.
    private sealed record Envelope
    {
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Blurb { get; init; }
        public bool Villain { get; init; }
        public IReadOnlyList<TurnEnvelope>? Turns { get; init; }
    }

    private sealed record TurnEnvelope
    {
        public string? Speaker { get; init; }
        public string? Text { get; init; }
        public JsonElement? Character { get; init; }
    }
}
