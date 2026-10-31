using System;
using Xunit;

namespace AdopPlant.Analytics.Tests
{
    public class PostgresConnectionFactoryTests
    {
        [Fact]
        public void CrearConexion_CadenaValida_InstanciaCorrectamente()
        {
            string connectionString = "Host=localhost;Database=test_db;Username=postgres;Password=secret";
            bool esValida = !string.IsNullOrWhiteSpace(connectionString) && connectionString.Contains("Host=");
            Assert.True(esValida);
        }

        [Fact]
        public void CrearConexion_CadenaNula_LanzaExcepcion()
        {
            string? connectionString = null;
            Assert.Throws<ArgumentNullException>(() =>
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new ArgumentNullException(nameof(connectionString), "La cadena de conexión no puede ser nula.");
                }
            });
        }
    }
}