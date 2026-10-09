using System.Net;
using System.Net.Http.Json;
using ConsultaRuc.Core;
using ConsultaRuc.Core.ApisNetPe;
using ConsultaRuc.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ConsultaRuc.Tests.Fakes.HandlerFalso;

namespace ConsultaRuc.Tests;

/// <summary>Levanta la API en memoria y comprueba el código HTTP de cada resultado posible.</summary>
public sealed class EndpointsTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DatosRuc Sunat = new("20131312955", "SUNAT", "ACTIVO", "HABIDO", null, "150101", "LIMA", "LIMA", "LIMA");
    private static readonly DatosDni Persona = new("12345678", "ANA MARIA", "QUISPE", "ROJAS"); // inventada

    [Fact]
    public async Task Ruc_Existe_Devuelve200ConLosDatos()
    {
        var proveedor = new ProveedorFalso { Ruc = _ => Sunat };

        var respuesta = await Cliente(proveedor).GetAsync("/ruc/20131312955");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(Sunat, await respuesta.Content.ReadFromJsonAsync<DatosRuc>());
    }

    [Theory]
    [InlineData("20131312956")] // dígito verificador incorrecto
    [InlineData("30131312955")] // prefijo que no existe
    [InlineData("2013131295")]  // 10 dígitos
    [InlineData("2013131295A")]
    public async Task Ruc_Invalido_Devuelve400_SinConsultarLaFuente(string ruc)
    {
        var proveedor = new ProveedorFalso();

        var respuesta = await Cliente(proveedor).GetAsync($"/ruc/{ruc}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.True(problema!.Errors.ContainsKey("numero"));
        Assert.Equal(0, proveedor.Llamadas);
    }

    [Fact]
    public async Task Ruc_ValidoPeroNoExiste_Devuelve404()
    {
        var proveedor = new ProveedorFalso { Ruc = _ => null };

        var respuesta = await Cliente(proveedor).GetAsync("/ruc/20100000050");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("RUC no encontrado", problema!.Title);
    }

    [Fact]
    public async Task Ruc_ServicioNoDisponible_Devuelve503_NoUn400()
    {
        var proveedor = new ProveedorFalso { Ruc = _ => throw new ServicioNoDisponibleException("apis.net.pe", "429") };

        var respuesta = await Cliente(proveedor).GetAsync("/ruc/20131312955");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Servicio de consulta no disponible", problema!.Title);
    }

    [Fact]
    public async Task Dni_Existe_Devuelve200ConLosDatos()
    {
        var proveedor = new ProveedorFalso { Dni = _ => Persona };

        var respuesta = await Cliente(proveedor).GetAsync("/dni/12345678");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(Persona, await respuesta.Content.ReadFromJsonAsync<DatosDni>());
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("1234567X")]
    public async Task Dni_Invalido_Devuelve400_SinConsultarLaFuente(string dni)
    {
        var proveedor = new ProveedorFalso();

        var respuesta = await Cliente(proveedor).GetAsync($"/dni/{dni}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, proveedor.Llamadas);
    }

    [Fact]
    public async Task Dni_NoExiste_Devuelve404()
    {
        var respuesta = await Cliente(new ProveedorFalso { Dni = _ => null }).GetAsync("/dni/12345678");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Dni_ServicioNoDisponible_Devuelve503()
    {
        var proveedor = new ProveedorFalso { Dni = _ => throw new ServicioNoDisponibleException("apis.net.pe", "timeout") };

        var respuesta = await Cliente(proveedor).GetAsync("/dni/12345678");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
    }

    [Fact]
    public async Task CadenaCompleta_SegundaConsulta_SaleDeLaCache()
    {
        // Sin reemplazar IConsultaProveedor: endpoint, caché y cliente reales; solo la red es falsa.
        var llamadasALaRed = 0;
        var handler = new HandlerFalso(_ =>
        {
            llamadasALaRed++;
            return Respuesta(HttpStatusCode.OK, """{"nombre":"SUNAT","numeroDocumento":"20131312955","estado":"ACTIVO"}""");
        });
        var cliente = fabrica.WithWebHostBuilder(web => web.ConfigureTestServices(servicios =>
            servicios.AddHttpClient<ApisNetPeCliente>().ConfigurePrimaryHttpMessageHandler(() => handler))).CreateClient();

        var primera = await cliente.GetAsync("/ruc/20131312955");
        var segunda = await cliente.GetAsync("/ruc/20131312955");

        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Equal(1, llamadasALaRed);
    }

    [Fact]
    public async Task CadenaCompleta_Limite429_Devuelve503()
    {
        var handler = new HandlerFalso(_ => Respuesta(HttpStatusCode.TooManyRequests, "<html>429 Too Many Requests</html>", "text/html"));
        var cliente = fabrica.WithWebHostBuilder(web => web.ConfigureTestServices(servicios =>
            servicios.AddHttpClient<ApisNetPeCliente>().ConfigurePrimaryHttpMessageHandler(() => handler))).CreateClient();

        var respuesta = await cliente.GetAsync("/dni/12345678");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
    }

    private HttpClient Cliente(IConsultaProveedor proveedor) =>
        fabrica.WithWebHostBuilder(web => web.ConfigureTestServices(servicios =>
            servicios.AddSingleton(proveedor))).CreateClient();

    private sealed class ProveedorFalso : IConsultaProveedor
    {
        public Func<string, DatosRuc?> Ruc { get; init; } = _ => throw new NotImplementedException();
        public Func<string, DatosDni?> Dni { get; init; } = _ => throw new NotImplementedException();
        public int Llamadas { get; private set; }

        public Task<DatosRuc?> ConsultarRucAsync(string ruc, CancellationToken cancellationToken = default)
        {
            Llamadas++;
            return Task.FromResult(Ruc(ruc));
        }

        public Task<DatosDni?> ConsultarDniAsync(string dni, CancellationToken cancellationToken = default)
        {
            Llamadas++;
            return Task.FromResult(Dni(dni));
        }
    }
}
