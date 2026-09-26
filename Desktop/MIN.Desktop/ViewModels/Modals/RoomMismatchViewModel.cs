using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Models;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.ViewModels.Base;

namespace MIN.Desktop.ViewModels.Modals;

/// <summary>
/// Модель окна смены идентификации комнаты
/// </summary>
public partial class RoomMismatchViewModel : ModalViewModelBase
{
    [ObservableProperty]
    public partial RoomInfo ActualRoom { get; set; }

    /// <summary>
    /// Появилось ли это окно во время reconnect
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReplaceCommand))]
    public partial bool IsReconnect { get; set; }

    /// <summary>
    /// Выбор действия
    /// </summary>
    public RoomMismatchChoice Choice { get; set; } = RoomMismatchChoice.Cancel;

    [RelayCommand]
    private void JoinNew()
    {
        Choice = RoomMismatchChoice.JoinNew;
        CloseByCode();
    }

    [RelayCommand(CanExecute = nameof(IsReconnect))]
    private void Replace()
    {
        Choice = RoomMismatchChoice.Replace;
        CloseByCode();
    }
}
