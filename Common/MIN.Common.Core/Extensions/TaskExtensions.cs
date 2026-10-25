namespace MIN.Common.Core.Extensions;

/// <summary>
/// Методы расширения для Task
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Выбрасывает исключение если результатом таски был <see langword="null"/>
    /// </summary>
    public static Task<TResult> OrThrowIfNull<TResult, TException>(this Task<TResult> task, Func<TException> exception)
        where TException : Exception
        => task.ContinueWith(x => x.Result ?? throw exception.Invoke());

    /// <summary>
    /// Выбрасывает исключение если результатом таски был <see langword="true"/>
    /// </summary>
    public static Task AndThrowIfTrue<TException>(this Task<bool> task, Func<TException> exception)
        where TException : Exception
        => task.ContinueWith(x =>
        {
            ArgumentNullException.ThrowIfNull(x);

            if (x.Result)
            {
                throw exception.Invoke();
            }
        });

    /// <summary>
    /// Выбрасывает исключение если условие по результату таски выполнилось
    /// </summary>
    public static Task<TResult> OrThrowIf<TResult, TException>(this Task<TResult> task,
        Func<TResult, bool> condition,
        Func<TException> exception)
        where TException : Exception
        => task.ContinueWith(x =>
        {
            if (condition.Invoke(x.Result))
            {
                throw exception.Invoke();
            }

            return x.Result;
        });

    /// <summary>
    /// Выбрасывает исключение если условие по результату таски выполнилось
    /// </summary>
    public static Task<TResult> OrThrowIf<TResult, TException>(this Task<TResult> task,
        Func<TResult, bool> condition,
        Func<TResult, TException> exception)
        where TException : Exception
        => task.ContinueWith(x =>
        {
            if (condition.Invoke(x.Result))
            {
                throw exception.Invoke(x.Result);
            }

            return x.Result;
        });
}
