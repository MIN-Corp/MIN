using MIN.Chat.DI.FeatureCollection;
using MIN.Core.DI.FeatureCollection;
using MIN.Discovery.DI.FeatureCollection;
using MIN.FileTransfer.DI.FeatureCollection;
using MIN.Helpers.DI.FeatureCollection;
using MIN.ScreenDemo.DI.FeatureCollection;
using MIN.Sessions.Core.DI.FeatureCollection;
using MIN.Voice.DI.FeatureCollection;

namespace MIN.DI.FeatureCollection;

/// <summary>
/// Набор функциональностей для MIN
/// </summary>
public interface IMinFeatureCollection
{
    /// <inheritdoc cref="IChatFeatureCollection"/>
    IChatFeatureCollection Chat { get; }

    /// <inheritdoc cref="ICoreFeatureCollection"/>
    ICoreFeatureCollection Core { get; }

    /// <inheritdoc cref="IDiscoveryFeatureCollection"/>
    IDiscoveryFeatureCollection Discovery { get; }

    /// <inheritdoc cref="IFileTransferFeatureCollection"/>
    IFileTransferFeatureCollection FileTransfer { get; }

    /// <inheritdoc cref="IHelperFeatureCollection"/>
    IHelperFeatureCollection Helper { get; }

    /// <inheritdoc cref="ISessionFeatureCollection"/>
    ISessionFeatureCollection Sessions { get; }

    /// <inheritdoc cref="IVoiceFeatureCollection"/>
    IVoiceFeatureCollection Voice { get; }

    /// <inheritdoc cref="IScreenDemoFeatureCollection"/>
    IScreenDemoFeatureCollection ScreenDemo { get; }
}
