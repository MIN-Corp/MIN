using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Stores.Contracts.Models;
using MIN.Desktop.Infrastructure.Events;
using MIN.Desktop.ViewModels.Base;

namespace MIN.Desktop.ViewModels.Cards;

/// <summary>
/// Модель карточки комнаты на боковой панели
/// </summary>
public partial class RecentRoomCardViewModel : CardViewModelBase
{
    private readonly RoomContext roomContext;
    private readonly RoomInfo roomInfo;
    private Guid lastMessageId;
    private IEventScope roomScope = null!;

    private int currentAmount;
    private int maximumAmount;

    /// <summary>
    /// Идентификатор комнаты
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Имя комнаты
    /// </summary>
    [ObservableProperty]
    public partial string RoomName { get; set; }

    /// <summary>
    /// Информация об подключенных участниках в формете (подключено)/(максимум)
    /// </summary>
    [ObservableProperty]
    public partial string ParticipantsInfo { get; set; } = string.Empty;

    /// <summary>
    /// Последнее сообщение
    /// </summary>
    [ObservableProperty]
    public partial string LastMessageContent { get; set; } = string.Empty;

    /// <summary>
    /// Количество пропущенных сообщений
    /// </summary>
    [ObservableProperty]
    public partial int MissedMessagesCount { get; set; }

    /// <summary>
    /// Время получения последнего сообщенния
    /// </summary>
    [ObservableProperty]
    public partial DateTime LastMessageReceivedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Выбрана ли карточка
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>
    /// Событие по нажатию на саму карточку
    /// </summary>
    public Action? Clicked { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RecentRoomCardViewModel"/>
    /// </summary>
    public RecentRoomCardViewModel(IEventBus eventBus,
        RoomContext roomContext,
        RoomInfo roomInfo,
        bool AsCreator)
    {
        this.roomContext = roomContext;
        this.roomInfo = roomInfo;

        RoomId = roomInfo.Id;
        RoomName = roomInfo.Name;
        currentAmount = roomInfo.ParticipantCount + (AsCreator ? 1 : 0);
        maximumAmount = roomInfo.MaximumParticipants;

        GetLastMessage();
        UpdateParticipantsInfo();
        SubscribeToEvents(eventBus);
    }

    private void GetLastMessage()
    {
        var lastMessage = roomContext.Messages.GetLastMessage();
        if (lastMessage is IDescribable describable)
        {
            lastMessageId = lastMessage.Id;
            LastMessageContent = describable.GetDescription();
        }
    }

    /// <summary>
    /// Выбрать карточку
    /// </summary>
    public void SelectCard()
    {
        IsSelected = true;
        MissedMessagesCount = 0;
    }

    /// <summary>
    /// Выбрать карточку
    /// </summary>
    [RelayCommand]
    public void SelectItem()
    {
        Clicked?.Invoke();
    }

    private void SubscribeToEvents(IEventBus eventBus)
    {
        roomScope = eventBus.CreateScope(RoomId);
        roomScope.Subscribe<ParticipantJoinedEvent>(OnParticipantJoined);
        roomScope.Subscribe<ParticipantLeftEvent>(OnParticipantLeft);
        roomScope.Subscribe<RoomInfoUpdatedMessageEvent>(OnRoomInfoUpdatedMessageEvent);
        roomScope.Subscribe<DescribableMessageReceivedEvent>(OnDescribableMessageReceivedEvent);
        roomScope.Subscribe<MessageDeletedEvent>(OnChatMessageDeleted);
        roomScope.Subscribe<MessageEditedEvent>(OnChatMessageEdited);
        roomScope.Subscribe<RoomDestroyedEvent>(OnRoomDestroyed);
    }

    private Task OnParticipantJoined(ParticipantJoinedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.IsRejoin)
        {
            return Task.CompletedTask;
        }

        currentAmount++;
        UpdateParticipantsInfo();
        return Task.CompletedTask;
    }

    private Task OnParticipantLeft(ParticipantLeftEvent eventMessage, CancellationToken cancellationToken)
    {
        if (!eventMessage.Message.IsLeftRoom)
        {
            return Task.CompletedTask;
        }

        currentAmount--;
        UpdateParticipantsInfo();
        return Task.CompletedTask;
    }

    private Task OnRoomDestroyed(RoomDestroyedEvent eventMessage, CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    private Task OnChatMessageDeleted(MessageDeletedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (lastMessageId == eventMessage.MessageId)
        {
            GetLastMessage();
        }
        return Task.CompletedTask;
    }

    private Task OnChatMessageEdited(MessageEditedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (lastMessageId == eventMessage.MessageId)
        {
            GetLastMessage();
        }

        return Task.CompletedTask;
    }

    private Task OnDescribableMessageReceivedEvent(DescribableMessageReceivedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (!IsSelected)
        {
            MissedMessagesCount++;
        }
        LastMessageReceivedAt = DateTime.Now;
        LastMessageContent = eventMessage.DescribableMessage.GetDescription();
        lastMessageId = eventMessage.MessageId;
        return Task.CompletedTask;
    }

    private Task OnRoomInfoUpdatedMessageEvent(RoomInfoUpdatedMessageEvent eventMessage, CancellationToken ct)
    {
        RoomName = eventMessage.RoomInfo.Name;
        roomInfo.Name = eventMessage.RoomInfo.Name;
        maximumAmount = eventMessage.RoomInfo.MaximumParticipants;

        UpdateParticipantsInfo();
        return Task.CompletedTask;
    }

    private void UpdateParticipantsInfo()
    {
        ParticipantsInfo = $"{currentAmount}/{maximumAmount}";
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public override void Dispose()
    {
        roomScope.Dispose();
        base.Dispose();
    }
}
