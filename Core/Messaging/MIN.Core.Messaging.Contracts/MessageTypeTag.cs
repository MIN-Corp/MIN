namespace MIN.Core.Messaging.Contracts;

/// <summary>
/// Определяет типы сообщений для маршрутизации.
/// Значения структурированы по диапазонам для обеспечения расширяемости.
/// </summary>
/// <remarks>
/// Диапазоны значений:
/// 0-31   - Системные сообщения
/// 32-47  - Сообщения обнаружения
/// 48-63  - Сообщения сессий
/// 64-95  - Сообщения чата
/// 96-127 - Сообщения управления комнатой
/// 128-142 - Сообщения для передачи файлов
/// 143-159 - Сообщения для звонков
/// 160-255 - Зарезервировано для будущих категорий
/// </remarks>
public enum MessageTypeTag : byte
{
    // ===== Системные сообщения (0-31) =====

    /// <summary>
    /// Сердцебиение для поддержания соединения.
    /// </summary>
    Heartbeat = 0,

    /// <summary>
    /// Приветственное сообщение при установке соединения (содержит публичный ключ).
    /// </summary>
    Handshake = 1,

    /// <summary>
    /// Подтверждение рукопожатия.
    /// </summary>
    HandshakeAck = 2,

    /// <summary>
    /// Сообщение об ошибке.
    /// </summary>
    Error = 3,

    /// <summary>
    /// Закрытие соединения.
    /// </summary>
    Disconnect = 4,

    /// <summary>
    /// Ответ на закрытие соединения.
    /// </summary>
    DisconnectAck = 5,

    /// <summary>
    /// Проверка доступности (ping).
    /// </summary>
    Ping = 6,

    /// <summary>
    /// Ответ на ping (pong).
    /// </summary>
    Pong = 7,

    /// <summary>
    /// Запрос на подключение к быстрому каналу
    /// </summary>
    FastChannelConnectRequest = 8,

    /// <summary>
    /// Ответ на подключение к быстрому каналу
    /// </summary>
    FastChannelConnectResponse = 9,

    /// <summary>
    /// Запрос на получение публичного ключа
    /// </summary>
    PublicKeyRequest = 10,

    /// <summary>
    /// Ответ на получение публичного ключа
    /// </summary>
    PublicKeyResponse = 11,

    // ===== Сообщения обнаружения (32-47) =====

    /// <summary>
    /// Запрос на обнаружение активных комнат в локальной сети.
    /// </summary>
    DiscoveryRequest = 32,

    /// <summary>
    /// Ответ на запрос обнаружения, содержащий информацию о комнате.
    /// </summary>
    DiscoveryResponse = 33,

    /// <summary>
    /// Широковещательное уведомление о появлении новой комнаты.
    /// </summary>
    RoomAnnouncement = 34,

    /// <summary>
    /// Запрос на получение полной информации о комнате.
    /// </summary>
    RoomInfoRequest = 35,

    /// <summary>
    /// Подробная информация о комнате.
    /// </summary>
    RoomInfoResponse = 36,

    // ===== Сообщения сессий (48-63) =====

    /// <summary>
    /// Запроса на хостинг шахмат.
    /// </summary>
    SessionHostRequest = 48,

    /// <summary>
    /// Готовность хостинга сессии.
    /// </summary>
    SessionReady = 49,

    /// <summary>
    /// Запроса на присоединения к сессии.
    /// </summary>
    SessionJoinRequest = 50,

    /// <summary>
    /// Ответ на присоединения к сессии.
    /// </summary>
    SessionJoinResponse = 51,

    /// <summary>
    /// Специфичное сообщение внутри сессии.
    /// </summary>
    SessionSpecific = 52,

    /// <summary>
    /// Выход из сессии.
    /// </summary>
    SessionLeave = 53,

    /// <summary>
    /// Участник вошёл в сессию.
    /// </summary>
    SessionParticipantJoined = 54,

    /// <summary>
    /// Участник вышел из сессии.
    /// </summary>
    SessionParticipantLeft = 55,

    /// <summary>
    /// Закрытие сервера сессии.
    /// </summary>
    SessionServerShutdown = 56,

    /// <summary>
    /// Ошибка захода сессии.
    /// </summary>
    SessionJoinFailed = 57,

    // ===== Сообщения чата (64-95) =====

    /// <summary>
    /// Обычное текстовое сообщение чата.
    /// </summary>
    ChatTextMessage = 64,

    /// <summary>
    /// Индикатор набора текста.
    /// </summary>
    TypingIndicator = 65,

    /// <summary>
    /// Запрос истории сообщений.
    /// </summary>
    ChatHistoryRequest = 66,

    /// <summary>
    /// Ответ с историей сообщений.
    /// </summary>
    ChatHistoryResponse = 67,

