using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Models;
using MIN.Desktop.Contracts.Models;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.ViewModels.Base;

namespace MIN.Desktop.ViewModels.Modals;

/// <summary>
/// Модель окна бана участника
/// </summary>
public partial class ParticipantBanViewModel : ModalViewModelBase
{
    /// <summary>
    /// Доступные варианты длительности бана
    /// </summary>
    public IReadOnlyList<BanDurationOption> DurationOptions { get; } = BanDurationOption.All;

    /// <summary>
    /// Имя участника
    /// </summary>
    [ObservableProperty]
    public partial string ParticipantName { get; set; } = "";

    /// <summary>
    /// Причина бана
    /// </summary>
    [ObservableProperty]
    public partial string Reason { get; set; } = "";

    /// <summary>
    /// Выбранная длительность бана
    /// </summary>
    [ObservableProperty]
    public partial BanDurationOption SelectedDuration { get; set; } = null!;

    /// <summary>
    /// Режим редактирования даты разбана (вместо нового бана)
    /// </summary>
    [ObservableProperty]
    public partial bool IsEditMode { get; set; }

    /// <summary>
    /// Выбранная длительность
    /// </summary>
    public BanDuration SelectedValue => SelectedDuration.Value;

    /// <summary>
    /// Инициализировать окно для нового бана
    /// </summary>
    public void InitializeForBan(string participantName)
    {
        ParticipantName = participantName;
        IsEditMode = false;
        SelectedDuration = DurationOptions[0];
    }

    /// <summary>
    /// Инициализировать окно для изменения даты разбана
    /// </summary>
    public void InitializeForEdit(BanInfo banInfo)
    {
        ParticipantName = banInfo.Name;
        Reason = banInfo.Reason;
        IsEditMode = true;
        SelectedDuration = DurationOptions[1];
    }

    [RelayCommand]
    private void Ban()
    {
        Close(ButtonOptions.Ok);
    }
}
