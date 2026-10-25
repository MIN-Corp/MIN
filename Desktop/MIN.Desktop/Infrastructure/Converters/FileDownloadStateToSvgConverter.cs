using System;
using System.Globalization;
using Avalonia.Media;
using MIN.Desktop.Contracts.Models.Enums;

namespace MIN.Desktop.Infrastructure.Converters;

/// <summary>
/// Конвертор для выделения карточки
/// </summary>
public class FileDownloadStateToSvgConverter : Converter<FileDownloadStateToSvgConverter>
{
    ///<inheritdoc />
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FileDownloadState state)
        {
            return Brushes.Transparent;
        }

        var path = "/Assets/Icons/";

        var icon = state switch
        {
            FileDownloadState.IsDownloading => "close.svg",
            FileDownloadState.Downloaded => "file.svg",
            FileDownloadState.Assembling => "file.svg",
            FileDownloadState.NotDownloaded => "download.svg",
            _ => string.Empty
        };

        return path + icon;
    }
}
