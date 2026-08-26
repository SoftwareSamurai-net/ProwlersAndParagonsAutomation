using System.Text;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Mcp;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Scores <see cref="CharacterTools.SearchPowers"/> against <see cref="PowerSearchExpectations"/>,
/// the labelled set PROGRESS.md item 4 calls for before the tie-ordering can be fixed.
///
/// <para><b>Two tests, two different jobs, and the split is the point.</b>
/// <see cref="ReportTheCurrentScore"/> is a measurement: it runs the whole set against
/// whatever <see cref="CharacterTools.SearchPowers"/> does today, prints a table, and never
/// fails — the set was written knowing the current search does not satisfy all of it, so
/// asserting the count here would just be a test that starts red and stays red.
/// <see cref="TheScoreNeverGetsWorse"/> is the gate: it asserts the count does not drop below
/// the number measured when this file was written. That is what turns the set into a ratchet
/// — a future scoring change is judged by whether this number goes up, not by eyeballing two
/// examples the way the change PROGRESS.md item 4 records already was.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class PowerSearchEvaluationTests
{
    private readonly RulesFixture _f;

    public PowerSearchEvaluationTests(RulesFixture f) => _f = f;

    private CharacterTools Tools() =>
        new(_f.Rules, _f.Costs, _f.Derived, _f.Validator);

    /// <summary>
    /// The number of expectations met. <see cref="TheScoreNeverGetsWorse"/> holds the suite to
    /// at least this; it must never be lowered to make a regression pass — lower it only
    /// alongside a note saying why the set itself changed.
    ///
    /// <para><b>Raised from 24 to 25</b> by widening the stopword list — see
    /// <c>docs/notes/s8-search.md</c> for the before/after table and what else was tried and
    /// reverted. "Through", "than", "anyone" and the rest added there carry no more information
    /// about a Power than "from" or "into", already on this list; dropping them as search terms
    /// shrank the filler-word tie-floods PROGRESS.md item 4 named without touching the scoring
    /// formula itself. "Walks through walls" and "he shoots fire from his hands" — the two cases
    /// quoted directly from that entry — were still unmet at that point: neither the
    /// tie-ordering fix nor the other change measured against this set moved either one, because
    /// neither word is in the matching Power's own printed description.</para>
    ///
    /// <para><b>Raised from 25 to 33 — every expectation met — by giving Powers a searchable
    /// vocabulary as data, not by touching <c>Score</c> or <c>Mentions</c>.</b> The scoring
    /// formula and the word-matching rule are unchanged; what moved is
    /// <c>data/rules/powers.json</c>'s <c>tags</c> field, the same field <c>OptionRow</c> already
    /// reads on the browser side as <c>Keywords</c> — "words it can be found by but does not
    /// print" — and that <see cref="CharacterTools.Score"/> already weighted at 6 points, between
    /// a name/id match and a category match. Phasing's own printed entry never says "walls" (it
    /// says "solid matter") and Blast's never says "fire" (it says "name the type of damage it
    /// inflicts when you buy it" — Blast's whole point is that the caller names the element), so
    /// no reordering of the same haystack could ever reach either one. Adding "walls"/"walk" to
    /// Phasing's tags and "fire"/"shoot"/"shooting" to Blast's — plus a broader pass across 65
    /// more Powers, each word read off that Power's own description — closed the gap. See
    /// PROGRESS.md item 4 for the full before/after table and how each word was chosen.</para>
    ///
    /// <para><b>Raised from 33 (of 33) to 60 (of 72) by widening the set, not by touching the
    /// search.</b> 33 of 33 met meant the ratchet could only ever hold or fail — it had stopped
    /// telling a working search apart from a regressed one. <c>PowerSearchExpectations.cs</c>
    /// gained 39 more entries (35 seeking a Power, 4 that should find nothing), written the same
    /// way as the first 33 and biased toward the 74 Powers PROGRESS.md item 4 records as still
    /// carrying only their original category tags, toward an effect landing on someone other
    /// than the caster, and toward a few sentences with no Power behind them at all. Neither
    /// <c>Score</c> nor <c>Mentions</c> in <c>mcp/CharacterTools.cs</c> changed, and no Power in
    /// <c>data/rules/powers.json</c> gained a tag for this slice — the number below is the
    /// search exactly as it already stood, read against a wider question.</para>
    ///
    /// <para><b>12 of the 39 new entries miss</b>, and every one is a real gap rather than a
    /// scoring accident:</para>
    /// <list type="bullet">
    /// <item>Three of the four "should find nothing" sentences do find something — office and
    /// small-talk vocabulary (a report's "numbers", a stamp collection's "countries") lands a
    /// weak coincidental hit on an unrelated Power's own description. This is what an honest
    /// word-matching search looks like on ordinary English, not a bug to chase; only the fourth
    /// ("parallel park") is clean.</item>
    /// <item><c>cloud_minds</c> and <c>buff</c> do not appear anywhere in a 25-row window for
    /// their sentences at all — neither Power's vocabulary reaches "forget" or "rally the team",
    /// which is exactly the class of gap PROGRESS.md item 4 predicts is still open: only 67 of
    /// 141 Powers were given a wider vocabulary, and these two were not among them.</item>
    /// <item><c>super_senses_lie_detection</c> misses its bar (position 7 of a top-3 ask) even
    /// though "lying" is the literal word in its own printed description — a description-only
    /// match is worth a flat 2 points regardless of how distinctive the word is, so it loses to
    /// several rows matching two or three ordinary words. This is the exact limitation the
    /// original 33/33 slice recorded as still open and never claimed to have fixed.</item>
    /// <item><c>elemental_control</c>, <c>power_absorption</c>, <c>psi_screen</c> and
    /// <c>form_gaseous</c> land just outside their window (one to three rows short). </item>
    /// <item><c>gestalt</c> misses by a wide margin (position 16 of a top-8 ask) — recorded in
    /// its own entry as a deliberately hard, obscure case kept in rather than dropped.</item>
    /// </list>
    ///
    /// <para>None of the 39 new sentences were edited after this number was measured. See the
    /// class remark's note on ambiguity for why: every miss above is a real gap in the search's
    /// vocabulary or its scoring weights, not a sentence that could as honestly have named a
    /// different Power.</para>
    /// </summary>
    private const int Baseline = 60;

    private const int TotalExpectations = 72;

    /// <summary>
    /// The row cap handed to <c>search_powers</c> for every query here. It has to be at least
    /// the largest <c>TopN</c> in the set (8) — asking for fewer than a query's own bar would
    /// make a pass impossible regardless of how the row actually ranks — and the tool's own
    /// ceiling is 25, so that is what this uses throughout.
    /// </summary>
    private const int SearchWindow = 25;

    private readonly record struct Outcome(
        PowerSearchExpectation Expectation, bool Met, int? Position, IReadOnlyList<string> TopIds);

    private List<Outcome> Evaluate()
    {
        var tools = Tools();
        var outcomes = new List<Outcome>();

        foreach (var expectation in PowerSearchExpectations.All)
        {
            var report = JsonNode.Parse(tools.SearchPowers(expectation.Query, SearchWindow))
                ?? throw new InvalidOperationException("search_powers returned no JSON.");

            var ids = report["matches"]!.AsArray()
                .Select(m => m!["id"]!.GetValue<string>())
                .ToList();

            // <b>A sentence meant to find nothing is judged on the search's own `found` count,
            // never on a position in `matches`.</b> `matches` is already cut to the caller's
            // window, so an empty `matches` list does not by itself mean nothing matched — see
            // SearchPowers' own comment on deciding anything from the cut list.
            if (expectation.ExpectNothing)
            {
                var found = report["found"]!.GetValue<int>();
                outcomes.Add(new Outcome(expectation, found == 0, null, ids.Take(5).ToList()));
                continue;
            }

            int? position = null;
            for (var i = 0; i < ids.Count; i++)
            {
                if (!expectation.AcceptablePowerIds.Contains(ids[i])) continue;
                position = i;
                break;
            }

            var met = position is { } p && p < expectation.TopN;
            outcomes.Add(new Outcome(expectation, met, position, ids.Take(5).ToList()));
        }

        return outcomes;
    }

    /// <summary>
    /// Runs the whole set and prints a readable table. Never fails — see the class remark for
    /// why a measurement and a gate have to be two different tests.
    /// </summary>
    [Fact]
    public void ReportTheCurrentScore()
    {
        var outcomes = Evaluate();
        var met = outcomes.Count(o => o.Met);

        var table = new StringBuilder();
        table.AppendLine($"search_powers scored {met} of {outcomes.Count} expectations met.");
        table.AppendLine();
        table.AppendLine(
            $"{"MET",-4} {"WANT (top N)",-13} {"AT",-4} {"QUERY",-55} {"EXPECTED",-20} TOP 5 RETURNED");

        foreach (var o in outcomes.OrderBy(o => o.Met).ThenBy(o => o.Expectation.Query, StringComparer.Ordinal))
        {
            var wanted = o.Expectation.ExpectNothing
                ? "(nothing)"
                : string.Join("/", o.Expectation.AcceptablePowerIds);
            var want = o.Expectation.ExpectNothing ? "found:0" : "top " + o.Expectation.TopN;
            var at = o.Position?.ToString() ?? "-";
            var query = o.Expectation.Query.Length > 55
                ? o.Expectation.Query[..52] + "..."
                : o.Expectation.Query;

            table.AppendLine(
                $"{(o.Met ? "yes" : "NO"),-4} {want,-13} {at,-4} "
                + $"{query,-55} {wanted,-20} {string.Join(", ", o.TopIds)}");
        }

        // A positive control on the measurement itself: this must count every expectation in
        // the set, not a subset a mistyped filter happened to select. Failing here means the
        // table above is not to be trusted, which is a different and worse problem than any
        // individual query missing its mark.
        Assert.Equal(TotalExpectations, outcomes.Count);
        Assert.Equal(PowerSearchExpectations.All.Count, outcomes.Count);

        // No assertion on `met` — see the class remark. Print it so a human reads the number
        // and the table rather than inferring pass/fail from a boolean nobody asked for.
        TestContext.Current.SendDiagnosticMessage(table.ToString());
    }

    /// <summary>
    /// The gate. <b>Broken and watched to fail</b>: forcing <c>search_powers</c>'s internal
    /// row count to 1 regardless of the caller's own <c>limit</c> argument drove this from 24
    /// of 33 to 16 of 33 and failed with that exact count in the message — confirming the
    /// assertion reads live search output on every run rather than a cached number, and that a
    /// query's <see cref="PowerSearchExpectation.TopN"/> above 1 is what a truncated result
    /// list actually costs.
    /// </summary>
    [Fact]
    public void TheScoreNeverGetsWorse()
    {
        var met = Evaluate().Count(o => o.Met);

        Assert.True(met >= Baseline,
            $"search_powers now meets only {met} of {PowerSearchExpectations.All.Count} "
            + $"labelled expectations, down from the recorded baseline of {Baseline}. Run "
            + $"{nameof(ReportTheCurrentScore)} to see which ones regressed.");
    }

    /// <summary>
    /// The two cases PROGRESS.md item 4 names by hand, held to their own bar directly — not
    /// folded into the aggregate count above, so a future change to unrelated vocabulary cannot
    /// let either one quietly slip back out of range while <see cref="TheScoreNeverGetsWorse"/>
    /// stays green on the strength of some other query improving.
    ///
    /// <para><b>What this actually tests is that <c>data/rules/powers.json</c>'s <c>tags</c>
    /// field reaches <c>search_powers</c>, not that the scorer changed</b> — the scorer
    /// (<c>Score</c>/<c>Mentions</c> in <c>CharacterTools.cs</c>) is untouched by this slice.
    /// Phasing's own printed description says "pass through solid matter" and never "walls";
    /// Blast's says "a damaging ranged attack" and never "fire". Both are found now only
    /// because their <c>tags</c> arrays carry "walls"/"walk" and "fire"/"shoot"/"shooting"
    /// respectively.</para>
    ///
    /// <para><b>Broken and watched to fail</b>: with <c>"walls"</c> and <c>"walk"</c> removed
    /// from Phasing's <c>tags</c> in <c>data/rules/powers.json</c> (nothing else changed), this
    /// failed with:
    /// <code>
    /// Assert.Contains() Failure: Item not found in collection
    /// Collection: ["wall_crawling", "constructs", "super_senses_hypersensitive_touch", "super_speed"]
    /// Not found:  "phasing"
    /// </code>
    /// confirming Phasing's presence in the "walls" result depends on that data and not on
    /// coincidence elsewhere in its name, category or description. Restored immediately after;
    /// <c>git diff data/rules/powers.json</c> was empty before committing.</para>
    /// </summary>
    [Fact]
    public void PhasingAndBlastAreFoundByTheirOwnVocabularyNotByTheScorer()
    {
        var tools = Tools();

        var walls = Parse(tools.SearchPowers("walks through walls", 25));
        var wallIds = walls["matches"]!.AsArray()
            .Select(m => m!["id"]!.GetValue<string>()).ToList();
        Assert.Contains("phasing", wallIds);
        Assert.True(wallIds.IndexOf("phasing") < 3,
            $"Phasing must rank in the top 3 for \"walks through walls\"; it is at "
            + $"{wallIds.IndexOf("phasing")} of {wallIds.Count}: [{string.Join(", ", wallIds)}]");

        var fire = Parse(tools.SearchPowers("he shoots fire from his hands", 25));
        var fireIds = fire["matches"]!.AsArray()
            .Select(m => m!["id"]!.GetValue<string>()).ToList();
        Assert.Contains("blast", fireIds);
        Assert.True(fireIds.IndexOf("blast") < 3,
            $"Blast must rank in the top 3 for \"he shoots fire from his hands\"; it is at "
            + $"{fireIds.IndexOf("blast")} of {fireIds.Count}: [{string.Join(", ", fireIds)}]");
    }

    private static JsonNode Parse(string json) =>
        JsonNode.Parse(json) ?? throw new InvalidOperationException("No JSON returned.");
}
