using System.Security.Cryptography;
using FluentAssertions;
using MIN.Chat.Messaging;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.RoomRelated.ParticipantRelated;
using MIN.Core.Messaging.Stateless.FastChannelConnect;
using MIN.Core.Messaging.Stateless.Handshake;
using MIN.Core.Messaging.Stateless.RoomRelated.Disconnect;
using MIN.Core.Messaging.Stateless.RoomRelated.Join;
using MIN.Core.Messaging.Stateless.RoomRelated.Leaving;
using MIN.Core.Messaging.Stateless.RoomRelated.RoomInfo;
using MIN.Core.Messaging.Stateless.RoomRelated.Sync;
using MIN.Core.Stores.Contracts.Models;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.TcpSockets.Models;
using MIN.Core.Transport.UdpSockets.Models;
using MIN.Tests.Common;
using Moq;
using Xunit;

namespace MIN.Core.Handlers.Tests;

/// <summary>
/// Тесты обработчиков: handshake, join, room info/sync, disconnect, fast channel, leaving
/// </summary>
public class AdvancedHandlerTests : IDisposable
{
    private readonly HandlerTestFactory factory = new();
    private readonly RoomContext roomContext;
    private readonly Guid selfId = Guid.NewGuid();
    private readonly Guid connectionId = Guid.NewGuid();
    private readonly Guid roomId;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="AdvancedHandlerTests"/>
    /// </summary>
    public AdvancedHandlerTests()
    {
        roomId = Guid.NewGuid();
        roomContext = factory.RoomFactory.GetOrCreateContext(roomId);
    }

    private MessageContext CreateContext(Role role = Role.Client)
        => TestMessageContext.Create(roomContext, selfId, connectionId, role);

    private Room RegisterRoom(string name = "Комната")
    {
        var room = new Room
        {
            Id = roomId,
            Name = name,
            HostParticipant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Хост" },
            MaximumParticipants = 10,
        };
        factory.RoomStore.Register(room);
        return room;
    }

    private void SetupEncryptor(byte[]? storedFingerprint = null)
    {
        var localKey = new byte[] { 1, 2, 3, 4 };
        factory.Encryptor.Setup(e => e.GetLocalPublicKey()).ReturnsAsync(localKey);
        factory.Encryptor.Setup(e => e.ComputeKeyFingerprint(It.IsAny<byte[]>())).Returns<byte[]>(k => SHA256.HashData(k));
        factory.Encryptor.Setup(e => e.InitializeSessionWithPartnerAsync(It.IsAny<Guid>(), It.IsAny<byte[]>())).Returns(Task.CompletedTask);
        factory.Encryptor.Setup(e => e.TryGetPartnerKeyFingerprintAsync(It.IsAny<Guid>())).ReturnsAsync(storedFingerprint);
        factory.Encryptor.Setup(e => e.TryInitializeSessionFromStoredAsync(It.IsAny<Guid>())).ReturnsAsync(true);
    }

    /// <summary>
    /// Handshake с полным ключом регистрирует соединение и отвечает Ack
    /// </summary>
    [Fact]
    public async Task HandshakeWithFullKeyShouldRegisterAndAck()
    {
        // Arrange
        SetupEncryptor();
        var handler = factory.GetHandler(MessageTypeTag.Handshake);
        var participant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Гость" };
        var message = new HandshakeMessage { Participant = participant, Version = new Version(2, 2, 0, 0), PublicKey = [9, 9, 9] };

        // Act
        var result = await handler.HandleAsync(message, CreateContext(Role.Host));

        // Assert
        result.IsSuccess.Should().BeTrue();
        roomContext.Connections.ConnectionExists(connectionId).Should().BeTrue();
        var ack = result.Response.Should().BeOfType<HandshakeAckMessage>().Subject;
        ack.PublicKey.Should().NotBeNull();
        factory.Encryptor.Verify(e => e.InitializeSessionWithPartnerAsync(participant.Id, It.IsAny<byte[]>()), Times.Once);
    }

