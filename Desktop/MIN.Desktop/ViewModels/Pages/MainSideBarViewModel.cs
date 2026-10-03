using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Events;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Contracts.Models.ReferenceCommands;
using MIN.Desktop.Contracts.Models.ReferenceCommands.Layout;
using MIN.Desktop.Infrastructure.Extensions;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Cards;
using MIN.Desktop.ViewModels.Pages.ChatViewModels;
using MIN.DI.FeatureCollection;

namespace MIN.Desktop.ViewModels.Pages;

/// <summary>
/// Модель боковой панели
/// </summary>
public partial class MainSideBarViewModel : RoutableViewModelBase
{
    private readonly IMinFeatureCollection featureCollection;
    private readonly IChatViewsRegistry chatViewsRegistry;
    private readonly IChatViewModelFactory chatViewModelFactory;
    private readonly SettingsSideBarViewModel settingsSideBarViewModel;
    private readonly DiscoveryViewModel discoveryViewModel;
    private readonly TrayService trayService;
    private readonly List<RecentRoomCardViewModel> allRooms = [];
    private readonly List<RoomInfo> savedRooms = [];
    private readonly ParticipantInfo localParticipant = null!;
    private RecentRoomCardViewModel? selectedRecentRoomCardViewModel;

    /// <summary>
    /// Последние комнаты
    /// </summary>
    [ObservableProperty]
    public partial AvaloniaList<RecentRoomCardViewModel> RecentRooms { get; set; } = [];

    /// <summary>
    /// Поле поиска локальных комнат
    /// </summary>
    [ObservableProperty]
    public partial string SearchTerm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsNavigationMode { get; set; }

    /// <inheritdoc />
    partial void OnSearchTermChanged(string value) => PerformRecentRoomSearch();

    /// <inheritdoc />
    public override ViewLayoutType LayoutType => ViewLayoutType.LeftSideBar;

    [ObservableProperty]
    public partial WindowLayout CurrentLayout { get; private set; }

    private void InitializeLayoutStyles()
    {
        this.RegisterMessageListener<LayoutModeChangedReferenceCommand, MainSideBarViewModel>((msg, _) =>
            CurrentLayout = msg.Layout);
    }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MainSideBarViewModel"/>
    /// </summary>
    public MainSideBarViewModel(IMinFeatureCollection featureCollection,
        IChatViewsRegistry chatViewsRegistry,
        IChatViewModelFactory chatViewModelFactory,
        SettingsSideBarViewModel settingsSideBarViewModel,
        DiscoveryViewModel discoveryViewModel,
        TrayService trayService)
    {
        this.featureCollection = featureCollection;
        this.chatViewsRegistry = chatViewsRegistry;
        this.chatViewModelFactory = chatViewModelFactory;
        this.settingsSideBarViewModel = settingsSideBarViewModel;
        this.discoveryViewModel = discoveryViewModel;
        this.trayService = trayService;

        if (!Design.IsDesignMode)
        {
            localParticipant = featureCollection.Core.IdentityService.SelfParticipant.ToParticipantInfo();

            this.RegisterMessageListener<RegisterRoomReferenceCommand, MainSideBarViewModel>(static (message, vm)
               => vm.RegisterChat(message.Room, message.View));

            this.RegisterMessageListener<LayoutModeChangedReferenceCommand, MainSideBarViewModel>((msg, _) =>
                IsNavigationMode = msg.Layout == WindowLayout.Narrow);

            trayService.NavigateToRoom += NavigateToChatView;

            SubscribeToEvents();
            InitializeLayoutStyles();
            _ = LoadRestoredRoomsAsync();
        }
    }

    private void SubscribeToEvents()
    {
        featureCollection.Core.EventBus.Subscribe<ErrorOccurredEvent>((e, _) =>
        {
            InAppNotifier.Error(e.ErrorMessage);
            return Task.CompletedTask;
        });
        featureCollection.Core.EventBus.Subscribe<RoomDestroyedEvent>((e, _) =>
        {
            UnregisterChat(e.RoomId);
            return Task.CompletedTask;
        });
    }

