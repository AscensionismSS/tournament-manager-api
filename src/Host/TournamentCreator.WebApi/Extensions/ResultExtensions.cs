using BuildingBlocks.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TournamentCreator.WebApi.Extensions;

public static class ResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(
        this Result<T> result,
        string errorTitle = "Erro na Operação",
        int failureStatusCode = StatusCodes.Status400BadRequest)
    {
        if (result.IsFailure)
        {
            var problemDetails = new ProblemDetails
            {
                Title = errorTitle,
                Detail = result.Error,
                Status = failureStatusCode
            };

            return failureStatusCode switch
            {
                StatusCodes.Status404NotFound => new NotFoundObjectResult(problemDetails),
                StatusCodes.Status409Conflict => new ConflictObjectResult(problemDetails),
                _ => new BadRequestObjectResult(problemDetails)
            };
        }

        return new OkObjectResult(result.Value);
    }

    public static IActionResult ToActionResult(
        this Result result,
        string errorTitle = "Erro na Operação",
        int failureStatusCode = StatusCodes.Status400BadRequest)
    {
        if (result.IsFailure)
        {
            var problemDetails = new ProblemDetails
            {
                Title = errorTitle,
                Detail = result.Error,
                Status = failureStatusCode
            };

            return failureStatusCode switch
            {
                StatusCodes.Status404NotFound => new NotFoundObjectResult(problemDetails),
                StatusCodes.Status409Conflict => new ConflictObjectResult(problemDetails),
                _ => new BadRequestObjectResult(problemDetails)
            };
        }

        return new OkResult();
    }
}
