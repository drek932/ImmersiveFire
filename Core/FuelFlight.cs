#define VERBOSE
using System;
using System.Collections.Generic;
using Il2Cpp;
using ImmersiveShared;
using UnityEngine;

namespace ImmersiveFire;

internal static class FuelFlight
{
	private sealed class Flight
	{
		public GameObject Copy;

		public Vector3[] Points;

		public float[] Lengths;

		public Vector3 Scale;

		public Quaternion Rotation;

		public float T;

		public float Duration;

		public float Hold;

		public Quaternion TurnTo;

		public float TurnLength;

		public float DoorT;

		public IntPtr Target;

		public GameObject SoundAt;
	}

	private const float Seconds = 0.4f;

	private const float EndScale = 0.6f;

	private const float DoorLead = 0.45f;

	private static readonly List<Flight> flights = new List<Flight>();

	private static int obstacleMask = -1;

	public static void Reset()
	{
		foreach (Flight flight in flights)
		{
			if (flight.Copy != null)
			{
				UnityEngine.Object.Destroy(flight.Copy);
			}
		}
		flights.Clear();
	}

	public static bool Start(GearItem gi, IList<Vector3> points, float speed, IntPtr target, GameObject soundAt, int freeEnd, int exitPoints, Quaternion? turnTo, int turnPoints, params Transform[] ignore)
	{
		try
		{
			GameObject gameObject = LookAlike(gi);
			if (gameObject == null)
			{
				return false;
			}
			List<Vector3> list = new List<Vector3> { gameObject.transform.position };
			exitPoints = Math.Min(exitPoints, points.Count);
			int num = -1;
			for (int i = 0; i < exitPoints; i++)
			{
				Straight(list, points[i]);
				if (i == turnPoints - 1)
				{
					num = list.Count - 1;
				}
			}
			int num2 = list.Count - 1;
			List<Vector3> list2 = new List<Vector3> { list[num2] };
			for (int j = exitPoints; j < points.Count; j++)
			{
				list2.Add(points[j]);
			}
			AroundObstacles(list2, gi.transform, freeEnd, ignore);
			for (int k = 1; k < list2.Count; k++)
			{
				list.Add(list2[k]);
			}
			Vector3[] array = list.ToArray();
			float[] array2 = new float[array.Length];
			for (int l = 1; l < array.Length; l++)
			{
				array2[l] = array2[l - 1] + Vector3.Distance(array[l - 1], array[l]);
			}
			float num3 = array2[array.Length - 1];
			float num4 = 0.4f * Mathf.Clamp(num3, 0.8f, 4f) / Math.Max(0.1f, speed);
			float num5 = array2[Math.Max(0, array.Length - 1 - freeEnd)];
			float num6 = 1f;
			for (int m = 0; m <= 200; m++)
			{
				if (Ease((float)m / 200f) * num3 >= num5)
				{
					num6 = (float)m / 200f;
					break;
				}
			}
			flights.Add(new Flight
			{
				Copy = gameObject,
				Points = array,
				Lengths = array2,
				Duration = num4,
				Target = target,
				SoundAt = soundAt,
				Scale = gameObject.transform.localScale,
				Rotation = gameObject.transform.rotation,
				Hold = array2[num2],
				TurnTo = (turnTo ?? gameObject.transform.rotation),
				TurnLength = ((num > 0) ? array2[num] : 0f),
				DoorT = Math.Max(0f, num6 - 0.45f / num4)
			});
			Verbose.Log($"Flight of {gi.name}: {num3:0.00} m in {num4:0.00} s" + ((exitPoints > 0) ? $", out of a bin in {array2[num2]:0.00} m" : "") + ((target != IntPtr.Zero) ? $", at the door after {num6 * num4:0.00} s, door opens at {Math.Max(0f, num6 - 0.45f / num4) * num4:0.00} s" : ""));
			return true;
		}
		catch (Exception ex)
		{
			Once.Warn("flight", "The fuel's flight into the fire failed: " + ex);
			return false;
		}
	}

	public static bool Approaching(IntPtr stove)
	{
		foreach (Flight flight in flights)
		{
			if (flight.Copy != null && flight.Target == stove && flight.T >= flight.DoorT)
			{
				return true;
			}
		}
		return false;
	}

	private static float Ease(float T)
	{
		return Mathf.Lerp(T * T * (3f - 2f * T), T * T, 0.4f);
	}

	private static void AroundObstacles(List<Vector3> path, Transform item, int freeEnd, Transform[] ignore)
	{
		if (obstacleMask < 0)
		{
			obstacleMask = Physics.DefaultRaycastLayers & ~(1 << LayerMask.NameToLayer("Gear"));
			int num = LayerMask.NameToLayer("Player");
			if (num >= 0)
			{
				obstacleMask &= ~(1 << num);
			}
		}
		int num2 = 0;
		for (int i = 0; i < path.Count - 1 - freeEnd; i++)
		{
			if (num2 >= 6)
			{
				break;
			}
			if (Physics.Linecast(path[i], path[i + 1], out var hitInfo, obstacleMask, QueryTriggerInteraction.Ignore))
			{
				Transform transform = hitInfo.collider.transform;
				if (!transform.IsChildOf(item) && !Ignored(transform, ignore) && !(hitInfo.distance < 0.05f))
				{
					Bounds bounds = hitInfo.collider.bounds;
					float val = ((bounds.size.magnitude > 6f) ? (hitInfo.point.y + 0.3f) : (bounds.max.y + 0.25f));
					float y = Math.Max(Math.Max(path[i].y, path[i + 1].y), val);
					Vector3 vector = new Vector3(hitInfo.point.x, y, hitInfo.point.z);
					path.Insert(i + 1, vector);
					num2++;
					i--;
					Verbose.Log("Flight goes over '" + hitInfo.collider.name + "' at " + FireZone.V(vector));
				}
			}
		}
	}

