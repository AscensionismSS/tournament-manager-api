package analyzer

import (
	"fmt"
	"strings"

	"parsegodota/pkg/dto"

	"github.com/dotabuff/manta"
	"github.com/dotabuff/manta/dota"
)

// --- Estruturas internas temporárias usadas durante o processamento do stream de eventos ---

type roshanKill struct {
	killer string
	time   float32
}

type aegisPickup struct {
	parent uint32
	time   float32
}

type aegisCombatLogPickup struct {
	targetHero string
	time       float32
}

type tormentorKill struct {
	killer string
	time   float32
}

type multikill struct {
	killer string
	tier   uint32
	time   float32
}

type combatLogEntry struct {
	steamID  uint64
	playerID int
	heroID   uint32
	heroName string
	team     string
}

// PlayersAnalyzer é o "motor" ou processador responsável por monitorar o histórico de combate
// (combat log) e as modificações nos heróis (modifiers) para construir a lista de status e eventos dos jogadores.
//
// 📌 POR QUE ESSA STRUCT ESTÁ AQUI E NÃO NO PACOTE DTO?
// Esta struct não é o modelo final que vira JSON. Ela armazena o ESTADO INTERNO de leitura dos eventos
// (como quantidade de sentries compradas acumuladas, buffs ativos, torres destruídas, etc.) conforme o arquivo
// .dem é lido. No final do processamento, chamamos `GetPlayersReport` que converte esse estado
// acumulado no DTO estruturado final (`dto.PlayersReport`).
type PlayersAnalyzer struct {
	// Dicionários para rastrear buffs de scepter/shard ativos por herói
	activeModifiers map[uint32]map[string]bool

	// Acumuladores de danos e ações de visão (wards) por nome de combate do herói
	structureDamage   map[string]int64
	heroDamage        map[string]int64
	heroHealing       map[string]int64
	obsWardsPurchased map[string]int
	senWardsPurchased map[string]int
	obsWardsDestroyed map[string]int

	// Listas de eventos capturados em tempo real durante o jogo
	roshanKills           []roshanKill
	aegisPickups          []aegisPickup
	aegisCombatLogPickups []aegisCombatLogPickup
	tormentorKills        []tormentorKill
	multikills            []multikill

	// Histórico de itens neutros e encantamentos/buffs por Entity Index do herói real (1 entrada por Tier)
	neutralItemsHistory map[int32][]string
	jungleBuffsHistory  map[int32][]string
	lastNeutralHandle   map[int32]uint32
	lastBuffHandle      map[int32]uint32

	// Dados do First Blood
	firstBloodKiller string
	firstBloodVictim string
	firstBloodTime   float32
}

// Garante em tempo de compilação que PlayersAnalyzer implementa a interface Analyzer.
var _ Analyzer = (*PlayersAnalyzer)(nil)

// NewPlayersAnalyzer inicializa o processador de jogadores.
func NewPlayersAnalyzer() *PlayersAnalyzer {
	return &PlayersAnalyzer{
		activeModifiers:   make(map[uint32]map[string]bool),
		structureDamage:   make(map[string]int64),
		heroDamage:        make(map[string]int64),
		heroHealing:       make(map[string]int64),
		obsWardsPurchased: make(map[string]int),
		senWardsPurchased:   make(map[string]int),
		obsWardsDestroyed:   make(map[string]int),
		neutralItemsHistory: make(map[int32][]string),
		jungleBuffsHistory:  make(map[int32][]string),
		lastNeutralHandle:   make(map[int32]uint32),
		lastBuffHandle:      make(map[int32]uint32),
	}
}

// Name retorna o identificador do analisador.
func (p *PlayersAnalyzer) Name() string {
	return "PlayersAnalyzer"
}

