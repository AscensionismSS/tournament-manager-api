namespace Tournaments.Application.Common.Interfaces;

/// <summary>
/// Contrato de serviço para armazenamento de arquivos (Banners, Anexos, etc.).
/// Permite alternar facilmente entre armazenamento local (dev) e provedores na nuvem (AWS S3, Cloudflare R2, Azure Blob).
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Salva o stream de arquivo no provedor de armazenamento configurado.
    /// </summary>
    /// <param name="stream">Fluxo de dados do arquivo.</param>
    /// <param name="originalFileName">Nome original do arquivo para preservação segura da extensão.</param>
    /// <param name="contentType">Tipo MIME do arquivo (ex: image/png).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>URL pública ou relativa do arquivo persistido.</returns>
    Task<string> UploadAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove o arquivo persistido a partir de sua URL.
    /// </summary>
    /// <param name="fileUrl">URL do arquivo a ser removido.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);
}
