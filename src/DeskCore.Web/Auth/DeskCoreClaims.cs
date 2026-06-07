namespace DeskCore.Web.Auth;

public static class DeskCoreClaims
{
    /// <summary>Guarda o cookie de autenticação emitido pela API, dentro do cookie criptografado do Web.</summary>
    public const string ApiAuthCookie = "deskcore:api-cookie";

    public const string FullName = "deskcore:full-name";
}
