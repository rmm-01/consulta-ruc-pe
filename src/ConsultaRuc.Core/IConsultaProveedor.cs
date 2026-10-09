namespace ConsultaRuc.Core;

/// <summary>
/// Fuente externa de datos de RUC y DNI.
/// Cada consulta tiene tres resultados posibles: los datos, null si el documento no existe,
/// o <see cref="ServicioNoDisponibleException"/> si no se pudo averiguar.
/// </summary>
public interface IConsultaProveedor
{
    Task<DatosRuc?> ConsultarRucAsync(string ruc, CancellationToken cancellationToken = default);

    Task<DatosDni?> ConsultarDniAsync(string dni, CancellationToken cancellationToken = default);
}

/// <summary>La fuente externa no respondió, limitó las consultas o devolvió algo que no se pudo interpretar.</summary>
public sealed class ServicioNoDisponibleException(string servicio, string mensaje, Exception? inner = null)
    : Exception($"{servicio}: {mensaje}", inner)
{
    public string Servicio { get; } = servicio;
}
