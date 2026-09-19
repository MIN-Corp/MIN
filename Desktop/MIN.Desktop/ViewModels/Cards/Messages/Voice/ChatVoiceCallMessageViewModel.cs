using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Voice.Events;
using MIN.Voice.Messaging;

namespace MIN.Desktop.ViewModels.Cards.Messages.Voice;

/// <summary>
/// Сообщение звонка участника
/// </summary>
public partial class ChatVoiceCallMessageViewModel : BaseChatMessageViewModel
{
    private readonly VoiceCallStartedMessage voiceCallStartedMessage;
    private readonly DispatcherTimer callTimer = new(TimeSpan.FromSeconds(1), DispatcherPriority.Background, Dispatcher.UIThread);

    /// <summary>
    /// Описание состояния звонка
    /// </summary>
    [ObservableProperty]
    public partial string Description { get; set; }

    /// <summary>
    /// Длительность звонка (если он идёт)
    /// </summary>
    [ObservableProperty]
    public partial TimeSpan CallDuration { get; set; }

    /// <summary>
    /// Звонок отклонён
    /// </summary>
    [ObservableProperty]
    public partial bool AsRejected { get; set; }

    /// <summary>
    /// Учавствует
    /// </summary>
    [ObservableProperty]
    public partial bool AsJoined { get; set; }

    /// <summary>
    /// Звонок завершён
    /// </summary>
    [ObservableProperty]
    public partial bool AsEnded { get; set; }

    /// <summary>
    /// Событие, возникающее по нажатию на кнопку присоединиться
    /// </summary>
    public event Func<Task>? OnJoinRequested;

    /// <summary>
    /// Событие, возникающее по нажатию на кнопку присоединиться
    /// </summary>
    public event Func<Task>? OnLeaveRequested;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatVoiceCallMessageViewModel"/>
    /// </summary>
    public ChatVoiceCallMessageViewModel(IEventScope roomScope,
        VoiceCallStartedMessage voiceCallStartedMessage,
        ParticipantInfo localParticipant,
        Thickness timePadding,
        bool isHostMessage,
        bool removeHeaders,
        bool isAvaibleForNetwork)
        : base(voiceCallStartedMessage,
            null,
            voiceCallStartedMessage.Sender.Name,
            timePadding,
            localParticipant.Id == voiceCallStartedMessage.SenderId,
            isHostMessage,
            removeHeaders,
            isAvaibleForNetwork)
    {
        AsEnded = voiceCallStartedMessage.IsEnded;
        AsJoined = localParticipant.Id == voiceCallStartedMessage.SenderId && !AsEnded;
        Description = (voiceCallStartedMessage as IDescribable).GetDescription();

        this.voiceCallStartedMessage = voiceCallStartedMessage;
        if (!AsEnded)
        {
            CallDuration = DateTime.Now - voiceCallStartedMessage.Timestamp;
            callTimer.Tick += OnCallTimerTick;
            callTimer.Start();
        }

        SubscribeToEvents(roomScope);
    }

    private void SubscribeToEvents(IEventScope roomScope)
    {
        roomScope.Subscribe<VoiceCallEndedEvent>(OnVoiceCallEnded);
        roomScope.Subscribe<VoiceCallEstablishedEvent>(OnVoiceCallEstablished);
    }

    private void OnCallTimerTick(object? sender, EventArgs e)
        => CallDuration = DateTime.Now - voiceCallStartedMessage.Timestamp;

    private Task OnVoiceCallEnded(VoiceCallEndedEvent eventMessage, CancellationToken cancellationToken)
    {
        callTimer.Stop();
        callTimer.Tick -= OnCallTimerTick;
        AsEnded = true;
        Description = (voiceCallStartedMessage as IDescribable).GetDescription();
        return Task.CompletedTask;
    }

    private Task OnVoiceCallEstablished(VoiceCallEstablishedEvent eventMessage, CancellationToken cancellationToken)
    {
        AsRejected = false;
        AsJoined = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void JoinVoiceCall()
    {
        OnJoinRequested?.Invoke();
    }

    [RelayCommand]
    private void LeaveOrRejectVoiceCall()
    {
        if (AsJoined)
        {
            AsJoined = false;
            OnLeaveRequested?.Invoke();
            return;
        }

        AsRejected = true;
    }
}
