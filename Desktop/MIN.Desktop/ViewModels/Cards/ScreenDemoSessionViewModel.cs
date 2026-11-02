using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MIN.Desktop.ViewModels.Cards;

/// <summary>
/// Карточка активной демонстрации экрана в звонке
/// </summary>
public partial class ScreenDemoSessionViewModel : ObservableObject
{
    /// <summary>
    /// Идентификатор подкомнаты демонстрации
    /// </summary>
    public int SubRoomId { get; }

    /// <summary>
    /// Идентификатор участника, который демонстрирует экран
    /// </summary>
    public Guid SharerId { get; }

    /// <summary>
    /// Кто демонстрирует экран
    /// </summary>
    [ObservableProperty]
    public partial string SharerName { get; set; }

    /// <summary>
    /// Демонстрирую ли я сам
    /// </summary>
    [ObservableProperty]
    public partial bool IsMine { get; set; }

    /// <summary>
    /// Смотрит ли локальный пользователь эту демонстрацию
    /// </summary>
    [ObservableProperty]
    public partial bool IsWatching { get; set; }

    /// <summary>
    /// Количество зрителей
    /// </summary>
    [ObservableProperty]
    public partial int ViewerCount { get; set; }

    /// <summary>
    /// Событие по нажатию на присоединение
    /// </summary>
    public Func<Task>? Joined { get; set; }

    /// <summary>
    /// Событие по выходу из демонстрации
    /// </summary>
    public Func<Task>? Left { get; set; }

    /// <summary>
    /// Событие по нажатию на остановку демонстрации
    /// </summary>
    public Func<Task>? Stopped { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoSessionViewModel"/>
    /// </summary>
    public ScreenDemoSessionViewModel(int subRoomId, Guid sharerId, string sharerName, bool isMine, int viewerCount)
    {
        SubRoomId = subRoomId;
        SharerId = sharerId;
        SharerName = sharerName;
        IsMine = isMine;
        ViewerCount = viewerCount;
    }

    [RelayCommand]
    private async Task OnJoinedClicked()
        => await Joined?.Invoke()!;

    [RelayCommand]
    private async Task OnLeftClicked()
        => await Left?.Invoke()!;

    [RelayCommand]
    private async Task OnStoppedClicked()
        => await Stopped?.Invoke()!;
}
