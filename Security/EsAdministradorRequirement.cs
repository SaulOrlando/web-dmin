using Microsoft.AspNetCore.Authorization;

namespace AdopPlant.Analytics.Security;

/// <summary>Exige un JWT válido cuyo usuario sea administrador activo.</summary>
public sealed class EsAdministradorRequirement : IAuthorizationRequirement
{
}
