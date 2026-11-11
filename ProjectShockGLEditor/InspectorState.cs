using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public static class InspectorState
{
    private sealed class State
    {
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public int Layer { get; set; }
        public int CollidesWith { get; set; }
        public bool? SimulatePhysics { get; set; }
        public float? RigidbodyMass { get; set; }
        public TransformState Transform { get; set; }
        public BrushBoxData? Brush { get; set; }
        public Dictionary<string, JToken> Properties { get; set; } = new();
        public OutputData[] Outputs { get; set; } = [];
    }

    public static string Capture(Object obj)
    {
        var serializer = JsonSerializer.Create(JsonHelper.GetSettings());
        
        var state = new State
        {
            Name = obj.Name, IsActive = obj.IsActive, Transform = TransformState.Capture(obj.Transform),
            Layer = obj.Layer, CollidesWith = obj.CollidesWith,
            SimulatePhysics = (obj as Entity)?.Rigidbody?.IsEnabled,
            RigidbodyMass = obj.GetComponent<Rigidbody>() is { IsDestroyed: false } body ? body.Mass : null,
            Brush = obj is BrushBox brush ? new BrushBoxData
            {
                Movable = brush.Movable,
                SimulatePhysics = brush.SimulatePhysics,
                Size = brush.Size, Texture = brush.TextureName, TextureFormat = brush.TextureFormat,
                TextureScale = brush.TextureScale, Color = brush.Color, Lit = brush.Lit
            } : null,
            Outputs = obj is Entity entity ? entity.Outputs.ToArray() : []
        };
        
        if (obj is Entity)
        {
            foreach (var property in EditableProperties.Get(obj.GetType()))
            {
                var value = property.GetValue(obj);
                state.Properties[property.Name] = value == null ? JValue.CreateNull() : JToken.FromObject(value, serializer);
            }
        }
        
        return JsonConvert.SerializeObject(state, Formatting.None, JsonHelper.GetSettings());
    }

    public static void Apply(Object obj, string snapshot)
    {
        obj = EditorObjectReference.Get(obj).Current;
        if (obj.IsDestroyed)
        {
            return;
        }
        
        var state = JsonConvert.DeserializeObject<State>(snapshot, JsonHelper.GetSettings())!;
        var serializer = JsonSerializer.Create(JsonHelper.GetSettings());
        
        obj.Name = state.Name;
        state.Transform.Apply(obj.Transform);
        
        if (obj is BrushBox brush && state.Brush is { } data)
        {
            brush.Size = data.Size;
            brush.Movable = data.Movable;
            brush.SimulatePhysics = data.SimulatePhysics;
            brush.TextureName = data.Texture;
            brush.TextureFormat = data.TextureFormat;
            brush.TextureScale = data.TextureScale;
            brush.Color = data.Color;
            brush.Lit = data.Lit;
        }
        
        if (obj is Entity entity)
        {
            if (state.SimulatePhysics is { } enabled)
            {
                entity.SetSimulatePhysics(enabled);
            }

            foreach (var property in EditableProperties.Get(obj.GetType()))
            {
                if (state.Properties.TryGetValue(property.Name, out var value))
                {
                    var current = property.GetValue(obj);
                    var token = current == null ? JValue.CreateNull() : JToken.FromObject(current, serializer);
                    
                    if (!JToken.DeepEquals(token, value))
                    {
                        property.SetValue(obj, value.ToObject(property.PropertyType, serializer));
                    }
                }
            }
            
            if (!JToken.DeepEquals(JToken.FromObject(entity.Outputs, serializer), JToken.FromObject(state.Outputs, serializer)))
            {
                entity.Outputs.Clear();
                entity.Outputs.AddRange(state.Outputs);
            }
        }
        
        if (state.RigidbodyMass is { } mass && obj.GetComponent<Rigidbody>() is { IsDestroyed: false } body)
        {
            body.Mass = mass;
        }

        obj.Layer = state.Layer;
        obj.CollidesWith = state.CollidesWith;
        obj.SetActive(state.IsActive);
    }
}
