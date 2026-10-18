using System;
using System.Collections.Generic;
using System.Text;
using ImmersiveShared;
using UnityEngine;

namespace ImmersiveFire
{
    /// <summary>
    /// The way out of a firewood bin for fuel that flies from it into a fire, searched against the bin's real shape:
    /// its colliders (walls, floor, the fixed part of its roof and the open lid where it stands now), with a box the size
    /// of the item, turned as it flies. A path search (A*) over a 5 cm grid around the bin, from the item lifted off what
    /// it lies on to past the bin's wall on the fire's side, then straightened. Other logs in the bin don't count (the
    /// item may pass through them in there). The item tries as it lies, then turned along the lid's hinge, then upright
    /// (narrowest), turning on a short lift where it lies. Falls back to the bin's boxes (ExitByBoxes) if no way is found.
    /// </summary>
    internal static partial class FirewoodBins
    {
        private const float Lift = 0.05f;           // m the item is lifted off what it lies on before it moves
        private const float Margin = 0.03f;         // m kept from the bin's walls, roof and lid on the way out
        private const float Cell = 0.05f;           // m, the search grid
        private const float NearStart = 0.15f;      // m around the start where the item may be as close as it lay (no margin)
        private const int MaxExpansions = 4000;     // grid cells searched per turn of the item at most

        /// <summary>The way out of a bin: its points (the first is the item lifted off), the turn the item takes on the way.</summary>
        public sealed class ExitPlan
        {
            public List<Vector3> Points;
            public Quaternion Rotation;     // of the item's box on its way out
            public int TurnPoints;          // the item turns to Rotation by the end of this many points (0: it keeps its turn)
            public bool Found;              // a clear way was found (else the rough way by the bin's boxes)
        }

        private static string lastHit;
        private static readonly HashSet<IntPtr> profiled = new();

        /// <summary>
        /// The way out of the bin an item lies in, towards <paramref name="toward"/> (the fire), or null when not in a bin.
        /// The item: its box (<paramref name="centre"/>, <paramref name="half"/> in its own axes, <paramref name="rotation"/>).
        /// </summary>
        public static ExitPlan ExitPath(Vector3 centre, Quaternion rotation, Vector3 half, Vector3 toward)
        {
            var bin = Holding(centre);
            if (bin == null) return null;
            var why = new StringBuilder();
            try
            {
                Physics.SyncTransforms();   // the lid's collider where the lid stands now
                Profile(bin);
                var root = bin.Root;
                var dir = root.InverseTransformDirection(toward - centre);
                dir.y = 0f;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.back;
                var start = centre + root.up * Lift;

                var turns = new List<(string name, Quaternion q)> { ("as it lies", rotation) };
                var along = AlongHinge(bin, rotation, half);
                if (Quaternion.Angle(along, rotation) > 10f) turns.Add(("along the hinge", along));
                turns.Add(("upright", Upright(rotation, half)));

                foreach (var (name, q) in turns)
                {
                    var from = start;
                    int turnPoints = 0;
                    if (Quaternion.Angle(q, rotation) > 1f)
                    {
                        // Turned on a short lift where it lies (as much higher as it gets taller turned).
                        float rise = Math.Max(0.12f, Extent(root.up, q, half) - Extent(root.up, rotation, half) + 0.05f);
                        from = start + root.up * rise;
                        if (!ClearAlong(bin, start, from, from, rotation, q, Vector3.Max(half - Vector3.one * Margin, half * 0.5f)))
                        {
                            why.Append($"; {name}: no room to turn ({lastHit})");
                            continue;
                        }
                        turnPoints = 2;
                    }
                    var path = Search(bin, from, q, half, dir, out string result);
                    if (path == null)
                    {
                        why.Append($"; {name}: {result}");
                        continue;
                    }
                    var points = new List<Vector3> { start };
                    if (turnPoints > 0) points.Add(from);
                    points.AddRange(path);
                    Verbose.Log($"[Bin] Way out {name}: {result}, {points.Count} points{why}");
                    return new ExitPlan { Points = points, Rotation = q, TurnPoints = turnPoints, Found = true };
                }
            }
            catch (Exception e)
            {
                Once.Warn("bin:exit", "Firewood bin way out: " + e);
            }
            // Lying on the bin's roof, not in it (as high as its top): it needs no way out, it flies from where it is.
            if (bin.Root.InverseTransformPoint(centre).y >= bin.BodyMax.y - 0.1f)
            {
                Verbose.Log($"[Bin] {FireZone.V(bin.Root.InverseTransformPoint(centre))} is on top of the bin: no way out needed");
                return new ExitPlan { Points = new List<Vector3>(), Rotation = rotation, Found = true };
            }
            Main.Log.Msg($"[Bin] No clear way out (item half {FireZone.V(half)}){why}: the way by the bin's boxes");
            var worldHalf = new Vector3(Extent(Vector3.right, rotation, half), Extent(Vector3.up, rotation, half), Extent(Vector3.forward, rotation, half));
            return new ExitPlan { Points = ExitByBoxes(bin, centre, toward, worldHalf), Rotation = rotation };
        }

