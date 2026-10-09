using System.Net;
using ConsultaRuc.Core;
using ConsultaRuc.Core.ApisNetPe;
using ConsultaRuc.Tests.Fakes;
using static ConsultaRuc.Tests.Fakes.HandlerFalso;

namespace ConsultaRuc.Tests;

public class ApisNetPeClienteTests
{
    // Formato real de la v1 con el RUC público de SUNAT (recortado).
    private const string RucSunat = """
        {"nombre":"SUPERINTENDENCIA NACIONAL DE ADUANAS Y DE ADMINISTRACION TRIBUTARIA - SUNAT","tipoDocumento":"6",
         "numeroDocumento":"20131312955","estado":"ACTIVO","condicion":"HABIDO",
         "direccion":"AV. GARCILASO DE LA VEGA NRO 1472 ","ubigeo":"150101","viaTipo":"AV.","zonaCodigo":"-",
         "interior":"-","distrito":"LIMA","provincia":"LIMA","departamento":"LIMA"}
        """;

    // Persona inventada.
    private const string DniInventado = """
        {"nombres":"ANA MARIA","apellidoPaterno":"QUISPE","apellidoMaterno":"ROJAS","numeroDocumento":"12345678"}
        """;

    // Lo que devuelve nginx cuando se supera el límite de consultas.
    private const string Html429 = "<html><head><title>429 Too Many Requests</title></head><body><center><h1>429 Too Many Requests</h1></center></body></html>";

    [Fact]
    public async Task ConsultarRuc_Existe_DevuelveDatosLimpios()
    {
        var (cliente, handler) = Crear(_ => Respuesta(HttpStatusCode.OK, RucSunat));

        var datos = await cliente.ConsultarRucAsync("20131312955");

        Assert.NotNull(datos);
        Assert.Equal("20131312955", datos.Ruc);
        Assert.StartsWith("SUPERINTENDENCIA NACIONAL", datos.RazonSocial);
        Assert.Equal("ACTIVO", datos.Estado);
        Assert.Equal("HABIDO", datos.Condicion);
        Assert.Equal("AV. GARCILASO DE LA VEGA NRO 1472", datos.Direccion); // sin el espacio final
        Assert.Equal("150101", datos.Ubigeo);
        Assert.Equal("/v1/ruc", handler.UltimaPeticion!.RequestUri!.AbsolutePath);
        Assert.Equal("?numero=20131312955", handler.UltimaPeticion.RequestUri.Query);
    }

    [Fact]
    public async Task ConsultarRuc_CamposConGuion_QuedanEnNull()
    {
        var (cliente, _) = Crear(_ => Respuesta(HttpStatusCode.OK,
            """{"nombre":"EMPRESA INVENTADA SAC","estado":"ACTIVO","condicion":"-","direccion":"-","ubigeo":" - "}"""));

        var datos = await cliente.ConsultarRucAsync("20100000050");

        Assert.Null(datos!.Condicion);
        Assert.Null(datos.Direccion);
        Assert.Null(datos.Ubigeo);
        Assert.Null(datos.Distrito); // campo ausente en el JSON
    }

