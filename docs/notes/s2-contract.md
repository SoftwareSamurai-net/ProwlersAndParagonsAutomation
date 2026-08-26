# s2-contract: two guards in `AccountsContractTests.cs` that claimed more than they checked

Both defects were in `tests/ProwlersAndParagonsAutomation.Tests/AccountsContractTests.cs`. Each
was proved by mutation before being touched, and each replacement was proved by mutation after.

## 1. The engine had no filesystem-isolation test at all

**The claim.** `CLAUDE.md`'s "### The engine never touches the filesystem" section, and,
verbatim, `TheEngineHasNoNetwork`'s own doc comment: "The engine has no filesystem access by
design." Nothing enforced it.

### Proving the gap

Two mutation attempts were needed, because the first one was silently inert rather than caught.

**Attempt 1 — a dead static field.** Added:

```csharp
private static readonly string _proofMutation = System.IO.File.ReadAllText("x");
```

to `engine/CostCalculator.cs`. Ran the full suite: `Passed! - Failed: 0, Passed: 3734`. This
looked like proof of the gap, but it wasn't a fair mutation — the field was never read anywhere,
and with `beforefieldinit` semantics the JIT is free to never trigger the type initializer for a
static field nothing ever accesses. The mutation may simply never have executed.

**Attempt 2 — a reachable, non-throwing call.** Replaced it with a call inside a method that
every test exercises, using an API that succeeds rather than throwing (so the effect is
observable only in *what the code touches*, not in behaviour):

```csharp
public int AbilityCost(CharacterSheet sheet)
{
    _ = System.IO.File.Exists("x-proof-mutation-does-not-exist");
    var covered = SelectedPackage(sheet)?.AbilitiesRank ?? 0;
    ...
```

`AbilityCost` is called by `TotalCost`, which is called by `CharacterValidator.CheckHpBudget`,
which nearly every test in the suite exercises indirectly. Ran the full suite:

```
Passed!  - Failed:     0, Passed:  3734, Skipped:     0, Total:  3734
```

Confirmed: a real, executed filesystem call left all 3,734 tests green. (An earlier attempt using
`File.ReadAllText` instead of `File.Exists` *did* fail 463 tests — but only because the read threw
`FileNotFoundException` and broke behaviour, not because anything asserted the property "no
filesystem access." That confirmed the gap is a missing guard, not a coincidentally-covered one:
the moment the mutation was made behaviourally silent, everything passed.)

Reverted from the pre-mutation backup; `git diff --stat` showed no change.

### The replacement: `TheEngineHasNoFilesystemAccess`

Added to `AccountsContractTests.cs`, next to `TheEngineHasNoNetwork`.

**Scope.** `engine/` and `sheets/` (via the existing `RulesSources()` helper), minus one
sanctioned exception: `FileSystemRulesSource.cs`. That file is the documented, deliberate
`IRulesSource` implementation the CLI hands the repository — the "host provides it" seam
`CLAUDE.md` describes — so it is excluded by filename rather than the ban being loosened.

**What is banned**, chosen deliberately rather than banning `System.IO` wholesale:

- `File.` / `Directory.` — the two static classes that actually touch a disk.
- `FileStream`, `StreamReader`, `StreamWriter` — types that wrap a disk handle (or could).
- An explicit `using System.IO;` — nothing legitimate here needs to write one, since the SDK's
  implicit usings already bring the namespace into every file project-wide. (This is *why* a
  stray `File.Exists` compiles silently in the first place, and why a missing `using` can't be
  the guard.)

**What is deliberately *not* banned**: `Path.Combine`, `Path.GetFullPath`, and similar — pure
string manipulation with nothing on the far end. `RulesRepository.FromBasePath` uses
`Path.Combine` legitimately (`engine/RulesRepository.cs`), and a survey of `engine/*.cs` and
`sheets/*.cs` found no `AppContext.BaseDirectory`, `StreamReader`, `StreamWriter`, or `FileStream`
in live code anywhere — only in doc comments (see below).

