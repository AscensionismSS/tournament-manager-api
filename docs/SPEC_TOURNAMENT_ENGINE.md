# Especificação Técnica e de Negócio: Motor de Torneios (Dota 2)
**Versão:** 1.1.0  
**Autor:** Antigravity Pairing Agent  
**Público-alvo:** Analista de Sistemas, Arquiteto de Software e Desenvolvedores  
**Stack Base:** C# / .NET 10, ASP.NET Core, SignalR, DDD, Feature-First, Modular Monolith  

---

## 1. Visão Geral do Sistema
O objetivo deste projeto é construir um **Motor de Torneios de Esports (foco inicial em Dota 2)** de alta performance, modular e resiliente, onde o **Backend é o único dono da verdade**. O sistema é responsável por:
1. Gerenciar o ciclo de vida completo do torneio de forma agnóstica à camada visual.
2. Orquestrar a formação de equipes via **Inscrição de Times Fechados** ou **Draft de Jogadores ao Vivo (In-House conduzido pelo Admin)** com comunicação em tempo real via **SignalR**.
3. Gerar tabelas de pontuação e chaves de mata-mata/dupla eliminação (Upper e Lower Bracket com Cross-Bracketing) para **4 a 8 equipes**.
4. Receber resultados de jogos via **Lançamento Manual Simplificado**, **W.O. (Walkover)** ou **Ingestão de JSON via Motor de Replay Parser externo**.
5. Validar e computar o avanço de fase, reversões administrativas (**Rollback**), desempates e consagração do campeão sem intervenção manual propensa a erros.

---

## 2. Princípios Arquiteturais (DDD & Modular Monolith)

### 2.1. Backend Dono da Verdade e Divisão de Agregados
- O frontend (Web, Mobile ou Desktop) é meramente uma camada de apresentação e envio de intenções.
- Nenhuma chave de torneio, avanço de fase ou cálculo de pontuação é executado no cliente.
- Para garantir alta performance no Entity Framework Core e evitar o carregamento de grafos gigantescos em memória (God Aggregate), o domínio é dividido em **3 Raízes de Agregado (`AggregateRoot`) independentes** que se comunicam via Domain Events:
  1. **`DraftSession` (AggregateRoot):** Gerencia a Player Pool, definição e ordenação de Capitães, máquina de estados do Snake Draft, `DraftVersion`, modo de substituição e alocação de Reservas.
  2. **`Tournament` (AggregateRoot):** Gerencia configurações (`SeriesConfig`, `TournamentFormatConfig`), grupos, tabela de classificação e a progressão topológica dos slots das chaves (`BracketSlot`).
  3. **`MatchSeries` (AggregateRoot):** Gerencia uma série específica (MD1, MD2, MD3, MD5), seus mapas individuais (`MatchGame`), validação de vitória, W.O. e metadados de partida (tempo, placar de kills e estatísticas opcionais de jogadores).

### 2.2. Isolamento Modular (Bounded Context)
- O módulo de torneios (`Modules/Tournaments`) é completamente agnóstico a sistemas externos de autenticação, Steam ou motores de leitura de arquivos `.dem` (Replays).
- Jogadores e Capitães são identificados no Domínio por identificadores opacos (`Guid` ou `ExternalUserId`).
- O futuro Motor de Replay Parser não faz parte deste domínio; ele atuará como um produtor externo que envia um DTO padronizado (`MatchResultIngestionDto`) para a camada de aplicação do Torneio.

### 2.3. Separação de Projetos por Fronteira Física
```text
src/
├── Shared/
│   └── BuildingBlocks.Domain/       # Classes base: Entity, AggregateRoot, DomainException, IDomainEvent, Result<T>
│
├── Modules/Tournaments/
│   ├── Tournaments.Contracts/       # DTOs públicos, Contratos de Replay JSON, IntegrationEvents (100% desacoplado)
│   ├── Tournaments.Domain/          # Aggregates (Tournament, DraftSession, MatchSeries), Value Objects, Regras de Chave
│   ├── Tournaments.Application/     # Casos de uso, Commands, Queries, FluentValidation, Handlers
│   └── Tournaments.Infrastructure/  # Persistência EF Core, Mapeamentos Fluent API, Concurrency Tokens, Repositórios
│
└── Host/
    └── TournamentCreator.WebApi/    # ASP.NET Core Web API, Controllers, Hub SignalR, Middlewares Globais
```

