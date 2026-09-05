using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Services.Contracts.Models;
using MIN.Core.Stores.Contracts.Registries.Models;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Models;
using MIN.Desktop.Contracts.Constants;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Contracts.Models.ReferenceCommands;
using MIN.Desktop.Contracts.Models.ReferenceCommands.Layout;
using MIN.Desktop.Infrastructure.Extensions;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Cards;
using MIN.Desktop.ViewModels.Modals;
using MIN.Desktop.ViewModels.Pages.ChatViewModels;
using MIN.Desktop.ViewModels.Windows;
using MIN.DI.FeatureCollection;
using MIN.Discovery.Events;
using MIN.Discovery.Services.Contracts.Enums;
using MIN.Helpers.Contracts.Models;

namespace MIN.Desktop.ViewModels.Pages;

/// <summary>
/// Модель обнаружения комнат
/// </summary>
public partial class DiscoveryViewModel : RoutableViewModelBase
{
    private readonly IChatViewModelFactory chatViewModelFactory;
    private readonly IMinFeatureCollection featureCollection;
    private readonly IDialogService dialogService;
    private readonly CancellationTokenSource lifeTimeCts = null!;
    private readonly ParticipantInfo localParticipant = null!;
    private CancellationTokenSource? discoveryCts;
    private CancellationTokenSource? createRoomCts;
    private IClipboard? clipboard;

    private Settings Settings => featureCollection.Helper.SettingsProvider.GetSettings();

    /// <inheritdoc />
    public override ViewLayoutType LayoutType => ViewLayoutType.Central;

    /// <summary>
    /// Идёт ли сейчас процесс обнаружения
    /// </summary>
    [ObservableProperty]
    public partial bool isDiscovering { get; set; }

    /// <summary>
    /// Выбранный метод
    /// </summary>
    [ObservableProperty]
    public partial DiscoveryMethod ChosenMethod { get; set; }

    /// <summary>
    /// Обнаруженные комнаты
    /// </summary>
    [ObservableProperty]
    public partial AvaloniaList<DiscoveredRoomCardViewModel> DiscoveredRooms { get; set; } = [];

    [ObservableProperty]
    public partial WindowLayout CurrentLayout { get; private set; } = WindowLayout.ThreeColumns;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="DiscoveryViewModel"/>
    /// </summary>
    public DiscoveryViewModel(IChatViewModelFactory chatViewModelFactory,
        IMinFeatureCollection featureCollection,
        ICtsProvider ctsProvider,
        IDialogService dialogService)
    {
        this.chatViewModelFactory = chatViewModelFactory;
        this.featureCollection = featureCollection;
        this.dialogService = dialogService;

        if (!Design.IsDesignMode)
        {
            localParticipant = featureCollection.Core.IdentityService.SelfParticipant.ToParticipantInfo();
            lifeTimeCts = ctsProvider.AppCts;
            SubscribeToEvents();
            InitializeLayoutStyles();

            this.RegisterMessageListener<CancelRoutingOperationReferenceCommand, DiscoveryViewModel>((vm, _) =>
            {
                createRoomCts?.Cancel();
            });
        }
    }

    private void InitializeLayoutStyles()
    {
        this.RegisterMessageListener<LayoutModeChangedReferenceCommand, DiscoveryViewModel>((msg, _) =>
            CurrentLayout = msg.Layout);
    }

    private void SubscribeToEvents()
    {
        featureCollection.Core.EventBus.Subscribe<RoomDiscoveredEvent>(OnRoomDiscovered);
    }

