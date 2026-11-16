namespace MIN.ScreenDemo.Services.Contracts.Models;

/// <summary>
/// Состояние потока демонстрации
/// </summary>
public sealed class DemoStreamState
{
    /// <summary>
    /// Текущий идентификатор кадра
    /// </summary>
    public long CurrentFrameId { get; set; } = -1;

    /// <summary>
    /// Последний кадр
    /// </summary>
    public long LastCompletedFrameId { get; set; } = -1;

    /// <summary>
    /// Количество фрагментов
    /// </summary>
    public int FragmentCount { get; set; }

    /// <summary>
    /// Фрагментов получено
    /// </summary>
    public int FragmentsReceived { get; set; }

    /// <summary>
    /// Является ли кадр ключевым
    /// </summary>
    public bool IsKeyframe { get; set; }

    /// <summary>
    /// Ширина экрана
    /// </summary>
    public int StreamWidth { get; set; }

    /// <summary>
    /// Высота экрана
    /// </summary>
    public int StreamHeight { get; set; }

    /// <summary>
    /// Таблица фрагментов
    /// </summary>
    public byte[][] Fragments { get; set; } = [];

    /// <summary>
    /// Список полученных
    /// </summary>
    public bool[] Received { get; set; } = [];

    /// <summary>
    /// Буферы декодирования
    /// </summary>
    public byte[][] DecodeBuffers { get; set; } = [];

    /// <summary>
    /// Номер буфера декодирования
    /// </summary>
    public int DecodeBufferIndex { get; set; }
}
