using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ImmersiveFire
{
    /// <summary>
    /// An inventory icon drawn from a model, like the game's own: the model seen from the front, a little from the left
    /// and from above, its own textures, lit from the upper left, on a clear background. Drawn here, not by a camera:
    /// the game's deferred lighting and effects would need a whole scene set up for it. Meshes the game keeps
    /// unreadable are read back from their GPU buffers.
    /// </summary>
    internal static class BinIcon
    {
        private const int Supersample = 2;      // drawn 2 x 2 times as large, then averaged: smooth edges
        private const int TextureSize = 512;    // textures are read at this size

        private sealed class Part
        {
            public Vector3[] Positions;         // in the root's space
            public Vector2[] Uvs;
            public List<(int[] triangles, Material material)> Subs = new();
        }

        private sealed class Tex
        {
            public Color32[] Pixels;
            public int Size;
            public Color Tint = Color.white;
        }

        /// <summary>
        /// The icons of the model under <paramref name="root"/> (closed, as it stands): the coloured one (inventory, the
        /// crafting preview) and a white one like the game's recipe list icons; null where it can't be drawn.
        /// </summary>
        public static (Texture2D colored, Texture2D white) Draw(Transform root, Vector3 front, int coloredSize, int whiteSize)
        {
            var parts = new List<Part>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf != null ? mf.GetComponent<MeshRenderer>() : null;
                if (mf == null || mr == null || mf.sharedMesh == null) continue;
                var part = Read(mf.sharedMesh, mr, mf.transform, root);
                if (part != null) parts.Add(part);
            }
            if (parts.Count == 0) return (null, null);
            var textures = new Dictionary<IntPtr, Tex>();
            // Smaller in its square than it could be: the game's icons leave room around them (and the crafting
            // preview has its description right above).
            return (Render(parts, textures, front, coloredSize, 0.74f, false), Render(parts, textures, front, whiteSize, 0.86f, true));
        }

        private static Texture2D Render(List<Part> parts, Dictionary<IntPtr, Tex> textures, Vector3 front, int size, float fit, bool white)
        {
            // The view: from the front, a little from the left (the viewer's) and from above.
            var up = Vector3.up;
            var left = Vector3.Cross(up, front).normalized;   // the viewer's left, looking at the front (Unity is left-handed)
            var toCamera = (front + left * 0.75f + up * 0.85f).normalized;
            var forward = -toCamera;
            var right = Vector3.Cross(up, forward).normalized;
            var camUp = Vector3.Cross(forward, right);
            // The light: from the upper left, a little from the front; a soft fill from the right.
            var key = (up * 1.1f + left * 0.8f + front * 0.6f).normalized;
            var fill = (-left + front * 0.3f).normalized;

            // Fit the model in the square, with a margin.
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in parts)
                foreach (var v in p.Positions)
                {
                    float x = Vector3.Dot(v, right), y = Vector3.Dot(v, camUp);
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
            int big = size * Supersample;
            float scale = big * fit / Math.Max(maxX - minX, maxY - minY);
            float cx = (minX + maxX) / 2f, cy = (minY + maxY) / 2f;

            var color = new Color[big * big];
            var depth = new float[big * big];
            for (int i = 0; i < depth.Length; i++) depth[i] = float.MaxValue;

            foreach (var part in parts)
                foreach (var (tris, material) in part.Subs)
                {
                    var tex = TextureOf(material, textures);
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                    {
                        int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                        if (i0 >= part.Positions.Length || i1 >= part.Positions.Length || i2 >= part.Positions.Length) continue;
                        var a = part.Positions[i0];
                        var b = part.Positions[i1];
                        var c = part.Positions[i2];
                        var n = Vector3.Cross(b - a, c - a);
                        if (n.sqrMagnitude < 1e-12f) continue;
                        n.Normalize();
                        if (Vector3.Dot(n, toCamera) < 0f) n = -n;   // either side may face us
                        float light = 0.5f + 0.7f * Math.Max(0f, Vector3.Dot(n, key)) + 0.2f * Math.Max(0f, Vector3.Dot(n, fill));
                        Fill(color, depth, big, tex, light, white,
                            Screen(a, right, camUp, forward, cx, cy, scale, big),
                            Screen(b, right, camUp, forward, cx, cy, scale, big),
                            Screen(c, right, camUp, forward, cx, cy, scale, big),
                            UvOf(part, i0), UvOf(part, i1), UvOf(part, i2));
                    }
                }

            // Averaged down (colour weighted by coverage); the coloured one brighter and a little warm like the game's icons.
            var pixels = new Color32[size * size];
            var warm = white ? Color.white : new Color(1.24f, 1.13f, 1.0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = 0f, g = 0f, bl = 0f, cover = 0f;
                    for (int sy = 0; sy < Supersample; sy++)
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            int i = (y * Supersample + sy) * big + x * Supersample + sx;
                            if (depth[i] == float.MaxValue) continue;
                            var c = color[i];
                            r += c.r; g += c.g; bl += c.b;
                            cover += 1f;
                        }
                    if (cover <= 0f) continue;
                    float alpha = cover / (Supersample * Supersample);
                    pixels[y * size + x] = new Color32(
                        To8(r / cover * warm.r), To8(g / cover * warm.g), To8(bl / cover * warm.b), To8(alpha));
                }

            var icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
            icon.SetPixels32(new Il2CppStructArray<Color32>(pixels));
            icon.Apply();
            icon.wrapMode = TextureWrapMode.Clamp;
            icon.name = white ? "ico_CraftItem__FirewoodBin_IMFI" : "ico_FirewoodBin_IMFI";
            icon.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return icon;
        }

        private static byte To8(float v) => (byte)Math.Max(0, Math.Min(255, (int)(v * 255f + 0.5f)));

        private static Vector2 UvOf(Part p, int i) => p.Uvs != null && i < p.Uvs.Length ? p.Uvs[i] : Vector2.zero;

        /// <summary>Pixel x, y (y up) and depth of a point.</summary>
        private static Vector3 Screen(Vector3 v, Vector3 right, Vector3 up, Vector3 forward, float cx, float cy, float scale, int big) =>
            new((Vector3.Dot(v, right) - cx) * scale + big / 2f, (Vector3.Dot(v, up) - cy) * scale + big / 2f, Vector3.Dot(v, forward));

        /// <summary>One triangle: every pixel whose centre it covers, nearer than what is there, gets its texture, lit.</summary>
        private static void Fill(Color[] color, float[] depth, int big, Tex tex, float light, bool white,
            Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (Math.Abs(area) < 1e-6f) return;
            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(a.x, Math.Min(b.x, c.x))));
            int x1 = Math.Min(big - 1, (int)Math.Ceiling(Math.Max(a.x, Math.Max(b.x, c.x))));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(a.y, Math.Min(b.y, c.y))));
            int y1 = Math.Min(big - 1, (int)Math.Ceiling(Math.Max(a.y, Math.Max(b.y, c.y))));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((b.x - px) * (c.y - py) - (b.y - py) * (c.x - px)) / area;
                    float w1 = ((c.x - px) * (a.y - py) - (c.y - py) * (a.x - px)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 < 0f || w1 < 0f || w2 < 0f) continue;
                    float z = w0 * a.z + w1 * b.z + w2 * c.z;
                    int i = y * big + x;
                    if (z >= depth[i]) continue;
                    depth[i] = z;
                    var uv = ua * w0 + ub * w1 + uc * w2;
                    var albedo = Sample(tex, uv);
                    if (white)
                    {
                        // White, shaded by the light, the planks showing faintly.
                        float lum = 0.3f * albedo.r + 0.59f * albedo.g + 0.11f * albedo.b;
                        float v = (0.6f + 0.4f * Mathf.Clamp01((light - 0.5f) / 0.8f)) * (0.86f + 0.3f * lum);
                        color[i] = new Color(v, v, v);
                    }
                    else color[i] = albedo * light;
                }
        }

        private static Color Sample(Tex tex, Vector2 uv)
        {
            if (tex == null || tex.Pixels == null) return tex != null ? tex.Tint : new Color(0.45f, 0.36f, 0.27f);
            int s = tex.Size;
            float u = (uv.x - (float)Math.Floor(uv.x)) * s - 0.5f;
            float v = (uv.y - (float)Math.Floor(uv.y)) * s - 0.5f;
            int x0 = (int)Math.Floor(u), y0 = (int)Math.Floor(v);
            float fx = u - x0, fy = v - y0;
            Color P(int x, int y)
            {
                x = ((x % s) + s) % s;
                y = ((y % s) + s) % s;
                return tex.Pixels[y * s + x];
            }
            var c = Color.Lerp(Color.Lerp(P(x0, y0), P(x0 + 1, y0), fx), Color.Lerp(P(x0, y0 + 1), P(x0 + 1, y0 + 1), fx), fy);
            return c * tex.Tint;
        }

        /// <summary>A material's main texture, read back at TextureSize (once per material).</summary>
        private static Tex TextureOf(Material material, Dictionary<IntPtr, Tex> cache)
        {
            if (material == null) return null;
            if (cache.TryGetValue(material.Pointer, out var known)) return known;
            var tex = new Tex();
            cache[material.Pointer] = tex;
            if (material.HasProperty("_Color")) tex.Tint = material.GetColor("_Color");
            Texture src = material.mainTexture;
            if (src == null)
                foreach (var name in material.GetTexturePropertyNames())
                    if ((src = material.GetTexture(name)) != null) break;
            if (src == null) return tex;
            var rt = RenderTexture.GetTemporary(TextureSize, TextureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var active = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, TextureSize, TextureSize), 0, 0);
            copy.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            var read = copy.GetPixels32();
            tex.Pixels = new Color32[read.Length];
            for (int i = 0; i < read.Length; i++) tex.Pixels[i] = read[i];
            tex.Size = TextureSize;
            Object.Destroy(copy);
            Main.Log.Msg($"[Bin] Icon texture '{src.name}' of '{material.name}' ({material.shader?.name}), tint {tex.Tint}");
            return tex;
        }

        // --- the model ---

        private static Part Read(Mesh mesh, MeshRenderer renderer, Transform t, Transform root)
        {
            try
            {
                var part = mesh.isReadable ? ReadCpu(mesh) : ReadGpu(mesh);
                if (part == null) return null;
                for (int i = 0; i < part.Positions.Length; i++)
                    part.Positions[i] = root.InverseTransformPoint(t.TransformPoint(part.Positions[i]));
                var materials = renderer.sharedMaterials;
                for (int s = 0; s < part.Subs.Count; s++)
                {
                    var m = materials != null && materials.Length > 0 ? materials[Math.Min(s, materials.Length - 1)] : null;
                    part.Subs[s] = (part.Subs[s].triangles, m);
                }
                Main.Log.Msg($"[Bin] Icon model '{mesh.name}': {(mesh.isReadable ? "readable" : "from the GPU")}, " +
                             $"{part.Positions.Length} vertices, {part.Subs.Count} part(s)");
                return part;
            }
            catch (Exception e)
            {
                Main.Log.Warning($"[Bin] Icon model '{mesh.name}': {e.Message}");
                return null;
            }
        }

        private static Part ReadCpu(Mesh mesh)
        {
            var part = new Part();
            var v = mesh.vertices;
            part.Positions = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) part.Positions[i] = v[i];
            var uv = mesh.uv;
            if (uv != null && uv.Length == v.Length)
            {
                part.Uvs = new Vector2[uv.Length];
                for (int i = 0; i < uv.Length; i++) part.Uvs[i] = uv[i];
            }
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                var tris = mesh.GetTriangles(s);
                var copy = new int[tris.Length];
                for (int i = 0; i < tris.Length; i++) copy[i] = tris[i];
                part.Subs.Add((copy, null));
            }
            return part;
        }

        /// <summary>A mesh the game keeps unreadable: its vertex and index buffers read back from the GPU.</summary>
        private static Part ReadGpu(Mesh mesh)
        {
            int count = mesh.vertexCount;
            if (count <= 0 || !mesh.HasVertexAttribute(VertexAttribute.Position)) return null;
            var streams = new Dictionary<int, byte[]>();
            byte[] Stream(int s)
            {
                if (streams.TryGetValue(s, out var known)) return known;
                var buffer = mesh.GetVertexBuffer(s);
                try
                {
                    var data = new Il2CppStructArray<byte>(buffer.count * buffer.stride);
                    buffer.GetData(new Il2CppSystem.Array(data.Pointer));
                    var bytes = new byte[data.Length];
                    for (int i = 0; i < bytes.Length; i++) bytes[i] = data[i];
                    return streams[s] = bytes;
                }
                finally
                {
                    buffer.Release();
                }
            }

            var part = new Part { Positions = new Vector3[count] };
            ReadAttribute(mesh, VertexAttribute.Position, count, Stream, (i, f) => part.Positions[i] = new Vector3(f[0], f[1], f[2]));
            if (mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                part.Uvs = new Vector2[count];
                ReadAttribute(mesh, VertexAttribute.TexCoord0, count, Stream, (i, f) => part.Uvs[i] = new Vector2(f[0], f[1]));
            }

            var indexBuffer = mesh.GetIndexBuffer();
            byte[] indices;
            int indexSize;
            try
            {
                indexSize = indexBuffer.stride;
                var data = new Il2CppStructArray<byte>(indexBuffer.count * indexBuffer.stride);
                indexBuffer.GetData(new Il2CppSystem.Array(data.Pointer));
                indices = new byte[data.Length];
                for (int i = 0; i < indices.Length; i++) indices[i] = data[i];
            }
            finally
            {
                indexBuffer.Release();
            }
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                int start = (int)mesh.GetIndexStart(s), n = (int)mesh.GetIndexCount(s), baseVertex = (int)mesh.GetBaseVertex(s);
                var tris = new int[n];
                for (int i = 0; i < n; i++)
                {
                    int at = (start + i) * indexSize;
                    tris[i] = baseVertex + (indexSize == 2 ? BitConverter.ToUInt16(indices, at) : (int)BitConverter.ToUInt32(indices, at));
                }
                part.Subs.Add((tris, null));
            }
            return part;
        }

        private static void ReadAttribute(Mesh mesh, VertexAttribute attribute, int count, Func<int, byte[]> stream, Action<int, float[]> set)
        {
            int s = mesh.GetVertexAttributeStream(attribute);
            int offset = mesh.GetVertexAttributeOffset(attribute);
            int stride = mesh.GetVertexBufferStride(s);
            int dim = mesh.GetVertexAttributeDimension(attribute);
            var format = mesh.GetVertexAttributeFormat(attribute);
            var bytes = stream(s);
            var f = new float[Math.Max(dim, 3)];
            for (int i = 0; i < count; i++)
            {
                int at = i * stride + offset;
                for (int k = 0; k < dim; k++) f[k] = Value(bytes, ref at, format);
                set(i, f);
            }
        }

        private static float Value(byte[] b, ref int at, VertexAttributeFormat format)
        {
            float v;
            switch (format)
            {
                case VertexAttributeFormat.Float32: v = BitConverter.ToSingle(b, at); at += 4; break;
                case VertexAttributeFormat.Float16: v = Mathf.HalfToFloat(BitConverter.ToUInt16(b, at)); at += 2; break;
                case VertexAttributeFormat.UNorm16: v = BitConverter.ToUInt16(b, at) / 65535f; at += 2; break;
                case VertexAttributeFormat.SNorm16: v = Math.Max(-1f, BitConverter.ToInt16(b, at) / 32767f); at += 2; break;
                case VertexAttributeFormat.UNorm8: v = b[at] / 255f; at += 1; break;
                case VertexAttributeFormat.SNorm8: v = Math.Max(-1f, (sbyte)b[at] / 127f); at += 1; break;
                default: throw new NotSupportedException("vertex format " + format);
            }
            return v;
        }
    }
}