---

## 3. Modos de Entrada e Formação de Equipes

### 3.1. Modo Equipes Prontas (Pre-Formed Teams)
- Equipes completas (5 titulares + até 2 reservas opcionais) se inscrevem com Tag e Nome.
- Validação no início: exige entre 4 e 8 equipes confirmadas.

### 3.2. Modo Draft de Jogadores (Admin-Driven In-House Live Draft)
- Jogadores entram individualmente em uma `PlayerPool`.
- **Operador Central:** Quem executa os cliques de escolha no sistema é exclusivamente o Administrador, seguindo a ordem visual do turno do capitão da vez.
- Composição mínima por time: **5 titulares** (1 Capitão + 4 escolhidos no Snake Draft).
- Reservas: **Até 2 reservas facultativos por time** (alocados após os titulares).
- **Etapa 1: Seleção de Capitães e Definição de Ordem (Seed do Draft)**
  - O Administrador seleciona os capitães a partir da Pool de inscritos (cada capitão selecionado funda automaticamente um Time e ocupa a vaga de 1º titular).
  - O Administrador pode renomear as equipes.
  - **Fechamento dos Capitães:** Ao finalizar a seleção de capitães, o Administrador escolhe uma das duas opções para definir a ordem dos turnos (`PickOrder`):
    - *Ordem Manual:* Segue exatamente a sequência definida pelo Admin na tela.
    - *Ordem Sorteada (Randomize):* O backend embaralha a ordem dos capitães (via `Random.Shared`) e fixa a sequência $1 \dots N$ antes de abrir a fase de picks de jogadores.
- **Etapa 2: Snake Draft (Serpentina Conduzida pelo Admin)**
  - O sistema destaca de qual Capitão é o turno atual na interface. O Admin clica no jogador escolhido na Pool para alocá-lo ao time do capitão da vez.
  - Ordem de escolha: $\text{Time } 1 \rightarrow \text{Time } 2 \rightarrow \dots \rightarrow \text{Time } N \rightarrow \text{Time } N \text{ (pick duplo)} \rightarrow \dots \rightarrow \text{Time } 1$.
  - Todos os clientes conectados recebem atualizações instantâneas via SignalR (`PlayerPickedEvent`).
- **Etapa 3: Modo de Substituição / Correção em Tempo Real**
  - Caso um pick seja registrado incorretamente, o Admin ativa o modo de substituição clicando no jogador já alocado em um time.
  - O sistema muda o status temporariamente para `Substituting`, bloqueia avanços de turno normais e restringe a troca apenas com jogadores disponíveis na Pool.
  - Ao confirmar ou cancelar a substituição, o fluxo do draft retorna exatamente ao índice do turno original (`CurrentPickIndex`) sem perder o progresso da serpentina.
- **Etapa 4: Fase de Reservas**
  - Desbloqueada exclusivamente após todos os times atingirem 5 titulares (1 Capitão + 4 Picks).
  - Alocação livre pelo Admin no card de qualquer time (máximo de 2 reservas por equipe), sem obrigação de esgotar a pool.
  - Ações de adição de reservas fora da pool inicial são auditadas e registradas no histórico do Draft.

### 3.3. Proteção contra Concorrência, Double-Click e F5
- **Versionamento de Estado (`DraftVersion`):** Propriedade configurada como *Optimistic Concurrency Token* no EF Core. Cada ação bem-sucedida incrementa a versão atômica. Requisições concorrentes ou cliques duplos com `ExpectedVersion` defasada são rejeitados com `409 Conflict`.
- **Imunidade a F5 (State Recovery):** O estado completo é persistido no banco. Em caso de refresh ou reconexão no SignalR, o cliente recebe o snapshot completo (`DraftStateSnapshotDto`) e sincroniza a tela instantaneamente.

---

## 4. Estrutura de Competição e Chaveamentos (4 a 8 Equipes)

### 4.1. Modo Direto para Playoffs (Single Elimination - Mata-Mata)
- **Restrição:** Exige exatamente 4 ou 8 equipes (não utiliza BYE para manter simetria estrita).
- Formato: Quartas de Final (se 8 times) $\rightarrow$ Semifinais $\rightarrow$ Grande Final.