// RegisterCallbacks escuta os eventos do Combat Log do Dota 2 para alimentar os acumuladores temporários.
func (p *PlayersAnalyzer) RegisterCallbacks(parser *manta.Parser) {
	parser.OnModifierTableEntry(func(m *dota.CDOTAModifierBuffTableEntry) error {
		if m.Parent == nil || m.ModifierClass == nil || m.EntryType == nil {
			return nil
		}
		name, ok := parser.LookupStringByIndex("ModifierNames", m.GetModifierClass())
		if !ok {
			return nil
		}
		parent := m.GetParent()
		if p.activeModifiers[parent] == nil {
			p.activeModifiers[parent] = make(map[string]bool)
		}
		switch m.GetEntryType() {
		case dota.DOTA_MODIFIER_ENTRY_TYPE_DOTA_MODIFIER_ENTRY_TYPE_ACTIVE:
			p.activeModifiers[parent][name] = true
			if name == "modifier_item_aegis" {
				p.aegisPickups = append(p.aegisPickups, aegisPickup{
					parent: parent,
					time:   float32(parser.Tick) / 30.0,
				})
			}
		case dota.DOTA_MODIFIER_ENTRY_TYPE_DOTA_MODIFIER_ENTRY_TYPE_REMOVED:
			p.activeModifiers[parent][name] = false
		}
		return nil
	})

	parser.Callbacks.OnCMsgDOTACombatLogEntry(func(entry *dota.CMsgDOTACombatLogEntry) error {
		if entry.Type == nil {
			return nil
		}

		lookup := func(idx *uint32) string {
			if idx == nil {
				return ""
			}
			v, ok := parser.LookupStringByIndex("CombatLogNames", int32(*idx))
			if !ok {
				return ""
			}
			return v
		}

		switch entry.GetType() {

		case dota.DOTA_COMBATLOG_TYPES_DOTA_COMBATLOG_DAMAGE:
			attacker := lookup(entry.AttackerName)
			if strings.HasPrefix(attacker, "npc_dota_hero_") && entry.Value != nil {
				if entry.IsTargetBuilding != nil && *entry.IsTargetBuilding {
					p.structureDamage[attacker] += int64(*entry.Value)
				}
				if entry.IsTargetHero != nil && *entry.IsTargetHero && (entry.IsTargetIllusion == nil || !*entry.IsTargetIllusion) {
					p.heroDamage[attacker] += int64(*entry.Value)
				}
			}

		case dota.DOTA_COMBATLOG_TYPES_DOTA_COMBATLOG_HEAL:
			attacker := lookup(entry.AttackerName)
			if strings.HasPrefix(attacker, "npc_dota_hero_") && entry.Value != nil {
				p.heroHealing[attacker] += int64(*entry.Value)
			}

		case dota.DOTA_COMBATLOG_TYPES_DOTA_COMBATLOG_PURCHASE:
			target := lookup(entry.TargetName)
			if strings.HasPrefix(target, "npc_dota_hero_") {
				itemName, ok := parser.LookupStringByIndex("CombatLogNames", int32(entry.GetValue()))
				if ok {
					switch itemName {
					case "item_ward_observer":
						p.obsWardsPurchased[target]++
					case "item_ward_sentry":
						p.senWardsPurchased[target]++
					}
				}
			}

		case dota.DOTA_COMBATLOG_TYPES_DOTA_COMBATLOG_DEATH:
			target := lookup(entry.TargetName)
			attacker := lookup(entry.AttackerName)

			if target == "npc_dota_observer_wards" && strings.HasPrefix(attacker, "npc_dota_hero_") {
				p.obsWardsDestroyed[attacker]++
			}
			if target == "npc_dota_roshan" {
				p.roshanKills = append(p.roshanKills, roshanKill{killer: summonToHero(attacker), time: entry.GetTimestamp()})
			}
			if target == "npc_dota_miniboss" {
				p.tormentorKills = append(p.tormentorKills, tormentorKill{killer: summonToHero(attacker), time: entry.GetTimestamp()})
			}

			isHeroVictim := entry.IsTargetHero != nil && *entry.IsTargetHero &&
				(entry.IsTargetIllusion == nil || !*entry.IsTargetIllusion)
			if isHeroVictim && strings.HasPrefix(attacker, "npc_dota_hero_") && p.firstBloodKiller == "" {
				p.firstBloodKiller = attacker
				p.firstBloodVictim = target
				p.firstBloodTime = entry.GetTimestamp()
			}

		case dota.DOTA_COMBATLOG_TYPES_DOTA_COMBATLOG_MULTIKILL:
			attacker := lookup(entry.AttackerName)
			tier := entry.GetValue()
			if strings.HasPrefix(attacker, "npc_dota_hero_") && tier == 5 {
				p.multikills = append(p.multikills, multikill{killer: attacker, tier: tier, time: entry.GetTimestamp()})
			}

		case dota.DOTA_COMBATLOG_TYPES_DOTA_COMBATLOG_MODIFIER_ADD:
			inflictor := lookup(entry.InflictorName)
			if inflictor == "modifier_item_aegis" {
				target := lookup(entry.TargetName)
				p.aegisCombatLogPickups = append(p.aegisCombatLogPickups, aegisCombatLogPickup{
					targetHero: target,
					time:       entry.GetTimestamp(),
				})
			}
		}

		return nil
	})

	parser.OnEntity(func(e *manta.Entity, op manta.EntityOp) error {
		if e == nil {
			return nil
		}
		className := e.GetClassName()
		if !strings.HasPrefix(className, "CDOTA_Unit_Hero_") {
			return nil
		}
		if isIllusion, ok := e.GetBool("m_bIsIllusion"); ok && isIllusion {
			return nil
		}

		entityIdx := e.GetIndex()
		nHandle := itemHandle(e, 16)
		bHandle := itemHandle(e, 17)
		rawItem := itemName(e, 16, parser)
		rawBuff := itemName(e, 17, parser)

		// Quando o jogador equipa/evolui um Tier na Jungle (mudou o handle do neutro ou do buff)
		// e ambos os nomes já estão resolvidos na tabela de entidades:
		if rawItem != "" && rawBuff != "" {
			if nHandle != p.lastNeutralHandle[entityIdx] || bHandle != p.lastBuffHandle[entityIdx] {
				p.lastNeutralHandle[entityIdx] = nHandle
				p.lastBuffHandle[entityIdx] = bHandle
				// Só adiciona novo Tier se o par (item + buff) mudou ou se o handle do buff (novo tier) mudou
				p.neutralItemsHistory[entityIdx] = append(p.neutralItemsHistory[entityIdx], rawItem)
				p.jungleBuffsHistory[entityIdx] = append(p.jungleBuffsHistory[entityIdx], rawBuff)
			}
		}

		return nil
	})
}

