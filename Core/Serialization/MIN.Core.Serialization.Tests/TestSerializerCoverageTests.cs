using FluentAssertions;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Serialization.Tests;

/// <summary>
/// Тесты на полноту покрытия <see cref="TestSerializer"/>
/// </summary>
public class TestSerializerCoverageTests
{
    /// <summary>
    /// Все конкретные IMessage якорных сборок имеют конструктор по умолчанию:
    /// тип без него молча выпадает из round-trip покрытия
    /// </summary>
    [Fact]
    public void AllConcreteMessageTypesShouldHaveParameterlessConstructor()
    {
        // Arrange
        var withoutCtor = TestSerializer.AnchorAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IMessage).IsAssignableFrom(type)
                && type.IsClass
                && !type.IsAbstract
                && type.GetConstructor(Type.EmptyTypes) == null)
            .Select(type => type.FullName)
            .ToList();

        // Assert
        withoutCtor.Should().BeEmpty(
            "типы сообщений без конструктора по умолчанию молча выпадают из round-trip покрытия: {0}",
            string.Join(", ", withoutCtor));
    }

    /// <summary>
    /// AllMessages содержит экземпляры всех конкретных IMessage якорных сборок
    /// </summary>
    [Fact]
    public void AllMessagesShouldCoverEveryConcreteMessageType()
    {
        // Arrange
        var expectedTypeNames = TestSerializer.AnchorAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IMessage).IsAssignableFrom(type)
                && type.IsClass
                && !type.IsAbstract
                && type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => type.FullName)
            .ToList();

        // Act
        var actualTypeNames = TestSerializer.AllMessages
            .Select(message => message.GetType().FullName)
            .Distinct()
            .ToList();

        // Assert
        actualTypeNames.Should().BeEquivalentTo(expectedTypeNames);
    }
}