    /// <summary>
    /// Повторное соединение с тем же id отклоняется
    /// </summary>
    [Fact]
    public async Task HandshakeDuplicateConnectionShouldError()
    {
        // Arrange
        SetupEncryptor();
        roomContext.Connections.Register(connectionId, new ParticipantInfo { Id = Guid.NewGuid() });
        var handler = factory.GetHandler(MessageTypeTag.Handshake);
        var message = new HandshakeMessage { Participant = new ParticipantInfo { Id = Guid.NewGuid() }, Version = new Version(2, 2, 0, 0), PublicKey = [1] };

        // Act
        var result = await handler.HandleAsync(message, CreateContext(Role.Host));

        // Assert
        result.ErrorMessage.Should().NotBeNull();
        result.CriticalErrorForConnection.Should().BeTrue();
    }

    /// <summary>
    /// Несовместимая версия отклоняется
    /// </summary>
    [Fact]
    public async Task HandshakeIncompatibleVersionShouldError()
    {
        // Arrange
        SetupEncryptor();
        factory.Version.Setup(v => v.IsVersionCompatible(It.IsAny<Version>())).Returns(false);
        var handler = factory.GetHandler(MessageTypeTag.Handshake);
        var message = new HandshakeMessage { Participant = new ParticipantInfo { Id = Guid.NewGuid() }, Version = new Version(1, 0), PublicKey = [1] };

        // Act
        var result = await handler.HandleAsync(message, CreateContext(Role.Host));

        // Assert
        result.ErrorMessage.Should().Contain("устаревшей версии");
        result.CriticalErrorForConnection.Should().BeTrue();
    }

    /// <summary>
    /// Совпавший отпечаток использует сохранённый ключ
    /// </summary>
    [Fact]
    public async Task HandshakeMatchingFingerprintShouldAckWithFingerprint()
    {
        // Arrange
        var partnerKey = new byte[] { 5, 6, 7 };
        var fingerprint = SHA256.HashData(partnerKey);
        SetupEncryptor(storedFingerprint: fingerprint);
        var handler = factory.GetHandler(MessageTypeTag.Handshake);
        var message = new HandshakeMessage
        {
            Participant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Вернувшийся" },
            Version = new Version(2, 2, 0, 0),
            PublicKeyFingerprint = fingerprint,
        };

        // Act
        var result = await handler.HandleAsync(message, CreateContext(Role.Host));

        // Assert
        var ack = result.Response.Should().BeOfType<HandshakeAckMessage>().Subject;
        ack.PublicKey.Should().BeNull();
        ack.PublicKeyFingerprint.Should().NotBeNull();
        factory.Encryptor.Verify(e => e.TryInitializeSessionFromStoredAsync(It.IsAny<Guid>()), Times.Once);
    }

    /// <summary>
    /// Неизвестный ключ запрашивается полностью
    /// </summary>
    [Fact]
    public async Task HandshakeUnknownKeyShouldRequestPublicKey()
    {
        // Arrange
        SetupEncryptor(storedFingerprint: null);
        var handler = factory.GetHandler(MessageTypeTag.Handshake);
        var message = new HandshakeMessage
        {
            Participant = new ParticipantInfo { Id = Guid.NewGuid() },
            Version = new Version(2, 2, 0, 0),
            PublicKeyFingerprint = [1, 2, 3],
        };

        // Act
        var result = await handler.HandleAsync(message, CreateContext(Role.Host));

        // Assert
        result.Response.Should().BeOfType<PublicKeyRequestMessage>();
    }

    /// <summary>
    /// Запрос ключа возвращает локальный публичный ключ
    /// </summary>
    [Fact]
    public async Task PublicKeyRequestShouldReturnKey()
    {
        // Arrange
        SetupEncryptor();
        var handler = factory.GetHandler(MessageTypeTag.PublicKeyRequest);

        // Act
        var result = await handler.HandleAsync(new PublicKeyRequestMessage(), CreateContext());

        // Assert
        var response = result.Response.Should().BeOfType<PublicKeyResponseMessage>().Subject;
        response.PublicKey.Should().Equal([1, 2, 3, 4]);
    }

    /// <summary>
    /// HandshakeAck регистрирует хост и предлагает быстрый канал
    /// </summary>
    [Fact]
    public async Task HandshakeAckShouldProposeFastChannel()
    {
        // Arrange
        SetupEncryptor();
        var room = RegisterRoom();
        room.ConnectionAddresses = [new TcpEndpoint { IpAddress = "127.0.0.1", Port = 5000, Origin = AddressOrigin.Lan }];
        var handler = factory.GetHandler(MessageTypeTag.HandshakeAck);
        var message = new HandshakeAckMessage
        {
            Participant = new ParticipantInfo { Id = Guid.NewGuid(), Name = "Хост" },
            PublicKey = [1, 2, 3],
        };

        // Act
        var result = await handler.HandleAsync(message, CreateContext(Role.Client));

        // Assert
        result.Response.Should().BeOfType<FastChannelConnectRequestMessage>();
        roomContext.Connections.ConnectionExists(connectionId).Should().BeTrue();
    }

