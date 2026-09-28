using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Desktop.Contracts.Interfaces;

namespace MIN.Desktop.ViewModels.Cards.Messages.Base;

/// <summary>
/// Базовая view модель текстового сообщения, способное отредактироваться и отвечено
/// </summary>
public abstract partial class BaseUpdateableReplyableChatMessageViewModel : BaseReplyableChatMessageViewModel
{
    /// <summary>
    /// Сообщение уже обновлено
    /// </summary>
    [ObservableProperty]
    public partial bool IsUpdated { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BaseUpdateableReplyableChatMessageViewModel"/>
    /// </summary>
    public BaseUpdateableReplyableChatMessageViewModel() { }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BaseUpdateableReplyableChatMessageViewModel"/>
    /// </summary>
    public BaseUpdateableReplyableChatMessageViewModel(IMessage message,
        IUpdateableMessage updateableMessage,
        IReplyable? replyable,
        IDialogService? dialogService,
        string name,
        Thickness timePadding,
        bool isLocal,
        bool isHost,
        bool removeHeaders,
        bool isAvaibleForNetwork)
        : base(message,
            replyable,
            dialogService,
            name,
            timePadding,
            isLocal,
            isHost,
            removeHeaders,
            isAvaibleForNetwork)
    {
        IsUpdated = updateableMessage.IsUpdated;
    }

    /// <summary>
    /// Обновить сообщение
    /// </summary>
    public virtual void Update(IMessage newMessage)
    {
        Message = newMessage;
        IsUpdated = true;
    }
}
