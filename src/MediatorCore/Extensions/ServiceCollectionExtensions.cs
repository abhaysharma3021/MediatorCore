using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace MediatorCore;

/// <summary>
/// Extension methods for registering MediatorCore services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MediatorCore services to the specified <see cref="IServiceCollection"/>
    /// </summary>
    public static IServiceCollection AddMediator(this IServiceCollection services, Action<MediatorOptions>? configure = null)
    {
        return AddMediator(services, Array.Empty<Assembly>(), configure);
    }

    /// <summary>
    /// Adds MediatorCore services and scans the specified assemblies for handlers
    /// </summary>
    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        return AddMediator(services, assemblies, null);
    }

    /// <summary>
    /// Adds MediatorCore services, scans assemblies for handlers, and configures options
    /// </summary>
    public static IServiceCollection AddMediator(this IServiceCollection services, Assembly[] assemblies, Action<MediatorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new MediatorOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddScoped<IMediator, Mediator>();

        if (assemblies?.Length > 0)
        {
            RegisterHandlers(services, assemblies);
        }

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        var requestHandlerInterface = typeof(IRequestHandler<,>);
        var notificationHandlerInterface = typeof(INotificationHandler<>);
        var pipelineBehaviorInterface = typeof(IPipelineBehavior<,>);

        // Get all concrete types from assemblies in one pass
        var allTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .ToList();

        // Register request handlers
        foreach (var type in allTypes)
        {
            var requestHandlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == requestHandlerInterface)
                .ToList();

            foreach (var interfaceType in requestHandlerInterfaces)
            {
                services.TryAddScoped(interfaceType, type);
            }
        }

        // Register notification handlers (allow multiple)
        foreach (var type in allTypes)
        {
            var notificationHandlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == notificationHandlerInterface)
                .ToList();

            foreach (var interfaceType in notificationHandlerInterfaces)
            {
                services.AddScoped(interfaceType, type);
            }
        }

        // Register pipeline behaviors
        foreach (var type in allTypes)
        {
            var pipelineBehaviorInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == pipelineBehaviorInterface)
                .ToList();

            foreach (var interfaceType in pipelineBehaviorInterfaces)
            {
                services.AddScoped(interfaceType, type);
            }
        }
    }

    /// <summary>
    /// Manually registers a request handler
    /// </summary>
    public static IServiceCollection AddRequestHandler<THandler, TRequest, TResponse>(this IServiceCollection services)
        where THandler : class, IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        services.TryAddScoped<IRequestHandler<TRequest, TResponse>, THandler>();
        return services;
    }

    /// <summary>
    /// Manually registers a notification handler
    /// </summary>
    public static IServiceCollection AddNotificationHandler<THandler, TNotification>(this IServiceCollection services)
        where THandler : class, INotificationHandler<TNotification>
        where TNotification : INotification
    {
        services.AddScoped<INotificationHandler<TNotification>, THandler>();
        return services;
    }

    /// <summary>
    /// Manually registers a pipeline behavior
    /// </summary>
    public static IServiceCollection AddPipelineBehavior<TBehavior, TRequest, TResponse>(this IServiceCollection services)
        where TBehavior : class, IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        services.AddScoped<IPipelineBehavior<TRequest, TResponse>, TBehavior>();
        return services;
    }

    /// <summary>
    /// Registers a pipeline behavior that applies to all requests
    /// </summary>
    public static IServiceCollection AddOpenPipelineBehavior(this IServiceCollection services, Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);

        if (!behaviorType.IsGenericType || behaviorType.GetGenericTypeDefinition() != typeof(IPipelineBehavior<,>))
        {
            throw new ArgumentException(
                $"Type {behaviorType.Name} must be an open generic type implementing IPipelineBehavior<,>",
                nameof(behaviorType));
        }

        services.AddScoped(typeof(IPipelineBehavior<,>), behaviorType);
        return services;
    }
}
