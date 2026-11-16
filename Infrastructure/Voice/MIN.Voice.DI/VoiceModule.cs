using Microsoft.Extensions.DependencyInjection;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Common.Mvc;
using MIN.Common.Mvc.Extensions;
using MIN.Core.Handlers.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Voice.DI.FeatureCollection;
using MIN.Voice.Handlers;
using MIN.Voice.Messaging;
using MIN.Voice.Services;

namespace MIN.Voice.DI;

/// <summary>
/// Модуль регистрации зависимостей для Voice
/// </summary>
public class VoiceModule : Module
{
    /// <inheritdoc />
    protected override void Load(IServiceCollection services)
    {
        services.RegisterMultipleInterfacesAssignableFromAnchor<IMessage, IVoiceMessagingAnchor>(ServiceLifetime.Singleton);
        services.RegisterMultipleInterfacesAssignableFromAnchor<IMessageHandler, IVoiceHandlerAnchor>(ServiceLifetime.Singleton);
        services.RegisterMultipleInterfacesAssignableTo<IHostedService, VoiceCallMonitor>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<PcmCodec>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<VoicePlaybackService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<AudioCaptureService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<VoiceAudioDetector>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<VoiceCallStateService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<VoiceDataTransmitter>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<MuteService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<VoiceFeatureCollection>(ServiceLifetime.Singleton);
    }
}
