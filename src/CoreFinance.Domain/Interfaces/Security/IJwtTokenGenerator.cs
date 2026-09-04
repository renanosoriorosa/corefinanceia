using CoreFinance.Domain.Entities;

namespace CoreFinance.Domain.Interfaces.Security;

public interface IJwtTokenGenerator
{
    TokenGerado Gerar(User usuario);
}

public record TokenGerado(string Token, DateTime ExpiraEm);
