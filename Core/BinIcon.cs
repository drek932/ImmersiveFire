using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace ImmersiveFire;

internal static class BinIcon
{
	private sealed class Part
	{
		public Vector3[] Positions;

		public Vector2[] Uvs;

		public List<(int[] triangles, Material material)> Subs = new List<(int[], Material)>();
	}

	private sealed class Tex
	{
		public Color32[] Pixels;

		public int Size;

		public Color Tint = Color.white;
	}

	private const int Supersample = 2;

	private const int TextureSize = 512;

	public static (Texture2D colored, Texture2D white) Draw(Transform root, Vector3 front, int coloredSize, int whiteSize)
	{
		List<Part> list = new List<Part>();
		foreach (MeshFilter componentsInChild in root.GetComponentsInChildren<MeshFilter>(includeInactive: true))
		{
			MeshRenderer meshRenderer = ((componentsInChild != null) ? componentsInChild.GetComponent<MeshRenderer>() : null);
			if (!(componentsInChild == null) && !(meshRenderer == null) && !(componentsInChild.sharedMesh == null))
			{
				Part part = Read(componentsInChild.sharedMesh, meshRenderer, componentsInChild.transform, root);
				if (part != null)
				{
					list.Add(part);
				}
			}
		}
		if (list.Count == 0)
		{
			return (colored: null, white: null);
		}
		Dictionary<System.IntPtr, Tex> textures = new Dictionary<System.IntPtr, Tex>();
		return (colored: Render(list, textures, front, coloredSize, 0.74f, white: false), white: Render(list, textures, front, whiteSize, 0.86f, white: true));
	}

