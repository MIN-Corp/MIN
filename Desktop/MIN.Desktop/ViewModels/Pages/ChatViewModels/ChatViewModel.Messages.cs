using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.Input;
using MIN.Chat.Messaging;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.RoomRelated;
using MIN.Core.Stores.Contracts.Constants;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.ViewModels.Cards.Messages;
using MIN.Desktop.ViewModels.Cards.Messages.Base;
using MIN.Desktop.ViewModels.Cards.Messages.Files;
using MIN.Desktop.ViewModels.Cards.Messages.Interfaces;
using MIN.Desktop.ViewModels.Cards.Messages.Sessions;
using MIN.Desktop.ViewModels.Cards.Messages.Voice;
using MIN.Desktop.ViewModels.Modals;
using MIN.FileTransfer.Messaging;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Voice.Messaging;

namespace MIN.Desktop.ViewModels.Pages.ChatViewModels;

/// <summary>
/// Методы использования сервисов для чата
/// </summary>
public partial class ChatViewModel
{
    private int maxRenderedMessages = StoreConstants.MessagesPageSize;

    /// <summary>
    /// Список сообщений для отображения в UI
    /// </summary>
    public AvaloniaList<BaseChatMessageViewModel> Messages { get; } = [];

    /// <summary>
    /// Raised before a batch of older messages is inserted at the top of the list.
    /// The view snapshots the scroll anchor so the exact position can be restored.
    /// </summary>
    public event Action? HistoryPrependStarted;

    /// <summary>
    /// Raised after the prepend batch has been fully inserted.
    /// </summary>
    public event Action? HistoryPrependCompleted;

    /// <summary>
    /// Raised when the view should restore the scroll position to the given card
    /// with the given offset (in pixels) from the top of the viewport.
    /// </summary>
    public event Action<BaseChatMessageViewModel, double>? ScrollRestoreRequested;

    private readonly int messageMaxPadding = 8;
    private readonly HashSet<Guid> seenMessageIds = [];

    private const int MaxTrackedSeenMessageIds = 10_000;

    private Guid? scrollAnchorMessageId;
    private double scrollAnchorSubOffset;

    private Guid? lastPrivateChatParticipantId;
    private IMessage? lastChatMessage;
    private bool hasScrolledHistory;
    private SystemChatMessageViewModel? loadMoreLabel;
    private int renderedMessageCount;

    private DateTime? oldestLoadedTimestamp;
    private Guid? oldestLoadedMessageId;

    private async Task AddMessageToChatFlow(IMessage message, bool appendOnTop = false, bool countTowardCap = true)
    {
        var isSelfMessage = message.SenderId == localParticipant.Id;
        var isHostMessage = room.HostParticipant.Id == message.SenderId;
        var isCurrentPrivate = message.RecipientId == localParticipant.Id
            || (message.SenderId == localParticipant.Id && message.RecipientId != null);

        BaseChatMessageViewModel? messageCard;
        switch (message)
        {
            case ChatTextMessage m:
                messageCard = await CreateTextMessageCard(m, isSelfMessage, isHostMessage, isCurrentPrivate, appendOnTop);
                break;

            case SessionReadyMessage m:
                messageCard = await CreateSessionMessageCard(m, isSelfMessage, isHostMessage, isCurrentPrivate, appendOnTop);
                break;

            case VoiceCallStartedMessage m:
                messageCard = await CreateVoiceMessageCard(m, isSelfMessage, isHostMessage, isCurrentPrivate, appendOnTop);
                break;

            case FileMetadataMessage m:
                messageCard = featureCollection.FileTransfer.FileHelperService.IsFileImage(m.FileName)
                    ? await CreateChatImagePreviewMessageCard(m, isSelfMessage, isHostMessage, isCurrentPrivate, appendOnTop)
                    : await CreateFileMessageCard(m, isSelfMessage, isHostMessage, isCurrentPrivate, appendOnTop);
                break;

            case SystemTextMessage m:
                messageCard = CreateSystemMessageLabel(m);
                break;

            case IDescribable d:
                messageCard = CreateDescribableLabel(d, message);
                break;

            default:
                return;
        }

        if (ShouldTrimExcessMessages())
        {
            ReplaceOldestWithLoadMore();
        }

        if (appendOnTop)
        {
            Messages.Insert(0, messageCard);
        }
        else
        {
            Messages.Add(messageCard);
        }

        if (countTowardCap)
        {
            renderedMessageCount++;
        }

        if (!IsAtBottom && message.SenderId != localParticipant.Id
            && !appendOnTop && !seenMessageIds.Contains(message.Id))
        {
            MissedMessagesCount++;
        }
        else if (IsAtBottom || message.SenderId == localParticipant.Id)
        {
            await ScrollToBottomAsync();
        }

        if (seenMessageIds.Add(message.Id) && seenMessageIds.Count > MaxTrackedSeenMessageIds)
        {
            seenMessageIds.Clear();
        }
    }

