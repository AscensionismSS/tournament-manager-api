using BuildingBlocks.Domain;

namespace Tournaments.Domain.Entities;

public class DraftTeam : Entity<Guid>
{
    private readonly List<DraftPlayer> _starters = [];
    private readonly List<DraftPlayer> _reserves = [];

    public string Name { get; private set; }
    public Guid? CaptainId { get; private set; }
    public int PickOrder { get; internal set; }

    public IReadOnlyCollection<DraftPlayer> Starters => _starters.AsReadOnly();
    public IReadOnlyCollection<DraftPlayer> Reserves => _reserves.AsReadOnly();

    protected DraftTeam() { Name = "Time Sem Nome"; }

    public DraftTeam(string? name, int pickOrder = 0)
    {
        Id = Guid.NewGuid();
        Name = string.IsNullOrWhiteSpace(name) ? $"Time {pickOrder + 1}" : name.Trim();
        PickOrder = pickOrder;
    }

    public DraftTeam(Guid captainId, string captainName, string? externalUserId = null)
    {
        Id = Guid.NewGuid();
        CaptainId = captainId;
        Name = string.IsNullOrWhiteSpace(captainName) ? "Time Sem Nome" : $"Team {captainName.Trim()}";

        // O Capitão é automaticamente o 1º titular do time
        _starters.Add(new DraftPlayer(captainId, captainName, externalUserId));
    }

    public void Rename(string? newName)
    {
        Name = string.IsNullOrWhiteSpace(newName) ? $"Time {PickOrder + 1}" : newName.Trim();
    }

    internal void AssignCaptain(DraftPlayer player)
    {
        if (CaptainId.HasValue)
            throw new DomainException($"A equipe '{Name}' já possui um capitão atribuído.");

        CaptainId = player.Id;
        if (_starters.Count == 0)
        {
            _starters.Add(player);
        }
        else
        {
            _starters.Insert(0, player);
        }
    }

    internal DraftPlayer RemoveCaptain()
    {
        if (!CaptainId.HasValue)
            throw new DomainException($"A equipe '{Name}' não possui capitão.");

        var captain = _starters.FirstOrDefault(p => p.Id == CaptainId.Value);
        if (captain == null)
            throw new DomainException($"Capitão não encontrado nos titulares da equipe '{Name}'.");

        _starters.Remove(captain);
        CaptainId = null;
        return captain;
    }

    internal void AddStarter(DraftPlayer player)
    {
        if (_starters.Count >= 5)
            throw new DomainException($"A equipe {Name} já possui os 5 titulares preenchidos.");

        _starters.Add(player);
    }

    internal void ReplaceStarter(Guid oldPlayerId, DraftPlayer newPlayer)
    {
        int index = _starters.FindIndex(p => p.Id == oldPlayerId);
        if (index == -1)
            throw new DomainException("Jogador a ser substituído não encontrado nos titulares.");

        _starters[index] = newPlayer;

        // Se o substituído era o Capitão (índice 0), atualiza o Capitão da equipe
        if (index == 0)
        {
            CaptainId = newPlayer.Id;
        }
    }

    internal void AddReserve(DraftPlayer player)
    {
        if (_reserves.Count >= 2)
            throw new DomainException($"A equipe {Name} já possui o limite máximo de 2 reservas.");

        _reserves.Add(player);
    }

    internal DraftPlayer RemoveReserve(Guid playerId)
    {
        var reserve = _reserves.FirstOrDefault(p => p.Id == playerId)
            ?? throw new DomainException($"Jogador reserva não encontrado na equipe '{Name}'.");

        _reserves.Remove(reserve);
        return reserve;
    }
}
