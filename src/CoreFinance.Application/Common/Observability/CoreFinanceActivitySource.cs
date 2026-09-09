using System.Diagnostics;

namespace CoreFinance.Application.Common.Observability;

/// <summary>
/// Fonte de spans manuais da camada de aplicação. A instrumentação automática cobre o que é
/// genérico (requisição HTTP, comando SQL); o que só faz sentido no domínio — "isto aqui é o
/// cálculo do comparativo anual" — precisa ser marcado à mão, e é o que esta fonte permite.
/// </summary>
// <see cref="ActivitySource"/> e <see cref="Activity"/> sao da BCL (System.Diagnostics), nao do
// OpenTelemetry: a API de tracing do .NET e nativa e o OTel apenas a coleta. Por isso instrumentar
// aqui NAO da a Application uma dependencia de vendor — mesmo criterio do IAppMetrics, que abstrai
// a metrica porque la o tipo concreto (Meter) so aparece na API.
public static class CoreFinanceActivitySource
{
    /// <summary>
    /// Nome registrado no <c>AddSource(...)</c> do pipeline de tracing. Sem esse registro a fonte
    /// existe, ninguém escuta, e todo <c>StartActivity</c> devolve <c>null</c>.
    /// </summary>
    public const string Name = "CoreFinance.Application";

    public static readonly ActivitySource Instance = new(Name, "1.0.0");
}
