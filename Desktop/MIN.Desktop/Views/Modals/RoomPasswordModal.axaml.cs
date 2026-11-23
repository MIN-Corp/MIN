using MIN.Desktop.Contracts.Attributes;
using MIN.Desktop.ViewModels.Modals;
using MIN.Desktop.Views.Base;

namespace MIN.Desktop.Views.Modals;

/// <summary>
/// Всплывающее окно ввода пароля комнаты
/// </summary>
[ModalForViewModel(typeof(RoomPasswordViewModel))]
public partial class RoomPasswordModal : ModalViewBase
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomPasswordModal"/>
    /// </summary>
    public RoomPasswordModal()
    {
        InitializeComponent();
    }
}
