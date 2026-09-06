namespace ProwlersAndParagonsAutomation.Play.Rules;

/// <summary>
/// Halving, in the direction the data names — the one place in <c>play/</c> that turns "half of
/// this" into a number.
///
/// <para><b>There is no <c>Math.Ceiling</c> anywhere else in this project, and that is the point.</b>
/// The book states its rounding convention once, in the Introduction's Glossary (p.7), and
/// <c>play_meta.json</c>'s <c>half_rounds_up</c> transcribes it — direction, scope, and the single
/// printed exception. A <c>Math.Ceiling(x / 2.0)</c> typed beside a rule is a second transcription
/// of that convention: it agrees with the file exactly until somebody corrects the file, and then it
/// does not, silently. Four halvings in the engine were spelled that way — the Toughness that
/// answers a lethal attack, the defences going all-out costs, the defences a charge costs, and a
/// Foe's Health — and none of them would have moved if the Glossary entry had.</para>
///
/// <para><b>Where an entry names its own direction, that direction wins</b>, which is the other
/// overload. Two entries carry one: <c>special_effects.interpretation.duration_rounds</c> and
/// <c>breaking_free.interpretation.reduction_rounds</c>, both this repository's readings of a page
/// that halves without saying which way. <c>gritty_tough_minions</c> is the book's own printed
/// exception and reads its <c>rounding</c> field the same way.</para>
/// </summary>
public static class Rounding
{
    /// <summary>
    /// Half of <paramref name="value"/>, rounded the way the Glossary's book-wide rule rounds.
    /// </summary>
    public static int Half(PlayRulesRepository play, int value)
    {
        ArgumentNullException.ThrowIfNull(play);

        return Half(value, play.GetMeta("half_rounds_up").Rounding!.Direction);
    }

    /// <summary>
    /// Half of <paramref name="value"/>, in the direction <paramref name="direction"/> names — a
    /// throw on anything but <c>up</c> or <c>down</c>, because a third word would be a rule this
    /// engine cannot apply and a default would apply the wrong one quietly.
    /// </summary>
    public static int Half(int value, string direction) => direction switch
    {
        "up" => (int)Math.Ceiling(value / 2.0),
        "down" => value / 2,
        var other => throw new InvalidOperationException(
            $"'{other}' is neither up nor down, so this engine cannot halve with it.")
    };
}
