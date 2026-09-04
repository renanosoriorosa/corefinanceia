using CoreFinance.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreFinance.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class BaseController : ControllerBase
{
    protected IActionResult RespostaOk<T>(Result<T> resultado)
    {
        if (!resultado.Success)
            return BadRequest(new { erro = resultado.ErrorMessage });

        return Ok(resultado.Data);
    }

    protected IActionResult RespostaOk(Result resultado)
    {
        if (!resultado.Success)
            return BadRequest(new { erro = resultado.ErrorMessage });

        return NoContent();
    }
}
