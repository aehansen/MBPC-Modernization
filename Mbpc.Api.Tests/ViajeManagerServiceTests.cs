// ============================================================
//  ViajeManagerServiceTests.cs
//  Proyecto  : Mbpc.Api.Tests
//  Framework : xUnit + Moq + FluentAssertions (.NET 8)
//  Método bajo prueba : ViajeManagerService.IniciarViajeAsync
// ============================================================

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Mbpc.Api.DTOs;
using Mbpc.Api.Models;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.Services;
using Mbpc.Api.Services.Auth;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using MongoDB.Driver;

using Oracle.ManagedDataAccess.Client;

using Xunit;

namespace Mbpc.Api.Tests.Services;

/// <summary>
/// Pruebas unitarias para <see cref="ViajeManagerService.IniciarViajeAsync"/>.
///
/// Estrategia de mocking para Oracle / Dapper:
///   Dapper extiende <see cref="System.Data.IDbConnection"/> con métodos de extensión
///   estáticos, lo que impide mockearlos directamente con Moq.
///   La única excepción controlable desde fuera es <see cref="OracleException"/>:
///   si la conexión no puede establecerse, ODP.NET lanza esa excepción antes de
///   invocar Dapper. Por eso:
///     • Flujo feliz / fallo Redis → simulamos entorno DEV (_env.IsDevelopment() == true).
///       El catch de OracleException en IniciarViajeAsync activa el bypass DEV y
///       continúa con un TravelId ficticio, evitando la conexión real a Oracle.
///     • Fallo Oracle producción         → simulamos entorno PROD (_env.IsDevelopment() == false).
///       El catch re-lanza la excepción; el test la captura con Assert.ThrowsAsync.
///
/// Nota: para que la excepción OracleException alcance el catch del servicio,
/// hacemos que la cadena de conexión sea intencionalmente inválida (host inexistente).
/// </summary>
public sealed class ViajeManagerServiceTests : IDisposable
{
    // ── Mocks ────────────────────────────────────────────────────────────────

    private readonly Mock<IMongoClient>                              _mongoClientMock        = new(MockBehavior.Strict);
    private readonly Mock<IMongoDatabase>                            _mongoDatabaseMock      = new(MockBehavior.Strict);
    private readonly Mock<IMongoCollection<ViajePosicionMongo>>      _viajesCollectionMock   = new(MockBehavior.Strict);
    private readonly Mock<IMongoCollection<ViajeDetalleMongo>>       _detallesCollectionMock = new(MockBehavior.Strict);
    private readonly Mock<ILogger<ViajeManagerService>>              _loggerMock             = new();
    private readonly Mock<IWebHostEnvironment>                       _envMock                = new(MockBehavior.Strict);
    private readonly Mock<IDistributedCache>                         _cacheMock              = new(MockBehavior.Strict);
    private readonly Mock<ICosteraUserContext>                       _costeraUserContextMock = new(MockBehavior.Strict);
    private readonly Mock<ICargaService>                             _cargaServiceMock       = new(MockBehavior.Strict);
    private readonly Mock<IBuqueService>                             _buqueServiceMock       = new(MockBehavior.Strict);

    // ── Configuración compartida ─────────────────────────────────────────────

    /// <summary>
    /// Cadena de conexión inválida para provocar OracleException sin conexión real.
    /// ODP.NET intenta resolver el host durante el Open() y falla con
    /// ORA-12541 (TNS: no listener) o similar, que es subclase de OracleException.
    /// </summary>
    private const string InvalidOracleConnStr =
        "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost-no-existe)(PORT=1521))" +
        "(CONNECT_DATA=(SERVICE_NAME=NOEXISTE)));User Id=test;Password=test;";

    private const string ValidMongoDb         = "TestDb";
    private const string LastMbpcCollection   = "last_mbpc";
    private const string DetailsMbpcCollection = "details_mbpc";

    // ── Constructor: configuración base de mocks ─────────────────────────────

    public ViajeManagerServiceTests()
    {
        // MongoDB: la cadena de dependencias es mongoClient → database → collections
        _mongoClientMock
            .Setup(c => c.GetDatabase(ValidMongoDb, null))
            .Returns(_mongoDatabaseMock.Object);

        _mongoDatabaseMock
            .Setup(d => d.GetCollection<ViajePosicionMongo>(LastMbpcCollection, null))
            .Returns(_viajesCollectionMock.Object);

        _mongoDatabaseMock
            .Setup(d => d.GetCollection<ViajeDetalleMongo>(DetailsMbpcCollection, null))
            .Returns(_detallesCollectionMock.Object);

        _costeraUserContextMock
            .Setup(c => c.GetCurrentCosteraId())
            .Returns(1);
    }

    // ── Helpers privados ──────────────────────────────────────────────────────

