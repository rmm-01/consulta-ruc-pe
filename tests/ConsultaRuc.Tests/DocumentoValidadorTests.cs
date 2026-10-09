using ConsultaRuc.Core;

namespace ConsultaRuc.Tests;

public class DocumentoValidadorTests
{
    [Theory]
    [InlineData("20131312955")] // SUNAT (público)
    [InlineData("20122476309")] // BCRP (público)
    [InlineData("10123456781")] // persona natural, DNI inventado 12345678
    [InlineData("20100000050")] // inventado: resto 1, 11 - 1 = 10 -> verificador 0
    [InlineData("20100000131")] // inventado: resto 0, 11 - 0 = 11 -> verificador 1
    public void ValidarRuc_Valido_DevuelveNull(string ruc)
    {
        Assert.Null(DocumentoValidador.ValidarRuc(ruc));
    }

    [Theory]
    [InlineData(null, "obligatorio")]
    [InlineData("", "obligatorio")]
    [InlineData("2013131295", "11 dígitos")]
    [InlineData("201313129550", "11 dígitos")]
    [InlineData("2013131295A", "solo puede contener números")]
    [InlineData(" 20131312955", "solo puede contener números")]
    [InlineData("2013131295٥", "solo puede contener números")]
    [InlineData("30131312955", "debe empezar con")]
    [InlineData("20131312956", "dígito verificador")]
    [InlineData("20131312595", "dígito verificador")] // dos dígitos intercambiados
    public void ValidarRuc_Invalido_DevuelveMotivo(string? ruc, string motivo)
    {
        var error = DocumentoValidador.ValidarRuc(ruc);

        Assert.NotNull(error);
        Assert.Contains(motivo, error);
    }

    [Theory]
    [InlineData("2013131295", 5)]
    [InlineData("2012247630", 9)]
    [InlineData("2010000005", 0)]
    [InlineData("2010000013", 1)]
    public void CalcularDigitoVerificador(string base10, int esperado)
    {
        Assert.Equal(esperado, DocumentoValidador.CalcularDigitoVerificador(base10));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("00000001")]
    public void ValidarDni_Valido_DevuelveNull(string dni)
    {
        Assert.Null(DocumentoValidador.ValidarDni(dni));
    }

    [Theory]
    [InlineData(null, "obligatorio")]
    [InlineData("1234567", "8 dígitos")]
    [InlineData("123456789", "8 dígitos")]
    [InlineData("1234567X", "solo puede contener números")]
    [InlineData("1234-567", "solo puede contener números")]
    public void ValidarDni_Invalido_DevuelveMotivo(string? dni, string motivo)
    {
        var error = DocumentoValidador.ValidarDni(dni);

        Assert.NotNull(error);
        Assert.Contains(motivo, error);
    }
}
