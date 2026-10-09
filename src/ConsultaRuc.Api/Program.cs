using System.ComponentModel;
using ConsultaRuc.Core;
using ConsultaRuc.Core.ApisNetPe;
using ConsultaRuc.Core.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddHttpClient<ApisNetPeCliente>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["ApisNetPe:BaseUrl"] ?? "https://api.apis.net.pe/");
    http.Timeout = TimeSpan.FromSeconds(10);
    http.DefaultRequestHeaders.UserAgent.ParseAdd("ConsultaRucPe/1.0 (+https://github.com/rmm-01/consulta-ruc-pe)");
});

// ValidateOnStart: una duración en cero o negativa detiene la API al iniciar, no en la primera consulta.
builder.Services.AddOptions<CacheOpciones>()
    .Bind(builder.Configuration.GetSection(CacheOpciones.Seccion))
    .Validate(o => o.EsValida(), "Las duraciones y MaxEntradas de la caché deben ser mayores que cero.")
    .ValidateOnStart();

builder.Services.AddSingleton<IMemoryCache>(sp => new MemoryCache(new MemoryCacheOptions
{
    SizeLimit = sp.GetRequiredService<IOptions<CacheOpciones>>().Value.MaxEntradas,
}));

// El resto de la API pide IConsultaProveedor y recibe el cliente envuelto en la caché.
builder.Services.AddTransient<IConsultaProveedor>(sp => new ConsultaConCache(
    sp.GetRequiredService<ApisNetPeCliente>(),
    sp.GetRequiredService<IMemoryCache>(),
    sp.GetRequiredService<IOptions<CacheOpciones>>()));

var app = builder.Build();

// La documentación interactiva solo se expone en desarrollo, no en producción.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(opciones => opciones.WithTitle("API de consulta de RUC y DNI"));
    app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();
}

app.UseHttpsRedirection();

app.MapGet("/ruc/{numero}", (
    [Description("RUC de 11 dígitos.")] string numero,
    IConsultaProveedor proveedor, ILogger<Program> logger, CancellationToken ct) =>
    ConsultarAsync("RUC", numero, DocumentoValidador.ValidarRuc,
        () => proveedor.ConsultarRucAsync(numero, ct), logger))
.WithName("ConsultarRuc")
.WithSummary("Datos de un contribuyente por RUC")
.WithDescription(
    "Valida el RUC (longitud, prefijo y dígito verificador) antes de consultar. " +
    "400: el número no es válido. 404: es válido pero no está registrado. 503: la fuente externa no respondió.")
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapGet("/dni/{numero}", (
    [Description("DNI de 8 dígitos.")] string numero,
    IConsultaProveedor proveedor, ILogger<Program> logger, CancellationToken ct) =>
    ConsultarAsync("DNI", numero, DocumentoValidador.ValidarDni,
        () => proveedor.ConsultarDniAsync(numero, ct), logger))
.WithName("ConsultarDni")
.WithSummary("Nombre de una persona por DNI")
.WithDescription(
    "Valida que el DNI tenga 8 dígitos antes de consultar. " +
    "400: el número no es válido. 404: no está registrado. 503: la fuente externa no respondió.")
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.Run();

// Mismo flujo para RUC y DNI: validar sin llamar a nadie, consultar y traducir cada resultado a su código HTTP.
static async Task<Results<Ok<T>, ValidationProblem, ProblemHttpResult>> ConsultarAsync<T>(
    string documento, string numero, Func<string, string?> validar, Func<Task<T?>> consultar, ILogger logger)
    where T : class
{
    var error = validar(numero);
    if (error is not null)
        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["numero"] = [error] });

    try
    {
        var datos = await consultar();
        if (datos is null)
            return TypedResults.Problem(
                title: $"{documento} no encontrado",
                detail: $"El {documento} {numero} no figura en la fuente consultada.",
                statusCode: StatusCodes.Status404NotFound);

        return TypedResults.Ok(datos);
    }
    catch (ServicioNoDisponibleException ex)
    {
        logger.LogWarning(ex, "No se pudo consultar el {Documento} {Numero}.", documento, numero);
        // 503 y no 400: el número puede ser correcto; lo que falló es la fuente externa.
        return TypedResults.Problem(
            title: "Servicio de consulta no disponible",
            detail: $"No se pudo consultar el {documento} en este momento. Intente más tarde.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

// Permite que las pruebas de integración levanten la API con WebApplicationFactory<Program>.
public partial class Program;
