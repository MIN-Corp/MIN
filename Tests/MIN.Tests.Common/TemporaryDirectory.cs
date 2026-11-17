namespace MIN.Tests.Common;

/// <summary>
/// Временная директория, удаляемая при освобождении
/// </summary>
public sealed class TemporaryDirectory : IDisposable
{
    /// <summary>
    /// Путь к временной директории
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TemporaryDirectory"/>
    /// </summary>
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "min_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Временные файлы могли остаться заблокированными — не валим тест
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
