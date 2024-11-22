using Xunit;
using Moq;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using gs_microsservices.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace gs_microsservices.Tests
{
    public class ConsumoControllerTests
    {
        private readonly Mock<IMongoCollection<dynamic>> _mockCollection;
        private readonly Mock<IMongoClient> _mockMongoClient;
        private readonly Mock<IConnectionMultiplexer> _mockRedis;

        public ConsumoControllerTests()
        {
            _mockCollection = new Mock<IMongoCollection<dynamic>>();
            _mockMongoClient = new Mock<IMongoClient>();
            _mockRedis = new Mock<IConnectionMultiplexer>();
        }

        [Fact]
        public async Task RegistrarConsumo_DeveRetornar201_SeDadosValidos()
        {
            // Arrange
            var mockDatabase = new Mock<IMongoDatabase>();
            _mockMongoClient
                .Setup(client => client.GetDatabase(It.IsAny<string>(), null))
                .Returns(mockDatabase.Object);

            mockDatabase
                .Setup(db => db.GetCollection<dynamic>(It.IsAny<string>(), null))
                .Returns(_mockCollection.Object);

            _mockCollection
                .Setup(c => c.InsertOneAsync(It.IsAny<object>(), null, default))
                .Returns(Task.CompletedTask);

            var mockRedisDb = new Mock<IDatabase>();
            _mockRedis
                .Setup(redis => redis.GetDatabase(It.IsAny<int>(), null))
                .Returns(mockRedisDb.Object);

            var controller = new ConsumoController(
                Options.Create(new gs_microsservices.Controllers.MongoSettings { DatabaseName = "Test" }),
                _mockMongoClient.Object,
                _mockRedis.Object
            );

            var dadosValidos = new { Nome = "Teste", Valor = 123 };

            // Act
            var result = await controller.RegistrarConsumo(dadosValidos);

            // Assert
            var createdResult = Assert.IsType<CreatedResult>(result);
            Assert.Equal(201, createdResult.StatusCode);
        }

        [Fact]
        public async Task ConsultarConsumo_DeveRetornar404_SeNaoExistiremDados()
        {
            // Arrange
            var mockDatabase = new Mock<IMongoDatabase>();
            var mockCursor = new Mock<IAsyncCursor<dynamic>>();
            mockCursor.Setup(cursor => cursor.Current).Returns(new List<dynamic>().AsReadOnly());
            mockCursor.SetupSequence(cursor => cursor.MoveNext(It.IsAny<CancellationToken>()))
                      .Returns(true)
                      .Returns(false);
            mockCursor.SetupSequence(cursor => cursor.MoveNextAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(true)
                      .ReturnsAsync(false);

            _mockMongoClient
                .Setup(client => client.GetDatabase(It.IsAny<string>(), null))
                .Returns(mockDatabase.Object);

            mockDatabase
                .Setup(db => db.GetCollection<dynamic>(It.IsAny<string>(), null))
                .Returns(_mockCollection.Object);

            _mockCollection
                .Setup(c => c.FindAsync(It.IsAny<FilterDefinition<dynamic>>(), null, default))
                .ReturnsAsync(mockCursor.Object);

            var controller = new ConsumoController(
                Options.Create(new gs_microsservices.Controllers.MongoSettings { DatabaseName = "Test" }),
                _mockMongoClient.Object,
                _mockRedis.Object
            );

            // Act
            var result = await controller.ConsultarConsumo();

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task ConsultarConsumo_DeveRetornarDadosDoCache_SeExistirem()
        {
            // Arrange
            var mockRedisDb = new Mock<IDatabase>();
            _mockRedis
                .Setup(redis => redis.GetDatabase(It.IsAny<int>(), null))
                .Returns(mockRedisDb.Object);

            mockRedisDb
                .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync("[{\"Nome\":\"Teste\",\"Valor\":123}]");

            var controller = new ConsumoController(
                Options.Create(new gs_microsservices.Controllers.MongoSettings { DatabaseName = "Test" }),
                _mockMongoClient.Object,
                _mockRedis.Object
            );

            // Act
            var result = await controller.ConsultarConsumo();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }
    }
}
