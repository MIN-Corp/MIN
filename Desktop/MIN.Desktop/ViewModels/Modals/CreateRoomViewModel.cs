using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Transport.Contracts.Helpers;
using MIN.Core.Transport.Contracts.Models;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.Infrastructure.Validators;
using MIN.Desktop.ViewModels.Base;
using MIN.Helpers.Services;

namespace MIN.Desktop.ViewModels.Modals;

/// <summary>
/// Модель окна создания комнаты
/// </summary>
public partial class CreateRoomViewModel : ModalViewModelBase
{
    private int roomCurrentParticipantCount;

    [ObservableProperty]
    [Display(Name = "Имя комнаты")]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Придумайте имя для комнаты")]
    [RoomName]
    [NotEndsWith(".")]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyDataErrorInfo]
    [RoomCapacity]
    public partial int RoomMaxPlayers { get; set; } = 8;

    [IntValue]
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Введите порт")]
    [Range(1024, ushort.MaxValue, ErrorMessage = "Порт должен быть от 1024 до 65535")]
    public partial int Port { get; set; } = 5550;

    /// <summary>
    /// Локальное обнаружение
    /// </summary>
    [ObservableProperty]
    public partial bool EnableLocalDiscovery { get; set; } = true;

    /// <summary>
    /// Проброска порта
    /// </summary>
    [ObservableProperty]
    public partial bool EnablePortForwarding { get; set; }

    /// <summary>
    /// Radmin
    /// </summary>
    [ObservableProperty]
    public partial bool EnableRadmin { get; set; }

    /// <summary>
    /// Публикация в web
    /// </summary>
    [ObservableProperty]
    public partial bool EnableWeb { get; set; }

    /// <summary>
    /// Создание или редактирование комнаты
    /// </summary>
    [ObservableProperty]
    public partial bool IsNew { get; set; } = true;

    /// <summary>
    /// Обнаружен Radmin
    /// </summary>
    [ObservableProperty]
    public partial bool RadminInstalled { get; set; }

    /// <summary>
    /// Обнаружен Hamachi
    /// </summary>
    [ObservableProperty]
    public partial bool HamachiInstalled { get; set; }

    /// <summary>
    /// Название окна
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "MIN - Создание комнаты";

    /// <summary>
    /// Настраиваемая комната
    /// </summary>
    public RoomInfo Room { get; set; } = new RoomInfo();

    /// <summary>
    /// Настройки глобальности сети
    /// </summary>
    public NetworkOptions NetworkOptions { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="CreateRoomViewModel"/>
    /// </summary>
    public CreateRoomViewModel(IRoomConnectionRegistry registry)
    {
        var installedVpns = NetworkHelper.GetVpnIps();

        if (installedVpns.Any(x => x.NetworkName.Contains("Radmin")))
        {
            RadminInstalled = true;
        }

        if (installedVpns.Any(x => x.NetworkName.Contains("Hamachi")))
        {
            HamachiInstalled = true;
        }

        Port += registry.GetServerConnectionCount();
    }

    /// <summary>
    /// Инициализироовать с уже созданной комнатой
    /// </summary>
    public void InitializeWithRoom(RoomInfo room, NetworkOptions networkOptions)
    {
        Title = $"Редактирование комнаты {room.Name}";

        IsNew = false;
        Room = room;
        Name = room.Name;
        roomCurrentParticipantCount = room.ParticipantCount;
        RoomMaxPlayers = room.MaximumParticipants;
        NetworkOptions = networkOptions;

        EnableLocalDiscovery = NetworkOptions.EnableLocalDiscovery;
        EnablePortForwarding = NetworkOptions.EnablePortForwarding;
        EnableRadmin = NetworkOptions.EnableRadmin;
        EnableWeb = NetworkOptions.EnableWeb;
        Port = NetworkOptions.PrefferredPort;
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        if (!IsNew && roomCurrentParticipantCount > RoomMaxPlayers)
        {
            InAppNotifier.Error("Вы не можете поставить максимальное количество участников меньше, чем их текущее кол-во");
            return;
        }

        Room.Name = Name;

        if (CollegePCNameParser.TryParseComputerName(Environment.MachineName, out var roomNumber, out var computerNumber))
        {
            Room.Cabinet = roomNumber.ToString();
            Room.PcNumber = computerNumber;
        }

        Room.MaximumParticipants = RoomMaxPlayers;

        NetworkOptions = new()
        {
            PrefferredPort = (short)Port,
            EnableLocalDiscovery = EnableLocalDiscovery,
            EnablePortForwarding = EnablePortForwarding,
            EnableRadmin = EnableRadmin,
            EnableWeb = EnableWeb,
        };

        Close(ButtonOptions.Ok);
    }

    private bool CanCreate() => !HasErrors;
}
