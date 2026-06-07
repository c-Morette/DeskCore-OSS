using System.Globalization;

namespace DeskCore.Web.Display;

/// <summary>
/// Localização leve por pares inline: <c>Loc.T("Português", "English")</c>. Mantém as
/// duas línguas lado a lado no ponto de uso (tradução por contexto, não literal). A
/// língua atual vem de <see cref="CultureInfo.CurrentUICulture"/> (definida pelo
/// RequestLocalization a partir do cookie de cultura). Padrão: PT-BR; "en" → inglês.
/// </summary>
public static class Loc
{
    public static bool IsEnglish => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

    public static string T(string pt, string en) => IsEnglish ? en : pt;
}
