using System.Buffers.Binary;

namespace TournamentCreator.WebApi.Services;

public record ImageDimensions(int Width, int Height);

/// <summary>
/// Leitor ultra-rápido e seguro de dimensões de imagens (PNG, JPEG, WEBP) diretamente dos cabeçalhos binários,
/// sem carregar bitmaps pesados em memória nem depender de bibliotecas externas vulneráveis.
/// </summary>
public static class ImageDimensionReader
{
    public static ImageDimensions? TryGetDimensions(Stream stream, string extension)
    {
        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            var ext = extension.ToLowerInvariant();
            return ext switch
            {
                ".png" => TryGetPngDimensions(stream),
                ".jpg" or ".jpeg" => TryGetJpegDimensions(stream),
                ".webp" => TryGetWebpDimensions(stream),
                _ => null
            };
        }
        catch
        {
            return null;
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
        }
    }

    private static ImageDimensions? TryGetPngDimensions(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[24];
        int read = stream.Read(buffer);
        if (read < 24) return null;

        // Assinatura PNG: 89 50 4E 47
        if (buffer[0] != 0x89 || buffer[1] != 0x50 || buffer[2] != 0x4E || buffer[3] != 0x47)
            return null;

        int width = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(20, 4));

        return new ImageDimensions(width, height);
    }

    private static ImageDimensions? TryGetJpegDimensions(Stream stream)
    {
        // JPEG inicia com FF D8
        int b1 = stream.ReadByte();
        int b2 = stream.ReadByte();
        if (b1 != 0xFF || b2 != 0xD8) return null;

        Span<byte> lenBuffer = stackalloc byte[2];

        while (true)
        {
            int markerPrefix = stream.ReadByte();
            while (markerPrefix != 0xFF && markerPrefix != -1)
            {
                markerPrefix = stream.ReadByte();
            }
            if (markerPrefix == -1) break;

            int marker = stream.ReadByte();
            while (marker == 0xFF)
            {
                marker = stream.ReadByte();
            }
            if (marker == -1) break;

            // Marcadores SOF (Start of Frame) que contêm largura e altura:
            // C0..C3, C5..C7, C9..CB, CD..CF
            bool isSof = (marker >= 0xC0 && marker <= 0xC3) ||
                         (marker >= 0xC5 && marker <= 0xC7) ||
                         (marker >= 0xC9 && marker <= 0xCB) ||
                         (marker >= 0xCD && marker <= 0xCF);

            if (isSof)
            {
                // Pula tamanho do segmento (2 bytes) e precisão de bits (1 byte)
                if (stream.Read(lenBuffer) < 2) return null;
                int precision = stream.ReadByte();
                if (precision == -1) return null;

                Span<byte> dimBuffer = stackalloc byte[4];
                if (stream.Read(dimBuffer) < 4) return null;

                int height = BinaryPrimitives.ReadUInt16BigEndian(dimBuffer.Slice(0, 2));
                int width = BinaryPrimitives.ReadUInt16BigEndian(dimBuffer.Slice(2, 2));

                return new ImageDimensions(width, height);
            }
            else
            {
                // Pula outros segmentos (APPn, DQT, DHT, etc.)
                if (stream.Read(lenBuffer) < 2) return null;
                int length = BinaryPrimitives.ReadUInt16BigEndian(lenBuffer);
                if (length < 2) return null;

                long toSkip = length - 2;
                if (stream.CanSeek)
                {
                    stream.Seek(toSkip, SeekOrigin.Current);
                }
                else
                {
                    for (int i = 0; i < toSkip; i++)
                    {
                        if (stream.ReadByte() == -1) return null;
                    }
                }
            }
        }

        return null;
    }

    private static ImageDimensions? TryGetWebpDimensions(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[30];
        int read = stream.Read(buffer);
        if (read < 30) return null;

        // RIFF .... WEBP
        if (buffer[0] != 'R' || buffer[1] != 'I' || buffer[2] != 'F' || buffer[3] != 'F' ||
            buffer[8] != 'W' || buffer[9] != 'E' || buffer[10] != 'B' || buffer[11] != 'P')
        {
            return null;
        }

        // VP8 (Lossy)
        if (buffer[12] == 'V' && buffer[13] == 'P' && buffer[14] == '8' && buffer[15] == ' ')
        {
            int width = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(26, 2)) & 0x3FFF;
            int height = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(28, 2)) & 0x3FFF;
            return new ImageDimensions(width, height);
        }

        // VP8L (Lossless)
        if (buffer[12] == 'V' && buffer[13] == 'P' && buffer[14] == '8' && buffer[15] == 'L')
        {
            if (buffer[20] != 0x2F) return null;
            uint b0 = buffer[21];
            uint b1 = buffer[22];
            uint b2 = buffer[23];
            uint b3 = buffer[24];

            int width = (int)(1 + (b0 | ((b1 & 0x3F) << 8)));
            int height = (int)(1 + (((b1 & 0xC0) >> 6) | (b2 << 2) | ((b3 & 0xF) << 10)));
            return new ImageDimensions(width, height);
        }

        // VP8X (Extended)
        if (buffer[12] == 'V' && buffer[13] == 'P' && buffer[14] == '8' && buffer[15] == 'X')
        {
            int width = 1 + buffer[24] + (buffer[25] << 8) + (buffer[26] << 16);
            int height = 1 + buffer[27] + (buffer[28] << 8) + (buffer[29] << 16);
            return new ImageDimensions(width, height);
        }

        return null;
    }
}
