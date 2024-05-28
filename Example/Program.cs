using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Chr.Avro.Confluent;
using System.Text.Json;

namespace KafkaTest;

public class Program
{
    public static void Main(string[] args)
    {
        var registryConfig = new SchemaRegistryConfig()
        {
            Url = "http://127.0.0.1:7575",
        };

        var schemaClient = new CachedSchemaRegistryClient(registryConfig);

        var consumerConfig = new ConsumerConfig()
        {
            BootstrapServers = "127.0.0.1:9092,127.0.0.1:9093,127.0.0.1:9094",
            GroupId = "sepehr-test",
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        var consumer = new ConsumerBuilder<Ignore, CatEvent>(consumerConfig)
            .SetAvroValueDeserializer(schemaClient)
            .SetStatisticsHandler((_, stat) => Console.WriteLine(stat))
            .SetErrorHandler((_, err) => Console.WriteLine(err))
            .Build();

        consumer.Subscribe("cat-inv-catland.Catland.dbo.Cats");

        var writeOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };
       
        while(true)
        {
            var msg = consumer.Consume();

            var payload = msg.Message.Value;

            Console.WriteLine(JsonSerializer.Serialize(payload, writeOptions));
            Console.WriteLine();

            consumer.Commit(msg);
        }
    }
}