**Comments are blanked before the scan.** `IRulesSource.cs`'s doc comment names
`File.ReadAllText` as history ("The repository used to call `File.ReadAllText` directly...");
`RulesRepository.cs`'s doc comment mentions `AppContext.BaseDirectory`;
`CharacterSheetRenderer.cs`'s doc comment mentions `File.WriteAllText`. All three are explaining
the architecture, not writing code that touches a disk. A raw-text scan would flag all three as
false positives — the same shape `NeitherTheRulebookNorTheRecordingsAreStagedIntoTheSite` already
guards against for the csproj scan (`WithoutXmlComments`). The new `WithoutCsComments` helper
blanks `//` and `/* */` comments the same way `WebPresentationTests.WithoutJsComments` already
does for JavaScript — C# and JS share that comment syntax.

**Positive control.** `RulesSources()` (minus the one exemption) must find more than 10 files —
guards against measuring an empty list, the exact failure mode `CLAUDE.md` names four times over.

### Mutation-testing the replacement

Re-applied the *same* `File.Exists` mutation to `CostCalculator.AbilityCost`, ran only the new
test:

```
[FAIL] TheEngineHasNoFilesystemAccess
engine/ and sheets/ read rules through IRulesSource and return strings; nothing else here may
reach the filesystem. FileSystemRulesSource.cs is the one sanctioned exception — the
host-provided implementation of IRulesSource for a host that has a disk. Offending files:
CostCalculator.cs
```

Red, naming the right file. Reverted with `git checkout -- engine/CostCalculator.cs` (the file
was unmodified relative to the just-made commit, so this was safe per `CLAUDE.md`'s stash
discipline — nothing uncommitted was at risk). Re-ran: green, 15/15 in the class (was 14 before
the new test was added).

## 2. `EveryAddressTheBrowserAsksForIsOneTheServerAnswers` was systematically defeated

**The old guard:**

```csharp
var routed = ServerSource();   // string.Concat of every worker/*.js file
...
var unanswered = asked.Where(address => !routed.Contains($"'{address}'", ...)).ToList();
```

A route literal appearing *anywhere* in *any* worker file — not just in the actual routing
condition in `worker/index.js` — satisfied it.

### Proving the defeat

`worker/errors.js` declares:

```js
const KNOWN_ROUTES = Object.freeze([
    '/api/auth/request', '/api/auth/verify', '/api/auth/signout', '/api/me',
    '/api/me/display-name',
    '/api/characters', '/api/rulebook/power',
    '/api/rulebook/search', '/api/rulebook/contents', '/api/rulebook/passage',
    '/api/admin/error-log',
    '/api/transcripts',
]);
```

— a list built for a *different* purpose (bounding the `error_log` table to a closed set of
route patterns), which happens to duplicate most of the addresses `worker/index.js` actually
routes.

Renamed the real routing condition in `worker/index.js`:

```diff
- if (path === '/api/me') return only('GET', method, () => auth.me(request, env, deps));
+ if (path === '/api/me-renamed-proof-mutation') return only('GET', method, () => auth.me(request, env, deps));
```

`/api/me` is no longer routed at all. Ran the (then-unmodified) old test:

```
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14
```

Green. The literal `'/api/me'` survived in `worker/errors.js`'s `KNOWN_ROUTES`, so
`routed.Contains("'/api/me'")` was still true even though nothing routes it any more. Defeat
reproduced exactly as briefed — a route whose literal also appears in `worker/errors.js`.

Reverted with `git checkout -- worker/index.js` (unmodified relative to the last commit at that
point — no uncommitted work existed anywhere in the tree, confirmed with `git status --short`
before the checkout).

### The replacement

Reads `worker/index.js` alone, structurally, rather than searching concatenated source for a
substring:

```csharp
var exact = Regex.Matches(indexJs, @"path\s*===\s*'(/api/[A-Za-z0-9/_.-]*)'", ...)
    .Select(m => m.Groups[1].Value).Distinct(...).ToList();

var prefixes = Regex.Matches(indexJs, @"path\.startsWith\('(/api/[A-Za-z0-9/_.-]*)'\)", ...)
    .Select(m => m.Groups[1].Value).Distinct(...).ToList();
```

An address the browser asks for is answered only if it equals one of `exact` or begins with one
of `prefixes`:

