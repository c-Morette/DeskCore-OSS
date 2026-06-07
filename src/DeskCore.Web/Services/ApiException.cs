namespace DeskCore.Web.Services;

/// <summary>Erro retornado pela API, já com status e mensagem amigável.</summary>
public sealed class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
