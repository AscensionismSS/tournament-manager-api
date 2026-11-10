namespace Tournaments.Domain.Enums;

public enum SeriesFormat
{
    Bo1 = 1,
    Bo2 = 2,
    Bo3 = 3,
    Bo5 = 5
}

public enum TournamentType
{
    DirectPlayoffs = 1,
    GroupStageAndPlayoffs = 2,
    GroupStageDirectFinal = 3
}

public enum PlayoffFormat
{
    SingleElimination = 1,
    DoubleElimination = 2
}

public enum DraftState
{
    SelectingCaptains = 1,
    CaptainOrderSelection = 2,
    DraftingStarters = 3,
    Substituting = 4,
    DraftingSubstitutes = 5,
    Completed = 6
}

public enum PickOrderType
{
    Manual = 1,
    Randomized = 2
}

public enum MatchSeriesStatus
{
    Pending = 1,
    ReadyToPlay = 2,
    InProgress = 3,
    Completed = 4,
    Canceled = 5
}

public enum ResultSource
{
    Manual = 1,
    ReplayParser = 2,
    Walkover = 3
}

public enum TournamentStatus
{
    Draft = 1,
    RegistrationOpen = 2,
    Drafting = 3,
    GroupStage = 4,
    Playoffs = 5,
    Completed = 6,
    Canceled = 7
}
