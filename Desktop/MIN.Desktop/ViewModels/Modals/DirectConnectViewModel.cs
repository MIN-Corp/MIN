using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Transport.Contracts.Extensions;
using MIN.Core.Transport.TcpSockets.Models;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.Infrastructure.Validators;
using MIN.Desktop.ViewModels.Base;

namespace MIN.Desktop.ViewModels.Modals;

/// <summary>
/// Модель окна подключения напрямую
/// </summary>
public partial class DirectConnectViewModel : ModalViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [Required(ErrorMessage = "Введите IP адрес")]
    public partial string IpAddress { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [IntValue]
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Введите порт")]
    [Range(1, ushort.MaxValue, ErrorMessage = "Порт должен быть от 1 до 65535")]
    public partial int Port { get; set; } = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial bool IsConnecting { get; set; }

    /// <summary>
    /// Полученная конечная точка
    /// </summary>
    public TcpEndpoint Endpoint { get; } = new();

    /// <summary>
    /// Событие по нажатию на кнопку
    /// </summary>
    public Action? OnConnect { get; set; }

    /// <summary>
    /// Включить кнопку подключения обратно
    /// </summary>
    public void EnableConnectButton()
    {
        IsConnecting = false;
    }

    [RelayCommand]
    private void StartEditing()
    {
        IsEditing = true;
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private void Connect()
    {
        Endpoint.IpAddress = IpAddress;
        Endpoint.Port = Convert.ToInt32(Port);

        Endpoint.Origin = Endpoint.AssumeOriginOutOfAddress();

        IsConnecting = true;
        OnConnect?.Invoke();
    }

    [RelayCommand]
    private async Task PasteIpAddress()
    {
        await Task.Yield();
        await TryParsePortAndValidate();
    }

    [RelayCommand]
    private async Task TryParsePortAndValidate()
    {
        if (IpAddressParser.TryParseIpAddress(IpAddress, out var gottenIpAddress, out var port))
        {
            try
            {
                var validatedIp = await IpAddressParser.ValidateIp(gottenIpAddress);
                Port = port;
                IpAddress = validatedIp;
                ErrorMessage = null;
                IsEditing = false;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                IsEditing = true;
            }
        }
        else
        {
            try
            {
                IpAddress = await IpAddressParser.ValidateIp(IpAddress);
                ErrorMessage = null;
                IsEditing = false;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                IsEditing = true;
            }
        }
    }

    private bool CanConnect() => !HasErrors && !IsConnecting && !IsEditing;
}
