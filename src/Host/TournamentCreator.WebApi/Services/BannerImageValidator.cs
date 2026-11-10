namespace TournamentCreator.WebApi.Services;

public record BannerValidationResult(
    bool IsValid,
    string? ErrorTitle = null,
    string? ErrorDetail = null,
    ImageDimensions? Dimensions = null)
{
    public static BannerValidationResult Success(ImageDimensions dimensions) =>
        new(true, Dimensions: dimensions);

    public static BannerValidationResult Failure(string title, string detail) =>
        new(false, ErrorTitle: title, ErrorDetail: detail);
}

public interface IBannerImageValidator
{
    BannerValidationResult Validate(Stream stream, string fileName, long fileLength);
}

/// <summary>
/// Validador desacoplado para banners de torneios com verificação de tamanho,
/// extensões permitidas, integridade binária (Magic Bytes) e resolução mínima/máxima.
/// Segue rigorosamente o Single Responsibility Principle (SRP).
/// </summary>
public class BannerImageValidator : IBannerImageValidator
{
    public const long MaxSizeBytes = 5 * 1024 * 1024; // 5 MB
    public const int MinWidth = 960;
    public const int MinHeight = 300;
    public const int MaxWidth = 3840;
    public const int MaxHeight = 2160;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    public BannerValidationResult Validate(Stream stream, string fileName, long fileLength)
    {
        if (fileLength <= 0)
        {
            return BannerValidationResult.Failure(
                "Nenhum arquivo enviado",
                "Por favor, selecione uma imagem para o banner.");
        }

        if (fileLength > MaxSizeBytes)
        {
            return BannerValidationResult.Failure(
                "Arquivo muito grande",
                "A imagem excede o limite máximo permitido de 5MB.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return BannerValidationResult.Failure(
                "Formato de arquivo não suportado",
                "Formatos aceitos: .png, .jpg, .jpeg, .webp.");
        }

        // Leitura e validação binária de dimensões e magic bytes em passagem única
        var dimensions = ImageDimensionReader.TryGetDimensions(stream, extension);
        if (dimensions == null)
        {
            return BannerValidationResult.Failure(
                "Conteúdo de arquivo inválido",
                "O arquivo não corresponde a uma imagem autêntica válida.");
        }

        if (dimensions.Width < MinWidth || dimensions.Height < MinHeight)
        {
            return BannerValidationResult.Failure(
                "Resolução insuficiente",
                $"A imagem possui {dimensions.Width}x{dimensions.Height}px. A resolução mínima para o banner é de {MinWidth}x{MinHeight}px (Recomendado: 1920x1080 ou 1920x600).");
        }

        if (dimensions.Width > MaxWidth || dimensions.Height > MaxHeight)
        {
            return BannerValidationResult.Failure(
                "Resolução excessiva",
                $"A imagem possui {dimensions.Width}x{dimensions.Height}px. A resolução máxima permitida é de {MaxWidth}x{MaxHeight}px.");
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return BannerValidationResult.Success(dimensions);
    }
}
