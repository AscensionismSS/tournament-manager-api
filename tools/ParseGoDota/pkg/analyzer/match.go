package analyzer

import (
	"fmt"
	"strings"
	"time"

	"parsegodota/pkg/dto"

	"github.com/dotabuff/manta"
	"github.com/dotabuff/manta/dota"
)

// SummaryAnalyzer é o "motor" ou processador responsável por ler e acumular
// dados gerais sobre a partida enquanto o arquivo .dem é processado.
//
// 📌 POR QUE ESSA STRUCT ESTÁ AQUI E NÃO NO PACOTE DTO?
// Esta struct não é o modelo final que vira JSON. Ela serve apenas como um
// ACUMULADOR DE ESTADO TEMPORÁRIO durante a execução do parser.
// Quando o parser termina de ler o arquivo, nós chamamos o método `GetSummaryData`
// que converte este estado acumulado no DTO final limpo (`dto.MatchSummaryData`).
type SummaryAnalyzer struct {
	// fileHeader guarda os metadados do arquivo .dem (versão, mapa, servidor, etc.)
	fileHeader *dota.CDemoFileHeader

	// fileInfo guarda informações da partida geradas no fim do replay (ID, vencedor, picks/bans)
	fileInfo *dota.CDemoFileInfo

	// startTime e endTime guardam o tempo de início e fim da partida (segundos do jogo)
	startTime float32
	endTime   float32

	// Acumuladores de mortes totais de cada equipe
	radiantKills int
	direKills    int
}

// Garante em tempo de compilação que SummaryAnalyzer implementa a interface Analyzer.
var _ Analyzer = (*SummaryAnalyzer)(nil)

// NewSummaryAnalyzer inicializa o processador de sumário.
func NewSummaryAnalyzer() *SummaryAnalyzer {
	return &SummaryAnalyzer{}
}

// Name retorna o identificador do analisador.
func (s *SummaryAnalyzer) Name() string {
	return "SummaryAnalyzer"
}

// RegisterCallbacks registra as funções que serão chamadas automaticamente
// pelo Manta quando encontrar cabeçalhos ou informações da partida.
func (s *SummaryAnalyzer) RegisterCallbacks(parser *manta.Parser) {
	parser.Callbacks.OnCDemoFileHeader(func(h *dota.CDemoFileHeader) error {
		if h == nil {
			return fmt.Errorf("cabeçalho do replay vazio ou ausente")
		}

		// Validação contra forjação: o diretório do jogo no cabeçalho original da Valve
		// deve conter obrigatoriamente a palavra "dota" (ex: dota, dota_v6791, dota_addons).
		dir := strings.ToLower(h.GetGameDirectory())
		if !strings.Contains(dir, "dota") {
			return fmt.Errorf("diretório de jogo inválido no cabeçalho: %q (não parece um replay de Dota 2)", h.GetGameDirectory())
		}

		// Validação de segurança: carimbo do arquivo de replay
		if h.GetDemoFileStamp() == "" && h.GetBuildNum() <= 0 {
			return fmt.Errorf("carimbo de arquivo do replay inválido ou ausente")
		}

		s.fileHeader = h
		return nil
	})

	parser.Callbacks.OnCDemoFileInfo(func(info *dota.CDemoFileInfo) error {
		if info == nil {
			return fmt.Errorf("metadados finais de reprodução (FileInfo) vazios ou ausentes")
		}

		gi := info.GetGameInfo()
		if gi == nil {
			return fmt.Errorf("estrutura GameInfo ausente no arquivo de replay")
		}

		di := gi.GetDota()
		if di == nil {
			return fmt.Errorf("detalhes da partida Dota ausentes no arquivo de replay")
		}

		// Validação contra arquivos vazios ou fakes: todo replay real processado
		// pelo menos gera um ID de partida válido maior que zero.
		if di.GetMatchId() <= 0 {
			return fmt.Errorf("ID da partida inválido ou nulo nos metadados: %d", di.GetMatchId())
		}

		s.fileInfo = info
		return nil
	})
}

