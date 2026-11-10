# ParseGoDota - Dota 2 Replay Parser

O **ParseGoDota** é um utilitário CLI de alta performance desenvolvido em Go para extração segura de dados estruturados de arquivos de replay do Dota 2 (`.dem` - engine Source 2). Ele utiliza a biblioteca `dotabuff/manta` para ler e decodificar os pacotes binários do jogo e exportar as informações consolidadas em formato JSON.

O parser foi projetado para rodar de forma extremamente rápida e silenciosa, sendo ideal para execução em lote ou integração como microsserviço de back-end.

---

## 📁 Estrutura do Projeto

A arquitetura separa estritamente as definições de dados (DTOs) da lógica de acúmulo de estados:

```text
ParseGoDota/
├── cmd/
│   └── parsegodota/
│       └── main.go           # Ponto de entrada (validações, controle de fluxo e exportação)
│
├── pkg/
│   ├── dto/                  # Objetos de Transferência de Dados (Pure Data Structures)
│   │   ├── hero.go           # Mapeamento estático de heróis do Dota 2
│   │   ├── match.go          # Dados gerais da partida (picks, bans, Roshan, Tormentor, etc.)
│   │   └── player.go         # Métricas de jogadores (KDA, inventário, Aghanim upgrades)
│   │
│   └── analyzer/             # Lógica de processamento e acúmulo dos eventos binários
│       ├── analyzer.go       # Interface comum dos analisadores
│       ├── match.go          # Coleta de metadados e draft da partida
│       └── player.go         # Processamento de estatísticas e eventos de combate
│
├── replays/                  # Diretório local para replays (.dem) temporários
├── convertido/               # Diretório onde os relatórios JSON são salvos
├── Dockerfile                # Empacotamento para Docker (Debian Slim)
└── README.md                 # Guia do projeto
```

---

## 🛡️ Camadas de Segurança e Estabilidade

Para ser executado com segurança em servidores de produção recebendo uploads públicos, o parser executa validações em várias camadas:

1. **Path Traversal:** Limpeza de caminhos com `filepath.Clean` e validação se o alvo é um arquivo regular.
2. **Filtro de Tamanho:** Rejeita arquivos menores que **500KB** (fakes/corrompidos) ou maiores que **350MB** (prevenção de DoS).
3. **Magic Bytes:** Validação estrita da assinatura do cabeçalho binário (`PBDEMS2\x00`).
4. **Validação de Cabeçalhos Internos:** O parser aborta a leitura imediatamente se o diretório do jogo no replay não for `"dota"` ou se o protocolo for inválido.
5. **Panic Recovery:** A execução do parser é encapsulada em um bloco de `recover()`. Erros críticos de parsing de pacotes corrompidos são retornados como erro convencional, prevenindo a queda abrupta da aplicação.

---

## 🚀 Como Executar

### Pré-requisitos
* Go 1.22 ou superior instalado.

### 1. Compilação
Gere o executável compilado na raiz do projeto:
```bash
go build -o parsegodota ./cmd/parsegodota/main.go
```

### 2. Modo Arquivo Único
Passe o caminho do replay `.dem` como argumento. O programa executará silenciosamente, gerará o JSON na pasta `convertido/` e apagará o arquivo original.
```bash
./parsegodota replays/8817940931.dem
```

### 3. Modo Pasta (Processamento em Lote)
Se você passar um diretório, o parser processará sequencialmente todos os arquivos `.dem` presentes nele, gerará os respectivos JSONs em `convertido/` e excluirá os replays originais.
```bash
./parsegodota replays/
```

---

## 🐳 Executando com Docker

Você pode encapsular o parser em um container Docker para facilitar o deploy em produção.

### 1. Construir a Imagem
```bash
docker build -t dota-parser .
```

### 2. Executar Compartilhando Pastas (Volumes)
Para ler arquivos da sua máquina local e salvar os JSONs de volta nela, monte as pastas do host nos volumes do container:
```bash
docker run --rm \
  -v /caminho/no/host/replays:/root/replays \
  -v /caminho/no/host/convertido:/root/convertido \
  dota-parser /root/replays
```
*(Substitua `/caminho/no/host/replays` e `/caminho/no/host/convertido` pelos caminhos reais da sua máquina)*
