using Microsoft.Playwright;

namespace ProwlersAndParagons.E2e;

/// <summary>
/// Thrown by <see cref="Harness.Control"/>. Reported as its own kind of verdict — see
/// <see cref="Runner"/>.
///
/// <para><b>Distinct from an ordinary failure on purpose.</b> "The palette never changed" and "the
/// palette changed to the wrong colour" are different bug reports, and this repository has a
/// documented history of reporting the first as the second and then of not reporting it at all:
/// three of its four historical guard faults were a feature that never ran being mistaken for a
/// feature that worked. <c>CLAUDE.md</c> requires the positive control to come first and to be its
/// own sentence.</para>
/// </summary>
public sealed class ControlFailedException(string what)
    : Exception($"the work did not happen: {what}");

/// <summary>
/// What one check is handed: a page, the browser's own complaints, and the two assertions.
///
/// <para><b>Nothing here may reach past the browser, and that is the rule the whole harness exists
/// to obey.</b> No <c>localStorage.setItem</c> to arrange a state, no calling into a component, no
/// planted storage pointer, and clicks are real mouse events at real coordinates rather than
/// <c>el.click()</c> from inside the page. The argument is in <c>PROGRESS.md</c> item 10: a whole
/// feature shipped here while nothing in the application ever wrote to the store it read from, and
/// every unit and component test passed <em>honestly</em>, because every one of them called the
/// store directly. A test that reaches a feature by hand cannot notice that nothing else reaches
/// it. The only reads that go round the front are the ones <em>asserting</em> on storage after the
/// app has written it.
/// </para>
///
/// <para><b>Clicking is <c>ILocator.ClickAsync</c> and never an evaluated <c>el.click()</c>.</b>
/// Playwright dispatches <c>Input.dispatchMouseEvent</c> at the element's centre through the
/// browser, and adds actionability waits on top, so a click at a coordinate the element has not
/// settled on yet is retried rather than lost.
/// </para>
/// </summary>
public sealed class Harness(IPage page, string baseUrl, IBrowser? browser = null)
{
    /// <summary>Every extra context this harness has opened, so the run can close them.</summary>
    private readonly List<IBrowserContext> _extraContexts = [];

    /// <summary>
    /// Installed into every document before anything else runs.
    ///
    /// <para>Two recordings that cannot be taken after the fact: whether the app's own boot screen
    /// was in the document that was <em>served</em> — by the time a check can look, Blazor has
    /// replaced it — and a per-document token the routes check reads back to tell a client-side
    /// navigation from a full reload.</para>
    /// </summary>
    internal const string Recorder = """
        window.__ppE2E = { sawBootScreen: null, document: Math.random().toString(36).slice(2) };
        document.addEventListener('DOMContentLoaded', () => {
            window.__ppE2E.sawBootScreen = !!document.querySelector('.boot');
        });
        """;

    /// <summary>The visible text of the page's own heading, or null.</summary>
    internal const string H1 =
        "document.querySelector('#main-content h1, main h1, h1') ? "
        + "document.querySelector('#main-content h1, main h1, h1').textContent.trim() : null";

    public IPage Page { get; } = page;

    /// <summary>The address the server is on, with no trailing slash.</summary>
    public string BaseUrl { get; } = baseUrl;

    /// <summary>
    /// Console errors and uncaught exceptions since this check started.
    ///
    /// <para><b>Cleared between checks.</b> An error logged while the boot check ran must not be
    /// reported by the palette check three minutes later.</para>
    /// </summary>
    public List<string> ConsoleErrors { get; } = [];

    /// <inheritdoc cref="ConsoleErrors"/>
    public List<string> Exceptions { get; } = [];

    /// <summary>
    /// The address and status of every response this page has received, cleared between checks.
    ///
    /// <para><b>This is a read and not a reach past the browser.</b> The rule is that no check may
    /// <em>arrange</em> a state the application did not create — no <c>localStorage.setItem</c>, no
    /// calling into a component. Watching what the browser was answered is the opposite: it is the
    /// only way to tell "the page says you are not allowed" from "the page says that because it
    /// never asked anybody", and it is what lets the rulebook check assert a real <c>401</c> from
    /// the accounts server rather than a sentence in the markup.</para>
    /// </summary>
    /// <remarks>
    /// <b>Read through <see cref="ResponsesSoFar"/> and never enumerated directly.</b> Playwright
    /// raises <c>Response</c> on its own thread while a check is running, so a <c>Where(...)</c>
    /// straight over this list throws <i>Collection was modified</i> the moment a page is still
    /// fetching — which is most of the time, and which a twin duly reported as a
    /// <c>[HARNESS]</c> verdict rather than as the defect it was supposed to prove.
    /// </remarks>
    private List<(string Method, string Url, int Status)> Responses { get; } = [];

