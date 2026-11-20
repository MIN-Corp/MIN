using FluentAssertions;
using MIN.Core.Events.Contracts.Models;
using Xunit;

namespace MIN.Core.Events.Tests;

/// <summary>
/// Тесты на <see cref="SubscriptionToken"/>
/// </summary>
public class SubscriptionTokenTests
{
    /// <summary>
    /// Освобождение токена вызывает отписку ровно один раз
    /// </summary>
    [Fact]
    public void DisposeShouldInvokeUnsubscribeOnce()
    {
        // Arrange
        var count = 0;
        var token = new SubscriptionToken(() => count++);

        // Act
        token.Dispose();
        token.Dispose();

        // Assert
        count.Should().Be(1);
    }
}
