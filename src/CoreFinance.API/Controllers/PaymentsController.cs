using CoreFinance.Application.Payments.Dtos;
using CoreFinance.Application.Payments.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CoreFinance.API.Controllers;

public class PaymentsController : BaseController
{
    private readonly IPaymentService _service;

    public PaymentsController(IPaymentService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterTodos()
    {
        var resultado = await _service.ObterTodosAsync();
        return RespostaOk(resultado);
    }

    [HttpGet("por-mes")]
    [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterPorMes([FromQuery] int mes, [FromQuery] int ano)
    {
        var resultado = await _service.ObterPorMesAnoAsync(mes, ano);
        return RespostaOk(resultado);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var resultado = await _service.ObterPorIdAsync(id);
        return RespostaOk(resultado);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CreatePaymentRequest request)
    {
        var resultado = await _service.CriarAsync(request);
        return RespostaOk(resultado);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] UpdatePaymentRequest request)
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
