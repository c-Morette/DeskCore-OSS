using System.Globalization;

namespace DeskCore.Shared.Localization;

/// <summary>
/// Mensagens bilíngues do backend por pares inline: <c>Msg.T("pt", "en")</c>.
/// A língua vem de <see cref="CultureInfo.CurrentUICulture"/>, que a API define a
/// partir do header <c>Accept-Language</c> enviado pelo BFF (idioma escolhido na UI).
/// Padrão PT-BR; "en" → inglês. Espelha o <c>Loc.T</c> da camada Web.
/// </summary>
public static class Msg
{
    public static bool IsEnglish => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

    public static string T(string pt, string en) => IsEnglish ? en : pt;
}
