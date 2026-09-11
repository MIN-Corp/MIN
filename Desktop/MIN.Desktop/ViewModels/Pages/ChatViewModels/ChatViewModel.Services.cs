using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Windows;
using MIN.FileTransfer.Messaging;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Models;

namespace MIN.Desktop.ViewModels.Pages.ChatViewModels;

/// <summary>
/// Методы использования сервисов для чата
/// </summary>
public partial class ChatViewModel : RoutableViewModelBase
{
    private Window parentWindow = null!;
    private Guid? replyToPreviewId;

    /// <summary>
    /// Превью ответа на вопрос (просто показать в строчке описание сообщения)
    /// </summary>
    [ObservableProperty]
    public partial string? ReplyToPreview { get; set; }

    /// <summary>
    /// Отправляемое сообщение в textBox
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    public partial string SendingMessage { get; set; } = string.Empty;

    private void InitializeNotifications()
    {
        parentWindow = MainWindowViewModel.GetWindow()!;

        featureCollection.Helper.NotificationService.OnNotificationClick += OnNotificationClick;
        featureCollection.Helper.NotificationService.NotificationTurnOffClicked += NotificationTurnOffClicked;
    }

    private void OnNotificationClick()
    {
        parentWindow.WindowState = WindowState.Normal;
        parentWindow.Focus();
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
        await featureCollection.Chat.ChatFileService.RequestFileDownloadAsync(roomId,
            fileMetadata,
            roomCts.Token
        );
    }

    private async Task OnSessionJoinRequested(SessionReadyMessage sessionReadyMessage)
    {
        try
        {
            await featureCollection.Chat.ChatSessionService.SendSessionJoinRequest(roomId,
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
            await featureCollection.Chat.ChatVoiceService.JoinCallAsync(roomId, subRoomId, roomCts.Token);
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
            await featureCollection.Voice.MuteService.MuteSelf(roomId, activeVoiceChatSubroomId.Value, roomCts.Token);
        }
    }

    private async Task OnUnmuteSelfRequested()
    {
        if (activeVoiceChatSubroomId != null)
        {
            await featureCollection.Voice.MuteService.UnmuteSelf(roomId, activeVoiceChatSubroomId.Value, roomCts.Token);
        }
    }

    private void OnMuteParticipantRequested(Guid participantId)
        => featureCollection.Voice.MuteService.MuteParticipant(participantId);

    private void OnUnmuteParticipantRequested(Guid participantId)
        => featureCollection.Voice.MuteService.UnmuteParticipant(participantId);

    private void OnNewDesiredVolumeRequested(Guid participantId, int volume)
        => featureCollection.Voice.VoicePlayback.ChangeParticipantVolume(participantId, volume);

    private async Task OnVoiceCallLeaveRequested(int subRoomId)
        => await featureCollection.Chat.ChatVoiceService.LeaveCallAsync(roomId, subRoomId, roomCts.Token);

    private async Task RequestVoiceCallStateAsync()
        => await featureCollection.Chat.ChatVoiceService.RequestCallStateAsync(roomId, roomCts.Token);

    private async Task OnCancelRequested(FileMetadataMessage fileMetadata)
        => await featureCollection.Chat.ChatFileService.CancelFileDownloadAsync(roomId,
            fileMetadata,
            roomCts.Token);

    private async Task OnHistoryClearRequested()
    {
        try
        {
            await featureCollection.Chat.ChatMessageService.ClearMessageHistoryAsync(roomId, roomCts.Token);
        }
        catch (Exception ex)
        {
            InAppNotifier.Info(ex.Message);
        }
    }

    private bool IsMessageValid() => !string.IsNullOrWhiteSpace(SendingMessage) || SomeFilesAttached;

    private async Task SendSelfStatusChangedMessage(OnlineStatus newStatus)
    {
#if DEBUG
        try
        {
            await featureCollection.Chat.ChatStatusService.SendSelfOnlineStatusChangedAsync(roomId,
                newStatus,
                roomCts.Token
            );
        }
        catch { }

        await Task.CompletedTask;
        return;
#else
        try
        {
            await featureCollection.Chat.ChatStatusService.SendSelfOnlineStatusChangedAsync(roomId,
                newStatus,
                roomCts.Token
            );
        }
        catch { }
#endif
    }

    private async Task SendSessionStartMessage(Session session)
        => await featureCollection.Chat.ChatSessionService.SendSessionHostRequestAsync(roomId, session, roomCts.Token);

    private async Task SendVoiceCallStartMessage()
        => await featureCollection.Chat.ChatVoiceService.StartCallAsync(roomId, roomCts.Token);

    private async Task OnMessageDeleteRequested(Guid id)
        => await featureCollection.Chat.ChatMessageService.DeleteMessageAsync(roomId, id, roomCts.Token);

    private async Task OnMessageEditRequested(Guid id, string newContent)
        => await featureCollection.Chat.ChatMessageService.EditTextMessageAsync(roomId, id, newContent, roomCts.Token);

    [RelayCommand(CanExecute = nameof(IsMessageValid))]
    private async Task SendMessage()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(SendingMessage) && AttachedFiles.Count == 0)
            {
                await featureCollection.Chat.ChatTextService.SendTextMessageAsync(roomId,
                    SendingMessage.Trim(),
                    chatSideBarViewModel.PrivateChatParticipantId,
                    replyToPreviewId,
                    roomCts.Token
                );
            }

            var lastFile = AttachedFiles.LastOrDefault();

            foreach (var fileAttachement in AttachedFiles)
            {
                if (featureCollection.FileTransfer.FileHelperService.IsFileImage(fileAttachement.File.FileName))
                {
                    try
                    {
                        using var bitmap = new Bitmap(fileAttachement.File.FilePath);
                    }
                    catch
                    {
                        try
                        {
                            using var bitmap = ImageHelper.SvgToBitmap(fileAttachement.File.FilePath);
                        }
                        catch (ArgumentException ex)
                        {
                            InAppNotifier.Error($"Не удалось загрузить файл {fileAttachement.File.FileName}: {ex.Message}");
                            return;
                        }
                    }
                }

                await featureCollection.Chat.ChatFileService.SendFileAsync(roomId,
                   fileAttachement == lastFile ? SendingMessage : string.Empty,
                   fileAttachement.File.FileName,
                   fileAttachement.File.FilePath,
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
}
