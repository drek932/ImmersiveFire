#define VERBOSE
using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using ImmersiveShared;
using UnityEngine;

namespace ImmersiveFire;

internal static class StoveDoors
{
	private enum Phase
	{
		Closed,
		Opening,
		Open,
		Closing,
		Settling
	}

	private sealed class Door
	{
		public WoodStove Stove;

		public ObjectAnim Anim;

		public Transform Hinge;

		public Quaternion Closed;

		public Quaternion Wide;

		public Vector3 Axis;

		public float Angle;

		public Phase Phase;

		public float T;

		public float LastNear;

		public bool Near;

		public bool Burning;
	}

	private const float WideAngle = 110f;

	private const float OpenSeconds = 0.15f;

	private const float CloseSeconds = 0.3f;

	private const float CloseAfter = 1.5f;

	private static readonly Vector3 DefaultAxis = Vector3.down;

	private const float FoldDownAngle = 85f;

	private static readonly Dictionary<IntPtr, Door> doors = new Dictionary<IntPtr, Door>();

	public static void Reset()
	{
		doors.Clear();
	}

	public static void Mark(WoodStove stove, bool fuelNear, bool burning)
	{
		if (!doors.TryGetValue(stove.Pointer, out var value))
		{
			ObjectAnim objectAnim = stove.m_ObjectAnim;
			if (objectAnim == null)
			{
				return;
			}
			Transform hinge = ((objectAnim.m_Target != null) ? objectAnim.m_Target.transform : objectAnim.transform);
			value = new Door
			{
				Stove = stove,
				Anim = objectAnim,
				Hinge = hinge
			};
			HowItOpens(value);
			doors[stove.Pointer] = value;
		}
		value.Near = fuelNear && burning;
		value.Burning = burning;
		if (value.Near)
		{
			value.LastNear = Time.time;
		}
	}

	public static void LateUpdate()
	{
		foreach (Door value in doors.Values)
		{
			try
			{
				if (!(value.Stove == null) && !(value.Hinge == null))
				{
					Step(value);
				}
			}
			catch (Exception ex)
			{
				Once.Warn("door:" + ex.GetType().Name, "Moving a stove door failed: " + ex);
			}
		}
	}

	private static void Step(Door d)
	{
		float time = Time.time;
		float doorCloseSpeedUp = Main.Settings.DoorCloseSpeedUp;
		bool flag = time - d.LastNear < 1.5f / doorCloseSpeedUp && d.Burning;
		switch (d.Phase)
		{
		case Phase.Closed:
			if (!d.Near || d.Stove.m_Open || d.Anim.IsAnimating())
			{
				return;
			}
			d.Closed = d.Hinge.localRotation;
			d.Wide = d.Closed * Quaternion.AngleAxis(d.Angle, d.Axis);
			if (!d.Stove.Open())
			{
				return;
			}
			Begin(d, Phase.Opening, 0f);
			Verbose.Log("Stove door of '" + d.Stove.gameObject.name + "' opening wide");
			break;
		case Phase.Opening:
			d.T = Math.Min(1f, d.T + Time.deltaTime / 0.15f);
			if (d.T >= 1f)
			{
				d.Phase = Phase.Open;
			}
			break;
		case Phase.Open:
			if (!flag || !d.Stove.m_Open)
			{
				Begin(d, Phase.Closing, 0f);
			}
			break;
		case Phase.Closing:
			if (d.Near && d.Stove.m_Open)
			{
				Begin(d, Phase.Opening, 1f - d.T);
				break;
			}
			d.T = Math.Min(1f, d.T + Time.deltaTime / (0.3f * Main.Settings.DoorTurnSlowDown));
			if (!(d.T < 1f))
			{
				if (d.Stove.m_Open)
				{
					d.Stove.Close();
				}
				d.Phase = Phase.Settling;
			}
			break;
		case Phase.Settling:
			if (d.Near && !d.Stove.m_Open && d.Stove.Open())
			{
				Begin(d, Phase.Opening, 0f);
			}
			else if (!d.Anim.IsAnimating())
			{
				d.Hinge.localRotation = d.Closed;
				d.Phase = Phase.Closed;
				return;
			}
			break;
		}
		d.Hinge.localRotation = Rotation(d);
	}

	private static void Begin(Door d, Phase phase, float t)
	{
		d.Phase = phase;
		d.T = t;
	}

	private static Quaternion Rotation(Door d)
	{
		return d.Phase switch
		{
			Phase.Opening => Quaternion.Slerp(d.Closed, d.Wide, Ease(d.T)), 
			Phase.Open => d.Wide, 
			Phase.Closing => Quaternion.Slerp(d.Wide, d.Closed, Ease(d.T)), 
			_ => d.Closed, 
		};
	}

	private static float Ease(float t)
	{
		return t * t * (3f - 2f * t);
	}

	private static void HowItOpens(Door d)
	{
		d.Axis = DefaultAxis;
		if (OpenTween(d.Anim, out var axis, out var angle))
		{
			d.Axis = axis;
		}
		bool flag = Math.Abs(d.Axis.y) > 0.7f;
		d.Angle = Math.Max(angle, flag ? 110f : 85f);
		Verbose.Log($"Stove door of '{d.Stove.gameObject.name}': hinge '{d.Hinge.name}', game opens {angle:0} deg around {FireZone.V(d.Axis)}, wide = {d.Angle:0} deg ({(flag ? "side" : "bottom/top")} hinge)");
	}

	public static bool OpenTween(ObjectAnim anim, out Vector3 axis, out float angle)
	{
		axis = Vector3.zero;
		angle = 0f;
		try
		{
			if (anim == null)
			{
				return false;
			}
			IEnumerable<iTweenEvent> enumerable = anim.m_Events;
			if (anim.m_Events == null || anim.m_Events.Length == 0)
			{
				enumerable = ((anim.m_Target != null) ? anim.m_Target : anim.gameObject).GetComponentsInChildren<iTweenEvent>(includeInactive: true);
			}
			if (enumerable == null)
			{
				return false;
			}
			foreach (iTweenEvent item in enumerable)
			{
				if (item == null || !string.Equals(item.tweenName, "open", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				Il2CppStringArray keys = item.keys;
				int num = 0;
				while (keys != null && num < keys.Length)
				{
					if (string.Equals(keys[num], "amount", StringComparison.OrdinalIgnoreCase))
					{
						Vector3 vector = item.vector3s[item.indexes[num]];
						if (!(vector.sqrMagnitude < 1E-08f))
						{
							axis = vector.normalized;
							angle = vector.magnitude * 360f;
							if (angle > 180f)
							{
								angle = vector.magnitude;
							}
							return true;
						}
					}
					num++;
				}
			}
		}
		catch (Exception ex)
		{
			Once.Warn("tween", "Reading an open tween: " + ex);
		}
		return false;
	}
}
