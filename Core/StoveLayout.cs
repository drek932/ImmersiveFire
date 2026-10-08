using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace ImmersiveFire;

internal sealed class StoveLayout
{
	public Transform Frame;

	public Vector3 Door;

	public float DoorHalfWidth;

	public float DoorHalfHeight;

	public float MinX;

	public float MaxX;

	public float FrontZ;

	public float CenterZ;

	public bool HasDoor;

	public bool Open;

	public bool Furnace;

	public float Scale = 1f;

	public float ExtraBack;

	public float Lowered;

	public string How;

	private static readonly Dictionary<IntPtr, StoveLayout> cache = new Dictionary<IntPtr, StoveLayout>();

	private float ZoneScale => Main.Settings.StoveZoneScale * Scale;

	public static void Reset()
	{
		cache.Clear();
	}

	public static StoveLayout Of(FireplaceInteraction fi)
	{
		if (cache.TryGetValue(fi.Pointer, out var value) && value.Frame != null)
		{
			return value;
		}
		StoveLayout stoveLayout = Measure(fi);
		cache[fi.Pointer] = stoveLayout;
		Main.Log.Msg("Stove layout of '" + fi.gameObject.name + "': " + stoveLayout.How);
		return stoveLayout;
	}

	private static StoveLayout Measure(FireplaceInteraction fi)
	{
		Transform transform = fi.transform;
		Transform transform2 = ModelRoot(transform);
		WoodStove woodStove = fi.TryCast<WoodStove>();
		ObjectAnim objectAnim = ((woodStove != null) ? woodStove.m_ObjectAnim : null);
		List<MeshFilter> list = new List<MeshFilter>();
		if (objectAnim != null)
		{
			foreach (MeshFilter componentsInChild in ((objectAnim.m_Target != null) ? objectAnim.m_Target.transform : objectAnim.transform).GetComponentsInChildren<MeshFilter>(includeInactive: false))
			{
				if (Usable(componentsInChild))
				{
					list.Add(componentsInChild);
				}
			}
		}
		if (list.Count == 0)
		{
			foreach (MeshFilter componentsInChild2 in transform2.GetComponentsInChildren<MeshFilter>(includeInactive: false))
			{
				if (Usable(componentsInChild2) && componentsInChild2.name.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					list.Add(componentsInChild2);
				}
			}
		}
		bool flag = list.Count > 0 && objectAnim != null;
		Vector3 centre;
		Vector3 vector = ((list.Count > 0) ? Centre(list, transform) : (SpotsCentre(fi, transform, out centre) ? centre : ((!(fi.Fire != null)) ? new Vector3(0f, 0.6f, -0.4f) : transform.InverseTransformPoint(fi.Fire.transform.position))));
		Vector3 vector2 = vector;
		bool flag2 = transform2.GetComponentInChildren<AmmoWorkBench>(includeInactive: true) != null && list.Count > 0;
		if (flag2)
		{
			Vector3 size = list[0].sharedMesh.bounds.size;
			Vector3 direction = ((size.x <= size.y && size.x <= size.z) ? Vector3.right : ((size.y <= size.z) ? Vector3.up : Vector3.forward));
			vector2 = transform.InverseTransformDirection(list[0].transform.TransformDirection(direction));
			vector2.y = 0f;
			if (Vector3.Dot(vector2, vector - transform.InverseTransformPoint(MiddleOf(transform2))) < 0f)
			{
				vector2 = -vector2;
			}
		}
		Vector3 vector3 = ((Math.Abs(vector2.z) >= Math.Abs(vector2.x)) ? new Vector3(0f, 0f, (vector2.z >= 0f) ? 1f : (-1f)) : new Vector3((vector2.x >= 0f) ? 1f : (-1f), 0f, 0f));
		Transform transform3 = new GameObject("ImmersiveFire frame").transform;
		transform3.SetParent(transform, worldPositionStays: false);
		transform3.localPosition = Vector3.zero;
		transform3.localRotation = Quaternion.LookRotation(-vector3, Vector3.up);
		StoveLayout stoveLayout = new StoveLayout
		{
			Frame = transform3,
			HasDoor = flag
		};
		if (list.Count > 0)
		{
			Bounds(list, transform3, out var min, out var max);
			stoveLayout.Door = (min + max) / 2f;
			stoveLayout.DoorHalfWidth = Math.Max(0.12f, (max.x - min.x) / 2f);
			stoveLayout.DoorHalfHeight = Math.Max(0.1f, (max.y - min.y) / 2f);
		}
		else
		{
			stoveLayout.Door = transform3.InverseTransformPoint(transform.TransformPoint(vector));
			stoveLayout.DoorHalfWidth = 0.35f;
			stoveLayout.DoorHalfHeight = 0.25f;
		}
		List<MeshFilter> list2 = new List<MeshFilter>();
		foreach (MeshFilter componentsInChild3 in transform2.GetComponentsInChildren<MeshFilter>(includeInactive: false))
		{
			if (Usable(componentsInChild3) && !list.Contains(componentsInChild3))
			{
				list2.Add(componentsInChild3);
			}
		}
		if (list2.Count > 0 && Bounds(list2, transform3, out var min2, out var max2, 2.5f))
		{
			stoveLayout.MinX = min2.x;
			stoveLayout.MaxX = max2.x;
			stoveLayout.FrontZ = ((list.Count > 0) ? Math.Min(min2.z, stoveLayout.Door.z) : min2.z);
			stoveLayout.CenterZ = (min2.z + max2.z) / 2f;
			if (list.Count == 0)
			{
				stoveLayout.Open = true;
				stoveLayout.Door = new Vector3((min2.x + max2.x) / 2f, Mathf.Clamp(max2.y * 0.8f, 0.25f, 0.5f), min2.z);
				stoveLayout.DoorHalfWidth = Math.Max(0.5f, (max2.x - min2.x) / 2f + 0.1f);
				stoveLayout.DoorHalfHeight = 0.3f;
			}
		}
		else
		{
			stoveLayout.MinX = stoveLayout.Door.x - 0.4f;
			stoveLayout.MaxX = stoveLayout.Door.x + 0.4f;
			stoveLayout.FrontZ = stoveLayout.Door.z;
			stoveLayout.CenterZ = stoveLayout.Door.z + 0.3f;
		}
		if (flag2)
		{
			stoveLayout.Furnace = true;
			stoveLayout.MinX = stoveLayout.Door.x - stoveLayout.DoorHalfWidth - 0.1f;
			stoveLayout.MaxX = stoveLayout.Door.x + stoveLayout.DoorHalfWidth + 0.1f;
			stoveLayout.FrontZ = stoveLayout.Door.z;
			stoveLayout.CenterZ = stoveLayout.Door.z + 0.3f;
			stoveLayout.Lowered = 0.1f * stoveLayout.DoorHalfHeight * 2f;
			stoveLayout.Door.y -= stoveLayout.Lowered;
		}
		string name = transform2.name;
		if (name.StartsWith("INTERACTIVE_StoveMetalA", StringComparison.OrdinalIgnoreCase))
		{
			stoveLayout.ExtraBack = 0.2f;
		}
		if (name.StartsWith("INTERACTIVE_Forge", StringComparison.OrdinalIgnoreCase))
		{
			stoveLayout.Scale = 1.05f;
		}
		stoveLayout.How = $"front = root {FireZone.V(vector3)}, door {(flag ? "animated" : ((list.Count > 0) ? "not animated" : "none (open hearth)"))} at {FireZone.V(stoveLayout.Door)} size {stoveLayout.DoorHalfWidth * 2f:0.00} x {stoveLayout.DoorHalfHeight * 2f:0.00} m, body ('{transform2.name}'{(flag2 ? ", its furnace only" : "")}) x {stoveLayout.MinX:0.00}..{stoveLayout.MaxX:0.00}, front z {stoveLayout.FrontZ:0.00}, middle z {stoveLayout.CenterZ:0.00}" + ((stoveLayout.Scale != 1f) ? $", zones x{stoveLayout.Scale:0.00}" : "") + ((stoveLayout.ExtraBack > 0f) ? $", {stoveLayout.ExtraBack:P0} further back" : "") + ((stoveLayout.Lowered > 0f) ? $", {stoveLayout.Lowered:0.00} m lower" : "");
		return stoveLayout;
	}

