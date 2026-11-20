using FluentAssertions;
using MIN.FileTransfer.Services;
using MIN.FileTransfer.Services.Contracts.Interfaces;
using MIN.Tests.Common;
using Xunit;

namespace MIN.FileTransfer.Tests;

/// <summary>
/// Тесты на <see cref="FileHelperService"/>
/// </summary>
public class FileHelperServiceTests
{
    private readonly IFileHelperService service = new FileHelperService();

    /// <summary>
    /// Расширения изображений распознаются
    /// </summary>
    [Theory]
    [InlineData("photo.png", true)]
    [InlineData("photo.JPG", true)]
    [InlineData("photo.jpeg", true)]
    [InlineData("anim.gif", true)]
    [InlineData("pic.webp", true)]
    [InlineData("doc.txt", false)]
    [InlineData("app.exe", false)]
    [InlineData("noextension", false)]
    public void IsFileImageShouldDetectImages(string fileName, bool expected)
    {
        // Act
        var result = service.IsFileImage(fileName);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// MIME-типы определяются по расширению, неизвестные — octet-stream
    /// </summary>
    [Theory]
    [InlineData("file.txt", "text/plain")]
    [InlineData("file.pdf", "application/pdf")]
    [InlineData("file.PNG", "image/png")]
    [InlineData("file.unknown", "application/octet-stream")]
    public void GetMimeTypeShouldMapExtensions(string fileName, string expected)
    {
        // Act
        var result = ((FileHelperService)service).GetMimeType(fileName);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Тип файла — расширение без точки
    /// </summary>
    [Fact]
    public void GetFileTypeShouldReturnExtension()
    {
        // Act & Assert
        service.GetFileType("archive.zip").Should().Be("zip");
        service.GetFileType("noextension").Should().Be("application/octet-stream");
    }

    /// <summary>
    /// Размер файла ограничен 10 МиБ и должен быть положительным
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(10 * 1024 * 1024, true)]
    [InlineData(10 * 1024 * 1024 + 1, false)]
    public void IsFileSizeAllowedShouldEnforceLimit(long size, bool expected)
    {
        // Act
        var result = service.IsFileSizeAllowed(size);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Опасные расширения заблокированы
    /// </summary>
    [Theory]
    [InlineData("file.txt", true)]
    [InlineData("file.png", true)]
    [InlineData("script.bat", false)]
    [InlineData("script.ps1", false)]
    [InlineData("script.sh", false)]
    [InlineData("script.cmd", false)]
    [InlineData("noextension", false)]
    public void IsExtensionAllowedShouldBlockDangerous(string fileName, bool expected)
    {
        // Act
        var result = service.IsExtensionAllowed(fileName);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Имя файла очищается от недопустимых символов
    /// </summary>
    [Fact]
    public void SanitizeFileNameShouldStripInvalidChars()
    {
        // Act
        var result = service.SanitizeFileName("a<b>c:d\"/|?*e.txt");

        // Assert
        result.Should().Be("abcde.txt");
    }

    /// <summary>
    /// Пустое имя заменяется заглушкой
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<>:")]
    public void SanitizeFileNameEmptyShouldUsePlaceholder(string fileName)
    {
        // Act
        var result = service.SanitizeFileName(fileName);

        // Assert
        result.Should().Be("безымянный_файл");
    }

    /// <summary>
    /// Размер форматируется с русскими единицами и запятой
    /// </summary>
    [Theory]
    [InlineData(500, "500 байт")]
    [InlineData(1536, "1,50 КБ")]
    [InlineData(1048576, "1,00 МБ")]
    [InlineData(1073741824, "1,00 ГБ")]
    public void FormatFileSizeShouldFormat(long bytes, string expected)
    {
        // Act
        var result = service.FormatFileSize(bytes);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Отрицательный размер отклоняется
    /// </summary>
    [Fact]
    public void FormatFileSizeNegativeShouldThrow()
    {
        // Act
        var act = () => service.FormatFileSize(-1);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Размер файла читается с диска
    /// </summary>
    [Fact]
    public void GetFileSizeShouldReadFromFile()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var filePath = Path.Combine(directory.Path, "file.bin");
        File.WriteAllBytes(filePath, new byte[1234]);

        // Act
        var size = service.GetFileSize(filePath);

        // Assert
        size.Should().Be(1234);
    }
}