        /// <summary>The item turned (the least) so that its longest side lies along the lid's hinge, flat.</summary>
        private static Quaternion AlongHinge(Bin bin, Quaternion rotation, Vector3 half)
        {
            var longest = rotation * Longest(half);
            var hinge = bin.Root.TransformDirection(bin.HingeAxis);
            return Quaternion.FromToRotation(longest, Vector3.Dot(longest, hinge) >= 0f ? hinge : -hinge) * rotation;
        }

        /// <summary>The item turned (the least) so that its longest side stands up: the narrowest it gets from above.</summary>
        private static Quaternion Upright(Quaternion rotation, Vector3 half)
        {
            var longest = rotation * Longest(half);
            return Quaternion.FromToRotation(longest, longest.y >= 0f ? Vector3.up : Vector3.down) * rotation;
        }

        private static Vector3 Longest(Vector3 half) =>
            half.x >= half.y && half.x >= half.z ? Vector3.right : half.y >= half.z ? Vector3.up : Vector3.forward;

        /// <summary>Half the size of a box (<paramref name="half"/> in its axes, turned by <paramref name="rotation"/>) along a direction.</summary>
        private static float Extent(Vector3 axis, Quaternion rotation, Vector3 half) =>
            Math.Abs(Vector3.Dot(axis, rotation * Vector3.right)) * half.x +
            Math.Abs(Vector3.Dot(axis, rotation * Vector3.up)) * half.y +
            Math.Abs(Vector3.Dot(axis, rotation * Vector3.forward)) * half.z;

        // --- the search ---

        private static readonly (int x, int y, int z)[] Steps =
        {
            (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1),
            (1, 0, 1), (1, 0, -1), (-1, 0, 1), (-1, 0, -1),
            (1, 1, 0), (-1, 1, 0), (0, 1, 1), (0, 1, -1),
        };

        private static long Key(int x, int y, int z) => ((long)(x + 4096) << 26) | ((long)(y + 4096) << 13) | (long)(z + 4096);

