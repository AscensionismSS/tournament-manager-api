namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record ConfirmSubstitutionRequest(Guid NewPlayerId, int ExpectedVersion);