    private async Task UpdateChatFlow()
    {
        DisposeMessageCards();
        Messages.Clear();
        RemoveLoadMoreLabel();
        hasScrolledHistory = false;
        renderedMessageCount = 0;
        maxRenderedMessages = StoreConstants.MessagesPageSize;
        oldestLoadedTimestamp = null;
        oldestLoadedMessageId = null;

        var context = featureCollection.Core.RoomFactory.GetOrCreateContext(roomId);
        var messages = context.Messages.GetRecentHistory().ToList();

        await RenderMessages(messages);

        if (room.TotalMessageCount > StoreConstants.MessagesPageSize)
        {
            ShowLoadMoreLabel();
        }
        oldestLoadedTimestamp = messages[0].Timestamp;
        oldestLoadedMessageId = messages[0].Id;

        await RestoreScrollOrGoBottomAsync();
    }

    /// <summary>
    /// Запомнить якорь прокрутки: первое видимое сообщение и его отступ от верха
    /// </summary>
    public void UpdateScrollAnchor(BaseChatMessageViewModel? topCard, double subOffset)
    {
        scrollAnchorMessageId = topCard?.Message?.Id;
        scrollAnchorSubOffset = subOffset;
    }

    private Task RestoreScrollOrGoBottomAsync()
    {
        var anchorCard = scrollAnchorMessageId is { } anchorId
            ? Messages.FirstOrDefault(x => x.Message?.Id == anchorId)
            : null;

        if (anchorCard != null)
        {
            ScrollRestoreRequested?.Invoke(anchorCard, scrollAnchorSubOffset);
            return Task.CompletedTask;
        }

        return ScrollToBottomAsync();
    }

    private void DisposeMessageCards()
    {
        foreach (var card in Messages)
        {
            card.Dispose();
        }
    }

    private async Task RenderMessages(List<IMessage> messages, bool appendOnTop = false)
    {
        if (appendOnTop && messages.Count > 0)
        {
            HistoryPrependStarted?.Invoke();
        }

        foreach (var message in messages)
        {
            await AddMessageToChatFlow(message, appendOnTop);
        }

        if (appendOnTop && messages.Count > 0)
        {
            HistoryPrependCompleted?.Invoke();
        }
    }

    private void RemoveMessage(Guid id)
    {
        var existingCard = Messages.FirstOrDefault(x => x.Message?.Id == id);
        if (existingCard == null)
        {
            return;
        }

        Messages.Remove(existingCard);
        renderedMessageCount--;

        var replyables = Messages.OfType<BaseReplyableChatMessageViewModel>();
        foreach (var replyable in replyables)
        {
            if (replyable.HasReply && replyable.ReplyToMessageId == id)
            {
                replyable.ResetReplyAsDeleted();
            }
        }
    }

    private void UpdateMessage(Guid id, IMessage newMessage)
    {
        var existingCard = Messages.FirstOrDefault(x => x.Message?.Id == id);
        if (existingCard == null)
        {
            return;
        }
        if (existingCard is IUpdateableChatMessageViewModel baseUpdateable)
        {
            baseUpdateable.Update(newMessage);
        }
        var replyables = Messages.OfType<BaseReplyableChatMessageViewModel>();
        foreach (var replyable in replyables)
        {
            if (replyable.HasReply && replyable.ReplyToMessageId == id)
            {
                replyable.SetNewDescription((existingCard.Message as IDescribable)?.GetDescription());
            }
        }
    }

