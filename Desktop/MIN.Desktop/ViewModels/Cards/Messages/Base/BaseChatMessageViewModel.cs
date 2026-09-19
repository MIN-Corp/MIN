using System;
using System.Threading.Tasks;
using Avalonia;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Modals;

namespace MIN.Desktop.ViewModels.Cards.Messages;

/// <summary>
/// Базовая view модель любого сообщения
/// </summary>
public abstract partial class BaseChatMessageViewModel : CardViewModelBase
{
    private readonly IDialogService? dialogService;

    /// <summary>
    /// Сообщение
    /// </summary>
    public IMessage? Message { get; }

    /// <summary>
    /// Имя отправителя сообщения
    /// </summary>
    public string SenderName { get; } = string.Empty;

    /// <summary>
    /// Время отправления
    /// </summary>
    public string Timestamp { get; }

    /// <summary>
    /// Отправитель = хост
    /// </summary>
    public bool IsHost { get; }

    /// <summary>
    /// Отправитель = ты
    /// </summary>
    public bool IsLocal { get; }

    /// <summary>
    /// Нужно ли убрать заголовки
    /// </summary>
    /// <remarks>
    /// Например если сообщение повторяется одним и тем же участником
    /// </remarks>
    public bool RemoveHeaders { get; }

    /// <summary>
    /// Может ли пользователь интерактировать с сообщением
    /// </summary>
    public bool IsAvaibleForNetwork { get; }

    /// <summary>
    /// Приватное ли сообщение
    /// </summary>
    public bool IsPrivate { get; init; }

    /// <summary>
    /// Выбрано ли сообщение
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Margin между сообщениями по прошедшему времени
    /// </summary>
    public Thickness TimePadding { get; }

    /// <summary>
    /// Пользователь захотел удалить сообщение
    /// </summary>
    public Func<Task>? OnDeleteRequested;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BaseChatMessageViewModel"/>
    /// </summary>
    public BaseChatMessageViewModel()
    {
        Timestamp = DateTime.Now.ToShortTimeString();
    }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BaseChatMessageViewModel"/>
    /// </summary>
    public BaseChatMessageViewModel(IMessage message)
    {
        Message = message;
        Timestamp = message.Timestamp.ToShortTimeString();
    }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BaseChatMessageViewModel"/>
    /// </summary>
    public BaseChatMessageViewModel(IMessage message,
       IDialogService? dialogService,
       string name,
       Thickness timePadding,
       bool isLocal,
       bool isHost,
       bool removeHeaders,
       bool isAvaibleForNetwork)
    {
        this.dialogService = dialogService;
        Message = message;
        SenderName = name;
        Timestamp = message.Timestamp.ToShortTimeString();
        TimePadding = timePadding;
        IsLocal = isLocal;
        IsHost = isHost;
        RemoveHeaders = removeHeaders;
        IsPrivate = !message.IsPublic;
        IsAvaibleForNetwork = isAvaibleForNetwork;
    }

    /// <summary>
    /// Удалить сообщение
    /// </summary>
    [RelayCommand]
    protected virtual async Task DeleteMessage()
    {
        if (dialogService == null)
        {
            OnDeleteRequested?.Invoke();
            return;
        }

        bool confirmation = await dialogService.ShowDialogAsync<DialogBoxViewModel>(model =>
        {
            model.Title = "Удаление сообщения";
            model.Description = $"Хотите удалить это сообщение?";
            model.ButtonOptions = ButtonOptions.YesNo;
        });

        if (confirmation)
        {
            OnDeleteRequested?.Invoke();
        }
    }
}
