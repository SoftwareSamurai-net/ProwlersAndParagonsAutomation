namespace ProwlersAndParagonsAutomation.Engine.Models;

/// <summary>
/// A generic option a Power's own printed text tells you to apply, overriding the Range rule
/// the option itself states. See <see cref="PowerModel.ProsAllowedByOwnText"/> for why this
/// exists and why it is not the removed <c>available_pros</c> list.
/// </summary>
public record ProAllowanceModel
{
    public string Id { get; init; } = "";

    /// <summary>
    /// Which of the option's printed grades this Power may pick, when the option is priced by
    /// grade and the Power's Range is not one the rulebook prices.
    ///
    /// <para><b>Empty means every grade the option prices, and that is the wrong answer where
    /// the grades encode a Range.</b> Zone/Nova costs +2 or +1 on a Ranged Power and +4 or +2
    /// on a Touch one, and Force Field is Self, which the rulebook does not price at all — so
    /// with nothing here, T-Kay's printed Force Field 12d (Zone) was accepted at +2 with
    /// <c>zone_ranged</c> and at +4 with <c>zone_touch</c>, no finding either way, and the
    /// same printed character costed two ways depending on which key was typed.</para>
    /// </summary>
    public IReadOnlyList<string> Grades { get; init; } = [];

    /// <summary>
    /// The Power's own printed sentence that allows this, with its page. Required, and
    /// asserted non-empty by a test: the standard for adding an entry here is that the
    /// rulebook says so, and a standard nothing checks is documentation rather than a rule.
    /// It was a free-text <c>notes</c> string on the Power at first, which no code read and
    /// no test asserted.
    /// </summary>
    public string Reason { get; init; } = "";
}