// GetPlayersReport compila os dados dos acumuladores temporários em uma struct PlayersReport limpa e tipada.
func (p *PlayersAnalyzer) GetPlayersReport(parser *manta.Parser) *dto.PlayersReport {
	entities := parser.FilterEntity(func(e *manta.Entity) bool {
		if e == nil {
			return false
		}
		c := e.GetClassName()
		return c == "CDOTA_PlayerResource" || c == "C_DOTA_PlayerResource" || strings.HasSuffix(c, "DOTA_PlayerResource")
	})
	if len(entities) == 0 {
		return &dto.PlayersReport{
			Radiant: []dto.PlayerStats{},
			Dire:    []dto.PlayerStats{},
		}
	}
	pr := entities[0]

	clMap := p.buildCombatLogMap(pr, parser)
	radiantData := firstEntityByClass(parser, "CDOTA_DataRadiant")
	direData := firstEntityByClass(parser, "CDOTA_DataDire")
	durationMin := matchDurationMin(parser)

	handleMap := make(map[uint32]dto.PlayerLink)
	var radiant, dire, others []dto.PlayerStats

	for i := 0; i < 30; i++ {
		prefixData := fmt.Sprintf("m_vecPlayerData.%04d", i)
		prefixTeam := fmt.Sprintf("m_vecPlayerTeamData.%04d", i)

		isValid, ok := pr.GetBool(prefixData + ".m_bIsValid")
		if !ok || !isValid {
			continue
		}

		name, _ := pr.GetString(prefixData + ".m_iszPlayerName")
		heroID, _ := pr.GetUint32(prefixTeam + ".m_nSelectedHeroID")
		kills, _ := pr.GetInt32(prefixTeam + ".m_iKills")
		deaths, _ := pr.GetInt32(prefixTeam + ".m_iDeaths")
		assists, _ := pr.GetInt32(prefixTeam + ".m_iAssists")
		team, _ := pr.GetInt32(prefixData + ".m_iPlayerTeam")
		steamID, _ := pr.GetUint64(prefixData + ".m_iPlayerSteamID")
		hHandle, _ := pr.GetUint64(prefixTeam + ".m_hSelectedHero")

		heroEntity := entityByHandle(parser, hHandle)

		heroName := dto.GetHeroName(heroID)
		if heroEntity != nil {
			heroName = heroClassToDisplay(heroEntity.GetClassName())
		}
		if heroID == 0 && heroName != "" {
			heroID = dto.GetHeroIDByName(heroName)
		}

		mainItems, backpack, neutralItem, jungleBuff := extractItems(heroEntity, parser)
		hasScepter, hasShard := extractAghanimStatus(p.activeModifiers, uint32(hHandle), heroEntity)
		netWorth, gpm, xpm := extractEconomyStats(pr, prefixTeam, team, radiantData, direData, durationMin)

		clName := ""
		if heroEntity != nil {
			clName = heroClassToCombatLog(heroEntity.GetClassName())
		}

		if uint32(hHandle) > 0 {
			handleMap[uint32(hHandle)] = dto.PlayerLink{
				SteamID:  steamID,
				PlayerID: i,
				HeroID:   heroID,
				HeroName: heroName,
				Team:     teamToStr(team),
			}
		}

		obsPlaced := p.obsWardsPurchased[clName]
		senPlaced := p.senWardsPurchased[clName]
		wardsDest := p.obsWardsDestroyed[clName]

		var teamData *manta.Entity
		switch team {
		case 2:
			teamData = radiantData
		case 3:
			teamData = direData
		}
		if teamData != nil {
			teamSlot, _ := pr.GetInt32(prefixTeam + ".m_iTeamSlot")
			keyPrefix := fmt.Sprintf("m_vecDataTeam.%04d", teamSlot)
			if op, ok := teamData.GetInt32(keyPrefix + ".m_iObserverWardsPlaced"); ok && op > 0 {
				obsPlaced = int(op)
			}
			if sp, ok := teamData.GetInt32(keyPrefix + ".m_iSentryWardsPlaced"); ok && sp > 0 {
				senPlaced = int(sp)
			}
			if wd, ok := teamData.GetInt32(keyPrefix + ".m_iWardsDestroyed"); ok && wd > 0 {
				wardsDest = int(wd)
			}
		}

		neutralHistory := []string{}
		if heroEntity != nil {
			if hist, exists := p.neutralItemsHistory[heroEntity.GetIndex()]; exists {
				neutralHistory = append(neutralHistory, hist...)
			}
		}
		if len(neutralHistory) == 0 && neutralItem != "" {
			neutralHistory = append(neutralHistory, neutralItem)
		}

		jungleBuffHistory := []string{}
		if heroEntity != nil {
			if hist, exists := p.jungleBuffsHistory[heroEntity.GetIndex()]; exists {
				jungleBuffHistory = append(jungleBuffHistory, hist...)
			}
		}
		if len(jungleBuffHistory) == 0 && jungleBuff != "" {
			jungleBuffHistory = append(jungleBuffHistory, jungleBuff)
		}

		stats := dto.PlayerStats{
			SteamID:         steamID,
			Name:            name,
			HeroID:          heroID,
			Hero:            heroName,
			Kills:           kills,
			Deaths:          deaths,
			Assists:         assists,
			NetWorth:        netWorth,
			GPM:             gpm,
			XPM:             xpm,
			Inventory: dto.PlayerInventory{
				MainItems:    mainItems,
				Backpack:     backpack,
				NeutralItem:  neutralItem,
				NeutralItems: neutralHistory,
				JungleBuff:   jungleBuff,
				JungleBuffs:  jungleBuffHistory,
			},
			Upgrades: dto.PlayerUpgrades{
				HasScepter: hasScepter,
				HasShard:   hasShard,
			},
			ObsPurchased:    obsPlaced,
			SenPurchased:    senPlaced,
			ObsDestroyed:    wardsDest,
			StructureDamage: p.structureDamage[clName],
			HeroDamage:      p.heroDamage[clName],
			HeroHealing:     p.heroHealing[clName],
		}

		switch team {
		case 2:
			radiant = append(radiant, stats)
		case 3:
			dire = append(dire, stats)
		default:
			others = append(others, stats)
		}
	}

	return &dto.PlayersReport{
		Radiant:        radiant,
		Dire:           dire,
		Others:         others,
		FirstBlood:     p.buildFirstBlood(clMap),
		RoshanKills:    p.buildRoshanKills(clMap, handleMap),
		TormentorKills: p.buildTormentorKills(clMap),
		Rampages:       p.buildRampages(clMap),
	}
}

