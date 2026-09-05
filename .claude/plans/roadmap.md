# CoreFinance — Roadmap de evolução (Auth → Analytics → Produtividade → Plataforma)

> Status geral: **fases 1 e 2 concluídas** (03 e 04/09/2026) · fases 3 e 4 pendentes.

## Contexto

O CoreFinance nasceu como um MVP funcional e bem estruturado: `FixedAccount` + `Payment`, painel do mês, dashboard anual de barras, Clean Architecture respeitada (API → Application → Domain ← Infra), Result Pattern, FluentValidation e middleware global de erro.

O que falta para o sistema "encorpar": ele era **single-user implícito** (resolvido na fase 1), a análise para em um gráfico de barras, e fechar o mês ainda é 100% manual. Os blocos priorizados foram **analytics + produtividade** e **plataforma**, com **autenticação já na fase 1** — porque `UserId` toca todas as entidades e adiar significaria retrabalho em tudo que fosse criado depois.

---

## Roadmap

| Fase | Entrega | Status |
|---|---|---|
| **1. Autenticação e multiusuário** | Cadastro/login JWT, `UserId` em todas as entidades, global query filter, telas de login/registro, guarda de rota | ✅ **Concluída** |
| **2. Analytics** | Linha de média no gráfico de barras, pizza por categoria/conta, comparativo mês-a-mês e ano-vs-ano (variação %), top 5 gastos, projeção de fechamento do ano, média móvel | ✅ **Concluída** |
| **3. Produtividade** | "Copiar mês anterior", valor sugerido = último pago, busca/filtros/paginação em Pagamentos, export CSV/Excel/PDF | ⬜ Pendente |
| **4. Plataforma** | Serilog estruturado, health checks, notificações de vencimento (HostedService), toggle de tema persistido (pedido no PRD e não implementado), testes xUnit + WebApplicationFactory | ⬜ Pendente |

Detalhamento abaixo. Fases 3 e 4 serão detalhadas quando chegarem.

> **Trilha paralela:** [Observabilidade](Observabilidade/00-visao-geral.md) — laboratório didático de health checks, logs, métricas, tracing, dashboards e alertas (Serilog + OpenTelemetry + Grafana/Prometheus/Loki/Tempo), rodando no mesmo `docker-compose.yml` atrás do profile `obs`. Não bloqueia as fases 3 e 4 e **absorve** dois itens da fase 4: *Serilog estruturado + correlation id* e *health checks*.

---

## ✅ Fase 1 — Autenticação e multiusuário (CONCLUÍDA)

Entregue na branch `feat/autenticacao-multiusuario`, commit `328ec36` — 55 arquivos.

**Abordagem adotada:** `User` como entidade de domínio própria + BCrypt + JWT, seguindo os padrões já usados no projeto. ASP.NET Identity foi descartado: traria 7 tabelas e um modelo de persistência estranho ao resto do código, contra a regra "código simples e objetivo / evitar overengineering" de `docs/RULES.md`.

**Isolamento:** global query filter do EF Core por `UserId` + carimbo automático no `SaveChangesAsync`. Nenhum service precisa lembrar de filtrar — `PainelService` e `DashboardService` viraram multi-tenant sem uma linha alterada.

### O que foi entregue

**Domain**
- `Common/OwnedEntity.cs` — `UserId` + `DefinirDono(Guid)`; `FixedAccount` e `Payment` passaram a herdar dela.
- `Entities/User.cs` — `Name`, `Email` (normalizado), `PasswordHash`, `Active`.
- `Interfaces/ICurrentUser.cs`, `Interfaces/Security/IPasswordHasher.cs`, `Interfaces/Security/IJwtTokenGenerator.cs`, `Interfaces/Repositories/IUserRepository.cs`.

