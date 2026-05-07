using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mogri.Json;

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
        if (value is not TConcrete concrete)
        {
            throw new NotSupportedException($"{typeof(TInterface).Name} must serialize as {typeof(TConcrete).Name}.");
        }

        JsonSerializer.Serialize(writer, concrete, options);
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
        var concreteList = new List<TConcrete>(value.Count);

        foreach (var item in value)
        {
            if (item is not TConcrete concrete)
            {
                throw new NotSupportedException($"{typeof(TInterface).Name} items must serialize as {typeof(TConcrete).Name}.");
            }

            concreteList.Add(concrete);
        }

        JsonSerializer.Serialize(writer, concreteList, options);
    }
}
