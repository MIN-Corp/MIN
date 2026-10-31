namespace MIN.ScreenDemo.Services.Contracts.Constants;

/// <summary>
/// Константы передачи демонстрации экрана
/// </summary>
public class ScreenDemoConstants
{
    /// <summary>
    /// Частота передачи кадров (30 FPS).
    /// Экранное содержимое (текст, код) читаемо, нагрузка на CPU умеренная
    /// </summary>
    public const int Fps = 30;

    /// <summary>
    /// Интервал между кадрами в миллисекундах
    /// </summary>
    public const int FrameIntervalMs = 1000 / Fps;

    /// <summary>
    /// Максимальная высота передаваемого кадра (пропорции сохраняются).
    /// Ограничивает нагрузку на OpenH264 и битрейт
    /// </summary>
    public const int MaxFrameHeight = 720;

    /// <summary>
    /// Целевой битрейт кодека в килобитах в секунду
    /// </summary>
    public const int TargetBitrateKbps = 3_000;

    /// <summary>
    /// Интервал отправки ключевых кадров (в кадрах) — каждые 2 секунды.
    /// Позволяет зрителю восстановиться после потери фрагментов без повторного запроса
    /// </summary>
    public const int KeyframeIntervalFrames = 2 * Fps;

    /// <summary>
    /// Целевой размер фрагмента кадра в байтах до кодирования сообщения.
    /// Сообщение серилизуется в JSON: байты превращаются в base64 (x4/3),
    /// плюс обвязка и заголовки — итог должен влезть в MessageBufferSize (16 КБ).
    /// 8 КБ * 4/3 = ~11 КБ — безопасно
    /// </summary>
    public const int FragmentTargetSize = 8 * 1024;

    /// <summary>
    /// Максимальное количество фрагментов на один кадр.
    /// Ключевой кадр 720p при пике ~150 КБ / 8 КБ = ~19 фрагментов — запас достаточен
    /// </summary>
    public const int MaxFragmentsPerFrame = 64;
}
