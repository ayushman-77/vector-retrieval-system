using System.Text.Json;
using Confluent.Kafka;

namespace VectorRetrievalSystem.Api.services
{
    public interface IKafkaProducerService
    {
        Task ProduceAsync<T>(string topic, T message);
    }

    public class KafkaProducerService : IKafkaProducerService
    {
        private readonly IProducer<Null, string> _producer;

        public KafkaProducerService(IConfiguration config)
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:29092"
            };
            _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
        }

        public async Task ProduceAsync<T>(string topic, T message)
        {
            var value = JsonSerializer.Serialize(message);
            await _producer.ProduceAsync(topic, new Message<Null, string> { Value = value });
        }
    }
}
