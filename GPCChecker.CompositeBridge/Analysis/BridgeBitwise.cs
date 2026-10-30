using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace GPC.Checkers.CompositeBridge;

/// <summary>
/// Keys of the input records of the bridge engine that are equal only for values equal bit for bit (doubles by their bits, so 0 and -0 or
/// two NaN payloads differ; strings ordinal; nested records, arrays and lists element by element). Other objects, as the Model materials,
/// are compared by reference: they are shared and must not be mutated during an analysis (<see cref="HBridgeInput"/>)
/// </summary>
internal sealed class BridgeBitwise
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();
    private readonly Dictionary<object, int> identities = new(new ReferenceIdentity());
    private readonly StringBuilder text = new();

    /// <summary>The key of a value; an excluded property of the top object (null: none) is left out</summary>
    internal string Key(object? value, string? excluded = null)
    {
        text.Clear();
        Append(value, excluded);
        return text.ToString();
    }

    private void Append(object? value, string? excluded = null)
    {
        switch (value)
        {
            case null: text.Append('~'); return;
            case double d: text.Append('d').Append(BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture)); return;
            case float f: text.Append('f').Append(BitConverter.DoubleToInt64Bits(f).ToString("X16", CultureInfo.InvariantCulture)); return;
            case string s: text.Append('s').Append(s.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(s); return;
            case bool b: text.Append(b ? 'T' : 'F'); return;
            case Enum e: text.Append('e').Append(Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)); return;
            case int or long or short or byte: text.Append('i').Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)); return;
        }
        var type = value.GetType();
        if (value is IEnumerable items)
        {
            text.Append('[');
            foreach (var item in items) { Append(item); text.Append(','); }
            text.Append(']');
            return;
        }
        if (type.Namespace != typeof(BridgeBitwise).Namespace)
        {
            // Model objects and anything else: identity, numbered in the order of first appearance
            if (!identities.TryGetValue(value, out int id)) identities.Add(value, id = identities.Count);
            text.Append('@').Append(id.ToString(CultureInfo.InvariantCulture));
            return;
        }
        text.Append('{').Append(type.FullName).Append(':');
        foreach (var property in Properties.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0).OrderBy(p => p.Name, StringComparer.Ordinal).ToArray()))
        {
            if (property.Name == excluded) continue;
            text.Append(property.Name).Append('=');
            Append(property.GetValue(value));
            text.Append(';');
        }
        text.Append('}');
    }

    private sealed class ReferenceIdentity : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