```csharp
var unanswered = asked
    .Where(address => !exact.Contains(address, StringComparer.Ordinal)
                    && !prefixes.Any(prefix => address.StartsWith(prefix, StringComparison.Ordinal)))
    .ToList();
```

This is what makes `/api/characters/{id}` and `/api/admin/invitations/{id}` resolve correctly:
neither is ever compared with `===` in `worker/index.js` — both are reached through
`path.startsWith('/api/characters/')` / `path.startsWith('/api/admin/invitations/')` followed by
`path.slice(...)` to pull out the id. A model that only knew exact literals would report both as
permanently unrouted.

**Extraction found today**: 13 distinct exact routes, 3 distinct prefixes
(`/api/characters/`, `/api/rulebook/`, `/api/admin/invitations/`).

**Two positive controls:**

- `exact.Count is >= 8 and <= 40` and `prefixes.Count is >= 2 and <= 15` — bounds loose enough
  that a genuine new route doesn't require editing this test, tight enough to catch the pattern
  going empty (too few — the safe direction, since every address then reads as unrouted).
  Swallowing the whole file is additionally impossible *by construction*: the capture group
  `[A-Za-z0-9/_.-]*` can only match path characters, and only appears immediately after the
  `path === '` / `path.startsWith('` boilerplate — there is no way for it to consume unrelated
  code even if that boilerplate recurs.
- The pre-existing `asked.Count >= 5` control on the browser-side scan was kept unchanged, per
  the brief.

### Mutation-testing the replacement

Re-applied the identical `/api/me` → `/api/me-renamed-proof-mutation` rename, ran only the new
test:

```
[FAIL] EveryAddressTheBrowserAsksForIsOneTheServerAnswers
The browser asks for these and worker/index.js routes none of them, exactly or by prefix: /api/me
```

Red, naming exactly the address the old guard missed. Reverted with `git checkout --
worker/index.js` (tree was clean before, confirmed with `git status --short`). Re-ran: green,
15/15 in the class.

## Mutation table

| # | Guard | Mutation | Old guard | New guard |
|---|---|---|---|---|
| 1a | filesystem isolation | dead static field calling `File.ReadAllText` (never read) | n/a — no guard existed | not applicable (inert mutation, discarded) |
| 1b | filesystem isolation | `File.ReadAllText` inside a real, executed method | n/a — no guard existed | (not run against old; there was no old guard) — broke 463 tests via thrown exception, not via a guard |
| 1c | filesystem isolation | `File.Exists` (non-throwing) inside a real, executed method | n/a — no guard existed | **RED**, names `CostCalculator.cs` |
| 2 | routing contract | rename `/api/me`'s routing condition to an address the browser never asks for | **GREEN** (defeated — `/api/me` survives in `worker/errors.js`) | **RED**, names `/api/me` |

## Full suite, before and after

Baseline (before any change, `dotnet test --configuration Release
-p:ContinuousIntegrationBuild=true`):

```
Passed!  - Failed:     0, Passed:  3734, Skipped:     0, Total:  3734 - ProwlersAndParagonsAutomation.Tests.dll
Passed!  - Failed:     0, Passed:   482, Skipped:     0, Total:   482 - ProwlersAndParagons.Web.Tests.dll
```

After (one new test — `TheEngineHasNoFilesystemAccess` — added;
`EveryAddressTheBrowserAsksForIsOneTheServerAnswers` rewritten in place, same name, same count of
one):

```
Passed!  - Failed:     0, Passed:  3735, Skipped:     0, Total:  3735 - ProwlersAndParagonsAutomation.Tests.dll
Passed!  - Failed:     0, Passed:   482, Skipped:     0, Total:   482 - ProwlersAndParagons.Web.Tests.dll
```

No `Catastrophic` in either run. `dotnet build --configuration Release
-p:ContinuousIntegrationBuild=true` on the test project alone: 0 warnings, 0 errors.

`worker/` was never left in a mutated state at any point where the JS suite was run for real; both
`worker/index.js` mutations were made, tested against the C# guard, and reverted within the same
step. `./scripts/test-worker.sh` on the final (unmodified) tree: `166 pass, 0 fail`.
