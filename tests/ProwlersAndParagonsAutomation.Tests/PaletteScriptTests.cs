using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <c>web/wwwroot/js/palette.js</c> is the doorbell, and this is what stops it becoming a
/// keyboard manager.
///
/// <para><b>The file says in as many words to resist growing it, and a comment cannot enforce
/// itself.</b> Its whole job is the one thing a component cannot do — hear a key pressed
/// somewhere else — and it is one listener, two focus calls and one question about the keyboard.
/// Everything else about the palette is decided in <c>Commands</c> and drawn by
/// <c>CommandPalette</c>, where it can be tested without a browser. The pressure this guards
/// against is real and has a name: putting the rulebook behind the chord meant a request, a
/// pause, a race guard and three more kinds of row, and every one of those could have been
/// written here.</para>
///
/// <para><b>A hash rather than a list of things it must not contain.</b> <c>CLAUDE.md</c> records
/// what a denylist of spellings buys — one more spelling and no more: a scan for <c>fetch(</c>
/// is beaten by <c>XMLHttpRequest</c>, one for both is beaten by <c>navigator.sendBeacon</c>, and
/// the spelling space is unbounded. A digest cannot be walked through in any spelling, and
/// changing this file deliberately costs one line in this test, in the same commit, which is
/// exactly the deliberation the file's own comment is asking for.</para>
///
/// <para><b>Line endings are normalised first</b>, so a checkout that converted them fails a
/// build somewhere else rather than failing here for a reason nobody can act on.</para>
/// </summary>
public sealed class PaletteScriptTests
{
    private static string Doorbell => Path.Combine(
        RulesFixture.RepoRoot, "web", "wwwroot", "js", "palette.js");

    /// <summary>
    /// The SHA-256 of the shipped file with its line endings normalised to <c>\n</c>.
    ///
    /// <para><b>To change <c>palette.js</c>: change it, run this test, and paste the digest it
    /// prints into this constant in the same commit.</b> That is the whole ceremony, and it is the
    /// point — a change to the doorbell should be a thing somebody decided rather than a thing
    /// that happened while they were adding a feature to the palette.</para>
    /// </summary>
    private const string AsShipped =
        "5b8bae631085e416c9d7e0964d4cd372a49abc9bef80609b2623862600e7efb3";

    /// <summary>How long the file was when the digest above was taken. Reported, never asserted.</summary>
    private const int LinesAsShipped = 96;

    [Fact]
    public void TheDoorbellHasNotGrown()
    {
        var source = File.ReadAllText(Doorbell).Replace("\r\n", "\n", StringComparison.Ordinal);

        var digest = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(source)));

        var lines = source.Split('\n').Length;

        Assert.True(
            digest == AsShipped,
            $"web/wwwroot/js/palette.js has changed. It is {lines} lines now and was "
            + LinesAsShipped.ToString(CultureInfo.InvariantCulture) + " when this digest was taken.\n"
            + "\n"
            + "That file is the doorbell: one listener, two focus calls and one question about "
            + "the keyboard, and its own header says to resist growing it. Everything else about "
            + "the palette — what it offers, what it looks like, what the keys do once focus is "
            + "inside it — belongs in web/Services/Commands.cs and web/Components/CommandPalette."
            + "razor, where it can be tested without a browser.\n"
            + "\n"
            + "If the change is deliberate, put this digest in AsShipped and the line count in "
            + $"LinesAsShipped, in the same commit as the change:\n  {digest}\n  {lines}");

        // The positive control on the assertion above. A digest comparison is satisfied trivially
        // by a file that could not be read at all if the failure were swallowed, and by an empty
        // one if the constant were ever regenerated from nothing — so this asserts the file really
        // is the doorbell rather than merely being the bytes it was.
        Assert.Contains("document.addEventListener", source, StringComparison.Ordinal);
        Assert.Contains("window.ppPalette", source, StringComparison.Ordinal);
    }
}