        /// <summary>
        /// A* over the grid in the bin's axes, from <paramref name="from"/> (world) to anywhere past the bin's body on the
        /// fire's side (<paramref name="dir"/>, flat, bin axes) by the item's size and the wall clearance. The way, straightened,
        /// as world points after the start; null if none (<paramref name="result"/> says why, or what was found).
        /// </summary>
        private static List<Vector3> Search(Bin bin, Vector3 from, Quaternion q, Vector3 half, Vector3 dir, out string result)
        {
            var root = bin.Root;
            float s = Cell / Safe(root.lossyScale.x);
            var o = root.InverseTransformPoint(from);
            var safe = half + Vector3.one * Margin;
            float along = Extent(root.TransformDirection(dir), q, half) / Safe(root.lossyScale.x);
            float hy = Extent(root.up, q, half) / Safe(root.lossyScale.y);
            var lo = new Vector3(bin.BodyMin.x - along - 0.6f, bin.InsideMin.y, bin.BodyMin.z - along - 0.6f);
            var hi = new Vector3(bin.BodyMax.x + along + 0.6f, Math.Max(bin.BodyMax.y, bin.HasLid ? bin.LidOpenMax.y : 0f) + hy + 0.5f,
                bin.BodyMax.z + along + 0.6f);
            var mid = (bin.BodyMin + bin.BodyMax) / 2f;
            float goal = Math.Abs(dir.x) * (bin.BodyMax.x - bin.BodyMin.x) / 2f + Math.Abs(dir.z) * (bin.BodyMax.z - bin.BodyMin.z) / 2f +
                         along + WallClearance;
            float rimY = bin.BodyMax.y + hy;

            Vector3 Local(int x, int y, int z) => o + new Vector3(x, y, z) * s;
            float ToGo(Vector3 l) => goal - ((l.x - mid.x) * dir.x + (l.z - mid.z) * dir.z);
            float Guess(Vector3 l) => Math.Max(0f, ToGo(l)) + 0.5f * Math.Max(0f, rimY - l.y);

            var free = new Dictionary<long, bool>();
            bool Free(int x, int y, int z, long key)
            {
                if (free.TryGetValue(key, out bool known)) return known;
                var l = Local(x, y, z);
                bool ok = l.x >= lo.x && l.x <= hi.x && l.y >= lo.y && l.y <= hi.y && l.z >= lo.z && l.z <= hi.z &&
                          !OverlapsBin(bin, root.TransformPoint(l), Near(l, o) ? half : safe, q);
                free[key] = ok;
                return ok;
            }

            var open = new PriorityQueue<(int x, int y, int z), float>();
            var cost = new Dictionary<long, float>();
            var came = new Dictionary<long, long>();
            var cells = new Dictionary<long, (int x, int y, int z)>();
            long startKey = Key(0, 0, 0);
            cost[startKey] = 0f;
            cells[startKey] = (0, 0, 0);
            open.Enqueue((0, 0, 0), Guess(o));
            var done = new HashSet<long>();
            int expanded = 0;
            long found = -1;
            while (open.Count > 0 && expanded < MaxExpansions)
            {
                var c = open.Dequeue();
                long ck = Key(c.x, c.y, c.z);
                if (!done.Add(ck)) continue;   // queued again on a shorter way, already gone through
                var cl = Local(c.x, c.y, c.z);
                expanded++;
                if (ToGo(cl) <= 0f)
                {
                    found = ck;
                    break;
                }
                float g = cost[ck];
                foreach (var (dx, dy, dz) in Steps)
                {
                    int nx = c.x + dx, ny = c.y + dy, nz = c.z + dz;
                    long nk = Key(nx, ny, nz);
                    if (!Free(nx, ny, nz, nk)) continue;
                    float ng = g + s * (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (cost.TryGetValue(nk, out float old) && old <= ng) continue;
                    cost[nk] = ng;
                    came[nk] = ck;
                    cells[nk] = (nx, ny, nz);
                    open.Enqueue((nx, ny, nz), ng + Guess(Local(nx, ny, nz)));
                }
            }
            if (found < 0)
            {
                result = $"no way in {expanded} cells ({free.Count} looked at; {lastHit})";
                return null;
            }

            // The cells back to the start, then straightened: from each point as far along as a sweep is clear.
            var chain = new List<Vector3>();
            for (long k = found; ; k = came[k])
            {
                var (x, y, z) = cells[k];
                chain.Add(root.TransformPoint(Local(x, y, z)));
                if (k == startKey) break;
            }
            chain.Reverse();
            var path = new List<Vector3>();
            int at = 0;
            while (at < chain.Count - 1)
            {
                int next = chain.Count - 1;
                while (next > at + 1 && !ClearSweep(bin, chain[at], chain[next], at == 0, half, safe, q, o)) next--;
                path.Add(chain[next]);
                at = next;
            }
            result = $"{expanded} cells searched, {chain.Count} on the way, {path.Count} straight legs";
            return path;
        }

        private static bool Near(Vector3 l, Vector3 start) => (l - start).sqrMagnitude < NearStart * NearStart;

        /// <summary>A straight leg of the way is clear of the bin (the item's box as close as it lay near the start).</summary>
        private static bool ClearSweep(Bin bin, Vector3 a, Vector3 b, bool fromStart, Vector3 half, Vector3 safe, Quaternion q, Vector3 start)
        {
            var root = bin.Root;
            bool near = Near(root.InverseTransformPoint(a), start) || Near(root.InverseTransformPoint(b), start);
            return !HitsBin(bin, a, b, near ? half : safe, q, fromStart);
        }

        /// <summary>
        /// The item's box gets from <paramref name="start"/> by <paramref name="at"/> to <paramref name="end"/> turning from
        /// <paramref name="from"/> to <paramref name="to"/> without touching the bin (looked at every 4 cm; where it starts
        /// doesn't count: it may lie against a wall).
        /// </summary>
        private static bool ClearAlong(Bin bin, Vector3 start, Vector3 at, Vector3 end, Quaternion from, Quaternion to, Vector3 half)
        {
            float first = Vector3.Distance(start, at), total = first + Vector3.Distance(at, end);
            int steps = Math.Max(1, Mathf.CeilToInt(total / 0.04f));
            for (int i = 1; i <= steps; i++)
            {
                float d = total * i / steps;
                var point = d <= first ? Vector3.Lerp(start, at, first > 0f ? d / first : 1f) : Vector3.Lerp(at, end, (d - first) / Math.Max(0.0001f, total - first));
                if (OverlapsBin(bin, point, half, Quaternion.Slerp(from, to, (float)i / steps))) return false;
            }
            return true;
        }

        /// <summary>
        /// Something of the bin (its own colliders: walls, roof, lid) is in the way of the item's box moving from
        /// <paramref name="from"/> to <paramref name="to"/>. <paramref name="startClear"/>: what it already touches where it
        /// starts doesn't count (what it lies on, a wall it leans on).
        /// </summary>
        private static bool HitsBin(Bin bin, Vector3 from, Vector3 to, Vector3 half, Quaternion rotation, bool startClear)
        {
            var d = to - from;
            float distance = d.magnitude;
            if (distance < 0.001f) return false;
            var hits = Physics.BoxCastAll(from, half, d / distance, rotation, distance, bin.Mask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider == null || !hit.collider.transform.IsChildOf(bin.Root)) continue;
                if (startClear && hit.distance <= 0f) continue;
                lastHit = $"'{hit.collider.name}' at {FireZone.V(bin.Root.InverseTransformPoint(hit.point))}";
                return true;
            }
            return false;
        }