    /// <summary>Construye la instancia SUT con la configuración actual de mocks.</summary>
    private ViajeManagerService BuildSut(string? oracleConnStr = null)
    {
        var mongoSettings = Options.Create(new MongoDbSettings
        {
            DatabaseName             = ValidMongoDb,
            LastMbpcCollectionName   = LastMbpcCollection,
            DetailsMbpcCollectionName = DetailsMbpcCollection
        });

        var oracleSettings = Options.Create(new OracleDbSettings
        {
            ConnectionString = oracleConnStr ?? InvalidOracleConnStr
        });

        return new ViajeManagerService(
            _mongoClientMock.Object,
            mongoSettings,
            oracleSettings,
            _loggerMock.Object,
            _envMock.Object,
            _cacheMock.Object,
            _costeraUserContextMock.Object,
            _cargaServiceMock.Object,
            _buqueServiceMock.Object);
    }

    /// <summary>DTO mínimo válido para crear un viaje en los tests.</summary>
    private static NuevoViajeDto BuildViajeDto() => new()
    {
        NombreBuque           = "ARA TEST",
        Origen                = "Puerto Buenos Aires",
        Destino               = "Puerto Montevideo",
        MuelleSalida          = "Muelle Norte",
        ProximoPuntoControl   = "ZOE Norte",
        FechaPartida          = DateTime.UtcNow.AddHours(1),
        ETA                   = DateTime.UtcNow.AddHours(8),
        ZOE                   = "ZN-01",
        Latitud               = -34.60m,
        Longitud              = -58.38m,
        RioCanalKmPar         = 100m,
        DeclaracionMalvinas   = DeclaracionMalvinasEnum.NoVieneDeMalvinas_L,
        CosteraId             = "1"
    };

    // ── TEST 1: Flujo Feliz ───────────────────────────────────────────────────

