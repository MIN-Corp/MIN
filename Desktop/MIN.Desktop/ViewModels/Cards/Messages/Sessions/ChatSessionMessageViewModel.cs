using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Stores.Contracts.Models;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Cards.Messages.Base;
using MIN.Desktop.ViewModels.Modals;
using MIN.Sessions.Core.DI.FeatureCollection;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Enums;

namespace MIN.Desktop.ViewModels.Cards.Messages.Sessions;

/// <summary>
/// Сообщение сессии участника
/// </summary>
public partial class ChatSessionMessageViewModel : BaseUpdateableReplyableChatMessageViewModel
{
    private readonly IDialogService dialogService;
    private readonly int? maximumParticipants;
    private readonly ParticipantInfo localParticipant;
    private readonly IClipboard? clipboard;
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
    /// Локальный пользователь — хост комнаты
    /// </summary>
    public bool IsLocalRoomHost { get; }

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
    /// Участвует ли пользователь уже в сессии
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LeaveSessionCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopSessionCommand))]
    public partial bool IsJoined { get; set; }

    /// <summary>
    /// Доступна ли сессия
    /// </summary>
    [ObservableProperty]
    public partial bool IsNotAvailable { get; set; }

    /// <summary>
    /// Является ли сессия туннелем
    /// </summary>
    [ObservableProperty]
    public partial bool IsTunnel { get; set; }

    /// <summary>
    /// Ожидание подключения игры (туннель)
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LeaveSessionCommand))]
    public partial bool IsWaitingForGame { get; set; }

    /// <summary>
    /// Информация о подключения к туннелю
    /// </summary>
    [ObservableProperty]
    public partial string TunnelConnectInfo { get; set; } = string.Empty;

    /// <summary>
    /// Подсказка для хоста туннеля: какой порт открыть
    /// </summary>
    [ObservableProperty]
    public partial string TunnelHostInfo { get; set; } = string.Empty;

    /// <summary>
    /// Получена информация о подключения к туннелю
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTunnelConnectVisible))]
    private partial bool TunnelConnectInfoAvailable { get; set; }

    /// <summary>
    /// Адрес подключения к туннелю
    /// </summary>
    [ObservableProperty]
    private partial string TunnelConnectAddress { get; set; } = string.Empty;

    /// <summary>
    /// Сессия была остановлена (можно запустить заново)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHostTunnelInfoVisible))]
    private partial bool IsSessionStopped { get; set; } = true;

    /// <summary>
    /// Показывать ли хосту туннеля подсказку о порте
    /// </summary>
    public bool IsHostTunnelInfoVisible => IsTunnel && IsLocal && !IsSessionStopped;

    /// <summary>
    /// Показывать ли участнику панель подключения
    /// </summary>
    public bool IsTunnelConnectVisible => TunnelConnectInfoAvailable && !IsLocal;

    /// <summary>
    /// Событие, возникающее по нажатию на кнопку присоединиться
    /// </summary>
    public event Func<Task>? OnJoinRequested;

    /// <summary>
    /// Пользователь покинул сессию
    /// </summary>
    public event Func<Task>? OnLeaveRequested;

    /// <summary>
    /// Хост остановил сессию
    /// </summary>
    public event Func<Task>? OnShutdownRequested;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatSessionMessageViewModel"/>
    /// </summary>
    public ChatSessionMessageViewModel(ISessionFeatureCollection sessionFeatureCollection,
        RoomContext? roomContext,
        IEventScope roomScope,
        IEventBus eventBus,
        IDialogService dialogService,
        IClipboard? clipboard,
        SessionReadyMessage sessionReadyMessage,
        ParticipantInfo localParticipant,
        Thickness timePadding,
        bool isLocalRoomHost,
        bool isHostMessage,
        bool removeHeaders,
        bool isAvailableForNetwork)
        : base(sessionReadyMessage,
            sessionReadyMessage,
            null,
            dialogService,
            sessionReadyMessage.Sender.Name,
            timePadding,
            localParticipant.Id == sessionReadyMessage.SenderId,
            isHostMessage,
            removeHeaders,
            isAvailableForNetwork)
    {
        this.dialogService = dialogService;
        this.localParticipant = localParticipant;
        this.clipboard = clipboard;

        IsLocalRoomHost = isLocalRoomHost;
        SessionMessage = sessionReadyMessage;

        currentAmount = sessionReadyMessage.CurrentParticipantAmount;
        maximumParticipants = sessionReadyMessage.Session.MaximumParticipants;
        asDownloaded = sessionFeatureCollection.SessionScanner.DownloadedSessions.ContainsKey(sessionReadyMessage.Session.SessionId);

        IsTunnel = SessionMessage.Session.Kind == SessionKind.PortTunnel;

        IsSessionStopped = IsLocal && roomContext?.SubRooms.GetSubRoom(sessionReadyMessage.SubRoomId)?.IsActive == false;

        FillLabels();
        SubscribeToEvents(roomScope, eventBus);
    }

    private void SubscribeToEvents(IEventScope roomScope, IEventBus eventBus)
    {
        Subscriptions.Add(eventBus.Subscribe<SessionRescanCompletedEvent>(OnSessionRescanCompleted));
        Subscriptions.Add(eventBus.Subscribe<ErrorOccurredEvent>(OnLocalErrorOccurred));

        Subscriptions.Add(roomScope.Subscribe<SessionProcessStartedEvent>(OnSessionProcessStarted));
        Subscriptions.Add(roomScope.Subscribe<SessionProcessEndedEvent>(OnSessionProcessEnded));
        Subscriptions.Add(roomScope.Subscribe<SessionDeactivatedEvent>(OnSessionDeactivated));
        Subscriptions.Add(roomScope.Subscribe<SessionParticipantJoinedEvent>(OnSessionParticipantJoined));
        Subscriptions.Add(roomScope.Subscribe<SessionParticipantLeftEvent>(OnSessionParticipantLeft));

        Subscriptions.Add(roomScope.Subscribe<TunnelListenerStartedEvent>(OnTunnelListenerStarted));
        Subscriptions.Add(roomScope.Subscribe<TunnelGameConnectFailedEvent>(OnTunnelGameConnectFailed));
    }

    private Task OnLocalErrorOccurred(ErrorOccurredEvent eventMessage, CancellationToken cancellationToken)
    {
        if (!IsWaitingForGame)
        {
            return Task.CompletedTask;
        }

        IsWaitingForGame = false;
        TunnelConnectInfoAvailable = false;
        UpdateStats();
        return Task.CompletedTask;
    }

    private Task OnSessionRescanCompleted(SessionRescanCompletedEvent eventMessage, CancellationToken cancellationToken)
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

        IsSessionStopped = false;

        if (IsLocal && !IsTunnel)
        {
            IsJoined = true;
            IsWaitingForGame = false;
            InAppNotifier.Info($"Идёт запуск сессии \"{eventMessage.Session.Name}\"... Это может занять некоторое время");
        }

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
        IsWaitingForGame = false;
        UpdateStats();
        return Task.CompletedTask;
    }

    private Task OnSessionDeactivated(SessionDeactivatedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.SubRoomId != SessionMessage.SubRoomId)
        {
            return Task.CompletedTask;
        }

        IsSessionStopped = true;
        IsJoined = false;
        IsWaitingForGame = false;
        currentAmount = 0;
        UpdateStats();
        return Task.CompletedTask;
    }

    private Task OnSessionParticipantJoined(SessionParticipantJoinedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.SubRoomId != SessionMessage.SubRoomId || IsSessionStopped)
        {
            return Task.CompletedTask;
        }

        if (eventMessage.Participant.Id == localParticipant.Id)
        {
            IsJoined = true;
            IsWaitingForGame = false;
        }

        currentAmount++;

        UpdateStats();
        return Task.CompletedTask;
    }

    private Task OnSessionParticipantLeft(SessionParticipantLeftEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.SubRoomId != SessionMessage.SubRoomId || IsSessionStopped)
        {
            return Task.CompletedTask;
        }


        if (eventMessage.Participant.Id == localParticipant.Id)
        {
            IsWaitingForGame = false;
            IsJoined = false;
        }

        currentAmount--;
        UpdateStats();
        return Task.CompletedTask;
    }

    private Task OnTunnelListenerStarted(TunnelListenerStartedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.SubRoomId != SessionMessage.SubRoomId)
        {
            return Task.CompletedTask;
        }

        IsWaitingForGame = true;
        TunnelConnectInfoAvailable = true;
        TunnelConnectAddress = $"127.0.0.1:{eventMessage.Port}";

        UpdateStats();
        return Task.CompletedTask;
    }

    private Task OnTunnelGameConnectFailed(TunnelGameConnectFailedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.SubRoomId != SessionMessage.SubRoomId)
        {
            return Task.CompletedTask;
        }

        IsWaitingForGame = false;
        TunnelConnectInfoAvailable = false;
        InAppNotifier.Error($"Не удалось подключиться к сессии: {eventMessage.Reason}");
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

        TunnelConnectInfo = $"Запустите игру и подключитесь к {TunnelConnectAddress}";

        JoinButtonStateText = (IsSessionStopped && IsLocal ? "Запустить заново"
           : IsTunnel && IsLocal ? "Остановить сессию"
           : IsWaitingForGame ? "Отменить подключение"
           : IsJoined ? "Покинуть сессию"
           : !asDownloaded && !IsTunnel ? "Скачать сессию"
           : isFull ? "Заполнено" : "Присоединиться") + $" (Участвуют: {participantsRatio})";

        TunnelHostInfo = IsJoined
            ? "Вы в сессии (сервер игры обнаружен)"
            : $"Запустите игру и откройте сервер на порту {SessionMessage.Session.TunnelPort}";

        CanJoin = IsTunnel && IsLocal || IsWaitingForGame || !isFull;
        IsNotAvailable = (!IsTunnel || !IsLocal) && (IsTunnel ? isFull
            : currentAmount <= 0 || isFull);
    }

    [RelayCommand]
    private async Task JoinSession()
    {
        if (IsSessionStopped && IsLocal)
        {
            OnJoinRequested?.Invoke();
            return;
        }

        if (IsTunnel && IsLocal)
        {
            OnShutdownRequested?.Invoke();
            return;
        }

        if (IsWaitingForGame)
        {
            IsWaitingForGame = false;
            TunnelConnectInfoAvailable = false;
            UpdateStats();
            OnLeaveRequested?.Invoke();
            return;
        }

        if (IsJoined)
        {
            OnLeaveRequested?.Invoke();
            return;
        }

        if (IsTunnel)
        {
            CanJoin = false;
            IsWaitingForGame = true;
            OnJoinRequested?.Invoke();
            _ = ResetWaitingAfterTimeoutAsync();
            return;
        }

        if (asDownloaded)
        {
            CanJoin = false;
            IsWaitingForGame = true;
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

    [RelayCommand]
    private async Task CopyTunnelAddress()
    {
        if (clipboard == null)
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(TunnelConnectAddress);
            var previous = TunnelConnectInfo;
            TunnelConnectInfo = "Скопировано!";
            await Task.Delay(1000);
            TunnelConnectInfo = previous;
        }
        catch
        {
            // ignored
        }
    }

    [RelayCommand(CanExecute = nameof(CanLeave))]
    private Task LeaveSession()
       => OnLeaveRequested?.Invoke() ?? Task.CompletedTask;

    private bool CanLeave() => IsJoined && IsAvailableForNetwork;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopSession()
           => OnShutdownRequested?.Invoke() ?? Task.CompletedTask;

    private bool CanStop() => IsAvailableForNetwork
           && (IsTunnel ? IsLocal
               : IsLocalRoomHost && (currentAmount > 0 || IsWaitingForGame));

    private async Task ResetWaitingAfterTimeoutAsync()
    {
        await Task.Delay(30_000);

        if (!IsWaitingForGame)
        {
            return;
        }

        IsWaitingForGame = false;
        TunnelConnectInfoAvailable = false;
        UpdateStats();
    }
}
