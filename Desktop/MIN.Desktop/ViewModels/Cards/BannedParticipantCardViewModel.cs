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
    [ObservableProperty]
    public partial BanInfo BanInfo { get; set; }

    /// <summary>
    /// Текст даты разбана
    /// </summary>
    [ObservableProperty]
    public partial string UnbanDateText { get; set; }

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
    public BannedParticipantCardViewModel(BanInfo banInfo, string date)
    {
        BanInfo = banInfo;
        UnbanDateText = date;
    }

    /// <summary>
    /// Обновить текст даты разбана
    /// </summary>
    public void UpdateUnbanDate(string unbanDateText)
    {
        UnbanDateText = unbanDateText;
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
