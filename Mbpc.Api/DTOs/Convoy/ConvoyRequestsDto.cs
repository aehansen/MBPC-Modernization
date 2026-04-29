// Mbpc.Api/DTOs/Convoy/ConvoyRequestsDto.cs

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs.Convoy;

/// <summary>
/// Payload para adjuntar barcazas a un convoy en viaje.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Hito 6.3 — Orquestación Híbrida:</strong>
/// <c>BarcazasIds</c> acepta identificadores string (ej: <c>"BCZ-999"</c>) en lugar de
/// <c>long</c>, ya que los viajes legacy en Oracle no garantizan IDs puramente numéricos.
/// </para>
/// </remarks>
public sealed class AdjuntarBarcazasRequest
{
    /// <summary>
    /// Lista de identificadores de barcazas a adjuntar al convoy.
    /// Admite tanto IDs numéricos como alfanuméricos (ej: "BCZ-001", "999").
    /// </summary>
    [Required(ErrorMessage = "Debe especificar al menos una barcaza.")]
    [MinLength(1, ErrorMessage = "La lista de barcazas no puede estar vacía.")]
    public List<string> BarcazasIds { get; init; } = [];

    /// <summary>
    /// Ubicación geográfica o nombre de punto donde se realiza la maniobra de adjuntar.
    /// </summary>
    [Required(ErrorMessage = "La ubicación es obligatoria.")]
    public string Ubicacion { get; init; } = string.Empty;
}

/// <summary>
/// Payload para separar barcazas de un convoy en viaje.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Hito 6.3 — Orquestación Híbrida:</strong>
/// <c>BarcazasIds</c> acepta identificadores string (ej: <c>"BCZ-999"</c>) en lugar de
/// <c>long</c>, ya que los viajes legacy en Oracle no garantizan IDs puramente numéricos.
/// </para>
/// </remarks>
public sealed class SepararConvoyRequest
{
    /// <summary>
    /// Lista de identificadores de barcazas a separar del convoy.
    /// Admite tanto IDs numéricos como alfanuméricos (ej: "BCZ-001", "999").
    /// </summary>
    [Required(ErrorMessage = "Debe especificar al menos una barcaza.")]
    [MinLength(1, ErrorMessage = "La lista de barcazas no puede estar vacía.")]
    public List<string> BarcazasIds { get; init; } = [];

    /// <summary>
    /// Ubicación geográfica o nombre de punto donde se realiza la maniobra de separar.
    /// </summary>
    [Required(ErrorMessage = "La ubicación es obligatoria.")]
    public string Ubicacion { get; init; } = string.Empty;
}