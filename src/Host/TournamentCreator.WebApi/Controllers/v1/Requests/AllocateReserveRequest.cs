namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record AllocateReserveRequest(Guid TeamId, Guid PlayerId, int ExpectedVersion);
