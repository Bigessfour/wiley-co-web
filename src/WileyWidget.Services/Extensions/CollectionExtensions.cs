using System.Collections.Generic;
using System.Linq;

namespace WileyWidget.Services.Extensions;

/// <summary>
/// Extension members for collections to provide convenient properties and methods.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Gets a value indicating whether the collection is empty.
    /// </summary>
    public static bool IsEmpty<T>(this IEnumerable<T> source) => source == null || !source.Any();

    /// <summary>
    /// Filters the collection to exclude null values and applies the predicate.
    /// </summary>
    public static IEnumerable<TSource> WhereNotNull<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        where TSource : class
        => source.Where(static x => x is not null).Where(predicate);
}