    /// <summary>A snapshot of <see cref="Responses"/>, safe to enumerate while the page is busy.</summary>
    public List<(string Method, string Url, int Status)> ResponsesSoFar()
    {
        lock (Responses) return [.. Responses];
    }

    /// <summary>Forget what has been seen so far. Called between checks by <see cref="Runner"/>.</summary>
    public void ClearResponses()
    {
        lock (Responses) Responses.Clear();
    }

    /// <summary>
    /// A second browser, in effect: a fresh context with its own cookie jar and its own empty local
    /// storage, on the same site.
    ///
    /// <para><b>Two checks need one and neither could be written honestly without it.</b>
    /// <c>ACCOUNT_SAVE</c> asks whether a character followed the <em>account</em> rather than the
    /// browser, which is not a question a single context can answer: whatever it finds might be
    /// what it left in local storage. <c>RULES</c> asks what a stranger is served, which needs a
    /// reader who is not signed in while one who is stays signed in.</para>
    ///
    /// <para><b>It inherits nothing except the site and the render latch.</b> The latch is carried
    /// over because it only ever shortens a run that is already red — see
    /// <see cref="AppEverRendered"/> — and everything else about the context is new by design.</para>
    /// </summary>
    public async Task<Harness> FreshContext()
    {
        if (browser is null)
        {
            throw new CheckFailedException(
                "this harness was built without a browser, so it cannot open a second context. "
                + "Runner.Drive is what supplies one.");
        }

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        });

        _extraContexts.Add(context);

        var fresh = await context.NewPageAsync();
        var harness = new Harness(fresh, BaseUrl, browser) { AppEverRendered = AppEverRendered };

        Watch(fresh, harness);
        await fresh.AddInitScriptAsync(Recorder);

        return harness;
    }

    /// <summary>
    /// Wire a page's own complaints and answers into the harness that will report them.
    ///
    /// <para>One place rather than two, because a fresh context that recorded nothing would give a
    /// check a permanently empty <see cref="Responses"/> — and every assertion made of it is an
    /// absence, which an empty list satisfies.</para>
    /// </summary>
    internal static void Watch(IPage watched, Harness into)
    {
        watched.Console += (_, message) =>
        {
            if (message.Type == "error") into.ConsoleErrors.Add(message.Text);
        };

        watched.PageError += (_, error) => into.Exceptions.Add(error);
        watched.Response += (_, response) =>
        {
            lock (into.Responses)
                into.Responses.Add((response.Request.Method, response.Url, response.Status));
        };
    }

    /// <summary>
    /// Close whatever <see cref="FreshContext"/> opened. Called between checks by
    /// <see cref="Runner"/>, so a check that opened one cannot leave it running through the next.
    /// </summary>
    public async Task CloseExtraContexts()
    {
        foreach (var context in _extraContexts) await context.CloseAsync();

        _extraContexts.Clear();
    }

    /// <summary>
    /// The main-frame response for the last <see cref="Goto"/> or <see cref="Reload"/>.
    ///
    /// <para>Read for its <em>status and its address</em>, both of which a rendered page cannot
    /// tell you: <c>web/wwwroot/_redirects</c> rewrites every path to <c>index.html</c> with a 200
    /// rather than a redirect precisely so a shared link keeps its path, and a 302 would drop it
    /// and land every reader on step one while the heading assertion stayed perfectly green.</para>
    /// </summary>
    public IResponse? LastDocument { get; private set; }

    /// <summary>
    /// Whether the app has ever rendered in this run. <c>null</c> until asked.
    ///
    /// <para><b>A one-way latch, and the "one-way" was the second attempt.</b> It exists so a site
    /// that cannot boot at all costs one timeout rather than ten. With a two-way latch the
    /// <c>base-href-dropped</c> twin — where the front door boots perfectly and every deep link
    /// cannot fetch the framework — turned ROUTES red for the <em>latch's</em> reason rather than
    /// for the address it could not load, which is a red verdict that proves nothing about the
    /// check. Once the app has rendered once, every later wait is measuring something real and
    /// must be allowed to run to its deadline.</para>
    ///
    /// <para>In the other direction it can hide nothing: it only ever short-circuits after a wait
    /// has already failed with nothing having rendered yet, so the run is red and staying red.</para>
    /// </summary>
    internal bool? AppEverRendered { get; set; }

    /// <summary>
    /// Assert that the work happened. Throws <see cref="ControlFailedException"/>.
    /// </summary>
    public static void Control(bool condition, string what)
    {
        if (!condition) throw new ControlFailedException(what);
    }

    /// <summary>Assert that the outcome is right.</summary>
    public static void Outcome(bool condition, string what)
    {
        if (!condition) throw new CheckFailedException(what);
    }

    /// <summary>
    /// Forty-five seconds, against a measured four for a render on a developer's machine.
    ///
    /// <para>The payload is ~27 MiB of WebAssembly and a loaded runner is slower than a laptop, but
    /// ten times the honest cost is headroom rather than a guess — and a deadline nobody has
    /// measured is how a hung step once cost this project a six-hour job. Every wait in this
    /// harness is bounded, which is what lets <c>scripts/e2e.sh</c> treat its own 300-second
    /// per-drive deadline as "the browser stopped answering" rather than "a check is slow".</para>
    /// </summary>
    public const int RenderTimeoutMs = 45_000;

    /// <summary>How long a single navigation may take.</summary>
    public const int NavigationTimeoutMs = 30_000;

    /// <summary>Evaluate an expression in the page and return it as <typeparamref name="T"/>.</summary>
    public Task<T> Eval<T>(string expression) => Page.EvaluateAsync<T>($"() => ({expression})");

    /// <summary>Go to an address, without waiting for the app.</summary>
    public async Task Goto(string path)
    {
        LastDocument = await Page.GotoAsync($"{BaseUrl}{path}",
            new PageGotoOptions { Timeout = NavigationTimeoutMs });
    }

    /// <summary>Reload the current address, without waiting for the app.</summary>
    public async Task Reload()
    {
        LastDocument = await Page.ReloadAsync(
            new PageReloadOptions { Timeout = NavigationTimeoutMs });
    }

    /// <summary>
    /// Wait until an expression is truthy in the page.
    ///
    /// <para><b>Named, and the name is in the failure.</b> Playwright's own message for a lapsed
    /// <c>WaitForFunctionAsync</c> quotes the expression, which for a nine-address loop is the same
    /// unhelpful sentence nine times over.</para>
    /// </summary>
    public async Task WaitFor(string expression, string what, int timeoutMs = RenderTimeoutMs)
    {
        try
        {
            await Page.WaitForFunctionAsync($"() => ({expression})",
                null, new PageWaitForFunctionOptions { Timeout = timeoutMs });
        }
        catch (TimeoutException)
        {
            throw new CheckFailedException($"waited {timeoutMs}ms for {what}");
        }
    }

    /// <summary>Wait until Blazor has rendered a page into the shell.</summary>
    public async Task WaitForApp()
    {
        if (AppEverRendered == false)
        {
            throw new CheckFailedException(
                "the app has not rendered once in this run — see the BOOT verdict above. Not "
                + "waited for again: this run is already red, and each further wait would be a "
                + "full timeout.");
        }

        try
        {
            await WaitFor("!!window.Blazor", "the framework to start");
            await WaitFor($"!document.querySelector('.boot') && ({H1})", "the app to render a page");
            AppEverRendered = true;
        }
        catch
        {
            AppEverRendered ??= false;
            throw;
        }
    }

    /// <summary>
    /// Go to an address and wait for the app to render it.
    ///
    /// <para><b>The address is put in front of whatever <see cref="WaitForApp"/> says.</b> Without
    /// it a failure reads "waited 45000ms for the framework to start", which is the same sentence
    /// whichever of nine addresses it was — and the <c>base-href-dropped</c> twin's whole point is
    /// that the front door works and a deep link does not.</para>
    /// </summary>
    public async Task Open(string path)
    {
        await Goto(path);

        try
        {
            await WaitForApp();
        }
        catch (Exception error)
        {
            throw new CheckFailedException($"{path}: {error.Message}", error);
        }
    }
}

/// <summary>
/// An outcome assertion that did not hold, or a wait that lapsed.
///
/// <para>A type of its own rather than <c>InvalidOperationException</c> so that
/// <see cref="Runner"/> can tell a check that <em>reported</em> a fault from a check that fell over
/// — the second is a harness bug and reads differently in the log.</para>
/// </summary>
public sealed class CheckFailedException : Exception
{
    public CheckFailedException(string message) : base(message) { }

    public CheckFailedException(string message, Exception inner) : base(message, inner) { }
}
