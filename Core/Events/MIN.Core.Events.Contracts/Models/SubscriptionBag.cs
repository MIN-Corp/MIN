namespace MIN.Core.Events.Contracts.Models;

/// <summary>
/// Мешок с токенами подписок для событий, который удобно Dispose
/// </summary>
public sealed class SubscriptionBag : IDisposable
{
    private readonly List<IDisposable> tokens = [];
    private bool disposed;

    /// <summary>
    /// Добавить токен подписки
    /// </summary>
    public IDisposable Add(IDisposable token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        tokens.Add(token);
        return token;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (var token in tokens)
        {
            try
            {
                token.Dispose();
            }
            catch { }
        }
        tokens.Clear();
    }
}
