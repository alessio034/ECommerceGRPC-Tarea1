using ApiGateway.BFF.Modelos;
using ApiGateway.BFF.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApiGateway.BFF.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IConfiguration config, TokenService tokens) : ControllerBase
{
    /// <summary>Inicia sesión y devuelve un JWT firmado por ApiGateway.BFF.</summary>
    /// <remarks>Usuarios de demostración: admin@example.com / Admin2026! (Admin) y user@example.com / Cliente2026! (Cliente).</remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<TokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ActionResult<TokenDto> Login(LoginDto dto)
    {
        var usuario = config.GetSection("UsuariosDemo").Get<List<UsuarioDemo>>()?
            .FirstOrDefault(u => u.Email.Equals(dto.Email, StringComparison.OrdinalIgnoreCase));

        // El mismo mensaje para correo inexistente y clave incorrecta: no revela qué correos existen.
        if (usuario is null || !usuario.ClaveCorrecta(dto.Clave))
            return Unauthorized(new { error = "Correo o clave incorrectos." });

        var (token, expira) = tokens.Emitir(usuario);
        return Ok(new TokenDto(token, expira, usuario.Rol));
    }
}