    private async Task LoadRestoredRoomsAsync()
    {
        var rooms = await featureCollection.Core.RoomPersistence.GetLoadedRoomsAsync();
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var room in rooms)
            {
                RegisterLoadedRoom(new RoomInfo(room));
            }
        });
    }

    private void RegisterLoadedRoom(RoomInfo roomInfo)
    {
        var context = featureCollection.Core.RoomFactory.GetOrCreateContext(roomInfo.Id);
        var card = new RecentRoomCardViewModel(featureCollection.Core.EventBus, context, roomInfo, AsCreator: false);
        card.Clicked += () => OpenLoadedRoom(roomInfo.Id);
        savedRooms.Add(roomInfo);
        Dispatcher.UIThread.Post(() => trayService.UpdateRooms(savedRooms));
        allRooms.Add(card);
        RecentRooms.Add(card);
    }

    private async void OpenLoadedRoom(Guid roomId)
    {
        if (IsNavigationMode)
        {
            GoBack();
        }

        if (allRooms.FirstOrDefault(x => x.RoomId == roomId) is { } card)
        {
            SelectChatCard(card);
        }

        if (chatViewsRegistry.TryGet(roomId, out var existing))
        {
            ChangeView(existing!);
            return;
        }

        if (!featureCollection.Core.RoomStore.TryGetRoom(roomId, out var room))
        {
            return;
        }

        var chatViewModel = chatViewModelFactory.Create();
        await chatViewModel.LoadRoomDataAndRefresh(room, Guid.Empty);
        ChangeView(chatViewModel);

        WeakReferenceMessenger.Default.Send(new RegisterRoomReferenceCommand(new RoomInfo(room), chatViewModel));
    }

    /// <summary>
    /// Открыть окно поиска комнат
    /// </summary>
    [RelayCommand]
    public void OpenDiscoveryViewAsync()
    {
        UnselectRecentRoomCard();
        ChangeView(discoveryViewModel);
    }

    /// <summary>
    /// Открыть настройки
    /// </summary>
    [RelayCommand]
    public void OpenSettingsViewAsync()
        => ChangeView(settingsSideBarViewModel);

    private void UnselectRecentRoomCard()
    {
        if (selectedRecentRoomCardViewModel != null)
        {
            selectedRecentRoomCardViewModel.IsSelected = false;
        }

        selectedRecentRoomCardViewModel = null;
    }

    private void SelectChatCard(RecentRoomCardViewModel card)
    {
        UnselectRecentRoomCard();
        selectedRecentRoomCardViewModel = card;
        card.SelectCard();
    }

    /// <summary>
    /// Зарегистрировать чат
    /// </summary>
    public void RegisterChat(RoomInfo roomInfo, ChatViewModel viewModel)
    {
        var roomId = roomInfo.Id;
        var existing = allRooms.FirstOrDefault(x => x.RoomId == roomId);
        if (existing != null)
        {
            chatViewsRegistry.Register(roomId, viewModel);
            SelectChatCard(existing);
            return;
        }

        var context = featureCollection.Core.RoomFactory.GetOrCreateContext(roomId);

        chatViewsRegistry.Register(roomId, viewModel);

        var card = new RecentRoomCardViewModel(featureCollection.Core.EventBus,
            context, roomInfo, localParticipant.Id == roomInfo.HostParticipant.Id);

        card.Clicked += () =>
        {
            if (IsNavigationMode)
            {
                GoBack();
            }

            if (selectedRecentRoomCardViewModel != card || CurrentLayout == WindowLayout.Narrow)
            {
                SelectChatCard(card);
                ChangeView(viewModel);
            }
        };

        savedRooms.Add(roomInfo);
        Dispatcher.UIThread.Post(() => trayService.UpdateRooms(savedRooms));

        allRooms.Add(card);
        RecentRooms.Add(card);
        SelectChatCard(card);
    }

    private void NavigateToChatView(Guid roomId)
    {
        var card = allRooms.FirstOrDefault(x => x.RoomId == roomId);
        card?.SelectItem();
    }

    private void UnregisterChat(Guid roomId)
    {
        chatViewsRegistry.Unregister(roomId);

        var room = allRooms.FirstOrDefault(x => x.RoomId == roomId);

        if (room != null)
        {
            var roomInfo = savedRooms.FirstOrDefault(x => x.Id == roomId);
            if (roomInfo != null)
            {
                savedRooms.Remove(roomInfo);
                Dispatcher.UIThread.Post(() => trayService.UpdateRooms(savedRooms));
            }
            RecentRooms.Remove(room);
            allRooms.Remove(room);
            room.Dispose();
        }

        if (selectedRecentRoomCardViewModel?.RoomId == roomId)
        {
            ChangeView(discoveryViewModel);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        WeakReferenceMessenger.Default.Send(new RestoreCentralReferenceCommand());
        IsNavigationMode = false;
    }

    [RelayCommand]
    private void PerformRecentRoomSearch()
    {
        RecentRooms.Clear();
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            RecentRooms.AddRange(allRooms);
        }
        else
        {
            RecentRooms.AddRange(allRooms.Where(r =>
                r.RoomName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
