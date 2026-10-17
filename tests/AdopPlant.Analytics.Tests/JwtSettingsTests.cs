using AdopPlant.Analytics.Security;
using Xunit;

namespace AdopPlant.Analytics.Tests;

public class JwtSettingsTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validar_ClaveAusente_RechazaConfiguracion(string? secret)
    {
        var settings = new JwtSettings { Secret = secret! };
        Assert.Throws<InvalidOperationException>(() => settings.Validar());
    }

    [Fact]
    public void Validar_ClaveDe63Caracteres_RechazaSinExponerLaClave()
    {
        var secret = new string('a', 63);
        var error = Assert.Throws<InvalidOperationException>(
            () => new JwtSettings { Secret = secret }.Validar());
        Assert.DoesNotContain(secret, error.Message);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(100)]
    public void Validar_ClaveConLongitudPermitida_Acepta(int length)
    {
        var settings = new JwtSettings { Secret = new string('a', length) };
        Assert.Null(Record.Exception(() => settings.Validar()));
    }

    [Theory]
    [InlineData("CAMBIAR")]
    [InlineData("cambiar")]
    [InlineData("Cambiar")]
    public void Validar_ClaveDeEjemploAunqueSeaLarga_Rechaza(string prefix)
    {
        var settings = new JwtSettings { Secret = prefix + new string('x', 64) };
        Assert.Throws<InvalidOperationException>(() => settings.Validar());
    }
}
