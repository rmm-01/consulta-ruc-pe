namespace ConsultaRuc.Core;

/// <summary>Datos de una persona identificada por DNI.</summary>
public sealed record DatosDni(string Dni, string Nombres, string ApellidoPaterno, string ApellidoMaterno)
{
    public string NombreCompleto => $"{Nombres} {ApellidoPaterno} {ApellidoMaterno}".Trim();
}
