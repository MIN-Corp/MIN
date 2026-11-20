using System.Runtime.CompilerServices;
using MIN.Tests.Common;

namespace MIN.Core.Serialization.Tests;

/// <summary>
/// Регистрирует тяжёлые сборки-якоря (Voice, ScreenDemo) для полного round-trip покрытия
/// </summary>
internal static class HeavyAnchorRegistration
{
    /// <summary>
    /// Выполняется при загрузке тестовой сборки — до обнаружения тестов
    /// </summary>
    [ModuleInitializer]
    internal static void Register()
    {
        TestSerializer.RegisterAnchorAssembly(typeof(MIN.Voice.Messaging.IVoiceMessagingAnchor).Assembly);
        TestSerializer.RegisterAnchorAssembly(typeof(MIN.ScreenDemo.Messaging.IScreenDemoMessagingAnchor).Assembly);
    }
}
