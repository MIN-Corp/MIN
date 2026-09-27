using System;
using MIN.Desktop.ViewModels.Pages.ChatViewModels;

namespace MIN.Desktop.Contracts.Interfaces;

/// <summary>
/// Сервис, хранящий UI представления комнат
/// </summary>
public interface IChatViewsRegistry
{
    /// <summary>
    /// Зарегистрировать комнату
    /// </summary>
    void Register(Guid roomId, ChatViewModel vm);

    /// <summary>
    /// Отрегистрировать комнату
    /// </summary>
    void Unregister(Guid roomId);

    /// <summary>
    /// Попытаться получить комнату
    /// </summary>
    bool TryGet(Guid roomId, out ChatViewModel? vm);
}
