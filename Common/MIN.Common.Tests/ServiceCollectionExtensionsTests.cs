using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MIN.Common.Mvc;
using MIN.Common.Mvc.Extensions;
using Xunit;

namespace MIN.Common.Tests;

/// <summary>
/// Тесты DI-расширений <see cref="ServiceCollectionExtensions"/>
/// </summary>
public class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// Тестовый модуль
    /// </summary>
    public class TestModule : Module
    {
        /// <inheritdoc />
        protected override void Load(IServiceCollection services)
            => services.AddSingleton<ITestService, TestServiceA>();
    }

    /// <summary>
    /// RegisterAsImplementedInterfaces связывает интерфейсы с одним экземпляром
    /// </summary>
    [Fact]
    public void RegisterAsImplementedInterfacesShouldShareInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterAsImplementedInterfaces<TestServiceA>(ServiceLifetime.Singleton);

        // Act
        var provider = services.BuildServiceProvider();
        var concrete = provider.GetRequiredService<TestServiceA>();
        var viaInterface = provider.GetRequiredService<ITestService>();

        // Assert
        viaInterface.Should().BeSameAs(concrete);
    }

    /// <summary>
    /// Сканирование по якорю регистрирует все реализации
    /// </summary>
    [Fact]
    public void RegisterFromAnchorShouldRegisterAllImplementations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterMultipleInterfacesAssignableFromAnchor<ITestService, ITestAnchor>(ServiceLifetime.Singleton);

        // Act
        var provider = services.BuildServiceProvider();
        var all = provider.GetServices<ITestService>().ToList();

        // Assert
        all.Should().HaveCount(2);
        all.Select(s => s.Name).Should().BeEquivalentTo(["A", "B"]);
    }

    /// <summary>
    /// RegisterModule применяет конфигурацию модуля
    /// </summary>
    [Fact]
    public void RegisterModuleShouldConfigure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterModule<TestModule>();

        // Act
        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ITestService>();

        // Assert
        service.Name.Should().Be("A");
    }
}

/// <summary>
/// Якорь для сканирования тестовой сборки
/// </summary>
public interface ITestAnchor { }

/// <summary>
/// Тестовый сервис
/// </summary>
public interface ITestService
{
    /// <summary>
    /// Имя сервиса
    /// </summary>
    string Name { get; }
}

/// <summary>
/// Реализация A
/// </summary>
public class TestServiceA : ITestService
{
    /// <inheritdoc />
    public string Name => "A";
}

/// <summary>
/// Реализация B
/// </summary>
public class TestServiceB : ITestService
{
    /// <inheritdoc />
    public string Name => "B";
}
