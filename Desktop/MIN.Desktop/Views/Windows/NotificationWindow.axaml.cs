using System;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace MIN.Desktop.Views.Windows;

/// <summary>
/// Всплывающее уведомление
/// </summary>
public partial class NotificationWindow : Window
{
    private readonly DispatcherTimer dismissTimer = new()
    {
        Interval = TimeSpan.FromSeconds(7)
    };

    /// <summary>
    /// Закрывается ли уведомление
    /// </summary>
    public bool IsDismissing { get; set; }

    /// <summary>
    /// Событие по нажатию на отписку от уведомлений
    /// </summary>
    public Action? NotificationTurnOffClicked { get; set; }

    /// <summary>
    /// Событие по нажатию на уведомление
    /// </summary>
    public Action? NotificationClicked { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="NotificationWindow"/>
    /// </summary>
    public NotificationWindow() { }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="NotificationWindow"/>
    /// </summary>
    public NotificationWindow(string message, string roomName, Window? owner)
    {
        InitializeComponent();

        if (owner != null)
        {
            Owner = owner;
        }

        DataContext = this;
        MessageText.Text = message;
        RoomNameText.Text = $"Комната {roomName}";

        InitializeTimer();
    }

    private void InitializeTimer()
    {
        dismissTimer.Tick += async (_, _) => await CloseWithAnimation();
        dismissTimer.Start();
    }

    private async void UnsubscribePressed(object? sender, RoutedEventArgs e)
    {
        NotificationTurnOffClicked?.Invoke();
        await CloseWithAnimation();
    }

    private async void ClosePressed(object? sender, RoutedEventArgs e)
    {
        await CloseWithAnimation();
    }

    private async void NotificationPressed(object? sender, PointerPressedEventArgs e)
    {
        NotificationClicked?.Invoke();
        await CloseWithAnimation();
    }

    /// <summary>
    /// Закрыть уведомление
    /// </summary>
    public async Task CloseWithAnimation()
    {
        dismissTimer.Stop();
        IsDismissing = true;
        Opacity = 0;

        Transitions?.Add(new DoubleTransition
        {
            Property = OpacityProperty,
            Duration = TimeSpan.FromSeconds(0.2)
        });

        // Wait for animations
        await Task.Delay(200);
        Close();
    }
}
