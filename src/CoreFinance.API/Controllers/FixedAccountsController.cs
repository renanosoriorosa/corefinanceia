using CoreFinance.Application.FixedAccounts.Dtos;
using CoreFinance.Application.FixedAccounts.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CoreFinance.API.Controllers;

public class FixedAccountsController : BaseController
{
    private readonly IFixedAccountService _service;

    public FixedAccountsController(IFixedAccountService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FixedAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterTodos()
    {
        var resultado = await _service.ObterTodosAsync();
        return RespostaOk(resultado);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FixedAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var resultado = await _service.ObterPorIdAsync(id);
        return RespostaOk(resultado);
    }

    [HttpPost]
    [ProducesResponseType(typeof(FixedAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CreateFixedAccountRequest request)
    {
        var resultado = await _service.CriarAsync(request);
        return RespostaOk(resultado);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(FixedAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] UpdateFixedAccountRequest request)
    {
        var resultado = await _service.AtualizarAsync(id, request);
        return RespostaOk(resultado);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Remover(Guid id)
    {
        var resultado = await _service.RemoverAsync(id);
        return RespostaOk(resultado);
    }
}