// --- Métodos privados para organizar os dados e links de jogadores ---

func normalizeCombatLogKey(s string) string {
	s = strings.TrimPrefix(s, "npc_dota_hero_")
	s = strings.ReplaceAll(s, "_", "")
	s = strings.ReplaceAll(s, " ", "")
	return strings.ToLower(s)
}

func (p *PlayersAnalyzer) buildCombatLogMap(pr *manta.Entity, parser *manta.Parser) map[string]combatLogEntry {
	clMap := make(map[string]combatLogEntry)
	for i := 0; i < 30; i++ {
		prefixData := fmt.Sprintf("m_vecPlayerData.%04d", i)
		prefixTeam := fmt.Sprintf("m_vecPlayerTeamData.%04d", i)
		isValid, _ := pr.GetBool(prefixData + ".m_bIsValid")
		if !isValid {
			continue
		}
		heroID, _ := pr.GetUint32(prefixTeam + ".m_nSelectedHeroID")
		hHandle, _ := pr.GetUint64(prefixTeam + ".m_hSelectedHero")
		team, _ := pr.GetInt32(prefixData + ".m_iPlayerTeam")
		steamID, _ := pr.GetUint64(prefixData + ".m_iPlayerSteamID")
		heroEntity := entityByHandle(parser, hHandle)

		var clName, heroName string
		if heroEntity != nil {
			clName = heroClassToCombatLog(heroEntity.GetClassName())
			heroName = heroClassToDisplay(heroEntity.GetClassName())
		}
		if clName == "" {
			heroName = dto.GetHeroName(heroID)
			clName = "npc_dota_hero_" + strings.ToLower(strings.ReplaceAll(heroName, " ", "_"))
		}
		if heroID == 0 && heroName != "" {
			heroID = dto.GetHeroIDByName(heroName)
		}
		if clName != "" {
			clMap[clName] = combatLogEntry{steamID: steamID, playerID: i, heroID: heroID, heroName: heroName, team: teamToStr(team)}
		}
	}
	return clMap
}

