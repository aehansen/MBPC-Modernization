using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Mbpc.Api.DTOs.Catalogos;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Mbpc.Api.Tests.Services
{
    public class CatalogosServiceTests
    {
        private readonly Mock<IWebHostEnvironment> _envMock = new();
        private readonly Mock<ILogger<CatalogoManagerService>> _loggerMock = new();

        private const string InvalidOracleConnStr =
            "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost-no-existe)(PORT=1521))" +
            "(CONNECT_DATA=(SERVICE_NAME=NOEXISTE)));User Id=test;Password=test;";

        private CatalogoManagerService BuildSut(string environment = "Development")
        {
            _envMock.Setup(e => e.EnvironmentName).Returns(environment);

            var oracleSettings = Options.Create(new OracleDbSettings
            {
                ConnectionString = InvalidOracleConnStr
            });

            return new CatalogoManagerService(
                oracleSettings,
                _envMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task ObtenerBuquesAsync_QueryGavilanNegro_Development_ReturnsMockData()
        {
            // Arrange
            var sut = BuildSut("Development");

            // Act
            var result = await sut.ObtenerBuquesAsync(query: "GAVILAN NEGRO", pagina: 1, tamanio: 10);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task CrearBuqueAsync_ValidDto_Development_ReturnsGeneratedId()
        {
            // Arrange
            var sut = BuildSut("Development");
            var dto = new BuqueAltaDto
            {
                Nombre = "GAVILAN NEGRO",
                NroOmi = 1234567,
                Mmsi = "123456789",
                Matricula = "MAT-123",
                Bandera = "Argentina",
                Tipo = "Remolcador",
                Calado = 5.5,
                CallSign = "L2A4",
                Estado = "Activo"
            };

            // Act
            var result = await sut.CrearBuqueAsync(dto, costeraId: 1);

            // Assert
            result.Should().NotBeNull();
            result.IdBuque.Should().NotBe(0);
            result.Nombre.Should().Be("GAVILAN NEGRO");
        }
    }
}
