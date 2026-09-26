using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Models;
using MIN.Desktop.Contracts.Constants;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Infrastructure.Extensions;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Cards;
using MIN.Desktop.ViewModels.Modals;
using MIN.Desktop.ViewModels.Pages.ChatViewModels;
using MIN.Desktop.ViewModels.Windows;
using MIN.DI.FeatureCollection;

namespace MIN.Desktop.ViewModels.Pages;

/// <summary>
/// Модель боковой панели чата
/// </summary>
public partial class ChatSideBarViewModel : RoutableViewModelBase
{
    private readonly IMinFeatureCollection featureCollection;
    private readonly IDialogService dialogService;
    private readonly CancellationTokenSource appCts = null!;
    private IClipboard? clipboard;

    private ParticipantInfo localParticipant = null!;
    private Guid roomId;

    /// <inheritdoc />
    public override ViewLayoutType LayoutType => ViewLayoutType.RightSideBar;

    /// <inheritdoc />
    public override bool RelatedToCentral => true;

    /// <inheritdoc />
    public override EventHandler? OnNavigatedTo { get; }

    /// <inheritdoc />
    public override EventHandler? OnNavigatedFrom { get; }

    /// <summary>
    /// Участники комнаты
    /// </summary>
    [ObservableProperty]
    public partial AvaloniaList<ParticipantCardViewModel> RoomParticipants { get; set; } = [];

    /// <summary>
    /// Комната
    /// </summary>
    [ObservableProperty]
    public partial Room Room { get; set; } = null!;

    /// <summary>
    /// Имя хоста
    /// </summary>
    [ObservableProperty]
    public partial string HostName { get; set; } = string.Empty;

    /// <summary>
    /// Адреса соединения
    /// </summary>
    [ObservableProperty]
    public partial AvaloniaList<ConnectionAddressViewModel> ConnectionAddresses { get; set; } = [];

    /// <summary>
    /// Кабинет
    /// </summary>
    [ObservableProperty]
    public partial string Classroom { get; set; } = string.Empty;

    /// <summary>
    /// Информация о кол-ве учатсников
    /// </summary>
    [ObservableProperty]
    public partial string ParticipantsInfo { get; set; } = string.Empty;

    /// <summary>
    /// Пинг
    /// </summary>
    [ObservableProperty]
    public partial int Ping { get; set; }

    /// <summary>
    /// Является локальный пользователь хостом
    /// </summary>
    [ObservableProperty]
    public partial bool IsHost { get; set; }

    /// <summary>
    /// Находиться ли сейчас комната онлайн
    /// </summary>
    [ObservableProperty]
    public partial bool IsOnline { get; set; } = true;

    /// <summary>
    /// Включены ли уведомления
    /// </summary>
    [ObservableProperty]
    public partial bool NotificationsEnabled { get; set; }

    /// <summary>
    /// Открыта ли панель
    /// </summary>
    public bool IsOpened { get; set; }

    /// <summary>
    /// Id выбранного участника для приватного общения
    /// </summary>
    public Guid? PrivateChatParticipantId { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatSideBarViewModel"/>
    /// </summary>
    public ChatSideBarViewModel(IMinFeatureCollection featureCollection,
        IDialogService dialogService,
        ICtsProvider ctsProvider)
    {
        this.featureCollection = featureCollection;
        this.dialogService = dialogService;

        if (!Design.IsDesignMode)
        {
            appCts = ctsProvider.AppCts;

            OnNavigatedTo = (sender, e) => IsOpened = true;
            OnNavigatedFrom = (sender, e) =>
            {
                if ((sender is ChatViewModel chatVm && chatVm.RoomId == roomId)
                    || (sender is ChatSideBarViewModel chatSideBarVm && chatSideBarVm.roomId == roomId))
                {
                    IsOpened = false;
                }
            };
        }
    }

    partial void OnNotificationsEnabledChanged(bool value)
        => Room.LocalRoomSettings.NotificationsEnabled = value;

    partial void OnIsOnlineChanged(bool value)
    {
        if (!value)
        {
            foreach (var participant in RoomParticipants)
            {
                participant.MarkAsOffline();
            }
        }
    }

    /// <summary>
    /// Подгрузить данные о комнате и перезагрузить страницу
    /// </summary>
    public void LoadRoomDataAndRefresh(Room room, ParticipantInfo localParticipant)
    {
        this.localParticipant = localParticipant;
        Room = room;
        roomId = room.Id;

        UpdateStats(room);
        UpdateParticipantFlow(room.CurrentParticipants);
    }

    /// <summary>
    /// Обновить статы
    /// </summary>
    public void UpdateStats(Room room)
    {
        IsHost = room.HostParticipant?.Id == localParticipant.Id;
        HostName = IsHost ? "Ты" : room.HostParticipant?.Name ?? "Неизвестно";

        ConnectionAddresses.Clear();

        clipboard ??= MainWindowViewModel.GetWindow()?.Clipboard;

        foreach (var address in room.ConnectionAddresses)
        {
            ConnectionAddresses.Add(new ConnectionAddressViewModel(address, clipboard));
        }

        Classroom = string.IsNullOrEmpty(room.Cabinet) ? DesktopConstants.UndefinedPcName : room.Cabinet;
    }

    /// <summary>
    /// Обновить пинг
    /// </summary>
    public void UpdatePing(int pingMs)
    {
        Ping = pingMs;
    }

    /// <summary>
    /// Пересортировать участников в зависимости от того
    /// </summary>
    public void ResortOnlineParticipants()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ResortOnlineParticipants);
            return;
        }
        RoomParticipants.SortBy(x => x.ParticipantLastSeenAt);
    }

    /// <summary>
    /// Обновить список участников
    /// </summary>
    public void UpdateParticipantFlow(IEnumerable<Participant> participants)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateParticipantFlow(participants));
            return;
        }

        RoomParticipants.Clear();

        foreach (var participant in participants)
        {
            var card = new ParticipantCardViewModel(participant,
                featureCollection.Core.EventBus,
                roomId,
                isHost: participant.Id == Room.HostParticipant.Id,
                isSelf: participant.Id == localParticipant.Id,
                asHost: localParticipant.Id == Room.HostParticipant.Id);

            card.OnPrivateChatMenuStripClicked += (selected, particpant) =>
            {
                foreach (var participantCard in RoomParticipants)
                {
                    if (participantCard.ParticipantId != participant.Id)
                    {
                        participantCard.IsSelected = false;
                    }
                }

                PrivateChatParticipantId = selected ? participant.Id : null;
            };

            card.OnKickParticipantClicked += async (participant) =>
            {
                var kickVm = await dialogService.ShowDialogAsync<ParticipantKickViewModel>(x => x.ParticipantName = participant.Name);

                if (kickVm! == true)
                {
                    try
                    {
                        await featureCollection.Chat.ChatRoomService.KickParticipantAsync(roomId,
                            participant.Id, kickVm!.Reason, appCts.Token);
                    }
                    catch (Exception ex)
                    {
                        InAppNotifier.Error(ex.Message);
                    }
                }
            };

            RoomParticipants.Add(card);
        }

        ParticipantsInfo = $"{Room.ParticipantCount}/{Room.MaximumParticipants}";
    }

    /// <summary>
    /// Закрыть страницу
    /// </summary>
    [RelayCommand]
    public void CloseAsync()
    {
        CloseView(this);
    }
}
