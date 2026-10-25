using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MIN.Desktop.Infrastructure.Converters;

/// <summary>
/// Форматирует связанное значение как строку из целого числа; пустая строка = null
/// </summary>
public partial class NullableIntToStringConverter : Converter<NullableIntToStringConverter>
{
    [GeneratedRegex("[^0-9]")]
    private static partial Regex DigitReplaceRegex();

    /// <inheritdoc />
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() ?? string.Empty;

    /// <inheritdoc />
    public override object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var str = value as string ?? value?.ToString() ?? string.Empty;
        str = DigitReplaceRegex().Replace(str, "");

        if (string.IsNullOrEmpty(str) || !int.TryParse(str, out var result))
        {
            return null!;
        }

        return result;
    }
}
