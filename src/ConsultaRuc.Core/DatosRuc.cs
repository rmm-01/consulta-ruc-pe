namespace ConsultaRuc.Core;

/// <summary>Datos de un contribuyente. Los campos que la fuente no informa quedan en null.</summary>
public sealed record DatosRuc(
    string Ruc,
    string RazonSocial,
    string? Estado,
    string? Condicion,
    string? Direccion,
    string? Ubigeo,
    string? Distrito,
    string? Provincia,
    string? Departamento);
