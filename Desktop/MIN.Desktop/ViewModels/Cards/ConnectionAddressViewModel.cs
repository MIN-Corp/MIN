using System;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Transport.Contracts.Extensions;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.TcpSockets.Models;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Pages;

namespace MIN.Desktop.ViewModels.Cards;

/// <summary>
/// View модель строчки адреса соединения
/// </summary>
public partial class ConnectionAddressViewModel : CardViewModelBase
{
    private readonly IClipboard? clipboard;
    private readonly ChatSideBarViewModel? owner;

    /// <summary>
    /// Исходная точка подключения. Null только у новой (ещё не сохранённой) строки
    /// </summary>
    public IEndpoint? Endpoint { get; }

    /// <summary>
    /// Строка создана через "Добавить адрес" и ещё не сохранена
    /// </summary>
    public bool IsNew { get; }

    /// <summary>
    /// Место получения адреса
    /// </summary>
    public string Origin => Endpoint?.GetOrigin() ?? string.Empty;

    /// <summary>
    /// Адрес
    /// </summary>
    public string Address => Endpoint?.GetAddress() ?? string.Empty;

    /// <summary>
    /// Выбран как адрес для кнопки подключения
    /// </summary>
    [ObservableProperty]
    public partial bool IsChosen { get; set; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string EditableAddress { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ConnectionAddressViewModel"/>
    /// </summary>
    public ConnectionAddressViewModel(IEndpoint? address, IClipboard? clipboard, ChatSideBarViewModel? owner, bool isNew = false)
    {
        Endpoint = address;
        this.clipboard = clipboard;
        this.owner = owner;
        IsNew = isNew;
    }

    /// <summary>
    /// Включить режим редактирования
    /// </summary>
    [RelayCommand]
    private void StartEdit()
    {
        EditableAddress = Endpoint?.GetAddress() ?? string.Empty;
        ErrorMessage = null;
        IsEditing = true;
    }

    /// <summary>
    /// Отменить редактирование
    /// </summary>
    [RelayCommand]
    private void CancelEdit()
    {
        if (IsNew)
        {
            owner?.CancelNewAddress(this);
            return;
        }

        ErrorMessage = null;
        IsEditing = false;
    }

    /// <summary>
    /// Проверить и сохранить адрес
    /// </summary>
    [RelayCommand]
    private async Task ApplyEdit()
    {
        var rawAddress = EditableAddress.Trim();

        if (!IpAddressParser.TryParseIpAddress(rawAddress, out var host, out var port))
        {
            ErrorMessage = "Адрес задаётся в формате ip:порт или домен:порт";
            return;
        }

        try
        {
            await IpAddressParser.ValidateIp(host);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        var endpoint = new TcpEndpoint()
        {
            IpAddress = host,
            Port = port,
        };
        endpoint.Origin = endpoint.AssumeOriginOutOfAddress();

        owner?.SaveAddress(this, endpoint);
    }

    /// <summary>
    /// Удалить этот адрес из списка комнаты
    /// </summary>
    [RelayCommand]
    private void RemoveSelf()
        => owner?.RemoveAddress(this);

    /// <summary>
    /// Выбрать этот адрес для кнопки подключения
    /// </summary>
    [RelayCommand]
    private void SetChosen()
        => owner?.ChooseConnectAddress(this);

    [RelayCommand]
    private async Task CopyToClipBoard(string message)
    {
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(message);
            InAppNotifier.Info("Скопировано в буфер обмена");
        }
    }
}