**Infra**
- `Data/AppDbContext.cs` — `HasQueryFilter` em `FixedAccount`/`Payment` e override de `SaveChangesAsync` carimbando o dono.
- `Data/AppDbContextFactory.cs` — `IDesignTimeDbContextFactory`, obrigatório para o `dotnet ef` funcionar com o novo construtor.
- `Data/Configurations/UserConfiguration.cs` + `UserId` (índice e FK) nas configurations existentes.
  A FK de `Payments.UserId` usa `DeleteBehavior.NoAction` — `Users → FixedAccounts → Payments` já é um caminho de cascata e o SQL Server recusa múltiplos (erro 1785).
- `Repositories/UserRepository.cs`, `Security/PasswordHasher.cs`, `Security/JwtTokenGenerator.cs`, `Security/JwtOptions.cs` (falha rápido se `Jwt:Key` faltar ou tiver menos de 32 chars).
- Migration `AddUsersAndOwnership` — cria `Users`, insere o usuário seed, adiciona `UserId` com default para preencher as linhas antigas, remove os defaults, cria índices e FKs.

**Application** — `Auth/` completo: DTOs, validators (senha ≥ 6), `AuthService` (`RegistrarAsync`, `LoginAsync` com mensagem genérica, `ObterPerfilAsync`).
`PaymentService` passou a validar a posse da conta fixa referenciada.

**API** — `Services/CurrentUser.cs` (claim `sub`), `Extensions/AuthenticationExtensions.cs` (`MapInboundClaims = false`, `ClockSkew = Zero`), Swagger com botão *Authorize*, `AuthController` (`[AllowAnonymous]` **por action**, não na classe — na classe o `GET /api/auth/eu` ficaria aberto), `[Authorize]` no `BaseController`, `UseAuthentication()` antes de `UseAuthorization()`.

**Frontend** — `authService.ts`, `AuthContext`/`useAuth`, interceptors de request (Bearer) e response (401 → `/login`), `AppShell` com guarda de rota (rotas públicas renderizam antes do gate de carregamento), telas `/login` e `/registrar`, rodapé da sidebar com usuário e "Sair", e o proxy `api/[...path]/route.ts` repassando o header `authorization`.

### Pendências herdadas da fase 1

- [ ] Rodar `dotnet ef database update -p src/CoreFinance.Infra -s src/CoreFinance.API` com o SQL Server no ar. Os dados existentes passam a pertencer a **admin@corefinance.local / corefinance123** — trocar essa senha depois.
- [ ] Tirar a chave JWT de `appsettings.json` / `docker-compose.yml` e usar secret em produção.

---

## ✅ Fase 2 — Analytics (CONCLUÍDA)

Objetivo: sair do "um gráfico de barras" para leitura de tendência. O domínio não mudou — tudo é leitura agregada em `DashboardService`, e nenhuma migration foi necessária.

**Decisão de escopo:** o plano previa um endpoint separado `GET /api/dashboard/por-conta`. Tudo acabou entregue **no `GET /api/dashboard/anual` existente**, porque todos os blocos da tela respondem aos mesmos três filtros (ano, conta fixa, avulsas). Um segundo endpoint significaria duplicar filtro, hook e o risco de os dois painéis divergirem — e a tela carrega em uma requisição só.

### O que foi entregue

**Application** (`Dashboard/Dtos/` + `Services/DashboardService.cs`)
- `DashboardAnualDto` ganhou `MediaMensal`, `ProjecaoAno`, `Comparativo`, `PorConta` e `TopGastos`.
- `MesValorDto` ganhou `MediaMovel` e `Projetado` (ambos anuláveis: `null` significa "não se aplica a este mês").
- `ComparativoDto`, `ContaValorDto`, `TopGastoDto` — novos.
- Regras de cálculo que valem registro:
  - **Média mensal** ignora meses sem lançamento; senão janeiro em branco derruba o patamar do ano inteiro.
  - **Média móvel (3 meses)** só aparece quando a janela inteira já aconteceu **e** começa depois do primeiro mês com lançamento — evita a curva despencando por causa dos zeros de quem começou a usar o sistema no meio do ano.
  - **Projeção** = realizado até o mês corrente + média × meses que faltam. Em ano fechado (ou futuro) é igual ao total, e nenhuma barra fantasma é desenhada.
  - **Comparativo anual** compara janeiro→mês de referência dos dois anos (não o ano inteiro contra um ano parcial). Exige buscar também os lançamentos de `ano - 1`.
  - **Variação percentual** é `null` quando o período anterior é zero — não existe "aumento de 100%" a partir do nada.
  - **Pizza**: acima de 6 contas, o excedente vira a fatia "Outras"; lançamento sem conta fixa aparece como "Avulsas".

