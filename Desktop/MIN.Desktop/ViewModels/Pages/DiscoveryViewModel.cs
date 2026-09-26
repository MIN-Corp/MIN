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
using MIN.Core.Stores.Contracts.Registries.Models;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Models;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Contracts.Models;
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
    private readonly IRoomConnectionUiService roomConnectionUiService;
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
        IRoomConnectionUiService roomConnectionUiService,
        IMinFeatureCollection featureCollection,
        ICtsProvider ctsProvider,
        IDialogService dialogService)
    {
        this.chatViewModelFactory = chatViewModelFactory;
        this.roomConnectionUiService = roomConnectionUiService;
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
            bool participantCreatingResult = await dialogService.ShowDialogAsync<CreateParticipantViewModel>();
            if (participantCreatingResult == false)
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
    public async Task CreateRoomUI() => await CreateRoom();

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

        var chatViewModel = chatViewModelFactory.Create();
        ChangeView(chatViewModel, createRoomCts.Token);

        var hostResult = await roomConnectionUiService.HostAsync(new RoomHostArgs()
        {
            RoomInfo = roomInfo,
            NetworkOptions = createViewModelResult.NetworkOptions,
            OnRoomReady = async room =>
            {
                await chatViewModel.LoadRoomDataAndRefresh(room, CoreRegistryConstants.LocalConnectionId);
                RegisterRoom(roomInfo, chatViewModel);

                InAppNotifier.Success($"Комната {room.Name} успешно создана!");
            }
        }, createRoomCts.Token);

        if (hostResult.Failure != null)
        {
            await featureCollection.Core.Lifecycle.ForgetHostingAsync(roomInfo.Id);
            await featureCollection.Discovery.DiscoveryService.StopDiscoveryAsync(roomInfo.Id);
            InAppNotifier.Info(hostResult.ErrorMessage ?? "Не удалось создать комнату");
            ChangeView(this);
            await CreateRoom(createViewModelResult.Room, createViewModelResult.NetworkOptions);
        }

        createRoomCts = null;
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
                await OnRoomJoin(discoveryInfo.Endpoints.First(x => x.Origin == origin), discoveryInfo.Room.Id);
                if (card != null)
                {
                    card.IsConnecting = false;
                }
            };

            DiscoveredRooms.Add(card);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Войти в комнату
    /// </summary>
    public async Task OnRoomJoin(IEndpoint endpoint, Guid? expectedRoomId)
    {
        if (!await ResolveParticipant())
        {
            return;
        }

        var connectCts = CancellationTokenSource.CreateLinkedTokenSource(lifeTimeCts.Token);

        var joinResult = await roomConnectionUiService.JoinAsync(new RoomJoinArgs()
        {
            Cts = connectCts,
            Endpoint = endpoint,
            ExpectedRoomId = expectedRoomId,
            OnRoomReady = async (room, connectionId) =>
            {
                if (room == null)
                {
                    return;
                }
                var newRoomInfo = new RoomInfo(room);

                var chatViewModel = chatViewModelFactory.Create();
                ChangeView(chatViewModel, connectCts.Token);

                await chatViewModel.LoadRoomDataAndRefresh(room, connectionId);
                RegisterRoom(newRoomInfo, chatViewModel);
            }
        }, connectCts.Token);

        if (joinResult.Failure != null && joinResult.RoomIdentityMismatchException != null)
        {
            switch (joinResult.RoomMismatchChoice)
            {
                case RoomMismatchChoice.JoinNew:
                    await OnRoomJoin(endpoint, null);
                    break;
                case RoomMismatchChoice.Replace:
                    await featureCollection.Core.Lifecycle.ForgetRoomAsync(joinResult.RoomIdentityMismatchException.ExpectedRoomId,
                        joinResult.RoomIdentityMismatchException.ConnectionId);
                    await OnRoomJoin(endpoint, null);
                    break;
                default:
                    break;
            }
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
            await OnRoomJoin(result.Endpoint, expectedRoomId: null);
            result.EnableConnectButton();
        };
    }

    [RelayCommand]
    private void ShowLeftSideBar()
    {
        WeakReferenceMessenger.Default.Send(new ShowNavigationReferenceCommand());
    }
}
