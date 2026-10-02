using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace MIN.Desktop.Infrastructure.Behaviors.ScrollViewers;

/// <summary>
/// Прокрутка <see cref="ScrollViewer"/> вниз и вверх
/// </summary>
public class AutoScrollToBottomBehavior : StyledElementBehavior<ScrollViewer>
{
    private const int ScrollMaxAttempts = 3;

    /// <summary>
    /// Авто скролл вверх
    /// </summary>
    public readonly static AttachedProperty<bool> AutoScrollUpProperty =
        AvaloniaProperty.RegisterAttached<AutoScrollToBottomBehavior, ScrollViewer, bool>("AutoScrollUp");

    /// <summary>
    /// Авто скролл вверх
    /// </summary>
    public readonly static AttachedProperty<bool> AutoScrollBottomProperty =
        AvaloniaProperty.RegisterAttached<AutoScrollToBottomBehavior, ScrollViewer, bool>("AutoScrollBottom");

    static AutoScrollToBottomBehavior()
    {
        AutoScrollUpProperty.Changed.AddClassHandler<ScrollViewer>(OnAutoScrollUpChanged);
        AutoScrollBottomProperty.Changed.AddClassHandler<ScrollViewer>(OnAutoScrollBottomChanged);
    }

    /// <summary>
    /// Установить авто скролл вверх
    /// </summary>
    public static void SetAutoScrollUp(ScrollViewer element, bool value) =>
        element.SetValue(AutoScrollUpProperty, value);

    /// <summary>
    /// Получить авто скролл вверх
    /// </summary>
    public static bool GetAutoScrollUp(ScrollViewer element) =>
        element.GetValue(AutoScrollUpProperty);

    /// <summary>
    /// Установить авто скролл вниз
    /// </summary>
    public static void SetAutoScrollBottom(ScrollViewer element, bool value) =>
        element.SetValue(AutoScrollBottomProperty, value);

    /// <summary>
    /// Получить авто скролл вниз
    /// </summary>
    public static bool GetAutoScrollBottom(ScrollViewer element) =>
        element.GetValue(AutoScrollBottomProperty);

    private static void OnAutoScrollUpChanged(ScrollViewer sv, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            sv.ScrollToHome();
        }
    }

    private static void OnAutoScrollBottomChanged(ScrollViewer sv, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            ScrollToEndWhenSettled(sv);
        }
    }

    private static async void ScrollToEndWhenSettled(ScrollViewer sv)
    {
        var dispatcher = Dispatcher.UIThread;

        for (var attempt = 0; attempt < ScrollMaxAttempts; attempt++)
        {
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            sv.ScrollToEnd();

            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            if (sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - 0.5)
            {
                break;
            }
        }
    }
}
