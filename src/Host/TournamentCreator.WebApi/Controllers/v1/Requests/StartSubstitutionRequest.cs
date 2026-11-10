namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record StartSubstitutionRequest(Guid PlayerId, Guid TeamId, int ExpectedVersion);
