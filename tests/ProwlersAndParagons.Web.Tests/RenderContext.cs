using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// A bUnit context wired the way <c>web/Program.cs</c> wires the real app: the same
/// services, built from the <b>real</b> <c>data/rules/*.json</c>.
///
/// <para>Using the shipped rules rather than a stub is the same decision the engine suite
/// made, and for the same reason — a component that renders correctly against invented data
/// and wrongly against the rulebook has been tested for nothing. It also means these tests
/// exercise the exact path the browser takes, minus the HTTP fetch.</para>
/// </summary>
public sealed class RenderContext : BunitContext
{
    public CharacterSession Session { get; }

    /// <summary>
    /// The accounts server this context's app is talking to.
    ///
    /// <para>Signed out until a test says otherwise. Reach for it to sign somebody in, to give
    /// the account a character, or to take the server away entirely.</para>
    /// </summary>
    public FakeApi Api { get; } = new();

    /// <summary>
    /// This browser's local storage, when the context was asked for one that really stores.
    /// Null otherwise — see the <c>storesForReal</c> parameter.
    /// </summary>
    public FakeLocalStorage? Storage { get; }

    /// <param name="storesForReal">
    /// Set to give the app a local storage that actually holds what is written to it, instead of
    /// bUnit's recorder, which answers null to every read.
    ///
    /// <para><b>It exists because every storage test in this project had to assert on which key was
    /// written rather than on what came back</b>, and a feature can satisfy every one of those while
    /// being unreachable by anybody using the app. That is not hypothetical here: the list of
    /// characters, the banner's switcher and both undo buffers were built, tested and shipped
    /// reading an index that nothing in the app ever added a character to, and the reason no test
    /// noticed is that no test could ever have had two characters in it. A context that stores lets
    /// a test press the button a person presses and then ask what is actually there.</para>
    ///
    /// <para>Opt-in rather than the default because it replaces bUnit's <c>IJSRuntime</c>, and with
    /// it <c>JSInterop.Invocations</c> — which most of this project's interop assertions read.
    /// <see cref="FakeLocalStorage.Calls"/> is the replacement for a test that takes this.</para>
    /// </param>
    /// <param name="recordingsUnavailable">
    /// Set to render the app as it is when the recordings could not be fetched — an empty
    /// library carrying the reason, which is what <see cref="ReplayLoader"/> answers when the
    /// gated route refuses or fails. There is no way to reach that state through the UI, and it
    /// is the state in which the replay pages have to say something true rather than guess.
    /// </param>
    public RenderContext(bool recordingsUnavailable = false, bool storesForReal = false)
    {
        var rules     = RulesRepository.FromBasePath(RepoRoot());
        var costs     = new CostCalculator(rules);
        var derived   = new DerivedStatsCalculator(rules);
        var validator = new CharacterValidator(rules, costs, derived);

        Services.AddSingleton(rules);
        Services.AddSingleton(costs);
        Services.AddSingleton(derived);
        Services.AddSingleton(validator);
        Services.AddSingleton(new ProConApplicability(rules));
        Services.AddSingleton(new SourceGrouping(rules));

        Services.AddScoped<CharacterSession>();

        // The accounts server, as far as the browser can tell — see FakeApi. Signed out by
        // default, which is the app every test written before accounts existed was written
        // against: an anonymous visitor whose character is in this browser.
        //
        // The recorded conversations are wired the same way now — bundled behind the gate
        // rather than an eagerly-built singleton — so FakeApi is given the real files from
        // data/transcripts, read the same way the rules are read from data/rules: a replay
        // that renders correctly against an invented transcript and wrongly against the
        // shipped ones has been tested for nothing. ReplayLoader does the fetching, through
        // this HttpClient, exactly as the app's does.
        Api.TranscriptsBundle = JsonSerializer.Serialize(
            TranscriptLibrary.FileNames.ToDictionary(
                name => name,
                name => JsonDocument.Parse(
                    File.ReadAllText(Path.Combine(RepoRoot(), "data", "transcripts", name))).RootElement,
                StringComparer.Ordinal));
        Api.TranscriptsUnavailable = recordingsUnavailable;

        Services.AddSingleton(Api);
        Services.AddScoped(_ => new HttpClient(Api) { BaseAddress = new Uri("https://pp.example.test/") });
        Services.AddScoped<ReplayLoader>();

        // Resolves bUnit's own IJSRuntime, so a component that persists can be rendered and
        // the interop it asks for can be read back off JSInterop.Invocations.
        //
        // Registered exactly as web/Program.cs registers them. A context that wired identity or
        // storage differently from the app would be a context in which the interesting half —
        // which store a signed-in visitor's character goes to — is decided here rather than
        // there.
        Services.AddScoped<Accounts>();
        Services.AddScoped<Invitations>();
        Services.AddScoped<ErrorLog>();
        Services.AddScoped<IIdentitySource>(s => s.GetRequiredService<Accounts>());
        Services.AddScoped<CharacterStore>();
        // The plural browser-side store. Registered here as well as in Program.cs because
        // ApiCharacterStore takes it — the account's store reads *which* character is open out of
        // this browser, since that is a fact about the tab rather than something another device
        // should decide. Leaving it out made every render test in the project fail at once, which
        // is at least the loud kind of wrong.
        Services.AddScoped<SavedCharacters>();
        Services.AddScoped<ApiCharacterStore>();
        Services.AddScoped<CharacterImport>();
        Services.AddScoped<AccountCharacterStore>();
Services.AddScoped<DiscardedCharacter>();
        Services.AddScoped<ICharacterStore>(s => s.GetRequiredService<AccountCharacterStore>());
        Services.AddScoped<RulebookReader>();

        // Every call into motion.js, with its failures swallowed. Registered here so a render
        // test exercises the same guarded path the app does rather than a bare IJSRuntime.
        Services.AddScoped<Motion>();

        // The command palette: what it offers, and the guarded calls into palette.js. Both are
        // registered for every render rather than only for the palette's own tests, because
        // the Powers section asks for a requested Power on each pass and the step above it
        // reads the same service — a component that could not resolve them would throw out of
        // renders that have nothing to do with the palette.
        Services.AddScoped<Commands>();
        Services.AddScoped<Shortcuts>();
        Services.AddScoped<Theme>();

        // The Home/End guard on every rank slider. Registered here for the same reason as the
        // three above it: a row that could not resolve it would throw on every render, not only
        // on a test written about the guard.
        Services.AddScoped<Sliders>();

        // The mode switch and the sample loader both call into JS. Loose mode records the
        // calls and answers nothing, which is right here: what those calls do to the
        // document is the browser's business, not a component's.
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Registered *after* bUnit's own, so this is the one resolved. Everything that is not
        // `ppStore.*` falls through to the same do-nothing answer bUnit's loose mode gives, which
        // is what the guarded services above already expect from a browser that will not play.
        if (storesForReal)
        {
            Storage = new FakeLocalStorage();
            Services.AddSingleton<IJSRuntime>(Storage);
        }

        Session = Services.GetRequiredService<CharacterSession>();

        // Program.cs subscribes this, and it has to be here too or the tests cannot see the
        // half of persistence that matters: a save fires on every change, so anything that
        // clears storage is racing a write nobody awaits. Without the subscription a test
        // asserting on that ordering asserts on nothing.
        var store = Services.GetRequiredService<ICharacterStore>();
        Session.Changed += () => _ = SaveThenAnnounce(Session.Version);

        // Mirrors Program.cs: NotifySaved fires once the write-through actually completes,
        // rather than on the edit that started it, and carries the version that was current
        // when this particular save began — see CharacterSession.Saved for why. A test
        // rendering MainLayout's "Saved" text through a real character mutation, rather than by
        // calling NotifySaved by hand, needs this wired the same way the app wires it.
        async Task SaveThenAnnounce(int version)
        {
            await store.SaveAsync(Session.Sheet, Session.Mode);
            Session.NotifySaved(version);
        }
    }

    /// <summary>Loads a sample so a rendered sheet has something in every section.</summary>
    public RenderContext With(SheetMode mode)
    {
        Session.LoadSample(mode);
        return this;
    }

    /// <summary>
    /// Sign in as somebody who looks after the site, which is who the demonstrations are for now.
    ///
    /// <para><b>The recordings and the two samples moved behind the account pages</b>, so a test
    /// that renders one has to be the person who can see it or it renders a refusal. That is the
    /// gate working; asserting on the refusal by accident is the failure to avoid, which is why
    /// this is a named step a test takes rather than a default the context applies.</para>
    /// </summary>
    public RenderContext AsAdministrator()
    {
        Api.SignedIn = ("acct_administrator", "Someone");
        Api.ManagesInvitations = true;
        return this;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (no .sln found above {AppContext.BaseDirectory}).");
    }
}
