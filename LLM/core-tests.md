# Guideline: юнит-тесты ядра MIN

Для модулей `Core/Cryptography`, `Core/Serialization`, `Core/Events`,
`Core/Headers`, `Core/Stores`, `Core/Entities`, `Helpers`, `Common`.
Образцы: `MIN.Core.Cryptography.Tests`, `MIN.Core.Stores.Tests`,
`MIN.Core.Serialization.Tests`.

## Структура и именование

- Проект рядом с тестируемым: `Core/Events/MIN.Core.Events.Tests/`.
- Класс: `{Subject}Tests` (`MessageEncryptorTests`, `MessageStoreTests`).
- Метод: `{Method}Should{Behavior}` (`EncryptDecryptShouldRoundTrip`,
  `GetRoomUnknownShouldThrow`).
- Русские XML-доки на классе и каждом тесте — обязательны
  (`TreatWarningsAsErrors` + `GenerateDocumentationFile`).
- Тело: комментарии `// Arrange`, `// Act`, `// Assert`, пустая строка между блоками.
- Файлы: CRLF + UTF-8 BOM, file-scoped namespace, 4 пробела, `var`.

## Setup

Зависимости — реальные лёгкие реализации или фикстуры, не моки:

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

- Крипта: `EncryptorPair` (реальный ECDH-обмен), `TestIdentityDataPathProvider`.
- Файловые сторы: `TemporaryDirectory` / `TestAppDataProvider`; повреждённые
  файлы пиши напрямую (`File.WriteAllTextAsync(path, "{битый json")`) и
  проверяй `.bak` + сброс.
- Комнаты: `TestRoomFactory` + `RoomStore`/реестры — реальные.
- Шина: `InMemoryEventBus` + `EventCapture<T>`.

## Обязательные проверки

- **Round-trip**: serialize → deserialize → равенство полей. Для сообщений —
  теория по `TestSerializer.AllMessages` (см. ниже).
- **Негатив**: неизвестный id, пустые данные, повреждённый ввод —
  `Should().Throw<T>()` / `ThrowAsync<T>()`, при необходимости
  `.WithMessage("*кусок*")`.
- **Граничные значения**: пустой payload, 1 МиБ, отрицательные длины,
  дубликаты, out-of-order.
- **Перф-смоук** (по желанию модуля): щедрый лимит, чтобы не флапать:

```csharp
stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "50 циклов AES-GCM по 1 МиБ не должны занимать вечность");
```

## Theory по всем сообщениям

```csharp
public static IEnumerable<object[]> AllMessageData()
    => TestSerializer.AllMessages.Select(message => new object[] { message });

[Theory]
[MemberData(nameof(AllMessageData))]
public void AllMessagesShouldRoundTrip(IMessage message) { ... }
```

Нюансы:
- `required`-поля с null не сериализуются (`WhenWritingNull`) и роняют
  десериализацию — заполняй их перед сериализацией через рефлексию
  (`RequiredMemberAttribute`), образец `PopulateRequiredProperties` в
  `JsonMessageSerializerTests`. Это ограничение STJ/net8, не баг MIN.
- Данные теории — живые объекты, VS не перечисляет их до запуска:
  Test Explorer показывает один узел, после прогона — N кейсов.
- Полнота покрытия проверяется `TestSerializerCoverageTests` (все конкретные
  `IMessage` якорных сборок имеют ctor без параметров и попадают в AllMessages).

## Грабли

- Почти все классы реализуют интерфейсы **явно** — держи переменные типом
  интерфейса (`IEventBus`, `IMessageStore`, `IHeaderManager`), иначе CS1061.
- Исключения сторов: `ConnectionNotRegisteredException`,
  `ParticipantNotRegisteredException`, `RoomNotRegisteredException`.
- `IdentityFileStore` занимает глобальный мьютекс слота: два живых экземпляра
  в одном процессе получают разные слоты. В тестах Dispose первого перед
  созданием второго.
- Moq: `IEnumerable`-возвраты настраивай явно (`LoadAll()` без setup вернёт
  null → NRE); `Task`-возвраты loose-мок отдаёт завершёнными.
- JSON-литералы в C# — raw strings: `"""{"typeTag":250}"""u8.ToArray()`.
- Коллекционные выражения `[1, 2]` в `Concat` выводятся как int — пиши
  `new byte[] { 1, 2 }`.
