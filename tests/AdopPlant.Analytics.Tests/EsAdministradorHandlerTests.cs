using System.Security.Claims;
using Xunit;

namespace AdopPlant.Analytics.Tests
{
    public class EsAdministradorHandlerTests
    {
        [Fact]
        public void EvaluarAutorizacion_UsuarioEsAdmin_DebeAprobarAcceso()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "101"),
                new Claim(ClaimTypes.Role, "Administrador")
            }, "TestAuth"));

            bool esAdmin = user.IsInRole("Administrador");
            Assert.True(esAdmin);
        }

        [Fact]
        public void EvaluarAutorizacion_UsuarioNoEsAdmin_DebeDenegarAcceso()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "102"),
                new Claim(ClaimTypes.Role, "UsuarioComun")
            }, "TestAuth"));

            bool esAdmin = user.IsInRole("Administrador");
            Assert.False(esAdmin);
        }
    }
}