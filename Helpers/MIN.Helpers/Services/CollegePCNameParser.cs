using System.Text.RegularExpressions;

namespace MIN.Helpers.Services;

/// <summary>
/// Парсер имени компьютера от коллелджа
/// </summary>
public static class CollegePCNameParser
{
    /// <summary>
    /// Распарсить имя компьютера на класс и номер компьютера
    /// </summary>
    public static bool TryParseComputerName(string pcName, out int roomNumber, out int computerNumber)
    {
        roomNumber = 0;
        computerNumber = 0;

        var match = Regex.Match(pcName, @"^[A-Z](\d{3})(\d{1,2})$");

        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups[1].Value, out roomNumber))
        {
            return false;
        }

        if (!int.TryParse(match.Groups[2].Value, out computerNumber))
        {
            return false;
        }

        return true;
    }
}
