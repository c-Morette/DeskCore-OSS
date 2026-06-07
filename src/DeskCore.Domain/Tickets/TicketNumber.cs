namespace DeskCore.Domain.Tickets;

/// <summary>
/// Formatação do número amigável do ticket (ex.: <c>TK-000001</c>).
/// O valor sequencial vem de uma sequence do PostgreSQL (camada Infrastructure).
/// </summary>
public static class TicketNumber
{
    public const string Prefix = "TK-";
    public const int Digits = 6;

    public static string Format(long sequenceValue) =>
        $"{Prefix}{sequenceValue.ToString("D" + Digits)}";
}