    /// <summary>
    /// HandshakeAck без комнаты проваливается
    /// </summary>
    [Fact]
    public async Task HandshakeAckWithoutRoomShouldFail()
    {
        // Arrange
        SetupEncryptor();
        var handler = factory.GetHandler(MessageTypeTag.HandshakeAck);
        var message = new HandshakeAckMessage { Participant = new ParticipantInfo { Id = Guid.NewGuid() }, PublicKey = [1] };

        // Act
        var result = await handler.HandleAsync(message, CreateContext(Role.Client));

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    /// <summary>
    /// Запрос входа возвращает ответ
    /// </summary>
    [Fact]
    public async Task JoinRequestShouldReturnResponse()
    {
        // Arrange
        RegisterRoom();
        var handler = factory.GetHandler(MessageTypeTag.RoomJoinRequest);

        // Act
        var result = await handler.HandleAsync(new RoomJoinRequestMessage { SenderId = Guid.NewGuid() }, CreateContext(Role.Host));

        // Assert
        result.Response.Should().BeOfType<RoomJoinResponseMessage>();
    }

    /// <summary>
    /// Запрос входа в полную комнату отклоняется
    /// </summary>
    [Fact]
    public async Task JoinRequestFullRoomShouldReject()
    {
        // Arrange
        var room = RegisterRoom();
        room.MaximumParticipants = 1;
        room.CurrentParticipants = [new Participant { Id = Guid.NewGuid() }];
        var handler = factory.GetHandler(MessageTypeTag.RoomJoinRequest);

        // Act
        var result = await handler.HandleAsync(new RoomJoinRequestMessage { SenderId = Guid.NewGuid() }, CreateContext(Role.Host));

        // Assert
        result.ErrorMessage.Should().Contain("заполнена");
    }

    /// <summary>
    /// Запрос входа от ожидающего кика отклоняется
    /// </summary>
    [Fact]
    public async Task JoinRequestPendingKickShouldReject()
    {
        // Arrange
        var room = RegisterRoom();
        var senderId = Guid.NewGuid();
        room.LocalRoomSettings.PendingKickParticipantIds[senderId] = "кикнут";
        var handler = factory.GetHandler(MessageTypeTag.RoomJoinRequest);

        // Act
        var result = await handler.HandleAsync(new RoomJoinRequestMessage { SenderId = senderId }, CreateContext(Role.Host));

        // Assert
        result.ErrorMessage.Should().Be("кикнут");
        result.CriticalErrorForConnection.Should().BeTrue();
    }

    /// <summary>
    /// Ответ на вход порождает ParticipantJoined
    /// </summary>
    [Fact]
    public async Task JoinResponseShouldReturnParticipantJoined()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.RoomJoinResponse);

        // Act
        var result = await handler.HandleAsync(new RoomJoinResponseMessage(), CreateContext());

