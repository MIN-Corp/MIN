# Guideline: интеграционные тесты MIN

Для `Core/Transport*`, `Core/Streaming`, `Core/Protocol`,
`Core/Services.Pipeline`, `Sessions/*Transport`.
Образцы: `MIN.Core.Transport.Tests`, `MIN.Core.Streaming.Tests`,
`MIN.Core.Protocol.Tests`, `InboundMessagePipelineTests`,
`MIN.Sessions.Core.Tests`.

## Два подхода

### 1. Реальные сокеты на loopback (транспорт)

Порты — через `TestPorts` (живёт в MIN.Core.Transport.Tests):

```csharp
var port = TestPorts.GetFreeTcpPort();
var hostingId = await server.Transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
await client.Transport.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
await TestWait.UntilAsync(() => server.StateChanges.Any(e => e.IsConnected && e.ConnectionId == clientId));
```

- Каждый узел — свой экземпляр транспорта + очереди полученных событий.
- Проверяй: байты в обе стороны, 1 МиБ, порядок сообщений, broadcast с
  exclude, битую/дублирующую преамбулу (сырой `TcpClient`), отрицательную
  длину кадра, отключение.
- UDP: мусор-датаграмма (<16 байт) не должна убивать приём subsequent пакетов.
- `ChannelTransport` — явные реализации: `var transport = (ITransport)channel;`.
- UPnP не трогаем: `SetUpEndpoints` с `new NetworkOptions()` (проброс выключен).

### 2. InMemoryTransport + маршрутизация (стриминг, pipeline, протокол)

Сквозной стенд без сокетов: `SendHandler` отправителя доставляет байты
получателю с реальной расшифровкой:

```csharp
harness.SenderTransport.SendHandler = async datagram =>
{
    var data = datagram.Data;
    ReadOnlyMemory<byte> inner = data[0] == (byte)HeaderMessageType.Encrypted
        ? harness.Pair.Bob.DecryptMessage(data.AsMemory(1), harness.Pair.AliceId)
        : data.AsMemory(1);
    await ((IChunkBufferAssembler)receiverAssembler).ProcessStreamChunk(
        inner, roomId, senderConnectionId, null, CancellationToken.None);
};
```

- Крипта — реальный `EncryptorPair`; в контексте отправителя зарегистрируй
  соединение получателя: `context.Connections.Register(connId, new ParticipantInfo { Id = pair.BobId })`.
- Проводной формат обычного сообщения: `[внешний маркер 0/1][внутренний маркер 0][json]`;
  зашифрованный: `[1][iv|cipher|tag]` от `[0][json]`.
- Чанк потока: `[внешний маркер][25-байтный заголовок][json]` (без внутреннего маркера).
- ACK: `[0x80][streamId 16][index 4]` → `IStreamManager.ProcessAck`.
- События сборки/отправки — fire-and-forget Func: жди через `TestWait.UntilAsync`.
- Raw payload: файл читай ВНУТРИ обработчика `MessageAssembled` (после сборки
  поток удаляет временный файл).
- Протокол (`HostHandshakeService`/`ClientHandshakeService`): подписка
  синхронная до первого await — `Deliver` можно звать сразу после старта task;
  ответ сервера = `"MIN"` + JSON `RoomInfo`.

## Что проверять в стриминге

- Все пакеты доходят и собираются байт-в-байт (1 чанк, N чанков, 1 МиБ).
- Out-of-order, дубли (в т.ч. после сборки), ACK на каждый чанк при
  `RequiresAcks`, пользовательский `StreamId`, отмена (cts + медленный
  SendHandler → `OnStreamFailed`).
- Негатив: End с неверным индексом, потерянный средний чанк
  (`InvalidDataException`), частичные чтения Stream (кастомный Stream с
  `maxRead`), пустой payload.

## Грабли

- `TcpListener`/`UdpClient` в тестах бинди на loopback; порты освобождай
  (using/Dispose), иначе следующий тест поймает занятый порт.
- `WaitEofAsync` для проверки закрытия сокета: `ReadAsync` вернул 0 или
  `IOException`, с таймаутом через CTS.
- `SessionScanner` пишет в `AppDomain.BaseDirectory/Скаченные сессии` —
  создавай уникальные GUID-директории и убирай за собой в Dispose.
- `ConnectionInfo` сериализуется PascalCase (`Type`/`Value`) — без camelCase.
