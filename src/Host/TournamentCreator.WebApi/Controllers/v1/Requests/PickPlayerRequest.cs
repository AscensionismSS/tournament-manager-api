namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record PickPlayerRequest(Guid PlayerId, int ExpectedVersion);
