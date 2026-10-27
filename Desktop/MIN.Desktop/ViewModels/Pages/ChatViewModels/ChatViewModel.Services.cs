using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Models;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Desktop.Contracts.Constants;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Models;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.Infrastructure.Validators;
using MIN.Desktop.ViewModels.Cards;
using MIN.Desktop.ViewModels.Windows;
using MIN.FileTransfer.Messaging;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Models;

namespace MIN.Desktop.ViewModels.Pages.ChatViewModels;

/// <summary>
/// Методы использования сервисов для чата
/// </summary>
public partial class ChatViewModel
{
    private readonly Window parentWindow = MainWindowViewModel.GetWindow()!;
    private CancellationTokenSource? createRoomCts;
    private CancellationTokenSource? reconnectRoomCts;
    private Guid? replyToPreviewId;

    /// <summary>
    /// Сохранённые адреса для выбора подключения
    /// </summary>
    public AvaloniaList<ConnectionAddressViewModel> ConnectableAddresses => chatSideBarViewModel.ConnectionAddresses;

    /// <summary>
    /// Адрес, выбранный в выпадающем списке подключения. Null — подключаться автоматически
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReconnectButtonText))]
    public partial ConnectionAddressViewModel? ChosenConnectAddress { get; set; }

    /// <summary>
    /// Текст кнопки подключения
    /// </summary>
    public string ReconnectButtonText => ChosenConnectAddress is null
        ? "Подключиться"
        : $"Подключиться · {ChosenConnectAddress.Address}";

    /// <summary>
    /// Идёт подключение
    /// </summary>
    [ObservableProperty]
    public partial bool IsConnecting { get; set; }

    /// <summary>
    /// Идёт рехостинг
    /// </summary>
    [ObservableProperty]
    public partial bool IsRehosting { get; set; }

    /// <summary>
    /// Превью ответа на вопрос (просто показать в строчке описание сообщения)
    /// </summary>
    [ObservableProperty]
    public partial string? ReplyToPreview { get; set; }

    /// <summary>
    /// Максимальное количество символов для отправки
    /// </summary>
    [ObservableProperty]
    public partial int MaxMessageLength { get; set; } = DesktopConstants.MaximumAvailableTextMessageSize;

    /// <summary>
    /// Отправляемое сообщение в textBox
    /// </summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxTextLength]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    public partial string SendingMessage { get; set; } = string.Empty;

    partial void OnMaxMessageLengthChanged(int value)
    {
        ValidateAllProperties();
        SendMessageCommand.NotifyCanExecuteChanged();
    }

    private void InitializeNotifications()
    {
        featureCollection.Helper.NotificationService.OnNotificationClick += OnNotificationClick;
        featureCollection.Helper.NotificationService.NotificationTurnOffClicked += NotificationTurnOffClicked;
    }

    private void OnNotificationClick()
    {
        if (isParentWindowClosed || !parentWindow.IsVisible)
        {
            Dispatcher.UIThread.Post(App.RequestShowMainWindow);
        }
        if (parentWindow.WindowState == WindowState.Minimized)
        {
            parentWindow.WindowState = WindowState.Normal;
        }
        parentWindow.Focus();
        parentWindow.Activate();
    }

    private void NotificationTurnOffClicked()
    {
        room.LocalRoomSettings.NotificationsEnabled = false;
        chatSideBarViewModel.NotificationsEnabled = false;
    }

    private void NotifyIfNeeded(IDescribable describable)
    {
        if (room.LocalRoomSettings.NotificationsEnabled
            && (parentWindow.WindowState == WindowState.Minimized || !isParentWindowActive))
        {
            featureCollection.Helper.NotificationService.Notify(describable, room.Name);
        }
    }

    private void NotifyIfNeeded(string message)
    {
        if (room.LocalRoomSettings.NotificationsEnabled
            && (parentWindow.WindowState == WindowState.Minimized || !isParentWindowActive))
        {
            featureCollection.Helper.NotificationService.Notify(message, room.Name);
        }
    }

    private void SetReplyTo(IMessage message)
    {
        replyToPreviewId = message.Id;
        ReplyToPreview = (message as IDescribable)?.GetDescription();
    }

    [RelayCommand]
    private void ResetReplyTo()
    {
        replyToPreviewId = null;
        ReplyToPreview = null;
    }

    private async Task OnDownloadRequested(FileMetadataMessage fileMetadata)
    {
        await featureCollection.Chat.ChatFileService.RequestFileDownloadAsync(RoomId,
            fileMetadata,
            roomCts.Token
        );
    }

    private async Task OnSessionJoinRequested(SessionReadyMessage sessionReadyMessage)
    {
        try
        {
            await featureCollection.Chat.ChatSessionService.SendSessionJoinRequest(RoomId,
                sessionReadyMessage,
                roomCts.Token
            );
        }
        catch (DirectoryNotFoundException e)
        {
            InAppNotifier.Error(e.Message);
        }
    }

    private async Task OnVoiceCallJoinRequested(int subRoomId)
    {
        try
        {
            await featureCollection.Chat.ChatVoiceService.JoinCallAsync(RoomId, subRoomId, roomCts.Token);
        }
        catch (DirectoryNotFoundException e)
        {
            InAppNotifier.Error(e.Message);
        }
    }

    private async Task OnMuteSelfRequested()
    {
        if (activeVoiceChatSubroomId != null)
        {
            await featureCollection.Voice.MuteService.MuteSelf(RoomId, activeVoiceChatSubroomId.Value, roomCts.Token);
        }
    }

    private async Task OnUnmuteSelfRequested()
    {
        if (activeVoiceChatSubroomId != null)
        {
            await featureCollection.Voice.MuteService.UnmuteSelf(RoomId, activeVoiceChatSubroomId.Value, roomCts.Token);
        }
    }

    private void OnMuteParticipantRequested(Guid participantId)
        => featureCollection.Voice.MuteService.MuteParticipant(participantId);

    private void OnUnmuteParticipantRequested(Guid participantId)
        => featureCollection.Voice.MuteService.UnmuteParticipant(participantId);

    private void OnNewDesiredVolumeRequested(Guid participantId, int volume)
        => featureCollection.Voice.VoicePlayback.ChangeParticipantVolume(participantId, volume);

    private async Task OnVoiceCallLeaveRequested(int subRoomId)
        => await featureCollection.Chat.ChatVoiceService.LeaveCallAsync(RoomId, subRoomId, roomCts.Token);

    private async Task RequestVoiceCallStateAsync()
        => await featureCollection.Chat.ChatVoiceService.RequestCallStateAsync(RoomId, roomCts.Token);

    private async Task OnCancelRequested(FileMetadataMessage fileMetadata)
        => await featureCollection.Chat.ChatFileService.CancelFileDownloadAsync(RoomId,
            fileMetadata,
            roomCts.Token);

    private async Task OnHistoryClearRequested()
    {
        try
        {
            await featureCollection.Chat.ChatMessageService.ClearMessageHistoryAsync(RoomId, roomCts.Token);
        }
        catch (Exception ex)
        {
            InAppNotifier.Info(ex.Message);
        }
    }

    private bool IsMessageValid()
        => (!string.IsNullOrWhiteSpace(SendingMessage) || SomeFilesAttached) && (SendingMessage.Length <= MaxMessageLength);

    private async Task SendSelfStatusChangedMessage(OnlineStatus newStatus)
    {
#if DEBUG
        try
        {
            await featureCollection.Chat.ChatStatusService.SendSelfOnlineStatusChangedAsync(RoomId,
                newStatus,
                roomCts.Token
            );
        }
        catch
        {
            // ignored
        }

        await Task.CompletedTask;
#else
        try
        {
            await featureCollection.Chat.ChatStatusService.SendSelfOnlineStatusChangedAsync(RoomId,
                newStatus,
                roomCts.Token
            );
        }
        catch
        {
            // ignored
        }
#endif
    }

    private async Task SendSessionStartMessage(Session session)
        => await featureCollection.Chat.ChatSessionService.SendSessionHostRequestAsync(RoomId, session, roomCts.Token);

    private async Task SendTunnelSessionStartMessage(string name, int gamePort,
       int? maximumParticipants, string? version, byte[]? thumbnailData)
        => await featureCollection.Chat.ChatSessionService.HostTunnelSessionAsync(RoomId, name, gamePort,
            maximumParticipants, version, thumbnailData, roomCts.Token);

    private async Task SendVoiceCallStartMessage()
        => await featureCollection.Chat.ChatVoiceService.StartCallAsync(RoomId, roomCts.Token);

    private async Task OnMessageDeleteRequested(Guid id)
        => await featureCollection.Chat.ChatMessageService.DeleteMessageAsync(RoomId, id, roomCts.Token);

    private async Task OnMessageEditRequested(Guid id, string newContent)
        => await featureCollection.Chat.ChatMessageService.EditTextMessageAsync(RoomId, id, newContent, roomCts.Token);

    [RelayCommand(CanExecute = nameof(IsMessageValid))]
    private async Task SendMessage()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(SendingMessage) && AttachedFiles.Count == 0)
            {
                await featureCollection.Chat.ChatTextService.SendTextMessageAsync(RoomId,
                    SendingMessage.Trim(),
                    chatSideBarViewModel.PrivateChatParticipantId,
                    replyToPreviewId,
                    roomCts.Token
                );
            }

            var lastFile = AttachedFiles.LastOrDefault();

            foreach (var fileAttachment in AttachedFiles)
            {
                if (featureCollection.FileTransfer.FileHelperService.IsFileImage(fileAttachment.File.FileName))
                {
                    try
                    {
                        using var bitmap = new Bitmap(fileAttachment.File.FilePath);
                    }
                    catch
                    {
                        try
                        {
                            using var bitmap = ImageHelper.SvgToBitmap(fileAttachment.File.FilePath);
                        }
                        catch (ArgumentException ex)
                        {
                            InAppNotifier.Error($"Не удалось загрузить файл {fileAttachment.File.FileName}: {ex.Message}");
                            return;
                        }
                    }
                }

                await featureCollection.Chat.ChatFileService.SendFileAsync(RoomId,
                    fileAttachment == lastFile ? SendingMessage : string.Empty,
                   fileAttachment.File.FileName,
                   fileAttachment.File.FilePath,
                   chatSideBarViewModel.PrivateChatParticipantId,
                   replyToPreviewId,
                   roomCts.Token
               );
            }

            AttachedFiles.Clear();
            SendingMessage = string.Empty;
            replyToPreviewId = null;
            ReplyToPreview = null;
        }
        catch (Exception ex)
        {
            InAppNotifier.Error($"Не удалось отправить сообщение: {ex.Message}");
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Reconnect()
    {
        if (IsConnecting)
        {
            reconnectRoomCts?.Cancel();
            reconnectRoomCts?.Dispose();
            reconnectRoomCts = null;
            IsConnecting = false;
            return;
        }

        IsConnecting = true;

        reconnectRoomCts = CancellationTokenSource.CreateLinkedTokenSource(roomCts.Token);

        var chosenAddress = ChosenConnectAddress;

        // Выбранный адрес могли удалить/изменить — тогда подключаемся автоматически
        if (chosenAddress != null && !ConnectableAddresses.Contains(chosenAddress))
        {
            chosenAddress = null;
        }

        if (chosenAddress != null)
        {
            await ConnectUsingAsync(chosenAddress.Endpoint!, reconnectRoomCts);
        }
        else
        {
            // Перебираем сохранённые адреса, пока какой-нибудь не заработает
            foreach (var endpoint in room.ConnectionAddresses.ToList())
            {
                var connected = await ConnectUsingAsync(endpoint, reconnectRoomCts);

                if (connected || reconnectRoomCts.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        IsConnecting = false;
        reconnectRoomCts = null;
    }

    /// <summary>
    /// Выбрать адрес для подключения (null — автоматически)
    /// </summary>
    [RelayCommand]
    private void ChooseConnectAddress(ConnectionAddressViewModel? addressViewModel)
    {
        if (ChosenConnectAddress != null)
        {
            ChosenConnectAddress.IsChosen = false;
        }

        ChosenConnectAddress = addressViewModel;

        if (ChosenConnectAddress != null)
        {
            ChosenConnectAddress.IsChosen = true;
        }
    }

    /// <summary>
    /// Одна попытка подключения по адресу. True — прекращать перебор адресов
    /// </summary>
    private async Task<bool> ConnectUsingAsync(IEndpoint endpoint, CancellationTokenSource cts)
    {
        var reconnectResult = await roomConnectionUiService.JoinAsync(new RoomJoinArgs()
        {
            Cts = cts,
            ExpectedRoomId = RoomId,
            Endpoint = endpoint,
            ShowLoadingDialog = false,
            OnRoomReady = async (readyRoom, readyConnectionId) =>
            {
                if (readyRoom == null)
                {
                    return;
                }

                await RefreshAfterReconnect(readyRoom, readyConnectionId);
            }
        }, cts.Token);

        if (reconnectResult.Failure == null || reconnectResult.RoomIdentityMismatchException == null)
        {
            return reconnectResult.Failure == null;
        }

        switch (reconnectResult.RoomMismatchChoice)
        {
            case RoomMismatchChoice.JoinNew:
                await discoveryViewModel.OnRoomJoin(endpoint, reconnectResult.RoomIdentityMismatchException.ActualRoom.Id);
                break;
            case RoomMismatchChoice.Replace:
                connectionId = reconnectResult.RoomIdentityMismatchException.ConnectionId;
                await Disconnect(asForget: true);
                await discoveryViewModel.OnRoomJoin(endpoint, reconnectResult.RoomIdentityMismatchException.ActualRoom.Id);
                break;
            case RoomMismatchChoice.None:
            case RoomMismatchChoice.Cancel:
            case null:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return true;

    }

    private async Task RefreshAfterReconnect(Room updatedRoom, Guid connectionId)
    {
        chatSideBarViewModel.LoadRoomDataAndRefresh(room, localParticipant);

        room = updatedRoom;
        this.connectionId = connectionId;

        RoomName = room.Name;
        IsHost = localParticipant.Id == room.HostParticipant.Id;
        IsOnline = room.IsOnline;
        IsAvaibleForNetwork = IsOnline || IsHost;

        await UpdateChatFlow();

        if (!IsHost)
        {
            await RequestVoiceCallStateAsync();
        }
        else
        {
            loadingTcs?.SetResult();
        }

        IsOnline = true;
        chatSideBarViewModel.IsOnline = true;
        IsConnecting = false;
        IsRehosting = false;

        InitializeConnectionActions();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Rehost()
    {
        if (IsRehosting)
        {
            createRoomCts?.Cancel();
            return;
        }

        IsRehosting = true;
        createRoomCts = CancellationTokenSource.CreateLinkedTokenSource(roomCts.Token);

        var hostResult = await roomConnectionUiService.HostAsync(new RoomHostArgs()
        {
            RoomInfo = new RoomInfo(room),
            NetworkOptions = room.LocalRoomSettings.NetworkOptions,
            OnRoomReady = async room =>
            {
                await RefreshAfterReconnect(room, CoreRegistryConstants.LocalConnectionId);
                InAppNotifier.Success($"Комната {room.Name} успешно хоститься!");
            }
        }, createRoomCts.Token);

        if (hostResult.Failure != null)
        {
            await featureCollection.Discovery.DiscoveryService.StopDiscoveryAsync(RoomId);
            InAppNotifier.Info(hostResult.ErrorMessage ?? "Не удалось захостить комнату");
        }

        createRoomCts = null;
        IsRehosting = false;
    }
}
