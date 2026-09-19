using System;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Desktop.Contracts.Interfaces;

namespace MIN.Desktop.ViewModels.Cards.Messages.Base;

/// <summary>
/// Базовая view модель любого сообщения
/// </summary>
public abstract partial class BaseReplyableChatMessageViewModel : BaseChatMessageViewModel
{
    /// <summary>
    /// Идентификатор сообщения, на которое дан ответ
    /// </summary>
    public Guid? ReplyToMessageId;

    /// <summary>
    /// Описание того сообщение, на которое это сообщение послужило ответом на него
    /// </summary>
    [ObservableProperty]
    public partial string? ReplyToDescription { get; set; }

    /// <summary>
    /// Сообщение выбрано в качестве создания на него ответа
    /// </summary>
    public Action? OnReplyRequested;

    /// <summary>
    /// Это сообщение имеет ответ
    /// </summary>
    public bool HasReply => ReplyToMessageId != null;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BaseChatMessageViewModel"/>
    /// </summary>
    public BaseReplyableChatMessageViewModel() { }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BaseChatMessageViewModel"/>
    /// </summary>
    public BaseReplyableChatMessageViewModel(IMessage message,
       IReplyable? replyable,
       IDialogService dialogService,
       string name,
       Thickness timePadding,
       bool isLocal,
       bool isHost,
       bool removeHeaders,
       bool isAvaibleForNetwork)
        : base(message,
            dialogService,
            name,
            timePadding,
            isLocal,
            isHost,
            removeHeaders,
            isAvaibleForNetwork)
    {
        ReplyToDescription = replyable?.ReplyToMessageDescription;
        ReplyToMessageId = replyable?.ReplyToMessageId;
    }

    [RelayCommand]
    private void SetAsReply()
    {
        OnReplyRequested?.Invoke();
    }

    /// <summary>
    /// Пометить сообщение как удалённое
    /// </summary>
    public void ResetReplyAsDeleted()
    {
        ReplyToMessageId = null;
        ReplyToDescription = "Сообщение было удалено";
    }

    /// <summary>
    /// Обновить описание
    /// </summary>
    public void SetNewDescription(string? description)
    {
        ReplyToDescription = description;
    }
}
