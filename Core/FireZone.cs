using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace ImmersiveFire
{
    internal enum FireKind { Campfire, Fireplace, Stove, Forge }

    /// <summary>
    /// A zone in a fire's own frame (turning with it around the vertical axis only): an upright cylinder or a box.
    /// Campfire: inside the stone ring. Pot belly stove: its door opening and firebox (plus a bigger box in front that
    /// opens the door, see StoveDoors). Other fires (not measured yet): around the flames (the Fire object).
    /// </summary>
    internal sealed class FireZone
    {
        public readonly Transform Frame;
        public readonly Vector3 Offset;     // centre, in the frame's yaw axes
        public readonly float Radius;       // cylinder
        public readonly float HalfHeight;   // cylinder
        public readonly Vector3 Half;       // box (x right, y up, z forward of the frame)
        public readonly bool IsBox;
        public readonly bool IsCone;
        public readonly bool IsSphere;
        public readonly Vector3 Axis;      // cone: direction from the apex (Offset), in the frame's yaw axes
        public readonly float Length, CosAngle, Angle; // cone (Angle: up and down from the axis)
        public readonly float SideAngle;    // cone: to the sides (wider than Angle = a flat funnel)
        public readonly float Spread;       // cone: half width of its apex, a line across the frame's x (a wide door)
        public readonly string How;

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

        /// <summary>A ball around <paramref name="centre"/> (frame's yaw axes).</summary>
        public static FireZone Sphere(Transform frame, Vector3 centre, float radius, string how) =>
            new FireZone(frame, centre, radius, how);

        /// <summary>
        /// A cone from <paramref name="apex"/> along <paramref name="axis"/> (frame's yaw axes), <paramref name="angle"/>
        /// degrees from its axis, <paramref name="length"/> metres long: wide where the fuel comes from, narrow at the fire.
        /// </summary>
        public static FireZone Cone(Transform frame, Vector3 apex, Vector3 axis, float angle, float length, string how) =>
            new FireZone(frame, apex, axis, angle, length, angle, 0f, how);

        /// <summary>
        /// A flat funnel: <paramref name="sideAngle"/> to the sides, <paramref name="angle"/> up and down, from an apex that is a
        /// line <paramref name="spread"/> to each side of <paramref name="apex"/> (the frame's x; the axis must have no x).
        /// With equal angles and no spread it is the round cone.
        /// </summary>
        public static FireZone Funnel(Transform frame, Vector3 apex, Vector3 axis, float angle, float sideAngle, float spread, float length, string how) =>
            new FireZone(frame, apex, axis, angle, length, sideAngle, spread, how);

        /// <summary>A box from <paramref name="min"/> to <paramref name="max"/> in the frame's yaw axes.</summary>
        public static FireZone Box(Transform frame, Vector3 min, Vector3 max, string how) =>
            new FireZone(frame, (min + max) / 2f, (max - min) / 2f, how);

        /// <summary>The same box grown (or shrunk) by <paramref name="scale"/> around its centre.</summary>
        public static FireZone ScaledBox(Transform frame, Vector3 min, Vector3 max, float scale, string how)
        {
            var centre = (min + max) / 2f;
            var half = (max - min) / 2f * scale;
            return new FireZone(frame, centre, half, scale > 1f ? $"{how} x{scale:0.00}" : how);
        }

        public Quaternion Yaw => Quaternion.Euler(0f, Frame.eulerAngles.y, 0f);

        public Vector3 Center => Frame.position + Yaw * Offset;

        /// <summary>Radius of a sphere holding the whole zone (for the physics query).</summary>
        public float Reach => IsCone ? Length + Spread : IsSphere ? Radius : IsBox ? Half.magnitude : Mathf.Sqrt(Radius * Radius + HalfHeight * HalfHeight);

        public bool Contains(Vector3 world)
        {
            var p = Quaternion.Inverse(Yaw) * (world - Center);
            if (IsSphere) return p.sqrMagnitude <= Radius * Radius;
            if (IsCone)
            {
                // From the nearest point of the apex line.
                if (Spread > 0f) p.x = Math.Sign(p.x) * Math.Max(0f, Math.Abs(p.x) - Spread);
                float d = p.magnitude;
                if (d > Length) return false;
                if (d < 0.001f) return true;
                float along = Vector3.Dot(p, Axis);
                if (along <= 0f) return false;
                // An elliptic cross-section: side (frame x) and up (across the axis) measured separately.
                var up = Vector3.Cross(Axis, Vector3.right).normalized;
                float side = p.x / (along * Mathf.Tan(SideAngle * Mathf.Deg2Rad));
                float vertical = Vector3.Dot(p, up) / (along * Mathf.Tan(Angle * Mathf.Deg2Rad));
                return side * side + vertical * vertical <= 1f;
            }
            if (IsBox) return Math.Abs(p.x) <= Half.x && Math.Abs(p.y) <= Half.y && Math.Abs(p.z) <= Half.z;
            return p.x * p.x + p.z * p.z <= Radius * Radius && p.y >= -HalfHeight && p.y <= HalfHeight;
        }

        /// <summary>A point in the frame's yaw axes, from the frame's pivot (for calibration logs).</summary>
        public Vector3 Local(Vector3 world) => Quaternion.Inverse(Yaw) * (world - Frame.position);

        public override string ToString() => IsSphere
            ? $"{How}: ball around {V(Center)}, radius {Radius:0.00} m"
            : IsCone
            ? $"{How}: cone from {V(Center)}, axis {V(Axis)}, {Angle:0} deg" + (SideAngle != Angle ? $" ({SideAngle:0} to the sides)" : "") +
              (Spread > 0f ? $", apex {Spread * 2f:0.00} m wide" : "") + $", {Length:0.00} m"
            : IsBox
            ? $"{How}: box centre {V(Center)}, size {V(Half * 2f)}"
            : $"{How}: centre {V(Center)}, radius {Radius:0.00} m, height {HalfHeight * 2f:0.00} m";

        public static string V(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";

        // --- which fire is it ---

        public static FireKind KindOf(FireplaceInteraction fi)
        {
            if (fi.Forge != null) return FireKind.Forge;
            if (fi.TryCast<Campfire>() != null) return FireKind.Campfire;
            string name = fi.gameObject.name;
            if (name.IndexOf("fireplace", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("hearth", StringComparison.OrdinalIgnoreCase) >= 0)
                return FireKind.Fireplace;
            return FireKind.Stove;
        }

        // --- the zone ---

        // First guesses, to be measured in the game (Debug/Calibration, key F6).
        private const float CampfireFallbackRadius = 0.35f;
        // From the ground (the campfire's pivot, where the stones lie) up to above the log pile (0.59 m mesh): a torch
        // thrown in often ends up lying on top of the logs.
        private const float CampfireBelow = 0.05f, CampfireAbove = 0.8f;

        public static FireZone For(FireplaceInteraction fi, FireKind kind)
        {
            if (kind == FireKind.Campfire)
            {
                var campfire = fi.TryCast<Campfire>();
                var frame = campfire.transform;
                float s = Main.Settings.CampfireZoneScale;
                float radius = InnerRingRadius(campfire, out string how) * s;
                // Below the ground as measured; up to above the logs, scaled with the setting.
                float above = CampfireAbove * s;
                return new FireZone(frame, new Vector3(0f, (above - CampfireBelow) / 2f, 0f), radius,
                    (above + CampfireBelow) / 2f, s > 1f ? $"{how}, zones x{s:0.00}" : how);
            }

            if (IsPotBellyStove(fi))
                // The door opening and the firebox behind it (F6 log: door mesh centre (0, 0.74, -0.23), 0.32 x 0.31 m,
                // front = -z; the body is 0.64 m wide). A log held against the open door can't get its centre closer than
                // 0.47-0.49 m to the stove's axis (measured), so the zone reaches 0.58 m out, at door height.
                return ScaledBox(fi.transform, new Vector3(-0.2f, 0.55f, -0.58f), new Vector3(0.2f, 0.95f, 0.15f), Main.Settings.StoveZoneScale,
                    "pot belly stove door and firebox");

            if (IsBarrel(fi))
                // Inside the drum, under its grill (F6: drum 0.81 m round, -0.2..1.05 m; grill at 0.86 m).
                return new FireZone(fi.transform, new Vector3(0f, 0.6f, 0f), 0.3f * Main.Settings.StoveZoneScale, 0.25f,
                    "fire barrel inside");

            // Everything else (metal stove, forge, two-burner stove, ranges, fireplaces): built from its own shape.
            return StoveLayout.Of(fi).Firebox();
        }

        public static bool IsPotBellyStove(FireplaceInteraction fi) =>
            fi.gameObject.name.StartsWith("INTERACTIVE_PotBellyStove", StringComparison.OrdinalIgnoreCase);

        public static bool IsBarrel(FireplaceInteraction fi) =>
            fi.gameObject.name.StartsWith("INTERACTIVE_FireBarrel", StringComparison.OrdinalIgnoreCase);

        /// <summary>The rim of a fire barrel, from its pivot (the drum mesh reaches 1.05 m, the grill sits at 0.86 m).</summary>
        public const float BarrelRim = 1.0f;

        /// <summary>
        /// Where fuel on the move (held, carried in a group, thrown) is drawn into a fire: a funnel from the door, wide
        /// towards where the player brings it; a ball around a campfire or a fire barrel. Items lying still are never taken
        /// from here (a woodpile next to the fire must stay), only from the fire's own zone.
        /// </summary>
        public static FireZone Funnel(FireplaceInteraction fi)
        {
            var campfire = fi.TryCast<Campfire>();
            if (campfire != null)
                // A ball over the fire: fuel brought within about a metre (a log on the ground included) is drawn in.
                // 1.32 m: 1.1 m + 20% (user, 0.9.2), reaching logs lying lower than the campfire on uneven ground.
                return Sphere(campfire.transform, new Vector3(0f, 0.2f, 0f), 1.32f * Main.Settings.CampfireZoneScale, "campfire pull");
            if (IsBarrel(fi))
                // Brought within about a metre of the drum, from any side: tossed in over the rim.
                return Sphere(fi.transform, new Vector3(0f, 0.5f, 0f), 1.1f * Main.Settings.StoveZoneScale, "fire barrel pull");
            if (IsPotBellyStove(fi))
                // A held log lies on the floor in front of the stove (measured: y 0.05, z -0.6), it can't be held up at the
                // door: the funnel goes from the door (centre (0, 0.74, -0.23)) forwards and down to the floor.
                return Cone(fi.transform, new Vector3(0f, 0.74f, -0.25f), new Vector3(0f, -0.6f, -1f), 40f, 1.0f * Main.Settings.FunnelScale,
                    "pot belly stove funnel");
            return StoveLayout.Of(fi).Funnel();
        }

        /// <summary>
        /// Beside a stove, left and right (not behind): fuel on the move there is drawn in too, flying out in front and in
        /// through the door. Pot belly: boxes from 0.3 m off the stove's axis (its body is 0.32 m round) to 1.2 m, from the
        /// floor up, from 0.6 m in front to 0.2 m past the axis. Null for campfires and barrels.
        /// </summary>
        public static FireZone[] Sides(FireplaceInteraction fi)
        {
            if (fi.TryCast<Campfire>() != null || IsBarrel(fi)) return null;
            if (!IsPotBellyStove(fi)) return StoveLayout.Of(fi).Sides();
            float s = Main.Settings.StoveZoneScale;
            return new[]
            {
                ScaledBox(fi.transform, new Vector3(-1.2f, -0.3f, -0.6f), new Vector3(-0.3f, 1.2f, 0.2f), s, "pot belly stove left side"),
                ScaledBox(fi.transform, new Vector3(0.3f, -0.3f, -0.6f), new Vector3(1.2f, 1.2f, 0.2f), s, "pot belly stove right side"),
            };
        }

        /// <summary>
        /// The area that opens a stove's door when fuel comes near: a box in front of the door, several times the size
        /// of the opening. Null for fires without a door.
        /// </summary>
        public static FireZone DoorTrigger(FireplaceInteraction fi)
        {
            if (fi.TryCast<Campfire>() != null || IsBarrel(fi)) return null;
            if (!IsPotBellyStove(fi)) return StoveLayout.Of(fi).Opener();
            // Wide and deep enough to open the door while the fuel is still on its way (the player often comes from the
            // side: logs were seen 1.4-1.75 m to the side), along the stove's sides too.
            // From below the stove's pivot: the floor may lie lower (measured: logs on it at y -0.04, 0.7.2 missed them).
            return ScaledBox(fi.transform, new Vector3(-1.3f, -0.3f, -1.8f), new Vector3(1.3f, 1.7f, 0.4f), Main.Settings.StoveZoneScale,
                "pot belly stove door opener");
        }

        /// <summary>
        /// Free space inside the stones, seen from the campfire's centre. The ring is uneven (a few stones lie well
        /// inside, measured 0.29-0.33 m against 0.4-0.6 m for most), so the inner edges are taken at their lower quarter:
        /// the zone reaches the stones without swallowing items put on top of them.
        /// </summary>
        private static float InnerRingRadius(Campfire campfire, out string how)
        {
            var ring = campfire.m_RockRingParent;
            if (ring == null)
            {
                how = "campfire, no stone ring (default radius)";
                return CampfireFallbackRadius;
            }

            var centre = campfire.transform.position;
            var edges = new List<float>();
            foreach (var mf in ring.GetComponentsInChildren<MeshFilter>(false))
            {
                // Only the stones: the cooking spots (flat cylinders) sit under the ring too.
                if (mf == null || mf.sharedMesh == null || mf.name.IndexOf("rock", StringComparison.OrdinalIgnoreCase) < 0) continue;
                // Mesh size, not the renderer's world box: turned stones would look bigger than they are.
                var size = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
                var at = mf.transform.TransformPoint(mf.sharedMesh.bounds.center);
                float d = new Vector2(at.x - centre.x, at.z - centre.z).magnitude;
                edges.Add(d - (Math.Abs(size.x) + Math.Abs(size.z)) / 4f);
            }
            if (edges.Count == 0)
            {
                how = "campfire, stone ring without stones (default radius)";
                return CampfireFallbackRadius;
            }
            edges.Sort();
            float inner = edges[edges.Count / 4];
            how = $"campfire inside {edges.Count} stones (inner edges {edges[0]:0.00}-{edges[edges.Count - 1]:0.00} m, lower quarter {inner:0.00} m)";
            return Mathf.Clamp(inner, 0.2f, 0.6f);
        }
    }
}
