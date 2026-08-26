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
    /// quoted directly from that entry — are still unmet: neither the tie-ordering fix nor the
    /// other change measured against this set moved either one, and the note explains why.
    /// </para>
    /// </summary>
    private const int Baseline = 25;

    private const int TotalExpectations = 33;

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
            var wanted = string.Join("/", o.Expectation.AcceptablePowerIds);
            var at = o.Position?.ToString() ?? "-";
            var query = o.Expectation.Query.Length > 55
                ? o.Expectation.Query[..52] + "..."
                : o.Expectation.Query;

            table.AppendLine(
                $"{(o.Met ? "yes" : "NO"),-4} {"top " + o.Expectation.TopN,-13} {at,-4} "
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
}
