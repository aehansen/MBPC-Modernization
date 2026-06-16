// ============================================================
//  ConvoyManagerServiceTests.cs
//  Proyecto  : Mbpc.Api.Tests
//  Framework : xUnit + Moq + FluentAssertions (.NET 8)
//  Método bajo prueba : ConvoyManagerService.FondearBarcazasAsync
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Mbpc.Api.DTOs.Convoy;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Mbpc.Api.Tests.Services;

public sealed class ConvoyManagerServiceTests
{
    private readonly Mock<IViajeService> _viajeServiceMock = new(MockBehavior.Strict);
    private readonly Mock<ICargaService> _cargaServiceMock = new(MockBehavior.Strict);
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<IMongoClient> _mongoClientMock = new(MockBehavior.Strict);
    private readonly Mock<IMongoDatabase> _mongoDatabaseMock = new(MockBehavior.Strict);
    private readonly Mock<IMongoCollection<ViajeDetalleMongo>> _detallesCollectionMock = new(MockBehavior.Strict);
    private readonly Mock<IMongoCollection<ViajePosicionMongo>> _viajesCollectionMock = new(MockBehavior.Strict);
    private readonly Mock<IHostEnvironment> _envMock = new(MockBehavior.Strict);
    private readonly Mock<ILogger<ConvoyManagerService>> _loggerMock = new();
    private readonly Mock<IMemoryCache> _cacheMock = new();

    private const string ValidMongoDb = "TestDb";
    private const string LastMbpcCollection = "last_mbpc";
    private const string DetailsMbpcCollection = "details_mbpc";

    public ConvoyManagerServiceTests()
    {
        _mongoClientMock
            .Setup(c => c.GetDatabase(ValidMongoDb, null))
            .Returns(_mongoDatabaseMock.Object);

        _mongoDatabaseMock
            .Setup(d => d.GetCollection<ViajeDetalleMongo>(DetailsMbpcCollection, null))
            .Returns(_detallesCollectionMock.Object);

        _mongoDatabaseMock
            .Setup(d => d.GetCollection<ViajePosicionMongo>(LastMbpcCollection, null))
            .Returns(_viajesCollectionMock.Object);
    }

    private ConvoyManagerService CrearServicio(string oracleConnStr = "Data Source=Test;")
    {
        var mongoSettings = Options.Create(new MongoDbSettings
        {
            DatabaseName = ValidMongoDb,
            DetailsMbpcCollectionName = DetailsMbpcCollection,
            LastMbpcCollectionName = LastMbpcCollection
        });

        var oracleSettings = Options.Create(new OracleDbSettings
        {
            ConnectionString = oracleConnStr
        });

        return new ConvoyManagerService(
            _viajeServiceMock.Object,
            _cargaServiceMock.Object,
            _serviceProviderMock.Object,
            _mongoClientMock.Object,
            mongoSettings,
            oracleSettings,
            _envMock.Object,
            _loggerMock.Object,
            _cacheMock.Object
        );
    }

    [Fact]
    public async Task FondearBarcazasAsync_CuandoElViajeNoExiste_LanzaKeyNotFoundException()
    {
        // Arrange
        var viajeId = "507f1f77bcf86cd799439011";
        var request = new FondearBarcazasRequest
        {
            BarcazasIds = new List<string> { "BCZ-001" },
            ZonaFondeo = "Zona Alfa"
        };

        _viajeServiceMock
            .Setup(s => s.GetViajeDetalleByIdAsync(viajeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, 0));

        var service = CrearServicio();

        // Act
        Func<Task> act = () => service.FondearBarcazasAsync(viajeId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"No se encontró el detalle operativo del viaje '{viajeId}'.");
    }

    [Fact]
    public async Task FondearBarcazasAsync_CuandoElRequestEsValidoYEstaEnDesarrollo_FondeaEnMongoDBYEvitaOracle()
    {
        // Arrange
        var viajeId = "507f1f77bcf86cd799439011";
        var request = new FondearBarcazasRequest
        {
            BarcazasIds = new List<string> { "BCZ-001", "BCZ-002" },
            ZonaFondeo = "Zona Alfa"
        };

        var detalleMongo = new ViajeDetalleMongo
        {
            Id = viajeId,
            VesselName = "Remolcador Test",
            Etapas = new List<EtapaMongo>
            {
                new()
                {
                    EtapaId = 1,
                    Barcazas = new List<BarcazaMongo>
                    {
                        new() { Nombre = "BCZ-001", MuelleActual = "Muelle 1" },
                        new() { Nombre = "BCZ-002", MuelleActual = "Muelle 2" },
                        new() { Nombre = "BCZ-003", MuelleActual = "Muelle 3" } // No se toca
                    }
                }
            }
        };

        _viajeServiceMock
            .Setup(s => s.GetViajeDetalleByIdAsync(viajeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((detalleMongo, 12345));

        _envMock
            .Setup(e => e.EnvironmentName)
            .Returns("Development");

        // Simular reemplazo en MongoDB
        _detallesCollectionMock
            .Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<ViajeDetalleMongo>>(),
                It.IsAny<ViajeDetalleMongo>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ReplaceResult>());

        var service = CrearServicio();

        // Act
        await service.FondearBarcazasAsync(viajeId, request, CancellationToken.None);

        // Assert
        // Las barcazas seleccionadas deben tener MuelleActual igual a la ZonaFondeo
        detalleMongo.Etapas.First().Barcazas.First(b => b.Nombre == "BCZ-001").MuelleActual.Should().Be("Zona Alfa");
        detalleMongo.Etapas.First().Barcazas.First(b => b.Nombre == "BCZ-002").MuelleActual.Should().Be("Zona Alfa");
        
        // La barcaza no seleccionada no se debe modificar
        detalleMongo.Etapas.First().Barcazas.First(b => b.Nombre == "BCZ-003").MuelleActual.Should().Be("Muelle 3");

        // El mock de Mongo ReplaceOneAsync debe ser llamado una vez
        _detallesCollectionMock.Verify(c => c.ReplaceOneAsync(
            It.IsAny<FilterDefinition<ViajeDetalleMongo>>(),
            detalleMongo,
            It.IsAny<ReplaceOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
