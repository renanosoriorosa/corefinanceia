<#
.SYNOPSIS
    Gera carga contra os endpoints de demonstracao da API, para a trilha de observabilidade
    ter trafego para observar.

.DESCRIPTION
    Observabilidade sem trafego e uma tela vazia. Este script produz os quatro cenarios que
    as fases seguintes usam como materia-prima: trafego misto, erro, lentidao e volume.

    Requer Observability:Demo:Enabled = true na API (ver DemoControllerConvention).

.PARAMETER Cenario
    misto  - trafego realista pelo /random (poucos erros, cauda de latencia). Padrao.
    erro   - 100% erro, para ver alerta de taxa de erro disparar.
    lento  - 100% lentidao, para ver P95 e alerta de latencia.
    volume - so sucesso, o mais rapido possivel, para ver throughput.

.EXAMPLE
    .\scripts\gerar-carga.ps1
    .\scripts\gerar-carga.ps1 -Cenario erro -Duracao 120
    .\scripts\gerar-carga.ps1 -Cenario lento -Delay 3000
    .\scripts\gerar-carga.ps1 -Cenario volume -Paralelo 20
#>
[CmdletBinding()]
param(
    [ValidateSet('misto', 'erro', 'lento', 'volume')]
    [string]$Cenario = 'misto',

    [ValidateRange(1, 3600)]
    [int]$Duracao = 60,

    [ValidateRange(0, 10000)]
    [int]$Delay = 3000,

    [ValidateRange(1, 100)]
    [int]$Paralelo = 4,

    [ValidateRange(0, 100)]
    [int]$ErrorRate = 5,

    [string]$BaseUrl = 'http://localhost:5176'
)

$ErrorActionPreference = 'Stop'

function Get-UrlDoCenario {
    switch ($Cenario) {
        'erro'   { "$BaseUrl/api/demo/error" }
        'lento'  { "$BaseUrl/api/demo/slow?delay=$Delay" }
        'volume' { "$BaseUrl/api/demo/success" }
        default  { "$BaseUrl/api/demo/random?errorRate=$ErrorRate" }
    }
}

Add-Type -AssemblyName System.Net.Http

$http = [System.Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(30)

$url = Get-UrlDoCenario
$fim = (Get-Date).AddSeconds($Duracao)
$porStatus = @{}
$falhas = 0
$total = 0
$latencias = [System.Collections.Generic.List[double]]::new()

Write-Host ""
Write-Host "Cenario  : $Cenario"
Write-Host "URL      : $url"
Write-Host "Duracao  : $Duracao s"
Write-Host "Paralelo : $Paralelo"
Write-Host ""
Write-Host "Gerando carga (Ctrl+C para parar)..."

$relogioTotal = [System.Diagnostics.Stopwatch]::StartNew()
$ultimoPonto = [System.Diagnostics.Stopwatch]::StartNew()

try {
    # PowerShell 5.1 nao tem ForEach-Object -Parallel. O modelo aqui e manter $Paralelo
    # requisicoes em voo o tempo todo: a cada conclusao, entra uma nova. WaitAny devolve
    # qual terminou, e so assim da para cronometrar cada requisicao individualmente —
    # dividir o tempo de um lote pelo paralelismo daria latencia menor que a real, porque
    # as requisicoes correm concorrentes e nao em sequencia.
    $emVoo = [System.Collections.Generic.List[object]]::new()

    while ((Get-Date) -lt $fim -or $emVoo.Count -gt 0) {
        while ($emVoo.Count -lt $Paralelo -and (Get-Date) -lt $fim) {
            $emVoo.Add([pscustomobject]@{
                Task     = $http.GetAsync($url)
                Relogio  = [System.Diagnostics.Stopwatch]::StartNew()
            })
        }

        if ($emVoo.Count -eq 0) { break }

        $tarefas = [System.Threading.Tasks.Task[]]($emVoo | ForEach-Object { $_.Task })
        $indice = [System.Threading.Tasks.Task]::WaitAny($tarefas)
        $concluida = $emVoo[$indice]
        $concluida.Relogio.Stop()
        $emVoo.RemoveAt($indice)

        $total++
        $latencias.Add($concluida.Relogio.Elapsed.TotalMilliseconds)

        try {
            $resposta = $concluida.Task.Result
            $status = [int]$resposta.StatusCode
            if (-not $porStatus.ContainsKey($status)) { $porStatus[$status] = 0 }
            $porStatus[$status]++
            $resposta.Dispose()
        }
        catch {
            # Conexao recusada, timeout: a API pode estar fora do ar. Conta e segue.
            $falhas++
        }

        # Um ponto a cada 250 ms, nao por requisicao: o cenario erro faz milhares por
        # segundo e inundaria o terminal, enquanto o cenario lento faz uma a cada 1,5 s e
        # ficaria mudo. Limitar por tempo funciona nos dois extremos.
        if ($ultimoPonto.ElapsedMilliseconds -ge 250) {
            Write-Host -NoNewline "."
            $ultimoPonto.Restart()
        }
    }
}
finally {
    $relogioTotal.Stop()
    $http.Dispose()

    Write-Host ""
    Write-Host ""
    Write-Host "Total de requisicoes: $total"

    if ($total -gt 0) {
        foreach ($status in ($porStatus.Keys | Sort-Object)) {
            $pct = [math]::Round(100 * $porStatus[$status] / $total, 1)
            Write-Host ("  HTTP {0}: {1} ({2}%)" -f $status, $porStatus[$status], $pct)
        }

        if ($falhas -gt 0) {
            Write-Host ("  sem resposta: {0}" -f $falhas)
        }

        $ordenadas = @($latencias | Sort-Object)
        $p50 = $ordenadas[[int][math]::Floor($ordenadas.Count * 0.50)]
        $p95 = $ordenadas[[math]::Min($ordenadas.Count - 1, [int][math]::Floor($ordenadas.Count * 0.95))]
        $media = ($latencias | Measure-Object -Average).Average
        $vazao = $total / $relogioTotal.Elapsed.TotalSeconds

        Write-Host ""
        Write-Host ("  latencia  media {0} ms | p50 {1} ms | p95 {2} ms" -f `
            [math]::Round($media, 0), [math]::Round($p50, 0), [math]::Round($p95, 0))
        Write-Host ("  vazao     {0} req/s" -f [math]::Round($vazao, 1))
    }
    Write-Host ""
}
