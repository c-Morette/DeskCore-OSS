using DeskCore.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeskCore.Infrastructure.Identity;

/// <summary>
/// Opções próprias para o token de redefinição de senha: expiração curta (30 min)
/// sem afetar o provider padrão do Identity — que segue valendo ~1 dia e é usado pela
/// confirmação de e-mail do cadastro. Como <see cref="DataProtectorTokenProvider{TUser}"/>
/// lê <c>IOptions&lt;DataProtectionTokenProviderOptions&gt;</c> (covariante), basta uma
/// subclasse de opções com tempo de vida próprio.
/// </summary>
public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public PasswordResetTokenProviderOptions()
    {
        Name = PasswordResetTokenProvider.ProviderName;
        TokenLifespan = TimeSpan.FromMinutes(30);
    }
}

/// <summary>Provider de token de redefinição de senha com expiração de 30 minutos.</summary>
public sealed class PasswordResetTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<AppUser>> logger)
    : DataProtectorTokenProvider<AppUser>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "DeskCorePasswordReset";
}
