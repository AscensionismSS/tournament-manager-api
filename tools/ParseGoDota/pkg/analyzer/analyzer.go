// Package analyzer define a interface base para os processadores de replay.
package analyzer

import "github.com/dotabuff/manta"

// Analyzer é o contrato que todo analisador de dados de replay deve seguir.
type Analyzer interface {
	// Name retorna o nome amigável do analisador (útil para logs/erros).
	Name() string

	// RegisterCallbacks registra as funções de escuta (callbacks) no parser do Manta
	// para coletar dados enquanto os bytes do replay são lidos.
	RegisterCallbacks(parser *manta.Parser)
}