**Frontend** (`web/src/components/dashboard/`)
- `format.ts` — `NOMES_MESES`, `formatarBRL`, `formatarPercentual` compartilhados (antes cada componente tinha a própria cópia).
- `GraficoBarras.tsx` — virou `ComposedChart`: barra real + barra de projeção (mesmo `stackId`, para ocuparem o mesmo espaço do mês) + linha de média móvel + `ReferenceLine` tracejada âmbar com o rótulo "Média R$ …". O tooltip mostra a distância para a média (vermelho acima, verde abaixo).
- `GraficoPizza.tsx` — donut com legenda em lista (nome, % e valor).
- `TopGastos.tsx` — ranking com barra de proporção.
- `ComparativoCards.tsx` — dois cards com variação; **gastar menos é verde**, gastar mais é vermelho (o inverso da convenção de bolsa, proposital).
- `dashboard/page.tsx` — média agora vem do backend (o `reduce` local saiu), card "Mês atual" virou "Projeção do ano", e a tabela ganhou a coluna "vs média".

### Verificação executada

Banco descartável no LocalDB (`CoreFinanceFase2`), API em `localhost:5188`, dados semeados via API: aluguel + internet de jan a set/2026, um lançamento avulso de R$ 5.000 em março e aluguel o ano inteiro em 2025.

| Cenário | Resultado |
|---|---|
| Ano corrente | média 1.655,56 (só os 9 meses com lançamento), projeção 19.866,68 = 14.900 + 3 × média, barras fantasma em out/nov/dez |
| Média móvel | `null` em jan/fev (janela incompleta) e out–dez (futuro); 2.766,67 no pico de março |
| Comparativo | set vs ago = 0%; acumulado 2026 (14.900) vs mesmo período de 2025 (8.100) = +83,95% |
| Ano fechado (2025) | projeção = total, nenhum mês projetado, variação anual `null` (não há 2024) |
| Ano vazio (2024) | tudo zerado, listas vazias, nenhuma variação — sem divisão por zero |
| Filtros | por conta fixa e "só fixas" recalculam média, projeção e pizza corretamente |

`dotnet build` e `npm run build` limpos. Banco de teste descartado no fim.

---

## ⬜ Fase 3 — Produtividade

- "Copiar mês anterior": endpoint que replica os pagamentos do mês anterior no mês atual, ignorando os que já existem.
- Valor sugerido no formulário = último valor pago daquela conta fixa.
- Busca, filtros e paginação em Pagamentos (hoje `/api/payments` devolve tudo).
- Export CSV / Excel / PDF do painel e do dashboard.

## ⬜ Fase 4 — Plataforma

- ~~Serilog estruturado + correlation id~~ → migrado para a trilha [Observabilidade](Observabilidade/00-visao-geral.md), fases 03 e 06.
- ~~Health checks (`/health`) incluindo o SQL Server~~ → migrado para a trilha [Observabilidade](Observabilidade/00-visao-geral.md), fase 01.
- Notificações de vencimento via `HostedService`.
- Toggle de tema claro/escuro persistido (está no PRD e nunca foi implementado).
- Testes xUnit nos services + integração com `WebApplicationFactory`.