    /// <summary>
    /// ESCENARIO 1 — FlujoFeliz_RetornaTrue
    ///
    /// Dado:
    ///   • Entorno DEV → el bypass de Oracle se activa al recibir OracleException,
    ///     asignando un TravelId ficticio y continuando el flujo.
    ///   • MongoDB (last_mbpc y details_mbpc) configurados para aceptar InsertOneAsync.
    ///   • Redis configurado para responder OK a RemoveAsync.
    ///
    /// Se verifica:
    ///   • El resultado es true.
    ///   • _viajesCollection.InsertOneAsync  fue llamado exactamente 1 vez.
    ///   • _detallesCollection.InsertOneAsync fue llamado exactamente 1 vez.
    ///   • _cache.RemoveAsync fue llamado exactamente 2 veces (una por cada clave).
    /// </summary>
    [Fact]
    public async Task IniciarViajeAsync_FlujoFeliz_RetornaTrue()
    {
        // Arrange
        _envMock.Setup(e => e.EnvironmentName).Returns("Development");

        // InsertOneAsync de ViajePosicionMongo
        _viajesCollectionMock
            .Setup(c => c.InsertOneAsync(
                It.IsAny<ViajePosicionMongo>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // InsertOneAsync de ViajeDetalleMongo
        _detallesCollectionMock
            .Setup(c => c.InsertOneAsync(
                It.IsAny<ViajeDetalleMongo>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Redis: RemoveAsync (2 claves)
        _cacheMock
            .Setup(r => r.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut    = BuildSut();
        var viaje  = BuildViajeDto();

        // Act
        var resultado = await sut.IniciarViajeAsync(viaje);

        // Assert
        resultado.Should().BeTrue("el flujo feliz completo debe retornar true");

        _viajesCollectionMock.Verify(
            c => c.InsertOneAsync(
                It.IsAny<ViajePosicionMongo>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "last_mbpc debe recibir exactamente un InsertOneAsync");

        _detallesCollectionMock.Verify(
            c => c.InsertOneAsync(
                It.IsAny<ViajeDetalleMongo>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "details_mbpc debe recibir exactamente un InsertOneAsync");

        _cacheMock.Verify(
            r => r.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "Redis debe recibir 2 llamadas RemoveAsync (una por cada clave particionada)");
    }

    // ── TEST 2: Fallo en Redis — Degradación Elegante ────────────────────────

    /// <summary>
    /// ESCENARIO 2 — FalloEnRedis_DegradacionElegante_RetornaTrue
    ///
    /// Dado:
    ///   • Entorno DEV (mismo bypass de Oracle que el flujo feliz).
    ///   • MongoDB configurado para ambas colecciones.
    ///   • Redis lanza InvalidOperationException en RemoveAsync (simula cluster caído).
    ///
    /// Se verifica:
    ///   • El resultado es true  (degradación elegante — Redis no es crítico).
    ///   • _logger.LogWarning fue invocado al menos una vez (la Fase 4 loguea el warning).
    ///   • MongoDB SÍ fue llamado (el fallo de Redis no aborta las fases anteriores).
    /// </summary>
    [Fact]
    public async Task IniciarViajeAsync_FalloEnRedis_DegradacionElegante_RetornaTrue()
    {
        // Arrange
        _envMock.Setup(e => e.EnvironmentName).Returns("Development");

        _viajesCollectionMock
            .Setup(c => c.InsertOneAsync(
                It.IsAny<ViajePosicionMongo>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _detallesCollectionMock
            .Setup(c => c.InsertOneAsync(
                It.IsAny<ViajeDetalleMongo>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Redis falla en todos los intentos (incluye los reintentos de Polly)
        _cacheMock
            .Setup(r => r.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis cluster no disponible"));

        var sut   = BuildSut();
        var viaje = BuildViajeDto();

        // Act
        var resultado = await sut.IniciarViajeAsync(viaje);

        // Assert
        resultado.Should().BeTrue(
            "un fallo en Redis es una degradación elegante; el viaje ya fue creado en Oracle y MongoDB");

        // Verificar que se logueó un Warning sobre Redis (Fase 4)
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Redis") ||
                                                   state.ToString()!.Contains("caché") ||
                                                   state.ToString()!.Contains("ADVERTENCIA") ||
                                                   state.ToString()!.Contains("invalidar")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "debe existir al menos un LogWarning relacionado al fallo de Redis");

        // MongoDB sí debe haber sido llamado (el fallo de Redis ocurre en Fase 4)
        _viajesCollectionMock.Verify(
            c => c.InsertOneAsync(
                It.IsAny<ViajePosicionMongo>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "last_mbpc debe haber sido insertado antes del fallo de Redis");

        _detallesCollectionMock.Verify(
            c => c.InsertOneAsync(
                It.IsAny<ViajeDetalleMongo>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "details_mbpc debe haber sido insertado antes del fallo de Redis");
    }

    // ── TEST 3: Fallo en Oracle en entorno Producción ────────────────────────

    /// <summary>
    /// ESCENARIO 3 — FalloEnOracle_EntornoProduccion_LanzaExcepcion
    ///
    /// Dado:
    ///   • Entorno PRODUCTION → el bypass DEV está desactivado.
    ///   • La cadena de conexión Oracle es inválida, lo que hace que ODP.NET
    ///     lance OracleException antes de poder ejecutar el SP.
    ///   • La política Polly reintentará 3 veces y luego re-lanzará la excepción.
    ///   • El catch de IniciarViajeAsync detecta PROD y re-lanza con throw.
    ///
    /// Se verifica:
    ///   • Se lanza OracleException (propagada sin envolver).
    ///   • MongoDB NO fue llamado en ningún momento (Times.Never).
    ///   • Redis  NO fue llamado en ningún momento (Times.Never).
    ///
    /// Nota sobre tiempo de ejecución: Polly aplica back-off exponencial (2s, 4s, 8s),
    /// por lo que este test puede tardar ~14 segundos en entornos reales.
    /// Para pipelines CI con restricciones de tiempo, considerar inyectar la política
    /// como dependencia con tiempos reducidos o usar un CancellationToken con timeout.
    /// </summary>
    [Fact]
    public async Task IniciarViajeAsync_FalloEnOracle_EntornoProduccion_LanzaExcepcion()
    {
        // Arrange
        _envMock.Setup(e => e.EnvironmentName).Returns("Production");

        // No se configuran mocks de MongoDB ni Redis: no deben ser llamados.
        // Si lo fueran, Moq lanzaría MockException (MockBehavior.Strict).

        var sut   = BuildSut(oracleConnStr: InvalidOracleConnStr);
        var viaje = BuildViajeDto();

        // Act & Assert
        var act = async () => await sut.IniciarViajeAsync(viaje);

        await act.Should()
            .ThrowAsync<OracleException>(
                "en producción, un fallo de Oracle debe propagarse sin capturarse");

        // MongoDB y Redis deben permanecer sin llamadas
        _viajesCollectionMock.Verify(
            c => c.InsertOneAsync(
                It.IsAny<ViajePosicionMongo>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "last_mbpc NO debe ser llamado cuando Oracle falla en producción");

        _detallesCollectionMock.Verify(
            c => c.InsertOneAsync(
                It.IsAny<ViajeDetalleMongo>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "details_mbpc NO debe ser llamado cuando Oracle falla en producción");

        _cacheMock.Verify(
            r => r.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Redis NO debe ser llamado cuando Oracle falla en producción");
    }

    // ── Tear-down ─────────────────────────────────────────────────────────────

    public void Dispose()
    {
        // Verificar que todos los mocks Strict no tuvieron llamadas inesperadas
        _mongoClientMock.VerifyAll();
        _mongoDatabaseMock.VerifyAll();
    }
}
