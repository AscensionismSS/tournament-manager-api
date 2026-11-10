// Package dto contains all pure data transfer objects (DTOs) for player-level data.
// No logic, no methods — only fields and json tags.
package dto

// PlayerInventory DTO aggregates all items carried by a player.
type PlayerInventory struct {
	MainItems    []string `json:"itens_principais"`
	Backpack     []string `json:"mochila"`
	NeutralItem  string   `json:"item_neutro"`
	NeutralItems []string `json:"itens_neutros"`
	JungleBuff   string   `json:"buff_jungle"`
	JungleBuffs  []string `json:"buffs_jungle"`
}

// PlayerUpgrades DTO aggregates permanent buffs and Aghanim upgrades.
type PlayerUpgrades struct {
	HasScepter bool `json:"aghanim_cetro"`
	HasShard   bool `json:"aghanim_shard"`
}

// PlayerStats is the DTO for a single player's end-of-game statistics.
// Identifiers (SteamID, HeroID) come first for easier back-end processing.
type PlayerStats struct {
	SteamID         uint64          `json:"steam_id"`
	Name            string          `json:"nome"`
	HeroID          uint32          `json:"hero_id"`
	Hero            string          `json:"heroi"`
	Kills           int32           `json:"kills"`
	Deaths          int32           `json:"deaths"`
	Assists         int32           `json:"assists"`
	NetWorth        int32           `json:"net_worth"`
	GPM             int32           `json:"gpm"`
	XPM             int32           `json:"xpm"`
	Inventory       PlayerInventory `json:"inventario"`
	Upgrades        PlayerUpgrades  `json:"upgrades"`
	ObsPurchased    int             `json:"wards_obs_compradas"`
	SenPurchased    int             `json:"wards_sen_compradas"`
	ObsDestroyed    int             `json:"wards_obs_destruidas"`
	StructureDamage int64           `json:"dano_estruturas"`
	HeroDamage      int64           `json:"dano_herois"`
	HeroHealing     int64           `json:"cura"`
}

// PlayersReport is the top-level DTO for all player data and match events combined.
type PlayersReport struct {
	Radiant        []PlayerStats      `json:"radiant"`
	Dire           []PlayerStats      `json:"dire"`
	Others         []PlayerStats      `json:"outros,omitempty"`
	FirstBlood     *FirstBloodEvent   `json:"first_blood,omitempty"`
	RoshanKills    []RoshanKillEvent  `json:"roshan_derrotado"`
	TormentorKills []TormentorKillEvent `json:"tormentor_derrotado"`
	Rampages       []RampageEvent     `json:"rampages"`
}
