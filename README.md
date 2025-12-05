# Abhay.MediatorCore

[![NuGet](https://img.shields.io/nuget/v/Abhay.MediatorCore.svg)](https://www.nuget.org/packages/Abhay.MediatorCore/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Abhay.MediatorCore.svg)](https://www.nuget.org/packages/Abhay.MediatorCore/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight, high-performance mediator library for .NET that implements the Mediator pattern with support for request/response, notifications, and pipeline behaviors.

## 🚀 Features

- ✅ **Request/Response Pattern** - Send commands and queries with typed responses
- ✅ **Notifications** - Publish events to multiple handlers
- ✅ **Pipeline Behaviors** - Add cross-cutting concerns (logging, validation, caching)
- ✅ **High Performance** - Optimized with delegate caching and minimal reflection overhead
- ✅ **Dependency Injection** - First-class Microsoft.Extensions.DependencyInjection support
- ✅ **Assembly Scanning** - Automatic handler registration
- ✅ **Lightweight** - Minimal dependencies, focused API
- ✅ **Multi-targeting** - Supports .NET 10.0

## 📦 Installation

```bash
dotnet add package Abhay.MediatorCore
```

Or via Package Manager Console:

```powershell
Install-Package Abhay.MediatorCore
```

## 🔨 Quick Start

### 1. Define a Request and Handler

```csharp
using MediatorCore;

// Request with response
public record GetUserQuery(int UserId) : IRequest<UserDto>;

// Handler
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto>
{
    private readonly IUserRepository _repository;
    
    public GetUserQueryHandler(IUserRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(request.UserId, cancellationToken);
        return new UserDto(user.Id, user.Name, user.Email);
    }
}
```

### 2. Register MediatorCore

```csharp
// In Program.cs or Startup.cs
builder.Services.AddMediator(typeof(Program).Assembly);
```

### 3. Use the Mediator

```csharp
public class UserController : ControllerBase
{
    private readonly IMediator _mediator;
    
    public UserController(IMediator mediator) => _mediator = mediator;
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _mediator.Send(new GetUserQuery(id));
        return Ok(user);
    }
}
```

## 📢 Notifications (Events)

Publish events to multiple handlers that run in parallel:

```csharp
// Define notification
public record UserCreatedNotification(int UserId, string Email) : INotification;

// Multiple handlers - all will be executed
public class SendWelcomeEmailHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly IEmailService _emailService;
    
    public SendWelcomeEmailHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }
    
    public async Task Handle(UserCreatedNotification notification, CancellationToken ct)
    {
        await _emailService.SendWelcomeEmailAsync(notification.Email, ct);
    }
}

public class CreateAuditLogHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly IAuditService _auditService;
    
    public CreateAuditLogHandler(IAuditService auditService)
    {
        _auditService = auditService;
    }
    
    public async Task Handle(UserCreatedNotification notification, CancellationToken ct)
    {
        await _auditService.LogAsync($"User {notification.UserId} created", ct);
    }
}

// Publish - all handlers execute in parallel
await _mediator.Publish(new UserCreatedNotification(userId, email));
```

## 🔄 Pipeline Behaviors

Add cross-cutting concerns that wrap around your handlers:

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }
    
    public async Task<TResponse> Handle(
        TRequest request, 
        CancellationToken cancellationToken, 
        RequestHandlerDelegate<TResponse> next)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", requestName);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await next();
            stopwatch.Stop();
            
            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms", 
                requestName, 
                stopwatch.ElapsedMilliseconds);
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex, 
                "Error handling {RequestName} after {ElapsedMs}ms", 
                requestName, 
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
```

### Validation Behavior Example

```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }
    
    public async Task<TResponse> Handle(
        TRequest request, 
        CancellationToken cancellationToken, 
        RequestHandlerDelegate<TResponse> next)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            
            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();
            
            if (failures.Count != 0)
                throw new ValidationException(failures);
        }
        
        return await next();
    }
}
```

## ⚙️ Configuration

```csharp
builder.Services.AddMediator(options =>
{
    // Throw exception if no handler registered for a notification
    options.ThrowIfNoNotificationHandlerRegistered = true;
}, typeof(Program).Assembly);
```

## 🎯 Manual Registration

If you prefer manual registration over assembly scanning:

```csharp
// Register without assembly scanning
services.AddMediator();

