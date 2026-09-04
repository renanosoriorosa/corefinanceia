using CoreFinance.Application.Auth.Dtos;
using CoreFinance.Application.Auth.Interfaces;
using CoreFinance.Application.Common;
using CoreFinance.Domain.Entities;
using CoreFinance.Domain.Interfaces.Repositories;
using CoreFinance.Domain.Interfaces.Security;
using FluentValidation;

namespace CoreFinance.Application.Auth.Services;

public class AuthService : IAuthService
{
    private const string CredenciaisInvalidas = "E-mail ou senha inválidos.";

    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IValidator<RegistrarRequest> _registrarValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthService(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IValidator<RegistrarRequest> registrarValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _registrarValidator = registrarValidator;
        _loginValidator = loginValidator;
    }

    public async Task<Result<AuthResponse>> RegistrarAsync(RegistrarRequest request)
    {
        var validacao = await _registrarValidator.ValidateAsync(request);
        if (!validacao.IsValid)
            return Result<AuthResponse>.Fail(validacao.Errors[0].ErrorMessage);

        if (await _repository.EmailExisteAsync(request.Email))
            return Result<AuthResponse>.Fail("E-mail já cadastrado.");

        var usuario = new User(request.Nome, request.Email, _passwordHasher.Gerar(request.Senha));

        await _repository.AdicionarAsync(usuario);
        await _repository.SalvarAsync();

        return Result<AuthResponse>.Ok(GerarResposta(usuario));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var validacao = await _loginValidator.ValidateAsync(request);
        if (!validacao.IsValid)
            return Result<AuthResponse>.Fail(validacao.Errors[0].ErrorMessage);

        var usuario = await _repository.ObterPorEmailAsync(request.Email);

        if (usuario is null || !_passwordHasher.Verificar(request.Senha, usuario.PasswordHash))
            return Result<AuthResponse>.Fail(CredenciaisInvalidas);

        if (!usuario.Active)
            return Result<AuthResponse>.Fail("Usuário inativo.");

        return Result<AuthResponse>.Ok(GerarResposta(usuario));
    }

    public async Task<Result<UsuarioDto>> ObterPerfilAsync(Guid usuarioId)
    {
        var usuario = await _repository.ObterPorIdAsync(usuarioId);

        if (usuario is null)
            return Result<UsuarioDto>.Fail("Usuário não encontrado.");

        return Result<UsuarioDto>.Ok(ToDto(usuario));
    }

    private AuthResponse GerarResposta(User usuario)
    {
        var token = _tokenGenerator.Gerar(usuario);

        return new AuthResponse
        {
            Token = token.Token,
            ExpiraEm = token.ExpiraEm,
            Usuario = ToDto(usuario)
        };
    }

    private static UsuarioDto ToDto(User usuario) => new()
    {
        Id = usuario.Id,
        Nome = usuario.Name,
        Email = usuario.Email
    };
}
