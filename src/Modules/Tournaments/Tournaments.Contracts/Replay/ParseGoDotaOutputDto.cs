using System.Text.Json.Serialization;

namespace Tournaments.Contracts.Replay;

public class ParseGoDotaOutputDto
{
    [JsonPropertyName("sumario")]
    public ParseGoDotaSummaryDto? Sumario { get; set; }

    [JsonPropertyName("jogadores_e_eventos")]
    public ParseGoDotaPlayersAndEventsDto? JogadoresEEventos { get; set; }

    [JsonPropertyName("radiant_bans")]
    public List<int> RadiantBans { get; set; } = [];

    [JsonPropertyName("dire_bans")]
    public List<int> DireBans { get; set; } = [];

    [JsonPropertyName("first_pick")]
    public string? FirstPick { get; set; }
}

public class ParseGoDotaSummaryDto
{
    [JsonPropertyName("match_id")]
    public long MatchId { get; set; }

    [JsonPropertyName("servidor")]
    public string? Servidor { get; set; }

    [JsonPropertyName("cliente")]
    public string? Cliente { get; set; }

    [JsonPropertyName("mapa")]
    public string? Mapa { get; set; }

    [JsonPropertyName("diretorio")]
    public string? Diretorio { get; set; }

    [JsonPropertyName("duracao_segundos")]
    public double DuracaoSegundos { get; set; }

    [JsonPropertyName("duracao_formatada")]
    public string? DuracaoFormatada { get; set; }

    [JsonPropertyName("data_hora_inicio")]
    public string? DataHoraInicio { get; set; }

    [JsonPropertyName("data_hora_fim")]
    public string? DataHoraFim { get; set; }

    [JsonPropertyName("vencedor")]
    public string? Vencedor { get; set; } // "Radiant" ou "Dire"

    [JsonPropertyName("time_radiant_tag")]
    public string? TimeRadiantTag { get; set; }

    [JsonPropertyName("time_dire_tag")]
    public string? TimeDireTag { get; set; }

    [JsonPropertyName("placar_radiant")]
    public int PlacarRadiant { get; set; }

    [JsonPropertyName("placar_dire")]
    public int PlacarDire { get; set; }

    [JsonPropertyName("radiant_bans")]
    public List<int>? RadiantBans { get; set; }

    [JsonPropertyName("dire_bans")]
    public List<int>? DireBans { get; set; }

    [JsonPropertyName("first_pick")]
    public string? FirstPick { get; set; }

    [JsonPropertyName("picks_bans")]
    public List<ParseGoDotaPickBanDto>? PicksBans { get; set; }
}

public class ParseGoDotaPickBanDto
{
    [JsonPropertyName("acao")]
    public string? Acao { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("hero_id")]
    public int HeroId { get; set; }

    [JsonPropertyName("heroi")]
    public string? Heroi { get; set; }
}

public class ParseGoDotaPlayersAndEventsDto
{
    [JsonPropertyName("radiant")]
    public List<ParseGoDotaPlayerDto> Radiant { get; set; } = [];

    [JsonPropertyName("dire")]
    public List<ParseGoDotaPlayerDto> Dire { get; set; } = [];

    [JsonPropertyName("roshan_derrotado")]
    public List<ParseGoDotaRoshanKillDto> RoshanKills { get; set; } = [];
}

public class ParseGoDotaRoshanKillDto
{
    [JsonPropertyName("tempo_segundos")]
    public double TempoSegundos { get; set; }

    [JsonPropertyName("tempo_formatado")]
    public string? TempoFormatado { get; set; }

    [JsonPropertyName("derrotado_por")]
    public ParseGoDotaPlayerLinkDto? DerrotadoPor { get; set; }

    [JsonPropertyName("foi_roubado")]
    public bool FoiRoubado { get; set; }

    [JsonPropertyName("aegis_pego_por")]
    public ParseGoDotaPlayerLinkDto? AegisPegoPor { get; set; }
}

public class ParseGoDotaPlayerLinkDto
{
    [JsonPropertyName("steam_id")]
    public long SteamId { get; set; }

    [JsonPropertyName("player_id")]
    public int PlayerId { get; set; }

    [JsonPropertyName("hero_id")]
    public int HeroId { get; set; }

    [JsonPropertyName("heroi")]
    public string? Heroi { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }
}

public class ParseGoDotaPlayerDto
{
    [JsonPropertyName("steam_id")]
    public long SteamId { get; set; } // SteamID64

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("hero_id")]
    public int HeroId { get; set; }

    [JsonPropertyName("heroi")]
    public string? Heroi { get; set; }

    [JsonPropertyName("kills")]
    public int Kills { get; set; }

    [JsonPropertyName("deaths")]
    public int Deaths { get; set; }

    [JsonPropertyName("assists")]
    public int Assists { get; set; }

    [JsonPropertyName("net_worth")]
    public int NetWorth { get; set; }

    [JsonPropertyName("gpm")]
    public int Gpm { get; set; }

    [JsonPropertyName("xpm")]
    public int Xpm { get; set; }

    [JsonPropertyName("inventario")]
    public ParseGoDotaInventoryDto? Inventario { get; set; }

    [JsonPropertyName("upgrades")]
    public ParseGoDotaUpgradesDto? Upgrades { get; set; }

    [JsonPropertyName("wards_obs_compradas")]
    public int WardsObsCompradas { get; set; }

    [JsonPropertyName("wards_sen_compradas")]
    public int WardsSenCompradas { get; set; }

    [JsonPropertyName("wards_obs_destruidas")]
    public int WardsObsDestruidas { get; set; }

    [JsonPropertyName("dano_estruturas")]
    public int DanoEstruturas { get; set; }

    [JsonPropertyName("dano_herois")]
    public long DanoHerois { get; set; }

    [JsonPropertyName("cura")]
    public long Cura { get; set; }
}

public class ParseGoDotaInventoryDto
{
    [JsonPropertyName("itens_principais")]
    public List<string> ItensPrincipais { get; set; } = [];

    [JsonPropertyName("mochila")]
    public List<string> Mochila { get; set; } = [];

    [JsonPropertyName("item_neutro")]
    public string? ItemNeutro { get; set; }

    [JsonPropertyName("itens_neutros")]
    public List<string> ItensNeutros { get; set; } = [];

    [JsonPropertyName("buff_jungle")]
    public string? BuffJungle { get; set; }

    [JsonPropertyName("buffs_jungle")]
    public List<string> BuffsJungle { get; set; } = [];
}

public class ParseGoDotaUpgradesDto
{
    [JsonPropertyName("aghanim_cetro")]
    public bool AghanimCetro { get; set; }

    [JsonPropertyName("aghanim_shard")]
    public bool AghanimShard { get; set; }
}
