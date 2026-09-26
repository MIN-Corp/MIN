using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Chat.Events;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Desktop.ViewModels.Base;

namespace MIN.Desktop.ViewModels.Cards;

/// <summary>
/// Модель карточки участника на боковой панели
/// </summary>
public partial class ParticipantCardViewModel : CardViewModelBase, IDisposable
{
    private readonly Participant participant;
    private readonly bool isSelf;
    private IEventScope roomScope = null!;

    /// <summary>
    /// Идентфикатор участника на карточке
    /// </summary>
    public Guid ParticipantId => participant.Id;

    /// <summary>
    /// Имя участника
    /// </summary>
    [ObservableProperty]
    public partial string ParticipantName { get; set; } = string.Empty;

    /// <summary>
    /// Статус участника
    /// </summary>
    [ObservableProperty]
    public partial OnlineStatus ParticipantStatus { get; set; }

    /// <summary>
    /// Время последнего онлайн
    /// </summary>
    [ObservableProperty]
    public partial DateTime ParticipantLastSeenAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Выбрана ли карточка в качестве приватного собеседника
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>
    /// Является ли сейчас пользователь не в сети
    /// </summary>
    [ObservableProperty]
    public partial bool IsOffline { get; set; }

    /// <summary>
    /// Является ли пользователь хостом
    /// </summary>
    [ObservableProperty]
    public partial bool IsHost { get; set; }

    /// <summary>
    /// Может ли кикнуть участника
    /// </summary>
    [ObservableProperty]
    public partial bool CanKick { get; set; }

    /// <summary>
    /// Может ли приватно общатся
    /// </summary>
    [ObservableProperty]
    public partial bool CanStartPrivateChat { get; set; }

    /// <summary>
    /// Может ли вообще что то делать
    /// </summary>
    [ObservableProperty]
    public partial bool CanInteract { get; set; }

    /// <summary>
    /// Событие по нажатию на кнопку начала приватного общения у участника
    /// </summary>
    public Action<bool, Participant>? OnPrivateChatMenuStripClicked { get; set; }

    /// <summary>
    /// Событие по нажатию на кнопку кика участника
    /// </summary>
    public Action<Participant>? OnKickParticipantClicked { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ParticipantCardViewModel"/>
    /// </summary>
    public ParticipantCardViewModel(Participant participant,
        IEventBus eventBus,
        Guid roomId,
        bool isHost,
        bool isSelf,
        bool asHost)
    {
        this.participant = participant;
        this.isSelf = isSelf;
        CanKick = asHost && !isSelf;
        IsHost = isHost;
        CanStartPrivateChat = !isSelf;
        CanInteract = CanKick || CanStartPrivateChat;

        if (!Design.IsDesignMode)
        {
            FillLabels();

            if (!isSelf)
            {
                SubscribeToEvents(eventBus, roomId);
            }
        }
    }

    private void SubscribeToEvents(IEventBus eventBus, Guid roomId)
    {
        roomScope = eventBus.CreateScope(roomId);
        roomScope.Subscribe<OnlineStatusChangedEvent>(OnOnlineStatusChanged);
    }

    private Task OnOnlineStatusChanged(OnlineStatusChangedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.Participant.Id == participant.Id)
        {
            ParticipantStatus = isSelf ? OnlineStatus.Online : eventMessage.Status;
            IsOffline = ParticipantStatus == OnlineStatus.Offline;
            if (eventMessage.Status == OnlineStatus.Offline)
            {
                ParticipantLastSeenAt = DateTime.Now;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Пометить как оффлайн (себя не помечает)
    /// </summary>
    public void MarkAsOffline()
    {
        if (IsOffline)
        {
            return;
        }

        ParticipantStatus = isSelf ? OnlineStatus.Online : OnlineStatus.Offline;
        IsOffline = ParticipantStatus == OnlineStatus.Offline;
        ParticipantLastSeenAt = DateTime.Now;
    }

    /// <summary>
    /// Отменить выбор карточки
    /// </summary>
    public void Unselect()
    {
        IsSelected = false;
    }

    [RelayCommand]

    private void TogglePrivateChat()
    {
        IsSelected = !IsSelected;
        OnPrivateChatMenuStripClicked?.Invoke(IsSelected, participant);
    }

    [RelayCommand]
    private void KickParticipant()
    {
        OnKickParticipantClicked?.Invoke(participant);
    }

    private void FillLabels()
    {
        ParticipantName = participant.Name;
        ParticipantStatus = isSelf ? OnlineStatus.Online : participant.CurrentStatus;
        IsOffline = ParticipantStatus == OnlineStatus.Offline;
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public override void Dispose()
    {
        roomScope.Dispose();
        base.Dispose();
    }
}
