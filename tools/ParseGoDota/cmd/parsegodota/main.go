package main

import (
	"bufio"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"os"
	"path/filepath"
	"strings"

	"parsegodota/pkg/analyzer"
	"parsegodota/pkg/dto"

	"github.com/dotabuff/manta"
)

func main() {
	if len(os.Args) < 2 {
		fmt.Println("Uso: parsegodota <caminho_do_arquivo.dem ou pasta_de_replays>")
		os.Exit(1)
	}

	targetPath := filepath.Clean(strings.TrimSpace(os.Args[1]))

	targetInfo, err := os.Stat(targetPath)
	if err != nil {
		log.Fatalf("Erro ao ler o caminho '%s': %v", targetPath, err)
	}

	if targetInfo.IsDir() {
		// Se for uma pasta, processa todos os arquivos .dem lá dentro de forma sequencial
		processDirectory(targetPath)
	} else {
		// Se for um arquivo específico, executa a análise individual
		processSingleFile(targetPath)
	}
}

// processDirectory varre a pasta informada convertendo cada replay encontrado
func processDirectory(dirPath string) {
	entries, err := os.ReadDir(dirPath)
	if err != nil {
		log.Fatalf("Erro ao ler diretório '%s': %v", dirPath, err)
	}

	var parsedCount int
	for _, entry := range entries {
		if entry.IsDir() {
			continue
		}

		name := entry.Name()
		if strings.HasSuffix(strings.ToLower(name), ".dem") {
			fullPath := filepath.Join(dirPath, name)
			log.Printf("[Pasta] Processando replay: %s...", name)

			// Executa a conversão e descarte do arquivo individual.
			// Usamos uma função anônima para isolar o escopo e garantir o fechamento de arquivos em sub-rotinas
			func(path string) {
				file, err := validateReplayFile(path)
				if err != nil {
					log.Printf("[Erro] Validação falhou para o arquivo %s: %v", path, err)
					return
				}
				defer file.Close()

				bufferedReader := bufio.NewReaderSize(file, 1024*1024)
				parser, err := manta.NewStreamParser(bufferedReader)
				if err != nil {
					log.Printf("[Erro] Falha ao inicializar o parser manta para %s: %v", path, err)
					return
				}

				matchAnalyzer := analyzer.NewSummaryAnalyzer()
				playerAnalyzer := analyzer.NewPlayersAnalyzer()

				analyzers := []analyzer.Analyzer{matchAnalyzer, playerAnalyzer}
				for _, az := range analyzers {
					az.RegisterCallbacks(parser)
				}

				// Roda o parsing com panic recovery
				err = func() (parserErr error) {
					defer func() {
						if r := recover(); r != nil {
							parserErr = fmt.Errorf("panic capturado: %v", r)
						}
					}()
					return parser.Start()
				}()

				// Fecha o arquivo antes da remoção e exportação
				file.Close()

				if err != nil {
					log.Printf("[Erro] Falha no processamento do arquivo %s: %v", path, err)
					return
				}

				exportJSON(path, matchAnalyzer, playerAnalyzer, parser)

				// Descarte automático do arquivo original
				if err := os.Remove(path); err != nil {
					log.Printf("[Aviso] Não foi possível remover o arquivo temporário %s: %v", path, err)
				} else {
					parsedCount++
				}
			}(fullPath)
		}
	}
	log.Printf("[Concluído] Processamento da pasta finalizado. %d replays convertidos com sucesso.", parsedCount)
}

// processSingleFile lida com a análise e descarte de um arquivo de replay individual
func processSingleFile(filePath string) {
	file, err := validateReplayFile(filePath)
	if err != nil {
		log.Fatalf("Erro de validação do replay: %v", err)
	}

	bufferedReader := bufio.NewReaderSize(file, 1024*1024)

	parser, err := manta.NewStreamParser(bufferedReader)
	if err != nil {
		file.Close()
		log.Fatalf("Erro ao inicializar o Manta parser: %v", err)
	}

	matchAnalyzer := analyzer.NewSummaryAnalyzer()
	playerAnalyzer := analyzer.NewPlayersAnalyzer()

	analyzers := []analyzer.Analyzer{matchAnalyzer, playerAnalyzer}

	for _, az := range analyzers {
		az.RegisterCallbacks(parser)
	}

	// Proteção contra pânicos (panics) internos da engine do Manta
	err = func() (parserErr error) {
		defer func() {
			if r := recover(); r != nil {
				parserErr = fmt.Errorf("o parser de replay falhou criticamente (pânico capturado): %v", r)
			}
		}()
		return parser.Start()
	}()

	// Fechamos o arquivo de replay de imediato, pois o processamento terminou.
	file.Close()

	if err != nil {
		log.Fatalf("Erro durante o processamento do replay: %v", err)
	}

	exportJSON(filePath, matchAnalyzer, playerAnalyzer, parser)

	// Após converter e salvar o JSON com sucesso, deletamos o arquivo de replay (.dem) original
	if err := os.Remove(filePath); err != nil {
		log.Printf("[Aviso] Não foi possível remover o arquivo de replay temporário %s: %v", filePath, err)
	}
}

