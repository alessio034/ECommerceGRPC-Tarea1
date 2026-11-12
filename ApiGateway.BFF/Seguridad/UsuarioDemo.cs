using System.Security.Cryptography;
using System.Text;

namespace ApiGateway.BFF.Seguridad;

// Usuarios de demostración del gateway (sección "UsuariosDemo" de appsettings.json).
// Solo para el curso: en producción las claves no viven en la configuración.
// En el Nivel 3, un proveedor de identidad (Keycloak) reemplaza esta lista.
public record UsuarioDemo(string Email, string Clave, string Rol, int UserId)
{
    public bool ClaveCorrecta(string intento) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Clave), Encoding.UTF8.GetBytes(intento));
}
