using ConsultaRuc.Core;
using ConsultaRuc.Core.ApisNetPe;
using ConsultaRuc.Core.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