    private void ShowLoadMoreLabel()
    {
        if (loadMoreLabel != null)
        {
            return;
        }

        loadMoreLabel = new SystemChatMessageViewModel
        {
            Text = "+ Загрузить ещё",
        };

        loadMoreLabel.OnClicked += OnLoadMoreClicked;
        Messages.Insert(0, loadMoreLabel);
    }

    private void RemoveLoadMoreLabel()
    {
        if (loadMoreLabel == null)
        {
            return;
        }

        loadMoreLabel.OnClicked -= OnLoadMoreClicked;

        Messages.Remove(loadMoreLabel);
        loadMoreLabel.Dispose();
        loadMoreLabel = null;
    }

    private async Task OnLoadMoreClicked()
    {
        var context = featureCollection.Core.RoomFactory.GetOrCreateContext(roomId);

        var olderInMemory = context.Messages
            .GetMessagesOlderThan(oldestLoadedTimestamp, oldestLoadedMessageId)
            .ToList();

        maxRenderedMessages += StoreConstants.MessagesPageSize;

        var messagesCount = context.Messages.GetMessageCount();

        if (!IsHost && IsAvaibleForNetwork
            && olderInMemory.Count < StoreConstants.MessagesPageSize
            && messagesCount < room.TotalMessageCount)
        {
            await featureCollection.Chat.ChatRoomService.SendChatHistoryRequest(
                roomId, oldestLoadedTimestamp, oldestLoadedMessageId, roomCts.Token);
            return;
        }

        RemoveLoadMoreLabel();

        await RenderMessages(olderInMemory, appendOnTop: true);
        hasScrolledHistory = true;

        if (olderInMemory.Count > 0)
        {
            oldestLoadedTimestamp = olderInMemory[^1].Timestamp;
            oldestLoadedMessageId = olderInMemory[^1].Id;
        }

        var stillMoreExists = context.Messages
            .GetMessagesOlderThan(oldestLoadedTimestamp, oldestLoadedMessageId, 1)
            .Any();

        if (stillMoreExists || messagesCount < room.TotalMessageCount)
        {
            ShowLoadMoreLabel();
        }
    }

    private bool ShouldTrimExcessMessages() => renderedMessageCount >= maxRenderedMessages;

    private void ReplaceOldestWithLoadMore()
    {
        for (var i = 0; i < Messages.Count; i++)
        {
            if (Messages[i] == loadMoreLabel || Messages[i].Message == null)
            {
                continue;
            }

            var trimmedMessage = Messages[i].Message!;
            Messages.RemoveAt(i);
            renderedMessageCount--;

            oldestLoadedTimestamp = trimmedMessage.Timestamp;
            oldestLoadedMessageId = trimmedMessage.Id;

            for (var j = i; j < Messages.Count; j++)
            {
                if (Messages[j].Message != null)
                {
                    oldestLoadedTimestamp = Messages[j].Message!.Timestamp;
                    oldestLoadedMessageId = Messages[j].Message!.Id;
                    break;
                }
            }

            break;
        }
        ShowLoadMoreLabel();
    }

    [RelayCommand]
    private async Task ClearHistoryUpToThisMoment()
    {
        var message = "Вы точно хотите очистить историю? "
            + "\nЭто поможет снизить нагрузку.";

        if (!IsHost)
        {
            message += "\nПри перезаходе из комнаты история возобновиться.";
        }

        bool confirmation = await dialogService.ShowDialogAsync<DialogBoxViewModel>(model =>
        {
            model.Title = $"Очищение истории для комнаты {room.Name}";
            model.Description = message;
            model.ButtonOptions = ButtonOptions.YesNo;
        });

        if (!confirmation)
        {
            return;
        }

        await OnHistoryClearRequested();
    }

