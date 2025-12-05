namespace MediatorCore;

public static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<Task<T>> tasks)
    {
        foreach (var task in tasks)
        {
            yield return await task;
        }
    }
}
