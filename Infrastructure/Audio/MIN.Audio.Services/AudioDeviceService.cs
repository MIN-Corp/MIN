using System.Text;
using MIN.Audio.Services.Contracts.Interfaces;
using MIN.Audio.Services.Contracts.Models;
using OpenTK.Audio.OpenAL;

namespace MIN.Audio.Services;

/// <inheritdoc cref="IAudioDeviceService"/>
public class AudioDeviceService : IAudioDeviceService
{
    private const string PossiblePrefix = "OpenAL Soft on ";

    IReadOnlyList<AudioDeviceInfo> IAudioDeviceService.GetInputDevices(bool asDecoded)
    => GetDevices(isInput: true, asDecoded);

    IReadOnlyList<AudioDeviceInfo> IAudioDeviceService.GetOutputDevices(bool asDecoded)
        => GetDevices(isInput: false, asDecoded);

    private static List<AudioDeviceInfo> GetDevices(bool isInput, bool asDecoded)
    {
        var result = new List<AudioDeviceInfo>();

        var listType = isInput
            ? AlcGetStringList.CaptureDeviceSpecifier
            : AlcGetStringList.AllDevicesSpecifier;

        try
        {
            var devices = ALC.GetString(ALDevice.Null, listType);
            if (devices != null)
            {
                AddDevicesFromList(devices, result, asDecoded);
            }
        }
        catch { }

        return result;
    }

    private static void AddDevicesFromList(List<string> devices, List<AudioDeviceInfo> result, bool asDecoded)
    {
        if (asDecoded)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        for (var i = 0; i < devices.Count; i++)
        {
            var name = devices[i];

            if (asDecoded)
            {
                var bytes = Encoding.GetEncoding("Windows-1251").GetBytes(name);
                name = Encoding.UTF8.GetString(bytes);
            }

            if (name.StartsWith(
                PossiblePrefix, StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(PossiblePrefix.Length);
            }

            result.Add(new AudioDeviceInfo(i, name));
        }
    }
}
