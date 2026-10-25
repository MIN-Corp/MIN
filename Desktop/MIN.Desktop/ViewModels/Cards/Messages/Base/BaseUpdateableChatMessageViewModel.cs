using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.ViewModels.Cards.Messages.Interfaces;

namespace MIN.Desktop.ViewModels.Cards.Messages.Base;

/// <summary>
/// Базовая view модель текстового сообщения, способное отредактироваться и отвечено
/// </summary>
public abstract partial class BaseUpdateableChatMessageViewModel : BaseChatMessageViewModel, IUpdateableChatMessageViewModel
{
    /// <summary>
    /// Сообщение уже обновлено
    /// </summary>
    [ObservableProperty]
    public partial bool IsUpdated { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BaseUpdateableChatMessageViewModel"/>
    /// </summary>
    public BaseUpdateableChatMessageViewModel() { }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BaseUpdateableChatMessageViewModel"/>
    /// </summary>
    public BaseUpdateableChatMessageViewModel(IMessage message,
        IUpdateableMessage updateableMessage,
        IDialogService? dialogService,
        string name,
        Thickness timePadding,
        bool isLocal,
        bool isHost,
        bool removeHeaders,
        bool isAvailableForNetwork)
        : base(message,
            dialogService,
            name,
            timePadding,
            isLocal,
            isHost,
            removeHeaders,
            isAvailableForNetwork)
    {
        IsUpdated = updateableMessage.IsUpdated;
    }

    /// <summary>
    /// Обновить сообщение
    /// </summary>
    public virtual void Update(IMessage newMessage)
    {
        Message = newMessage;
        if (newMessage is IAuthoredMessage authoredMessage)
        {
            SenderName = authoredMessage.SenderName;
        }
        IsUpdated = true;
    }
}
