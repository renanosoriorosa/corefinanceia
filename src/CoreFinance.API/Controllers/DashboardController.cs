using CoreFinance.Application.Dashboard.Dtos;
using CoreFinance.Application.Dashboard.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CoreFinance.API.Controllers;

public class DashboardController : BaseController
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service)
    {
        _service = service;
    }

    [HttpGet("anual")]
    [ProducesResponseType(typeof(DashboardAnualDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterAnual(
        [FromQuery] int ano,
        [FromQuery] Guid? contaFixaId = null,
        [FromQuery] bool incluirNaoFixas = true)
    {
        var resultado = await _service.ObterAnualAsync(ano, contaFixaId, incluirNaoFixas);
        return RespostaOk(resultado);
    }
}
