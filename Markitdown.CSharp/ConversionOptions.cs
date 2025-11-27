using System.Collections.Concurrent;

namespace MarkItDown.CSharp;

public sealed class ConversionOptions
{
    public static ConversionOptions Empty => new();

    private readonly ConcurrentDictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

    public ConversionOptions()
    {
    }

    public ConversionOptions(IEnumerable<KeyValuePair<string, object?>> initialValues)
    {
        foreach (var pair in initialValues)
        {
            _values[pair.Key] = pair.Value;
        }
    }

    public T? Get<T>(string key)
    {
        return _values.TryGetValue(key, out var value) && value is T typed ? typed : default;
    }

    public void Set(string key, object? value)
    {
        _values[key] = value;
    }

    public bool Contains(string key) => _values.ContainsKey(key);

    public ConversionOptions CloneAndMerge(params (string key, object? value)[] updates)
    {
        var clone = new ConversionOptions(_values);
        foreach (var (key, value) in updates)
        {
            clone.Set(key, value);
        }

        return clone;
    }

    public IReadOnlyDictionary<string, object?> AsReadOnly() => _values;

    public IEnumerable<KeyValuePair<string, object?>> Entries => _values;
}

