using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;

namespace MediatorCore;

/// <summary>
/// Main mediator implementation that dispatches requests and publishes notifications
/// </summary>
public class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MediatorOptions _options;

    // Cache for compiled handler delegates to avoid reflection on every call
    private static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, Task<object>>> _handlerCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> _notificationHandlerCache = new();

    public Mediator(IServiceProvider serviceProvider, MediatorOptions options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var responseType = typeof(TResponse);
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);

        var handler = _serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException(
                $"No handler of type '{handlerType.Name}' registered for request '{requestType.Name}'. " +
                $"Ensure the handler is registered in the DI container.");

        // Get or create cached handler delegate
        var handlerFunc = _handlerCache.GetOrAdd(requestType, _ =>
        {
            var handleMethod = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.Handle))
                ?? throw new InvalidOperationException($"Handle method not found on {handlerType.Name}");

            return (h, r, ct) =>
            {
                var task = (Task<TResponse>)handleMethod.Invoke(h, new object[] { r, ct })!;
                return task.ContinueWith(t => (object)t.Result!, TaskScheduler.Current);
            };
        });

        // Get pipeline behaviors
        var pipelineBehaviors = GetPipelineBehaviors(requestType, responseType);

        // Create the handler delegate
        RequestHandlerDelegate<TResponse> handlerDelegate = async () =>
        {
            var result = await handlerFunc(handler, request, cancellationToken);
            return (TResponse)result;
        };

        // Build pipeline in reverse order
        for (int i = pipelineBehaviors.Count - 1; i >= 0; i--)
        {
            var behavior = pipelineBehaviors[i];
            var currentDelegate = handlerDelegate;
            handlerDelegate = () => InvokeBehavior(behavior, request, cancellationToken, currentDelegate, requestType, responseType);
        }

        return await handlerDelegate();
    }

    private static async Task<TResponse> InvokeBehavior<TResponse>(
        object behavior,
        object request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next,
        Type requestType,
        Type responseType)
    {
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var handleMethod = behaviorType.GetMethod(nameof(IPipelineBehavior<IRequest<TResponse>, TResponse>.Handle))
            ?? throw new InvalidOperationException($"Handle method not found on {behaviorType.Name}");

        var result = handleMethod.Invoke(behavior, new object[] { request, cancellationToken, next });
        return await (Task<TResponse>)result!;
    }

    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        var notificationType = notification.GetType();
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);

        var handlers = _serviceProvider.GetServices(handlerType).ToList();

        if (handlers.Count == 0 && _options.ThrowIfNoNotificationHandlerRegistered)
        {
            throw new InvalidOperationException(
                $"No handlers registered for notification '{notificationType.Name}'. " +
                $"Register at least one handler or set ThrowIfNoNotificationHandlerRegistered to false.");
        }

        if (handlers.Count == 0)
            return;

        // Cache the Handle method
        var handleMethod = _notificationHandlerCache.GetOrAdd(notificationType, _ =>
            handlerType.GetMethod(nameof(INotificationHandler<INotification>.Handle))
                ?? throw new InvalidOperationException($"Handle method not found on {handlerType.Name}")
        );

        // Execute all handlers in parallel
        var tasks = new Task[handlers.Count];
        for (int i = 0; i < handlers.Count; i++)
        {
            tasks[i] = (Task)handleMethod.Invoke(handlers[i], new object[] { notification, cancellationToken })!;
        }

        await Task.WhenAll(tasks);
    }

    private List<object> GetPipelineBehaviors(Type requestType, Type responseType)
    {
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        return _serviceProvider.GetServices(behaviorType).ToList()!;
    }
}
