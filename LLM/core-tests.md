# Guideline: core unit tests for MIN

For the modules `Core/Cryptography`, `Core/Serialization`, `Core/Events`,
`Core/Headers`, `Core/Stores`, `Core/Entities`, `Helpers`, `Common`.
Reference projects: `MIN.Core.Cryptography.Tests`, `MIN.Core.Stores.Tests`,
`MIN.Core.Serialization.Tests`.

## Structure and naming

- Test project next to the tested one: `Core/Events/MIN.Core.Events.Tests/`.
- Class: `{Subject}Tests` (`MessageEncryptorTests`, `MessageStoreTests`).
- Method: `{Method}Should{Behavior}` (`EncryptDecryptShouldRoundTrip`,
  `GetRoomUnknownShouldThrow`).
- Russian XML docs on the class and on every test are mandatory
  (`TreatWarningsAsErrors` + `GenerateDocumentationFile`).
- Body: `// Arrange`, `// Act`, `// Assert` comments, blank line between blocks.
- Files: CRLF + UTF-8 BOM, file-scoped namespace, 4-space indent, `var`.

## Setup

Dependencies are real lightweight implementations or fixtures, not mocks:

```csharp
public class MessageEncryptorTests
{
    /// <summary>
    /// Зашифрованное сообщение расшифровывается в исходные байты
    /// </summary>
    [Fact]
    public async Task EncryptDecryptShouldRoundTrip()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var plaintext = "Привет, MIN!"u8.ToArray();

        // Act
        var encrypted = pair.Alice.EncryptMessage(plaintext, pair.BobId);
        var decrypted = pair.Bob.DecryptMessage(encrypted, pair.AliceId);

        // Assert
        decrypted.Should().Equal(plaintext);
    }
}
```

- Crypto: `EncryptorPair` (real ECDH exchange), `TestIdentityDataPathProvider`.
- File stores: `TemporaryDirectory` / `TestAppDataProvider`; write corrupted
  files directly (`File.WriteAllTextAsync(path, "{битый json")`) and assert
  `.bak` creation + reset.
- Rooms: `TestRoomFactory` + real `RoomStore`/registries.
- Event bus: `InMemoryEventBus` + `EventCapture<T>`.

## Mandatory checks

- **Round-trip**: serialize → deserialize → field equality. For messages —
  a theory over `TestSerializer.AllMessages` (see below).
- **Negative cases**: unknown id, empty data, corrupted input —
  `Should().Throw<T>()` / `ThrowAsync<T>()`, with `.WithMessage("*fragment*")`
  when needed.
- **Boundary values**: empty payload, 1 MiB, negative lengths, duplicates,
  out-of-order.
- **Perf smoke** (module-dependent): generous limit so it never flakes:

```csharp
stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "50 cycles of AES-GCM over 1 MiB must not take forever");
```

## Theory over all messages

```csharp
public static IEnumerable<object[]> AllMessageData()
    => TestSerializer.AllMessages.Select(message => new object[] { message });

[Theory]
[MemberData(nameof(AllMessageData))]
public void AllMessagesShouldRoundTrip(IMessage message) { ... }
```

Nuances:
- `required` fields left null are not serialized (`WhenWritingNull`) and break
  deserialization — populate them via reflection before serializing
  (`RequiredMemberAttribute`); see `PopulateRequiredProperties` in
  `JsonMessageSerializerTests`. This is an STJ/net8 limitation, not a MIN bug.
- Theory data are live objects; VS does not enumerate them until run time:
  Test Explorer shows a single node, then N cases after the run.
- Coverage completeness is enforced by `TestSerializerCoverageTests` (every
  concrete `IMessage` in the anchor assemblies has a parameterless ctor and
  gets into AllMessages).

## Pitfalls

- Almost all classes implement their interfaces **explicitly** — keep variable
  types as the interface (`IEventBus`, `IMessageStore`, `IHeaderManager`),
  otherwise CS1061.
- Store exceptions: `ConnectionNotRegisteredException`,
  `ParticipantNotRegisteredException`, `RoomNotRegisteredException`.
- `IdentityFileStore` holds a global slot mutex: two live instances in one
  process get different slots. In tests, Dispose the first one before creating
  the second.
- Moq: set up `IEnumerable` returns explicitly (`LoadAll()` without setup
  returns null → NRE); loose mocks return `Task`s already completed.
- JSON literals in C# — raw strings: `"""{"typeTag":250}"""u8.ToArray()`.
- Collection expressions `[1, 2]` in `Concat` are inferred as int — write
  `new byte[] { 1, 2 }`.
