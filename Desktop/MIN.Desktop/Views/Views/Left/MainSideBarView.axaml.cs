using MIN.Desktop.ViewModels.Pages;
using MIN.Desktop.Views.Base;

namespace MIN.Desktop.Views.Views.Left;

/// <summary>
/// Страница боковой панели
/// </summary>
public partial class MainSideBarView : RoutableViewBase<MainSideBarViewModel>
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MainSideBarView"/>
    /// </summary>
    public MainSideBarView()
    {
        InitializeComponent();
    }
}
