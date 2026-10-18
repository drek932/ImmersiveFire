#if VERBOSE
using System.Collections.Generic;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace ImmersiveFire
{
    /// <summary>
    /// TEST ONLY (remove before release): key F6 shows the zones of the fires nearby as small dots (bottom ring, top
    /// ring, centre line) and writes their geometry to the log, to fit each fire type's zone. F6 again hides the dots.
    /// </summary>
    internal static class Calibration
    {
        public const KeyCode Key = KeyCode.F6;
        private const float Around = 10f;

        private static readonly List<GameObject> dots = new();

        public static void Toggle()
        {
            if (dots.Count > 0)
            {
                Clear();
                HUDMessage.AddMessage("Fire zones hidden", 2f, false, true);
                return;
            }

            var player = GameManager.GetPlayerTransform();
            if (player == null) return;
            int count = 0;
            foreach (var (fi, kind, zone, door, funnel, sides) in FuelFeeder.Fires())
            {
                if ((fi.transform.position - player.position).magnitude > Around) continue;
                Main.Log.Msg(Describe(fi, kind, zone) + (door != null ? $"  door opener {door}\n" : "") +
                             (funnel != null ? $"  funnel {funnel}\n" : ""));
                Show(zone, kind);
                if (door != null) ShowBox(door, Color.white);
                if (funnel != null && funnel.IsCone) ShowCone(funnel, new Color(1f, 0.5f, 0f));
                if (funnel != null && funnel.IsSphere) ShowSphere(funnel, new Color(1f, 0.5f, 0f));
                if (sides != null) foreach (var s in sides) ShowBox(s, new Color(1f, 0.5f, 0f));
                count++;
            }
            HUDMessage.AddMessage($"Fire zones shown: {count} (F6 hides)", 2f, false, true);
        }

        public static void Clear()
        {
            foreach (var d in dots) if (d != null) Object.Destroy(d);
            dots.Clear();
        }

        private static void Show(FireZone zone, FireKind kind)
        {
            var colour = kind switch
            {
                FireKind.Campfire => Color.yellow,
                FireKind.Fireplace => Color.cyan,
                FireKind.Forge => Color.magenta,
                _ => Color.green,
            };
            if (zone.IsBox)
            {
                ShowBox(zone, colour);
                return;
            }
            var centre = zone.Center;
            var yaw = Quaternion.Euler(0f, zone.Frame.eulerAngles.y, 0f);
            for (int ring = -1; ring <= 1; ring++)
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI * 2f / 24f;
                    var local = new Vector3(Mathf.Cos(a) * zone.Radius, ring * zone.HalfHeight, Mathf.Sin(a) * zone.Radius);
                    Dot(centre + yaw * local, colour);
                }
            for (int i = -2; i <= 2; i++) Dot(centre + Vector3.up * zone.HalfHeight * i / 2f, Color.red);
        }

        /// <summary>A ball zone: three circles (flat, across and along).</summary>
        private static void ShowSphere(FireZone zone, Color colour)
        {
            var c = zone.Center;
            float r = zone.Radius;
            for (int i = 0; i < 36; i++)
            {
                float a = i * Mathf.PI * 2f / 36f;
                float cos = Mathf.Cos(a) * r, sin = Mathf.Sin(a) * r;
                Dot(c + new Vector3(cos, 0f, sin), colour);
                Dot(c + new Vector3(cos, sin, 0f), colour);
                Dot(c + new Vector3(0f, sin, cos), colour);
            }
        }

        /// <summary>A cone zone: rings across it every 20 cm and dots along its axis.</summary>
        private static void ShowCone(FireZone zone, Color colour)
        {
            var apex = zone.Center;
            var axis = zone.Yaw * zone.Axis;
            // Across the frame (the cone's side, where its apex line runs) and up across the axis.
            var side = zone.Yaw * Vector3.right;
            var other = Vector3.Cross(axis, side).normalized;
            float tanUp = Mathf.Tan(zone.Angle * Mathf.Deg2Rad);
            float tanSide = Mathf.Tan(zone.SideAngle * Mathf.Deg2Rad);
            for (float d = 0.2f; d <= zone.Length + 0.001f; d += 0.2f)
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI * 2f / 24f;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    var across = side * (c * d * tanSide + Mathf.Sign(c) * zone.Spread) + other * (s * d * tanUp);
                    Dot(apex + axis * d + across, colour);
                }
            for (float d = 0f; d <= zone.Length + 0.001f; d += 0.1f) Dot(apex + axis * d, Color.red);
            for (float x = -zone.Spread; x <= zone.Spread + 0.001f; x += 0.08f) Dot(apex + side * x, Color.red);
        }

        /// <summary>Dots along the 12 edges of a box zone.</summary>
        private static void ShowBox(FireZone zone, Color colour)
        {
            var centre = zone.Center;
            var yaw = zone.Yaw;
            var h = zone.Half;
            for (int axis = 0; axis < 3; axis++)
                for (int a = -1; a <= 1; a += 2)
                    for (int b = -1; b <= 1; b += 2)
                    {
                        float length = axis == 0 ? h.x : axis == 1 ? h.y : h.z;
                        int steps = Mathf.Max(2, Mathf.CeilToInt(length * 2f / 0.08f));
                        for (int i = 0; i <= steps; i++)
                        {
                            float t = -1f + 2f * i / steps;
                            var local = axis == 0 ? new Vector3(t * h.x, a * h.y, b * h.z)
                                : axis == 1 ? new Vector3(a * h.x, t * h.y, b * h.z)
                                : new Vector3(a * h.x, b * h.y, t * h.z);
                            Dot(centre + yaw * local, colour);
                        }
                    }
        }

        private static float nextHeldLog;

        /// <summary>While the dots are shown: where the moved fuel is, in each nearby stove's frame (to fit the zones).</summary>
        public static void LogHeld()
        {
            if (dots.Count == 0 || Time.time < nextHeldLog) return;
            var pm = GameManager.GetPlayerManagerComponent();
            var held = pm != null && pm.IsInPlacementMode() ? pm.m_ObjectToPlaceGearItem : null;
            if (held == null) return;
            nextHeldLog = Time.time + 0.5f;
            Vector3 point = held.transform.position;
            foreach (var c in held.GetComponentsInChildren<Collider>(false))
                if (c != null && c.enabled && !c.isTrigger) { point = c.bounds.center; break; }
            foreach (var (fi, kind, zone, door, funnel, sides) in FuelFeeder.Fires())
            {
                if ((door == null && funnel == null) || (fi.transform.position - point).magnitude > 2.5f) continue;
                Main.Log.Msg($"[Calibration] {held.name} at local {FireZone.V(zone.Local(point))} of '{fi.gameObject.name}': " +
                             $"in the firebox zone {zone.Contains(point)}, in the funnel {funnel != null && funnel.Contains(point)}, " +
                             $"beside {sides != null && System.Array.Exists(sides, s => s.Contains(point))}, " +
                             $"in the door opener {door != null && door.Contains(point)}");
            }
        }

        private static void Dot(Vector3 at, Color colour)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ImmersiveFire zone dot";
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            go.transform.position = at;
            go.transform.localScale = Vector3.one * 0.025f;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = colour;
            dots.Add(go);
        }

        private static string Describe(FireplaceInteraction fi, FireKind kind, FireZone zone)
        {
            var sb = new StringBuilder();
            var root = fi.transform;
            sb.AppendLine($"[Calibration] {kind} '{Path(root)}' ({fi.GetIl2CppType().Name}) at {FireZone.V(root.position)}, yaw {root.eulerAngles.y:0}");
            sb.AppendLine($"  zone {zone}");
            var fire = fi.Fire;
            if (fire != null)
                sb.AppendLine($"  Fire on '{fire.gameObject.name}' at local {FireZone.V(root.InverseTransformPoint(fire.transform.position))}, " +
                              $"state {fire.GetFireState()}, burning {fire.m_ElapsedOnTODSeconds / 60f:0} min ({fire.GetMinutesBurning():0} min), " +
                              $"left {fire.GetRemainingLifeTimeHours():0.00} h");
            var campfire = fi.TryCast<Campfire>();
            if (campfire != null && campfire.m_RockRingCollider != null)
                sb.AppendLine($"  stone ring collider {campfire.m_RockRingCollider.GetIl2CppType().Name}: {Box(root, campfire.m_RockRingCollider.bounds)}");
            if (fi.m_CookingSlots != null)
                foreach (var slot in fi.m_CookingSlots)
                    if (slot != null) sb.AppendLine($"  cooking slot '{slot.name}' at local {FireZone.V(root.InverseTransformPoint(slot.transform.position))}");
            foreach (var c in fi.GetComponentsInChildren<Collider>(false))
                if (c != null && c.enabled)
                    sb.AppendLine($"  collider '{c.name}' {c.GetIl2CppType().Name}{(c.isTrigger ? " (trigger)" : "")} layer {LayerMask.LayerToName(c.gameObject.layer)}: {Box(root, c.bounds)}");
            foreach (var mf in fi.GetComponentsInChildren<MeshFilter>(false))
                if (mf != null && mf.sharedMesh != null)
                    sb.AppendLine($"  mesh '{mf.name}' ({mf.sharedMesh.name}): local centre {FireZone.V(root.InverseTransformPoint(mf.transform.TransformPoint(mf.sharedMesh.bounds.center)))}, size {FireZone.V(Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale))}");
            return sb.ToString();
        }

        private static string Box(Transform root, Bounds b) =>
            $"local centre {FireZone.V(root.InverseTransformPoint(b.center))}, world size {FireZone.V(b.size)}";

        private static string Path(Transform t)
        {
            var s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}
#endif