	private static Texture2D Render(List<Part> parts, Dictionary<System.IntPtr, Tex> textures, Vector3 front, int size, float fit, bool white)
	{
		Vector3 up = Vector3.up;
		Vector3 normalized = Vector3.Cross(up, front).normalized;
		Vector3 normalized2 = (front + normalized * 0.75f + up * 0.85f).normalized;
		Vector3 vector = -normalized2;
		Vector3 normalized3 = Vector3.Cross(up, vector).normalized;
		Vector3 vector2 = Vector3.Cross(vector, normalized3);
		Vector3 normalized4 = (up * 1.1f + normalized * 0.8f + front * 0.6f).normalized;
		Vector3 normalized5 = (-normalized + front * 0.3f).normalized;
		float num = float.MaxValue;
		float num2 = float.MinValue;
		float num3 = float.MaxValue;
		float num4 = float.MinValue;
		foreach (Part part in parts)
		{
			Vector3[] positions = part.Positions;
			foreach (Vector3 lhs in positions)
			{
				float val = Vector3.Dot(lhs, normalized3);
				float val2 = Vector3.Dot(lhs, vector2);
				num = System.Math.Min(num, val);
				num2 = System.Math.Max(num2, val);
				num3 = System.Math.Min(num3, val2);
				num4 = System.Math.Max(num4, val2);
			}
		}
		int num5 = size * 2;
		float scale = (float)num5 * fit / System.Math.Max(num2 - num, num4 - num3);
		float cx = (num + num2) / 2f;
		float cy = (num3 + num4) / 2f;
		Color[] array = new Color[num5 * num5];
		float[] array2 = new float[num5 * num5];
		for (int j = 0; j < array2.Length; j++)
		{
			array2[j] = float.MaxValue;
		}
		foreach (Part part2 in parts)
		{
			foreach (var sub in part2.Subs)
			{
				int[] item = sub.triangles;
				Tex tex = TextureOf(sub.material, textures);
				for (int k = 0; k + 2 < item.Length; k += 3)
				{
					int num6 = item[k];
					int num7 = item[k + 1];
					int num8 = item[k + 2];
					if (num6 >= part2.Positions.Length || num7 >= part2.Positions.Length || num8 >= part2.Positions.Length)
					{
						continue;
					}
					Vector3 vector3 = part2.Positions[num6];
					Vector3 vector4 = part2.Positions[num7];
					Vector3 vector5 = part2.Positions[num8];
					Vector3 vector6 = Vector3.Cross(vector4 - vector3, vector5 - vector3);
					if (!(vector6.sqrMagnitude < 1E-12f))
					{
						vector6.Normalize();
						if (Vector3.Dot(vector6, normalized2) < 0f)
						{
							vector6 = -vector6;
						}
						float light = 0.5f + 0.7f * System.Math.Max(0f, Vector3.Dot(vector6, normalized4)) + 0.2f * System.Math.Max(0f, Vector3.Dot(vector6, normalized5));
						Fill(array, array2, num5, tex, light, white, Screen(vector3, normalized3, vector2, vector, cx, cy, scale, num5), Screen(vector4, normalized3, vector2, vector, cx, cy, scale, num5), Screen(vector5, normalized3, vector2, vector, cx, cy, scale, num5), UvOf(part2, num6), UvOf(part2, num7), UvOf(part2, num8));
					}
				}
			}
		}
		Color32[] array3 = new Color32[size * size];
		Color color = (white ? Color.white : new Color(1.24f, 1.13f, 1f));
		for (int l = 0; l < size; l++)
		{
			for (int m = 0; m < size; m++)
			{
				float num9 = 0f;
				float num10 = 0f;
				float num11 = 0f;
				float num12 = 0f;
				for (int n = 0; n < 2; n++)
				{
					for (int num13 = 0; num13 < 2; num13++)
					{
						int num14 = (l * 2 + n) * num5 + m * 2 + num13;
						if (array2[num14] != float.MaxValue)
						{
							Color color2 = array[num14];
							num9 += color2.r;
							num10 += color2.g;
							num11 += color2.b;
							num12 += 1f;
						}
					}
				}
				if (!(num12 <= 0f))
				{
					float v = num12 / 4f;
					array3[l * size + m] = new Color32(To8(num9 / num12 * color.r), To8(num10 / num12 * color.g), To8(num11 / num12 * color.b), To8(v));
				}
			}
		}
		Texture2D texture2D = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		texture2D.SetPixels32(new Il2CppStructArray<Color32>(array3));
		texture2D.Apply();
		texture2D.wrapMode = TextureWrapMode.Clamp;
		texture2D.name = (white ? "ico_CraftItem__FirewoodBin_IMFI" : "ico_FirewoodBin_IMFI");
		texture2D.hideFlags = HideFlags.DontUnloadUnusedAsset;
		return texture2D;
	}

	private static byte To8(float v)
	{
		return (byte)System.Math.Max(0, System.Math.Min(255, (int)(v * 255f + 0.5f)));
	}

	private static Vector2 UvOf(Part p, int i)
	{
		if (p.Uvs == null || i >= p.Uvs.Length)
		{
			return Vector2.zero;
		}
		return p.Uvs[i];
	}

	private static Vector3 Screen(Vector3 v, Vector3 right, Vector3 up, Vector3 forward, float cx, float cy, float scale, int big)
	{
		return new Vector3((Vector3.Dot(v, right) - cx) * scale + (float)big / 2f, (Vector3.Dot(v, up) - cy) * scale + (float)big / 2f, Vector3.Dot(v, forward));
	}

