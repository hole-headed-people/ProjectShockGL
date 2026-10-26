using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProjectShockGL.Tools.Converters;

public class Vector3Converter : JsonConverter<Vector3>
{
    public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        
        writer.WriteValue(value.X);
        writer.WriteValue(value.Y);
        writer.WriteValue(value.Z);
        
        writer.WriteEndArray();
    }

    public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        var array = JArray.Load(reader);

        return new Vector3(
            array[0].Value<float>(),
            array[1].Value<float>(),
            array[2].Value<float>()
        );
    }
}