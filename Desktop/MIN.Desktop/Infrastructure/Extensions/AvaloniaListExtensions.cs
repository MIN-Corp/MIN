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
        var orderChanged = sorted.Where((item, i) => !ReferenceEquals(list[i], item)).Any();
        if (!orderChanged)
        {
            return;
        }
        list.Clear();
        list.AddRange(sorted);
    }

    /// <summary>
    /// Очистить список, освободив ресурсы элементов
    /// </summary>
    public static void ClearAndDispose<T>(this AvaloniaList<T> list) where T : IDisposable
    {
        foreach (var item in list)
        {
            item.Dispose();
        }
        list.Clear();
    }
}
