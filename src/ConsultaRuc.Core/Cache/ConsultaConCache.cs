using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ConsultaRuc.Core.Cache;

/// <summary>
/// Envuelve a otro proveedor y recuerda sus respuestas: los documentos encontrados y los "no existe",
/// cada uno con su propia duración. Los errores no se guardan, así la siguiente consulta vuelve a intentar.
/// </summary>
public sealed class ConsultaConCache(IConsultaProveedor fuente, IMemoryCache cache, IOptions<CacheOpciones> opciones)
    : IConsultaProveedor
{
    public Task<DatosRuc?> ConsultarRucAsync(string ruc, CancellationToken cancellationToken = default) =>
        ObtenerAsync($"ruc:{ruc}", () => fuente.ConsultarRucAsync(ruc, cancellationToken));

    public Task<DatosDni?> ConsultarDniAsync(string dni, CancellationToken cancellationToken = default) =>
        ObtenerAsync($"dni:{dni}", () => fuente.ConsultarDniAsync(dni, cancellationToken));

    private async Task<T?> ObtenerAsync<T>(string clave, Func<Task<T?>> consultar) where T : class
    {
        // Se guarda un envoltorio y no el valor directo: así "no existe" (null) se distingue de "no está en caché".
        if (cache.TryGetValue(clave, out Entrada<T>? guardada))
            return guardada!.Valor;

        // Si la fuente lanza ServicioNoDisponibleException, sale de aquí sin guardar nada.
        var valor = await consultar();

        var duracion = valor is null ? opciones.Value.DuracionNoEncontrado : opciones.Value.DuracionEncontrado;
        cache.Set(clave, new Entrada<T>(valor), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = duracion,
            Size = 1, // cada documento cuenta 1 frente a MaxEntradas
        });

        return valor;
    }

    private sealed record Entrada<T>(T? Valor);
}