    [Fact]
    public async Task ConsultarDni_Existe_DevuelveDatos()
    {
        var (cliente, handler) = Crear(_ => Respuesta(HttpStatusCode.OK, DniInventado));

        var datos = await cliente.ConsultarDniAsync("12345678");

        Assert.Equal(new DatosDni("12345678", "ANA MARIA", "QUISPE", "ROJAS"), datos);
        Assert.Equal("ANA MARIA QUISPE ROJAS", datos!.NombreCompleto);
        Assert.Equal("/v1/dni", handler.UltimaPeticion!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Consultar_NoExiste404_DevuelveNull()
    {
        var (cliente, _) = Crear(_ => Respuesta(HttpStatusCode.NotFound, """{"message":"not found"}"""));

        Assert.Null(await cliente.ConsultarRucAsync("20100000050"));
        Assert.Null(await cliente.ConsultarDniAsync("12345678"));
    }

    [Fact]
    public async Task Consultar_LimiteDeConsultas429_LanzaServicioNoDisponible()
    {
        var (cliente, _) = Crear(_ => Respuesta(HttpStatusCode.TooManyRequests, Html429, "text/html"));

        var ex = await Assert.ThrowsAsync<ServicioNoDisponibleException>(() => cliente.ConsultarRucAsync("20131312955"));

        Assert.Equal("apis.net.pe", ex.Servicio);
        Assert.Contains("429", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Consultar_OtrosErroresHttp_LanzaServicioNoDisponible(HttpStatusCode status)
    {
        var (cliente, _) = Crear(_ => Respuesta(status, ""));

        await Assert.ThrowsAsync<ServicioNoDisponibleException>(() => cliente.ConsultarDniAsync("12345678"));
    }

    [Theory]
    [InlineData("<html>Mantenimiento</html>")]
    [InlineData("{\"nombre\":")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{"nombre":"-"}""")]
    [InlineData("""{"estado":"ACTIVO"}""")]
    public async Task ConsultarRuc_Respuesta200Inservible_LanzaServicioNoDisponible(string cuerpo)
    {
        var (cliente, _) = Crear(_ => Respuesta(HttpStatusCode.OK, cuerpo));

        await Assert.ThrowsAsync<ServicioNoDisponibleException>(() => cliente.ConsultarRucAsync("20131312955"));
    }

    [Fact]
    public async Task ConsultarRuc_200ConHtmlDeMantenimiento_LanzaServicioNoDisponible()
    {
        var (cliente, _) = Crear(_ => Respuesta(HttpStatusCode.OK, "<html>En mantenimiento</html>", "text/html"));

        await Assert.ThrowsAsync<ServicioNoDisponibleException>(() => cliente.ConsultarRucAsync("20131312955"));
    }

    [Fact]
    public async Task ConsultarRuc_CharsetDesconocido_LanzaServicioNoDisponible()
    {
        var (cliente, _) = Crear(_ =>
        {
            var r = Respuesta(HttpStatusCode.OK, "<html>Error</html>");
            r.Content.Headers.ContentType!.CharSet = "charset-inventado";
            return r;
        });

        await Assert.ThrowsAsync<ServicioNoDisponibleException>(() => cliente.ConsultarRucAsync("20131312955"));
    }

    [Fact]
    public async Task ConsultarDni_SinNombres_LanzaServicioNoDisponible()
    {
        var (cliente, _) = Crear(_ => Respuesta(HttpStatusCode.OK, """{"apellidoPaterno":"QUISPE"}"""));

        await Assert.ThrowsAsync<ServicioNoDisponibleException>(() => cliente.ConsultarDniAsync("12345678"));
    }

    [Fact]
    public async Task Consultar_ErrorDeRed_LanzaServicioNoDisponible()
    {
        var (cliente, _) = Crear(_ => throw new HttpRequestException("Host desconocido."));

        await Assert.ThrowsAsync<ServicioNoDisponibleException>(() => cliente.ConsultarRucAsync("20131312955"));
    }

    [Fact]
    public async Task Consultar_TiempoAgotado_LanzaServicioNoDisponible()
    {
        var (cliente, _) = Crear(_ => throw new TaskCanceledException("timeout"));

        await Assert.ThrowsAsync<ServicioNoDisponibleException>(() => cliente.ConsultarRucAsync("20131312955"));
    }

    [Fact]
    public async Task Consultar_CanceladoPorElLlamador_PropagaCancelacion()
    {
        var (cliente, _) = Crear(_ => Respuesta(HttpStatusCode.OK, RucSunat));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cliente.ConsultarRucAsync("20131312955", cts.Token));
    }

    private static (ApisNetPeCliente, HandlerFalso) Crear(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new HandlerFalso(responder);
        return (new ApisNetPeCliente(new HttpClient(handler) { BaseAddress = new Uri("https://apis.test/") }), handler);
    }
}
