using Avalonia.Controls;
using ScreenDemoStreamViewModel = MIN.Desktop.ViewModels.Cards.ScreenDemoStreamViewModel;

namespace MIN.Desktop.Views.Cards;

/// <summary>
/// Встроенный просмотр потока демонстрации экрана
/// </summary>
public partial class ScreenDemoStreamView : UserControl
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoStreamView"/>
    /// </summary>
    public ScreenDemoStreamView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is ScreenDemoStreamViewModel viewModel)
        {
            viewModel.FrameRendered += () => StreamImage.InvalidateVisual();
        }
    }
}
