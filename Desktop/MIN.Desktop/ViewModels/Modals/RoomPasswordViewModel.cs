using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.ViewModels.Base;

namespace MIN.Desktop.ViewModels.Modals;

/// <summary>
/// Модель окна ввода пароля комнаты
/// </summary>
public partial class RoomPasswordViewModel : ModalViewModelBase
{
    [ObservableProperty]
    [Display(Name = "Пароль комнаты")]
    [NotifyCanExecuteChangedFor(nameof(ProceedCommand))]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Введите пароль")]
    public partial string Password { get; set; } = "";

    [RelayCommand(CanExecute = nameof(CanProceed))]
    private void Proceed() => Close(ButtonOptions.Ok);

    private bool CanProceed() => !HasErrors;
}
