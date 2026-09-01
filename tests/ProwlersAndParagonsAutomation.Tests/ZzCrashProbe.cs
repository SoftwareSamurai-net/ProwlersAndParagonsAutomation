namespace ProwlersAndParagonsAutomation.Tests;

// SPIKE ONLY — never committed to main. Measures what a crashed test process looks like
// under Microsoft.Testing.Platform, because docs/guide/testing.md's claim ("a crashed test
// process still prints `Passed! - Failed: 0`") was measured under VSTest.
public sealed class ZzCrashProbe
{
    private static int Recurse(int n) => Recurse(n + 1) + Recurse(n + 2);

    [Fact]
    public void OverflowTheStack() => Assert.Equal(0, Recurse(0));
}
