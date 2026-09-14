using System;
using System.Linq;
using Avalonia.Collections;

namespace MIN.Desktop.Infrastructure.Extensions;

/// <summary>
/// Расширения для <see cref="AvaloniaList{T}"/>
/// </summary>
public static class AvaloniaListExtensions
{
    /// <summary>
    /// Отсортировать по критерию
    /// </summary>
    public static void SortBy<T, TKey>(this AvaloniaList<T> list, Func<T, TKey> keySelector)
    {
        var sorted = list.OrderBy(keySelector).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var item = sorted[i];
            var currentIndex = list.IndexOf(item);
            if (currentIndex != i)
            {
                list.Move(currentIndex, i);
            }
        }
    }
}