        // Assert
        var joined = result.Response.Should().BeOfType<ParticipantJoinedMessage>().Subject;
        joined.Participant.Id.Should().Be(factory.Identity.SelfParticipant.Id);
    }

    /// <summary>
    /// ParticipantJoined добавляет участника и на хосте отвечает Accepted
    /// </summary>
    [Fact]
    public async Task ParticipantJoinedShouldAddAndAcceptOnHost()
    {
        // Arrange
        using var capture = new EventCapture<ParticipantJoinedEvent>(factory.EventBus);
        var handler = factory.GetHandler(MessageTypeTag.ParticipantJoined);
        var participant = new Participant { Id = Guid.NewGuid(), Name = "Новый" };
        var message = new ParticipantJoinedMessage { Participant = participant, SenderId = participant.Id };

        // Act
        var result = await handler.HandleAsync(message, CreateContext(Role.Host));

        // Assert
        roomContext.Participants.TryGetParticipantById(participant.Id, out _).Should().BeTrue();
        result.Response.Should().BeOfType<ParticipantAcceptedMessage>();
        await capture.WaitAsync();
        capture.Events.Single().IsRejoin.Should().BeFalse();
    }

    /// <summary>
    /// Повторный вход вернувшегося участника переводит его в онлайн
    /// </summary>
    [Fact]
    public async Task ParticipantJoinedRejoinShouldMarkOnline()
    {
        // Arrange
        using var capture = new EventCapture<ParticipantJoinedEvent>(factory.EventBus);
        var participant = new Participant { Id = Guid.NewGuid(), Name = "Вернувшийся", CurrentStatus = OnlineStatus.Offline };
        roomContext.Participants.AddParticipant(participant);
        var handler = factory.GetHandler(MessageTypeTag.ParticipantJoined);
        var message = new ParticipantJoinedMessage { Participant = new Participant { Id = participant.Id, Name = "Вернувшийся" }, SenderId = participant.Id };

        // Act
        await handler.HandleAsync(message, CreateContext(Role.Host));

        // Assert
        participant.CurrentStatus.Should().Be(OnlineStatus.Online);
        await capture.WaitAsync();
        capture.Events.Single().IsRejoin.Should().BeTrue();
    }

    /// <summary>
    /// ParticipantAccepted запрашивает информацию о комнате
    /// </summary>
    [Fact]
    public async Task ParticipantAcceptedShouldRequestRoomInfo()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.ParticipantAccepted);

        // Act
        var result = await handler.HandleAsync(new ParticipantAcceptedMessage(), CreateContext());

        // Assert
        result.Response.Should().BeOfType<RoomInfoRequestMessage>();
    }

    /// <summary>
    /// Запрос информации о комнате возвращает снапшот для участника
    /// </summary>
    [Fact]
    public async Task RoomInfoRequestShouldReturnRoomSnapshot()
    {
        // Arrange
        RegisterRoom();
        var handler = factory.GetHandler(MessageTypeTag.RoomInfoRequest);
        var requesterId = Guid.NewGuid();

        // Act
        var result = await handler.HandleAsync(new RoomInfoRequestMessage { SenderId = requesterId }, CreateContext(Role.Host));

        // Assert
        var response = result.Response.Should().BeOfType<RoomInfoResponseMessage>().Subject;
        response.Room.Id.Should().Be(roomId);
    }

    /// <summary>
    /// Ответ с информацией о комнате регистрирует её и публикует вход
    /// </summary>
    [Fact]
    public async Task RoomInfoResponseFreshShouldRegisterRoomAndJoin()
    {
        // Arrange
        using var capture = new EventCapture<RoomStateChangedEvent>(factory.EventBus);
        var handler = factory.GetHandler(MessageTypeTag.RoomInfoResponse);
        var room = new Room
        {
            Id = roomId,
            Name = "Комната",
            HostParticipant = new ParticipantInfo { Id = Guid.NewGuid() },
            ChatHistory = [new ChatTextMessage { Content = "история" }],
        };

        // Act
        var result = await handler.HandleAsync(new RoomInfoResponseMessage { Room = room }, CreateContext(Role.Client));

        // Assert
        factory.RoomStore.RoomExists(roomId).Should().BeTrue();
        roomContext.Messages.GetMessageCount().Should().Be(1);
        result.ResultEvent.Should().BeOfType<RoomJoinedEvent>();
        await capture.WaitAsync();
    }

    /// <summary>
    /// Ответ с информацией при локальной истории запрашивает синхронизацию
    /// </summary>
    [Fact]
    public async Task RoomInfoResponseWithLocalHistoryShouldRequestSync()
    {
        // Arrange
        RegisterRoom();
        roomContext.Participants.AddParticipant(new Participant { Id = selfId });
        roomContext.Messages.AddMessage(new ChatTextMessage { Content = "локальное" });
        var handler = factory.GetHandler(MessageTypeTag.RoomInfoResponse);
        var room = new Room
        {
            Id = roomId,
            Name = "Комната",
            HostParticipant = new ParticipantInfo { Id = Guid.NewGuid() },
            CurrentParticipants = [new Participant { Id = selfId }],
        };

        // Act
        var result = await handler.HandleAsync(new RoomInfoResponseMessage { Room = room }, CreateContext(Role.Client));

        // Assert
        result.Response.Should().BeOfType<RoomSyncRequestMessage>();
    }

    /// <summary>
    /// Обновление информации о комнате меняет имя и лимит
    /// </summary>
    [Fact]
    public async Task RoomInfoUpdatedShouldUpdateRoom()
    {
        // Arrange
        var room = RegisterRoom();
        var handler = factory.GetHandler(MessageTypeTag.RoomInfoUpdated);
        var message = new RoomInfoUpdatedMessage
        {
            Room = new RoomInfo { Id = roomId, Name = "Новое имя", MaximumParticipants = 20 },
            Endpoints = [],
        };

        // Act
        var result = await handler.HandleAsync(message, CreateContext());

        // Assert
        room.Name.Should().Be("Новое имя");
        room.MaximumParticipants.Should().Be(20);
        result.ResultEvent.Should().BeOfType<RoomInfoUpdatedMessageEvent>();
    }

    /// <summary>
    /// Запрос синхронизации возвращает упущенные сообщения
    /// </summary>
    [Fact]
    public async Task SyncRequestShouldReturnMissedMessages()
    {
        // Arrange
        var anchorTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        roomContext.Messages.AddMessage(new ChatTextMessage { Content = "старое", Timestamp = anchorTime.AddMinutes(-1) });
        roomContext.Messages.AddMessage(new ChatTextMessage { Content = "новое", Timestamp = anchorTime.AddMinutes(1) });
        var handler = factory.GetHandler(MessageTypeTag.RoomSyncRequest);
        var request = new RoomSyncRequestMessage
        {
            SenderId = Guid.NewGuid(),
            LocalParticipant = new Participant { Id = Guid.NewGuid() },
            MessagesAfterTimestamp = anchorTime,
        };

        // Act
        var result = await handler.HandleAsync(request, CreateContext(Role.Host));

        // Assert
        var response = result.Response.Should().BeOfType<RoomSyncResponseMessage>().Subject;
        response.MissedMessages.Should().HaveCount(1);
        ((ChatTextMessage)response.MissedMessages[0]).Content.Should().Be("новое");
    }

    /// <summary>
    /// Ответ синхронизации добавляет упущенные и удаляет отсутствующие
    /// </summary>
    [Fact]
    public async Task SyncResponseShouldAddMissedAndRemoveDeleted()
    {
        // Arrange
        RegisterRoom();
        var kept = new ChatTextMessage { Content = "оставить" };
        var deleted = new ChatTextMessage { Content = "удалить" };
        roomContext.Messages.AddMessage(kept);
        roomContext.Messages.AddMessage(deleted);
        var handler = factory.GetHandler(MessageTypeTag.RoomSyncResponse);
        var missed = new ChatTextMessage { Content = "упущенное" };
        var response = new RoomSyncResponseMessage
        {
            MissedMessages = [missed],
            UpdatedMessages = [],
            ExistingMessageIds = [kept.Id, missed.Id],
        };

        // Act
        var result = await handler.HandleAsync(response, CreateContext(Role.Client));

        // Assert
        roomContext.Messages.GetMessageById(missed.Id).Should().NotBeNull();
        roomContext.Messages.GetMessageById(deleted.Id).Should().BeNull();
        roomContext.Messages.GetMessageById(kept.Id).Should().NotBeNull();
        factory.Router.Verify(r => r.PublishLocally(It.IsAny<MIN.Core.Messaging.Stateless.RoomRelated.Messages.MessageDeleteMessage>(), roomId, It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()), Times.Once);
        result.ResultEvent.Should().BeOfType<RoomStateChangedEvent>();
    }

    /// <summary>
    /// Disconnect подтверждается и проваливает обработку критично
    /// </summary>
    [Fact]
    public async Task DisconnectShouldAckAndFailCritical()
    {
        // Arrange
        RegisterRoom();
        roomContext.Participants.AddParticipant(new Participant { Id = selfId, CurrentStatus = OnlineStatus.Online });
        var handler = factory.GetHandler(MessageTypeTag.Disconnect);
        var message = new DisconnectMessage { Reason = "кик" };

        // Act
        var result = await handler.HandleAsync(message, CreateContext(Role.Client));

        // Assert
        factory.MessageSender.Verify(s => s.SendAsync(It.IsAny<DisconnectAckMessage>(), roomId, connectionId, It.IsAny<CancellationToken>()), Times.Once);
        result.IsSuccess.Should().BeFalse();
        result.CriticalErrorForConnection.Should().BeTrue();
        result.CriticalErrorForDestroy.Should().BeTrue();
        result.ErrorMessage.Should().Contain("кик");
    }

    /// <summary>
    /// DisconnectAck публикует событие
    /// </summary>
    [Fact]
    public async Task DisconnectAckShouldPublishEvent()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.DisconnectAck);
        var senderId = Guid.NewGuid();

        // Act
        var result = await handler.HandleAsync(new DisconnectAckMessage { SenderId = senderId }, CreateContext());

        // Assert
        var ackEvent = result.ResultEvent.Should().BeOfType<DisconnectAckReceived>().Subject;
        ackEvent.ParticipantId.Should().Be(senderId);
        ackEvent.ConnectionId.Should().Be(connectionId);
    }

    /// <summary>
    /// Запрос быстрого канала возвращает UDP endpoint комнаты
    /// </summary>
    [Fact]
    public async Task FastChannelRequestShouldReturnUdpEndpoint()
    {
        // Arrange
        var udpEndpoint = new UdpEndpoint { IpAddress = "192.168.1.10", Port = 5001, Origin = AddressOrigin.Lan };
        factory.Transport.Endpoints = [udpEndpoint];
        factory.RoomConnections.RegisterServerConnection(roomId, Guid.NewGuid());
        var handler = factory.GetHandler(MessageTypeTag.FastChannelConnectRequest);

        // Act
        var result = await handler.HandleAsync(new FastChannelConnectRequestMessage { AddressOrigin = AddressOrigin.Lan }, CreateContext(Role.Host));

        // Assert
        var response = result.Response.Should().BeOfType<FastChannelConnectResponseMessage>().Subject;
        response.FastChannelEndpoint.Should().BeSameAs(udpEndpoint);
    }

    /// <summary>
    /// Ответ без UDP endpoint оставляет на TCP и начинает вход
    /// </summary>
    [Fact]
    public async Task FastChannelResponseNullShouldJoinViaTcp()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.FastChannelConnectResponse);

        // Act
        var result = await handler.HandleAsync(new FastChannelConnectResponseMessage { FastChannelEndpoint = null }, CreateContext());

        // Assert
        result.Response.Should().BeOfType<RoomJoinRequestMessage>();
        factory.Transport.Connections.Should().BeEmpty();
    }

    /// <summary>
    /// Ответ с UDP endpoint подключает быстрый канал по адресу TCP-соединения
    /// </summary>
    [Fact]
    public async Task FastChannelResponseShouldConnectUdp()
    {
        // Arrange
        var room = RegisterRoom();
        room.ConnectionAddresses = [new TcpEndpoint { IpAddress = "127.0.0.1", Port = 5000, Origin = AddressOrigin.Lan }];
        var handler = factory.GetHandler(MessageTypeTag.FastChannelConnectResponse);
        var response = new FastChannelConnectResponseMessage
        {
            FastChannelEndpoint = new UdpEndpoint { IpAddress = "10.0.0.1", Port = 6000, Origin = AddressOrigin.Lan },
        };

        // Act
        var result = await handler.HandleAsync(response, CreateContext());

        // Assert
        result.Response.Should().BeOfType<RoomJoinRequestMessage>();
        factory.Transport.Connections.Should().ContainSingle().Which.Should().Be(connectionId);
    }

    /// <summary>
    /// Выход из комнаты помечает участника и подтверждается
    /// </summary>
    [Fact]
    public async Task RoomLeaveShouldMarkAndAck()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.RoomLeave);
        var senderId = Guid.NewGuid();

        // Act
        var result = await handler.HandleAsync(new RoomLeaveMessage { SenderId = senderId }, CreateContext(Role.Host));

        // Assert
        factory.Lifecycle.Verify(l => l.MarkParticipantAsLeftRoom(roomId, senderId), Times.Once);
        result.Response.Should().BeOfType<RoomLeaveMessageAckMessage>();
    }

    /// <summary>
    /// Подтверждение выхода завершает ожидание
    /// </summary>
    [Fact]
    public async Task RoomLeaveAckShouldComplete()
    {
        // Arrange
        var handler = factory.GetHandler(MessageTypeTag.RoomLeaveAck);

        // Act
        var result = await handler.HandleAsync(new RoomLeaveMessageAckMessage(), CreateContext());

        // Assert
        factory.Lifecycle.Verify(l => l.CompleteRoomLeaveAck(roomId), Times.Once);
        result.IsSuccess.Should().BeTrue();
        result.StopPropagation.Should().BeTrue();
    }

    /// <inheritdoc />
    public void Dispose() => factory.Dispose();
}
