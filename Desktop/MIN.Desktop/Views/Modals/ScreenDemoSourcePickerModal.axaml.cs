using MIN.Desktop.Contracts.Attributes;
using MIN.Desktop.ViewModels.Modals;
using MIN.Desktop.Views.Base;

namespace MIN.Desktop.Views.Modals;

/// <summary>
/// Всплывающее окно "регистрации" участника
/// </summary>
[ModalForViewModel(typeof(ScreenDemoSourcePickerViewModel))]
public partial class ScreenDemoSourcePickerModal : ModalViewBase
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoSourcePickerModal"/>
    /// </summary>
    public ScreenDemoSourcePickerModal()
    {
        InitializeComponent();
    }
}
