using System.Security.Claims;
using Xunit;

namespace AdopPlant.Analytics.Tests
{
    public class PoliticaAutorizacionTests
    {
        [Fact]
        public void ValidarRequisitoPolitica_UsuarioCumpleRol_ApruebaValidacion()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "KarlaCeren"),
                new Claim("Scope", "Analytics.Read")
            }, "TestAuth");

            var principal = new ClaimsPrincipal(identity);
            bool cumpleRequisito = principal.HasClaim("Scope", "Analytics.Read");

            Assert.True(cumpleRequisito);
        }

        [Fact]
        public void ValidarRequisitoPolitica_UsuarioSinPermisos_DeniegaValidacion()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "UsuarioSinPermiso")
            }, "TestAuth");

            var principal = new ClaimsPrincipal(identity);
            bool cumpleRequisito = principal.HasClaim("Scope", "Analytics.Read");

            Assert.False(cumpleRequisito);
        }
    }
}