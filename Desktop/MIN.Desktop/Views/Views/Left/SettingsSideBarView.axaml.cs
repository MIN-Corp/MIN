using MIN.Desktop.ViewModels.Pages;
using MIN.Desktop.Views.Base;

namespace MIN.Desktop.Views.Views.Left;

/// <summary>
/// Страница боковой панели
/// </summary>
public partial class SettingsSideBarView : RoutableViewBase<SettingsSideBarViewModel>
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SettingsSideBarView"/>
    /// </summary>
    public SettingsSideBarView()
    {
        InitializeComponent();
    }
}
