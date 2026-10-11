namespace AdopPlant.Analytics.Security;

/// <summary>
/// Configuración del JWT. Debe coincidir EXACTAMENTE con la del backend
/// Python (variable de entorno JWT_SECRET_KEY y algoritmo HS256).
/// </summary>
public sealed class JwtSettings
{
    public string Secret { get; set; } = string.Empty;

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Secret) || Secret.Length < 64)
        {
            throw new InvalidOperationException(
                "Jwt:Secret debe tener al menos 64 caracteres y ser idéntico al " +
                "JWT_SECRET_KEY del backend FastAPI.");
        }

        if (Secret.StartsWith("CAMBIAR", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Debes reemplazar Jwt:Secret por el valor real de JWT_SECRET_KEY " +
                "(o definir la variable de entorno Jwt__Secret).");
        }
    }
}