    private async Task<ChatTextMessageViewModel> CreateTextMessageCard(ChatTextMessage msg,
            bool isSelf, bool isHost, bool isCurrentPrivate, bool withAppendOnTop)
    {
        var removeHeaders = isSelf || lastChatMessage?.SenderId == msg.SenderId;
        var timePadding = CalculateTimePadding(msg.Timestamp);

        var card = new ChatTextMessageViewModel(msg, dialogService, timePadding, isSelf, isHost, removeHeaders, parentWindow.Clipboard, IsAvaibleForNetwork);
        card.OnDeleteRequested += () => OnMessageDeleteRequested(msg.Id);
        card.OnEditRequested += (newContent) => OnMessageEditRequested(msg.Id, newContent);
        card.OnReplyRequested += () => SetReplyTo(msg);

        if (!withAppendOnTop)
        {
            await InsertPrivateChatSystemMessageIfNeeded(msg.SenderId, msg.RecipientId, isCurrentPrivate);
        }

        lastChatMessage = msg;
        return card;
    }

    private async Task<ChatFileMessageViewModel> CreateFileMessageCard(FileMetadataMessage msg,
        bool isSelf, bool isHost, bool isCurrentPrivate, bool withAppendOnTop)
    {
        var removeHeaders = isSelf || lastChatMessage?.SenderId == msg.SenderId;
        var timePadding = CalculateTimePadding(msg.Timestamp);

        var card = new ChatFileMessageViewModel(featureCollection.FileTransfer,
            dialogService, roomScope, msg, timePadding,
            localParticipant, isHost, removeHeaders, parentWindow.Clipboard, IsAvaibleForNetwork);

        card.OnDownloadRequested += () => OnDownloadRequested(msg);
        card.OnCancelRequested += () => OnCancelRequested(msg);
        card.OnDeleteRequested += () => OnMessageDeleteRequested(msg.Id);
        card.OnEditRequested += (newContent) => OnMessageEditRequested(msg.Id, newContent);
        card.OnReplyRequested += () => SetReplyTo(msg);

        if (!withAppendOnTop)
        {
            await InsertPrivateChatSystemMessageIfNeeded(msg.SenderId, msg.RecipientId, isCurrentPrivate);
        }

        lastChatMessage = msg;
        return card;
    }

    private async Task<ChatFileImagePreviewMessageViewModel> CreateChatImagePreviewMessageCard(FileMetadataMessage msg,
        bool isSelf, bool isHost, bool isCurrentPrivate, bool withAppendOnTop)
    {
        var removeHeaders = isSelf || lastChatMessage?.SenderId == msg.SenderId;
        var timePadding = CalculateTimePadding(msg.Timestamp);

        var card = new ChatFileImagePreviewMessageViewModel(featureCollection.FileTransfer,
            dialogService, roomScope, msg, timePadding,
            localParticipant, isHost, removeHeaders, parentWindow.Clipboard, IsAvaibleForNetwork);

        card.OnDownloadRequested += () => OnDownloadRequested(msg);
        card.OnCancelRequested += () => OnCancelRequested(msg);
        card.OnDeleteRequested += () => OnMessageDeleteRequested(msg.Id);
        card.OnEditRequested += (newContent) => OnMessageEditRequested(msg.Id, newContent);
        card.OnReplyRequested += () => SetReplyTo(msg);

        if (!withAppendOnTop)
        {
            await InsertPrivateChatSystemMessageIfNeeded(msg.SenderId, msg.RecipientId, isCurrentPrivate);
        }

        lastChatMessage = msg;
        return card;
    }

    private async Task<ChatSessionMessageViewModel> CreateSessionMessageCard(SessionReadyMessage msg,
           bool isSelf, bool isHost, bool isCurrentPrivate, bool withAppendOnTop)
    {
        var removeHeaders = isSelf || lastChatMessage?.SenderId == msg.SenderId;
        var timePadding = CalculateTimePadding(msg.Timestamp);

        var card = new ChatSessionMessageViewModel(featureCollection.Sessions,
            featureCollection.Core.RoomFactory.GetRoomContext(roomId), roomScope,
            featureCollection.Core.EventBus, dialogService, parentWindow.Clipboard,
            msg, localParticipant, timePadding, IsHost, isHost, removeHeaders, IsAvaibleForNetwork);

        card.OnJoinRequested += () => OnSessionJoinRequested(msg);
        card.OnReplyRequested += () => SetReplyTo(msg);
        card.OnLeaveRequested += () => featureCollection.Chat.ChatSessionService.SendSessionLeaveAsync(roomId, msg.SubRoomId, roomCts.Token);
        card.OnShutdownRequested += () => featureCollection.Chat.ChatSessionService.SendSessionStopAsync(roomId, msg.SubRoomId, roomCts.Token);
        card.OnDeleteRequested += () => featureCollection.Chat.ChatSessionService.DeleteSessionMessageAsync(roomId, msg, roomCts.Token);

        if (!withAppendOnTop)
        {
            await InsertPrivateChatSystemMessageIfNeeded(msg.SenderId, msg.RecipientId, isCurrentPrivate);
        }

        lastChatMessage = msg;
        return card;
    }

