# Fase 01 — Health Checks

> ⬅️ [Visão geral](00-visao-geral.md) · ➡️ próxima: [02 — Endpoints de demonstração](02-endpoints-de-demonstracao.md)
> **Containers novos:** nenhum. Esta fase é 100% .NET — a vitória rápida da trilha.
> **Status:** ✅ concluída e validada em 2026-09-05.

---

## Objetivo pedagógico

Responder às perguntas **1 e 2** da spec: *a aplicação está saudável?* e *a aplicação está disponível?*

E entender por que **"vivo" e "pronto" são coisas diferentes** — a distinção que orquestradores (Docker, Kubernetes) usam para decidir entre **reiniciar** um container e apenas **parar de mandar tráfego** para ele.

---

## O que entra no projeto

**Pacotes** (`src/CoreFinance.API/CoreFinance.API.csproj`):

```xml
<PackageReference Include="AspNetCore.HealthChecks.SqlServer" Version="8.*" />
<PackageReference Include="AspNetCore.HealthChecks.UI.Client" Version="8.*" />
```

**Arquivos novos:**

```text
src/CoreFinance.API/Extensions/HealthCheckExtensions.cs   ← segue o padrão de AuthenticationExtensions.cs
src/CoreFinance.API/Health/SelfHealthCheck.cs             ← check de liveness, sem dependência externa
```

**Arquivos alterados:**

```text
src/CoreFinance.API/Program.cs         ← AddHealthChecksConfig() + MapHealthChecksConfig()
src/CoreFinance.API/appsettings.json   ← seção HealthChecks (timeouts)
src/Dockerfile                          ← instalar curl + instrução HEALTHCHECK
docker-compose.yml                      ← healthcheck do serviço api
```

---

## Passos

### 1. Adicionar os pacotes

`AspNetCore.HealthChecks.SqlServer` é do projeto Xabaril, o de-facto standard do ecossistema .NET. Ele executa um `SELECT 1` na connection string e reporta o resultado.
`AspNetCore.HealthChecks.UI.Client` traz só o `UIResponseWriter`, que serializa o resultado em **JSON estruturado** — a spec (seção 7) pede explicitamente resposta estruturada em vez de texto puro.

### 2. Criar `Health/SelfHealthCheck.cs`

Um `IHealthCheck` que retorna `Healthy` incondicionalmente. Parece inútil, mas não é: é a definição formal de *liveness* — "o processo subiu, o pipeline de requisição responde, a thread pool não está travada". Se ele não responder, é porque o processo **está** com problema.

```csharp
public sealed class SelfHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HealthCheckResult.Healthy("API respondendo."));
}
```

### 3. Criar `Extensions/HealthCheckExtensions.cs`

Duas extensões, no mesmo formato de `AuthenticationExtensions.cs` e `SwaggerExtensions.cs` (padrão já usado no projeto — não invente um terceiro estilo):

**`AddHealthChecksConfig(IServiceCollection, IConfiguration)`**

```csharp
services.AddHealthChecks()
    .AddCheck<SelfHealthCheck>("self", tags: new[] { "live" })
    .AddSqlServer(
        connectionString: configuration.GetConnectionString("DefaultConnection")!,
        healthQuery: "SELECT 1;",
        name: "sqlserver",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "db" });

// Preparado para o futuro (seção 7 da spec) — descomente quando existir:
// .AddRedis(...)          tags: ["ready", "cache"]
// .AddRabbitMQ(...)       tags: ["ready", "mq"]
// .AddUrlGroup(...)       tags: ["ready", "external"]
```

**`MapHealthChecksConfig(WebApplication)`**

```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
}).AllowAnonymous();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
}).AllowAnonymous();
```

O sistema de **tags** é o mecanismo central: um único registro de checks, três visões filtradas dele. Adicionar Redis amanhã com `tags: ["ready"]` já entra no readiness sem tocar no mapeamento.

### 4. Ligar no `Program.cs`

```csharp
builder.Services.AddHealthChecksConfig(builder.Configuration);   // junto dos outros Add*
...
app.MapHealthChecksConfig();                                     // antes de app.MapControllers()
```

