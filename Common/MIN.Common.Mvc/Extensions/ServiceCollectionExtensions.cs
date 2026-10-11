using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace MIN.Common.Mvc.Extensions;

/// <summary>
/// Методы расширения для <see cref="IServiceCollection"/>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует все интерфейсы указанного типа
    /// </summary>
    /// <param name="services"><inheritdoc cref="IServiceCollection"/></param>
    /// <param name="lifetime"><inheritdoc cref="ServiceLifetime"/></param>
    /// <typeparam name="TService">Тип, для которого осуществляется регистрация</typeparam>
    public static IServiceCollection RegisterAsImplementedInterfaces<TService>(this IServiceCollection services, ServiceLifetime lifetime)
    {
        services.TryAdd(new ServiceDescriptor(typeof(TService), typeof(TService), lifetime));
        var interfaces = typeof(TService).GetTypeInfo()
            .ImplementedInterfaces
            .Where(i => i != typeof(IDisposable)
                 && i != typeof(IAsyncDisposable)
                 && i.IsPublic);

        foreach (Type interfaceType in interfaces)
        {
            services.TryAdd(new ServiceDescriptor(interfaceType,
                provider => provider.GetRequiredService(typeof(TService)),
                lifetime));
        }

        return services;
    }

    /// <summary>
    /// Регистрирует списком реализации от якоря
    /// </summary>
    /// <typeparam name="TService">Тип, для которого осуществляется регистрация</typeparam>
    /// <typeparam name="TAnchor">Якорь для определения сборки</typeparam>
    /// <param name="services"><inheritdoc cref="IServiceCollection"/></param>
    /// <param name="lifetime"><inheritdoc cref="ServiceLifetime"/></param>
    public static void RegisterMultipleInterfacesAssignableFromAnchor<TService, TAnchor>(this IServiceCollection services, ServiceLifetime lifetime)
    {
        var serviceType = typeof(TService);
        var types = typeof(TAnchor).Assembly.GetTypes()
            .Where(t => serviceType.IsAssignableFrom(t) &&
                        !(t.IsAbstract ||
                          t.IsInterface));

        foreach (var type in types)
        {
            services.TryAddEnumerable(new ServiceDescriptor(serviceType, type, lifetime));
        }
    }

    /// <summary>
    /// Регистрирует списком реализации от интерфейса
    /// </summary>
    /// <typeparam name="TService">Тип, для которого осуществляется регистрация</typeparam>
    /// <typeparam name="TInterface">Тип, для которого осуществляется регистрация</typeparam>
    /// <param name="services"><inheritdoc cref="IServiceCollection"/></param>
    /// <param name="lifetime"><inheritdoc cref="ServiceLifetime"/></param>
    public static void RegisterMultipleInterfacesAssignableTo<TInterface, TService>(this IServiceCollection services, ServiceLifetime lifetime)
    {
        services.TryAddEnumerable(new ServiceDescriptor(typeof(TInterface), typeof(TService), lifetime));
    }

    /// <summary>
    /// Регистрация модуля в <see cref="IServiceCollection"/>
    /// </summary>
    public static void RegisterModule<TModule>(this IServiceCollection services)
    {
        if (Activator.CreateInstance(typeof(TModule)) is Module module)
        {
            module.Configure(services);
        }
    }
}