    private async Task<ChatVoiceCallMessageViewModel> CreateVoiceMessageCard(VoiceCallStartedMessage msg,
       bool isSelf, bool isHost, bool isCurrentPrivate, bool withAppendOnTop)
    {
        var removeHeaders = isSelf || lastChatMessage?.SenderId == msg.SenderId;
        var timePadding = CalculateTimePadding(msg.Timestamp);

        var card = new ChatVoiceCallMessageViewModel(roomScope, msg, localParticipant, timePadding, isHost, removeHeaders, IsAvaibleForNetwork);

        card.OnJoinRequested += () => OnVoiceCallJoinRequested(msg.SubRoomId);
        card.OnLeaveRequested += () => OnVoiceCallLeaveRequested(msg.SubRoomId);

        if (!withAppendOnTop)
        {
            await InsertPrivateChatSystemMessageIfNeeded(msg.SenderId, msg.RecipientId, isCurrentPrivate);
        }

        lastChatMessage = msg;
        return card;
    }

    private static SystemChatMessageViewModel CreateSystemMessageLabel(SystemTextMessage msg)
    {
        var card = new SystemChatMessageViewModel(msg)
        {
            Text = msg.Content,
            IsPrivate = msg.RecipientId != null,
        };

        return card;
    }

    private static SystemChatMessageViewModel CreateDescribableLabel(IDescribable describable, IMessage message)
    {
        var card = new SystemChatMessageViewModel(message)
        {
            Text = describable.GetDescription(),
        };

        return card;
    }

    #region Helper methods

    private Thickness CalculateTimePadding(DateTime timestamp)
    {
        if (lastChatMessage == null)
        {
            return new Thickness(0, 0, 0, 0);
        }

        var minutes = (int)(timestamp - lastChatMessage.Timestamp).TotalMinutes;
        var gap = Math.Min(minutes, messageMaxPadding);
        return new Thickness(0, gap, 0, 0);
    }

    private async Task SendSystemMessage(SystemTextMessage systemMessage, bool needsToNotify = false,
        bool countTowardCap = false)
    {
        await AddMessageToChatFlow(systemMessage, countTowardCap: countTowardCap);

        if (needsToNotify)
        {
            await PublishNewDescribable(systemMessage.Id, systemMessage, roomCts.Token);
            NotifyIfNeeded(systemMessage);
        }
    }

    private async Task InsertPrivateChatSystemMessageIfNeeded(Guid senderId, Guid? recipientId,
        bool isCurrentPrivate)
    {
        if (!isCurrentPrivate)
        {
            return;
        }

        var otherParticipantId = senderId == localParticipant.Id ? recipientId : senderId;
        if (lastPrivateChatParticipantId != null && otherParticipantId == lastPrivateChatParticipantId)
        {
            return;
        }

        lastPrivateChatParticipantId = otherParticipantId;

        var sender = room.CurrentParticipants.FirstOrDefault(x => x.Id == senderId);
        var recipient = room.CurrentParticipants.FirstOrDefault(x => x.Id == recipientId);

        await SendSystemMessage(new SystemTextMessage
        {
            Content = recipient?.Id != localParticipant.Id
                ? $"Это начало приватного общения с {recipient?.Name}"
                : $"{sender?.Name} прислал вам приватное сообщение:",
            RecipientId = localParticipant.Id,
        });
    }

    #endregion
}
