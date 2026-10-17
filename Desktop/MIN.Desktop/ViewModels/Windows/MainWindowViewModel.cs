using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MIN.Core.Transport.Contracts.Helpers;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Contracts.Models;
using MIN.Desktop.Contracts.Models.ReferenceCommands;
using MIN.Desktop.Contracts.Models.ReferenceCommands.Layout;
using MIN.Desktop.Infrastructure.Extensions;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Base.Interfaces;
using MIN.Desktop.ViewModels.Pages;

namespace MIN.Desktop.ViewModels.Windows;

/// <summary>
/// Модель главного окна
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IMultiRoutingWindow
{
    private readonly Dictionary<ViewLayoutType, List<IRoutableViewModel>> navigationStack = new()
    {
        { ViewLayoutType.LeftSideBar, [] },
        { ViewLayoutType.Central, [] },
        { ViewLayoutType.RightSideBar, [] },
    };

    private readonly Dictionary<ViewLayoutType, CancellationTokenSource?> viewChangeBusyCtsByLayout = new()
    {
        { ViewLayoutType.LeftSideBar, null },
        { ViewLayoutType.Central, null },
        { ViewLayoutType.RightSideBar, null },
    };

    /// <summary>
    /// Поступил сигнал отмены при переходе
    /// </summary>
    public Action? RoutingCancellationRequested { get; set; }

    /// <summary>
    /// Окно было спрятано в tray
    /// </summary>
    public Action? OnMinimizedToTray { get; set; }

    [ObservableProperty]
    public partial bool IsCancellingRouting { get; set; }

    [ObservableProperty]
    public partial WindowLayout LayoutMode { get; set; } = WindowLayout.ThreeColumns;

    /// <inheritdoc />
    [ObservableProperty]
    public partial object? LeftSideBarViewModel { get; set; }

    /// <inheritdoc />
    [ObservableProperty]
    public partial object? CentralViewModel { get; set; }

    /// <inheritdoc />
    [ObservableProperty]
    public partial object? RightSideBarViewModel { get; set; }

    /// <inheritdoc />
    public Dictionary<ViewLayoutType, List<IRoutableViewModel>> NavigationStack => navigationStack;

    /// <inheritdoc />
    public Dictionary<ViewLayoutType, CancellationTokenSource?> ViewChangeBusyCtsByLayout => viewChangeBusyCtsByLayout;

    /// <inheritdoc />
    object? IMultiRoutingWindow.GetViewModelOutOfLayoutType(ViewLayoutType type)
        => type switch
        {
            ViewLayoutType.LeftSideBar => LeftSideBarViewModel,
            ViewLayoutType.Central => CentralViewModel,
            ViewLayoutType.RightSideBar => RightSideBarViewModel,
            _ => null
        };

    /// <summary>
    /// Текущие уведомления внутри приложения
    /// </summary>
    public AvaloniaList<NotificationItem> Notifications { get; init; } = [];

    partial void OnLayoutModeChanged(WindowLayout value)
        => WeakReferenceMessenger.Default.Send(new LayoutModeChangedReferenceCommand(value));

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MainWindowViewModel"/>
    /// </summary>
    public MainWindowViewModel(MainSideBarViewModel mainSideBarViewModel, DiscoveryViewModel discoveryViewModel)
    {
        this.RegisterMessageListener<ShowViewReferenceCommand, MainWindowViewModel>(async (message, vm) =>
        {
            try
            {
                await vm.ShowAsync(message.ViewModel, message.CancellationToken);
            }
            finally
            {
                IsCancellingRouting = false;
            }
        });
        this.RegisterMessageListener<CloseViewReferenceCommand, MainWindowViewModel>(static (message, vm)
            => vm.CloseAsync(message.LayoutType));
        this.RegisterMessageListener<ShowPreviousViewReferenceCommand, MainWindowViewModel>(async (message, vm)
            =>
        {
            if (message.RoutableViewModelType != null)
            {
                await vm.BackToAsync(message.RoutableViewModelType, message.LayoutType);
            }
            else
            {
                await vm.BackAsync(message.LayoutType);
            }
        });

        this.RegisterMessageListener<NotificationAddReferenceCommand, MainWindowViewModel>(static async (message, vm) =>
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                vm.Notifications.Add(message.NotificationItem);
            });
            await Task.Delay(7000);
            WeakReferenceMessenger.Default.Send(new NotificationCloseReferenceCommand(message.NotificationItem));
        });
        this.RegisterMessageListener<NotificationCloseReferenceCommand, MainWindowViewModel>(static async (message, vm) =>
        {
            message.NotificationItem.IsDismissed = true;

            await Task.Delay(1000); // Wait for animations

            if (!Design.IsDesignMode) // Prevent design preview crashes
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    vm.Notifications.Remove(message.NotificationItem);
                });
            }
        });

        this.RegisterMessageListener<ShowNavigationReferenceCommand, MainWindowViewModel>(async (message, vm) =>
        {
            // Это костыль, потому что discovery не может сослаться на mainSideBarViewModel
            await this.ShowAsync(mainSideBarViewModel);
        });

        if (!Design.IsDesignMode)
        {
            Task.Run(() =>
            {
                if (!NetworkHelper.HasInternetConnectivity())
                {
                    InAppNotifier.Warning("Launcher may not be connected to internet");
                }
                return Task.CompletedTask;
            });
        }

        _ = this.ShowAsync(mainSideBarViewModel);
        _ = this.ShowAsync(discoveryViewModel);
    }

    [RelayCommand]
    private void CancelRouting()
    {
        IsCancellingRouting = true;
        RoutingCancellationRequested?.Invoke();
        WeakReferenceMessenger.Default.Send(new CancelRoutingOperationReferenceCommand());
    }

    [RelayCommand]
    private void Minimize()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Windows[0].WindowState = WindowState.Minimized;
        }
    }

    [RelayCommand]
    private void Maximize()
    {
        var window = GetWindow();
        if (window is null)
        {
            return;
        }

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    [RelayCommand]
    private void Close()
    {
        GetWindow()?.Close();
    }

    /// <summary>
    /// Обновить разметку страницы из под размеров окнна
    /// </summary>
    public void UpdateLayout(WindowLayout newMode)
    {
        if (LayoutMode == newMode)
        {
            return;
        }

        LayoutMode = newMode;
        this.ArrangeLayout();
    }

    /// <summary>
    /// Получить окно приложения
    /// </summary>
    public static Window? GetWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime
            ? lifetime.Windows[0]
            : null;
}
