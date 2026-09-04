using CoreFinance.Application.Auth.Dtos;
using CoreFinance.Application.Auth.Interfaces;
using CoreFinance.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreFinance.API.Controllers;

public class AuthController : BaseController
{
    private readonly IAuthService _service;
    private readonly ICurrentUser _currentUser;

    public AuthController(IAuthService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpPost("registrar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Registrar([FromBody] RegistrarRequest request)
    {
        var resultado = await _service.RegistrarAsync(request);
        return RespostaOk(resultado);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var resultado = await _service.LoginAsync(request);
        return RespostaOk(resultado);
    }

    [HttpGet("eu")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ObterPerfil()
    {
        var resultado = await _service.ObterPerfilAsync(_currentUser.Id);
        return RespostaOk(resultado);
    }
}
