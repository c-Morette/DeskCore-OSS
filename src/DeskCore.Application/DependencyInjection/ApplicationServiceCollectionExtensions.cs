using DeskCore.Application.Abstractions.Notifications;
using DeskCore.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeskCore.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IMetricsService, MetricsService>();

        // Abstrações sem efeito real na V1 (escopo §21).
        services.AddSingleton<INotificationService, NullNotificationService>();
        // Fallback: usa o NullEmailSender só se a Infra não registrou um sender real (SMTP).
        services.TryAddSingleton<IEmailSender, NullEmailSender>();

        return services;
    }
}
