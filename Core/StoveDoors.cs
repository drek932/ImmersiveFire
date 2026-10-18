using System;
using System.Collections.Generic;
using Il2Cpp;
using ImmersiveShared;
using UnityEngine;

namespace ImmersiveFire
{
    /// <summary>
    /// A burning stove opens its door wide when fuel comes near (held, carried in a group or thrown), so it can be pushed
    /// in; the door closes again a moment after the fuel is gone.
    /// The game's Open() / Close() are called for the state (WoodStove.m_Open), sounds and its own tweens, which are left
    /// to finish: stopping them (0.5.1) left ObjectAnim.IsAnimating() true for good, so the door never opened again.
    /// The door's turn is set every LateUpdate, after the game's tween moved it (pot belly: RotateBy 35 degrees around
    /// the hinge's (0, -1, 0), ~0.9 s): what is seen is this mod's fast, wide turn.
    /// </summary>
    internal static class StoveDoors
    {
        private const float WideAngle = 110f;       // degrees from closed
        private const float OpenSeconds = 0.15f;
        private const float CloseSeconds = 0.3f;     // base closing turn, slowed down by its own setting (up to x2)
        private const float CloseAfter = 1.5f;      // base s without fuel near, sped up the same way
        private static readonly Vector3 DefaultAxis = Vector3.down; // the pot belly's (log of 0.4.0), when the game's tween says nothing
        private const float FoldDownAngle = 85f;    // doors hinged at the bottom or top: not into the floor

        private enum Phase { Closed, Opening, Open, Closing, Settling }

        private sealed class Door
        {
            public WoodStove Stove;
            public ObjectAnim Anim;
            public Transform Hinge;
            public Quaternion Closed, Wide;
            public Vector3 Axis;
            public float Angle;
            public Phase Phase;
            public float T, LastNear;
            public bool Near, Burning;
        }

        private static readonly Dictionary<IntPtr, Door> doors = new();

        public static void Reset() => doors.Clear();

        /// <summary>Called every frame (Update) for each stove with a door: is fuel near it?</summary>
        public static void Mark(WoodStove stove, bool fuelNear, bool burning)
        {
            if (!doors.TryGetValue(stove.Pointer, out var d))
            {
                var anim = stove.m_ObjectAnim;
                if (anim == null) return;
                var hinge = anim.m_Target != null ? anim.m_Target.transform : anim.transform;
                d = new Door { Stove = stove, Anim = anim, Hinge = hinge };
                HowItOpens(d);
                doors[stove.Pointer] = d;
            }
            d.Near = fuelNear && burning;
            d.Burning = burning;
            if (d.Near) d.LastNear = Time.time;
        }

        /// <summary>Every LateUpdate: after the game's tweens moved the doors this frame.</summary>
        public static void LateUpdate()
        {
            foreach (var d in doors.Values)
            {
                try
                {
                    if (d.Stove == null || d.Hinge == null) continue;
                    Step(d);
                }
                catch (Exception e)
                {
                    Once.Warn("door:" + e.GetType().Name, "Moving a stove door failed: " + e);
                }
            }
        }

