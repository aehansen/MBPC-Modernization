// Mbpc.Api/DTOs/Convoy/ConvoyDto.cs

namespace Mbpc.Api.DTOs.Convoy;

/// <summary>
/// Representa el estado actual de una barcaza dentro de un convoy.
/// Separa responsabilidades de la BarcazaDto legacy que vivía dentro de ViajeDto.
/// </summary>
public record BarcazaConvoyDto(
    string Id,
    string Nombre,
    string Bandera,
    string? Matricula,
    string TipoCarga,
    double Tonelaje,
    string Unidad,
    string? MuelleActual,
    EstadoBarcaza Estado
);

/// <summary>
/// Representa el remolcador que encabeza el convoy.
/// </summary>
public record RemolcadorConvoyDto(
    string Id,
    string Nombre,
    string Estado,
    DateTimeOffset? FechaSalida
);

/// <summary>
/// DTO raíz que expone la composición explícita de un Convoy.
/// Desacopla la estructura implícita que existía dentro de ViajeDto.
/// </summary>
public class ConvoyDto
{
    /// <summary>Identificador del viaje al que pertenece este convoy.</summary>
    public string ViajeId { get; init; } = null!;

    /// <summary>Nombre del buque / viaje.</summary>
    public string NombreBuque { get; init; } = null!;

    /// <summary>Remolcador que lidera el convoy. Puede ser null si aún no fue asignado.</summary>
    public RemolcadorConvoyDto? Remolcador { get; init; }

    /// <summary>Lista de barcazas que componen el convoy.</summary>
    public IReadOnlyList<BarcazaConvoyDto> Barcazas { get; init; } = [];

    /// <summary>Total de tonelaje consolidado del convoy.</summary>
    public double TonelajeTotal => Barcazas.Sum(b => b.Tonelaje);

    /// <summary>Cantidad de barcazas activas (no fondeadas ni en mantenimiento).</summary>
    public int BarcazasActivas => Barcazas.Count(b => b.Estado == EstadoBarcaza.EnTransito || b.Estado == EstadoBarcaza.Amarrada);
}

/// <summary>
/// Enumera los estados posibles de una barcaza dentro del convoy.
/// Centraliza el vocabulario de dominio que antes era un string libre.
/// </summary>
public enum EstadoBarcaza
{
    EnTransito,
    Amarrada,
    Fondeada,
    EnCarga,
    EnDescarga,
    FueraDeServicio
}

/// <summary>
/// Payload de entrada para la operación de amarre de una barcaza.
/// </summary>
public class AmarrarBarcazaRequest
{
    /// <summary>Identificador del muelle destino. No puede ser nulo ni vacío.</summary>
    public string NuevoMuelle { get; init; } = string.Empty;
}

/// <summary>
/// Payload de entrada para la operación de fondeo de una barcaza.
/// </summary>
public class FondearBarcazaRequest
{
    /// <summary>Zona de fondeo designada. No puede ser nula ni vacía.</summary>
    public string ZonaFondeo { get; init; } = string.Empty;
}
