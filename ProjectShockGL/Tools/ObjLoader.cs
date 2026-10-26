using System.Globalization;
using System.Numerics;
using ProjectShockGL.Rendering;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Tools;

public static class ObjLoader
{
    public static Mesh Load(GL gl, string path)
    {
        var positions = new List<Vector3>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        
        var vertices = new List<float>();
        var indices = new List<uint>();
        var vertexCache = new Dictionary<(int v, int vt, int vn), uint>();

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var command = parts[0];

            switch (command)
            {
                case "v":
                    positions.Add(new Vector3(
                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                        float.Parse(parts[3], CultureInfo.InvariantCulture)
                    ));
                    break;

                case "vt":
                    uvs.Add(new Vector2(
                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture)
                    ));
                    break;

                case "vn":
                    normals.Add(new Vector3(
                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                        float.Parse(parts[3], CultureInfo.InvariantCulture)
                    ));
                    break;

                case "f":
                    ParseFace(parts, positions, uvs, normals, vertices, indices, vertexCache);
                    break;
            }
        }

        return new Mesh(gl, vertices.ToArray(), indices.ToArray());
    }

    private static void ParseFace(string[] parts, List<Vector3> positions, List<Vector2> uvs, List<Vector3> normals, List<float> vertices,
        List<uint> indices, Dictionary<(int v, int vt, int vn), uint> vertexCache)
    {
        var faceVertexIndices = new List<uint>();

        for (var i = 1; i < parts.Length; i++)
        {
            var vertexParts = parts[i].Split('/');

            var vIndex = ParseIndex(vertexParts[0], positions.Count);
            var vtIndex = vertexParts.Length > 1 && !string.IsNullOrEmpty(vertexParts[1])
                ? ParseIndex(vertexParts[1], uvs.Count)
                : -1;
            
            var vnIndex = vertexParts.Length > 2 && !string.IsNullOrEmpty(vertexParts[2])
                ? ParseIndex(vertexParts[2], normals.Count)
                : -1;

            var key = (vIndex, vtIndex, vnIndex);

            if (!vertexCache.TryGetValue(key, out var meshIndex))
            {
                meshIndex = (uint)(vertices.Count / 8);
                vertexCache[key] = meshIndex;

                var position = positions[vIndex];
                var uv = vtIndex >= 0 ? uvs[vtIndex] : Vector2.Zero;
                var normal = vnIndex >= 0 ? normals[vnIndex] : Vector3.UnitY;

                vertices.Add(position.X);
                vertices.Add(position.Y);
                vertices.Add(position.Z);
                
                vertices.Add(normal.X);
                vertices.Add(normal.Y);
                vertices.Add(normal.Z);
                
                vertices.Add(uv.X);
                vertices.Add(uv.Y);
            }

            faceVertexIndices.Add(meshIndex);
        }
        
        for (var i = 1; i < faceVertexIndices.Count - 1; i++)
        {
            indices.Add(faceVertexIndices[0]);
            indices.Add(faceVertexIndices[i]);
            indices.Add(faceVertexIndices[i + 1]);
        }
    }

    private static int ParseIndex(string token, int count)
    {
        var index = int.Parse(token, CultureInfo.InvariantCulture);
        return index < 0 ? count + index : index - 1;
    }
}