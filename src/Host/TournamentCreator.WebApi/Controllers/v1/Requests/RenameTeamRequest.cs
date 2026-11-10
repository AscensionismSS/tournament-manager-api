namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record RenameTeamRequest(string? NewName, int ExpectedVersion, Guid? TeamId = null);
