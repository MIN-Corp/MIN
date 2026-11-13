using Avalonia.Controls;
using MIN.Desktop.ViewModels.Windows;

namespace MIN.Desktop.Views.Windows;

/// <summary>
/// Окно просмотра чужой демонстрации экрана
/// </summary>
public partial class ScreenDemoViewerWindow : Window
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoViewerWindow"/>
    /// </summary>
    public ScreenDemoViewerWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is ScreenDemoViewerViewModel viewModel)
        {
            viewModel.FrameRendered += () => ScreenImage.InvalidateVisual();
        }
    }
}