// validateReplayFile realiza a validação de segurança e integridade do replay.
func validateReplayFile(filePath string) (*os.File, error) {
	cleanedPath := filepath.Clean(strings.TrimSpace(filePath))
	if cleanedPath == "." || cleanedPath == "" {
		return nil, fmt.Errorf("caminho inválido ou vazio")
	}
	if !strings.HasSuffix(strings.ToLower(cleanedPath), ".dem") {
		return nil, fmt.Errorf("extensão inválida (esperado .dem)")
	}

	file, err := os.Open(cleanedPath)
	if err != nil {
		return nil, fmt.Errorf("não foi possível abrir o arquivo: %w", err)
	}

	fileInfo, err := file.Stat()
	if err != nil {
		file.Close()
		return nil, fmt.Errorf("erro ao obter informações do arquivo: %w", err)
	}
	if !fileInfo.Mode().IsRegular() {
		file.Close()
		return nil, fmt.Errorf("o caminho não aponta para um arquivo regular")
	}

	// Limita o tamanho do arquivo para evitar Denial of Service (DoS) por uploads de gigabytes.
	const maxReplaySize = 350 * 1024 * 1024 // 350 MB
	const minReplaySize = 500 * 1024        // 500 KB

	if fileInfo.Size() < minReplaySize {
		file.Close()
		return nil, fmt.Errorf("arquivo muito pequeno (%d bytes) para ser um replay de Dota 2 válido", fileInfo.Size())
	}
	if fileInfo.Size() > maxReplaySize {
		file.Close()
		return nil, fmt.Errorf("arquivo muito grande (%d bytes) - limite máximo permitido de 350MB", fileInfo.Size())
	}

	header := make([]byte, 8)
	if _, err = io.ReadFull(file, header); err != nil {
		file.Close()
		return nil, fmt.Errorf("erro ao ler cabeçalho: %w", err)
	}
	if _, err = file.Seek(0, 0); err != nil {
		file.Close()
		return nil, fmt.Errorf("erro ao reiniciar ponteiro: %w", err)
	}

	switch string(header) {
	case "PBDEMS2\x00":
		return file, nil
	case "PBUFDEM\x00":
		file.Close()
		return nil, fmt.Errorf("replay do Dota 2 Source 1 (antigo) não suportado")
	default:
		file.Close()
		return nil, fmt.Errorf("assinatura inválida: não é um replay Source 2 do Dota 2")
	}
}

// FullReplayData é o JSON de saída final completo gerado pelo programa.
type FullReplayData struct {
	Summary *dto.MatchSummaryData `json:"sumario"`
	Players *dto.PlayersReport    `json:"jogadores_e_eventos"`
}

// exportJSON exporta os DTOs preenchidos como um arquivo JSON na pasta "convertido/".
func exportJSON(filePath string, matchAz *analyzer.SummaryAnalyzer, playerAz *analyzer.PlayersAnalyzer, parser *manta.Parser) {
	summaryData := matchAz.GetSummaryData(parser)
	playersData := playerAz.GetPlayersReport(parser)

	// Reconciliação inteligente: Se algum jogador estiver sem HeroID (ou zero),
	// deduz o herói a partir dos picks do draft daquela equipe
	reconcileMissingHeroIDs(summaryData, playersData)

	fullData := FullReplayData{
		Summary: summaryData,
		Players: playersData,
	}

	outputDir := "convertido"
	if err := os.MkdirAll(outputDir, 0755); err != nil {
		log.Fatalf("[Erro] Não foi possível criar a pasta '%s': %v", outputDir, err)
	}

	var filename string
	if summaryData != nil && summaryData.MatchID > 0 {
		filename = fmt.Sprintf("%d.json", summaryData.MatchID)
	} else {
		base := filepath.Base(filePath)
		filename = strings.TrimSuffix(base, filepath.Ext(base)) + ".json"
	}

	outputPath := filepath.Join(outputDir, filename)

	jsonData, err := json.MarshalIndent(fullData, "", "  ")
	if err != nil {
		log.Fatalf("[Erro] Falha ao converter para JSON: %v", err)
	}
	if err := os.WriteFile(outputPath, jsonData, 0644); err != nil {
		log.Fatalf("[Erro] Falha ao salvar JSON: %v", err)
	}
}

// reconcileMissingHeroIDs preenche qualquer HeroID == 0 utilizando os heróis escolhidos (PICK) no draft
func reconcileMissingHeroIDs(summary *dto.MatchSummaryData, players *dto.PlayersReport) {
	if summary == nil || players == nil || len(summary.PicksBans) == 0 {
		return
	}

	reconcileTeam := func(teamPlayers []dto.PlayerStats, teamName string) {
		var teamPicks []dto.PickBanData
		for _, pb := range summary.PicksBans {
			if pb.Action == "PICK" && strings.EqualFold(pb.Team, teamName) {
				teamPicks = append(teamPicks, pb)
			}
		}

		if len(teamPicks) == 0 {
			return
		}

		assignedHeroes := make(map[uint32]bool)
		for _, p := range teamPlayers {
			if p.HeroID > 0 {
				assignedHeroes[p.HeroID] = true
			}
		}

		var unassignedPicks []dto.PickBanData
		for _, pick := range teamPicks {
			if !assignedHeroes[pick.HeroID] {
				unassignedPicks = append(unassignedPicks, pick)
			}
		}

		unassignedIdx := 0
		for i := range teamPlayers {
			if teamPlayers[i].HeroID == 0 && unassignedIdx < len(unassignedPicks) {
				teamPlayers[i].HeroID = unassignedPicks[unassignedIdx].HeroID
				if teamPlayers[i].Hero == "" || strings.HasPrefix(teamPlayers[i].Hero, "Unknown") {
					teamPlayers[i].Hero = unassignedPicks[unassignedIdx].Hero
				}
				unassignedIdx++
			}
		}
	}

	reconcileTeam(players.Radiant, "Radiant")
	reconcileTeam(players.Dire, "Dire")
}
