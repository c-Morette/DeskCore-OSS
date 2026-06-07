namespace DeskCore.Web.Display;

/// <summary>Uma barra de um gráfico de barras (rótulo já pronto, valor e classe de cor do tema).</summary>
public sealed record ChartBar(string Label, long Count, string Color);
