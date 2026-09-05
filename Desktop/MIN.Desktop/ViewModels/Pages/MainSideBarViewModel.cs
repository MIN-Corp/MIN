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
    private readonly SettingsSideBarViewModel settingsSideBarViewModel;
    private readonly DiscoveryViewModel discoveryViewModel;
    private readonly TrayService trayService;
    private readonly Dictionary<Guid, ChatViewModel> activeChatViews = [];
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
        SettingsSideBarViewModel settingsSideBarViewModel,
        DiscoveryViewModel discoveryViewModel,
        TrayService trayService)
    {
        this.featureCollection = featureCollection;
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
        }
    }

    private void SubscribeToEvents()
    {
        featureCollection.Core.EventBus.Subscribe<ErrorOccurredEvent>((e, _) =>
        {
            InAppNotifier.Error(e.ErrorMessage);
            return Task.CompletedTask;
        });
        featureCollection.Core.EventBus.Subscribe<RoomClosedEvent>((e, _) =>
        {
            UnregisterChat(e.RoomId);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Открыть настройки
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
    public void OpenSettingsViewAsync() => ChangeView(settingsSideBarViewModel);

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
        var context = featureCollection.Core.RoomFactory.GetOrCreateContext(roomId);

        activeChatViews[roomId] = viewModel;

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
        activeChatViews.Remove(roomId);
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
