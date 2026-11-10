using System.Buffers.Binary;
using TournamentCreator.WebApi.Services;
using Xunit;

namespace Tournaments.UnitTests;

public class BannerImageValidatorTests
{
    private readonly BannerImageValidator _validator = new();

    [Fact]
    public void Validate_ZeroLengthFile_ReturnsFailure()
    {
        using var stream = new MemoryStream();
        var result = _validator.Validate(stream, "banner.png", 0);

        Assert.False(result.IsValid);
        Assert.Equal("Nenhum arquivo enviado", result.ErrorTitle);
    }

    [Fact]
    public void Validate_FileExceedsMaxSize_ReturnsFailure()
    {
        using var stream = new MemoryStream();
        const long overLimit = 5 * 1024 * 1024 + 1; // 5MB + 1 byte
        var result = _validator.Validate(stream, "banner.png", overLimit);

        Assert.False(result.IsValid);
        Assert.Equal("Arquivo muito grande", result.ErrorTitle);
    }

    [Theory]
    [InlineData("banner.exe")]
    [InlineData("script.sh")]
    [InlineData("document.pdf")]
    [InlineData("image.gif")]
    [InlineData("image.bmp")]
    public void Validate_UnsupportedExtension_ReturnsFailure(string fileName)
    {
        using var stream = new MemoryStream(new byte[100]);
        var result = _validator.Validate(stream, fileName, 100);

        Assert.False(result.IsValid);
        Assert.Equal("Formato de arquivo não suportado", result.ErrorTitle);
    }

    [Fact]
    public void Validate_CorruptedContent_ReturnsFailure()
    {
        var randomBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        using var stream = new MemoryStream(randomBytes);
        var result = _validator.Validate(stream, "fake.png", randomBytes.Length);

        Assert.False(result.IsValid);
        Assert.Equal("Conteúdo de arquivo inválido", result.ErrorTitle);
    }

    [Fact]
    public void Validate_BelowMinimumResolution_ReturnsFailure()
    {
        // 800x200 PNG (menor que o mínimo de 960x300)
        var pngBytes = CreatePngHeaderBytes(800, 200);
        using var stream = new MemoryStream(pngBytes);
        var result = _validator.Validate(stream, "small_banner.png", pngBytes.Length);

        Assert.False(result.IsValid);
        Assert.Equal("Resolução insuficiente", result.ErrorTitle);
    }

    [Fact]
    public void Validate_AboveMaximumResolution_ReturnsFailure()
    {
        // 4000x3000 PNG (maior que o máximo de 3840x2160)
        var pngBytes = CreatePngHeaderBytes(4000, 3000);
        using var stream = new MemoryStream(pngBytes);
        var result = _validator.Validate(stream, "huge_banner.png", pngBytes.Length);

        Assert.False(result.IsValid);
        Assert.Equal("Resolução excessiva", result.ErrorTitle);
    }

    [Fact]
    public void Validate_ValidBannerResolution_ReturnsSuccessAndRewindsStream()
    {
        // 1920x1080 PNG padrão
        var pngBytes = CreatePngHeaderBytes(1920, 1080);
        using var stream = new MemoryStream(pngBytes);
        // Desloca stream para simular leitura prévia
        stream.Position = 10;

        var result = _validator.Validate(stream, "hero_banner.png", pngBytes.Length);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Dimensions);
        Assert.Equal(1920, result.Dimensions.Width);
        Assert.Equal(1080, result.Dimensions.Height);
        Assert.Equal(0, stream.Position); // Stream rebobinado para persistência segura
    }

    private static byte[] CreatePngHeaderBytes(int width, int height)
    {
        var bytes = new byte[32];
        bytes[0] = 0x89;
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
        bytes[4] = 0x0D;
        bytes[5] = 0x0A;
        bytes[6] = 0x1A;
        bytes[7] = 0x0A;
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), 13);
        bytes[12] = (byte)'I';
        bytes[13] = (byte)'H';
        bytes[14] = (byte)'D';
        bytes[15] = (byte)'R';
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }
}
