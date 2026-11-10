using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Replay;

namespace Tournaments.Infrastructure.ReplayParsing;

public class GoCliReplayParserService : IDotaReplayParserService
{
    private static readonly byte[] ExpectedMagicBytes = Encoding.ASCII.GetBytes("PBDEMS2\0");
    private const long MinReplaySizeBytes = 500 * 1024; // 500 KB
    private const long MaxReplaySizeBytes = 350 * 1024 * 1024; // 350 MB

    private readonly string _toolsDir;
    private readonly string _executablePath;
    private readonly ILogger<GoCliReplayParserService> _logger;

    private static readonly string[] CandidateBinaryNames = OperatingSystem.IsWindows()
        ? ["parsegodota.exe", "parsegodota", "dota-parser.exe", "dota-parser"]
        : ["parsegodota", "dota-parser", "parsegodota.exe"];

    public GoCliReplayParserService(IConfiguration configuration, ILogger<GoCliReplayParserService> logger)
    {
        _logger = logger;

        // Procura pasta tools/ParseGoDota a partir do diretório base da aplicação ou raiz da solution
        var baseDir = AppContext.BaseDirectory;
        var configuredExe = configuration["ReplayParser:ExecutablePath"];

        if (!string.IsNullOrWhiteSpace(configuredExe) && File.Exists(configuredExe))
        {
            _executablePath = Path.GetFullPath(configuredExe);
            _toolsDir = Path.GetDirectoryName(_executablePath)!;
        }
        else
        {
            // Busca subindo diretórios até encontrar 'tools/ParseGoDota/<binário>'
            var foundExe = FindExecutableRecursively(baseDir) ?? FindExecutableRecursively(Directory.GetCurrentDirectory());
            if (foundExe != null)
            {
                _executablePath = foundExe;
                _toolsDir = Path.GetDirectoryName(foundExe)!;
            }
            else
            {
                // Fallback default relativo
                var defaultBinaryName = OperatingSystem.IsWindows() ? "parsegodota.exe" : "parsegodota";
                _toolsDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "tools", "ParseGoDota"));
                _executablePath = Path.Combine(_toolsDir, defaultBinaryName);
            }
        }

        EnsureUnixExecutablePermission(_executablePath);
    }

    private static void EnsureUnixExecutablePermission(string filePath)
    {
        if (!OperatingSystem.IsWindows() && File.Exists(filePath))
        {
            try
            {
                File.SetUnixFileMode(filePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            catch
            {
                // Silencia se não for suportado pelo sistema de arquivos
            }
        }
    }

    private static string? FindExecutableRecursively(string startDir)
    {
        var current = new DirectoryInfo(startDir);
        for (int i = 0; i < 6 && current != null; i++)
        {
            foreach (var binaryName in CandidateBinaryNames)
            {
                var candidate = Path.Combine(current.FullName, "tools", "ParseGoDota", binaryName);
                if (File.Exists(candidate))
                    return candidate;
            }

            current = current.Parent;
        }
        return null;
    }

    public async Task<ParseGoDotaOutputDto> ParseReplayAsync(Stream replayStream, string originalFileName, CancellationToken cancellationToken = default)
    {
        if (replayStream == null)
            throw new DomainException("Stream de replay não pode ser nulo.");

        // 1. Validação física do arquivo (Segurança em Profundidade)
        ValidateReplayStream(replayStream, originalFileName);

        if (!File.Exists(_executablePath))
            throw new DomainException($"Executável do parser de replay não encontrado em: {_executablePath}");

        var runId = Guid.NewGuid().ToString("N");
        var isolatedRunDir = Path.Combine(_toolsDir, "runs", runId);
        var replaysDir = Path.Combine(isolatedRunDir, "replays");
        var convertidoDir = Path.Combine(isolatedRunDir, "convertido");
        Directory.CreateDirectory(replaysDir);
        Directory.CreateDirectory(convertidoDir);

        var tempFileName = $"{Guid.NewGuid():N}.dem";
        var tempFilePath = Path.Combine(replaysDir, tempFileName);

        try
        {
            // 2. Grava o stream no disco na pasta isolada
            replayStream.Position = 0;
            await using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await replayStream.CopyToAsync(fileStream, cancellationToken);
            }

            // 3. Executa o parsegodota.exe com WorkingDirectory isolado
            var relativeReplayPath = Path.Combine("replays", tempFileName);
            var (exitCode, stdOut, stdErr) = await RunParserProcessAsync(isolatedRunDir, relativeReplayPath, cancellationToken);

            if (exitCode != 0)
            {
                var errDetail = !string.IsNullOrWhiteSpace(stdErr) ? stdErr : stdOut;
                _logger.LogError("ParseGoDota falhou com código {ExitCode}. Erro: {Error}", exitCode, errDetail);
                throw new DomainException($"O parser de replay falhou ao analisar o arquivo: {errDetail.Trim()}");
            }

            // 4. Localiza o JSON gerado na pasta convertido/ isolada
            var generatedJsonFile = FindGeneratedJsonFile(convertidoDir, tempFileName);
            if (generatedJsonFile == null || !File.Exists(generatedJsonFile))
            {
                throw new DomainException("O parser concluiu mas nenhum arquivo JSON de resultado foi encontrado em 'convertido/'.");
            }

            try
            {
                var jsonContent = await File.ReadAllTextAsync(generatedJsonFile, cancellationToken);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogError("O arquivo JSON de resultado gerado pelo parser em {Path} está vazio.", generatedJsonFile);
                    throw new DomainException("O parser concluiu a execução mas o arquivo JSON gerado está vazio.");
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                };

                ParseGoDotaOutputDto? parsedResult;
                try
                {
                    parsedResult = JsonSerializer.Deserialize<ParseGoDotaOutputDto>(jsonContent, options);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Erro ao desserializar JSON do parser. Conteúdo: {Json}", jsonContent);
                    throw new DomainException($"O JSON gerado pelo parser possui formato inválido: {ex.Message}");
                }

                if (parsedResult == null)
                {
                    _logger.LogError("Deserialização de ParseGoDotaOutputDto retornou nulo. JSON: {Json}", jsonContent);
                    throw new DomainException("O JSON gerado pelo parser não pôde ser interpretado pelo servidor.");
                }

                if (parsedResult.Sumario == null)
                {
                    _logger.LogError("ParseGoDotaOutputDto.Sumario é nulo. JSON: {Json}", jsonContent);
                    throw new DomainException("O JSON gerado pelo parser não contém os dados de sumário da partida (placar/duração/vencedor).");
                }

                if (parsedResult.JogadoresEEventos == null)
                {
                    _logger.LogError("ParseGoDotaOutputDto.JogadoresEEventos é nulo. JSON: {Json}", jsonContent);
                    throw new DomainException("O JSON gerado pelo parser não contém os dados dos jogadores e eventos.");
                }

                return parsedResult;
            }
            finally
            {
                // Limpeza do arquivo JSON temporário
                try { File.Delete(generatedJsonFile); } catch { /* silencia */ }
            }
        }
        finally
        {
            // Limpeza completa do diretório isolado por GUID
            try
            {
                if (Directory.Exists(isolatedRunDir))
                {
                    Directory.Delete(isolatedRunDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao limpar diretório temporário isolado {Directory}", isolatedRunDir);
            }
        }
    }

    private static void ValidateReplayStream(Stream stream, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".dem", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("O arquivo de replay deve possuir extensão '.dem'.");

        if (stream.Length < MinReplaySizeBytes)
            throw new DomainException($"O arquivo de replay é muito pequeno ({stream.Length / 1024} KB). Mínimo exigido: 500 KB.");

        if (stream.Length > MaxReplaySizeBytes)
            throw new DomainException($"O arquivo de replay excede o limite máximo permitido de 350 MB.");

        // Valida Magic Bytes (PBDEMS2\0)
        stream.Position = 0;
        var header = new byte[8];
        var bytesRead = stream.Read(header, 0, 8);
        if (bytesRead < 8 || !header.SequenceEqual(ExpectedMagicBytes))
        {
            throw new DomainException("O arquivo enviado não é um replay válido do Dota 2 Source 2 (assinatura PBDEMS2 não encontrada).");
        }
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunParserProcessAsync(string workingDirectory, string argReplayPath, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _executablePath,
            Arguments = argReplayPath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var stdOutBuilder = new StringBuilder();
        var stdErrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdOutBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stdErrBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
            process.WaitForExit();
            return (process.ExitCode, stdOutBuilder.ToString(), stdErrBuilder.ToString());
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* silencia */ }
            if (timeoutCts.IsCancellationRequested)
            {
                throw new DomainException("Tempo limite excedido (45 segundos) ao processar o replay. Processo abortado por segurança.");
            }
            throw;
        }
    }

    private static string? FindGeneratedJsonFile(string convertidoDir, string tempFileName)
    {
        // 1. Procura pelo nome original sem extensão
        var byName = Path.Combine(convertidoDir, Path.GetFileNameWithoutExtension(tempFileName) + ".json");
        if (File.Exists(byName)) return byName;

        // 2. Como o diretório é 100% isolado por GUID para esta execução, o arquivo JSON presente pertence a este replay
        var dirInfo = new DirectoryInfo(convertidoDir);
        var recentJson = dirInfo.GetFiles("*.json")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        return recentJson?.FullName;
    }
}
