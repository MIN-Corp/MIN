using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Desktop.ViewModels.Base;
using MIN.Voice.Events;

namespace MIN.Desktop.ViewModels.Cards;

/// <summary>
/// Модель карточки участника в voice чате
/// </summary>
public partial class ParticipantVoiceCardViewModel : CardViewModelBase
{
    private readonly DispatcherTimer talkingTimer = new(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, Dispatcher.UIThread);

    private readonly Participant participant;

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
    /// Является ли участник локальным пользователем
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelf { get; set; }

    /// <summary>
    /// Заглушен ли микрофон у участника
    /// </summary>
    [ObservableProperty]
    public partial bool MicMuted { get; set; }

    /// <summary>
    /// Заглушен участникик посредством пользователя
    /// </summary>
    [ObservableProperty]
    public partial bool ForceMuted { get; set; }

    /// <summary>
    /// Разговаривает ли сейчас участник
    /// </summary>
    [ObservableProperty]
    public partial bool Talking { get; set; }

    /// <summary>
    /// Выбранная громкость
    /// </summary>
    [ObservableProperty]
    public partial int DesiredVolume { get; set; } = 100;

    /// <summary>
    /// Демонстрирует ли участник экран
    /// </summary>
    [ObservableProperty]
    public partial bool IsStreaming { get; set; }

    /// <summary>
    /// Подкомната демонстрации участника (если демонстрирует)
    /// </summary>
    [ObservableProperty]
    public partial int? DemoSubRoomId { get; set; }

    /// <summary>
    /// Смотрит ли локальный пользователь демонстрацию этого участника
    /// </summary>
    [ObservableProperty]
    public partial bool IsWatchingDemo { get; set; }

    /// <summary>
    /// Событие по переключению заглушки участника
    /// </summary>
    public Action<bool>? OnForceMuted;

    /// <summary>
    /// Событие по переключению заглушки участника
    /// </summary>
    public Action<int>? OnParticipantDesiredVolumeChanged;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ParticipantVoiceCardViewModel"/>
    /// </summary>
    public ParticipantVoiceCardViewModel(Participant participant,
        IEventScope roomScope,
        bool isSelf)
    {
        this.participant = participant;
        IsSelf = isSelf;

        if (!Design.IsDesignMode)
        {
            ParticipantName = participant.Name;
            SubscribeToEvents(roomScope);
            talkingTimer.Tick += OnTalkingTimerTick;
        }
    }

    partial void OnDesiredVolumeChanged(int value) => OnParticipantDesiredVolumeChanged?.Invoke(value / 2);

    private void OnTalkingTimerTick(object? sender, EventArgs e)
        => Talking = false;

    private void SubscribeToEvents(IEventScope roomScope)
    {
        roomScope.Subscribe<VoiceDataReceivedEvent>(OnVoiceDataReceived);
        roomScope.Subscribe<VoiceMuteStateChangedEvent>(OnVoiceMuteStateChanged);
    }

    private Task OnVoiceDataReceived(VoiceDataReceivedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.ParticipantId == participant.Id)
        {
            Talking = true;
            talkingTimer.Start();
        }
        return Task.CompletedTask;
    }

    private Task OnVoiceMuteStateChanged(VoiceMuteStateChangedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.ParticipantId == participant.Id)
        {
            MicMuted = eventMessage.Muted;
            if (MicMuted)
            {
                Talking = false;
                talkingTimer.Stop();
            }
        }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void ToggleMute()
    {
        OnForceMuted?.Invoke(ForceMuted);
        ForceMuted = !ForceMuted;
    }
}
