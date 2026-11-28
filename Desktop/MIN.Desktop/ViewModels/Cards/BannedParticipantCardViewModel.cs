using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Models;

namespace MIN.Desktop.ViewModels.Cards;

/// <summary>
/// Модель карточки забаненного участника в чёрном списке
/// </summary>
public partial class BannedParticipantCardViewModel : ObservableObject
{
    /// <summary>
    /// Информация о бане участника
    /// </summary>
    public BanInfo BanInfo { get; private set; } = null!;

    /// <summary>
    /// Причина бана
    /// </summary>
    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Текст даты разбана
    /// </summary>
    [ObservableProperty]
    public partial string UnbanDateText { get; set; } = string.Empty;

    /// <summary>
    /// Действует ли бан по IP
    /// </summary>
    [ObservableProperty]
    public partial bool IsIpBanned { get; set; }

    /// <summary>
    /// Текст IP-адреса бана
    /// </summary>
    [ObservableProperty]
    public partial string IpAddressText { get; set; } = string.Empty;

    /// <summary>
    /// Событие по нажатию на кнопку изменения даты разбана
    /// </summary>
    public Action<BannedParticipantCardViewModel>? OnEditClicked { get; set; }

    /// <summary>
    /// Событие по нажатию на кнопку удаления из чёрного списка
    /// </summary>
    public Action<BannedParticipantCardViewModel>? OnRemoveClicked { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BannedParticipantCardViewModel"/>
    /// </summary>
    public BannedParticipantCardViewModel(BanInfo banInfo)
    {
        UpdateBan(banInfo);
    }

    /// <summary>
    /// Обновить бан
    /// </summary>
    public void UpdateBan(BanInfo banInfo)
    {
        BanInfo = banInfo;
        Reason = banInfo.Reason;
        IsIpBanned = banInfo.IsIpBanned;
        IpAddressText = banInfo.IpAddress is { } ip ? $"IP: {ip}" : "";
        UnbanDateText = banInfo.FormatUnbanDate();
    }

    [RelayCommand]
    private void Edit()
    {
        OnEditClicked?.Invoke(this);
    }

    [RelayCommand]
    private void Remove()
    {
        OnRemoveClicked?.Invoke(this);
    }
}
