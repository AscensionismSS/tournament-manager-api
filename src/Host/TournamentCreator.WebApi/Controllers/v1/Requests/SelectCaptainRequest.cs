namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record SelectCaptainRequest(Guid PlayerId, int ExpectedVersion, Guid? TeamId = null);
