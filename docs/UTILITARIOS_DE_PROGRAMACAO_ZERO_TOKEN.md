# Guia de Utilitários de Programação ("Modo Máquina" / Economia de Tokens)

Este guia explica como usar as ferramentas automáticas do seu projeto (**OpenAPI / Swagger JSON, Validação via Terminal e Swagger UI**) respeitando a arquitetura **Feature-First + Dumb UI** do seu Flutter (`TournametCreaterFront`) e **economizando até 80% de tokens de IA**.

---

## 1. Você está certo? Isso economiza Token de IA?

**SIM, você acertou em cheio!** Veja a diferença:

| Como a maioria faz (Gasta MUITO Token 💸) | Modo Máquina / CLI (Gasta ZERO Token ⚡) |
| :--- | :--- |
| Você pede para a IA vasculhar o backend todo para descobrir quais rotas e campos mudaram. | Você roda **1 comando no terminal** que baixa o `swagger.json` atualizado em **0,2 segundos** sem gastar 1 token! |
| Você pede para a IA testar se uma rota está funcionando. | Você testa em 3 segundos pelo `http://localhost:5000/swagger` ou `curl` direto no seu PC. |

---

## 2. Como é feito HOJE no seu Flutter (`TournametCreaterFront`)?

Consultei (em modo leitura) a estrutura atual do seu `TournametCreaterFront` para você ver exatamente onde cada coisa mora hoje:

```text
TournametCreaterFront/lib/
├── core/
│   └── network/
│       ├── api_client.dart                    <-- Cliente HTTP centralizado
│       ├── api_endpoints.dart                 <-- Todas as URLs da API centralizadas
│       ├── swagger_v1.json                    <-- Contrato oficial do Backend salvo aqui!
│       ├── tournament_events_service.dart     <-- Serviço SSE (Sininho e SnackBar)
│       └── tournament_webhook_event_dto.dart  <-- DTO de eventos SSE com ícones e deduplicação
│
└── features/                                  <-- Tudo separado na pasta certa de cada Feature!
    ├── auth/models/                           (login_request.dart, steam_preview_dto.dart...)
    ├── members/models/                        (member_dto.dart, recovery_request_dto.dart...)
    ├── admin/invitations/models/              (invitation_dto.dart, create_invitation_request.dart)
    ├── admin/tournaments/models/              (tournament_dto.dart, bracket_slot_dto.dart...)
    └── tournaments/models/                    (match_series_dto.dart, tournament_standings_dto.dart)
```

### ⚠️ Por que você NÃO deve usar um gerador cego (`swagger_parser`) no seu projeto?
Sua dúvida estava **100% certa**:
1. Um gerador automático genérico (`swagger_parser`) jogaria **todos os 58 DTOs dentro de uma única pasta genérica** (`generated/models`), quebrando a sua organização por `features/`.
2. Mais importante ainda: no seu Flutter, você usa o padrão **Dumb UI (UI Passiva)**!
   - Por exemplo, o seu arquivo `lib/features/admin/tournaments/models/tournament_dto.dart` não tem só os campos do JSON: ele tem dezenas de funções visuais prontas (`bannerPrimaryStatusColor`, `bannerPrimaryStatusIcon`, `playoffMdLabel`, `slotsSummaryLabel`, `entryModeIcon`).
   - Se um gerador automático sobrescrevesse esse arquivo, **ele apagaria todas as suas cores, ícones e formatações visuais**!

---

## 3. Onde fica a "Configuração" que direciona cada coisa para a pasta certa?

A sua dúvida principal: **"Essa configuração de pastas fica dentro do JSON (`swagger_v1.json`)?"**

**Resposta: NÃO fica dentro do JSON!**
O `swagger_v1.json` vem do Backend C#, e o Backend não sabe como são as pastas do seu Flutter. Dentro do `swagger_v1.json` só existem os nomes dos DTOs e seus campos (`id`, `name`, `teamCount`, etc.).

