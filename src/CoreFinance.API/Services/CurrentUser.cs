using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoreFinance.Domain.Interfaces;

namespace CoreFinance.API.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid Id
    {
        get
        {
            var claim = _accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)
                        ?? _accessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier);

            return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
        }
    }

    public bool Autenticado => Id != Guid.Empty;
}
