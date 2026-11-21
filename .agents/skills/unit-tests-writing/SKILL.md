---
name: unit-tests-writing
description: Determines the test type for a MIN module and routes to the guideline in LLM/ with patterns and fixtures
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

Helps determine which type of test is needed for a MIN module and routes to the
detailed guideline in `/LLM/`. All tests are written in `MIN.*.Tests` projects
located **next to the module under test**.

## Test Type Definition

### By location/name of the tested project:

| Pattern | Test type | Guideline |
|---------|-----------|-----------|
| `Core/Cryptography`, `Core/Serialization`, `Core/Events`, `Core/Headers`, `Core/Stores`, `Core/Entities`, `Helpers`, `Common` | Core unit tests | `LLM/core-tests.md` |
| `Core/Handlers`, `Infrastructure/*/Handlers` | Handler tests | `LLM/handler-tests.md` |
| `Core/Services*`, `Infrastructure/*/Services` | Service tests | `LLM/service-tests.md` |
| `Core/Transport*`, `Core/Streaming`, `Core/Protocol`, `Core/Services.Pipeline`, `Sessions/*Transport` | Integration tests | `LLM/integration-tests.md` |

### By fixtures used:

| Fixture | Test type |
|---------|-----------|
| `EncryptorPair`, `TestSerializer`, `TemporaryDirectory`, `TestRoomFactory` | Core unit tests |
| `HandlerTestFactory` + `GetHandler(tag)` | Handler tests |
| `Mock<IMessageRouter>` / `Mock<IMessageSender>` + real stores | Service tests |
| `TestPorts` + real sockets, `InMemoryTransport` + `SendHandler`, `CapturingDispatcher` | Integration tests |

Full fixture catalog: `LLM/fixtures.md`.

## Test Project Placement

The test project lives in the same folder as the tested one:

- `Core/Cryptography/MIN.Core.Cryptography/` → `Core/Cryptography/MIN.Core.Cryptography.Tests/`
- `Infrastructure/Chat/MIN.Chat.Services/` → `Infrastructure/Chat/MIN.Chat.Tests/`
- `Helpers/MIN.Helpers/` → `Helpers/MIN.Helpers.Tests/`

Shared fixture library: `Common/MIN.Tests.Common`. In `MIN.sln` test projects
are grouped in the same virtual solution folders as their tested modules.

## Commands

```bash
# Single project
dotnet test Core/Events/MIN.Core.Events.Tests/MIN.Core.Events.Tests.csproj

# All tests
find Core Infrastructure Helpers Common -name '*.Tests.csproj' -not -path '*/obj/*' | while read p; do dotnet test "$p"; done

# Single test
dotnet test <csproj> --filter "FullyQualifiedName~MessageEncryptorTests.EncryptDecryptShouldRoundTrip"
```

## Next Steps

After determining the test type, open the corresponding guideline in `/LLM/`:
it covers structure, naming, setup patterns, fixture usage and pitfalls.