// Manually register handlers
services.AddRequestHandler<GetUserQueryHandler, GetUserQuery, UserDto>();
services.AddNotificationHandler<SendEmailHandler, UserCreatedNotification>();
services.AddPipelineBehavior<LoggingBehavior<GetUserQuery, UserDto>, GetUserQuery, UserDto>();
```

## 📝 Commands Without Response

For commands that don't return a value, use `Unit`:

```csharp
public record CreateUserCommand(string Name, string Email) : IRequest<Unit>;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Unit>
{
    public async Task<Unit> Handle(CreateUserCommand request, CancellationToken ct)
    {
        // Create user logic
        await _repository.CreateAsync(request.Name, request.Email, ct);
        return Unit.Value;
    }
}

// Or implement IRequestHandler<CreateUserCommand> which returns Unit by default
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand>
{
    public async Task<Unit> Handle(CreateUserCommand request, CancellationToken ct)
    {
        await _repository.CreateAsync(request.Name, request.Email, ct);
        return Unit.Value;
    }
}
```

## 🧪 Sample Projects

Check out the [samples](./samples) folder for complete working examples:

- **Basic Request/Response** - Simple command and query examples
- **Notifications** - Event publishing with multiple handlers
- **Pipeline Behaviors** - Logging, validation, and caching examples
- **Real-world Scenarios** - ASP.NET Core integration, CRUD operations

To run the samples:

```bash
cd samples/MediatorCore.Samples
dotnet run
```

## 📊 Performance

Abhay.MediatorCore is optimized for high performance:

- **Delegate Caching** - Handler invocations are cached to minimize reflection overhead
- **Efficient Pipeline** - Behaviors are chained efficiently without unnecessary allocations
- **Concurrent Notifications** - Multiple notification handlers execute in parallel
- **Minimal Allocations** - Uses modern C# patterns to reduce GC pressure

## 🏗️ Architecture Patterns

This library works great with:

- **CQRS** (Command Query Responsibility Segregation)
- **Clean Architecture**
- **Domain-Driven Design (DDD)**
- **Vertical Slice Architecture**
- **Event-Driven Architecture**

## 🔧 Requirements

- **.NET 10.0**
- **Microsoft.Extensions.DependencyInjection.Abstractions** 10.0.0+

## 📖 API Reference

### Core Interfaces

```csharp
// Send a request and get a response
public interface IMediator
{
    Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request, 
        CancellationToken cancellationToken = default);
    
    Task Publish<TNotification>(
        TNotification notification, 
        CancellationToken cancellationToken = default)
        where TNotification : INotification;
}

// Define a request
public interface IRequest<out TResponse> { }

// Define a request handler
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

// Define a notification
public interface INotification { }

// Define a notification handler
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}

// Define a pipeline behavior
public interface IPipelineBehavior<in TRequest, TResponse>
{
    Task<TResponse> Handle(
        TRequest request, 
        CancellationToken cancellationToken, 
        RequestHandlerDelegate<TResponse> next);
}
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 💡 Inspired By

This library is inspired by [MediatR](https://github.com/jbogard/MediatR) by Jimmy Bogard, reimagined with a focus on simplicity, performance, and modern .NET features.

## 🙏 Acknowledgments

- Jimmy Bogard for the original MediatR pattern
- The .NET community for feedback and support

## 📞 Support

- **GitHub Issues**: [Report bugs or request features](https://github.com/abhaysharma3021/MediatorCore/issues)
- **Discussions**: [Ask questions and share ideas](https://github.com/abhaysharma3021/MediatorCore/discussions)

## ⭐ Show Your Support

If you find this library helpful, please give it a star on GitHub!

---

Made with ❤️ by [Abhay](https://github.com/abhaysharma3021)