	public FireZone Firebox()
	{
		if (!Open)
		{
			return FireZone.ScaledBox(Frame, new Vector3(Door.x - DoorHalfWidth, Door.y - DoorHalfHeight, Door.z - 0.33f), new Vector3(Door.x + DoorHalfWidth, Door.y + DoorHalfHeight, Door.z + 0.4f), ZoneScale, "stove door and firebox");
		}
		return FireZone.ScaledBox(Frame, new Vector3(Door.x - DoorHalfWidth, Door.y - DoorHalfHeight, Door.z - 0.12f), new Vector3(Door.x + DoorHalfWidth, Door.y + DoorHalfHeight, Door.z + 0.55f), ZoneScale, "hearth fire");
	}

	public FireZone Funnel()
	{
		return FireZone.Funnel(Frame, new Vector3(Door.x, Door.y, Door.z - 0.02f), new Vector3(0f, -0.6f, -1f), 40f, 55f, Math.Max(0f, DoorHalfWidth - 0.15f), (Open ? 1.3f : 1f) * Main.Settings.FunnelScale * Scale, Open ? "hearth funnel" : "stove funnel");
	}

	public FireZone[] Sides()
	{
		float y = Math.Max(1.2f, Door.y + DoorHalfHeight + 0.4f);
		float scale = ZoneScale * (Open ? 1.1f : 1f);
		float num = FrontZ - 0.3f;
		float num2 = CenterZ + 0.2f;
		num2 += (num2 - num) * ExtraBack;
		return new FireZone[2]
		{
			FireZone.ScaledBox(Frame, new Vector3(MinX - 1.1f, -0.3f, num), new Vector3(MinX, y, num2), scale, Furnace ? "furnace left side" : "stove left side"),
			FireZone.ScaledBox(Frame, new Vector3(MaxX, -0.3f, num), new Vector3(MaxX + 1.1f, y, num2), scale, Furnace ? "furnace right side" : "stove right side")
		};
	}

