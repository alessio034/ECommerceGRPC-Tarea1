using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.BFF.Seguridad;

// Emite el JWT propio del gateway. El mismo emisor, audiencia y clave
// son los que Program.cs usa para VALIDAR el token en cada solicitud.
public class TokenService(IConfiguration config)
{
    public (string Token, DateTime Expira) Emitir(UsuarioDemo u)
    {
        var expira = DateTime.UtcNow.AddMinutes(config.GetValue("Jwt:MinutosDeVida", 60));
        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Clave"]!));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = config["Jwt:Emisor"],          // ApiGateway.BFF
            Audience = config["Jwt:Audiencia"],
            Expires = expira,
            SigningCredentials = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", u.UserId.ToString()),     // el id en UserDB
                new Claim("email", u.Email),
                new Claim("role", u.Rol),                  // Admin o Cliente
                new Claim("jti", Guid.NewGuid().ToString()),
            ]),
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expira);
    }
}