    /// <summary>
    /// Удаление сообщения.
    /// </summary>
    MessageDelete = 68,

    /// <summary>
    /// Редактирование сообщения.
    /// </summary>
    MessageEdit = 69,

    /// <summary>
    /// Реакция на сообщение (лайк, эмодзи).
    /// </summary>
    MessageReaction = 70,

    /// <summary>
    /// Системные сообщения (участник зашёл, загрузка)
    /// </summary>
    SystemMessage = 71,

    /// <summary>
    /// Смена статуса онлайн
    /// </summary>
    OnlineStatusChanged = 72,

    /// <summary>
    /// Удаление истории чата
    /// </summary>
    ChatHistoryClear = 73,

    // ===== Сообщения управления комнатой (96-127) =====

    /// <summary>
    /// Запрос на создание комнаты.
    /// </summary>
    RoomCreateRequest = 96,

    /// <summary>
    /// Обновление информации о комнате.
    /// </summary>
    RoomInfoUpdated = 97,

    /// <summary>
    /// Запрос на присоединение к комнате.
    /// </summary>
    RoomJoinRequest = 98,

    /// <summary>
    /// Ответ на запрос присоединения.
    /// </summary>
    RoomJoinResponse = 99,

    /// <summary>
    /// Подробная информация о комнате.
    /// </summary>
    RoomJoinRejectAck = 100,

    /// <summary>
    /// Уведомление о присоединении нового участника.
    /// </summary>
    ParticipantJoined = 101,

    /// <summary>
    /// Подтверждение о присоединении нового участника.
    /// </summary>
    ParticipantAccepted = 102,

    /// <summary>
    /// Уведомление о выходе участника.
    /// </summary>
    ParticipantLeft = 103,

    /// <summary>
    /// Обновление информации об участнике.
    /// </summary>
    ParticipantUpdated = 104,

    /// <summary>
    /// Сообщение о миграции хоста.
    /// </summary>
    HostMigration = 105,

    /// <summary>
    /// Сообщение о выходе из комнаты.
    /// </summary>
    RoomLeave = 106,

    /// <summary>
    /// Подтерждение выхода из комнаты.
    /// </summary>
    RoomLeaveAck = 107,

    // ===== Сообщения для передачи файлов (128-142) =====

    /// <summary>
    /// Метаданные файла (имя, размер, тип).
    /// </summary>
    FileMetadata = 128,

    /// <summary>
    /// Запрос на передачу файла.
    /// </summary>
    FileTransferRequest = 129,

    /// <summary>
    /// Ответ на запрос передачи файла.
    /// </summary>
    FileTransferResponse = 130,

    /// <summary>
    /// пакет (фрагмент) файла.
    /// </summary>
    FileChunk = 131,

    /// <summary>
    /// Подтверждение получения пакета.
    /// </summary>
    FileChunkAck = 132,

    /// <summary>
    /// Завершение передачи файла.
    /// </summary>
    FileTransferComplete = 133,

    /// <summary>
    /// Отмена передачи файла.
    /// </summary>
    FileTransferCancel = 134,

    // ===== Сообщения для звонков (143-159) =====

    /// <summary>
    /// Запрос на звонок.
    /// </summary>
    VoiceCallStartRequest = 143,

    /// <summary>
    /// Приглашение к звонку (звонок).
    /// </summary>
    VoiceCallStarted = 144,

    /// <summary>
    /// Принятие приглашения на звонок.
    /// </summary>
    VoiceCallJoinRequest = 145,

    /// <summary>
    /// Звонок подтверждён.
    /// </summary>
    VoiceCallEstablished = 146,

    /// <summary>
    /// Уведомление о присоединении нового участника к звонку.
    /// </summary>
    VoiceParticipantJoined = 147,

    /// <summary>
    /// Уведомление об уходе участника из звонка.
    /// </summary>
    VoiceParticipantLeft = 148,

    /// <summary>
    /// Звуковые данные сообщения.
    /// </summary>
    VoiceData = 149,

    /// <summary>
    /// Выход из звонка.
    /// </summary>
    VoiceCallLeave = 150,

    /// <summary>
    /// Уведомление об уходе последнего участника из звонка.
    /// </summary>
    VoiceCallEnded = 151,

    /// <summary>
    /// Запрос на получение состояние звонка в комнате.
    /// </summary>
    VoiceStateRequest = 152,

    /// <summary>
    /// Ответ на состояние звонка в комнате.
    /// </summary>
    VoiceStateResponse = 153,

    /// <summary>
    /// Ответ на состояние звонка в комнате.
    /// </summary>
    VoiceMuteState = 154,

    // ===== Зарезервировано (160-255) =====
    // Свободные диапазоны для будущих категорий
}
