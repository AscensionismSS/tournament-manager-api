# Guia Definitivo de Engenharia, Segurança e Arquitetura no Backend (C# / .NET 10 & DDD)

Este guia foi elaborado para transformar os problemas reais resolvidos neste projeto em uma referência técnica e pedagógica definitiva. Aqui você aprenderá **o que são**, **como funcionam**, **por que acontecem** e **como nunca mais cometer** esses erros em projetos de alta escala e missão crítica.

---

## Sumário
1. [Vulnerabilidades de Autorização: IDOR e BOLA](#1-vulnerabilidades-de-autorização-idor-e-bola)
2. [O Princípio do Menor Privilégio e Segregação de Funções](#2-o-princípio-do-menor-privilégio-e-segregação-de-funções)
3. [Algoritmos de Ordenação e Quebra de Transitividade no IntroSort (.NET)](#3-algoritmos-de-ordenação-e-quebra-de-transitividade-no-introsort-net)
4. [Concorrência, Condições de Corrida e Isolamento de I/O em Disco](#4-concorrência-condições-de-corrida-e-isolamento-de-io-em-disco)
5. [Invariantes Ricas e Máquinas de Estado em Domain-Driven Design (DDD)](#5-invariantes-ricas-e-máquinas-de-estado-em-domain-driven-design-ddd)
6. [Trilha de Estudos e Livros Recomendados](#6-trilha-de-estudos-e-livros-recomendados)
7. [Checklist Prático para Code Reviews Futuros](#7-checklist-prático-para-code-reviews-futuros)

---

## 1. Vulnerabilidades de Autorização: IDOR e BOLA

### O que é IDOR e BOLA?
- **IDOR (Insecure Direct Object Reference)**: Ocorre quando um sistema recebe um identificador fornecido pelo cliente (como um GUID, ID numérico ou string) e acessa diretamente um recurso no banco ou na memória sem validar se quem fez a requisição é o legítimo dono daquele recurso.
- **BOLA (Broken Object Level Authorization)**: É a evolução moderna do IDOR na classificação da **OWASP API Security Top 10 (#1)**. Representa a falha mais comum e crítica em APIs modernas.

### O Caso Prático no SignalR (`TournamentDraftHub`)
Considere o endpoint WebSockets original:

```csharp
// ❌ CÓDIGO VULNERÁVEL (IDOR / BOLA)
public async Task JoinUserChannel(Guid userId)
{
    string groupName = GetUserGroupName(userId);
    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
}
```

#### Como um invasor explorava isso?
No SignalR/WebSockets, uma vez conectado, o atacante abria o console do navegador e executava:
```javascript
// O atacante escuta os eventos pessoais de outro usuário (picks táticos, notificações confidenciais)
hubConnection.invoke("JoinUserChannel", "guid-de-outro-usuario-alvo");
```
O servidor aceitava cegamente o `userId` enviado e colocava o atacante dentro da sala privada da vítima!

#### A Correção Definitiva (Defesa em Profundidade):
```csharp
// ✅ CÓDIGO SEGURO E AUDITADO
public async Task JoinUserChannel(Guid userId)
{
    var callerUserId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? Context.User?.FindFirst("sub")?.Value;

    bool isSuperAdmin = Context.User?.IsInRole("SuperAdmin") == true;

    // Regra: O usuário só pode assinar seu próprio canal, a menos que seja SuperAdmin
    if (!Guid.TryParse(callerUserId, out var authenticatedUserId) ||
        (authenticatedUserId != userId && !isSuperAdmin))
    {
        _logger.LogWarning("Tentativa de IDOR bloqueada. Conexão {ConnId} tentou espionar {Target}", Context.ConnectionId, userId);
        throw new HubException("Acesso negado: Você não tem permissão para assinar o canal pessoal de outro usuário.");
    }

    await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId));
}
```

> **Regra de Ouro**: Nunca confie em IDs vindos do cliente. A identidade deve ser sempre extraída do token JWT validado criptograficamente no servidor (`Context.User` / `User.Claims`).

---

## 2. O Princípio do Menor Privilégio e Segregação de Funções

### O Conceito
O **Princípio do Menor Privilégio (PoLP - Principle of Least Privilege)** estabelece que qualquer módulo, usuário ou perfil deve ter acesso apenas às informações e recursos estritamente necessários para desempenhar sua função específica.

### O Erro no Escopo de Admins
No sistema existiam duas roles:
1. `TournamentAdmin`: Administrador local de um torneio específico (ex: gerencia inscrições, resultados de partidas).
2. `SuperAdmin`: Administrador global da plataforma (gerencia infraestrutura, contas, logins e segurança).

A listagem de tokens de recuperação de senha (`/recovery-requests`) e a aprovação de troca de Steam estavam com:
```csharp
// ❌ RISCO DE ESCALAÇÃO DE PRIVILÉGIO
[Authorize(Roles = "TournamentAdmin,SuperAdmin")]
[HttpGet("recovery-requests")]
```
**O Impacto**: Um organizador de campeonato comum conseguia ver tokens de redefinição de senha e URLs de recuperação pertencentes a qualquer outro usuário ou administrador do sistema!

### A Correção:
```csharp
// ✅ SEGREGAÇÃO ESTRITA
[Authorize(Roles = "SuperAdmin")]
[HttpGet("recovery-requests")]
```
Qualquer operação que toque em credenciais, tokens de autenticação ou controle global do draft deve ser restrita exclusivamente a `SuperAdmin`.

---

## 3. Algoritmos de Ordenação e Quebra de Transitividade no IntroSort (.NET)

### Por que o `.Sort()` do .NET pode lançar `ArgumentException`?
No .NET (`Array.Sort` e `List<T>.Sort`), o algoritmo padrão é o **IntroSort** (uma combinação de QuickSort, HeapSort e InsertionSort). Para que o algoritmo funcione, o comparador fornecido (`IComparer<T>` ou delegate `(a, b) => ...`) **precisa obedecer rigorosamente às propriedades matemáticas de uma Relação de Ordem Fraca Estrita (Strict Weak Ordering)**:
1. **Reflexividade**: `Compare(x, x) == 0`
2. **Antissimetria**: `Compare(x, y) == -Compare(y, x)`
3. **Transitividade de Menor que**: Se `A < B` e `B < C`, então obrigatoriamente `A < C`.
4. **Transitividade de Equivalência**: Se `A == B` e `B == C`, então `A == C`.

### Como o Confronto Direto (Head-to-Head) quebra a matemática?
Imagine 3 equipes empatadas em pontos e saldo de mapas:
- Time A venceu Time B: `Compare(A, B) < 0` (A fica na frente)
- Time B venceu Time C: `Compare(B, C) < 0` (B fica na frente)
- Time C venceu Time A: `Compare(C, A) < 0` (C fica na frente de A, ou seja, `A > C`)

Isso forma um **ciclo (Pedra-Papel-Tesoura)**:
`A < B` e `B < C`, mas `A > C`!

Quando o IntroSort do .NET particiona o array e tenta posicionar os pivôs, ele detecta que o comparador produz contradições lógicas e lança:
> `System.ArgumentException: Unable to sort because the IComparer.Compare() method returns inconsistent results.`

### A Solução por Particionamento Hierárquico (Mini-Tabela / GroupBy)
Em vez de tentar resolver empates múltiplos através de um comparador 1v1 dentro de um único `Sort()`, usamos **Particionamento Hierárquico**:

```
Todos os Times
   │
   ├── 1. Agrupa por Pontos (Decrescente)
   │      │
   │      └── Se empate > 1 time:
   │             2. Agrupa por Saldo de Mapas (Decrescente)
   │                    │
   │                    └── Se empate > 1 time:
   │                           3. Avalia Confronto Direto (Mini-Tabela)
   │                              - Se 2 times: 1v1 direto
   │                              - Se >= 3 times: Calcula vitórias internas no subgrupo
   │                                 • Se houver separação: agrupa por vitórias internas
   │                                 • Se ciclo perfeito: passa para Critério 4
   │                                        │
   │                                        └── 4. Métricas Escalares (Transitivas por natureza!)
   │                                              - Tempo de Vitória (Escalar long)
   │                                              - Saldo de Kills (Escalar int)
   │                                              - TeamId (Guid determinístico)
```

**Por que essa abordagem é à prova de falhas?**
Escalares numéricos (`long`, `int`, `Guid`) são matematicamente transitivos. É impossível ocorrer ciclo comparando números absolutos de vitórias ou tempo em segundos!

---

## 4. Concorrência, Condições de Corrida e Isolamento de I/O em Disco

### O Problema do Diretório Compartilhado
No parser de replay em Go (`parsegodota`), o executável esperava a pasta fixa `tools/ParseGoDota/convertido/` para salvar o JSON resultante.

```
Upload 1 (Partida A) ───┐
                        ├──► grava em tools/ParseGoDota/convertido/
Upload 2 (Partida B) ───┘
```

#### Condição de Corrida (Race Condition):
1. Usuário 1 envia Replay A. O parser inicia.
2. Simultaneamente, Usuário 2 envia Replay B. O parser de B inicia.
3. O parser de B termina primeiro e grava `convertido/123.json`.
4. O processo de A acorda, lê a pasta `convertido/`, encontra `123.json` (que é de B!), consome os dados errados e apaga o arquivo!
5. Quando o processo de B vai ler seu arquivo, ele não existe mais! Ambos falham ou trocam de dados!

### A Solução: Isolamento por Sandbox (Diretório por GUID)
Cada requisição de parsing cria um **espaço de trabalho temporário exclusivo**:

```csharp
var runId = Guid.NewGuid().ToString("N");
var isolatedRunDir = Path.Combine(_toolsDir, "runs", runId);
var replaysDir = Path.Combine(isolatedRunDir, "replays");
var convertidoDir = Path.Combine(isolatedRunDir, "convertido");
Directory.CreateDirectory(replaysDir);
Directory.CreateDirectory(convertidoDir);

try
{
    // O processo roda com WorkingDirectory apontando para a pasta ISOLADA
    var psi = new ProcessStartInfo
    {
        FileName = _executablePath,
        WorkingDirectory = isolatedRunDir, // <--- Sandbox por GUID!
        Arguments = Path.Combine("replays", tempFileName)
    };
    ...
}
finally
{
    // Limpeza atômica e recursiva da sandbox
    Directory.Delete(isolatedRunDir, recursive: true);
}
```
**Resultado**: Podem ocorrer 100 uploads simultâneos. Cada um roda em seu próprio diretório sem nunca interferir nos demais.

---

## 5. Invariantes Ricas e Máquinas de Estado em Domain-Driven Design (DDD)

### O que são Invariantes de Domínio?
Invariantes são **regras de negócio que devem permanecer verdadeiras em 100% do tempo de vida do agregado**. Elas não são meras validações de formulário; são leis fundamentais do modelo.

### Caso 1: Bug do Empate 1x1 em MD2 (Melhor de 2)
Em uma série MD2, os placares possíveis são:
- 2x0 (Vitória do Time A)
- 0x2 (Vitória do Time B)
- 1x1 (Empate válido)

No método `UpdateGame()` do `MatchSeries`, se um mapa fosse editado e o placar ficasse 1x1, o código verificava apenas:
```csharp
// ❌ REGRESSÃO DE ESTADO INDEVIDA
if (ScoreTeamA >= winsNeeded) FinalizeSeries(...);
else if (ScoreTeamB >= winsNeeded) FinalizeSeries(...);
else Status = MatchSeriesStatus.InProgress; // <--- Regredia para InProgress quando 1x1!
```
**A Invariante**: Uma série Bo2 com 2 mapas disputados já atingiu seu limite máximo de jogos. O status deve ser `Completed`, com `WinnerTeamId = null` e `LoserTeamId = null`. Ela nunca pode regredir para `InProgress`.

### Caso 2: Trava de Integridade Topológica (Rollback de Grupos vs Playoffs)
Um torneio é uma máquina de estados:
`Draft` ➔ `Registration` ➔ `Drafting` ➔ `GroupStage` ➔ `Playoffs` ➔ `Completed`

Se as partidas dos Playoffs já foram geradas a partir dos resultados da Fase de Grupos:
- Permitir o Rollback de uma partida de grupo alteraria a classificação dos grupos.
- Os times já promovidos na chave dos Playoffs ficariam inconsistentes com a nova tabela!

```csharp
// ✅ PROTEÇÃO DE INVARIANTE TOPOLÓGICA NO SERVIÇO DE PROGRESSÃO
if (currentSlot == null) // Partida de Fase de Grupos
{
    if (tournament.Status == TournamentStatus.Playoffs || tournament.Status == TournamentStatus.Completed)
    {
        throw new DomainException($"Não é possível reverter a partida de grupo '{seriesToUndo.SlotCode}' pois os Playoffs já foram iniciados ou concluídos. Reverta os Playoffs para a Fase de Grupos primeiro.");
    }
}
```

### Caso 3: Desacoplamento de Políticas de Acesso no Domínio
O usuário solicitou:
> "O admin pode trocar o nome a hora que quiser, já vem com nome padrão para não ficar como null".

Anteriormente, o domínio bloqueava qualquer alteração de nome se o draft não estivesse concluído e se o time não tivesse 5 jogadores:
```csharp
// ✅ DOMÍNIO DISTINGUE POLÍTICA DE ADMIN VS POLÍTICA DE PARTICIPANTE
public void RenameTeam(Guid teamId, string? newName, int expectedVersion, string? actingUserId = null, bool isAdmin = true)
{
    ValidateVersion(expectedVersion);
    var team = _teams.FirstOrDefault(t => t.Id == teamId) ?? throw new DomainException("Equipe não encontrada.");

    if (!isAdmin)
    {
        if (State != DraftState.Completed)
            throw new DomainException("O capitão só pode alterar o nome da equipe após a conclusão do draft.");

        if (team.Starters.Count < 5)
            throw new DomainException($"A equipe precisa ter no mínimo 5 jogadores para ser renomeada pelo capitão.");
    }

    // Garante fallback para nome padrão caso venha vazio ou nulo
    var effectiveName = string.IsNullOrWhiteSpace(newName) ? $"Time {team.PickOrder + 1}" : newName.Trim();
    team.Rename(effectiveName);
    DraftVersion++;
}
```

---

## 6. Trilha de Estudos e Livros Recomendados

Para atingir a maestria em arquitetura backend, segurança e C#, recomendamos seguir esta trilha:

### 📚 Livros Fundamentais
1. **Domain-Driven Design: Atacando as Complexidades no Coração do Software** (*Eric Evans*)
   - *Por que ler*: O livro que criou o DDD. Ensina a pensar no modelo conceitual, Ubiquitous Language, Aggregates, Entities e Value Objects.
2. **Implementando Domain-Driven Design (O Livro Vermelho)** (*Vaughn Vernon*)
   - *Por que ler*: Traduz a teoria do Evans em código prático, detalhando tratamento de invariantes, concorrência otimista e repositórios.
3. **CLR via C#** (*Jeffrey Richter*)
   - *Por que ler*: O livro definitivo sobre como o .NET funciona por baixo dos panos (threads, memória, garbage collection, algoritmos de ordenação e delegates).
4. **Clean Architecture: O Guia do Artesão para Estrutura e Design de Software** (*Robert C. Martin*)
   - *Por que ler*: Ensina a regra da dependência (o Domínio nunca depende da Infraestrutura ou da Web).
5. **Concurrency in C# Cookbook** (*Stephen Cleary*)
   - *Por que ler*: O guia prático definitivo para dominar `async/await`, `CancellationToken`, canais, tarefas assíncronas e I/O não bloqueante.

### 🌐 Guias Oficiais e Práticas de Segurança
- **OWASP API Security Top 10**: Leitura obrigatória anual (especialmente API1: BOLA, API2: Broken Authentication, API5: Broken Function Level Authorization).
- **Microsoft Learn - ASP.NET Core SignalR Security**: Guia oficial de autenticação e proteção de canais em tempo real.

---

## 7. Checklist Prático para Code Reviews Futuros

Antes de abrir qualquer Pull Request no backend, faça a si mesmo as seguintes perguntas:

### 🛡️ Checklist de Segurança (BOLA / Auth):
- [ ] Os endpoints que alteram recursos privados extraem a identidade do usuário diretamente das Claims autenticadas (`Context.User` / `User.FindFirst`)?
- [ ] Se o endpoint recebe um `{id}` na rota, existe checagem garantindo que o recurso pertence ao usuário solicitante ou que o usuário tem role administrativa adequada?
- [ ] Os hubs do SignalR validam permissões ao entrar em grupos (`AddToGroupAsync`)?
- [ ] Tokens de segurança ou URLs de recuperação de senha estão isolados exclusivamente para `SuperAdmin`?

### ⚡ Checklist de Algoritmos e Dados:
- [ ] Qualquer comparador personalizado (`IComparer<T>` ou `.Sort()`) obedece estritamente às 4 regras do IntroSort?
- [ ] Se há comparação de 3 ou mais itens onde A pode ganhar de B e B de C, foi usado particionamento (`GroupBy`) em vez de comparação relativa 1v1?
- [ ] Em empates circulares, existe desempate garantido por métrica escalar única (`long`, `int` ou `Guid`)?

### 💾 Checklist de Concorrência e I/O:
- [ ] Operações com arquivos externos ou ferramentas CLI geram pastas e arquivos com identificadores únicos (GUIDs)?
- [ ] Existe bloco `try { ... } finally { Directory.Delete(sandbox, true); }` para limpar os recursos temporários mesmo em caso de erro?
- [ ] Há `CancellationToken` repassado para timeouts de processos externos para evitar travamentos de processos órfãos no servidor?

### 🏛️ Checklist de DDD:
- [ ] As entidades protegem suas invariantes contra estados inválidos (ex: Bo2 nunca regredindo para `InProgress` quando completado)?
- [ ] Há concorrência otimista implementada (`ExpectedVersion` / `DraftVersion`) em agregados sujeitos a edições concorrentes?
