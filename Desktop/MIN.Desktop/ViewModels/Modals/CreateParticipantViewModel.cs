using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Infrastructure.Validators;
using MIN.Desktop.ViewModels.Base;

namespace MIN.Desktop.ViewModels.Modals;

/// <summary>
/// Модель окна создания комнаты
/// </summary>
public partial class CreateParticipantViewModel : ModalViewModelBase
{
    private readonly IIdentityService identityService;

    [ObservableProperty]
    [Display(Name = "Имя участника")]
    [NotifyCanExecuteChangedFor(nameof(ProceedCommand))]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Введите своё имя")]
    [ParticipantName]
    [NotEndsWith(".")]
    public partial string Name { get; set; } = "";

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="CreateParticipantViewModel"/>
    /// </summary>
    public CreateParticipantViewModel(IIdentityService identityService)
    {
        this.identityService = identityService;
    }

    [RelayCommand(CanExecute = nameof(CanProceed))]
    private async Task Proceed()
    {
        var participant = identityService.SelfParticipant.ToParticipantInfo();
        participant.Name = Name;
        await identityService.SaveParticipant(participant);
        Close(ButtonOptions.Ok);
    }

    private bool CanProceed() => !HasErrors;
}
