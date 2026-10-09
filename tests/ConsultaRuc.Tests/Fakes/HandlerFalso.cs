using System.Net;

namespace ConsultaRuc.Tests.Fakes;

/// <summary>Simula la red: devuelve lo que indique la prueba, sin salir a internet.</summary>
internal sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public HttpRequestMessage? UltimaPeticion { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UltimaPeticion = request;
        return Task.FromResult(responder(request));
    }

    public static HttpResponseMessage Respuesta(HttpStatusCode status, string cuerpo, string tipo = "application/json") =>
        new(status) { Content = new StringContent(cuerpo, System.Text.Encoding.UTF8, tipo) };
}
