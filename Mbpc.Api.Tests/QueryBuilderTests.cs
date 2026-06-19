using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Mbpc.Api.DTOs;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Services;
using Mbpc.Api.Services.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Mbpc.Api.Tests
{
    public class QueryBuilderTests
    {
        private readonly Mock<ICosteraUserContext> _costeraUserContextMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly Mock<ILogger<QueryBuilderManagerService>> _loggerMock;
        private readonly Mock<IWebHostEnvironment> _envMock;
        private readonly IOptions<OracleDbSettings> _oracleSettings;

        public QueryBuilderTests()
        {
            _costeraUserContextMock = new Mock<ICosteraUserContext>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _loggerMock = new Mock<ILogger<QueryBuilderManagerService>>();
            _envMock = new Mock<IWebHostEnvironment>();

            _oracleSettings = Options.Create(new OracleDbSettings
            {
                ConnectionString = "" // Vacío para forzar bypass de base de datos
            });

            // Configurar entorno como desarrollo
            _envMock.Setup(e => e.EnvironmentName).Returns("Development");
            _envMock.Setup(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);
        }

        [Fact]
        public async Task ObtenerMetadataAsync_DebeRetornarEstructuraCorrecta()
        {
            // Arrange
            _costeraUserContextMock.Setup(c => c.GetCurrentCosteraId()).Returns(0); // Admin
            var service = new QueryBuilderManagerService(
                _oracleSettings,
                _costeraUserContextMock.Object,
                _httpContextAccessorMock.Object,
                _loggerMock.Object,
                _envMock.Object
            );

            // Act
            var metadata = await service.ObtenerMetadataAsync();

            // Assert
            metadata.Should().NotBeEmpty();
            metadata.Should().Contain(e => e.Name == "Viaje");
            metadata.Should().Contain(e => e.Name == "Buque");

            var viaje = metadata.Find(e => e.Name == "Viaje");
            viaje.Fields.Should().Contain(f => f.Name == "Origen");
            viaje.Fields.Should().Contain(f => f.Name == "Destino");
        }

        [Fact]
        public async Task EjecutarConsultaAsync_DebeFiltrarPorCriterioEnBypassDesarrollo()
        {
            // Arrange
            _costeraUserContextMock.Setup(c => c.GetCurrentCosteraId()).Returns(0); // Admin (sin filtro de seguridad)
            var service = new QueryBuilderManagerService(
                _oracleSettings,
                _costeraUserContextMock.Object,
                _httpContextAccessorMock.Object,
                _loggerMock.Object,
                _envMock.Object
            );

            var request = new QueryRequestDto
            {
                EntidadPrincipal = "Viaje",
                Columnas = new List<string> { "Id", "Origen", "Destino", "Estado" },
                Filtros = new List<QueryFilterDto>
                {
                    new() { Campo = "Origen", Operador = "CONTAINS", Valor = "Zárate" }
                }
            };

            // Act
            var result = await service.EjecutarConsultaAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Columnas.Should().Contain("Puerto de Origen");
            result.Filas.Should().NotBeEmpty();
            result.Filas.Should().HaveCount(1);
            result.Filas[0]["Puerto de Origen"].ToString().Should().Contain("Zárate");
        }

        [Fact]
        public async Task EjecutarConsultaAsync_DebeAplicarFiltroSeguridadGeografica_ParaOperadorJurisdiccional()
        {
            // Arrange
            _costeraUserContextMock.Setup(c => c.GetCurrentCosteraId()).Returns(1); // CosteraId = 1 (Gualeguaychú)
            var service = new QueryBuilderManagerService(
                _oracleSettings,
                _costeraUserContextMock.Object,
                _httpContextAccessorMock.Object,
                _loggerMock.Object,
                _envMock.Object
            );

            var request = new QueryRequestDto
            {
                EntidadPrincipal = "Viaje",
                Columnas = new List<string> { "Id", "Origen", "Destino", "Estado" },
                Filtros = new List<QueryFilterDto>() // Todos los viajes
            };

            // Act
            var result = await service.EjecutarConsultaAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Filas.Should().NotBeEmpty();
            
            // Todos los registros resultantes deben pertenecer a la jurisdicción (CosteraId = 1)
            // En los datos mock: VJ-801 (Costera 1) y VJ-4015 (Costera 1) deben retornar.
            // VJ-3032 (Zarate, Costera 2) debe estar excluido.
            result.Filas.Should().HaveCount(2);
            result.Filas.Should().NotContain(f => f.ContainsValue("Zárate"));
        }
    }
}
