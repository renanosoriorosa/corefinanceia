namespace CoreFinance.Application.Auth.Dtos;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraEm { get; set; }
    public UsuarioDto Usuario { get; set; } = new();
}
