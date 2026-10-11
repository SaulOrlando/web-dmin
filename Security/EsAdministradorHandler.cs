using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace AdopPlant.Analytics.Security;

public sealed class EsAdministradorHandler : AuthorizationHandler<EsAdministradorRequirement>
{
    private readonly AdminCheckService _admin;

    public EsAdministradorHandler(AdminCheckService admin) => _admin = admin;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EsAdministradorRequirement requirement)
    {
        var sub = context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(sub, out var idUsuario))
        {
            return;
        }

        var info = await _admin.ObtenerAsync(idUsuario, CancellationToken.None);

        if (info is null)
        {
            return;
        }

        if (!string.Equals(info.Estado, "ACTIVO", StringComparison.Ordinal))
        {
            return;
        }

        if (!info.EsAdministrador)
        {
            return;
        }

        context.Succeed(requirement);
    }
}
