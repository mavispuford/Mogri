using System.Text.Json;
using System.Text.Json.Serialization;

namespace MobileDiffusion.Json;

/// <summary>
/// Enables System.Text.Json serialization/deserialization for interface-typed properties
/// by mapping them to a known concrete implementation.
/// </summary>
public class InterfaceConverter<TInterface, TConcrete> : JsonConverter<TInterface>
    where TConcrete : class, TInterface
{
    public override TInterface? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<TConcrete>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options)
    {
        if (value is TConcrete concrete)
        {
            JsonSerializer.Serialize(writer, concrete, options);
        }
        else
        {
            JsonSerializer.Serialize(writer, value, value!.GetType(), options);
        }
    }
}

/// <summary>
/// Enables System.Text.Json serialization/deserialization for List properties typed with
/// an interface by mapping items to a known concrete implementation.
/// </summary>
public class InterfaceListConverter<TInterface, TConcrete> : JsonConverter<List<TInterface>>
    where TConcrete : class, TInterface
{
    public override List<TInterface>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var concreteList = JsonSerializer.Deserialize<List<TConcrete>>(ref reader, options);
        return concreteList?.Cast<TInterface>().ToList();
    }

    public override void Write(Utf8JsonWriter writer, List<TInterface> value, JsonSerializerOptions options)
    {
        var concreteList = value.OfType<TConcrete>().ToList();
        JsonSerializer.Serialize(writer, concreteList, options);
    }
}
