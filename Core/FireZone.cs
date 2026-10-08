using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace ImmersiveFire;

internal sealed class FireZone
{
	public readonly Transform Frame;

	public readonly Vector3 Offset;

	public readonly float Radius;

	public readonly float HalfHeight;

	public readonly Vector3 Half;

	public readonly bool IsBox;

	public readonly bool IsCone;

	public readonly bool IsSphere;

	public readonly Vector3 Axis;

	public readonly float Length;

	public readonly float CosAngle;

	public readonly float Angle;

	public readonly float SideAngle;

	public readonly float Spread;

	public readonly string How;

	private const float CampfireFallbackRadius = 0.35f;

	private const float CampfireBelow = 0.05f;

	private const float CampfireAbove = 0.8f;

	public const float BarrelRim = 1f;

	public Quaternion Yaw => Quaternion.Euler(0f, Frame.eulerAngles.y, 0f);

	public Vector3 Center => Frame.position + Yaw * Offset;

	public float Reach
	{
		get
		{
			if (!IsCone)
			{
				if (!IsSphere)
				{
					if (!IsBox)
					{
						return Mathf.Sqrt(Radius * Radius + HalfHeight * HalfHeight);
					}
					return Half.magnitude;
				}
				return Radius;
			}
			return Length + Spread;
		}
	}

	private FireZone(Transform frame, Vector3 offset, float radius, float halfHeight, string how)
	{
		Frame = frame;
		Offset = offset;
		Radius = radius;
		HalfHeight = halfHeight;
		How = how;
	}

	private FireZone(Transform frame, Vector3 offset, Vector3 half, string how)
	{
		Frame = frame;
		Offset = offset;
		Half = half;
		HalfHeight = half.y;
		IsBox = true;
		How = how;
	}

	private FireZone(Transform frame, Vector3 apex, Vector3 axis, float angle, float length, float sideAngle, float spread, string how)
	{
		Frame = frame;
		Offset = apex;
		Axis = axis.normalized;
		Angle = angle;
		SideAngle = sideAngle;
		Spread = spread;
		CosAngle = Mathf.Cos(angle * Mathf.Deg2Rad);
		Length = length;
		IsCone = true;
		How = how;
	}

	private FireZone(Transform frame, Vector3 centre, float radius, string how)
	{
		Frame = frame;
		Offset = centre;
		Radius = radius;
		HalfHeight = radius;
		IsSphere = true;
		How = how;
	}

	public static FireZone Sphere(Transform frame, Vector3 centre, float radius, string how)
	{
		return new FireZone(frame, centre, radius, how);
	}

	public static FireZone Cone(Transform frame, Vector3 apex, Vector3 axis, float angle, float length, string how)
	{
		return new FireZone(frame, apex, axis, angle, length, angle, 0f, how);
	}

	public static FireZone Funnel(Transform frame, Vector3 apex, Vector3 axis, float angle, float sideAngle, float spread, float length, string how)
	{
		return new FireZone(frame, apex, axis, angle, length, sideAngle, spread, how);
	}

	public static FireZone Box(Transform frame, Vector3 min, Vector3 max, string how)
	{
		return new FireZone(frame, (min + max) / 2f, (max - min) / 2f, how);
	}

	public static FireZone ScaledBox(Transform frame, Vector3 min, Vector3 max, float scale, string how)
	{
		Vector3 offset = (min + max) / 2f;
		Vector3 half = (max - min) / 2f * scale;
		return new FireZone(frame, offset, half, (scale > 1f) ? $"{how} x{scale:0.00}" : how);
	}

