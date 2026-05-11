public static class EnumerationUtility
{
    /// <summary>
    /// A unified Internal Iterator that handles setup, traversal, and teardown.
    /// </summary>
    public static void Enumerate<T>(
        IEnumerable<T> items,
        Action<T> action,
        Action? before = null,
        Action? after = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(action);

        before?.Invoke();

        foreach (var item in items)
        {
            action(item);
        }

        after?.Invoke();
    }
}