func (p *PlayersAnalyzer) resolveLink(clName string, clMap map[string]combatLogEntry) dto.PlayerLink {
	resolved := summonToHero(clName)
	if info, ok := clMap[resolved]; ok {
		return dto.PlayerLink{SteamID: info.steamID, PlayerID: info.playerID, HeroID: info.heroID, HeroName: info.heroName, Team: info.team}
	}
	normResolved := normalizeCombatLogKey(resolved)
	for k, info := range clMap {
		if normalizeCombatLogKey(k) == normResolved || normalizeCombatLogKey(info.heroName) == normResolved {
			return dto.PlayerLink{SteamID: info.steamID, PlayerID: info.playerID, HeroID: info.heroID, HeroName: info.heroName, Team: info.team}
		}
	}
	return dto.PlayerLink{SteamID: 0, PlayerID: -1, HeroID: 0, HeroName: combatLogToDisplay(clName), Team: ""}
}

func (p *PlayersAnalyzer) buildFirstBlood(clMap map[string]combatLogEntry) *dto.FirstBloodEvent {
	if p.firstBloodKiller == "" {
		return nil
	}
	return &dto.FirstBloodEvent{
		TimeSeconds: p.firstBloodTime, TimeString: fmtTime(p.firstBloodTime),
		Killer: p.resolveLink(p.firstBloodKiller, clMap),
		Victim: p.resolveLink(p.firstBloodVictim, clMap),
	}
}

