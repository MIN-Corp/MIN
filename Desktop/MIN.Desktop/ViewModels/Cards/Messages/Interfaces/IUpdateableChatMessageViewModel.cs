using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Desktop.ViewModels.Cards.Messages.Interfaces;

/// <summary>
/// Карточка сообщения, способная обновиться при редактировании/переименовании автора
/// </summary>
public interface IUpdateableChatMessageViewModel
{
    /// <summary>
    /// Обновить сообщение
    /// </summary>
    void Update(IMessage newMessage);
}