### 5. `HEALTHCHECK` no `src/Dockerfile`

No stage `base`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1
```

Usa `/health/live`, **não** `/health`: se o SQL Server cair, o Docker não deve reiniciar a API — reiniciar não conserta um banco fora do ar, só piora.

### 6. `docker-compose.yml`

Nada obrigatório, mas dá para deixar o `web` esperar a API ficar saudável:

```yaml
  web:
    depends_on:
      api:
        condition: service_healthy
```

---

## Como validar

```powershell
docker compose up -d --build api

# 1. Liveness — deve responder Healthy imediatamente
curl http://localhost:5176/health/live

# 2. Readiness — Healthy com o SQL Server no ar
curl http://localhost:5176/health/ready

# 3. Visão completa, com cada check detalhado
curl http://localhost:5176/health

# 4. Sem token! Health check não pode exigir autenticação.
#    (não passe Authorization em nenhum dos comandos acima)

# 5. Estado do container segundo o Docker
docker inspect --format "{{.State.Health.Status}}" corefinance-api
```

**O teste que importa** — derrubar a dependência:

```powershell
docker stop sqlserver_container
curl http://localhost:5176/health/live    # continua Healthy   ✅
curl http://localhost:5176/health/ready   # vira Unhealthy, 503 ✅
docker start sqlserver_container
# aguarde ~30s e o ready volta a Healthy sozinho
```

Se `live` cair junto com o banco, o desenho está errado.

---

## Dicas e armadilhas

> ⚠️ **A imagem `mcr.microsoft.com/dotnet/aspnet:8.0` não tem `curl` nem `wget`.**
> É a armadilha número um desta fase: o `HEALTHCHECK` fica eternamente `unhealthy` sem nenhuma mensagem útil. Daí o `apt-get install curl` no passo 5. Alternativa sem instalar nada: uma imagem *chiseled* + endpoint de health checado de fora, mas para um lab o `curl` é mais simples e visível.

> ⚠️ **Não coloque o health check dentro de um controller.** Ele nasceria com o `[Authorize]` do `BaseController` e o Docker/Prometheus levariam 401. Use `MapHealthChecks` (minimal API) e `.AllowAnonymous()` explícito.

> 💡 **`/health/ready` custa uma ida ao banco.** Se o Prometheus ou o Docker bater a cada 5 s, você criou carga onde não havia. Intervalo de 30 s é suficiente; para checks caros existe `AddCheck(..., timeout:)` e cache. Na [fase 04](04-metricas-otel-collector-prometheus.md) o resultado vira **métrica**, e aí ninguém precisa mais ficar batendo no endpoint.

> 💡 **Degraded existe e é subutilizado.** `HealthStatus.Degraded` é para "funciona, mas mal" — cache fora, fila acumulando. Devolve 200, mas aparece amarelo no dashboard. Bom para o alerta que avisa antes de virar incidente.

> 💡 **Não exponha detalhe demais.** O `UIResponseWriter` inclui a `Exception.Message` do check que falhou, e mensagem de erro de banco costuma conter servidor e nome do database. Em produção isso vira endpoint interno ou com filtro. Neste lab, ok — mas saiba que é uma escolha.

---

## Conceitos aprendidos

- **Liveness vs Readiness vs Health**: reiniciar o processo vs tirar do balanceador vs diagnosticar.
- **Tags** como forma de compor visões diferentes do mesmo conjunto de checks.
- Health check como **contrato de infraestrutura**: quem consome é o orquestrador, não o humano.
- Por que health check não pode depender de autenticação nem ser caro.

---

## Critério de aceite

- [x] `/health`, `/health/live` e `/health/ready` respondem JSON estruturado, sem token
- [x] Com o SQL Server parado: `live` Healthy (200) e `ready` Unhealthy (503) — e o `ready` voltou sozinho a 200 depois do `docker start`, sem reiniciar a API
- [x] `docker inspect` mostra o container como `healthy`
- [x] `dotnet build` limpo (0 erros, 0 avisos) e nenhum endpoint existente afetado — protegido sem token segue 401, Swagger segue 200 e os endpoints de health não entram no `swagger.json`