### 4.2. Modo com Fase de Grupos + Playoffs (Double Elimination)
Suporta de 4 a 8 equipes com transição para chaves Upper e Lower:

| Qtd Equipes | Fase de Grupos / Classificação | Fase Play-In (Se houver) | Playoffs (Upper / Lower Bracket) |
| :---: | :--- | :--- | :--- |
| **4** | Grupo único todos contra todos | Não aplicável | **2 Upper** (1º x 2º) e **2 Lower** (3º x 4º) |
| **5** | Grupo único todos contra todos | **4º vs 5º** jogam partida eliminatória (perdedor é eliminado) | **2 Upper** (1º x 2º) e **2 Lower** (3º vs Vencedor do Play-In) |
| **6** | Grupo único todos contra todos | Não aplicável | **4 Upper** (1º x 4º, 2º x 3º) e **2 Lower** (5º e 6º aguardam derrotados da Upper) |
| **7** | Grupo único todos contra todos | **6º vs 7º** jogam partida eliminatória (perdedor é eliminado) | **4 Upper** (1º a 4º) e **2 Lower** (5º + Vencedor do Play-In aguardam derrotados da Upper) |
| **8** | **Opção 1:** 2 Grupos de 4 (A e B)<br>**Opção 2:** Grupo Único de 8 | Não aplicável (Todos avançam: 4 Upper e 4 Lower) | **Opção 1 (2 Grupos):**<br>• Upper: 1ºA x 2ºB (U1) e 1ºB x 2ºA (U2)<br>• Lower: 3ºA x 4ºB (L1) e 3ºB x 4ºA (L2)<br>**Opção 2 (Grupo Único):**<br>• Upper: 1º x 4º (U1) e 2º x 3º (U2)<br>• Lower: 5º x 8º (L1) e 6º x 7º (L2) |

- **Regra de Inversão na Queda (Cross-Bracketing Drop-Down):** Para evitar que duas equipes se enfrentem novamente logo na sequência ao cair da Upper para a Lower Bracket:
  - O perdedor do confronto Upper 1 (U1) cai obrigatoriamente para enfrentar o vencedor da Lower 2 (L2).
  - O perdedor do confronto Upper 2 (U2) cai obrigatoriamente para enfrentar o vencedor da Lower 1 (L1).
- **Nota Especial (Final Direta):** Também é suportado o modo Grupo Único $\rightarrow$ Final Direta, onde apenas o 1º e o 2º colocados avançam para disputar o título.

---

## 5. Regras de Séries, Resultados e Desempates

### 5.1. Matriz de Compatibilidade de Séries (Invariante DDD)
A complexidade das séries nunca pode regredir entre as fases, e saltos desbalanceados são proibidos pelo domínio:

$$\text{Fase de Grupos} \le \text{Playoffs} \le \text{Grande Final}$$

```text
[Grupos: MD1] ──► [Playoffs: MD1] ──► [Final: MD1 ou MD3]
[Grupos: MD1] ──► [Playoffs: MD3] ──► [Final: MD3 ou MD5]
[Grupos: MD2] ──► [Playoffs: MD3] ──► [Final: MD3 ou MD5]
[Grupos: MD3] ──► [Playoffs: MD3] ──► [Final: MD3 ou MD5]
[Grupos: MD3] ──► [Playoffs: MD5] ──► [Final: MD5]
[Direto Playoff] ─► [Playoffs: MD1] ──► [Final: MD1 ou MD3]
[Direto Playoff] ─► [Playoffs: MD3] ──► [Final: MD3 ou MD5]
```
*Proibição Restrita:* Se os playoffs forem MD1, a final **nunca** pode ser MD5.

### 5.2. Pontuação da Fase de Grupos e Saldo de Mapas
- **Séries MD1 e MD3:** Vitória na série = 3 pontos, Derrota na série = 0 pontos.
- **Séries MD2 (Padrão Dota 2):** Vitória 2x0 = 3 pontos | Empate 1x1 = 1 ponto para cada | Derrota 0x2 = 0 pontos.
- **Contabilização de Mapas (`MapWins` / `MapLosses`):** Independente da pontuação da série, a tabela do grupo armazena individualmente quantos mapas cada equipe venceu e perdeu (ex: vencer uma MD3 por 2x1 soma +3 pontos, +2 `MapWins` e +1 `MapLosses`, gerando `MapDifferential = +1`).

