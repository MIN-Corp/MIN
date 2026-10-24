using System.ComponentModel.DataAnnotations;
using static MIN.Desktop.Contracts.Constants.DesktopConstants;

namespace MIN.Desktop.Infrastructure.Validators;

/// <summary>
/// Максимальная длина текста из динамического поля
/// </summary>
public sealed class MaxTextLengthAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        if (value is string s && s.Length > MaximumAvailableTextMessageSize)
        {
            return new ValidationResult($"Достигнут лимит по кол-ву символов: {MaximumAvailableTextMessageSize}");
        }

        return ValidationResult.Success;
    }
}
