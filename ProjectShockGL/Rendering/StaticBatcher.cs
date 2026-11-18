using System.Numerics;

namespace ProjectShockGL.Rendering;

internal sealed class StaticBatcher
{
    private readonly Dictionary<BatchKey, Batch> _batches = [];

    private readonly record struct BatchKey(Texture Texture, Color Color, bool Lit, bool BackfaceCulling,
        int RenderOrder, int Segment);
    private readonly record struct Source(Mesh Mesh, int Revision, Matrix4x4 Model);
    private sealed record Batch(Mesh Mesh, Source[] Sources);

    internal void Submit(IReadOnlyList<RenderCommand> commands, RenderQueue target)
    {
        var groups = new Dictionary<BatchKey, List<(MeshCommand Command, Source Source)>>();
        var ordered = commands.OrderBy(command => command.RenderOrder).ToArray();
        var keys = new Dictionary<MeshCommand, BatchKey>();
        var segment = 0;

        foreach (var command in ordered)
        {
            if (command is not MeshCommand mesh || !TrySource(mesh, out var source))
            {
                segment++;
                continue;
            }

            var key = new BatchKey(mesh.Texture!, mesh.Color, mesh.Lit, mesh.BackfaceCulling, mesh.RenderOrder, segment);
            
            if (!groups.TryGetValue(key, out var group))
            {
                group = [];
                groups.Add(key, group);
            }

            group.Add((mesh, source));
            keys.Add(mesh, key);
        }

        var used = new HashSet<BatchKey>();
        var emitted = new HashSet<BatchKey>();
        
        foreach (var command in ordered)
        {
            if (command is not MeshCommand mesh || !keys.TryGetValue(mesh, out var key) || groups[key].Count < 2)
            {
                target.Add(command);
                
                continue;
            }

            if (!emitted.Add(key))
            {
                continue;
            }
            
            used.Add(key);
            
            var sources = groups[key].Select(item => item.Source).ToArray();
            
            if (!_batches.TryGetValue(key, out var batch) || !sources.SequenceEqual(batch.Sources))
            {
                var combined = Build(sources);
                var previous = batch;
                
                batch = new Batch(combined, sources);
                
                _batches[key] = batch;
                previous?.Mesh.Dispose();
            }

            target.Add(new MeshCommand
            {
                Mesh = batch.Mesh, Texture = key.Texture, Color = key.Color, Lit = key.Lit,
                BackfaceCulling = key.BackfaceCulling, RenderOrder = key.RenderOrder,
                Position = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One
            });
        }

        foreach (var key in _batches.Keys.Where(key => !used.Contains(key)).ToArray())
        {
            _batches[key].Mesh.Dispose();
            _batches.Remove(key);
        }
    }

    private static bool TrySource(MeshCommand command, out Source source)
    {
        source = default;
        
        if (!command.StaticBatchEligible || command.Mesh is not { IsDisposed: false } mesh ||
            command.Texture is not { IsOpaque: true, Handle: not 0 } ||
            command.Shader != null || command.IgnoreDepth || command.Color.A != 1f ||
            mesh.Vertices.Length == 0 || mesh.Indices.Length == 0)
        {
            return false;
        }

        var model = Matrix4x4.CreateScale(command.Scale) *
                    Matrix4x4.CreateFromQuaternion(command.Rotation) *
                    Matrix4x4.CreateTranslation(command.Position);
        
        if (!Matrix4x4.Invert(model, out var inverse) || !float.IsFinite(inverse.M11) ||
            !float.IsFinite(inverse.M22) || !float.IsFinite(inverse.M33))
        {
            return false;
        }

        source = new Source(mesh, mesh.Revision, model);
        
        return true;
    }

    private static Mesh Build(Source[] sources)
    {
        var vertices = new float[sources.Sum(source => source.Mesh.Vertices.Length)];
        var indices = new uint[sources.Sum(source => source.Mesh.Indices.Length)];
        
        var vertexOffset = 0;
        var indexOffset = 0;

        foreach (var source in sources)
        {
            Matrix4x4.Invert(source.Model, out var inverse);
            
            var normals = Matrix4x4.Transpose(inverse);
            var input = source.Mesh.Vertices.Span;
            
            for (var i = 0; i < input.Length; i += 8)
            {
                var position = Vector3.Transform(new Vector3(input[i], input[i + 1], input[i + 2]), source.Model);
                var normal = Vector3.TransformNormal(new Vector3(input[i + 3], input[i + 4], input[i + 5]), normals);
                
                var output = vertexOffset + i;
                
                vertices[output] = position.X;
                vertices[output + 1] = position.Y;
                vertices[output + 2] = position.Z;
                vertices[output + 3] = normal.X;
                vertices[output + 4] = normal.Y;
                vertices[output + 5] = normal.Z;
                vertices[output + 6] = input[i + 6];
                vertices[output + 7] = input[i + 7];
            }

            foreach (var index in source.Mesh.Indices.Span)
            {
                indices[indexOffset++] = checked(index + (uint)(vertexOffset / 8));
            }

            vertexOffset += input.Length;
        }

        return sources[0].Mesh.CreateCombined(vertices, indices);
    }

    internal void Clear()
    {
        var meshes = _batches.Values.Select(batch => batch.Mesh).ToArray();
        
        _batches.Clear();
        
        Cleanup.Run(meshes.Select<Mesh, Action>(mesh => mesh.Dispose).ToArray());
    }
}