	private static void Fill(Color[] color, float[] depth, int big, Tex tex, float light, bool white, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
	{
		float num = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
		if (System.Math.Abs(num) < 1E-06f)
		{
			return;
		}
		int num2 = System.Math.Max(0, (int)System.Math.Floor(System.Math.Min(a.x, System.Math.Min(b.x, c.x))));
		int num3 = System.Math.Min(big - 1, (int)System.Math.Ceiling(System.Math.Max(a.x, System.Math.Max(b.x, c.x))));
		int num4 = System.Math.Max(0, (int)System.Math.Floor(System.Math.Min(a.y, System.Math.Min(b.y, c.y))));
		int num5 = System.Math.Min(big - 1, (int)System.Math.Ceiling(System.Math.Max(a.y, System.Math.Max(b.y, c.y))));
		for (int i = num4; i <= num5; i++)
		{
			for (int j = num2; j <= num3; j++)
			{
				float num6 = (float)j + 0.5f;
				float num7 = (float)i + 0.5f;
				float num8 = ((b.x - num6) * (c.y - num7) - (b.y - num7) * (c.x - num6)) / num;
				float num9 = ((c.x - num6) * (a.y - num7) - (c.y - num7) * (a.x - num6)) / num;
				float num10 = 1f - num8 - num9;
				if (num8 < 0f || num9 < 0f || num10 < 0f)
				{
					continue;
				}
				float num11 = num8 * a.z + num9 * b.z + num10 * c.z;
				int num12 = i * big + j;
				if (!(num11 >= depth[num12]))
				{
					depth[num12] = num11;
					Vector2 uv = ua * num8 + ub * num9 + uc * num10;
					Color color2 = Sample(tex, uv);
					if (white)
					{
						float num13 = 0.3f * color2.r + 0.59f * color2.g + 0.11f * color2.b;
						float num14 = (0.6f + 0.4f * Mathf.Clamp01((light - 0.5f) / 0.8f)) * (0.86f + 0.3f * num13);
						color[num12] = new Color(num14, num14, num14);
					}
					else
					{
						color[num12] = color2 * light;
					}
				}
			}
		}
	}

	private static Color Sample(Tex tex, Vector2 uv)
	{
		if (tex == null || tex.Pixels == null)
		{
			if (tex == null)
			{
				return new Color(0.45f, 0.36f, 0.27f);
			}
			return tex.Tint;
		}
		int s = tex.Size;
		float num = (uv.x - (float)System.Math.Floor(uv.x)) * (float)s - 0.5f;
		float num2 = (uv.y - (float)System.Math.Floor(uv.y)) * (float)s - 0.5f;
		int num3 = (int)System.Math.Floor(num);
		int num4 = (int)System.Math.Floor(num2);
		float t = num - (float)num3;
		float t2 = num2 - (float)num4;
		return Color.Lerp(Color.Lerp(P(num3, num4), P(num3 + 1, num4), t), Color.Lerp(P(num3, num4 + 1), P(num3 + 1, num4 + 1), t), t2) * tex.Tint;
		Color P(int x, int y)
		{
			x = (x % s + s) % s;
			y = (y % s + s) % s;
			return tex.Pixels[y * s + x];
		}
	}

	private static Tex TextureOf(Material material, Dictionary<System.IntPtr, Tex> cache)
	{
		if (material == null)
		{
			return null;
		}
		if (cache.TryGetValue(material.Pointer, out var value))
		{
			return value;
		}
		Tex tex = new Tex();
		cache[material.Pointer] = tex;
		if (material.HasProperty("_Color"))
		{
			tex.Tint = material.GetColor("_Color");
		}
		Texture texture = material.mainTexture;
		if (texture == null)
		{
			foreach (string texturePropertyName in material.GetTexturePropertyNames())
			{
				if ((texture = material.GetTexture(texturePropertyName)) != null)
				{
					break;
				}
			}
		}
		if (texture == null)
		{
			return tex;
		}
		RenderTexture temporary = RenderTexture.GetTemporary(512, 512, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
		RenderTexture active = RenderTexture.active;
		Graphics.Blit(texture, temporary);
		RenderTexture.active = temporary;
		Texture2D texture2D = new Texture2D(512, 512, TextureFormat.RGBA32, mipChain: false);
		texture2D.ReadPixels(new Rect(0f, 0f, 512f, 512f), 0, 0);
		texture2D.Apply();
		RenderTexture.active = active;
		RenderTexture.ReleaseTemporary(temporary);
		Il2CppStructArray<Color32> pixels = texture2D.GetPixels32();
		tex.Pixels = new Color32[pixels.Length];
		for (int i = 0; i < pixels.Length; i++)
		{
			tex.Pixels[i] = pixels[i];
		}
		tex.Size = 512;
		UnityEngine.Object.Destroy(texture2D);
		Main.Log.Msg($"[Bin] Icon texture '{texture.name}' of '{material.name}' ({material.shader?.name}), tint {tex.Tint}");
		return tex;
	}

	private static Part Read(Mesh mesh, MeshRenderer renderer, Transform t, Transform root)
	{
		try
		{
			Part part = (mesh.isReadable ? ReadCpu(mesh) : ReadGpu(mesh));
			if (part == null)
			{
				return null;
			}
			for (int i = 0; i < part.Positions.Length; i++)
			{
				part.Positions[i] = root.InverseTransformPoint(t.TransformPoint(part.Positions[i]));
			}
			Il2CppReferenceArray<Material> sharedMaterials = renderer.sharedMaterials;
			for (int j = 0; j < part.Subs.Count; j++)
			{
				Material item = ((sharedMaterials != null && sharedMaterials.Length > 0) ? sharedMaterials[System.Math.Min(j, sharedMaterials.Length - 1)] : null);
				part.Subs[j] = (part.Subs[j].triangles, item);
			}
			Main.Log.Msg($"[Bin] Icon model '{mesh.name}': {(mesh.isReadable ? "readable" : "from the GPU")}, {part.Positions.Length} vertices, {part.Subs.Count} part(s)");
			return part;
		}
		catch (System.Exception ex)
		{
			Main.Log.Warning("[Bin] Icon model '" + mesh.name + "': " + ex.Message);
			return null;
		}
	}

	private static Part ReadCpu(Mesh mesh)
	{
		Part part = new Part();
		Il2CppStructArray<Vector3> vertices = mesh.vertices;
		part.Positions = new Vector3[vertices.Length];
		for (int i = 0; i < vertices.Length; i++)
		{
			part.Positions[i] = vertices[i];
		}
		Il2CppStructArray<Vector2> uv = mesh.uv;
		if (uv != null && uv.Length == vertices.Length)
		{
			part.Uvs = new Vector2[uv.Length];
			for (int j = 0; j < uv.Length; j++)
			{
				part.Uvs[j] = uv[j];
			}
		}
		for (int k = 0; k < mesh.subMeshCount; k++)
		{
			if (mesh.GetTopology(k) == MeshTopology.Triangles)
			{
				Il2CppStructArray<int> triangles = mesh.GetTriangles(k);
				int[] array = new int[triangles.Length];
				for (int l = 0; l < triangles.Length; l++)
				{
					array[l] = triangles[l];
				}
				part.Subs.Add((array, null));
			}
		}
		return part;
	}

	private static Part ReadGpu(Mesh mesh)
	{
		int vertexCount = mesh.vertexCount;
		if (vertexCount <= 0 || !mesh.HasVertexAttribute(VertexAttribute.Position))
		{
			return null;
		}
		Dictionary<int, byte[]> streams = new Dictionary<int, byte[]>();
		Part part = new Part
		{
			Positions = new Vector3[vertexCount]
		};
		ReadAttribute(mesh, VertexAttribute.Position, vertexCount, Stream, delegate(int i, float[] f)
		{
			part.Positions[i] = new Vector3(f[0], f[1], f[2]);
		});
		if (mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
		{
			part.Uvs = new Vector2[vertexCount];
			ReadAttribute(mesh, VertexAttribute.TexCoord0, vertexCount, Stream, delegate(int i, float[] f)
			{
				part.Uvs[i] = new Vector2(f[0], f[1]);
			});
		}
		GraphicsBuffer indexBuffer = mesh.GetIndexBuffer();
		int stride;
		byte[] array;
		try
		{
			stride = indexBuffer.stride;
			Il2CppStructArray<byte> il2CppStructArray = new Il2CppStructArray<byte>(indexBuffer.count * indexBuffer.stride);
			indexBuffer.GetData(new Il2CppSystem.Array(il2CppStructArray.Pointer));
			array = new byte[il2CppStructArray.Length];
			for (int j = 0; j < array.Length; j++)
			{
				array[j] = il2CppStructArray[j];
			}
		}
		finally
		{
			indexBuffer.Release();
		}
		for (int k = 0; k < mesh.subMeshCount; k++)
		{
			if (mesh.GetTopology(k) == MeshTopology.Triangles)
			{
				int indexStart = (int)mesh.GetIndexStart(k);
				int indexCount = (int)mesh.GetIndexCount(k);
				int baseVertex = (int)mesh.GetBaseVertex(k);
				int[] array2 = new int[indexCount];
				for (int l = 0; l < indexCount; l++)
				{
					int startIndex = (indexStart + l) * stride;
					array2[l] = baseVertex + (int)((stride == 2) ? System.BitConverter.ToUInt16(array, startIndex) : System.BitConverter.ToUInt32(array, startIndex));
				}
				part.Subs.Add((array2, null));
			}
		}
		return part;
		byte[] Stream(int s)
		{
			if (streams.TryGetValue(s, out var value))
			{
				return value;
			}
			GraphicsBuffer vertexBuffer = mesh.GetVertexBuffer(s);
			try
			{
				Il2CppStructArray<byte> il2CppStructArray2 = new Il2CppStructArray<byte>(vertexBuffer.count * vertexBuffer.stride);
				vertexBuffer.GetData(new Il2CppSystem.Array(il2CppStructArray2.Pointer));
				byte[] array3 = new byte[il2CppStructArray2.Length];
				for (int m = 0; m < array3.Length; m++)
				{
					array3[m] = il2CppStructArray2[m];
				}
				return streams[s] = array3;
			}
			finally
			{
				vertexBuffer.Release();
			}
		}
	}

	private static void ReadAttribute(Mesh mesh, VertexAttribute attribute, int count, System.Func<int, byte[]> stream, System.Action<int, float[]> set)
	{
		int vertexAttributeStream = mesh.GetVertexAttributeStream(attribute);
		int vertexAttributeOffset = mesh.GetVertexAttributeOffset(attribute);
		int vertexBufferStride = mesh.GetVertexBufferStride(vertexAttributeStream);
		int vertexAttributeDimension = mesh.GetVertexAttributeDimension(attribute);
		VertexAttributeFormat vertexAttributeFormat = mesh.GetVertexAttributeFormat(attribute);
		byte[] b = stream(vertexAttributeStream);
		float[] array = new float[System.Math.Max(vertexAttributeDimension, 3)];
		for (int i = 0; i < count; i++)
		{
			int at = i * vertexBufferStride + vertexAttributeOffset;
			for (int j = 0; j < vertexAttributeDimension; j++)
			{
				array[j] = Value(b, ref at, vertexAttributeFormat);
			}
			set(i, array);
		}
	}

	private static float Value(byte[] b, ref int at, VertexAttributeFormat format)
	{
		float result;
		switch (format)
		{
		case VertexAttributeFormat.Float32:
			result = System.BitConverter.ToSingle(b, at);
			at += 4;
			break;
		case VertexAttributeFormat.Float16:
			result = Mathf.HalfToFloat(System.BitConverter.ToUInt16(b, at));
			at += 2;
			break;
		case VertexAttributeFormat.UNorm16:
			result = (float)(int)System.BitConverter.ToUInt16(b, at) / 65535f;
			at += 2;
			break;
		case VertexAttributeFormat.SNorm16:
			result = System.Math.Max(-1f, (float)System.BitConverter.ToInt16(b, at) / 32767f);
			at += 2;
			break;
		case VertexAttributeFormat.UNorm8:
			result = (float)(int)b[at] / 255f;
			at++;
			break;
		case VertexAttributeFormat.SNorm8:
			result = System.Math.Max(-1f, (float)(sbyte)b[at] / 127f);
			at++;
			break;
		default:
			throw new System.NotSupportedException("vertex format " + format);
		}
		return result;
	}
}
