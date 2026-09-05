using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using MIN.Core.Entities.Contracts.Models;

namespace MIN.Desktop.Infrastructure.Services;

/// <summary>
/// Сервис по предаставлению сохранения приложения в трее
/// </summary>
public sealed class TrayService : IDisposable
{
    private TrayIcon? trayIcon;
    private readonly NativeMenu menu = [];
    private readonly List<NativeMenuItem> roomItems = [];

    /// <summary>
    /// Инициализировать
    /// </summary>
    public void Initialize(string iconUri)
    {
        trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri(iconUri))),
            ToolTipText = "MIN",
            Menu = menu
        };
        trayIcon.Clicked += (_, _) => Dispatcher.UIThread.Post(() => ShowRequested?.Invoke());

        var separator = new NativeMenuItemSeparator();
        roomItems.Add(separator);
        AddMenuItem("Показать", () => ShowRequested?.Invoke());
        menu.Items.Add(separator);
        AddMenuItem("Выход", () => ExitRequested?.Invoke());
    }

    /// <summary>
    /// Нажата кнопка "Показать" на трее
    /// </summary>
    public event Action? ShowRequested;

    /// <summary>
    /// Нажата кнопка "Выход" на трее
    /// </summary>
    public event Action? ExitRequested;

    /// <summary>
    /// Пользователь захотел навигироваться к комнате
    /// </summary>
    public event Action<Guid>? NavigateToRoom;

    /// <summary>
    /// Добавить кнопку меню
    /// </summary>
    public void AddMenuItem(string header, Action onClick)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => Dispatcher.UIThread.Post(onClick);
        menu.Items.Add(item);
    }

    /// <summary>
    /// Обновить комнаты для трея
    /// </summary>
    public void UpdateRooms(IEnumerable<RoomInfo> rooms)
    {
        // Порядок: [комнаты...] | separator | Показать | Выход
        foreach (var item in roomItems)
        {
            menu.Items.Remove(item);
        }
        roomItems.Clear();

        foreach (var room in rooms)
        {
            var item = new NativeMenuItem($"Комната - {room.Name}");
            item.Click += (_, _) => NavigateToRoom?.Invoke(room.Id);
            roomItems.Add(item);
            menu.Items.Insert(roomItems.IndexOf(item), item);
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => trayIcon?.Dispose();
}
