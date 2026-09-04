using Microsoft.Extensions.Configuration;

namespace CoreFinance.Infra.Security;

/// <summary>
/// Configuração do token compartilhada entre a geração (Infra) e a validação (API).
/// </summary>
public class JwtOptions
{
    public string Key { get; init; } = null!;
    public string Issuer { get; init; } = null!;
    public string Audience { get; init; } = null!;
    public int ExpiraEmMinutos { get; init; }

    public static JwtOptions Carregar(IConfiguration configuration)
    {
        var secao = configuration.GetSection("Jwt");

        var key = secao["Key"];

        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            throw new InvalidOperationException("Configuração 'Jwt:Key' ausente ou menor que 32 caracteres.");

        return new JwtOptions
        {
            Key = key,
            Issuer = secao["Issuer"] ?? "CoreFinance",
            Audience = secao["Audience"] ?? "CoreFinance",
            ExpiraEmMinutos = int.TryParse(secao["ExpiraEmMinutos"], out var minutos) ? minutos : 480
        };
    }
}