    private async Task<bool> ResolveParticipant()
    {
        var selfParticipant = featureCollection.Core.IdentityService.SelfParticipant;

        if (selfParticipant.Name != string.Empty)
        {
            localParticipant.Name = selfParticipant.Name;
        }
        else
        {
            var participantCreatingResult = await dialogService.ShowDialogAsync<CreateParticipantViewModel>();
            if (participantCreatingResult != null && participantCreatingResult == false)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Обработчик создания комнаты
    /// </summary>
    [RelayCommand]
    public async Task CreateRoomUI()
    {
        await CreateRoom();
    }

    private async Task CreateRoom(RoomInfo? loopRoom = null, NetworkOptions? loopNetworkOptions = null)
    {
        var createViewModelResult = await dialogService.ShowDialogAsync<CreateRoomViewModel>(vm =>
        {
            if (loopRoom != null && loopNetworkOptions != null)
            {
                vm.InitializeWithRoom(loopRoom, loopNetworkOptions.Value);
            }
        });
        if (createViewModelResult! == false)
        {
            return;
        }

        if (!await ResolveParticipant())
        {
            return;
        }

        var roomInfo = createViewModelResult!.Room;
        var roomId = roomInfo.Id;

        createRoomCts = CancellationTokenSource.CreateLinkedTokenSource(lifeTimeCts.Token);

        try
        {
            var chatViewModel = chatViewModelFactory.Create();
            ChangeView(chatViewModel, createRoomCts.Token);

            var room = await featureCollection.Core.Lifecycle.StartHostingAsync(roomInfo, createViewModelResult.NetworkOptions, createRoomCts.Token);
            await featureCollection.Chat.ChatRoomService.ManageDiscoveryOutOfSettings(roomInfo,
                room.ConnectionAddresses, createViewModelResult.NetworkOptions, cancellationToken: createRoomCts.Token);

            await chatViewModel.LoadRoomDataAndRefresh(room, CoreRegistryConstants.LocalConnectionId);
            RegisterRoom(roomInfo, chatViewModel);

            InAppNotifier.Success($"Комната {room.Name} успешно создана!");
        }
        catch (OperationCanceledException)
        {
            await featureCollection.Core.Lifecycle.StopHostingAsync(roomInfo.Id);
            await featureCollection.Discovery.DiscoveryService.StopDiscoveryAsync(roomInfo.Id);
            InAppNotifier.Info("Создание комнаты было отменено");
            ChangeView(this);
            await CreateRoom(createViewModelResult.Room, createViewModelResult.NetworkOptions);
        }
        catch (Exception ex)
        {
            await featureCollection.Core.Lifecycle.StopHostingAsync(roomInfo.Id);
            await featureCollection.Discovery.DiscoveryService.StopDiscoveryAsync(roomInfo.Id);
            InAppNotifier.Error($"Не удалось создать комнату: {ex.Message}");
            ChangeView(this);
            await CreateRoom(createViewModelResult.Room, createViewModelResult.NetworkOptions);
        }
        finally
        {
            createRoomCts = null;
        }
    }

    private static void RegisterRoom(RoomInfo roomInfo, ChatViewModel chatViewModel)
    {
        WeakReferenceMessenger.Default.Send(new RegisterRoomReferenceCommand(roomInfo, chatViewModel));
    }

    /// <summary>
    /// Обработчик обнаружения комнат
    /// </summary>
    [RelayCommand]
    public void ChoseMethod(DiscoveryMethod discoveryMethod)
    {
        ChosenMethod = discoveryMethod;
    }

    /// <summary>
    /// Обработчик обнаружения комнат
    /// </summary>
    [RelayCommand]
    public void DiscoverRooms()
    {
        if (isDiscovering)
        {
            discoveryCts?.Cancel();
        }
        else
        {
            _ = PerformDiscovery();
        }
    }

    private async Task PerformDiscovery()
    {
        isDiscovering = true;
        DiscoveredRooms.Clear();
        discoveryCts = CancellationTokenSource.CreateLinkedTokenSource(lifeTimeCts.Token);

        try
        {
            await featureCollection.Discovery.DiscoveryService.DiscoverRoomsAsync(
                TimeSpan.FromMilliseconds(Settings.DiscoveryTimeout), discoveryCts.Token);
        }
        catch (Exception ex)
        {
            InAppNotifier.Error($"Ошибка обнаружения: {ex.Message}");
        }
        finally
        {
            isDiscovering = false;
        }
    }

    private Task OnRoomDiscovered(RoomDiscoveredEvent e, CancellationToken cancellationToken)
    {
        clipboard ??= MainWindowViewModel.GetWindow()?.Clipboard;

        foreach (var discoveryInfo in e.RoomDiscoveryInfos)
        {
            var card = new DiscoveredRoomCardViewModel(featureCollection.Core.EventBus,
                discoveryInfo.Room,
                discoveryInfo.Endpoints,
                localParticipant.Id == discoveryInfo.Room.HostParticipant.Id,
                featureCollection.Core.Registry.IsConnected(discoveryInfo.Room.Id),
                clipboard);

            card.Clicked += async (origin) =>
            {
                await OnRoomJoin(discoveryInfo.Endpoints.First(x => x.Origin == origin));
                if (card != null)
                {
                    card.IsConnecting = false;
                }
            };

            DiscoveredRooms.Add(card);
        }
        return Task.CompletedTask;
    }

    private async Task OnRoomJoin(IEndpoint endpoint)
    {
        if (!await ResolveParticipant())
        {
            return;
        }

        var connectCts = CancellationTokenSource.CreateLinkedTokenSource(lifeTimeCts.Token);
        LoadingViewModel? loadingVm = null;

        try
        {
            ConnectionResult connectionResult = new();

            _ = dialogService.ShowDialogAsync<LoadingViewModel>(async vm =>
            {
                await vm.LoadRoomDataAndRefresh(async room =>
                    {
                        if (room == null)
                        {
                            return;
                        }
                        var newRoomInfo = new RoomInfo(room);

                        var chatViewModel = chatViewModelFactory.Create();
                        ChangeView(chatViewModel, connectCts.Token);

                        await chatViewModel.LoadRoomDataAndRefresh(room, connectionResult.ConnectionId);
                        RegisterRoom(newRoomInfo, chatViewModel);
                    }, connectCts, DesktopConstants.RoomConnectionTimeoutMs);

                loadingVm = vm;
            });

            connectionResult = await featureCollection.Core.Lifecycle.ConnectAsync(endpoint, connectCts.Token);

            if (loadingVm != null)
            {
                loadingVm.RoomId = connectionResult.RoomId;
            }
        }
        catch (Exception ex)
        {
            loadingVm?.CloseByCode();
            InAppNotifier.Error($"Произошла ошибка при подключении: {ex.Message}");
        }
    }

    /// <summary>
    /// Обработчик подключения напрямую
    /// </summary>
    [RelayCommand]
    public async Task ConnectDirectly()
    {
        var result = await dialogService.ShowAsync<DirectConnectViewModel>();
        if (result == null)
        {
            return;
        }

        result.OnConnect += async () =>
        {
            await OnRoomJoin(result.Endpoint);
            result.EnableConnectButton();
        };
    }

    [RelayCommand]
    private void ShowLeftSideBar()
    {
        WeakReferenceMessenger.Default.Send(new ShowNavigationReferenceCommand());
    }
}
