// Package dto contains all pure data transfer objects (DTOs) for match-level data.
// No logic, no methods — only fields and json tags.
package dto

// PlayerLink identifies the player and hero involved in a match event.
type PlayerLink struct {
	SteamID  uint64 `json:"steam_id"`
	PlayerID int    `json:"player_id"`
	HeroID   uint32 `json:"hero_id"`
	HeroName string `json:"heroi"`
	Team     string `json:"time"` // "Radiant" | "Dire" | ""
}

// PickBanData represents a single pick or ban action during the draft.
type PickBanData struct {
	Action string `json:"acao"`    // "PICK" | "BAN"
	Team   string `json:"time"`    // "Radiant" | "Dire"
	HeroID uint32 `json:"hero_id"`
	Hero   string `json:"heroi"`
}

// MatchSummaryData is the top-level match metadata DTO.
type MatchSummaryData struct {
	MatchID           uint64        `json:"match_id"`
	Server            string        `json:"servidor"`
	Client            string        `json:"cliente"`
	Map               string        `json:"mapa"`
	GameDirectory     string        `json:"diretorio"`
	DurationSeconds   float32       `json:"duracao_segundos"`
	DurationFormatted string        `json:"duracao_formatada"`
	DateTimeStart     string        `json:"data_hora_inicio,omitempty"`
	DateTimeEnd       string        `json:"data_hora_fim,omitempty"`
	Winner            string        `json:"vencedor"`
	TeamRadiantTag    string        `json:"time_radiant_tag,omitempty"`
	TeamDireTag       string        `json:"time_dire_tag,omitempty"`
	ScoreRadiant      int           `json:"placar_radiant"`
	ScoreDire         int           `json:"placar_dire"`
	PicksBans         []PickBanData `json:"picks_bans,omitempty"`
}

// FirstBloodEvent contains data about the first hero kill of the match.
type FirstBloodEvent struct {
	TimeSeconds float32    `json:"tempo_segundos"`
	TimeString  string     `json:"tempo_formatado"`
	Killer      PlayerLink `json:"matador"`
	Victim      PlayerLink `json:"vitima"`
}

// RoshanKillEvent contains data about a single Roshan kill.
type RoshanKillEvent struct {
	TimeSeconds float32    `json:"tempo_segundos"`
	TimeString  string     `json:"tempo_formatado"`
	Killer      PlayerLink `json:"derrotado_por"`
	IsStolen    bool       `json:"foi_roubado"`
	TakenBy     PlayerLink `json:"aegis_pego_por"`
}

// TormentorKillEvent contains data about a single Tormentor kill.
type TormentorKillEvent struct {
	TimeSeconds float32    `json:"tempo_segundos"`
	TimeString  string     `json:"tempo_formatado"`
	Killer      PlayerLink `json:"derrotado_por"`
}

// RampageEvent contains data about a Rampage (quintuple kill).
type RampageEvent struct {
	TimeSeconds float32    `json:"tempo_segundos"`
	TimeString  string     `json:"tempo_formatado"`
	Killer      PlayerLink `json:"realizado_por"`
}
