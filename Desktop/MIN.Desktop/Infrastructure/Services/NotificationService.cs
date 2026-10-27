using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Desktop.ViewModels.Windows;
using MIN.Desktop.Views.Windows;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Desktop.Infrastructure.Services;

/// <inheritdoc cref="INotificationService"/>
public class NotificationService : INotificationService
{
    private const int NotificationsOffset = 8;
    private const int AllowedNotificationsAtOnce = 3;

    private readonly static List<NotificationWindow> activeNotifications = [];
    private Window? parentWindow;
    private bool closing;

    /// <summary>
    /// Событие по нажатию на уведомление
    /// </summary>
    public event Action? OnNotificationClick;

    /// <summary>
    /// Событие по нажатию на выключение уведомления
    /// </summary>
    public event Action? NotificationTurnOffClicked;

    void INotificationService.Notify(IDescribable describable, string roomName)
    {
        EnsureParentWindow();
        Dispatcher.UIThread.Post(() => CreateAndShow(describable.GetDescription(), roomName));
    }

    void INotificationService.Notify(string message, string roomName)
    {
        EnsureParentWindow();
        Dispatcher.UIThread.Post(() => CreateAndShow(message, roomName));
    }

    private void EnsureParentWindow()
    {
        if (parentWindow == null)
        {
            parentWindow = MainWindowViewModel.GetWindow();
            parentWindow!.Closed += (_, _) =>
            {
                closing = true;
                foreach (var n in activeNotifications)
                {
                    n.Close();
                }

                activeNotifications.Clear();
            };
        }
    }

    private void CreateAndShow(string message, string roomName)
    {
        var notification = new NotificationWindow(message, roomName, parentWindow);
        notification.NotificationClicked += OnNotificationClick;
        notification.NotificationTurnOffClicked += NotificationTurnOffClicked;
        notification.Closed += (_, _) =>
        {
            if (closing)
            {
                return;
            }

            activeNotifications.Remove(notification);
            RepositionAll();
        };

        if (activeNotifications.Count > AllowedNotificationsAtOnce - 1)
        {
            activeNotifications.Remove(activeNotifications.Last());
        }

        activeNotifications.Insert(0, notification);
        notification.Show();
        RepositionAll();
    }

    private void RepositionAll()
    {
        if (parentWindow == null)
        {
            return;
        }

        var screen = parentWindow.Screens.Primary;
        var baseX = screen!.WorkingArea.Right - NotificationsOffset;
        var baseY = screen.WorkingArea.Bottom - NotificationsOffset;
        var offsetY = 0;

        foreach (var n in activeNotifications.Where(x => !x.IsDismissing))
        {
            var newX = (int)(baseX - n.Width);
            var newY = (int)(baseY - n.Height - offsetY);
            n.Position = new PixelPoint(newX, newY);
            offsetY += (int)n.Height + NotificationsOffset;
        }
    }
}
