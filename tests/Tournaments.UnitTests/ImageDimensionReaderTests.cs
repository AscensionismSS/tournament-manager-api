using System.Buffers.Binary;
using TournamentCreator.WebApi.Services;
using Xunit;

namespace Tournaments.UnitTests;

public class ImageDimensionReaderTests
{
    [Fact]
    public void TryGetDimensions_ValidPng_ReturnsCorrectDimensions()
    {
        // Constrói um cabeçalho PNG válido de 1920x1080
        var pngBytes = new byte[32];
        // Assinatura PNG
        pngBytes[0] = 0x89;
        pngBytes[1] = 0x50;
        pngBytes[2] = 0x4E;
        pngBytes[3] = 0x47;
        pngBytes[4] = 0x0D;
        pngBytes[5] = 0x0A;
        pngBytes[6] = 0x1A;
        pngBytes[7] = 0x0A;
        // Tamanho IHDR (13)
        BinaryPrimitives.WriteInt32BigEndian(pngBytes.AsSpan(8, 4), 13);
        // "IHDR"
        pngBytes[12] = (byte)'I';
        pngBytes[13] = (byte)'H';
        pngBytes[14] = (byte)'D';
        pngBytes[15] = (byte)'R';
        // Largura: 1920
        BinaryPrimitives.WriteInt32BigEndian(pngBytes.AsSpan(16, 4), 1920);
        // Altura: 1080
        BinaryPrimitives.WriteInt32BigEndian(pngBytes.AsSpan(20, 4), 1080);

        using var stream = new MemoryStream(pngBytes);
        var dimensions = ImageDimensionReader.TryGetDimensions(stream, ".png");

        Assert.NotNull(dimensions);
        Assert.Equal(1920, dimensions.Width);
        Assert.Equal(1080, dimensions.Height);
    }

    [Fact]
    public void TryGetDimensions_ValidWebpLossy_ReturnsCorrectDimensions()
    {
        // Constrói cabeçalho WEBP VP8 simples (1920x1080)
        var webpBytes = new byte[32];
        webpBytes[0] = (byte)'R'; webpBytes[1] = (byte)'I'; webpBytes[2] = (byte)'F'; webpBytes[3] = (byte)'F';
        webpBytes[8] = (byte)'W'; webpBytes[9] = (byte)'E'; webpBytes[10] = (byte)'B'; webpBytes[11] = (byte)'P';
        webpBytes[12] = (byte)'V'; webpBytes[13] = (byte)'P'; webpBytes[14] = (byte)'8'; webpBytes[15] = (byte)' ';

        // Offset 26 e 28
        BinaryPrimitives.WriteUInt16LittleEndian(webpBytes.AsSpan(26, 2), 1920);
        BinaryPrimitives.WriteUInt16LittleEndian(webpBytes.AsSpan(28, 2), 1080);

        using var stream = new MemoryStream(webpBytes);
        var dimensions = ImageDimensionReader.TryGetDimensions(stream, ".webp");

        Assert.NotNull(dimensions);
        Assert.Equal(1920, dimensions.Width);
        Assert.Equal(1080, dimensions.Height);
    }

    [Fact]
    public void TryGetDimensions_CorruptedFile_ReturnsNull()
    {
        var randomBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        using var stream = new MemoryStream(randomBytes);
        var dimensions = ImageDimensionReader.TryGetDimensions(stream, ".png");

        Assert.Null(dimensions);
    }
}
