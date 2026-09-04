using CoreFinance.Application.Auth.Dtos;
using CoreFinance.Application.Common;

namespace CoreFinance.Application.Auth.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegistrarAsync(RegistrarRequest request);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request);
    Task<Result<UsuarioDto>> ObterPerfilAsync(Guid usuarioId);
}