### Então quem guarda essa configuração no seu Front?
Existem **3 arquivos que trabalham juntos** lá no seu projeto `TournametCreaterFront`:

1. **`tool/sync_swagger.dart` (O Baixador em Modo Máquina):**
   - Quando você roda `dart run tool/sync_swagger.dart` no terminal do Flutter, ele **não cria arquivos `.dart` em pastas aleatórias**.
   - A única coisa que ele faz é ir em `http://localhost:5000/swagger/v1/swagger.json` e salvar a cópia atualizada dentro de `lib/core/network/swagger_v1.json` (em 1 segundo, gastando zero tokens).
2. **`AGENTS.md` do Front (A Regra da IA — Linha 58):**
   - É onde está escrito a lei que proíbe jogar arquivos em pasta aleatória:
     > *"lib/features/<nome_da_feature>: Isolada por contexto de negócio contendo suas próprias subpastas: presentation/, controllers/ e models/."*
3. **`ARCHITECTURE.md` do Front (O Mapa de Endereços — Linhas 37 a 80):**
   - É onde está desenhada a árvore exata dizendo em qual pasta cada DTO mora:

| Grupo / Schema no `swagger_v1.json` | Pasta Configurada no `ARCHITECTURE.md` do Front |
| :--- | :--- |
| **`Auth`** (`AuthResponseDto`, `LoginApiRequest`, `SteamPreviewDto`) | `lib/features/auth/models/` |
| **`Invitations`** (`InvitationDto`, `CreateInvitationApiRequest`) | `lib/features/admin/invitations/models/` |
| **`Users`** (`MemberDto`, `RecoveryRequestAdminDto`) | `lib/features/members/models/` |
| **`Tournaments` (Criação/Lista/Chaves)** (`TournamentDto`, `BracketSlotDto`, `TournamentConfigurationOptionsDto`) | `lib/features/admin/tournaments/models/` |
| **`Matches` & `Standings`** (`MatchSeriesDto`, `TournamentStandingsDto`) | `lib/features/tournaments/models/` |
| **Eventos SSE (Sininho/SnackBar)** (`TournamentWebhookEventDto`) | `lib/core/network/tournament_webhook_event_dto.dart` |

> **Nota legal:** Inclusive olhando os seus arquivos `tournament_dto.dart`, `bracket_slot_dto.dart` e `tournament_webhook_event_dto.dart` no Flutter agora, **eles já estão 100% sincronizados** com todos os campos novos que criamos hoje no backend (`groupCount`, `groupStageSeriesFormat`, `playoffSeriesFormat`, `grandFinalSeriesFormat`, `version` e o serviço SSE `tournament_events_service.dart`)!

---

## 4. Os 4 Comandos "Modo Máquina" para Verificar e Testar sem Gastar Token

Sempre que você quiser testar ou verificar algo sem gastar token de IA:

### 1. Painel Visual do Swagger UI (Testar Rotas no Navegador)
👉 Abra: **`http://localhost:5000/swagger`**
- Faça login em `/api/v1/auth/login`, copie o `token` e cole no botão verde **`Authorize`**.
- Clique em **Try it out -> Execute** em qualquer rota para testar criação de torneio, início e resultados.

### 2. Atualizar o Contrato `swagger_v1.json` do Front em 1 Comando
```powershell
Invoke-WebRequest -Uri "http://localhost:5000/swagger/v1/swagger.json" -OutFile "c:\Users\gabri\Desktop\TournametCreaterFront\lib\core\network\swagger_v1.json"
```

### 3. Testar o Sininho / SnackBar (SSE) ao Vivo pelo Terminal
```powershell
curl.exe -N http://localhost:5000/api/v1/tournaments/events
```
Qualquer partida finalizada ou campeonato iniciado mostrará o JSON pulando na hora nessa tela.

### 4. Validar todas as 125 Regras do Backend em 1 Segundo
```powershell
dotnet test --no-restore
```
