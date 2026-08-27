using Microsoft.JSInterop;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// A browser's local storage, in a dictionary.
///
/// <para>It exists so the tests drive <see cref="CharacterStore.SaveAsync"/> and
/// <see cref="CharacterStore.LoadAsync"/> themselves. They used to go through a static
/// helper on the store that serialized and deserialized in one call — which meant the
/// methods the app actually runs had no coverage at all, the schema-version gate could not
/// fail (both sides used the same constant), and the mode was not even carried.</para>
/// </summary>
public sealed class FakeLocalStorage : IJSRuntime
{
    private readonly Dictionary<string, string> _items = new(StringComparer.Ordinal);

    /// <summary>Set to throw from every call, as a browser with storage disabled does.</summary>
    public bool Refuses { get; set; }

    private readonly List<(string Identifier, string Key)> _calls = [];

    /// <summary>Writes a value behind the store's back, to stand in for hand-edited storage.</summary>
    public void Poke(string key, string value) => _items[key] = value;

    public string? Peek(string key) => _items.GetValueOrDefault(key);

    /// <summary>
    /// Every call that reached here, in order, as identifier and the key it named.
    ///
    /// <para><b>Order is the thing several of these tests are actually about</b>, and it cannot be
    /// read off the end state: a write and a clear of the same key leave the same dictionary
    /// whichever way round they happened, and which way round they happened is exactly the
    /// difference between keeping a character and losing one. bUnit's own
    /// <c>JSInterop.Invocations</c> records this for a component test, but only while bUnit's
    /// runtime is the one wired up — a context that swapped this in for it would have no
    /// recorder at all without this list.</para>
    /// </summary>
    public IReadOnlyList<(string Identifier, string Key)> Calls => _calls;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (Refuses) throw new JSException("Storage is disabled in this browser.");

        var key = args is [string k, ..] ? k : "";

        _calls.Add((identifier, key));

        switch (identifier)
        {
            case "ppStore.save" when args is [_, string value]:
                _items[key] = value;
                return default;

            case "ppStore.clear":
                _items.Remove(key);
                return default;

            case "ppStore.load":
                // The store asks for a string?; hand it back through the generic signature.
                // A miss falls to `default`, which is a completed ValueTask carrying null —
                // what localStorage.getItem answers for a key that is not there. Writing it
                // as `FromResult(... : default!)` instead makes the whole conditional
                // maybe-null and the ValueTask's type argument stops matching.
                return _items.GetValueOrDefault(key) is TValue stored ? ValueTask.FromResult(stored) : default;

            default:
                return default;
        }
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);
}
