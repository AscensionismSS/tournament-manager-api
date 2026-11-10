using Microsoft.Extensions.Logging;
using Tournaments.Application.Common.Interfaces;

namespace Tournaments.Infrastructure.Services;

/// <summary>
/// Implementação local de armazenamento que grava arquivos no diretório wwwroot/uploads/banners.
/// Ideal para desenvolvimento local e ambientes autocontidos.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _uploadsFolder;
    private const string RelativeBasePath = "/uploads/banners";

    public LocalFileStorageService(ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        _uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "banners");

        if (!Directory.Exists(_uploadsFolder))
        {
            Directory.CreateDirectory(_uploadsFolder);
        }
    }

    public async Task<string> UploadAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(_uploadsFolder, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        _logger.LogInformation("Arquivo salvo com sucesso no armazenamento local: {FilePath}", filePath);
        return $"{RelativeBasePath}/{uniqueFileName}";
    }

    public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return Task.CompletedTask;

        try
        {
            var fileName = Path.GetFileName(fileUrl);
            var filePath = Path.Combine(_uploadsFolder, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Arquivo removido do armazenamento local: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Falha ao remover arquivo '{FileUrl}' do armazenamento local: {Message}", fileUrl, ex.Message);
        }

        return Task.CompletedTask;
    }
}
