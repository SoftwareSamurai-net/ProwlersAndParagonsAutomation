# S3 — presentation-guard hardening

Two proved defects in `tests/ProwlersAndParagonsAutomation.Tests/WebPresentationTests.cs`, both
confirmed by mutation before being touched and both re-confirmed by mutation after the fix.

## 1. `NoComponentNamesAColour` missed `light-dark()`

### What the guard claimed

That no `.razor` component, `app.css`, or `index.html` names a colour by hex, by keyword, or by
building one from raw channel values — the property the whole four-palette architecture (Hero/
Villain × light/dark) depends on, since it is what keeps switching palettes a one-attribute change.

### The mutation that defeated it

Three mutations, each planted in a real rule (`.btn` in `app.css`) against the **old** guard, each
confirmed green before any test code changed:

| # | Mutation | Old guard result |
|---|---|---|
| 1 | `color: light-dark(white, black);` | **Passed** (3/3) — `light-dark` was not in the six-name channel-function list, and its arguments follow `(`/`,` rather than the `:` the keyword regex required |
| 2 | `border-bottom: 1px solid black;` | **Passed** (3/3) — the keyword regex was anchored on `:\s*` immediately before the colour word; `black` here follows `solid `, not `:` |
| 3 | `background: color(display-p3 1 0 0);` | **Passed** (3/3) — `color()` was not in the channel-function list either |

Failure messages: none — all three mutations produced a clean `Passed!` run, which is the defect.

### What replaced it

- **Keyword regex**: dropped the `:\s*` anchor entirely. The replacement matches a colour keyword
  anywhere, bounded by `(?<![\w-])` / `(?![\w-])` — a hyphen-aware boundary rather than plain `\b`,
  because `\b` treats `-` as a boundary too and `white-space` (a real property name used throughout
  `app.css`) is "white" immediately followed by one. Checked against the whole `web/` tree with the
  position requirement dropped entirely: zero matches outside comments.
- **Channel-function list**: extended from six names to eleven — added `color`, `light-dark`,
  `color-contrast`, `device-cmyk` (every other named CSS Color 4/5 function; `color-mix` stays
  excluded because it is already masked separately, since this codebase's one use of it takes only
  tokens). Still a denylist, documented as one: a full allow-list-of-safe-functions inversion was
  considered and rejected, because the razor scan runs over files that mix markup with C# — a
  Razor `@code` block is full of unrelated calls (`ToList()`, `Where()`, `Select()`) that an
  allow-everything-else rule would have to special-case one by one, which is the same denylist
  problem moved up a level.
- **`Scannable`**: now also strips `///` XML doc-comment lines from the razor (non-CSS) scan,
  alongside the existing `@* *@` Razor-comment strip. Needed because dropping the keyword regex's
  colon anchor turned one real doc comment — `ChooseTier.razor`'s `/// … dead white above their
  cost rule …` — into a false positive; it is prose about a screenshot, not a declaration, the same
  reasoning the Razor-comment exclusion already rests on. Scoped to `///` specifically, not general
  `//` or `/* */` C# comments, because neither appears carrying this kind of prose anywhere in
  `web/` today.
- **`currentColor`**: deliberately left unflagged, on the same reasoning as `transparent` — neither
  names a hue; both are a reference to something else rather than a colour chosen here. Recorded in
  the doc comment as a deliberate decision, not an oversight.

### Mutation table against the new guard

