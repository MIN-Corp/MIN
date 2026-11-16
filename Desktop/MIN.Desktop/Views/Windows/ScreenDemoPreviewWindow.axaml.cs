using Avalonia.Controls;
using MIN.Desktop.ViewModels.Windows;

namespace MIN.Desktop.Views.Windows;

/// <summary>
/// Окно предпросмотра собственной демонстрации экрана
/// </summary>
public partial class ScreenDemoPreviewWindow : Window
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoPreviewWindow"/>
    /// </summary>
    public ScreenDemoPreviewWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is ScreenDemoPreviewViewModel viewModel)
        {
            viewModel.FrameRendered += () => ScreenImage.InvalidateVisual();
        }
    }
}
