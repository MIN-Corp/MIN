using System.Text.Json;
using FluentAssertions;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Identity;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Identity.Tests;

/// <summary>
/// Тесты на <see cref="IdentityService"/> и <see cref="IdentityFileStore"/>
/// </summary>
public class IdentityServiceTests
{
    /// <summary>
    /// Первый доступ генерирует и сохраняет идентификатор
    /// </summary>
    [Fact]
    public void FirstAccessShouldGenerateAndPersistId()
    {
        // Arrange
        using var appData = new TestAppDataProvider();
        using var service = new IdentityService(appData, new TestLoggerProvider());

        // Act
        var participant = ((IIdentityService)service).SelfParticipant;

        // Assert
        participant.Id.Should().NotBe(Guid.Empty);
        File.Exists(Path.Combine(appData.SharedDirectory, "identity", "uuid.json")).Should().BeTrue();
    }

    /// <summary>
    /// Новый экземпляр после перезапуска загружает тот же идентификатор
    /// </summary>
    [Fact]
    public void SecondInstanceShouldLoadSameId()
    {
        // Arrange
        using var appData = new TestAppDataProvider();
        Guid firstId;
        using (var first = new IdentityService(appData, new TestLoggerProvider()))
        {
            firstId = ((IIdentityService)first).SelfParticipant.Id;
        }

        // Act
        using var second = new IdentityService(appData, new TestLoggerProvider());
        var secondId = ((IIdentityService)second).SelfParticipant.Id;

        // Assert
        secondId.Should().Be(firstId);
    }

    /// <summary>
    /// Сохранение обновляет имя и уведомляет подписчика
    /// </summary>
    [Fact]
    public async Task SaveParticipantShouldUpdateNameAndNotify()
    {
        // Arrange
        using var appData = new TestAppDataProvider();
        var notified = false;
        using (var service = new IdentityService(appData, new TestLoggerProvider()))
        {
            var identity = (IIdentityService)service;
            identity.SelfParticipantSaved = _ => { notified = true; return Task.CompletedTask; };
            var newParticipant = new ParticipantInfo { Id = identity.SelfParticipant.Id, Name = "Новое имя" };

            // Act
            await identity.SaveParticipant(newParticipant);
        }

        // Assert
        notified.Should().BeTrue();
        using var verifier = new IdentityService(appData, new TestLoggerProvider());
        ((IIdentityService)verifier).SelfParticipant.Name.Should().Be("Новое имя");
    }

    /// <summary>
    /// Сохранение без подписчика не должно падать
    /// </summary>
    [Fact]
    public async Task SaveWithoutSubscriberShouldNotThrow()
    {
        // Arrange
        using var appData = new TestAppDataProvider();
        using var service = new IdentityService(appData, new TestLoggerProvider());
        var identity = (IIdentityService)service;
        var participant = new ParticipantInfo { Id = identity.SelfParticipant.Id, Name = "Имя" };

        // Act
        var act = () => identity.SaveParticipant(participant);

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Подделанный идентификатор с неверным хешем пересоздаётся
    /// </summary>
    [Fact]
    public void TamperedHashShouldRegenerateId()
    {
        // Arrange
        using var appData = new TestAppDataProvider();
        Guid originalId;
        using (var first = new IdentityService(appData, new TestLoggerProvider()))
        {
            originalId = ((IIdentityService)first).SelfParticipant.Id;
        }

        var identityPath = Path.Combine(appData.SharedDirectory, "identity", "uuid.json");
        using (var doc = JsonDocument.Parse(File.ReadAllText(identityPath)))
        {
            var entries = doc.RootElement.GetProperty("data").EnumerateArray().ToList();
            var tampered = entries.Select(e => new
            {
                slot = e.GetProperty("slot").GetInt32(),
                id = Guid.NewGuid(),
                name = e.GetProperty("name").GetString(),
                hash = e.GetProperty("hash").GetString(),
            });
            File.WriteAllText(identityPath, JsonSerializer.Serialize(new { schemaVersion = 1, data = tampered }));
        }

        // Act
        using var second = new IdentityService(appData, new TestLoggerProvider());
        var newId = ((IIdentityService)second).SelfParticipant.Id;

        // Assert
        newId.Should().NotBe(originalId);
    }

    /// <summary>
    /// Директории identity строятся от идентификатора участника
    /// </summary>
    [Fact]
    public void IdentityDataPathProviderShouldUseParticipantId()
    {
        // Arrange
        using var appData = new TestAppDataProvider();
        using var service = new IdentityService(appData, new TestLoggerProvider());
        var identity = (IIdentityService)service;

        // Act
        var paths = new IdentityDataPathProvider(appData, identity);

        // Assert
        paths.RoomsDirectory.Should().Contain(identity.SelfParticipant.Id.ToString());
        paths.CryptographyDirectory.Should().Contain("cryptography");
        paths.FilesDirectory.Should().Contain("files");
    }
}
