using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace MobileDiffusion.Json;

/// <summary>
///     Created to handle deserializing to a Dictionary in a better way.
///     Json.Net deserializes dictionaries of string, object to JArray etc when the value contains an array.
/// </summary>
public class CustomContractResolver : DefaultContractResolver
{
    public new static readonly CustomContractResolver Instance = new CustomContractResolver();

    protected override JsonContract CreateContract(Type type)
    {
        JsonContract contract = base.CreateContract(type);

        contract.Converter = new CustomJsonConverter();

        return contract;
    }

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);

        return property;
    }
}
