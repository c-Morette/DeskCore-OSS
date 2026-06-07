namespace DeskCore.Infrastructure.Storage;

/// <summary>
/// Configuração do armazenamento local de anexos. <see cref="RootPath"/> deve
/// apontar para um volume persistente fora do diretório público (ex.: <c>/app/uploads</c>).
/// </summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string RootPath { get; set; } = "uploads";
}
