using DeskCore.Application.Abstractions.Notifications;
using DeskCore.Application.Abstractions.Persistence;
using DeskCore.Application.Abstractions.Storage;
using DeskCore.Application.Abstractions.Tickets;
using DeskCore.Domain.Entities;
using DeskCore.Infrastructure.Identity;
using DeskCore.Infrastructure.Maintenance;
using DeskCore.Infrastructure.Notifications;
using DeskCore.Infrastructure.Persistence;
using DeskCore.Infrastructure.Seeding;
using DeskCore.Infrastructure.Storage;
using DeskCore.Infrastructure.Tickets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeskCore.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

        services.AddDbContext<DeskCoreDbContext>(options =>
            options.UseNpgsql(connectionString, npg =>
                npg.MigrationsAssembly(typeof(DeskCoreDbContext).Assembly.FullName)));

        // Expõe o contexto como abstração consumida pela camada de aplicação.
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<DeskCoreDbContext>());

        services
            .AddIdentity<AppUser, IdentityRole>(options =>
            {
                // Senha forte (escopo §17)
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 4;

                // Lockout após tentativas inválidas
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                // Usuário
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;

                // Token de redefinição de senha com janela curta (30 min); o de
                // confirmação de e-mail segue no provider padrão (~1 dia).
                options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName;
            })
            .AddEntityFrameworkStores<DeskCoreDbContext>()
            .AddErrorDescriber<LocalizedIdentityErrorDescriber>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);

        // Cookie de autenticação (segurança; a API ajusta os eventos 401/403 na Fase 4).
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "DeskCore.Auth";
            options.Cookie.HttpOnly = true;
            // SameAsRequest (não Always): requisições externas chegam como HTTPS via
            // Nginx (ForwardedHeaders) e recebem o cookie Secure; a chamada interna do
            // BFF (Web->API por HTTP na rede do compose) recebe sem Secure, permitindo
            // que o CookieContainer a armazene. O cookie da API nunca vai a um browser
            // por HTTP, então não há regressão de segurança.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

        // DataProtection com app name fixo (chaves estáveis entre restarts/instâncias).
        var dataProtection = services.AddDataProtection().SetApplicationName("DeskCore");
        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            Directory.CreateDirectory(keysPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        // Envio de e-mail por SMTP (Resend). Só registra a implementação real quando
        // configurado; senão a Application registra o NullEmailSender via TryAdd.
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>();
        if (emailOptions?.IsConfigured == true)
            services.AddSingleton<IEmailSender, SmtpEmailSender>();

        // Armazenamento de anexos
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddScoped<IFileStorage, LocalFileStorage>();

        // Geração de Number via sequence PG
        services.AddScoped<ITicketNumberGenerator, TicketNumberGenerator>();

        // Migration + seed no startup (single-tenant)
        services.AddHostedService<DatabaseInitializerHostedService>();

        // Expurgo de retenção (LGPD): logs > 12 meses (1x/dia).
        services.AddHostedService<DataRetentionHostedService>();

        return services;
    }
}
