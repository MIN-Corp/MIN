using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Stores.Contracts.Constants;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.ViewModels.Base;
using MIN.DI.FeatureCollection;

namespace MIN.Desktop.ViewModels.Pages.ChatViewModels;

/// <summary>
/// Модель чата
/// </summary>
public partial class ChatViewModel : RoutableViewModelBase
{
    private readonly ChatSideBarViewModel chatSideBarViewModel;
    private readonly DiscoveryViewModel discoveryViewModel;
    private readonly MainSideBarViewModel mainSideBarViewModel;
    private readonly IDialogService dialogService;

    private readonly IMinFeatureCollection featureCollection;
    private readonly CancellationTokenSource roomCts = new();
    private readonly ParticipantInfo localParticipant = null!;

    private TaskCompletionSource? loadingTcs = new();
    private Guid roomId;
    private Guid connectionId;
    private Room room = null!;

    /// <inheritdoc />
    public override ViewLayoutType LayoutType => ViewLayoutType.Central;

    /// <inheritdoc />
    public override EventHandler? OnNavigatedTo { get; }

    /// <inheritdoc />
    public override EventHandler? OnNavigatedFrom { get; }

    /// <summary>
    /// Идентификатор комнаты
    /// </summary>
    public Guid RoomId => roomId;

    /// <summary>
    /// Имя комнаты
    /// </summary>
    [ObservableProperty]
    public partial string RoomName { get; set; } = string.Empty;

    /// <summary>
    /// Является ли локальный пользователь хостом комнаты
    /// </summary>
    [ObservableProperty]
    public partial bool IsHost { get; set; }

    /// <summary>
    /// Подключены ли мы сейчас к комнате, или хостим
    /// </summary>
    [ObservableProperty]
    public partial bool IsOnline { get; set; }

    /// <summary>
    /// Можем ли мы воспользоваться функцией комнаты
    /// </summary>
    [ObservableProperty]
    public partial bool IsAvaibleForNetwork { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatViewModel"/>
    /// </summary>
    public ChatViewModel(ChatSideBarViewModel chatSideBarViewModel,
        MainSideBarViewModel mainSideBarViewModel,
        DiscoveryViewModel discoveryViewModel,
        IMinFeatureCollection featureCollection,
        IDialogService dialogService)
    {
        this.featureCollection = featureCollection;
        this.mainSideBarViewModel = mainSideBarViewModel;
        this.discoveryViewModel = discoveryViewModel;
        this.chatSideBarViewModel = chatSideBarViewModel;
        this.dialogService = dialogService;

        if (!Design.IsDesignMode)
        {
            localParticipant = featureCollection.Core.IdentityService.SelfParticipant.ToParticipantInfo();

            OnNavigatedTo = ActionOnNavigatedTo;
            OnNavigatedFrom = ActionOnNavigatedFrom;

            InitializeNotifications();
            InitializeTimers();
            InitializeLayoutStyles();
            InitializeParentFormWindowStateEvents();
            InitializeObservableCollections();
        }
    }

    private async void ActionOnNavigatedTo(object? sender, EventArgs e)
    {
        if (chatSideBarViewModel.IsOpened)
        {
            ChangeView(chatSideBarViewModel);
        }

        if (!hasScrolledHistory)
        {
            return;
        }

        hasScrolledHistory = false;
        renderedMessageCount = 0;
        maxRenderedMessages = StoreConstants.MessagesPageSize;
        oldestLoadedTimestamp = null;
        oldestLoadedMessageId = null;

        var context = featureCollection.Core.RoomFactory.GetOrCreateContext(roomId);
        var lastHistory = context.Messages.GetRecentHistory().ToList();

        await RenderMessages(lastHistory);

        if (lastHistory.Count > 0)
        {
            var oldest = lastHistory[0];
            oldestLoadedTimestamp = oldest.Timestamp;
            oldestLoadedMessageId = oldest.Id;
        }

        var moreExists = context.Messages
            .GetMessagesOlderThan(oldestLoadedTimestamp, oldestLoadedMessageId, 1)
            .Any();

        if (moreExists || context.Messages.GetMessageCount() < room.TotalMessageCount)
        {
            ShowLoadMoreLabel();
        }
    }

    private void ActionOnNavigatedFrom(object? sender, EventArgs e)
    {
        if (hasScrolledHistory)
        {
            Messages.Clear();
            RemoveLoadMoreLabel();
        }
    }

    /// <inheritdoc />
    public override async Task ViewContentLoadAsync(CancellationToken cancellationToken = default)
    {
        if (loadingTcs != null)
        {
            await loadingTcs.Task;
        }
    }

    partial void OnIsOnlineChanged(bool value)
    {
        IsAvaibleForNetwork = value || IsHost;
    }

    /// <summary>
    /// Подгрузить данные о комнате и перезагрузить страницу
    /// </summary>
    public async Task LoadRoomDataAndRefresh(Room room, Guid connectionId)
    {
        ToggleRightSideBar();
        chatSideBarViewModel.LoadRoomDataAndRefresh(room, localParticipant);

        this.room = room;
        this.connectionId = connectionId;

        RoomName = room.Name;
        IsHost = localParticipant.Id == room.HostParticipant.Id;
        IsOnline = room.IsOnline;
        chatSideBarViewModel.IsOnline = IsOnline;
        IsAvaibleForNetwork = IsOnline || IsHost;
        roomId = room.Id;
        SubscribeToEvents(featureCollection.Core.EventBus);

        await UpdateChatFlow();

        if (!IsHost)
        {
            await RequestVoiceCallStateAsync();
        }
        else
        {
            loadingTcs?.SetResult();
            loadingTcs = null;
        }
    }

    private async Task CleanUpServicesAsync(bool asForget)
    {
        if (IsHost)
        {
            await featureCollection.Discovery.DiscoveryService.StopDiscoveryAsync(roomId);
            if (asForget)
            {
                await featureCollection.Core.Lifecycle.ForgetHostingAsync(roomId);
            }
            else
            {
                await featureCollection.Core.Lifecycle.StopHostingAsync(roomId);
            }
        }
        else
        {
            if (asForget)
            {
                await featureCollection.Core.Lifecycle.ForgetRoomAsync(roomId, connectionId);
            }
            else
            {
                await featureCollection.Core.Lifecycle.DisconnectAsync(roomId, connectionId);
            }
        }
    }

    private async Task Disconnect(bool asForget)
    {
        if (asForget)
        {
            await DisposeAsync();
        }

        await CleanUpServicesAsync(asForget);

        if (asForget)
        {
            ChangeView(discoveryViewModel);
        }
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    public async ValueTask DisposeAsync()
    {
        ClearParentFormEvents();
        roomScope.Dispose();
        errorToken.Dispose();
        typingTimer.Dispose();
        await roomCts.CancelAsync();
        roomCts.Dispose();
    }
}
