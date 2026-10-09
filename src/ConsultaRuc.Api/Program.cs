using ConsultaRuc.Core;
using ConsultaRuc.Core.ApisNetPe;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddHttpClient<IConsultaProveedor, ApisNetPeCliente>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["ApisNetPe:BaseUrl"] ?? "https://api.apis.net.pe/");
    http.Timeout = TimeSpan.FromSeconds(10);
    http.DefaultRequestHeaders.UserAgent.ParseAdd("ConsultaRucPe/1.0 (+https://github.com/rmm-01/consulta-ruc-pe)");
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
