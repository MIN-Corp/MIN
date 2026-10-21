using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;

namespace MIN.Desktop.Infrastructure.Validators;

/// <summary>
/// Проверяет, можно ли использовать значение в качестве имени комнаты
/// </summary>
public sealed class FileNameAttribute : TypedValidationAttribute<string>
{
    private const int MaxRoomName = 70;

    readonly static internal char[] InvalidPathCharacters = Path.GetInvalidFileNameChars();

    /// <inheritdoc />
    protected override ValidationResult? IsValid(string? value, ValidationContext context)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }
        if (value.Length > MaxRoomName)
        {
            return new ValidationResult($"{context.DisplayName} не может быть больше {MaxRoomName} символов");
        }
        var indexOfAny = value.IndexOfAny(InvalidPathCharacters);
        if (indexOfAny > -1)
        {
            return new ValidationResult($"{context.DisplayName} не может содержать '{value[indexOfAny]}'. Вот запрещённые символы: {string.Join(' ', InvalidPathCharacters.Where(c => c > 31))}");
        }

        return ValidationResult.Success;
    }
}
