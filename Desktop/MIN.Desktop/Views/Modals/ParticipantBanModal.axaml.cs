using MIN.Desktop.Contracts.Attributes;
using MIN.Desktop.ViewModels.Modals;
using MIN.Desktop.Views.Base;

namespace MIN.Desktop.Views.Modals;

/// <summary>
/// Всплывающее окно бана участника
/// </summary>
[ModalForViewModel(typeof(ParticipantBanViewModel))]
public partial class ParticipantBanModal : ModalViewBase
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ParticipantBanModal"/>
    /// </summary>
    public ParticipantBanModal()
    {
        InitializeComponent();
    }
}