        /// <summary>The item's box at <paramref name="at"/> touches something of the bin.</summary>
        private static bool OverlapsBin(Bin bin, Vector3 at, Vector3 half, Quaternion rotation)
        {
            foreach (var c in Physics.OverlapBox(at, half, rotation, bin.Mask, QueryTriggerInteraction.Ignore))
                if (c != null && c.transform.IsChildOf(bin.Root))
                {
                    lastHit = $"'{c.name}' touched at {FireZone.V(bin.Root.InverseTransformPoint(at))}";
                    return true;
                }
            return false;
        }

        /// <summary>
        /// Once per bin, in the log: its cross-sections (x = 0 and x = 0.6 in its axes), the heights where its colliders
        /// are along lines straight down every 5 cm front to back — its real shape inside, with the lid open.
        /// </summary>
        private static void Profile(Bin bin)
        {
            if (!profiled.Add(bin.Root.gameObject.Pointer)) return;
            var root = bin.Root;
            var sb = new StringBuilder($"[Bin] Shape (along vertical lines: v = faces up met going down, ^ = faces down met going up; L = lid), " +
                                       $"lid {(bin.T >= 1f ? "open" : "not open")}:");
            foreach (float x in new[] { 0f, 0.6f })
            {
                sb.Append($"\n  x {x:0.0}:");
                for (float z = bin.BodyMin.z - 0.05f; z <= bin.BodyMax.z + 0.051f; z += 0.05f)
                {
                    sb.Append($" z{z:0.00}[v");
                    March(bin, root.TransformPoint(new Vector3(x, 3f, z)), -root.up, sb);
                    sb.Append(" ^");
                    March(bin, root.TransformPoint(new Vector3(x, -0.2f, z)), root.up, sb);
                    sb.Append(']');
                }
            }
            Main.Log.Msg(sb.ToString());
        }

        /// <summary>The heights (bin axes) where a line from <paramref name="origin"/> meets the bin's faces, one after another.</summary>
        private static void March(Bin bin, Vector3 origin, Vector3 dir, StringBuilder sb)
        {
            var root = bin.Root;
            float left = 3.4f;
            for (int i = 0; i < 10 && left > 0f; i++)
            {
                if (!Physics.Raycast(origin, dir, out var hit, left, bin.Mask, QueryTriggerInteraction.Ignore)) break;
                if (hit.collider != null && hit.collider.transform.IsChildOf(root))
                {
                    bool lid = bin.Lid != null && hit.collider.transform.IsChildOf(bin.Lid);
                    sb.Append(' ').Append(lid ? "L" : "").Append(root.InverseTransformPoint(hit.point).y.ToString("0.00"));
                }
                float step = hit.distance + 0.002f;
                origin += dir * step;
                left -= step;
            }
        }
    }
}
