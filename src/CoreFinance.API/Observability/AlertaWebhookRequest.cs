namespace CoreFinance.API.Observability;

/// <summary>
/// Recorte do payload que o Grafana envia ao contact point do tipo webhook.
/// </summary>
// Recorte de proposito: o payload real tem uma duzia de campos (groupKey, externalURL,
// truncatedAlerts...) e nada obriga a conhecer todos. Contrato de entrada se modela pelo que
// se usa — campo que ninguem le so cria manutencao. O binder ignora o resto em silencio.
public class AlertaWebhookRequest
{
    /// <summary>Estado do grupo inteiro: <c>firing</c> ou <c>resolved</c>.</summary>
    public string Status { get; set; } = string.Empty;

    public List<AlertaWebhookItem> Alerts { get; set; } = [];
}

/// <summary>Um alerta individual dentro da notificação.</summary>
public class AlertaWebhookItem
{
    public string Status { get; set; } = string.Empty;

    /// <summary>Labels da regra: <c>alertname</c>, <c>severity</c>, <c>sinal</c>.</summary>
    public Dictionary<string, string> Labels { get; set; } = [];

    /// <summary>Anotações da regra: <c>summary</c>, <c>description</c>, <c>runbook</c>.</summary>
    public Dictionary<string, string> Annotations { get; set; } = [];

    public DateTimeOffset? StartsAt { get; set; }

    // O Grafana manda o valor de cada refId da regra (A, B, C) que levou ao disparo. E o que
    // permite ao log dizer "disparou com 0.83" em vez de so "disparou". Nullable porque uma
    // expressao pode nao ter produzido valor.
    public Dictionary<string, double?> Values { get; set; } = [];
}
