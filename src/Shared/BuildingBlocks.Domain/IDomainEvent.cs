namespace BuildingBlocks.Domain;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