| # | Mutation | New guard result |
|---|---|---|
| 1 | `color: light-dark(white, black);` | **Failed** — `app.css names a colour keyword.` (caught by the widened keyword regex before the function list even needs to fire) |
| 2 | `border-bottom: 1px solid black;` | **Failed** — `app.css names a colour keyword.` |
| 3 | `background: color(display-p3 1 0 0);` | **Failed** — `app.css builds a colour from raw channel values. Add a token to theme.css instead.` (caught by the extended function list, a distinct failure path from #1/#2) |

All three restored from committed state and the guard re-run green (3/3) after each.

## 2. `EachOwnerActuallyWritesTheClassItOwns` is a bare substring match

### What the guard claimed

The positive half of "one component owns each repeated class": that the file named as an owner in
`OwnedClasses` really does write the class it is credited with, so the exclusivity check above it
(`OnlyOneComponentWritesEachRepeatedClass`) cannot be satisfied by an owner that quietly stopped
writing the class at all.

### The mutation that defeated it

`Assert.Contains($"\"{cssClass}", source, StringComparison.Ordinal)` — a raw substring check with
no closing boundary. Renaming the literal `"panel"` to `"panelish"` in `Panel.razor`'s `ClassName`
property (the exact case the task named) was applied against the **old** guard and re-confirmed:

```
Passed!  - Failed: 0, Passed: 15, Skipped: 0, Total: 15
```

All fifteen theory cases stayed green, because `"panelish"` still contains the substring `"panel`
(quote + the first five letters) even though the component never writes the real `panel` class
anywhere again.

### What replaced it

Reused the real tokenizer from `OnlyOneComponentWritesEachRepeatedClass` (factored out as
`ClassAttributeValues`/`ClassAttributeTokens`) rather than writing a third spelling of it, per the
instruction. Nine of the fifteen owned classes are written straight into a `class="…"` attribute
and are read that way, split on whitespace, checked for exact membership.

**Six cannot be read by the attribute tokenizer at all**, because the class never appears inside a
`class="…"` attribute in source text — this is the finding the task asked for:

| Class | Owner | Where it actually lives |
|---|---|---|
| `panel` | `Panel.razor` | `ClassName` property: `string.Join(' ', new[] { "panel", … })` |
| `field` | `Field.razor` | `ClassName` property: `string.IsNullOrEmpty(Class) ? "field" : $"field {Class}"` |
| `sheet-section` | `SheetSection.razor` | same shape, `ClassName` |
| `stat-blocks` | `StatBlockRow.razor` | same shape, `ClassName` |
| `options` | `OptionList.razor` | same shape, `ClassName` |
| `option` | `OptionRow.razor` | `RowClass` property: `(Selected ? "option selected" : "option") + …` |
| `rule-line` | `RuledLines.razor` | `Lines` property (a `RenderFragment` lambda): `builder.AddAttribute(1, "class", "rule-line")` — C# calling into the render tree, not markup at all |

For these seven entries (six distinct member shapes; `Lines` is the seventh, its own case), the
class is read from the named C# member's own body: `MemberBody` is a small lexer that locates the
member by its expression-bodied declaration (`<member>\s*=>`, which also skips the earlier markup
reference `@ClassName` that appears higher in every one of these files), then walks forward
tracking `(`/`)`/`{`/`}` depth — skipping over string-literal contents (interpolation holes
included) so an embedded `{Class}` never throws the depth count off — stopping at the first `;` at
depth zero. `ClassLiteralTokens` then pulls every quoted string literal out of that bounded text,
replaces `{…}` interpolation holes with a space, and splits on whitespace — the same exact-token
membership check as the attribute path, never a prefix.

**A broader alternative was tried and rejected**: scanning the *whole file* for quoted C# string
literals, rather than bounding to the named member. This fails concretely, not just in theory —
`OptionRow.razor` writes `role="@(Navigable ? "option" : null)"`, an ARIA role value that happens to
spell the same word as the CSS class `option`. A whole-file scan would keep reporting `RowClass` as
writing `option` even after that property stopped, exactly the same class of bug being fixed.
Verified by mutation (see table below): scoping to `MemberBody` correctly ignores the `role="option"`
decoy.

### Mutation table against the new guard

| # | Mutation | New guard result |
|---|---|---|
| 1 | `panel` → `panelish` in `Panel.razor`'s `ClassName` (required case) | **Failed**, only the `("panel", "Panel.razor")` case — `Collection: ["panelish", "no-print"]`, `Not found: "panel"`. The unrelated `("panel-head", "Panel.razor")` case, untouched, stayed green. |
| 2 | `option` → `optionish` in `OptionRow.razor`'s `RowClass` | **Failed**, only the `("option", "OptionRow.razor")` case — `Collection: ["optionish", "selected", "optionish", "current", "tip-dismissed"]`, `Not found: "option"`. Proves the `role="option"` ARIA decoy elsewhere in the same file does not leak a false pass. |
| 3 | `rule-line` → `rule-lineish` in `RuledLines.razor`'s `AddAttribute` call | **Failed**, only the `("rule-line", "RuledLines.razor")` case — `Collection: ["span", "class", "rule-lineish", "aria-hidden", "true"]`, `Not found: "rule-line"`. |

All three restored from committed state; `EachOwnerActuallyWritesTheClassItOwns` and
`OnlyOneComponentWritesEachRepeatedClass` both re-run green (15/15 each) after every mutation.

## Verification

- `dotnet build tests/ProwlersAndParagonsAutomation.Tests` — clean, 0 warnings.
- `dotnet test tests/ProwlersAndParagonsAutomation.Tests --filter FullyQualifiedName~WebPresentationTests`
  — 166/166 (same count as before; no test cases were added, only helper methods and two rewritten
  bodies).
- `dotnet test --configuration Release -p:ContinuousIntegrationBuild=true` (whole solution) — two
  `Passed!` lines: `ProwlersAndParagonsAutomation.Tests.dll` 3734/3734, `ProwlersAndParagons.Web.Tests.dll`
  482/482. No `Catastrophic`, no warnings.
- Every mutation above was applied against committed work, watched to fail (old guard) or watched to
  fail correctly (new guard), then restored with a matching `Edit` rather than a bare
  `git checkout -- <path>`, and `git diff --stat` confirmed clean before the next step.
