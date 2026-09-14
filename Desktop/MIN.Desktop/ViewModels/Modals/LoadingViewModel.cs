using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;

namespace MIN.Desktop.ViewModels.Modals;

/// <summary>
/// Модель окна захода в комнаты
/// </summary>
public partial class LoadingViewModel : ModalViewModelBase
{
    private readonly IEventBus eventBus;
    private System.Timers.Timer timeoutTimer = null!;
    private Action<Room?> onRoomReady = null!;
    private CancellationTokenSource cts = null!;

    private IDisposable roomStateToken = null!;
    private IDisposable errorToken = null!;
    private bool gotRoom;

    /// <summary>
    /// Идентификатор комнаты
    /// </summary>
    /// <remarks>
    /// null, если ещё не прошёл этап протокола
    /// </remarks>
    public Guid? RoomId { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="LoadingViewModel"/>
    /// </summary>
    public LoadingViewModel(IEventBus eventBus)
    {
        this.eventBus = eventBus;

        if (!Design.IsDesignMode)
        {
            SubscribeToEvents();
        }
    }

    /// <summary>
    /// Подгрузить данные о комнате и перезагрузить
    /// </summary>
    public Task LoadRoomDataAndRefresh(Action<Room?> onRoomReady,
        CancellationTokenSource cts,
        int timeoutMs = 10000)
    {
        timeoutTimer = new System.Timers.Timer { Interval = timeoutMs };
        timeoutTimer.Elapsed += OnTimeout;
        timeoutTimer.Start();

        this.onRoomReady = onRoomReady;
        this.cts = cts;
        return Task.CompletedTask;
    }

    private void OnTimeout(object? sender, EventArgs e)
    {
        timeoutTimer.Stop();
        onRoomReady.Invoke(null!);

        Dispatcher.UIThread.Post(() =>
        {
            CloseByCode();
            InAppNotifier.Warning("Не удалось подключиться: Время подключения истекло.\nВозможно, комнаты уже и нет");
        });
    }

    private void SubscribeToEvents()
    {
        roomStateToken = eventBus.Subscribe<RoomStateChangedEvent>(OnRoomStateChangedEventReceived);
        errorToken = eventBus.Subscribe<ErrorOccurredEvent>(OnErrorOccured);
    }

    private Task OnErrorOccured(ErrorOccurredEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.RoomId != RoomId)
        {
            return Task.CompletedTask;
        }

        onRoomReady.Invoke(null!);
        Dispatcher.UIThread.Post(() =>
        {
            CloseByCode();
        });
        return Task.CompletedTask;
    }

    private Task OnRoomStateChangedEventReceived(RoomStateChangedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.Room.Id != RoomId)
        {
            return Task.CompletedTask;
        }

        gotRoom = true;
        Dispatcher.UIThread.Post(() =>
        {
            CloseByCode(ButtonOptions.Ok);
            InAppNotifier.Success($"Подключение к комнате {eventMessage.Room.Name} прошло успешно!");
        });
        onRoomReady.Invoke(eventMessage.Room);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Остановить загрузку
    /// </summary>
    [RelayCommand]
    public void StopLoading()
    {
        roomStateToken.Dispose();
        errorToken.Dispose();

        if (!gotRoom)
        {
            cts.Cancel();
        }

        timeoutTimer.Stop();
        timeoutTimer.Dispose();
    }
}