func (p *PlayersAnalyzer) buildRoshanKills(clMap map[string]combatLogEntry, handleMap map[uint32]dto.PlayerLink) []dto.RoshanKillEvent {
	out := make([]dto.RoshanKillEvent, 0, len(p.roshanKills))
	for _, r := range p.roshanKills {
		killerLink := p.resolveLink(r.killer, clMap)
		event := dto.RoshanKillEvent{
			TimeSeconds: r.time,
			TimeString:  fmtTime(r.time),
			Killer:      killerLink,
		}

		var aegisTakenBy dto.PlayerLink
		hasPickup := false

		for _, cp := range p.aegisCombatLogPickups {
			if cp.time >= r.time-5.0 && cp.time <= r.time+120.0 {
				aegisTakenBy = p.resolveLink(cp.targetHero, clMap)
				if aegisTakenBy.HeroName != "" || aegisTakenBy.SteamID > 0 {
					hasPickup = true
					break
				}
			}
		}

		if !hasPickup {
			for _, mp := range p.aegisPickups {
				if mp.time >= r.time-5.0 && mp.time <= r.time+120.0 {
					if link, ok := handleMap[mp.parent]; ok && (link.HeroID > 0 || link.HeroName != "") {
						aegisTakenBy = link
						hasPickup = true
						break
					}
				}
			}
		}

		if hasPickup {
			event.TakenBy = aegisTakenBy
			if killerLink.Team != "" && aegisTakenBy.Team != "" && !strings.EqualFold(killerLink.Team, aegisTakenBy.Team) {
				event.IsStolen = true
			}
		}

		out = append(out, event)
	}
	return out
}

func (p *PlayersAnalyzer) buildTormentorKills(clMap map[string]combatLogEntry) []dto.TormentorKillEvent {
	out := make([]dto.TormentorKillEvent, 0, len(p.tormentorKills))
	for _, t := range p.tormentorKills {
		out = append(out, dto.TormentorKillEvent{TimeSeconds: t.time, TimeString: fmtTime(t.time), Killer: p.resolveLink(t.killer, clMap)})
	}
	return out
}

func (p *PlayersAnalyzer) buildRampages(clMap map[string]combatLogEntry) []dto.RampageEvent {
	out := make([]dto.RampageEvent, 0)
	for _, m := range p.multikills {
		if m.tier == 5 {
			out = append(out, dto.RampageEvent{TimeSeconds: m.time, TimeString: fmtTime(m.time), Killer: p.resolveLink(m.killer, clMap)})
		}
	}
	return out
}

// --- Funções auxiliares para extração de itens e economia ---

