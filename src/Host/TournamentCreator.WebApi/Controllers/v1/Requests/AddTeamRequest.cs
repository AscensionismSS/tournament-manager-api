namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record AddTeamRequest(Guid? TeamId, string TeamName);
