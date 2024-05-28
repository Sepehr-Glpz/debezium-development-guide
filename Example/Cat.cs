
using Avro;
using Avro.Specific;

namespace KafkaTest;
public class Cat
{
    public long Id { get; set; }

    public int? Power { get; set; }

    public string? Name { get; set; }

    public string? LastName { get; set; }

    public long? CreationDate { get; set; }
}
