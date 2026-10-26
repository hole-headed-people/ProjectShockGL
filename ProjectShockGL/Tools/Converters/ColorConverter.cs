using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Tools.Converters;

public class ColorConverter : JsonConverter<Color>
{
    public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var array = JArray.Load(reader);

        return new Color(
            array[0].Value<float>(),
            array[1].Value<float>(),
            array[2].Value<float>(),
            array[3].Value<float>()
        );
    }

    public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        writer.WriteValue(value.R);
        writer.WriteValue(value.G);
        writer.WriteValue(value.B);
        writer.WriteValue(value.A);
        writer.WriteEndArray();
    }
}