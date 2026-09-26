using System;
using System.Threading.Tasks;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.ViewModels.Cards.Messages.Base;

namespace MIN.Desktop.ViewModels.Cards.Messages;

/// <summary>
/// Базовая view модель текстового сообщения, способное отредактироваться
/// </summary>
public abstract partial class BaseTextContentChatMessageViewModel : BaseReplyableChatMessageViewModel
{
    /// <summary>
    /// Идёт редактирование
    /// </summary>
    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    /// <summary>
    /// Сообщение уже отредактировано
    /// </summary>
    [ObservableProperty]
    public partial bool IsEdited { get; set; }

    /// <summary>
    /// Новый контент
    /// </summary>
    [ObservableProperty]
    public partial string EditContent { get; set; } = string.Empty;

    /// <summary>
    /// Текущий контент
    /// </summary>
    [ObservableProperty]
    public partial string Content { get; set; } = string.Empty;

    /// <summary>
    /// Пользователь захотел отредактировать сообщение
    /// </summary>
    public Func<string, Task>? OnEditRequested;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BaseTextContentChatMessageViewModel"/>
    /// </summary>
    public BaseTextContentChatMessageViewModel() { }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BaseChatMessageViewModel"/>
    /// </summary>
    public BaseTextContentChatMessageViewModel(IMessage message,
        IContentEditable contentEditable,
        IReplyable replyable,
        IDialogService dialogService,
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
        Content = contentEditable.Content;
        IsEdited = contentEditable.IsUpdated;
    }

    /// <summary>
    /// Пришла новая версия сообщения
    /// </summary>
    public void MessageEdited(string newContent)
    {
        Content = newContent;
        IsEdited = true;
        IsEditing = false;
    }

    /// <summary>
    /// Сообщение отредактировано
    /// </summary>
    [RelayCommand]
    protected virtual async Task ConfirmEditMessage()
    {
        if (!IsEditing)
        {
            return;
        }

        if (EditContent == string.Empty)
        {
            await DeleteMessage();
            return;
        }

        EditContent = EditContent.Trim();

        if (Content != EditContent)
        {
            OnEditRequested?.Invoke(EditContent);
        }
        else
        {
            IsEditing = false;
        }
    }

    /// <summary>
    /// Отредактировать сообщение
    /// </summary>
    [RelayCommand]
    protected virtual void ToggleEditingMessage()
    {
        if (IsEditing)
        {
            CancelEditMessage();
            return;
        }

        EditContent = Content;
        IsEditing = true;
    }

    /// <summary>
    /// Остановить редактирование сообщения
    /// </summary>
    [RelayCommand]
    protected virtual void CancelEditMessage()
    {
        if (!IsEditing)
        {
            return;
        }

        IsEditing = false;
    }
}
