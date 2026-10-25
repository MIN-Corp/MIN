namespace MIN.Common.Core.Extensions;

/// <summary>
/// Методы расширения для списков
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// Убирает все элементы, прошедшие кастомный маппинг по функции
    /// </summary>
    /// <param name="list">Список</param>
    /// <param name="extraParameter">Дополнительный параметр в функцию</param>
    /// <param name="predicate">Предикат, который вызывается для каждого элемента</param>
    public static void RemoveByPredicate<TItem, TParameter>(this IList<TItem> list, TParameter extraParameter, Func<TItem, TParameter, bool> predicate)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            TItem item = list[i];
            if (!predicate.Invoke(item, extraParameter))
            {
                continue;
            }

            // This reduces GC pressure for resizing arrays.
            list[i] = list[^1];
            list.RemoveAt(list.Count - 1);
        }
    }

    /// <summary>
    /// Получить индекс искомого элемента массива
    /// </summary>
    /// <typeparam name="T">Тип искомого элемента</typeparam>
    /// <param name="list">Список</param>
    /// <param name="itemToFind">Экземпляр искомого элемента</param>
    /// <returns>Индекс искомого элемента</returns>
    public static int GetIndex<T>(this T[] list, T itemToFind) => Array.IndexOf(list, itemToFind);
}
