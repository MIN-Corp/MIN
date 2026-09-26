using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Cards.Messages.Base;
using MIN.Desktop.ViewModels.Modals;
using MIN.Sessions.Core.DI.FeatureCollection;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;

namespace MIN.Desktop.ViewModels.Cards.Messages.Sessions;

/// <summary>
/// Сообщение сессии участника
/// </summary>
public partial class ChatSessionMessageViewModel : BaseReplyableChatMessageViewModel, IDisposable
{
    private readonly IDialogService dialogService = null!;
    private readonly int? maximumParticipants;
    private IDisposable rescanToken = null!;
    private bool asDownloaded;
    private int currentAmount;

    /// <summary>
    /// Содержимое сообщения
    /// </summary>
    public SessionReadyMessage SessionMessage { get; init; }

    /// <summary>
    /// Изображение сессии
    /// </summary>
    public Bitmap? SessionImage { get; set; }

    /// <summary>
    /// Название сессии
    /// </summary>
    [ObservableProperty]
    public partial string SessionTitle { get; set; } = string.Empty;

    /// <summary>
    /// Состояние сессии в виде текста кнопки
    /// </summary>
    [ObservableProperty]
    public partial string JoinButtonStateText { get; set; } = string.Empty;

    /// <summary>
    /// Можно ли зайти
    /// </summary>
    [ObservableProperty]
    public partial bool CanJoin { get; set; }

    /// <summary>
    /// Учавствует ли пользователь уже в сессии
    /// </summary>
    [ObservableProperty]
    public partial bool IsJoined { get; set; }

    /// <summary>
    /// Доступна ли сессия
    /// </summary>
    [ObservableProperty]
    public partial bool IsNotAvailable { get; set; }

    /// <summary>
    /// Событие, возникающее по нажатию на кнопку присоединиться
    /// </summary>
    public event Func<Task>? OnJoinRequested;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatSessionMessageViewModel"/>
    /// </summary>
    public ChatSessionMessageViewModel(ISessionFeatureCollection sessionFeatureCollection,
        IEventScope roomScope,
        IEventBus eventBus,
        IDialogService dialogService,
        SessionReadyMessage sessionReadyMessage,
        ParticipantInfo localParticipant,
        Thickness timePadding,
        bool isHostMessage,
        bool removeHeaders,
        bool isAvaibleForNetwork)
        : base(sessionReadyMessage,
            null,
            dialogService,
            sessionReadyMessage.Sender.Name,
            timePadding,
            localParticipant.Id == sessionReadyMessage.SenderId,
            isHostMessage,
            removeHeaders,
            isAvaibleForNetwork)
    {

        this.dialogService = dialogService;
        SessionMessage = sessionReadyMessage;

        currentAmount = sessionReadyMessage.CurrentParticipantAmount;
        maximumParticipants = sessionReadyMessage.Session.MaximumParticipants;
        asDownloaded = sessionFeatureCollection.SessionScanner.DownloadedSessions.ContainsKey(sessionReadyMessage.Session.SessionId);

        FillLabels();
        SubscribeToEvents(roomScope, eventBus);
    }

    private void SubscribeToEvents(IEventScope roomScope, IEventBus eventBus)
    {
        rescanToken = eventBus.Subscribe<SessionRescanCompletedEvent>(OnSessionRescanCompletedEvent);
        roomScope.Subscribe<SessionProcessStartedEvent>(OnSessionProcessStarted);
        roomScope.Subscribe<SessionProcessEndedEvent>(OnSessionProcessEnded);
        roomScope.Subscribe<SessionParticipantJoinedEvent>(OnSessionParticipantJoined);
        roomScope.Subscribe<SessionParticipantLeftEvent>(OnSessionParticipantLeft);
    }

    private Task OnSessionRescanCompletedEvent(SessionRescanCompletedEvent eventMessage, CancellationToken cancellationToken)
    {
        asDownloaded = eventMessage.DownloadedSessions.ContainsKey(SessionMessage.Session.SessionId);
        UpdateStats();
        return Task.CompletedTask;
    }

    private Task OnSessionProcessStarted(SessionProcessStartedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.SubRoomId != SessionMessage.SubRoomId)
        {
            return Task.CompletedTask;
        }

        IsJoined = true;
        InAppNotifier.Info($"Идёт запуск сессии \"{eventMessage.Session.Name}\"... Это может занять некоторое время");
        UpdateStats();
        return Task.CompletedTask;
    }

    private Task OnSessionProcessEnded(SessionProcessEndedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.SubRoomId != SessionMessage.SubRoomId)
        {
            return Task.CompletedTask;
        }

        IsJoined = false;
        UpdateStats();
        return Task.CompletedTask;
    }

    private Task OnSessionParticipantJoined(SessionParticipantJoinedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.SubRoomId != SessionMessage.SubRoomId)
        {
            return Task.CompletedTask;
        }

        currentAmount++;

        UpdateStats();
        return Task.CompletedTask;
    }

    private Task OnSessionParticipantLeft(SessionParticipantLeftEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.SubRoomId != SessionMessage.SubRoomId)
        {
            return Task.CompletedTask;
        }

        currentAmount--;

        UpdateStats();
        return Task.CompletedTask;
    }

    private void FillLabels()
    {
        if (SessionMessage.ThumbnailData != null)
        {
            using var ms = new MemoryStream(SessionMessage.ThumbnailData);
            SessionImage = new Bitmap(ms);
        }

        SessionTitle = $"{SessionMessage.Session.Name} (v. {SessionMessage.Session.Version})";

        UpdateStats();
    }

    private void UpdateStats()
    {
        var participantsRatio = $"{currentAmount}"
            + (maximumParticipants.HasValue ? $"/{maximumParticipants.Value}" : string.Empty);

        var isFull = maximumParticipants.HasValue && currentAmount >= maximumParticipants.Value;

        JoinButtonStateText = (!asDownloaded
            ? "Скачать сессию"
            : IsJoined ? "Вы учавствуете"
            : isFull ? "Заполнено" : "Присоединиться") + $" (Учавствуют: {participantsRatio})";

        CanJoin = (!maximumParticipants.HasValue || !isFull) && !IsJoined;
        IsNotAvailable = currentAmount <= 0 || isFull;
    }

    [RelayCommand]
    private async Task JoinSession()
    {
        if (asDownloaded)
        {
            CanJoin = false;
            IsJoined = true;
            OnJoinRequested?.Invoke();
            return;
        }

        bool confirmation = await dialogService.ShowDialogAsync<DialogBoxViewModel>(model =>
        {
            model.Title = "Скачивание сессии";
            model.Description = $"Хотите скачать сессию {SessionMessage.Session.Name}?\n" +
            $"Вы будете перенесены по ссылке {SessionMessage.Session.DownloadLink}";
            model.ButtonOptions = ButtonOptions.YesNo;
        });

        if (confirmation)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SessionMessage.Session.DownloadLink,
                UseShellExecute = true
            });
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    void IDisposable.Dispose()
    {
        rescanToken.Dispose();
    }
}
