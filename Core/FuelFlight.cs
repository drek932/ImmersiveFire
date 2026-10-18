using System;
using System.Collections.Generic;
using Il2Cpp;
using ImmersiveShared;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImmersiveFire
{
    /// <summary>
    /// The fire pulls the fuel in: when fuel on the move goes into a fire, a look-alike of it (its meshes only: no
    /// physics, not a game item) flies into the fire along a smooth path through given points and shrinks a little,
    /// while the real item is used up at once by the game's rules.
    /// </summary>
    internal static class FuelFlight
    {
        private const float Seconds = 0.4f;         // per metre of path (0.32 .. 1.6 s); the pull speed setting slows it
        private const float EndScale = 0.6f;
        private const float DoorLead = 0.45f;       // s before the fuel reaches the stove's door that the door opens

        private sealed class Flight
        {
            public GameObject Copy;
            public Vector3[] Points;            // world, the item's place first
            public float[] Lengths;             // running length along the path at each point
            public Vector3 Scale;
            public Quaternion Rotation;
            public float T, Duration;
            public float Hold;                  // path length it keeps its turn and size for (out of a bin)
            public Quaternion TurnTo;           // the turn it takes on its way inside a bin (else its own)
            public float TurnLength;            // path length by which it has taken it
            public float DoorT;                 // share of the flight's time after which the target stove opens its door
            public IntPtr Target;               // the stove it flies into (its door opens on the way), or zero
            public GameObject SoundAt;          // the fire: the "add fuel" sound plays there on landing
        }

        private static readonly List<Flight> flights = new();

        public static void Reset()
        {
            foreach (var f in flights) if (f.Copy != null) Object.Destroy(f.Copy);
            flights.Clear();
        }

        /// <summary>
        /// A look-alike of the item flies from where it is (its middle) through <paramref name="points"/> (the last is the
        /// end), around anything in the way (except the item's own source and the fire itself, <paramref name="ignore"/>).
        /// <paramref name="target"/>: the stove it flies into, which opens its door on the way (or zero).
        /// <paramref name="exitPoints"/>: the first points are its way out of a firewood bin — followed straight, keeping
        /// its turn and size until out (it fitted in there as it lies).
        /// </summary>
        public static bool Start(GearItem gi, IList<Vector3> points, float speed, IntPtr target, GameObject soundAt, int freeEnd,
            int exitPoints, Quaternion? turnTo, int turnPoints, params Transform[] ignore)
        {
            try
            {
                var copy = LookAlike(gi);
                if (copy == null) return false;
                var path = new List<Vector3> { copy.transform.position };
                // Out of a bin: straight legs with points close together, so the curve through them follows them (no
                // sagging into the bin's wall). Nothing but the bin itself is in the way there. It may turn on the way
                // inside (by the end of turnPoints of them) to fit out past the open lid.
                exitPoints = Math.Min(exitPoints, points.Count);
                int turnEnd = -1;
                for (int i = 0; i < exitPoints; i++)
                {
                    Straight(path, points[i]);
                    if (i == turnPoints - 1) turnEnd = path.Count - 1;
                }
                int exitEnd = path.Count - 1;
                var rest = new List<Vector3> { path[exitEnd] };
                for (int i = exitPoints; i < points.Count; i++) rest.Add(points[i]);
                AroundObstacles(rest, gi.transform, freeEnd, ignore);
                for (int i = 1; i < rest.Count; i++) path.Add(rest[i]);

                var all = path.ToArray();
                var lengths = new float[all.Length];
                for (int i = 1; i < all.Length; i++) lengths[i] = lengths[i - 1] + Vector3.Distance(all[i - 1], all[i]);
                // About 0.4 s a metre: a short hop a little less, a long way across a room up to 1.6 s.
                float total = lengths[all.Length - 1];
                float duration = Seconds * Mathf.Clamp(total, 0.8f, 4f) / Math.Max(0.1f, speed);
                // The door opens DoorLead before the fuel gets to it (the start of the free legs: lined up in front of it).
                float atDoor = lengths[Math.Max(0, all.Length - 1 - freeEnd)];
                float doorTime = 1f;
                for (int k = 0; k <= 200; k++)
                    if (Ease(k / 200f) * total >= atDoor) { doorTime = k / 200f; break; }
                flights.Add(new Flight
                {
                    Copy = copy, Points = all, Lengths = lengths, Duration = duration, Target = target, SoundAt = soundAt,
                    Scale = copy.transform.localScale, Rotation = copy.transform.rotation, Hold = lengths[exitEnd],
                    TurnTo = turnTo ?? copy.transform.rotation, TurnLength = turnEnd > 0 ? lengths[turnEnd] : 0f,
                    DoorT = Math.Max(0f, doorTime - DoorLead / duration),
                });
                Verbose.Log($"Flight of {gi.name}: {total:0.00} m in {duration:0.00} s" + (exitPoints > 0 ? $", out of a bin in {lengths[exitEnd]:0.00} m" : "") +
                            (target != IntPtr.Zero ? $", at the door after {doorTime * duration:0.00} s, door opens at {Math.Max(0f, doorTime - DoorLead / duration) * duration:0.00} s" : ""));
                return true;
            }
            catch (Exception e)
            {
                Once.Warn("flight", "The fuel's flight into the fire failed: " + e);
                return false;
            }
        }

        /// <summary>Fuel is on its way into this stove, DoorLead or less from its door: time to open the door.</summary>
        public static bool Approaching(IntPtr stove)
        {
            foreach (var f in flights)
                if (f.Copy != null && f.Target == stove && f.T >= f.DoorT) return true;
            return false;
        }

        /// <summary>The share of the path covered at the share <paramref name="T"/> of the time: eases out of its place, faster towards the end (drawn in).</summary>
        private static float Ease(float T) => Mathf.Lerp(T * T * (3f - 2f * T), T * T, 0.4f);

        private static int obstacleMask = -1;

        /// <summary>
        /// Each leg of the path is checked with a line cast; where something solid is in the way (furniture, a stove's
        /// corner, a bin's wall) a point is added above it (its top + 0.25 m), and the new legs are checked again.
        /// The last <paramref name="freeEnd"/> legs (into the fire: through a door) are not checked: the fire itself is the end.
        /// </summary>
        private static void AroundObstacles(List<Vector3> path, Transform item, int freeEnd, Transform[] ignore)
        {
            if (obstacleMask < 0)
            {
                obstacleMask = Physics.DefaultRaycastLayers & ~(1 << LayerMask.NameToLayer("Gear"));
                int player = LayerMask.NameToLayer("Player");
                if (player >= 0) obstacleMask &= ~(1 << player);
            }
            int added = 0;
            for (int i = 0; i < path.Count - 1 - freeEnd && added < 6; i++)
            {
                if (!Physics.Linecast(path[i], path[i + 1], out var hit, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
                var t = hit.collider.transform;
                if (t.IsChildOf(item) || Ignored(t, ignore)) continue;
                if (hit.distance < 0.05f) continue; // touching where it starts (the floor it lies on)
                // Over its top — but a floor, the ground or a wall is huge: then just 0.3 m over where it was hit.
                var b = hit.collider.bounds;
                float top = b.size.magnitude > 6f ? hit.point.y + 0.3f : b.max.y + 0.25f;
                float over = Math.Max(Math.Max(path[i].y, path[i + 1].y), top);
                var detour = new Vector3(hit.point.x, over, hit.point.z);
                path.Insert(i + 1, detour);
                added++;
                i--; // check the leg up to the new point again
                Verbose.Log($"Flight goes over '{hit.collider.name}' at {FireZone.V(detour)}");
            }
        }

        /// <summary>A straight leg from the path's last point to <paramref name="to"/>, as points 10 cm apart.</summary>
        private static void Straight(List<Vector3> path, Vector3 to)
        {
            var from = path[path.Count - 1];
            int n = Math.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / 0.1f));
            for (int k = 1; k <= n; k++) path.Add(Vector3.Lerp(from, to, (float)k / n));
        }

        private static bool Ignored(Transform t, Transform[] ignore)
        {
            if (ignore == null) return false;
            foreach (var root in ignore)
                if (root != null && t.IsChildOf(root)) return true;
            return false;
        }

        public static void Update()
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                if (f.Copy == null)
                {
                    flights.RemoveAt(i);
                    continue;
                }
                f.T = Math.Min(1f, f.T + Time.deltaTime / f.Duration);
                float t = Ease(f.T);
                f.Copy.transform.position = At(f, t);
                // Turns and shrinks only once out of a bin (Hold): turning in there would swing it through a wall.
                float total = f.Lengths[f.Lengths.Length - 1];
                float r = f.Hold <= 0f ? t : total - f.Hold < 0.001f ? 1f : Mathf.Clamp01((t * total - f.Hold) / (total - f.Hold));
                // Inside a bin it may first turn to fit out past the open lid (TurnTo, by TurnLength).
                var turned = f.TurnLength > 0f ? Quaternion.Slerp(f.Rotation, f.TurnTo, Mathf.Clamp01(t * total / f.TurnLength)) : f.TurnTo;
                f.Copy.transform.rotation = turned * Quaternion.Euler(0f, 90f * r, 0f);
                f.Copy.transform.localScale = f.Scale * Mathf.Lerp(1f, EndScale, r);
                if (f.T < 1f) continue;
                FuelFeeder.FeedSound(f.SoundAt); // landed in the fire
                Object.Destroy(f.Copy);
                flights.RemoveAt(i);
            }
        }

        /// <summary>The point at <paramref name="t"/> (0..1) of the path's length, on a Catmull-Rom curve through its points.</summary>
        private static Vector3 At(Flight f, float t)
        {
            var p = f.Points;
            float total = f.Lengths[p.Length - 1];
            if (p.Length < 2 || total < 0.0001f) return p[p.Length - 1];
            float along = t * total;
            int seg = 0;
            while (seg < p.Length - 2 && f.Lengths[seg + 1] < along) seg++;
            float segLength = f.Lengths[seg + 1] - f.Lengths[seg];
            float u = segLength > 0.0001f ? (along - f.Lengths[seg]) / segLength : 1f;
            var p0 = p[Math.Max(seg - 1, 0)];
            var p1 = p[seg];
            var p2 = p[seg + 1];
            var p3 = p[Math.Min(seg + 2, p.Length - 1)];
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
        }

        /// <summary>
        /// The item's visible meshes (the most detailed LOD only) under a new object at the item's middle (the box of its
        /// meshes: pivots may sit at one end), so the path is its middle's and it turns about its middle.
        /// </summary>
        private static GameObject LookAlike(GearItem gi)
        {
            var t = gi.transform;
            var shown = new List<MeshFilter>();
            var box = new Bounds();
            foreach (var mf in gi.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                // Lower details of the same model would draw on top of it.
                string name = mf.name;
                int lod = name.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
                if (lod >= 0 && !name.Substring(lod).StartsWith("_LOD0", StringComparison.OrdinalIgnoreCase)) continue;
                if (shown.Count == 0) box = mr.bounds;
                else box.Encapsulate(mr.bounds);
                shown.Add(mf);
            }
            if (shown.Count == 0) return null;

            var root = new GameObject("ImmersiveFire flying fuel");
            root.transform.SetPositionAndRotation(box.center, t.rotation);
            root.transform.localScale = t.lossyScale;
            int parts = 0;
            foreach (var mf in shown)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                string name = mf.name;
                var part = new GameObject(name);
                part.transform.SetParent(root.transform, false);
                part.transform.position = mf.transform.position;
                part.transform.rotation = mf.transform.rotation;
                var s = mf.transform.lossyScale;
                var rs = t.lossyScale;
                part.transform.localScale = new Vector3(s.x / Safe(rs.x), s.y / Safe(rs.y), s.z / Safe(rs.z));
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
                parts++;
            }
            if (parts > 0) return root;
            Object.Destroy(root);
            return null;
        }

        private static float Safe(float v) => Math.Abs(v) < 0.0001f ? 1f : v;
    }
}
