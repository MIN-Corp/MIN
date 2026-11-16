using MIN.ScreenDemo.Services.Contracts.Enums;

namespace MIN.ScreenDemo.Services.Contracts.Models;

/// <summary>
/// Информация об источнике захвата экрана (монитор или окно)
/// </summary>
/// <param name="Id">Непрозрачный идентификатор источника (платформно-зависимый, на Windows — HMONITOR или HWND в строковом виде)</param>
/// <param name="Kind">Тип источника</param>
/// <param name="Title">Отображаемое название (заголовок окна или имя монитора)</param>
/// <param name="IsPrimary">Является ли монитор основным (для окон всегда false)</param>
public sealed record ScreenSourceInfo(string Id, ScreenDemoSourceKind Kind, string Title, bool IsPrimary);