### 5.3. Critérios de Desempate (Tiebreakers)
A ordenação da tabela de grupos aplica os seguintes critérios em cascata:
1. **Critério 1:** Pontuação total (`Points`).
2. **Critério 2:** Saldo de Mapas / Vitórias líquidas de mapas (`MapDifferential = MapWins - MapLosses`).
3. **Critério 3:** Confronto Direto (`Head-to-Head`) entre as equipes empatadas.
   - *Regra de Empate Triplo/Circular:* Caso 3 ou mais equipes estejam empatadas e apresentem um ciclo de vitórias mútuas (Time A venceu B, B venceu C, C venceu A), o sistema ignora o Critério 3 e avança automaticamente para o Critério 4.
4. **Critério 4:** Métricas de Partida (Menor Tempo de Jogo acumulado em mapas vencidos $\rightarrow$ Maior Saldo de Kills da equipe).
5. **Critério 5 (Persistindo empate em linha de corte de eliminação/Upper):** Partida extra de desempate (*Tiebreaker Match* em MD1).

### 5.4. Modos de Registro de Resultado (Manual, Replay Parser e W.O.)
Cada jogo (`MatchGame`) dentro de uma série pode ser concluído pelo Admin por três vias:

| Modo de Entrada (`ResultSource`) | Dados Obrigatórios | Dados Opcionais / Comportamento no Motor |
| :--- | :--- | :--- |
| **1. Manual Simplificado (`Manual`)** | `WinnerTeamId`, `DurationSeconds`, `TeamAKills`, `TeamBKills` | Estatísticas individuais de jogadores (`PlayerStats`) ficam nulas/vazias sem quebrar nenhuma regra de negócio. |
| **2. Upload de Replay (`ReplayParser`)** | Todos os dados do modo manual + `DotaMatchId` (se houver) + Lista de `PlayerMatchStats` | Recebe o JSON completo gerado pela engine externa de parser (Kills, Deaths, Assists, HeroId, NetWorth, GPM/XPM de cada jogador). |
| **3. W.O. (`Walkover`)** | `WinnerTeamId`, `ForfeitedTeamId`, `Reason` | **Proteção de Desempate:** Soma a vitória de mapa/série para o vencedor, mas atribui **Penalidade Neutra de Estatística** (Kills = 0 e Não contabiliza para média de vitória mais rápida), impedindo que uma vitória por W.O. de 00:00 minutos trapaceie o Critério 4 de desempate. |

### 5.5. Reversão Administrativa de Resultado (Rollback)
O Administrador pode reverter ou editar o resultado de uma série finalizada (`RevertMatchSeriesResultCommand`), desde que a partida subsequente da chave (para a qual o vencedor ou o perdedor foram promovidos) ainda esteja com status `Pending` (não tenha nenhum jogo iniciado).

### 5.6. Regras da Grande Final (Double Elimination)
- A Grande Final é disputada em série única (MD3 ou MD5 conforme configuração).
- **Sem vantagem de mapas:** O finalista vindo da Upper Bracket não recebe mapa de bonificação (padrão competitivo The International).

---

## 6. Roteiro de Implementação em Fases

1. **Fase 1 - Core Domain:**
   - Raízes de Agregado separadas (`Tournament`, `DraftSession`, `MatchSeries`).
   - Value Objects (`SeriesConfig`, `TournamentFormatConfig`, `GameMetrics`, `BracketSlot`).
   - Invariantes de validação de séries, chaveamento simétrico, Cross-Bracketing e desempates.
2. **Fase 2 - Draft Engine & SignalR:**
   - Máquina de estados do Draft conduzida pelo Admin (Capitães c/ Ordem Manual ou Sorteada, Snake, Substituição, Reservas).
   - Hub SignalR para broadcast de eventos em grupo (`TournamentGroup`) e snapshot de recuperação (`DraftStateSnapshotDto`).
3. **Fase 3 - Casos de Uso & Contratos de Ingestão (Application Layer):**
   - Commands e Handlers para operação manual, W.O., Rollback e contrato de recepção do JSON do Replay Parser (`Tournaments.Contracts`).
4. **Fase 4 - Persistência & Endpoints:**
   - Mapeamentos EF Core (com `IsConcurrencyToken` no `DraftVersion` e JSONB para estatísticas ricas do Replay Parser), Controllers enxutas e Middlewares globais de exceção.
