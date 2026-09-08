# Fase 04 — Métricas: OpenTelemetry + Collector + Prometheus

> ⬅️ anterior: [03 — Logs](03-logs-serilog-loki.md) · ➡️ próxima: [05 — Traces](05-traces-tempo.md)
> **Containers novos:** `otel-collector`, `prometheus`.
> **Status:** ✅ concluída e validada em 2026-09-05. O que a execução mudou em relação ao plano
> está em [Resultado da execução](#resultado-da-execucao), no fim do documento.

---

## Objetivo pedagógico

Responder às perguntas **3, 4 e 5**: *quantas requisições? qual a taxa de erro? qual o tempo de resposta?*

E entender por que métrica é um sinal **agregado e barato** — 10 milhões de requisições cabem em algumas séries temporais, enquanto 10 milhões de logs custam gigabytes. É o sinal que você olha primeiro, sempre.

---

## O que entra no projeto

**Pacotes** (todos **estáveis** — benefício direto de ter escolhido o Collector):

```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.18.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.18.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.18.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.12.*" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.18.*" />
```

> ⚠️ **Não use 1.11.** O plano original pedia `1.11.*`, mas o build acusa `NU1902` — o
> `OpenTelemetry.Exporter.OpenTelemetryProtocol` até 1.12.0 tem vulnerabilidade moderada conhecida
> ([GHSA-4625-4j76-fww9](https://github.com/advisories/GHSA-4625-4j76-fww9)). Da 1.14 em diante o
> aviso some. A `Instrumentation.Runtime` fica em 1.12 porque é a última publicada.

> Sem o Collector, exportar Prometheus direto exigiria `OpenTelemetry.Exporter.Prometheus.AspNetCore`, que ainda é **beta**. Esse é um argumento concreto a favor da decisão de arquitetura tomada em [00](00-visao-geral.md).

**Arquivos novos:**

```text
src/CoreFinance.API/Observability/ObservabilityExtensions.cs
src/CoreFinance.API/Observability/AppMetrics.cs
src/CoreFinance.API/Health/HealthMetricsPublisher.cs
src/CoreFinance.Application/Common/Observability/IAppMetrics.cs
docker/otel-collector/otel-collector-config.yml
docker/prometheus/prometheus.yml
```

**Arquivos alterados:**

```text
src/CoreFinance.API/Program.cs                                    ← 1 linha
src/CoreFinance.API/CoreFinance.API.csproj                        ← 5 pacotes
src/CoreFinance.API/appsettings.json                              ← Observability:Enabled
src/CoreFinance.Application/Payments/Services/PaymentService.cs   ← 1 linha + injeção
docker/grafana/provisioning/datasources/datasources.yml
docker-compose.yml
```

---

## Passos

### 1. `Observability/ObservabilityExtensions.cs`

```csharp
public static IServiceCollection AddObservability(
    this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
{
    var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

    services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            serviceName:    configuration["OTEL_SERVICE_NAME"] ?? "corefinance-api",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString())
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = env.EnvironmentName,
                ["service.namespace"]      = "corefinance"
            }))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(AppMetrics.MeterName)
            .AddOtlpExporter());     // lê OTEL_EXPORTER_OTLP_ENDPOINT do ambiente

    return services;
}
```

O `Resource` é o que carimba **todo** sinal exportado com `service.name`. Sem ele, os dados chegam anônimos e você não consegue separar aplicações no mesmo backend.

### 2. Métrica de negócio — sem quebrar a Clean Architecture

`Application` não deve conhecer OpenTelemetry. A saída é a mesma que o projeto já usa para `ICurrentUser`: **interface na Application, implementação na API**.

```csharp
// Application/Common/Observability/IAppMetrics.cs
public interface IAppMetrics
{
    void PagamentoCriado(bool contaFixa);
}
```

```csharp
// API/Observability/AppMetrics.cs  (singleton)
public sealed class AppMetrics : IAppMetrics, IDisposable
{
    public const string MeterName = "CoreFinance.App";

    private readonly Meter _meter = new(MeterName, "1.0.0");
    private readonly Counter<long> _pagamentosCriados;

    public AppMetrics() =>
        _pagamentosCriados = _meter.CreateCounter<long>(
            "corefinance.payments.created", unit: "{payment}",
            description: "Pagamentos criados.");

    public void PagamentoCriado(bool contaFixa) =>
        _pagamentosCriados.Add(1, new KeyValuePair<string, object?>("is_fixed_account", contaFixa));

    public void Dispose() => _meter.Dispose();
}
```

Uma linha em `PaymentService.CriarAsync` após o salvamento: `_metrics.PagamentoCriado(request.IsFixedAccount);`

> ⚠️ **Cuidado com a dimensão da tag.** `is_fixed_account` tem 2 valores possíveis — perfeito. Se você usasse `user_id` como tag, cada usuário criaria uma série temporal nova. Cardinalidade mata Prometheus exatamente como mata Loki, só que aqui a conta chega em RAM.

### 3. Métrica de saúde — ligando a fase 01 nos alertas

`IHealthCheckPublisher` recebe o resultado dos health checks no intervalo configurado e alimenta um `ObservableGauge`:

```csharp
// Healthy = 1, Degraded = 0.5, Unhealthy = 0
_meter.CreateObservableGauge("corefinance.health.status", () => _ultimoValor);
```

Registro:

```csharp
services.Configure<HealthCheckPublisherOptions>(o => { o.Delay = TimeSpan.FromSeconds(5); o.Period = TimeSpan.FromSeconds(15); });
services.AddSingleton<IHealthCheckPublisher, HealthMetricsPublisher>();
```

> 💡 **Por que isso é melhor que o Grafana bater em `/health`:** o Grafana alerta sobre dados que já estão no Prometheus; se ele precisasse fazer HTTP na API, você teria mais um caminho de rede para falhar e nenhum histórico de saúde. Com a métrica, a saúde vira série temporal — dá para ver *quando* ficou ruim e *por quanto tempo*.

### 4. `docker/otel-collector/otel-collector-config.yml`

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  memory_limiter:
    check_interval: 1s
    limit_mib: 256
  batch:
    timeout: 5s

exporters:
  prometheus:
    endpoint: 0.0.0.0:8889
    resource_to_telemetry_conversion:
      enabled: true      # service.name vira label da métrica

extensions:
  health_check:
    endpoint: 0.0.0.0:13133

service:
  extensions: [health_check]
  pipelines:
    metrics:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [prometheus]
```

O pipeline `traces` entra na [fase 05](05-traces-tempo.md). Repare no desenho: **receiver → processor → exporter**. É o modelo mental inteiro do Collector.

> ⚠️ `memory_limiter` deve ser **o primeiro** processor da lista. Ele existe para o Collector recusar dados em vez de morrer por OOM — se vier depois do `batch`, protege tarde demais.

### 5. `docker/prometheus/prometheus.yml`

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: otel-collector
    static_configs:
      - targets: ["otel-collector:8889"]

  - job_name: prometheus
    static_configs:
      - targets: ["localhost:9090"]
```

**Modelo pull:** o Prometheus vai buscar. É por isso que dá para `curl http://localhost:8889/metrics` e ver exatamente o que ele veria — a capacidade de depuração mais útil desta fase.

### 6. Compose

```yaml
  otel-collector:
    image: otel/opentelemetry-collector-contrib:0.121.0
    container_name: corefinance-otel-collector
    profiles: ["obs"]
    command: ["--config=/etc/otelcol/config.yml"]
    volumes:
      - ./docker/otel-collector/otel-collector-config.yml:/etc/otelcol/config.yml:ro
    ports:
      - "8889:8889"        # só para depuração
    restart: unless-stopped

  prometheus:
    image: prom/prometheus:v3.2.1
    container_name: corefinance-prometheus
    profiles: ["obs"]
    command:
      - --config.file=/etc/prometheus/prometheus.yml
      - --storage.tsdb.retention.time=7d
      - --web.enable-lifecycle
    ports:
      - "9090:9090"
    volumes:
      - ./docker/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus-data:/prometheus
    depends_on:
      - otel-collector
    restart: unless-stopped
```

Variáveis no serviço `api`:

```yaml
      OTEL_SERVICE_NAME: corefinance-api
      OTEL_EXPORTER_OTLP_ENDPOINT: http://otel-collector:4317
      OTEL_EXPORTER_OTLP_PROTOCOL: grpc
      OTEL_RESOURCE_ATTRIBUTES: service.namespace=corefinance,deployment.environment=local
```

E no `datasources.yml`:

```yaml
  - name: Prometheus
    type: prometheus
    uid: prometheus
    access: proxy
    url: http://prometheus:9090
```

---

## Como validar

### Passo 0 — descobrir os nomes reais das métricas

> ⚠️ **Regra de ouro (seção 11 da spec): não assuma nomes.** A spec cita `http_requests_total` — **essa métrica não existe no .NET 8.** O meter nativo `Microsoft.AspNetCore.Hosting` publica `http.server.request.duration` (histograma em segundos), que o exporter converte para `http_server_request_duration_seconds_{bucket,sum,count}`. O contador de requisições é o `_count` do histograma.

```powershell
.\scripts\gerar-carga.ps1 -Duracao 30

curl http://localhost:8889/metrics | Select-String "http_server"
curl http://localhost:8889/metrics | Select-String "corefinance"
curl http://localhost:8889/metrics | Select-String "dotnet|process_runtime"
```

**Anote os nomes que aparecerem** — são eles que vão para as queries e para o dashboard da [fase 07](07-dashboard-grafana.md). Os nomes de runtime variam conforme a versão da `Instrumentation.Runtime` (`process_runtime_dotnet_*` nas versões mais antigas, `dotnet_*` nas novas).

Os nomes efetivamente observados nesta execução estão em [Resultado da execução](#resultado-da-execucao).

### Passo 1 — target UP

`http://localhost:9090/targets` → `otel-collector` deve estar **UP**. Se estiver `DOWN`, os suspeitos são: nome do serviço errado, containers em redes diferentes, ou o Collector nem subiu (`docker logs corefinance-otel-collector`).

### Passo 2 — as consultas que importam

No Prometheus (`:9090`) ou no Grafana Explore:

```promql
# Requisições por segundo
sum(rate(http_server_request_duration_seconds_count[5m]))

# Total de requisições na última hora
sum(increase(http_server_request_duration_seconds_count[1h]))

# Taxa de erro (0–1)
sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m]))
/
sum(rate(http_server_request_duration_seconds_count[5m]))

# Latência P95
histogram_quantile(0.95,
  sum by (le) (rate(http_server_request_duration_seconds_bucket[5m])))

# Latência média
sum(rate(http_server_request_duration_seconds_sum[5m]))
/
sum(rate(http_server_request_duration_seconds_count[5m]))

# Por rota — onde está o gargalo
topk(5, sum by (http_route) (rate(http_server_request_duration_seconds_sum[5m])))

# Métrica de negócio
sum(increase(corefinance_payments_created_total[24h]))

# Saúde
corefinance_health_status
```

### Passo 3 — provar cada uma

```powershell
.\scripts\gerar-carga.ps1 -Cenario erro   -Duracao 60    # taxa de erro sobe
.\scripts\gerar-carga.ps1 -Cenario lento  -Delay 3000    # P95 dispara, média sobe pouco
docker stop sqlserver_container                          # corefinance_health_status cai para 0
```

O cenário `lento` é o mais instrutivo: com poucas requisições lentas no meio de muitas rápidas, a **média mal se mexe e o P95 explode**. É a demonstração prática de por que média engana.

---

## Dicas e armadilhas

> ⚠️ **`rate` vs `increase` vs `irate`.** `rate` = média por segundo na janela (para gráficos e alertas). `increase` = quanto cresceu na janela (para totais). `irate` = taxa instantânea entre os dois últimos pontos (só para depuração fina, é ruidoso demais para alerta).

> ⚠️ **Janela precisa ser maior que o scrape.** Com `scrape_interval: 15s`, um `rate(...[15s])` frequentemente pega um ponto só e devolve vazio. Regra prática: **pelo menos 4× o intervalo de scrape** — daí o `[5m]` em tudo aqui.

> 💡 **Como um histograma funciona.** Ele não guarda cada medição: guarda contadores acumulados por *bucket* (`le="0.005"`, `le="0.01"`…). `histogram_quantile` **interpola** dentro do bucket — então o P95 é uma estimativa, e sua precisão depende dos buckets terem sido escolhidos perto dos seus tempos reais. A instrumentação do ASP.NET Core .NET 8 já traz buckets razoáveis por padrão.

> 💡 **`le` é obrigatório no `sum by`.** `histogram_quantile` precisa da dimensão dos buckets. Esquecer o `by (le)` é o erro de PromQL número um e devolve `NaN` sem explicar nada.

> 💡 **Contador só cresce; `rate` cuida do reset.** Quando a API reinicia, o contador zera. O `rate` detecta a queda e trata como reset — por isso você nunca deve fazer a derivada "na mão" com `delta`.

> 💡 **Se a métrica não aparecer**, siga o fluxo: API exporta? (`docker logs corefinance-api` procurando erro de OTLP) → Collector recebeu? (`docker logs corefinance-otel-collector`) → Collector expõe? (`curl :8889/metrics`) → Prometheus coleta? (`/targets`). Cada seta é um lugar possível de falha, e o Collector no meio existe justamente para dar esse ponto de inspeção.

> 💡 **Métrica não tem detalhe, por definição.** Ela te diz "5% de erro". Não te diz *qual* requisição, *qual* usuário, *qual* stack. Para isso existem as fases [05](05-traces-tempo.md) e [03](03-logs-serilog-loki.md) — e é exatamente essa a razão de existirem três sinais.

---

## Conceitos aprendidos

- **Pull vs push**: por que o Prometheus faz scrape em vez de receber.
- **Tipos de métrica**: counter (só cresce), gauge (sobe e desce), histogram (distribuição).
- **Cardinalidade** de labels — de novo, agora custando RAM em vez de disco.
- **Percentil vs média** e por que P95 é o número que importa.
- Pipeline do **Collector**: receiver → processor → exporter.
- **Resource attributes** e `service.name` como identidade dos sinais.
- Instrumentar domínio **sem violar a inversão de dependência**.

---

## Critério de aceite

- [x] Nomes reais das métricas anotados a partir de `:8889/metrics` (não copiados da spec)
- [x] Target `otel-collector` **UP** no Prometheus
- [x] As 6 consultas de PromQL acima retornam dados
- [x] Cenário de erro move a taxa de erro; cenário lento move o P95 muito mais que a média
- [x] `corefinance_payments_created_total` incrementa ao criar pagamento
- [x] `corefinance_health_status` cai para 0 com o SQL Server parado
- [x] Datasource Prometheus provisionado no Grafana

---

<a id="resultado-da-execucao"></a>

## Resultado da execução (2026-09-05)

### Nomes reais das métricas

Lidos de `curl http://localhost:8889/metrics` com a stack no ar. **São estes que valem** para as fases 07 e 08:

| Família | Métricas |
|---|---|
| HTTP servidor | `http_server_request_duration_seconds_{bucket,sum,count}`, `http_server_active_requests` |
| HTTP cliente | `http_client_request_duration_seconds_*`, `http_client_active_requests`, `http_client_open_connections`, `http_client_request_time_in_queue_seconds`, `dns_lookup_duration_seconds` |
| Kestrel | `kestrel_active_connections`, `kestrel_connection_duration_seconds`, `kestrel_queued_connections` |
| Roteamento | `aspnetcore_routing_match_attempts_total` |
| Runtime | `process_runtime_dotnet_*` (GC, JIT, thread pool, exceptions, monitor lock, timers) |
| Negócio | `corefinance_payments_created_total` |
| Saúde | `corefinance_health_status`, `corefinance_health_check_status` |
| Meta | `target_info` |

Confirmado: **`http_requests_total` não existe** — o contador de requisições é o `_count` do histograma, exatamente como a fase previa. E a `Instrumentation.Runtime` 1.12 ainda usa o prefixo antigo `process_runtime_dotnet_*`, não `dotnet_*`.

Labels de `http_server_request_duration_seconds_*`: `http_request_method`, `http_response_status_code`, `http_route`, `network_protocol_version`, `url_scheme` — mais os resource attributes (`service_name`, `service_namespace`, `deployment_environment`, `service_version`, `service_instance_id`, `telemetry_sdk_*`).

> ⚠️ **`job` e `instance` não são os da aplicação.** O Prometheus injeta os seus no scrape e renomeia os que vieram do resource para `exported_job` e `exported_instance`. Ou seja: `job="corefinance-api"` **não retorna nada** — o job é `otel-collector`. Para filtrar a aplicação, use `service_name="corefinance-api"`.

### Números medidos

Carga mista de 90 s (`gerar-carga.ps1`), comparando o que o script mediu no cliente com o que o PromQL devolveu:

| | script (cliente) | PromQL (servidor) |
|---|---|---|
| taxa de erro | 5,7 % | 5,2 % |
| latência média | 135 ms | 132 ms |
| P95 | 384 ms | 469 ms |

Média praticamente idêntica; **o P95 diverge porque é estimado por interpolação de bucket**, não medido. É a lição do histograma, aparecendo sozinha no primeiro teste.

O cenário lento reforçou os dois pontos de uma vez — 30 requisições de 3 s no meio de ~2.700 rápidas:

- média global subiu de **132 ms → 156 ms** (+18 %), quase nada;
- P95 da rota `api/Demo/slow` foi a **4,87 s** para requisições que levaram **3,0 s**.

> 💡 Esse 4,87 s não é erro de medição: os buckets padrão do ASP.NET Core saltam de 2,5 s para 5 s. Com todas as amostras caindo no mesmo bucket, `histogram_quantile` só sabe interpolar entre 2,5 e 5. **A precisão do percentil é a resolução dos buckets naquela faixa** — se a sua latência real vive numa faixa mal coberta, o percentil mente para cima. Corrigir isso é definir buckets próprios via View do OpenTelemetry, não trocar a query.

### Métrica de saúde

`corefinance_health_status` caiu para **0** com `docker stop sqlserver_container` e voltou a **1** sozinha depois do `docker start`.

> ⚠️ **Conte até ~1 minuto, não até 15 segundos.** O caminho inteiro é: `docker stop` (até 10 s de grace) → publisher roda no período de 15 s → o check do SQL espera o `ConnectTimeout` de 5 s → exportação OTLP a cada 15 s → scrape do Prometheus a cada 15 s. Na prática foram **~80 s** para a queda e **~45 s** para a recuperação. Isso não é lentidão do lab: é a latência real de qualquer alerta baseado em métrica, e o motivo de a [fase 08](08-alertas.md) precisar de um `for:` compatível com ela.

Além do gauge previsto no plano, foi adicionado `corefinance_health_check_status`, com o label `health_check` (`self`, `sqlserver`). Cardinalidade de 2 séries, e responde "qual dependência caiu?" sem abrir o `/health`.

> 💡 O gauge **não emite amostra nenhuma** antes do primeiro ciclo do publisher. Devolver 0 na partida seria afirmar que a API está fora do ar justamente enquanto ela sobe — e alertas de "saúde = 0" disparariam a cada deploy.

### Desvios do plano original

| Desvio | Por quê |
|---|---|
| Pacotes OTel em `1.18.*` (Runtime em `1.12.*`), não `1.11.*` | `NU1902` no exportador OTLP até 1.12.0 |
| Collector publica também `4317` e `4318`, não só `8889` | Mesmo motivo do `3100` do Loki na fase 03: rodando a API pelo Visual Studio, fora do compose, ela precisa alcançar o Collector por `localhost` |
| `OTEL_METRIC_EXPORT_INTERVAL: 15000` no compose | Padrão do SDK é 60 s. Alinhado ao `scrape_interval`, o efeito da carga aparece em ~15 s em vez de sumir por um minuto |
| `deployment.environment` e `service.namespace` vêm do ambiente, não do código | O plano carimbava `EnvironmentName` por cima do `OTEL_RESOURCE_ATTRIBUTES`, e a métrica saía como `Development` enquanto o log do Loki dizia `local` |
| Flag `Observability:Enabled` (padrão `true`) | Armadilha 1 da [visão geral](00-visao-geral.md#7-armadilhas-globais-leia-antes-de-começar) previa a saída para rodar sem o profile `obs`; agora ela existe de fato |
| `spike_limit_mib: 64` no `memory_limiter` | Sem ele o Collector 0.121 assume 20 % do limite; explicitar deixa a intenção visível |
| `AppMetrics` registrado duas vezes (concreto + interface) | O `HealthMetricsPublisher` é da API e precisa do tipo concreto; a Application só enxerga `IAppMetrics`. Sem a segunda linha seriam dois `Meter` com o mesmo nome |

### Pendências herdadas para a fase 07

- Todas as queries do dashboard precisam filtrar por `service_name`, nunca por `job`.
- Considerar buckets customizados para `http.server.request.duration` se o P95 continuar impreciso na faixa de segundos.
