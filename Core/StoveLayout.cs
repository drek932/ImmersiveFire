using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImmersiveFire
{
    /// <summary>
    /// The zones of a stove-like fire (metal stove, forge, two-burner stove, kitchen ranges, fireplaces) built from its
    /// own geometry, the same way as the hand-measured pot belly stove: the door (meshes under the door's ObjectAnim
    /// target, or children named "door"), which side is the front (the door's side, snapped to the nearest axis: the
    /// forge's doors face local +z, the stoves' -z), the body's size. A helper child object "ImmersiveFire frame" is
    /// turned so that its local -z is the front; all zones are in its axes.
    /// Measured: metal stove door (0.22, 0.76, -0.48) 0.88 x 0.64 m; forge doors (-0.08, 0.82 / 1.15, 0.31).
    /// </summary>
    internal sealed class StoveLayout
    {
        public Transform Frame;
        public Vector3 Door;            // door centre, in the frame (z < 0: in front)
        public float DoorHalfWidth, DoorHalfHeight;
        public float MinX, MaxX;        // the body across
        public float FrontZ, CenterZ;   // the body's front and middle along the frame's z
        public bool HasDoor;
        public bool Open;               // no door at all: an open hearth (fireplace)
        public bool Furnace;            // a small furnace built into something bigger (the ammo workbench): body = the furnace only
        public float Lowered;           // the furnace door zones this much lower (10% of the door height, user)
        public float Bottom = -0.3f;    // where the furnace's zones start: below the floor under the bench
        private string HowMore = "";
        public float Scale = 1f;        // per model, on top of the settings (forge: 5% bigger, user)
        public float ExtraBack;         // per model: sides and door opener reach this share further back (6-burner: 20%)
        public string How;

        private static readonly Dictionary<IntPtr, StoveLayout> cache = new();

        public static void Reset() => cache.Clear();

        public static StoveLayout Of(FireplaceInteraction fi)
        {
            if (cache.TryGetValue(fi.Pointer, out var known) && known.Frame != null) return known;
            var layout = Measure(fi);
            cache[fi.Pointer] = layout;
            Main.Log.Msg($"Stove layout of '{fi.gameObject.name}': {layout.How}");
            return layout;
        }

        private static StoveLayout Measure(FireplaceInteraction fi)
        {
            var root = fi.transform;
            // The whole model: the fire may sit on a child ("INTERACTIVE_StoveMetalA/WoodStove"), the body above it.
            var model = ModelRoot(root);
            var stove = fi.TryCast<WoodStove>();
            var anim = stove != null ? stove.m_ObjectAnim : null;

            // The door: what the door animation moves, else children named "door".
            var doorParts = new List<MeshFilter>();
            if (anim != null)
            {
                var hinge = anim.m_Target != null ? anim.m_Target.transform : anim.transform;
                foreach (var mf in hinge.GetComponentsInChildren<MeshFilter>(false)) if (Usable(mf)) doorParts.Add(mf);
            }
            if (doorParts.Count == 0)
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(false))
                    if (Usable(mf) && mf.name.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0) doorParts.Add(mf);
            bool hasDoor = doorParts.Count > 0 && anim != null;

            // Which side is the front, in the root's axes.
            // No door (a fireplace): the cooking spots stand in front of the fire, towards the room (F6 on
            // INTERACTIVE_FirePlace: logs at the pivot, spots at z +0.41; the fire's own offset pointed sideways).
            Vector3 doorLocal;
            if (doorParts.Count > 0) doorLocal = Centre(doorParts, root);
            else if (SpotsCentre(fi, root, out var spots)) doorLocal = spots;
            else if (fi.Fire != null) doorLocal = root.InverseTransformPoint(fi.Fire.transform.position);
            else doorLocal = new Vector3(0f, 0.6f, -0.4f);
            var facing = doorLocal;
            // The ammo workbench's furnace: a small door in the side of the bench, off its fire object's pivot -> it
            // faces along the door's own thin side, away from the bench's middle (F6 log: 0.33 x 0.33 x 0.06 door).
            bool furnace = model.GetComponentInChildren<AmmoWorkBench>(true) != null && doorParts.Count > 0;
            if (furnace)
            {
                var size = doorParts[0].sharedMesh.bounds.size;
                var thin = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
                facing = root.InverseTransformDirection(doorParts[0].transform.TransformDirection(thin));
                facing.y = 0f;
                if (Vector3.Dot(facing, doorLocal - root.InverseTransformPoint(MiddleOf(model))) < 0f) facing = -facing;
            }
            var front = Math.Abs(facing.z) >= Math.Abs(facing.x)
                ? new Vector3(0f, 0f, facing.z >= 0f ? 1f : -1f)
                : new Vector3(facing.x >= 0f ? 1f : -1f, 0f, 0f);

            var frameGo = new GameObject("ImmersiveFire frame");
            var frame = frameGo.transform;
            frame.SetParent(root, false);
            frame.localPosition = Vector3.zero;
            frame.localRotation = Quaternion.LookRotation(-front, Vector3.up); // the frame's -z is the front

            var l = new StoveLayout { Frame = frame, HasDoor = hasDoor };

            // The door in the frame.
            if (doorParts.Count > 0)
            {
                Bounds(doorParts, frame, out var dMin, out var dMax);
                l.Door = (dMin + dMax) / 2f;
                l.DoorHalfWidth = Math.Max(0.12f, (dMax.x - dMin.x) / 2f);
                l.DoorHalfHeight = Math.Max(0.1f, (dMax.y - dMin.y) / 2f);
            }
            else
            {
                l.Door = frame.InverseTransformPoint(root.TransformPoint(doorLocal));
                l.DoorHalfWidth = 0.35f;
                l.DoorHalfHeight = 0.25f;
            }

            // The body: everything but the door, up to 2.5 m high (chimney pipes left out).
            var body = new List<MeshFilter>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(false))
                if (Usable(mf) && !doorParts.Contains(mf)) body.Add(mf);
            if (body.Count > 0 && Bounds(body, frame, out var bMin, out var bMax, 2.5f))
            {
                l.MinX = bMin.x;
                l.MaxX = bMax.x;
                l.FrontZ = doorParts.Count > 0 ? Math.Min(bMin.z, l.Door.z) : bMin.z;
                l.CenterZ = (bMin.z + bMax.z) / 2f;
                l.Bottom = Math.Min(-0.3f, bMin.y - 0.3f);
                if (doorParts.Count == 0)
                {
                    // An open hearth: the pot belly's pattern made bigger. Its "door" is the front of the fire (the log
                    // pile), as wide as the pile with a margin, at least a metre; the hearth around it is part of the house.
                    l.Open = true;
                    l.Door = new Vector3((bMin.x + bMax.x) / 2f, Mathf.Clamp(bMax.y * 0.8f, 0.25f, 0.5f), bMin.z);
                    l.DoorHalfWidth = Math.Max(0.5f, (bMax.x - bMin.x) / 2f + 0.1f);
                    l.DoorHalfHeight = 0.3f;
                }
            }
            else
            {
                l.MinX = l.Door.x - 0.4f;
                l.MaxX = l.Door.x + 0.4f;
                l.FrontZ = l.Door.z;
                l.CenterZ = l.Door.z + 0.3f;
            }

            if (furnace)
            {
                // Only the furnace counts as the body, not the whole bench.
                l.Furnace = true;
                l.MinX = l.Door.x - l.DoorHalfWidth - 0.1f;
                l.MaxX = l.Door.x + l.DoorHalfWidth + 0.1f;
                l.FrontZ = l.Door.z;
                l.CenterZ = l.Door.z + 0.3f;
                // Zones 10% of the door's height lower and down to below the floor under the bench (user).
                l.Lowered = 0.1f * l.DoorHalfHeight * 2f;
                l.Door.y -= l.Lowered;
                l.HowMore = $", every zone down to {l.Bottom:0.00} (below the floor)";
            }

            // Per model, asked for in the game.
            string name = model.name;
            if (name.StartsWith("INTERACTIVE_StoveMetalA", StringComparison.OrdinalIgnoreCase)) l.ExtraBack = 0.2f;
            if (name.StartsWith("INTERACTIVE_Forge", StringComparison.OrdinalIgnoreCase)) l.Scale = 1.05f;

            l.How = $"front = root {FireZone.V(front)}, door {(hasDoor ? "animated" : doorParts.Count > 0 ? "not animated" : "none (open hearth)")} " +
                    $"at {FireZone.V(l.Door)} size {l.DoorHalfWidth * 2f:0.00} x {l.DoorHalfHeight * 2f:0.00} m, " +
                    $"body ('{model.name}'{(furnace ? ", its furnace only" : "")}) x {l.MinX:0.00}..{l.MaxX:0.00}, front z {l.FrontZ:0.00}, middle z {l.CenterZ:0.00}" +
                    (l.Scale != 1f ? $", zones x{l.Scale:0.00}" : "") + (l.ExtraBack > 0f ? $", {l.ExtraBack:P0} further back" : "") +
                    (l.Lowered > 0f ? $", {l.Lowered:0.00} m lower" : "") + l.HowMore;
            return l;
        }

        // --- the zones (all scaled by the stove zones setting, the funnel by its own) ---

        private float ZoneScale => Main.Settings.StoveZoneScale * Scale;

        /// <summary>
        /// Lying fuel: the door opening and a bit of firebox behind it, out to 0.33 m in front (a log against it). An open
        /// hearth: the log pile itself, from just in front of it (short of the cooking spots) to past its back.
        /// </summary>
        public FireZone Firebox() => Open
            ? FireZone.ScaledBox(Frame,
                new Vector3(Door.x - DoorHalfWidth, Door.y - DoorHalfHeight, Door.z - 0.12f),
                new Vector3(Door.x + DoorHalfWidth, Door.y + DoorHalfHeight, Door.z + 0.55f),
                ZoneScale, "hearth fire")
            : FireZone.ScaledBox(Frame,
                new Vector3(Door.x - DoorHalfWidth, Furnace ? Bottom : Door.y - DoorHalfHeight, Door.z - 0.33f),
                new Vector3(Door.x + DoorHalfWidth, Door.y + DoorHalfHeight, Door.z + 0.4f),
                ZoneScale, "stove door and firebox");

        /// <summary>
        /// Fuel on the move: from the door forwards and down to the floor, as the pot belly's but flat and wide so it is
        /// hard to miss (55 degrees to the sides, 40 up and down) and from the whole width of a wide door. An open hearth
        /// reaches further (1.3 m).
        /// </summary>
        public FireZone Funnel() => FireZone.Funnel(Frame, new Vector3(Door.x, Door.y, Door.z - 0.02f),
            new Vector3(0f, -0.6f, -1f), 40f, 55f, Math.Max(0f, DoorHalfWidth - 0.15f),
            (Open ? 1.3f : 1.0f) * Main.Settings.FunnelScale * Scale, Open ? "hearth funnel" : "stove funnel");

        /// <summary>Beside the body, left and right (1.1 m out), from 0.3 m in front of it to 0.2 m past its middle; not behind.</summary>
        public FireZone[] Sides()
        {
            float top = Math.Max(1.2f, Door.y + DoorHalfHeight + 0.4f);
            // An open hearth's sides 10% bigger (user, at a fireplace).
            float s = ZoneScale * (Open ? 1.1f : 1f);
            float z0 = FrontZ - 0.3f, z1 = CenterZ + 0.2f;
            z1 += (z1 - z0) * ExtraBack;
            return new[]
            {
                FireZone.ScaledBox(Frame, new Vector3(MinX - 1.1f, Furnace ? Bottom : -0.3f, z0), new Vector3(MinX, top, z1), s, Furnace ? "furnace left side" : "stove left side"),
                FireZone.ScaledBox(Frame, new Vector3(MaxX, Furnace ? Bottom : -0.3f, z0), new Vector3(MaxX + 1.1f, top, z1), s, Furnace ? "furnace right side" : "stove right side"),
            };
        }

        /// <summary>Opens the door for fuel coming near: in front and along the sides, a metre around.</summary>
        public FireZone Opener()
        {
            if (!HasDoor) return null;
            float z0 = FrontZ - 1.6f, z1 = CenterZ + 0.4f;
            z1 += (z1 - z0) * ExtraBack;
            return FireZone.ScaledBox(Frame,
                new Vector3(MinX - 1.0f, Furnace ? Bottom : -0.3f, z0),
                new Vector3(MaxX + 1.0f, Math.Max(1.7f, Door.y + DoorHalfHeight + 0.9f), z1),
                ZoneScale, "stove door opener");
        }

        /// <summary>The middle of the fire's cooking spots, in <paramref name="space"/>; false when it has none.</summary>
        private static bool SpotsCentre(FireplaceInteraction fi, Transform space, out Vector3 centre)
        {
            centre = Vector3.zero;
            var slots = fi.m_CookingSlots;
            if (slots == null || slots.Length == 0) return false;
            int n = 0;
            foreach (var s in slots)
            {
                if (s == null) continue;
                centre += space.InverseTransformPoint(s.transform.position);
                n++;
            }
            if (n == 0) return false;
            centre /= n;
            return true;
        }

        /// <summary>The middle of a model's visible meshes (world).</summary>
        private static Vector3 MiddleOf(Transform model)
        {
            bool any = false;
            var box = new Bounds();
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (r == null || !r.enabled) continue;
                if (!any) { box = r.bounds; any = true; }
                else box.Encapsulate(r.bounds);
            }
            return any ? box.center : model.position;
        }

        /// <summary>The model's top object: up through parents named like interactive objects ("INTERACTIVE_...").</summary>
        internal static Transform ModelRoot(Transform t)
        {
            var top = t;
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name.StartsWith("INTERACTIVE_", StringComparison.OrdinalIgnoreCase)) top = p;
            return top;
        }

        // --- geometry ---

        private static bool Usable(MeshFilter mf)
        {
            if (mf == null || mf.sharedMesh == null) return false;
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled) return false;
            string name = mf.name;
            // Cooking spots are flat cylinders on top; lower details repeat the model.
            if (name.StartsWith("Cylinder", StringComparison.OrdinalIgnoreCase)) return false;
            int lod = name.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
            return lod < 0 || name.Substring(lod).StartsWith("_LOD0", StringComparison.OrdinalIgnoreCase);
        }

        private static Vector3 Centre(List<MeshFilter> parts, Transform space)
        {
            Bounds(parts, space, out var min, out var max);
            return (min + max) / 2f;
        }

        private static bool Bounds(List<MeshFilter> parts, Transform space, out Vector3 min, out Vector3 max, float maxHeight = float.MaxValue)
        {
            bool any = false;
            min = max = Vector3.zero;
            foreach (var mf in parts)
            {
                var b = mf.sharedMesh.bounds;
                if (b.size.y * Math.Abs(mf.transform.lossyScale.y) > maxHeight) continue;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = space.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { min = max = p; any = true; }
                    else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                }
            }
            return any;
        }
    }
}
