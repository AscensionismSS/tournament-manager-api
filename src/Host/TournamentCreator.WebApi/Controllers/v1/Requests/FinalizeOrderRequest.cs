using Tournaments.Domain.Enums;

namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record FinalizeOrderRequest(PickOrderType OrderType, IReadOnlyList<Guid>? ManualOrderIds, int ExpectedVersion);
