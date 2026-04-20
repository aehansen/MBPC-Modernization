// Mbpc.Api/DTOs/Convoy/ConvoyRequestsDto.cs

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs.Convoy;

/// <summary>
/// Payload para adjuntar una o más barcazas a un viaje en curso.
/// </summary>
public sealed class AdjuntarBarcazasRequest
{
    /// <summary>
    /// Identificadores numéricos de las barcazas a incorporar al convoy.
    /// </summary>
    [Required]
    public List<long> BarcazasIds { get; init; } = [];

    /// <summary>
    /// Ubicación operacional donde se realiza el adjuntado (e.g. "Puerto Madero — Muelle 3").
    /// </summary>
    [Required]
    public string Ubicacion { get; init; } = string.Empty;
}

/// <summary>
/// Payload para separar una o más barcazas de un convoy en curso.
/// </summary>
public sealed class SepararConvoyRequest
{
    /// <summary>
    /// Identificadores numéricos de las barcazas a retirar del convoy.
    /// </summary>
    [Required]
    public List<long> BarcazasIds { get; init; } = [];

    /// <summary>
    /// Ubicación operacional donde se realiza la separación.
    /// </summary>
    [Required]
    public string Ubicacion { get; init; } = string.Empty;
}
