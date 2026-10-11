using MIN.Desktop.Contracts.Attributes;
using MIN.Desktop.ViewModels.Modals;
using MIN.Desktop.Views.Base;

namespace MIN.Desktop.Views.Modals;

/// <summary>
/// Всплывающее окно смены идентификации комнаты
/// </summary>
[ModalForViewModel(typeof(RoomMismatchViewModel))]
public partial class RoomMismatchModal : ModalViewBase
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomMismatchModal"/>
    /// </summary>
    public RoomMismatchModal()
    {
        InitializeComponent();
    }
}
