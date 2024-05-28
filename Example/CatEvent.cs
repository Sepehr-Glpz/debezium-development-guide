
using Avro;
using Avro.Reflect;

namespace KafkaTest;
public class CatEvent
{
    public Cat? Before { get; set; }

    public Cat? After { get; set; }

    public string? Op { get; set; }
}