	private static void Straight(List<Vector3> path, Vector3 to)
	{
		Vector3 a = path[path.Count - 1];
		int num = Math.Max(1, Mathf.CeilToInt(Vector3.Distance(a, to) / 0.1f));
		for (int i = 1; i <= num; i++)
		{
			path.Add(Vector3.Lerp(a, to, (float)i / (float)num));
		}
	}

	private static bool Ignored(Transform t, Transform[] ignore)
	{
		if (ignore == null)
		{
			return false;
		}
		foreach (Transform transform in ignore)
		{
			if (transform != null && t.IsChildOf(transform))
			{
				return true;
			}
		}
		return false;
	}

	public static void Update()
	{
		for (int num = flights.Count - 1; num >= 0; num--)
		{
			Flight flight = flights[num];
			if (flight.Copy == null)
			{
				flights.RemoveAt(num);
			}
			else
			{
				flight.T = Math.Min(1f, flight.T + Time.deltaTime / flight.Duration);
				float num2 = Ease(flight.T);
				flight.Copy.transform.position = At(flight, num2);
				float num3 = flight.Lengths[flight.Lengths.Length - 1];
				float num4 = ((flight.Hold <= 0f) ? num2 : ((num3 - flight.Hold < 0.001f) ? 1f : Mathf.Clamp01((num2 * num3 - flight.Hold) / (num3 - flight.Hold))));
				Quaternion quaternion = ((flight.TurnLength > 0f) ? Quaternion.Slerp(flight.Rotation, flight.TurnTo, Mathf.Clamp01(num2 * num3 / flight.TurnLength)) : flight.TurnTo);
				flight.Copy.transform.rotation = quaternion * Quaternion.Euler(0f, 90f * num4, 0f);
				flight.Copy.transform.localScale = flight.Scale * Mathf.Lerp(1f, 0.6f, num4);
				if (!(flight.T < 1f))
				{
					FuelFeeder.FeedSound(flight.SoundAt);
					UnityEngine.Object.Destroy(flight.Copy);
					flights.RemoveAt(num);
				}
			}
		}
	}

	private static Vector3 At(Flight f, float t)
	{
		Vector3[] points = f.Points;
		float num = f.Lengths[points.Length - 1];
		if (points.Length < 2 || num < 0.0001f)
		{
			return points[^1];
		}
		float num2 = t * num;
		int i;
		for (i = 0; i < points.Length - 2 && f.Lengths[i + 1] < num2; i++)
		{
		}
		float num3 = f.Lengths[i + 1] - f.Lengths[i];
		float num4 = ((num3 > 0.0001f) ? ((num2 - f.Lengths[i]) / num3) : 1f);
		Vector3 vector = points[Math.Max(i - 1, 0)];
		Vector3 vector2 = points[i];
		Vector3 vector3 = points[i + 1];
		Vector3 vector4 = points[Math.Min(i + 2, points.Length - 1)];
		float num5 = num4 * num4;
		float num6 = num5 * num4;
		return 0.5f * (2f * vector2 + (vector3 - vector) * num4 + (2f * vector - 5f * vector2 + 4f * vector3 - vector4) * num5 + (3f * vector2 - vector - 3f * vector3 + vector4) * num6);
	}

	private static GameObject LookAlike(GearItem gi)
	{
		Transform transform = gi.transform;
		List<MeshFilter> list = new List<MeshFilter>();
		Bounds bounds = default(Bounds);
		foreach (MeshFilter componentsInChild in gi.GetComponentsInChildren<MeshFilter>(includeInactive: false))
		{
			if (componentsInChild == null || componentsInChild.sharedMesh == null)
			{
				continue;
			}
			MeshRenderer component = componentsInChild.GetComponent<MeshRenderer>();
			if (component == null || !component.enabled)
			{
				continue;
			}
			string name = componentsInChild.name;
			int num = name.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
			if (num < 0 || name.Substring(num).StartsWith("_LOD0", StringComparison.OrdinalIgnoreCase))
			{
				if (list.Count == 0)
				{
					bounds = component.bounds;
				}
				else
				{
					bounds.Encapsulate(component.bounds);
				}
				list.Add(componentsInChild);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		GameObject gameObject = new GameObject("ImmersiveFire flying fuel");
		gameObject.transform.SetPositionAndRotation(bounds.center, transform.rotation);
		gameObject.transform.localScale = transform.lossyScale;
		int num2 = 0;
		foreach (MeshFilter item in list)
		{
			MeshRenderer component2 = item.GetComponent<MeshRenderer>();
			GameObject gameObject2 = new GameObject(item.name);
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			gameObject2.transform.position = item.transform.position;
			gameObject2.transform.rotation = item.transform.rotation;
			Vector3 lossyScale = item.transform.lossyScale;
			Vector3 lossyScale2 = transform.lossyScale;
			gameObject2.transform.localScale = new Vector3(lossyScale.x / Safe(lossyScale2.x), lossyScale.y / Safe(lossyScale2.y), lossyScale.z / Safe(lossyScale2.z));
			gameObject2.AddComponent<MeshFilter>().sharedMesh = item.sharedMesh;
			gameObject2.AddComponent<MeshRenderer>().sharedMaterials = component2.sharedMaterials;
			num2++;
		}
		if (num2 > 0)
		{
			return gameObject;
		}
		UnityEngine.Object.Destroy(gameObject);
		return null;
	}

	private static float Safe(float v)
	{
		if (!(Math.Abs(v) < 0.0001f))
		{
			return v;
		}
		return 1f;
	}
}