func extractItems(heroEntity *manta.Entity, parser *manta.Parser) (main []string, backpack []string, neutral string, jungleBuff string) {
	main = []string{}
	backpack = []string{}
	if heroEntity == nil {
		return
	}
	for slot := 0; slot < 6; slot++ {
		if item := itemName(heroEntity, slot, parser); item != "" {
			main = append(main, item)
		}
	}
	for slot := 6; slot < 9; slot++ {
		if item := itemName(heroEntity, slot, parser); item != "" {
			backpack = append(backpack, item)
		}
	}
	neutral = itemName(heroEntity, 16, parser)
	jungleBuff = itemName(heroEntity, 17, parser)
	return
}

func extractAghanimStatus(mods map[uint32]map[string]bool, handle uint32, heroEntity *manta.Entity) (scepter bool, shard bool) {
	if heroEntity == nil {
		return
	}
	if m := mods[handle]; m != nil {
		scepter = m["modifier_item_ultimate_scepter_consumed"] || m["modifier_item_ultimate_scepter_consumed_alchemist"]
		shard = m["modifier_item_aghanims_shard"]
	}
	return
}

func extractEconomyStats(pr *manta.Entity, prefixTeam string, team int32, radiantData, direData *manta.Entity, durationMin float32) (netWorth, gpm, xpm int32) {
	var teamData *manta.Entity
	switch team {
	case 2:
		teamData = radiantData
	case 3:
		teamData = direData
	}
	if teamData == nil {
		return
	}
	teamSlot, _ := pr.GetInt32(prefixTeam + ".m_iTeamSlot")
	keyPrefix := fmt.Sprintf("m_vecDataTeam.%04d", teamSlot)
	nw, _ := teamData.GetInt32(keyPrefix + ".m_iNetWorth")
	gold, _ := teamData.GetInt32(keyPrefix + ".m_iTotalEarnedGold")
	xp, _ := teamData.GetInt32(keyPrefix + ".m_iTotalEarnedXP")
	netWorth = nw
	if durationMin > 0 {
		gpm = int32(float32(gold) / durationMin)
		xpm = int32(float32(xp) / durationMin)
	}
	return
}

func itemHandle(heroEntity *manta.Entity, slot int) uint32 {
	val := heroEntity.Get(fmt.Sprintf("m_hItems.%04d", slot))
	if val == nil {
		return 0
	}
	var handle uint32
	switch v := val.(type) {
	case uint32:
		handle = v
	case int32:
		handle = uint32(v)
	case int:
		handle = uint32(v)
	case uint64:
		handle = uint32(v)
	}
	if handle == 0 || handle == 16777215 {
		return 0
	}
	return handle
}

func itemName(heroEntity *manta.Entity, slot int, parser *manta.Parser) string {
	handle := itemHandle(heroEntity, slot)
	if handle == 0 {
		return ""
	}
	e := parser.FindEntityByHandle(uint64(handle))
	if e == nil {
		return ""
	}
	name := strings.TrimPrefix(e.GetClassName(), "CDOTA_Item_")
	return "item_" + strings.ToLower(strings.ReplaceAll(name, " ", "_"))
}

var heroDisplayNames = map[string]string{
	"SkeletonKing": "Wraith King", "Shredder": "Timbersaw", "Windrunner": "Windranger",
	"Zuus": "Zeus", "Nevermore": "Shadow Fiend", "Rattletrap": "Clockwerk",
	"Furion": "Nature's Prophet", "Life_Stealer": "Lifestealer", "DoomBringer": "Doom",
	"Obsidian_Destroyer": "Outworld Destroyer", "Wisp": "Io", "Magnataur": "Magnus",
	"Treant": "Treant Protector", "AbyssalUnderlord": "Underlord", "Antimage": "Anti-Mage",
	"Vengefulspirit": "Vengeful Spirit", "Centaur": "Centaur Warrunner", "Skywrath_Mage": "Skywrath Mage",
}

func heroClassToDisplay(className string) string {
	name := strings.TrimPrefix(className, "CDOTA_Unit_Hero_")
	if override, ok := heroDisplayNames[name]; ok {
		return override
	}
	name = strings.ReplaceAll(name, "_", " ")
	var out []rune
	for i, r := range name {
		if i > 0 && r >= 'A' && r <= 'Z' {
			if prev := rune(name[i-1]); (prev >= 'a' && prev <= 'z') || (prev >= '0' && prev <= '9') {
				out = append(out, ' ')
			}
		}
		out = append(out, r)
	}
	return string(out)
}

