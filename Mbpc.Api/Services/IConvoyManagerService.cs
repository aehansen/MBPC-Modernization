// Mbpc.Api/Services/IConvoyManagerService.cs

using Mbpc.Api.DTOs.Convoy;

namespace Mbpc.Api.Services;

/// <summary>
/// Contrato del servicio de gestión de convoyes.
/// Todas las operaciones son asíncronas para no bloquear el thread pool en I/O de base de datos.
/// </summary>
public interface IConvoyManagerService
{
    /// <summary>
    /// Obtiene la composición completa de un convoy a partir del identificador de viaje.
    /// </summary>
    /// <param name="viajeId">Identificador único del viaje.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <see cref="ConvoyDto"/> con remolcador y barcazas, o <c>null</c> si el viaje no existe.
    /// </returns>
    Task<ConvoyDto?> ObtenerConvoyPorViajeIdAsync(string viajeId, CancellationToken ct = default);

    /// <summary>
    /// Amarra una barcaza a un muelle específico dentro del convoy.
    /// </summary>
    /// <param name="barcazaId">Identificador de la barcaza.</param>
    /// <param name="request">Datos del muelle destino.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <exception cref="KeyNotFoundException">Si la barcaza no existe.</exception>
    /// <exception cref="InvalidOperationException">Si la barcaza está fondeada o fuera de servicio.</exception>
    /// <exception cref="ValidationException">Si el muelle destino es inválido o está bloqueado.</exception>
    Task AmarrarBarcazaAsync(string barcazaId, AmarrarBarcazaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Fondea una barcaza en una zona habilitada.
    /// </summary>
    /// <param name="barcazaId">Identificador de la barcaza.</param>
    /// <param name="request">Zona de fondeo designada.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <exception cref="KeyNotFoundException">Si la barcaza no existe.</exception>
    /// <exception cref="InvalidOperationException">Si la barcaza ya está fondeada o amarrada activamente.</exception>
    /// <exception cref="ValidationException">Si la zona de fondeo está fuera de rango operacional.</exception>
    Task FondearBarcazaAsync(string barcazaId, FondearBarcazaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Adjunta una o más barcazas a un viaje en curso, creando una nueva etapa en MongoDB
    /// y sincronizando con el SP <c>mbpc.adjuntar_barcazas</c> en Oracle.
    /// </summary>
    /// <param name="viajeId">Identificador del viaje (ObjectId de Mongo).</param>
    /// <param name="request">Lista de barcazas a adjuntar y ubicación operacional.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><c>true</c> si la operación fue exitosa; <c>false</c> en caso contrario.</returns>
    /// <exception cref="KeyNotFoundException">Si el viaje no existe en MongoDB.</exception>
    /// <exception cref="InvalidOperationException">Si el viaje no tiene etapas activas.</exception>
    Task<bool> AdjuntarBarcazasAsync(string viajeId, AdjuntarBarcazasRequest request, CancellationToken ct = default);

    /// <summary>
    /// Separa una o más barcazas de un convoy en curso, creando una nueva etapa en MongoDB
    /// y sincronizando con el SP <c>mbpc.separar_convoy</c> en Oracle.
    /// </summary>
    /// <param name="viajeId">Identificador del viaje (ObjectId de Mongo).</param>
    /// <param name="request">Lista de barcazas a separar y ubicación operacional.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><c>true</c> si la operación fue exitosa; <c>false</c> en caso contrario.</returns>
    /// <exception cref="KeyNotFoundException">Si el viaje no existe en MongoDB.</exception>
    /// <exception cref="InvalidOperationException">Si el viaje no tiene etapas activas.</exception>
    Task<bool> SepararConvoyAsync(string viajeId, SepararConvoyRequest request, CancellationToken ct = default);
}
