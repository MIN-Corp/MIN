---
name: unit-tests-writing
description: Определяет тип теста для модуля MIN и направляет к guideline-у в LLM/ с паттернами и фикстурами
keywords:
  - unit tests
  - testing
  - xUnit
  - FluentAssertions
  - Moq
  - fixtures
---

# Unit Tests Writing Helper (MIN)

## Purpose

Помогает определить, какой тип теста нужен для модуля MIN, и направляет к
детальному guideline-у в папке `/LLM/`. Все тесты пишутся в проектах
`MIN.*.Tests`, расположенных **рядом с тестируемым модулем**.

## Test Type Definition

### По расположению/имени тестируемого проекта:

| Pattern | Тип теста | Guideline |
|---------|-----------|-----------|
| `Core/Cryptography`, `Core/Serialization`, `Core/Events`, `Core/Headers`, `Core/Stores`, `Core/Entities`, `Helpers`, `Common` | Юнит-тесты ядра | `LLM/core-tests.md` |
| `Core/Handlers`, `Infrastructure/*/Handlers` | Тесты обработчиков | `LLM/handler-tests.md` |
| `Core/Services*`, `Infrastructure/*/Services` | Тесты сервисов | `LLM/service-tests.md` |
| `Core/Transport*`, `Core/Streaming`, `Core/Protocol`, `Core/Services.Pipeline`, `Sessions/*Transport` | Интеграционные тесты | `LLM/integration-tests.md` |

### По используемым фикстурам:

| Фикстура | Тип теста |
|----------|-----------|
| `EncryptorPair`, `TestSerializer`, `TemporaryDirectory`, `TestRoomFactory` | Юнит-тесты ядра |
| `HandlerTestFactory` + `GetHandler(tag)` | Тесты обработчиков |
| `Mock<IMessageRouter>` / `Mock<IMessageSender>` + реальные сторы | Тесты сервисов |
| `TestPorts` + реальные сокеты, `InMemoryTransport` + `SendHandler`, `CapturingDispatcher` | Интеграционные тесты |

Полный каталог фикстур: `LLM/fixtures.md`.

## Размещение тестовых проектов

Тестовый проект лежит в той же папке, что и тестируемый:

- `Core/Cryptography/MIN.Core.Cryptography/` → `Core/Cryptography/MIN.Core.Cryptography.Tests/`
- `Infrastructure/Chat/MIN.Chat.Services/` → `Infrastructure/Chat/MIN.Chat.Tests/`
- `Helpers/MIN.Helpers/` → `Helpers/MIN.Helpers.Tests/`

Общая библиотека фикстур: `Common/MIN.Tests.Common`. В `MIN.sln` все тестовые
проекты сгруппированы в виртуальную папку `Tests`.

## Commands

```bash
# Один проект
dotnet test Core/Events/MIN.Core.Events.Tests/MIN.Core.Events.Tests.csproj

# Все тесты
find Core Infrastructure Helpers Common -name '*.Tests.csproj' -not -path '*/obj/*' | while read p; do dotnet test "$p"; done

# Один тест
dotnet test <csproj> --filter "FullyQualifiedName~MessageEncryptorTests.EncryptDecryptShouldRoundTrip"
```

## Next Steps

После определения типа теста открой соответствующий guideline в `/LLM/`:
там структура, именование, паттерны setup, работа с фикстурами и грабли.