	public FireZone Opener()
	{
		if (!HasDoor)
		{
			return null;
		}
		float num = FrontZ - 1.6f;
		float num2 = CenterZ + 0.4f;
		num2 += (num2 - num) * ExtraBack;
		return FireZone.ScaledBox(Frame, new Vector3(MinX - 1f, -0.3f, num), new Vector3(MaxX + 1f, Math.Max(1.7f, Door.y + DoorHalfHeight + 0.9f), num2), ZoneScale, "stove door opener");
	}

	private static bool SpotsCentre(FireplaceInteraction fi, Transform space, out Vector3 centre)
	{
		centre = Vector3.zero;
		Il2CppReferenceArray<CookingSlot> cookingSlots = fi.m_CookingSlots;
		if (cookingSlots == null || cookingSlots.Length == 0)
		{
			return false;
		}
		int num = 0;
		foreach (CookingSlot item in cookingSlots)
		{
			if (!(item == null))
			{
				centre += space.InverseTransformPoint(item.transform.position);
				num++;
			}
		}
		if (num == 0)
		{
			return false;
		}
		centre /= (float)num;
		return true;
	}

	private static Vector3 MiddleOf(Transform model)
	{
		bool flag = false;
		Bounds bounds = default(Bounds);
		foreach (MeshRenderer componentsInChild in model.GetComponentsInChildren<MeshRenderer>(includeInactive: false))
		{
			if (!(componentsInChild == null) && componentsInChild.enabled)
			{
				if (!flag)
				{
					bounds = componentsInChild.bounds;
					flag = true;
				}
				else
				{
					bounds.Encapsulate(componentsInChild.bounds);
				}
			}
		}
		if (!flag)
		{
			return model.position;
		}
		return bounds.center;
	}

	internal static Transform ModelRoot(Transform t)
	{
		Transform result = t;
		Transform parent = t.parent;
		while (parent != null)
		{
			if (parent.name.StartsWith("INTERACTIVE_", StringComparison.OrdinalIgnoreCase))
			{
				result = parent;
			}
			parent = parent.parent;
		}
		return result;
	}

	private static bool Usable(MeshFilter mf)
	{
		if (mf == null || mf.sharedMesh == null)
		{
			return false;
		}
		MeshRenderer component = mf.GetComponent<MeshRenderer>();
		if (component == null || !component.enabled)
		{
			return false;
		}
		string name = mf.name;
		if (name.StartsWith("Cylinder", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		int num = name.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
		if (num >= 0)
		{
			return name.Substring(num).StartsWith("_LOD0", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static Vector3 Centre(List<MeshFilter> parts, Transform space)
	{
		Bounds(parts, space, out var min, out var max);
		return (min + max) / 2f;
	}

	private static bool Bounds(List<MeshFilter> parts, Transform space, out Vector3 min, out Vector3 max, float maxHeight = float.MaxValue)
	{
		bool flag = false;
		min = (max = Vector3.zero);
		foreach (MeshFilter part in parts)
		{
			Bounds bounds = part.sharedMesh.bounds;
			if (bounds.size.y * Math.Abs(part.transform.lossyScale.y) > maxHeight)
			{
				continue;
			}
			for (int i = 0; i < 8; i++)
			{
				Vector3 position = new Vector3(((i & 1) == 0) ? bounds.min.x : bounds.max.x, ((i & 2) == 0) ? bounds.min.y : bounds.max.y, ((i & 4) == 0) ? bounds.min.z : bounds.max.z);
				Vector3 vector = space.InverseTransformPoint(part.transform.TransformPoint(position));
				if (!flag)
				{
					min = (max = vector);
					flag = true;
				}
				else
				{
					min = Vector3.Min(min, vector);
					max = Vector3.Max(max, vector);
				}
			}
		}
		return flag;
	}
}