func heroClassToCombatLog(className string) string {
	name := strings.TrimPrefix(className, "CDOTA_Unit_Hero_")
	switch name {
	case "DarkWillow":
		return "npc_dota_hero_dark_willow"
	case "PhantomLancer":
		return "npc_dota_hero_phantom_lancer"
	case "QueenOfPain":
		return "npc_dota_hero_queenofpain"
	case "SkeletonKing":
		return "npc_dota_hero_skeleton_king"
	default:
		return "npc_dota_hero_" + strings.ToLower(name)
	}
}

func combatLogToDisplay(clName string) string {
	if !strings.HasPrefix(clName, "npc_dota_hero_") {
		if strings.Contains(clName, "zombie") {
			return "Undying (Zombie)"
		}
		if strings.Contains(clName, "skeleton") {
			return "Wraith King (Skeleton)"
		}
		return clName
	}
	name := strings.TrimPrefix(clName, "npc_dota_hero_")
	parts := strings.Split(name, "_")
	for i, part := range parts {
		if len(part) > 0 {
			parts[i] = strings.ToUpper(part[:1]) + part[1:]
		}
	}
	camel := strings.Join(parts, "")
	if camel == "Queenofpain" {
		camel = "QueenOfPain"
	}
	return heroClassToDisplay("CDOTA_Unit_Hero_" + camel)
}

func summonToHero(attacker string) string {
	if strings.HasPrefix(attacker, "npc_dota_hero_") {
		return attacker
	}
	switch {
	case strings.Contains(attacker, "zombie"):
		return "npc_dota_hero_undying"
	case strings.Contains(attacker, "skeleton"):
		return "npc_dota_hero_skeleton_king"
	case strings.Contains(attacker, "eidolon"):
		return "npc_dota_hero_enigma"
	case strings.Contains(attacker, "treant"), strings.Contains(attacker, "furion"):
		return "npc_dota_hero_furion"
	case strings.Contains(attacker, "bear"):
		return "npc_dota_hero_lone_druid"
	}
	return attacker
}

func teamToStr(team int32) string {
	switch team {
	case 2:
		return "Radiant"
	case 3:
		return "Dire"
	default:
		return ""
	}
}

func matchDurationMin(parser *manta.Parser) float32 {
	for _, e := range parser.FilterEntity(func(e *manta.Entity) bool {
		return e != nil && e.GetClassName() == "CDOTAGamerulesProxy"
	}) {
		var start, end float32
		if v := e.Get("m_pGameRules.m_flGameStartTime"); v != nil {
			if f, ok := v.(float32); ok {
				start = f
			}
		}
		if v := e.Get("m_pGameRules.m_flGameEndTime"); v != nil {
			if f, ok := v.(float32); ok {
				end = f
			}
		}
		if end > start && start > 0 {
			return (end - start) / 60.0
		}
		break
	}
	return float32(parser.Tick) / 30.0 / 60.0
}

func firstEntityByClass(parser *manta.Parser, className string) *manta.Entity {
	list := parser.FilterEntity(func(e *manta.Entity) bool {
		if e == nil {
			return false
		}
		c := e.GetClassName()
		return c == className || c == "C_"+className || strings.HasSuffix(c, className)
	})
	if len(list) > 0 {
		return list[0]
	}
	return nil
}

func entityByHandle(parser *manta.Parser, handle uint64) *manta.Entity {
	if handle == 0 || handle == 16777215 {
		return nil
	}
	return parser.FindEntityByHandle(handle)
}

func fmtTime(sec float32) string {
	if sec < 0 {
		sec = 0
	}
	return fmt.Sprintf("%02d:%02d", int(sec)/60, int(sec)%60)
}
