using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using StackExchange.Redis;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace gs_microsservices.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ConsumoController : ControllerBase
    {
        private readonly IMongoCollection<dynamic> _consumoCollection;
        private readonly IConnectionMultiplexer _redis;

        public ConsumoController(IOptions<MongoSettings> settings, IMongoClient mongoClient, IConnectionMultiplexer redis)
        {
            _redis = redis;
            var database = mongoClient.GetDatabase(settings.Value.DatabaseName);
            _consumoCollection = database.GetCollection<dynamic>("Consumos");
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarConsumo([FromBody] object dadosConsumo)
        {
            if (dadosConsumo == null)
                return BadRequest("Dados inválidos");

            // Salva os dados no MongoDB
            await _consumoCollection.InsertOneAsync(dadosConsumo);

            // Invalida o cache após nova inserção
            var cache = _redis.GetDatabase();
            await cache.KeyDeleteAsync("consumos");

            return Created("", new { Message = "Consumo registrado com sucesso" });
        }

        [HttpGet]
        public async Task<IActionResult> ConsultarConsumo()
        {
            var cache = _redis.GetDatabase();
            var cacheKey = "consumos";
            var cachedData = await cache.StringGetAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                // Garantimos que o valor é tratado como não-nulo
                return Ok(JsonConvert.DeserializeObject<List<dynamic>>(cachedData)!);
            }

            // Caso não tenha cache, consulta o MongoDB
            var consumos = await _consumoCollection.Find(_ => true).ToListAsync();
            if (!consumos.Any())
                return NotFound("Nenhum dado encontrado");

            // Armazena no cache com expiração de 5 minutos
            await cache.StringSetAsync(cacheKey, JsonConvert.SerializeObject(consumos), TimeSpan.FromMinutes(5));

            return Ok(consumos);
        }
    }
    public class MongoSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
    }
}
