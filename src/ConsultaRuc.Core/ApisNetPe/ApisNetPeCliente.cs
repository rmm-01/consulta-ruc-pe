using System.Net;
using System.Text.Json;

namespace ConsultaRuc.Core.ApisNetPe;

/// <summary>
/// Cliente de la API v1 de apis.net.pe: gratuita y sin token, pero con límite de consultas (responde 429).
/// </summary>
public sealed class ApisNetPeCliente(HttpClient http) : IConsultaProveedor
{
    public const string Servicio = "apis.net.pe";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<DatosRuc?> ConsultarRucAsync(string ruc, CancellationToken cancellationToken = default)
    {
        var respuesta = await PedirAsync<RespuestaRuc>($"v1/ruc?numero={Uri.EscapeDataString(ruc)}", cancellationToken);
        if (respuesta is null)
            return null;

        var razonSocial = Limpiar(respuesta.Nombre)
            ?? throw new ServicioNoDisponibleException(Servicio, "respuesta de RUC sin razón social.");

        return new DatosRuc(
            ruc,
            razonSocial,
            Limpiar(respuesta.Estado),
            Limpiar(respuesta.Condicion),
            Limpiar(respuesta.Direccion),
            Limpiar(respuesta.Ubigeo),
            Limpiar(respuesta.Distrito),
            Limpiar(respuesta.Provincia),
            Limpiar(respuesta.Departamento));
    }

    public async Task<DatosDni?> ConsultarDniAsync(string dni, CancellationToken cancellationToken = default)
    {
        var respuesta = await PedirAsync<RespuestaDni>($"v1/dni?numero={Uri.EscapeDataString(dni)}", cancellationToken);
        if (respuesta is null)
            return null;

        var nombres = Limpiar(respuesta.Nombres)
            ?? throw new ServicioNoDisponibleException(Servicio, "respuesta de DNI sin nombres.");

        return new DatosDni(
            dni,
            nombres,
            Limpiar(respuesta.ApellidoPaterno) ?? "",
            Limpiar(respuesta.ApellidoMaterno) ?? "");
    }

    /// <summary>Devuelve el cuerpo deserializado, o null si la API responde 404 (documento inexistente).</summary>
    private async Task<T?> PedirAsync<T>(string ruta, CancellationToken cancellationToken) where T : class
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.GetAsync(ruta, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ServicioNoDisponibleException(Servicio, "no respondió.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Cancelación sin que la pidiera el llamador = se agotó el tiempo de espera del HttpClient.
            throw new ServicioNoDisponibleException(Servicio, "tiempo de espera agotado.", ex);
        }

        using (respuesta)
        {
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return null;

            // El código de estado se revisa antes de leer el cuerpo: el 429 llega como HTML de nginx, no como JSON.
            if (respuesta.StatusCode == HttpStatusCode.TooManyRequests)
                throw new ServicioNoDisponibleException(Servicio, "límite de consultas alcanzado (429).");

            if (!respuesta.IsSuccessStatusCode)
                throw new ServicioNoDisponibleException(Servicio, $"respondió {(int)respuesta.StatusCode}.");

            // Se leen los bytes y no el texto: ReadAsStringAsync y ReadFromJsonAsync lanzan
            // InvalidOperationException si el charset de la cabecera es desconocido. JSON siempre es UTF-8.
            var cuerpo = await respuesta.Content.ReadAsByteArrayAsync(cancellationToken);
            try
            {
                return JsonSerializer.Deserialize<T>(cuerpo, OpcionesJson)
                    ?? throw new JsonException("Cuerpo vacío.");
            }
            catch (JsonException ex)
            {
                throw new ServicioNoDisponibleException(Servicio, "respuesta con formato inesperado.", ex);
            }
        }
    }

    /// <summary>La API usa "-" para los campos sin dato y a veces deja espacios al final.</summary>
    private static string? Limpiar(string? valor)
    {
        var limpio = valor?.Trim();
        return string.IsNullOrEmpty(limpio) || limpio == "-" ? null : limpio;
    }

    private sealed record RespuestaRuc(
        string? Nombre, string? Estado, string? Condicion, string? Direccion, string? Ubigeo,
        string? Distrito, string? Provincia, string? Departamento);

    private sealed record RespuestaDni(string? Nombres, string? ApellidoPaterno, string? ApellidoMaterno);
}
