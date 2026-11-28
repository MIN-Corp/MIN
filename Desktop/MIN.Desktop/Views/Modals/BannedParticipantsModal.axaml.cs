using MIN.Desktop.Contracts.Attributes;
using MIN.Desktop.ViewModels.Modals;
using MIN.Desktop.Views.Base;

namespace MIN.Desktop.Views.Modals;

/// <summary>
/// Всплывающее окно чёрного списка участников
/// </summary>
[ModalForViewModel(typeof(BannedParticipantsViewModel))]
public partial class BannedParticipantsModal : ModalViewBase
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BannedParticipantsModal"/>
    /// </summary>
    public BannedParticipantsModal()
    {
        InitializeComponent();
    }
}
