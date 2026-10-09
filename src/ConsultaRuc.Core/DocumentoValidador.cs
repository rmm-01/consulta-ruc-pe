namespace ConsultaRuc.Core;

/// <summary>
/// Validación local de RUC y DNI: no llama a ningún servicio.
/// Cada método devuelve el motivo por el que el número no es válido, o null si es válido.
/// </summary>
public static class DocumentoValidador
{
    public const int LongitudRuc = 11;
    public const int LongitudDni = 8;

    // 10 = persona natural con DNI, 15/16/17 = otras personas naturales, 20 = persona jurídica.
    private static readonly string[] PrefijosRuc = ["10", "15", "16", "17", "20"];

    // Pesos que SUNAT aplica a los 10 primeros dígitos del RUC para calcular el verificador.
    private static readonly int[] PesosRuc = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    public static string? ValidarRuc(string? ruc)
    {
        var error = ValidarSoloDigitos(ruc, LongitudRuc, "RUC");
        if (error is not null)
            return error;

        if (!PrefijosRuc.Contains(ruc![..2]))
            return $"El RUC debe empezar con {string.Join(", ", PrefijosRuc)}.";

        if (ruc[^1] - '0' != CalcularDigitoVerificador(ruc))
            return "El dígito verificador del RUC no es correcto.";

        return null;
    }

    public static string? ValidarDni(string? dni) => ValidarSoloDigitos(dni, LongitudDni, "DNI");

    /// <summary>Módulo 11 sobre los 10 primeros dígitos del RUC.</summary>
    public static int CalcularDigitoVerificador(string ruc)
    {
        var suma = 0;
        for (var i = 0; i < PesosRuc.Length; i++)
            suma += (ruc[i] - '0') * PesosRuc[i];

        // 11 - resto da de 1 a 11; los casos de dos cifras se reducen a una.
        return (11 - suma % 11) switch
        {
            10 => 0,
            11 => 1,
            var digito => digito,
        };
    }

    private static string? ValidarSoloDigitos(string? numero, int longitud, string nombre)
    {
        if (string.IsNullOrEmpty(numero))
            return $"El {nombre} es obligatorio.";

        // IsAsciiDigit y no IsDigit: IsDigit también acepta dígitos de otros alfabetos (por ejemplo, '٣').
        if (!numero.All(char.IsAsciiDigit))
            return $"El {nombre} solo puede contener números.";

        if (numero.Length != longitud)
            return $"El {nombre} debe tener {longitud} dígitos.";

        return null;
    }
}
