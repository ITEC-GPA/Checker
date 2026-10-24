namespace GPC.Design.Compatibility;

internal static class CollectionExtensions
{
    internal static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key, TValue value)
    {
        Guard.NotNull(source);
        if (source.ContainsKey(key)) return false;
        source.Add(key, value); return true;
    }

    internal static TValue? GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> source, TKey key)
    {
        Guard.NotNull(source);
        return source.TryGetValue(key, out var value) ? value : default;
    }

    internal static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> source, TKey key, TValue defaultValue)
    {
        Guard.NotNull(source);
        return source.TryGetValue(key, out var value) ? value : defaultValue;
    }

    internal static HashSet<T> ToHashSet<T>(this IEnumerable<T> source) => new(source);

    internal static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
    {
        Guard.NotNull(source); Guard.NotNull(keySelector);
        return Iterate();
        IEnumerable<T> Iterate()
        {
            var seen = new HashSet<TKey>();
            foreach (var item in source) if (seen.Add(keySelector(item))) yield return item;
        }
    }

    internal static T? MinBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector) => ExtremeBy(source, keySelector, false);
    internal static T? MaxBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector) => ExtremeBy(source, keySelector, true);

    private static T? ExtremeBy<T, TKey>(IEnumerable<T> source, Func<T, TKey> keySelector, bool maximum)
    {
        Guard.NotNull(source); Guard.NotNull(keySelector);
        using var items = source.GetEnumerator();
        if (!items.MoveNext())
        {
            if (default(T) is null) return default;
            throw new InvalidOperationException("Sequence contains no elements");
        }
        T best = items.Current; TKey key = keySelector(best);
        var comparer = Comparer<TKey>.Default;
        while (items.MoveNext())
        {
            var nextKey = keySelector(items.Current);
            if (nextKey is null) continue;
            int comparison = key is null ? (maximum ? 1 : -1) : comparer.Compare(nextKey, key);
            if (maximum ? comparison > 0 : comparison < 0) { best = items.Current; key = nextKey; }
        }
        return best; // Equal keys retain the first item; one pass and constant auxiliary storage.
    }

    internal static IEnumerable<T> SkipLast<T>(this IEnumerable<T> source, int count)
    {
        Guard.NotNull(source);
        return count <= 0 ? source : Iterate();
        IEnumerable<T> Iterate()
        {
            var pending = new Queue<T>();
            foreach (var item in source)
            {
                pending.Enqueue(item);
                if (pending.Count > count) yield return pending.Dequeue();
            }
        }
    }
}
