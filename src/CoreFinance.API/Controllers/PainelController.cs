using CoreFinance.Application.Painel.Dtos;
using CoreFinance.Application.Painel.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CoreFinance.API.Controllers;

public class PainelController : BaseController
{
    private readonly IPainelService _service;

    public PainelController(IPainelService service)
    {
        _service = service;
    }

    [HttpGet("mes-atual")]
    [ProducesResponseType(typeof(PainelMesAtualDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterMesAtual()
    {
        var resultado = await _service.ObterMesAtualAsync();
        return RespostaOk(resultado);
    }
}
