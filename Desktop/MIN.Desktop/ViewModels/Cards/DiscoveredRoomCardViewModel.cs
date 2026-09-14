using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Desktop.Contracts.Constants;
using MIN.Desktop.ViewModels.Base;

namespace MIN.Desktop.ViewModels.Cards;

/// <summary>
/// Модель карточки комнаты на боковой панели
/// </summary>
public partial class DiscoveredRoomCardViewModel : CardViewModelBase
{
    private readonly RoomInfo room;
    private readonly bool asHost;

    private IDisposable errorToken = null!;
    private bool joined;

    /// <summary>
    /// Идёт ли сейчас заход в комнату
    /// </summary>
    [ObservableProperty]
    public partial bool IsConnecting { get; set; }

    /// <summary>
    /// Идёт ли сейчас заход в комнату
    /// </summary>
    [ObservableProperty]
    public partial bool IsAccessible { get; set; }

    /// <summary>
    /// Модель комнаты
    /// </summary>
    [ObservableProperty]
    public partial RoomInfo Room { get; set; }

    /// <summary>
    /// Адрес соединения
    /// </summary>
    [ObservableProperty]
    public partial AvaloniaList<ConnectionAddressViewModel> ConnectionAddresses { get; set; } = [];

    /// <summary>
    /// Кабинет
    /// </summary>
    [ObservableProperty]
    public partial string Cabinet { get; set; } = string.Empty;

    /// <summary>
    /// Состояние подключения к комнате
    /// </summary>
    [ObservableProperty]
    public partial string ConnectionStatus { get; set; } = string.Empty;

    /// <summary>
    /// Событие по нажатию на присоединения, выбрав способ
    /// </summary>
    public Action<AddressOrigin>? Clicked { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="DiscoveredRoomCardViewModel"/>
    /// </summary>
    public DiscoveredRoomCardViewModel(IEventBus eventBus,
        RoomInfo room,
        IEnumerable<IEndpoint> endpoints,
        bool asHost,
        bool asJoined,
        IClipboard? clipboard)
    {
        this.asHost = asHost;
        this.room = room;

        Room = room;
        joined = asJoined;

        foreach (var endpoint in endpoints)
        {
            ConnectionAddresses.Add(new ConnectionAddressViewModel(endpoint, clipboard));
        }

        Cabinet = string.IsNullOrEmpty(room.Cabinet)
            ? DesktopConstants.UndefinedPcName
            : room.Cabinet;

        ManageConnectButtonAccessability();
        SubscribeToEvents(eventBus);
    }

    /// <summary>
    /// Выбрать карточку
    /// </summary>
    [RelayCommand]
    public void JoinRoom()
    {
        IsConnecting = true;
        Clicked?.Invoke(AddressOrigin.LAN);
    }

    private void SubscribeToEvents(IEventBus eventBus)
    {
        var roomScope = eventBus.CreateScope(room.Id);
        roomScope.Subscribe<ParticipantJoinedEvent>(OnParticipantJoined);
        roomScope.Subscribe<ParticipantLeftEvent>(OnParticipantLeft);
        roomScope.Subscribe<RoomInfoUpdatedMessageEvent>(OnRoomInfoUpdatedMessageEvent);
        roomScope.Subscribe<RoomDestroyedEvent>(OnRoomDestroyed);
        roomScope.Subscribe<RoomJoinedEvent>(OnRoomJoined);
        errorToken = eventBus.Subscribe<ErrorOccurredEvent>(OnErrorOccured);
    }

    private Task OnErrorOccured(ErrorOccurredEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.RoomId != room.Id)
        {
            return Task.CompletedTask;
        }

        IsConnecting = false;
        return Task.CompletedTask;
    }

    private Task OnParticipantJoined(ParticipantJoinedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.IsRejoin)
        {
            return Task.CompletedTask;
        }

        room.ParticipantCount++;
        ManageConnectButtonAccessability();
        return Task.CompletedTask;
    }

    private Task OnParticipantLeft(ParticipantLeftEvent eventMessage, CancellationToken cancellationToken)
    {
        if (!eventMessage.Message.IsLeftRoom)
        {
            return Task.CompletedTask;
        }

        room.ParticipantCount--;
        ManageConnectButtonAccessability();
        return Task.CompletedTask;
    }

    private Task OnRoomDestroyed(RoomDestroyedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (asHost)
        {
            Dispose();
            return Task.CompletedTask;
        }

        joined = false;
        room.ParticipantCount--;
        ManageConnectButtonAccessability();
        return Task.CompletedTask;
    }

    private Task OnRoomJoined(RoomJoinedEvent eventMessage, CancellationToken cancellationToken)
    {
        IsConnecting = false;
        joined = true;

        room.Name = eventMessage.RoomInfo.Name;
        room.MaximumParticipants = eventMessage.RoomInfo.MaximumParticipants;
        room.ParticipantCount = eventMessage.RoomInfo.ParticipantCount;

        ManageConnectButtonAccessability();
        return Task.CompletedTask;
    }

    private Task OnRoomInfoUpdatedMessageEvent(RoomInfoUpdatedMessageEvent eventMessage, CancellationToken ct)
    {
        if (eventMessage.RoomInfo.Id != room.Id)
        {
            return Task.CompletedTask;
        }

        room.Name = eventMessage.RoomInfo.Name;
        room.MaximumParticipants = eventMessage.RoomInfo.MaximumParticipants;
        room.ParticipantCount = eventMessage.RoomInfo.ParticipantCount;

        ManageConnectButtonAccessability();
        return Task.CompletedTask;
    }

    private void ManageConnectButtonAccessability()
    {
        var isFull = room.ParticipantCount >= room.MaximumParticipants;
        var isNotAccessible = isFull || asHost || joined;

        IsAccessible = !isNotAccessible;

        if (isNotAccessible)
        {
            ConnectionStatus = asHost ? "Твоя комната"
                : joined ? "Уже зашёл"
                : isFull ? "Заполнено" : "Не доступно";
        }
        else
        {
            ConnectionStatus = "Присоединиться";
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public override void Dispose()
    {
        errorToken.Dispose();
        base.Dispose();
    }
}
