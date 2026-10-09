using ConsultaRuc.Core;
using ConsultaRuc.Core.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;

namespace ConsultaRuc.Tests;

public sealed class ConsultaConCacheTests : IDisposable
{
    private static readonly DatosRuc Sunat = new("20131312955", "SUNAT", "ACTIVO", "HABIDO", null, "150101", "LIMA", "LIMA", "LIMA");
    private static readonly DatosDni Persona = new("12345678", "ANA MARIA", "QUISPE", "ROJAS"); // inventada

    private readonly RelojManual _reloj = new();
    private readonly MemoryCache _memoria;
    private readonly CacheOpciones _opciones = new()
    {
        DuracionEncontrado = TimeSpan.FromHours(24),
        DuracionNoEncontrado = TimeSpan.FromHours(1),
    };

    public ConsultaConCacheTests()
    {
        _memoria = new MemoryCache(new MemoryCacheOptions { Clock = _reloj, SizeLimit = 100 });
    }

    [Fact]
    public async Task Encontrado_SegundaConsulta_NoLlamaALaFuente()
    {
        var fuente = new FuenteFalsa { Ruc = _ => Sunat };
        var cache = Crear(fuente);

        var primera = await cache.ConsultarRucAsync("20131312955");
        var segunda = await cache.ConsultarRucAsync("20131312955");

        Assert.Equal(Sunat, primera);
        Assert.Equal(Sunat, segunda);
        Assert.Equal(1, fuente.Llamadas);
    }

    [Fact]
    public async Task Encontrado_VenceA_Las24Horas()
    {
        var fuente = new FuenteFalsa { Ruc = _ => Sunat };
        var cache = Crear(fuente);
        await cache.ConsultarRucAsync("20131312955");

        _reloj.Avanzar(TimeSpan.FromHours(23));
        await cache.ConsultarRucAsync("20131312955");
        Assert.Equal(1, fuente.Llamadas);

        _reloj.Avanzar(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        await cache.ConsultarRucAsync("20131312955");
        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public async Task NoExiste_SeGuarda_YVenceALaHora()
    {
        var fuente = new FuenteFalsa { Ruc = _ => null };
        var cache = Crear(fuente);

        Assert.Null(await cache.ConsultarRucAsync("20100000050"));
        Assert.Null(await cache.ConsultarRucAsync("20100000050"));
        Assert.Equal(1, fuente.Llamadas);

        _reloj.Avanzar(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        await cache.ConsultarRucAsync("20100000050");
        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public async Task ServicioNoDisponible_NoSeGuarda_YLaSiguienteReintenta()
    {
        var falla = true;
        var fuente = new FuenteFalsa
        {
            Ruc = _ => falla ? throw new ServicioNoDisponibleException("apis.net.pe", "429") : Sunat,
        };
        var cache = Crear(fuente);

        await Assert.ThrowsAsync<ServicioNoDisponibleException>(() => cache.ConsultarRucAsync("20131312955"));

        falla = false;
        var resultado = await cache.ConsultarRucAsync("20131312955");

        Assert.Equal(Sunat, resultado);
        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public async Task RucYDni_ConLosMismosDigitos_NoSeMezclan()
    {
        // Mismo texto en las dos claves si no llevaran prefijo: "ruc:" y "dni:" las separan.
        var fuente = new FuenteFalsa { Ruc = _ => Sunat, Dni = _ => Persona };
        var cache = Crear(fuente);

        await cache.ConsultarRucAsync("12345678");
        var dni = await cache.ConsultarDniAsync("12345678");

        Assert.Equal(Persona, dni);
        Assert.Equal(2, fuente.Llamadas);
    }

    [Fact]
    public async Task DocumentosDistintos_SeGuardanPorSeparado()
    {
        var fuente = new FuenteFalsa { Ruc = ruc => Sunat with { Ruc = ruc } };
        var cache = Crear(fuente);

        var a = await cache.ConsultarRucAsync("20131312955");
        var b = await cache.ConsultarRucAsync("20122476309");

        Assert.Equal("20131312955", a!.Ruc);
        Assert.Equal("20122476309", b!.Ruc);
        Assert.Equal(2, fuente.Llamadas);
    }

    [Theory]
    [InlineData(0, 1, 10)]
    [InlineData(1, 0, 10)]
    [InlineData(1, 1, 0)]
    [InlineData(-1, 1, 10)]
    public void Opciones_ValoresNoPositivos_NoSonValidas(int horasEncontrado, int horasNoEncontrado, int maxEntradas)
    {
        var opciones = new CacheOpciones
        {
            DuracionEncontrado = TimeSpan.FromHours(horasEncontrado),
            DuracionNoEncontrado = TimeSpan.FromHours(horasNoEncontrado),
            MaxEntradas = maxEntradas,
        };

        Assert.False(opciones.EsValida());
    }

    public void Dispose() => _memoria.Dispose();

    private ConsultaConCache Crear(IConsultaProveedor fuente) => new(fuente, _memoria, Options.Create(_opciones));

    private sealed class FuenteFalsa : IConsultaProveedor
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

    /// <summary>Reloj que solo avanza cuando la prueba lo pide.</summary>
    private sealed class RelojManual : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

        public void Avanzar(TimeSpan tiempo) => UtcNow += tiempo;
    }
}
