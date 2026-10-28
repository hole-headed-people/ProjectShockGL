using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL.Tools;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

internal static class ShaderParameterJson
{
    public static void Add(ShaderParameters parameters, string name, object? value, UniformType type)
    {
        try
        {
            var token = value as JToken ?? (value == null ? JValue.CreateNull() :
                JToken.FromObject(value, JsonSerializer.Create(JsonHelper.GetSettings())));
            
            switch (type)
            {
                case UniformType.Float: 
                    parameters.Set(name, Number(token)); 
                    break;
                case UniformType.Int: 
                    parameters.Set(name, Integer(token)); 
                    break;
                case UniformType.Bool:
                    if (token.Type == JTokenType.Boolean)
                    {
                        parameters.Set(name, token.Value<bool>());
                    }
                    else
                    {
                        var number = Integer(token);
                        
                        if (number is not (0 or 1))
                        {
                            throw new InvalidDataException("Expected true/false or 0/1.");
                        }
                        
                        parameters.Set(name, number != 0);
                    }
                    
                    break;
                case UniformType.FloatVec2:
                    var v2 = Components(token, 2);
                    parameters.Set(name, new Vector2(v2[0], v2[1]));
                    break;
                case UniformType.FloatVec3:
                    var v3 = Components(token, 3);
                    parameters.Set(name, new Vector3(v3[0], v3[1], v3[2]));
                    break;
                case UniformType.FloatVec4:
                    var v4 = Components(token, 4);
                    parameters.Set(name, new Vector4(v4[0], v4[1], v4[2], v4[3]));
                    break;
                case UniformType.FloatMat4:
                    var matrix = Matrix(token);
                    parameters.Set(name, new Matrix4x4(
                        matrix[0], matrix[1], matrix[2], matrix[3],
                        matrix[4], matrix[5], matrix[6], matrix[7],
                        matrix[8], matrix[9], matrix[10], matrix[11],
                        matrix[12], matrix[13], matrix[14], matrix[15]));
                    break;
                default: throw new InvalidDataException($"Uniform type {type} is not supported by profiles.");
            }
        }
        catch (Exception error) when (error is InvalidDataException or JsonException or OverflowException or
                                     FormatException or ArgumentException or InvalidCastException)
        {
            throw new InvalidDataException($"Postprocess parameter '{name}' ({type}): {error.Message}", error);
        }
    }

    private static float Number(JToken token)
    {
        if (token.Type is not (JTokenType.Integer or JTokenType.Float))
        {
            throw new InvalidDataException("Expected a JSON number.");
        }
        
        var value = token.Value<double>();
        var result = (float)value;

        if (!float.IsFinite(result))
        {
            throw new InvalidDataException("Expected a finite float value.");
        }
        
        return result;
    }

    private static int Integer(JToken token)
    {
        if (token.Type is not (JTokenType.Integer or JTokenType.Float))
            throw new InvalidDataException("Expected an integer number.");
        var value = token.Value<decimal>();
        if (value != decimal.Truncate(value) || value < int.MinValue || value > int.MaxValue)
            throw new InvalidDataException("Expected a whole number in the Int32 range.");
        return (int)value;
    }

    private static float[] Components(JToken token, int count)
    {
        if (token is JArray array && array.Count == count)
        {
            return array.Select(Number).ToArray();
        }
        
        if (token is JObject obj)
        {
            var names = obj.GetValue("x", StringComparison.OrdinalIgnoreCase) != null ? "xyzw" : "rgba";
            
            return Enumerable.Range(0, count).Select(i => Number(obj.GetValue(names[i].ToString(),
                StringComparison.OrdinalIgnoreCase) ?? throw new InvalidDataException($"Missing component '{names[i]}'."))).ToArray();
        }
        
        throw new InvalidDataException($"Expected {count} numeric components.");
    }

    private static float[] Matrix(JToken token)
    {
        if (token is JArray array)
        {
            if (array.Count == 16)
            {
                return array.Select(Number).ToArray();
            }

            if (array.Count == 4 && array.All(row => row is JArray { Count: 4 }))
            {
                return array.Cast<JArray>().SelectMany(row => row.Select(Number)).ToArray();
            }
        }
        
        if (token is JObject obj)
        {
            return Enumerable.Range(0, 16).Select(i => Number(obj.GetValue($"M{i / 4 + 1}{i % 4 + 1}",
                StringComparison.OrdinalIgnoreCase) ?? throw new InvalidDataException("Expected M11..M44 matrix components."))).ToArray();
        }
        
        throw new InvalidDataException("Expected a row-major 4x4 matrix (16 values or four rows).");
    }
}
