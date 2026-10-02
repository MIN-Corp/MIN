using Avalonia.Controls;
using MIN.Desktop.Contracts.Constants;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.ViewModels.Windows;
using MIN.Desktop.Views.Base;

namespace MIN.Desktop.Views;

/// <summary>
/// Главное окно
/// </summary>
public partial class MainWindow : WindowEx<MainWindowViewModel>
{
    /// <summary>
    /// Текущий Layout
    /// </summary>
    public WindowLayout CurrentLayout { get; private set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MainWindow"/>
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        CalculateResize(e.NewSize.Width);
    }

    private void CalculateResize(double width)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var mode = width switch
        {
            > 1100 => WindowLayout.ThreeColumns,
            > 600 => WindowLayout.TwoColumns,
            _ => WindowLayout.Narrow
        };
        CurrentLayout = mode;
        UpdateColumnDefinitions(mode);
        vm.UpdateLayout(mode);
    }

    private void UpdateColumnDefinitions(WindowLayout layout)
    {
        switch (layout)
        {
            case WindowLayout.ThreeColumns:
                Center.SetValue(Grid.ColumnProperty, 1);
                Right.SetValue(Grid.ColumnProperty, 2);
                break;

            case WindowLayout.TwoColumns:
                Center.SetValue(Grid.ColumnProperty, 1);
                Right.SetValue(Grid.ColumnProperty, 1);
                break;

            case WindowLayout.Narrow:
                Center.SetValue(Grid.ColumnProperty, 0);
                Right.SetValue(Grid.ColumnProperty, 0);
                break;
        }

        var defs = layout switch
        {
            WindowLayout.ThreeColumns => "Auto,*,Auto",
            WindowLayout.TwoColumns => "Auto,*",
            WindowLayout.Narrow => "*",
            _ => "*"
        };
        WindowGrid.ColumnDefinitions = ColumnDefinitions.Parse(defs);
    }

    /// <summary>
    /// При следующем закрытии сохранить в трей
    /// </summary>
    public bool MinimizeToTrayEnabled = DesktopConstants.MinimizeToTrayEnabled;

    /// <inheritdoc cref="Window.OnClosing(WindowClosingEventArgs)"/>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (MinimizeToTrayEnabled)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }
}
