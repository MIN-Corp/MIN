namespace MIN.Audio.Services.Contracts.Models;

/// <summary>
/// Модель устройства звука (ввод и вывод)
/// </summary>
public sealed record AudioDeviceInfo(int Index, string Name);