	public bool Contains(Vector3 world)
	{
		Vector3 lhs = Quaternion.Inverse(Yaw) * (world - Center);
		if (IsSphere)
		{
			return lhs.sqrMagnitude <= Radius * Radius;
		}
		if (IsCone)
		{
			if (Spread > 0f)
			{
				lhs.x = (float)Math.Sign(lhs.x) * Math.Max(0f, Math.Abs(lhs.x) - Spread);
			}
			float magnitude = lhs.magnitude;
			if (magnitude > Length)
			{
				return false;
			}
			if (magnitude < 0.001f)
			{
				return true;
			}
			float num = Vector3.Dot(lhs, Axis);
			if (num <= 0f)
			{
				return false;
			}
			Vector3 normalized = Vector3.Cross(Axis, Vector3.right).normalized;
			float num2 = lhs.x / (num * Mathf.Tan(SideAngle * Mathf.Deg2Rad));
			float num3 = Vector3.Dot(lhs, normalized) / (num * Mathf.Tan(Angle * Mathf.Deg2Rad));
			return num2 * num2 + num3 * num3 <= 1f;
		}
		if (IsBox)
		{
			if (Math.Abs(lhs.x) <= Half.x && Math.Abs(lhs.y) <= Half.y)
			{
				return Math.Abs(lhs.z) <= Half.z;
			}
			return false;
		}
		if (lhs.x * lhs.x + lhs.z * lhs.z <= Radius * Radius && lhs.y >= 0f - HalfHeight)
		{
			return lhs.y <= HalfHeight;
		}
		return false;
	}

	public Vector3 Local(Vector3 world)
	{
		return Quaternion.Inverse(Yaw) * (world - Frame.position);
	}

	public override string ToString()
	{
		if (!IsSphere)
		{
			if (!IsCone)
			{
				if (IsBox)
				{
					return $"{How}: box centre {V(Center)}, size {V(Half * 2f)}";
				}
				return $"{How}: centre {V(Center)}, radius {Radius:0.00} m, height {HalfHeight * 2f:0.00} m";
			}
			return $"{How}: cone from {V(Center)}, axis {V(Axis)}, {Angle:0} deg" + ((SideAngle != Angle) ? $" ({SideAngle:0} to the sides)" : "") + ((Spread > 0f) ? $", apex {Spread * 2f:0.00} m wide" : "") + $", {Length:0.00} m";
		}
		return $"{How}: ball around {V(Center)}, radius {Radius:0.00} m";
	}

	public static string V(Vector3 v)
	{
		return $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
	}

