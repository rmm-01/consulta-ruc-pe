namespace ConsultaRuc.Core.Cache;

/// <summary>Configuración de la caché (sección "Cache" de appsettings.json).</summary>
public sealed class CacheOpciones
{
    public const string Seccion = "Cache";

    /// <summary>Cuánto dura un documento encontrado. Los datos de un RUC o DNI cambian poco.</summary>
    public TimeSpan DuracionEncontrado { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Cuánto dura un "no existe". Más corto: un RUC recién inscrito debe aparecer pronto.</summary>
    public TimeSpan DuracionNoEncontrado { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Máximo de documentos en memoria; al llenarse, la caché descarta entradas.</summary>
    public int MaxEntradas { get; set; } = 10_000;

    public bool EsValida() =>
        DuracionEncontrado > TimeSpan.Zero && DuracionNoEncontrado > TimeSpan.Zero && MaxEntradas > 0;
}