// GetSummaryData transforma os dados brutos temporários acumulados nesta struct
// em um DTO limpo (MatchSummaryData) pronto para ser exportado como JSON.
func (s *SummaryAnalyzer) GetSummaryData(parser *manta.Parser) *dto.MatchSummaryData {
	s.readEntityStats(parser)

	data := &dto.MatchSummaryData{
		ScoreRadiant: s.radiantKills,
		ScoreDire:    s.direKills,
	}

	if s.fileHeader != nil {
		data.Server = s.fileHeader.GetServerName()
		data.Client = s.fileHeader.GetClientName()
		data.Map = s.fileHeader.GetMapName()
		data.GameDirectory = s.fileHeader.GetGameDirectory()
	}

	if s.fileInfo != nil {
		dur := s.gameDurationSec()
		data.DurationSeconds = dur
		data.DurationFormatted = fmt.Sprintf("%02d:%02d", int(dur)/60, int(dur)%60)

		if gi := s.fileInfo.GetGameInfo(); gi != nil {
			if di := gi.GetDota(); di != nil {
				data.MatchID = di.GetMatchId()

				if di.EndTime != nil && di.GetEndTime() > 0 {
					endUnix := int64(di.GetEndTime())
					data.DateTimeStart = time.Unix(endUnix-int64(s.fileInfo.GetPlaybackTime()), 0).Format("02/01/2006 15:04:05")
					data.DateTimeEnd = time.Unix(endUnix, 0).Format("15:04:05")
				}

				data.Winner = teamLabel(di.GetGameWinner())
				data.TeamRadiantTag = di.GetRadiantTeamTag()
				data.TeamDireTag = di.GetDireTeamTag()

				if pbs := di.GetPicksBans(); len(pbs) > 0 {
					data.PicksBans = make([]dto.PickBanData, 0, len(pbs))
					for _, e := range pbs {
						action := "BAN"
						if e.GetIsPick() {
							action = "PICK"
						}
						heroID := uint32(e.GetHeroId())
						data.PicksBans = append(data.PicksBans, dto.PickBanData{
							Action: action,
							Team:   teamLabel(int32(e.GetTeam())),
							HeroID: heroID,
							Hero:   dto.GetHeroName(heroID),
						})
					}
				}
			}
		}
	}

	return data
}

// --- Métodos auxiliares privados para ler entidades do Manta ---

func (s *SummaryAnalyzer) readEntityStats(parser *manta.Parser) {
	for _, e := range parser.FilterEntity(func(e *manta.Entity) bool {
		return e != nil && e.GetClassName() == "CDOTAGamerulesProxy"
	}) {
		if v := e.Get("m_pGameRules.m_flGameStartTime"); v != nil {
			if f, ok := v.(float32); ok {
				s.startTime = f
			}
		}
		if v := e.Get("m_pGameRules.m_flGameEndTime"); v != nil {
			if f, ok := v.(float32); ok {
				s.endTime = f
			}
		}
		break
	}

	for _, t := range parser.FilterEntity(func(e *manta.Entity) bool {
		return e != nil && e.GetClassName() == "CDOTATeam"
	}) {
		tn, ok1 := anyToInt(t.Get("m_iTeamNum"))
		kills, ok2 := anyToInt(t.Get("m_iHeroKills"))
		if !ok1 || !ok2 {
			continue
		}
		switch tn {
		case 2:
			s.radiantKills = kills
		case 3:
			s.direKills = kills
		}
	}
}

func (s *SummaryAnalyzer) gameDurationSec() float32 {
	if s.endTime > s.startTime && s.startTime > 0 {
		return s.endTime - s.startTime
	}
	if s.fileInfo != nil {
		return s.fileInfo.GetPlaybackTime()
	}
	return 0
}

func teamLabel(n int32) string {
	switch n {
	case 2:
		return "Radiant"
	case 3:
		return "Dire"
	default:
		return "Desconhecido"
	}
}

func anyToInt(v interface{}) (int, bool) {
	if v == nil {
		return 0, false
	}
	switch val := v.(type) {
	case int:
		return val, true
	case int8:
		return int(val), true
	case int16:
		return int(val), true
	case int32:
		return int(val), true
	case int64:
		return int(val), true
	case uint:
		return int(val), true
	case uint8:
		return int(val), true
	case uint16:
		return int(val), true
	case uint32:
		return int(val), true
	case uint64:
		return int(val), true
	case float32:
		return int(val), true
	case float64:
		return int(val), true
	}
	return 0, false
}
