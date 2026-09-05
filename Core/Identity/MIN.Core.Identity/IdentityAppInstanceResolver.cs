namespace MIN.Core.Identity;

/// <summary>
/// Резолвер количества запущенных экземпляров приложения на этом ПК
/// </summary>
public sealed class IdentityAppInstanceResolver : IDisposable
{
    private const int MaxSlots = 64;
    private const string MutexPrefix = "Global\\MIN-Identity-Slot-";

    private readonly Mutex? ownedMutex;

    /// <summary>
    /// Слот приложения
    /// </summary>
    public int Slot { get; private set; } = MaxSlots;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="IdentityAppInstanceResolver"/>
    /// </summary>
    public IdentityAppInstanceResolver()
    {
        for (var slot = 0; slot < MaxSlots; slot++)
        {
            var mutex = new Mutex(initiallyOwned: false, MutexPrefix + slot);
            bool owned;
            try
            {
                owned = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                owned = true; // прежний владелец упал — слот наш
            }
            catch (UnauthorizedAccessException)
            {
                mutex.Close();
                continue;
            }

            if (owned)
            {
                ownedMutex = mutex;
                Slot = slot;
                return;
            }

            mutex.Close();
        }

        // Все слоты заняты: id не будет уникально персистентным (крайний случай).
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => ownedMutex?.Close();
}
