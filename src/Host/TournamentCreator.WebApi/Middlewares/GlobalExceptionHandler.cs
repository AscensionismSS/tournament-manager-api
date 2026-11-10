using BuildingBlocks.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TournamentCreator.WebApi.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(exception, "Exceção interceptada pelo GlobalExceptionHandler: {Message}", exception.Message);

        ProblemDetails problemDetails;

        if (exception is ValidationException valEx)
        {
            var errors = valEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            problemDetails = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Erro de Validação de Entrada",
                Type = "https://tournaments.local/errors/validation",
                Detail = "Um ou mais campos enviados na requisição são inválidos.",
                Instance = httpContext.Request.Path
            };
        }
        else if (exception is DbUpdateConcurrencyException)
        {
            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflito de Concorrência",
                Type = "https://tournaments.local/errors/concurrency",
                Detail = "Os dados foram alterados por outra ação simultânea. Por favor, recarregue a tela e tente novamente.",
                Instance = httpContext.Request.Path
            };
        }
        else if (exception.Message.Contains("Conflito de versão no Draft", StringComparison.OrdinalIgnoreCase))
        {
            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflito de Concorrência no Draft",
                Type = "https://tournaments.local/errors/draft-concurrency",
                Detail = "O estado do Draft foi alterado por outra ação simultânea. A tela foi sincronizada com a versão mais recente.",
                Instance = httpContext.Request.Path
            };
        }
        else if (exception is DomainException domainEx)
        {
            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Regra de Torneio Violada",
                Type = "https://tournaments.local/errors/domain-rule",
                Detail = domainEx.Message,
                Instance = httpContext.Request.Path
            };
        }
        else
        {
            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro Interno no Servidor",
                Type = "https://tournaments.local/errors/internal-server-error",
                Detail = "Ocorreu uma falha inesperada no processamento da solicitação.",
                Instance = httpContext.Request.Path
            };
        }

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
