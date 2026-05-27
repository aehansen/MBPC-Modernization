using System;
using System.Collections.Generic;

namespace Mbpc.Api.DTOs
{
    // ── DTOs PARA NOTAS DE BITÁCORA ─────────────────────────────────────
    public record NotaBitacoraDto(
        string Id,
        string Texto,
        string Usuario,
        DateTime FechaHora,
        string Categoria
    );

    public record AgregarNotaBitacoraDto(
        string Texto,
        string Categoria
    );

    // ── DTOs PARA AGENCIAS MARÍTIMAS ───────────────────────────────────
    public record AgenciaDto(
        string Rol,
        string Nombre,
        string Contacto
    );

    public record AsignarAgenciaDto(
        string Rol,
        string Nombre,
        string Contacto
    );

    // ── DTOs PARA DATOS PBIP (SEGURIDAD) ───────────────────────────────
    public record DatosPbipDto(
        string ContactoOcpm,
        string NroInmarsat,
        double ArqueoBruto,
        int NivelProteccion
    );

    public record ActualizarDatosPbipDto(
        string ContactoOcpm,
        string NroInmarsat,
        double ArqueoBruto,
        int NivelProteccion
    );

    // ── DTO VISTA CONSOLIDADA (Para el Panel del Operador) ──────────────
    public record ViajeComplementosDto(
        string ViajeId,
        List<NotaBitacoraDto> NotasBitacora,
        List<AgenciaDto> Agencias,
        DatosPbipDto? DatosPbip
    );
}