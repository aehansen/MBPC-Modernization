using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Mbpc.Api.DTOs;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MongoDB.Driver;
using Xunit;

namespace Mbpc.Api.Tests.Services
{
    public class ViajeComplementoServiceTests : IDisposable
    {
        private readonly IMongoClient _mongoClient;
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<ViajeDetalleMongo> _detailsCollection;
        private readonly string _databaseName;

        public ViajeComplementoServiceTests()
        {
            // Conexión a la instancia de MongoDB para pruebas de integración.
            // Para ambientes CI/CD o local, se asume una instancia activa.
            _databaseName = $"Mbpc_IntegrationTests_{Guid.NewGuid():N}";
            _mongoClient = new MongoClient("mongodb://localhost:27017");
            _database = _mongoClient.GetDatabase(_databaseName);
            _detailsCollection = _database.GetCollection<ViajeDetalleMongo>("details_mbpc");
        }

        private ViajeComplementoManagerService CreateSut(IHttpContextAccessor httpContextAccessor)
        {
            var mongoSettings = Options.Create(new MongoDbSettings
            {
                DatabaseName = _databaseName,
                DetailsMbpcCollectionName = "details_mbpc"
            });

            return new ViajeComplementoManagerService(
                _mongoClient,
                mongoSettings,
                httpContextAccessor,
                NullLogger<ViajeComplementoManagerService>.Instance
            );
        }

        [Fact]
        public async Task Debe_Agregar_Nota_Sin_Borrar_Otros_Datos()
        {
            // Arrange: 1. Crear documento base con datos de Agencia y PBIP
            var viajeId = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            var documentoBase = new ViajeDetalleMongo
            {
                Id = viajeId,
                IdViaje = 12345L,
                VesselName = "Remolcador de Prueba",
                Agencias = new List<AgenciaMongo>
                {
                    new AgenciaMongo { Rol = "Principal", Nombre = "Agencia del Plata S.A.", Contacto = "contacto@delplata.com" }
                },
                DatosPbip = new DatosPbipMongo
                {
                    ContactoOcpm = "Oficial OCPM",
                    NroInmarsat = "1234567",
                    ArqueoBruto = 500.50,
                    NivelProteccion = 1
                },
                NotasBitacora = new List<NotaBitacoraMongo>()
            };

            await _detailsCollection.InsertOneAsync(documentoBase);

            // Mock de HttpContext con un usuario por defecto
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            var claims = new[] { new Claim(ClaimTypes.Name, "Operador_Test") };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            var context = new DefaultHttpContext { User = principal };
            mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            var sut = CreateSut(mockHttpContextAccessor.Object);
            var nuevaNotaDto = new AgregarNotaBitacoraDto(
                Texto: "Paso de control exitoso sin novedades en km 102.",
                Categoria: "Navegación"
            );

            // Act: 2. Ejecutar la inyección atómica de la nota
            var resultado = await sut.AgregarNotaBitacoraAsync(viajeId, nuevaNotaDto);

            // Assert: 3. Validar mediante el driver que el documento persiste el estado previo y añade la nota
            var docEnBase = await _detailsCollection.Find(x => x.Id == viajeId).FirstOrDefaultAsync();

            docEnBase.Should().NotBeNull("el documento original debe seguir existiendo");
            
            // Validar que los datos de Agencia y PBIP se conservan intactos (Atomicidad)
            docEnBase.Agencias.Should().HaveCount(1);
            docEnBase.Agencias[0].Nombre.Should().Be("Agencia del Plata S.A.");
            docEnBase.DatosPbip.Should().NotBeNull();
            docEnBase.DatosPbip!.ContactoOcpm.Should().Be("Oficial OCPM");

            // Validar que se añadió el nuevo elemento al array de notas de bitácora
            docEnBase.NotasBitacora.Should().HaveCount(1, "se debe añadir la nueva nota al array de notas de bitácora");
            docEnBase.NotasBitacora![0].Id.Should().Be(resultado.Id);
            docEnBase.NotasBitacora[0].Texto.Should().Be("Paso de control exitoso sin novedades en km 102.");
            docEnBase.NotasBitacora[0].Usuario.Should().Be("Operador_Test");
            docEnBase.NotasBitacora[0].Categoria.Should().Be("Navegación");
        }

        [Fact]
        public async Task Debe_Capturar_Usuario_Desde_HttpContext()
        {
            // Arrange: 1. Crear documento de viaje e insertar
            var viajeId = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            var documentoBase = new ViajeDetalleMongo
            {
                Id = viajeId,
                IdViaje = 54321L,
                VesselName = "Buque de Inspección"
            };

            await _detailsCollection.InsertOneAsync(documentoBase);

            // Mock de HttpContextAccessor con un usuario ficticio "Inspector_Especial_PNA"
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            var claims = new[] { new Claim(ClaimTypes.Name, "Inspector_Especial_PNA") };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            var context = new DefaultHttpContext { User = principal };
            mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            var sut = CreateSut(mockHttpContextAccessor.Object);
            var agregarNotaDto = new AgregarNotaBitacoraDto(
                Texto: "Inspección de bodegas completada con éxito.",
                Categoria: "Inspección"
            );

            // Act: 2. Ejecutar AgregarNotaBitacoraAsync
            var resultado = await sut.AgregarNotaBitacoraAsync(viajeId, agregarNotaDto);

            // Assert: 3. Verificar que no confía en el DTO para el autor y usa el HttpContext
            var docEnBase = await _detailsCollection.Find(x => x.Id == viajeId).FirstOrDefaultAsync();

            docEnBase.Should().NotBeNull();
            docEnBase.NotasBitacora.Should().HaveCount(1);
            docEnBase.NotasBitacora![0].Usuario.Should().Be("Inspector_Especial_PNA", "el autor debe ser extraído de forma segura desde los Claims del Token JWT en HttpContext");
            resultado.Usuario.Should().Be("Inspector_Especial_PNA");
        }

        public void Dispose()
        {
            // Cleanup: Eliminar la base de datos de integración temporal
            try
            {
                _mongoClient.DropDatabase(_databaseName);
            }
            catch
            {
                // Ignorar errores de cleanup
            }
        }
    }
}
