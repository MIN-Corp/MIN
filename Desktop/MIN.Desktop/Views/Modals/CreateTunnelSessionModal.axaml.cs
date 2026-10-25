using MIN.Desktop.Contracts.Attributes;
using MIN.Desktop.ViewModels.Modals;
using MIN.Desktop.Views.Base;

namespace MIN.Desktop.Views.Modals;

/// <summary>
/// Всплывающее окно создания сессии
/// </summary>
[ModalForViewModel(typeof(CreateTunnelSessionViewModel))]
public partial class CreateTunnelSessionModal : ModalViewBase
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="CreateTunnelSessionModal"/>
    /// </summary>
    public CreateTunnelSessionModal()
    {
        InitializeComponent();
    }
}