        private static void Step(Door d)
        {
            float now = Time.time;
            float speedUp = Main.Settings.DoorCloseSpeedUp;
            bool wanted = now - d.LastNear < CloseAfter / speedUp && d.Burning;
            switch (d.Phase)
            {
                case Phase.Closed:
                    // Opened by the game (fire menu) or its tween still running: not ours.
                    if (!d.Near || d.Stove.m_Open || d.Anim.IsAnimating()) return;
                    d.Closed = d.Hinge.localRotation;
                    d.Wide = d.Closed * Quaternion.AngleAxis(d.Angle, d.Axis);
                    if (!d.Stove.Open()) return;
                    Begin(d, Phase.Opening, 0f);
                    Verbose.Log($"Stove door of '{d.Stove.gameObject.name}' opening wide");
                    break;

                case Phase.Opening:
                    d.T = Math.Min(1f, d.T + Time.deltaTime / OpenSeconds);
                    if (d.T >= 1f) d.Phase = Phase.Open;
                    break;

                case Phase.Open:
                    // No fuel near for a while, or the game closed it meanwhile (fire menu closed): close.
                    if (!wanted || !d.Stove.m_Open) Begin(d, Phase.Closing, 0f);
                    break;

                case Phase.Closing:
                    // Fuel came back while closing: open again from here.
                    if (d.Near && d.Stove.m_Open)
                    {
                        Begin(d, Phase.Opening, 1f - d.T);
                        break;
                    }
                    d.T = Math.Min(1f, d.T + Time.deltaTime / (CloseSeconds * Main.Settings.DoorTurnSlowDown));
                    if (d.T < 1f) break;
                    if (d.Stove.m_Open) d.Stove.Close();
                    d.Phase = Phase.Settling;
                    break;

                case Phase.Settling:
                    // The game's close tween runs on; the door is kept shut until it is done. Fuel again: open at once.
                    if (d.Near && !d.Stove.m_Open && d.Stove.Open())
                    {
                        Begin(d, Phase.Opening, 0f);
                        break;
                    }
                    if (!d.Anim.IsAnimating())
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

        private static Quaternion Rotation(Door d) => d.Phase switch
        {
            Phase.Opening => Quaternion.Slerp(d.Closed, d.Wide, Ease(d.T)),
            Phase.Open => d.Wide,
            Phase.Closing => Quaternion.Slerp(d.Wide, d.Closed, Ease(d.T)),
            _ => d.Closed,
        };

        private static float Ease(float t) => t * t * (3f - 2f * t);

        /// <summary>
        /// The axis the door turns around, from the game's own "open" tween (iTween RotateBy "amount", in turns of 360
        /// degrees, around the hinge's own axes): wide open is the same way, 110 degrees for a side hinge, 85 for a door
        /// hinged at its bottom or top (it would go into the floor).
        /// </summary>
        private static void HowItOpens(Door d)
        {
            d.Axis = DefaultAxis;
            if (OpenTween(d.Anim, out var axis, out float gameAngle)) d.Axis = axis;
            bool sideHinge = Math.Abs(d.Axis.y) > 0.7f;
            d.Angle = Math.Max(gameAngle, sideHinge ? WideAngle : FoldDownAngle);
            Verbose.Log($"Stove door of '{d.Stove.gameObject.name}': hinge '{d.Hinge.name}', game opens {gameAngle:0} deg around " +
                        $"{FireZone.V(d.Axis)}, wide = {d.Angle:0} deg ({(sideHinge ? "side" : "bottom/top")} hinge)");
        }

        /// <summary>
        /// The game's "open" tween of an ObjectAnim (iTween RotateBy "amount", in turns of 360 degrees around the target's
        /// own axes): its axis and angle. False if it has none.
        /// </summary>
        public static bool OpenTween(ObjectAnim anim, out Vector3 axis, out float angle)
        {
            axis = Vector3.zero;
            angle = 0f;
            try
            {
                if (anim == null) return false;
                System.Collections.Generic.IEnumerable<iTweenEvent> events = anim.m_Events;
                // Some keep their tweens on the target itself (the firewood bin's "Hinge"), not in m_Events.
                if (anim.m_Events == null || anim.m_Events.Length == 0)
                    events = (anim.m_Target != null ? anim.m_Target : anim.gameObject).GetComponentsInChildren<iTweenEvent>(true);
                if (events == null) return false;
                foreach (var e in events)
                {
                    if (e == null || !string.Equals(e.tweenName, "open", StringComparison.OrdinalIgnoreCase)) continue;
                    var keys = e.keys;
                    for (int i = 0; keys != null && i < keys.Length; i++)
                    {
                        if (!string.Equals(keys[i], "amount", StringComparison.OrdinalIgnoreCase)) continue;
                        var amount = e.vector3s[e.indexes[i]];
                        if (amount.sqrMagnitude < 1e-8f) continue;
                        axis = amount.normalized;
                        angle = amount.magnitude * 360f;
                        if (angle > 180f) angle = amount.magnitude; // given in degrees after all
                        return true;
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
}