	public static FireKind KindOf(FireplaceInteraction fi)
	{
		if (fi.Forge != null)
		{
			return FireKind.Forge;
		}
		if (fi.TryCast<Campfire>() != null)
		{
			return FireKind.Campfire;
		}
		string name = fi.gameObject.name;
		if (name.IndexOf("fireplace", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("hearth", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return FireKind.Fireplace;
		}
		return FireKind.Stove;
	}

	public static FireZone For(FireplaceInteraction fi, FireKind kind)
	{
		if (kind == FireKind.Campfire)
		{
			Campfire? campfire = fi.TryCast<Campfire>();
			Transform transform = campfire.transform;
			float campfireZoneScale = Main.Settings.CampfireZoneScale;
			string how;
			float radius = InnerRingRadius(campfire, out how) * campfireZoneScale;
			float num = 0.8f * campfireZoneScale;
			return new FireZone(transform, new Vector3(0f, (num - 0.05f) / 2f, 0f), radius, (num + 0.05f) / 2f, (campfireZoneScale > 1f) ? $"{how}, zones x{campfireZoneScale:0.00}" : how);
		}
		if (IsPotBellyStove(fi))
		{
			return ScaledBox(fi.transform, new Vector3(-0.2f, 0.55f, -0.58f), new Vector3(0.2f, 0.95f, 0.15f), Main.Settings.StoveZoneScale, "pot belly stove door and firebox");
		}
		if (IsBarrel(fi))
		{
			return new FireZone(fi.transform, new Vector3(0f, 0.6f, 0f), 0.3f * Main.Settings.StoveZoneScale, 0.25f, "fire barrel inside");
		}
		return StoveLayout.Of(fi).Firebox();
	}

	public static bool IsPotBellyStove(FireplaceInteraction fi)
	{
		return fi.gameObject.name.StartsWith("INTERACTIVE_PotBellyStove", StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsBarrel(FireplaceInteraction fi)
	{
		return fi.gameObject.name.StartsWith("INTERACTIVE_FireBarrel", StringComparison.OrdinalIgnoreCase);
	}

	public static FireZone Funnel(FireplaceInteraction fi)
	{
		Campfire campfire = fi.TryCast<Campfire>();
		if (campfire != null)
		{
			return Sphere(campfire.transform, new Vector3(0f, 0.2f, 0f), 1.32f * Main.Settings.CampfireZoneScale, "campfire pull");
		}
		if (IsBarrel(fi))
		{
			return Sphere(fi.transform, new Vector3(0f, 0.5f, 0f), 1.1f * Main.Settings.StoveZoneScale, "fire barrel pull");
		}
		if (IsPotBellyStove(fi))
		{
			return Cone(fi.transform, new Vector3(0f, 0.74f, -0.25f), new Vector3(0f, -0.6f, -1f), 40f, 1f * Main.Settings.FunnelScale, "pot belly stove funnel");
		}
		return StoveLayout.Of(fi).Funnel();
	}

	public static FireZone[] Sides(FireplaceInteraction fi)
	{
		if (fi.TryCast<Campfire>() != null || IsBarrel(fi))
		{
			return null;
		}
		if (!IsPotBellyStove(fi))
		{
			return StoveLayout.Of(fi).Sides();
		}
		float stoveZoneScale = Main.Settings.StoveZoneScale;
		return new FireZone[2]
		{
			ScaledBox(fi.transform, new Vector3(-1.2f, -0.3f, -0.6f), new Vector3(-0.3f, 1.2f, 0.2f), stoveZoneScale, "pot belly stove left side"),
			ScaledBox(fi.transform, new Vector3(0.3f, -0.3f, -0.6f), new Vector3(1.2f, 1.2f, 0.2f), stoveZoneScale, "pot belly stove right side")
		};
	}

	public static FireZone DoorTrigger(FireplaceInteraction fi)
	{
		if (fi.TryCast<Campfire>() != null || IsBarrel(fi))
		{
			return null;
		}
		if (!IsPotBellyStove(fi))
		{
			return StoveLayout.Of(fi).Opener();
		}
		return ScaledBox(fi.transform, new Vector3(-1.3f, -0.3f, -1.8f), new Vector3(1.3f, 1.7f, 0.4f), Main.Settings.StoveZoneScale, "pot belly stove door opener");
	}

	private static float InnerRingRadius(Campfire campfire, out string how)
	{
		GameObject rockRingParent = campfire.m_RockRingParent;
		if (rockRingParent == null)
		{
			how = "campfire, no stone ring (default radius)";
			return 0.35f;
		}
		Vector3 position = campfire.transform.position;
		List<float> list = new List<float>();
		foreach (MeshFilter componentsInChild in rockRingParent.GetComponentsInChildren<MeshFilter>(includeInactive: false))
		{
			if (!(componentsInChild == null) && !(componentsInChild.sharedMesh == null) && componentsInChild.name.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				Vector3 vector = Vector3.Scale(componentsInChild.sharedMesh.bounds.size, componentsInChild.transform.lossyScale);
				Vector3 vector2 = componentsInChild.transform.TransformPoint(componentsInChild.sharedMesh.bounds.center);
				float magnitude = new Vector2(vector2.x - position.x, vector2.z - position.z).magnitude;
				list.Add(magnitude - (Math.Abs(vector.x) + Math.Abs(vector.z)) / 4f);
			}
		}
		if (list.Count == 0)
		{
			how = "campfire, stone ring without stones (default radius)";
			return 0.35f;
		}
		list.Sort();
		float value = list[list.Count / 4];
		how = $"campfire inside {list.Count} stones (inner edges {list[0]:0.00}-{list[list.Count - 1]:0.00} m, lower quarter {value:0.00} m)";
		return Mathf.Clamp(value, 0.2f, 0.6f);
	}
}
