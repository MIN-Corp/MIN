# Guideline: integration tests for MIN

For `Core/Transport*`, `Core/Streaming`, `Core/Protocol`,
`Core/Services.Pipeline`, `Sessions/*Transport`.
Reference projects: `MIN.Core.Transport.Tests`, `MIN.Core.Streaming.Tests`,
`MIN.Core.Protocol.Tests`, `InboundMessagePipelineTests`,
`MIN.Sessions.Core.Tests`.

## Two approaches

### 1. Real sockets on loopback (transport)

Ports via `TestPorts` (lives in MIN.Core.Transport.Tests):

```csharp
var port = TestPorts.GetFreeTcpPort();
var hostingId = await server.Transport.StartHostingAsync(null, (ushort)port, 1, CancellationToken.None);
await client.Transport.ConnectAsync(new TcpEndpoint { IpAddress = "127.0.0.1", Port = port }, clientId, CancellationToken.None);
await TestWait.UntilAsync(() => server.StateChanges.Any(e => e.IsConnected && e.ConnectionId == clientId));
```

- Each node gets its own transport instance + a queue of received events.
- Assert: bytes in both directions, 1 MiB, message order, broadcast with
  exclude, broken/duplicate preamble (raw `TcpClient`), negative frame
  length, disconnect.
- UDP: a garbage datagram (<16 bytes) must not kill reception of subsequent
  packets.
- `ChannelTransport` has explicit implementations: `var transport = (ITransport)channel;`.
- No UPnP: `SetUpEndpoints` with `new NetworkOptions()` (forwarding disabled).

### 2. InMemoryTransport + routing (streaming, pipeline, protocol)

End-to-end stand without sockets: the sender's `SendHandler` delivers bytes to
the receiver with real decryption:

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

- Crypto is a real `EncryptorPair`; register the receiver's connection in the
  sender's context: `context.Connections.Register(connId, new ParticipantInfo { Id = pair.BobId })`.
- Wire format of a regular message: `[outer marker 0/1][inner marker 0][json]`;
  encrypted: `[1][iv|cipher|tag]` of `[0][json]`.
- Stream chunk: `[outer marker][25-byte header][json]` (no inner marker).
- ACK: `[0x80][streamId 16][index 4]` → `IStreamManager.ProcessAck`.
- Assemble/send events are fire-and-forget Funcs: wait via `TestWait.UntilAsync`.
- Raw payload: read the file INSIDE the `MessageAssembled` handler (assembly
  deletes the temp file right after).
- Protocol (`HostHandshakeService`/`ClientHandshakeService`): subscription is
  synchronous up to the first await — `Deliver` can be called right after the
  task starts; the server response = `"MIN"` + JSON `RoomInfo`.

## What to assert in streaming

- All packets arrive and reassemble byte-for-byte (1 chunk, N chunks, 1 MiB).
- Out-of-order, duplicates (including after assembly), ACK per chunk with
  `RequiresAcks`, custom `StreamId`, cancellation (cts + slow
  SendHandler → `OnStreamFailed`).
- Negative: End with wrong index, missing middle chunk
  (`InvalidDataException`), partial Stream reads (custom Stream with
  `maxRead`), empty payload.

## Pitfalls

- Bind `TcpListener`/`UdpClient` in tests to loopback; release ports
  (using/Dispose), otherwise the next test catches a busy port.
- `WaitEofAsync` for socket-close checks: `ReadAsync` returned 0 or
  `IOException`, with a CTS timeout.
- `SessionScanner` writes into `AppDomain.BaseDirectory/Скаченные сессии` —
  create unique GUID directories and clean up in Dispose.
- `ConnectionInfo` serializes PascalCase (`Type`/`Value`) — no camelCase.
