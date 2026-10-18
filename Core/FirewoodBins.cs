using System;
using System.Collections.Generic;
using System.Text;
using Il2Cpp;
using Il2CppTLD.Interactions;
using ImmersiveShared;
using UnityEngine;

namespace ImmersiveFire
{
    /// <summary>
    /// The firewood bin, a piece of furniture (FirewoodBins.Furniture): not a container — its lid opens all the way
    /// (110 degrees, around the axis of the game's own lid tween) and closes from its interactions, and its colliders are
    /// the model's own hollow shape, so logs can be put inside by hand. Auto-feed takes fuel from bins near a fire: the
    /// lid opens by itself first and closes again after.
    /// </summary>
    internal static partial class FirewoodBins
    {
        private const float WideAngle = 110f;
        private const float AutoCloseAfter = 1.5f;
        private const float OpenSlower = 1.5f;      // opening takes this many times as long as closing
        private const float PauseWhenOpen = 0.25f;  // s the lid stays open before the fuel comes out
        private const float RimClearance = 0.15f;   // m between the rim and the bottom of fuel going over it
        private const float WallClearance = 0.35f;  // m between the wall and fuel going out over it before it turns down
        private const float LidClearance = 0.08f;   // m between the open lid and fuel rising past it

        private sealed class Bin
        {
            public Transform Root, Lid;
            public Quaternion Closed, Wide;
            public float T;                 // 0 closed .. 1 open
            public bool Want, AutoOpened;
            public float AutoUntil;
            public float OpenedAt = -1f;    // when the lid got all the way open (-1: not open)
            public Vector3 InsideMin, InsideMax; // in the root's axes
            public Vector3 BodyMin, BodyMax;     // the body's box, root axes
            public Vector3 OpenMin, OpenMax;     // the opening under the lid (x, z), root axes
            public Vector3 LidOpenMin, LidOpenMax; // the box of the open lid, root axes
            public bool HasLid;
            public Vector3 HingeAxis = Vector3.right;   // the lid turns about this (root axes, flat)
            public Vector3 HingeAt;                     // root axes
            public Vector3 ToLid;                       // from the hinge towards the closed lid's side (root axes, flat; zero: no lid)
            public int Mask;                     // the layers of its own colliders
            public readonly List<SimpleInteraction> Handles = new();
            public bool Active => Root != null && Root.gameObject.activeInHierarchy;
        }

        private static readonly Dictionary<IntPtr, Bin> bins = new();   // by the furniture's GameObject

        /// <summary>Bins gone (broken down, another scene) are forgotten.</summary>
        public static void Reset()
        {
            var gone = new List<IntPtr>();
            foreach (var kv in bins) if (kv.Value.Root == null) gone.Add(kv.Key);
            foreach (var key in gone) bins.Remove(key);
        }

        /// <summary>
        /// A new piece of our furniture (see FirewoodBins.Furniture) becomes a bin: its lid opens all the way around the
        /// axis of the game's own lid tween, from the interactions on the body and the lid. Measured (log of 0.13.0): base
        /// mesh OBJ_WoodBinBase 1.97 x 1.27 x 1.18, hinge at (0, 1.25, 0.08), lid OBJ_WoodBinLid 1.97 x 0.07 x 0.66.
        /// </summary>
        private static void Make(GameObject go)
        {
            if (go == null || bins.ContainsKey(go.Pointer)) return;
            var root = go.transform;
            var hinge = root.Find("Hinge");
            var bin = new Bin { Root = root, Lid = hinge };
            if (hinge != null)
            {
                bin.Closed = hinge.localRotation;
                bin.Wide = bin.Closed * Quaternion.AngleAxis(Math.Max(WideAngle, lidGameAngle), lidAxis);
            }

            if (BodyBox(go, hinge, out var min, out var max))
            {
                // Inside the walls (about 5 cm thick), up to a little above the rim.
                bin.BodyMin = min;
                bin.BodyMax = max;
                bin.InsideMin = min + new Vector3(0.05f, 0.03f, 0.05f);
                bin.InsideMax = max - new Vector3(0.05f, -0.1f, 0.05f);
                bin.OpenMin = bin.InsideMin;
                bin.OpenMax = bin.InsideMax;
            }
            // The opening is what the closed lid covers; the open lid stands on the hinge's other side.
            if (hinge != null)
            {
                var lidMeshes = new List<MeshFilter>();
                foreach (var mf in hinge.GetComponentsInChildren<MeshFilter>(true))
                    if (mf != null && mf.sharedMesh != null) lidMeshes.Add(mf);
                if (lidMeshes.Count > 0)
                {
                    LocalBox(lidMeshes, root, out var lMin, out var lMax);
                    bin.OpenMin = new Vector3(Math.Max(lMin.x, bin.InsideMin.x), 0f, Math.Max(lMin.z, bin.InsideMin.z));
                    bin.OpenMax = new Vector3(Math.Min(lMax.x, bin.InsideMax.x), 0f, Math.Min(lMax.z, bin.InsideMax.z));
                    // Where the open lid stands: the closed lid's box corners turned about the hinge by the opening. (This
                    // bin's lid is the front half of its roof: open, it stands up over the ridge, leaning out over the
                    // opening — fuel must rise clear of it.)
                    var hingeAt = root.InverseTransformPoint(hinge.position);
                    var turn = Quaternion.Inverse(root.rotation) * hinge.parent.rotation * (bin.Wide * Quaternion.Inverse(bin.Closed)) *
                               Quaternion.Inverse(hinge.parent.rotation) * root.rotation;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = new Vector3((i & 1) == 0 ? lMin.x : lMax.x, (i & 2) == 0 ? lMin.y : lMax.y, (i & 4) == 0 ? lMin.z : lMax.z);
                        var open = hingeAt + turn * (corner - hingeAt);
                        bin.LidOpenMin = i == 0 ? open : Vector3.Min(bin.LidOpenMin, open);
                        bin.LidOpenMax = i == 0 ? open : Vector3.Max(bin.LidOpenMax, open);
                    }
                    bin.HasLid = true;
                    var axis = root.InverseTransformDirection(hinge.parent.TransformDirection(bin.Closed * lidAxis));
                    axis.y = 0f;
                    if (axis.sqrMagnitude > 1e-4f) bin.HingeAxis = axis.normalized;
                    // Towards the lid's side: from the hinge to the closed lid's middle, flat.
                    var toLid = (lMin + lMax) / 2f - hingeAt;
                    toLid.y = 0f;
                    if (toLid.sqrMagnitude > 1e-4f) bin.ToLid = toLid.normalized;
                    bin.HingeAt = hingeAt;
                }
            }

            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                if (c != null && !c.isTrigger) bin.Mask |= 1 << c.gameObject.layer;

            // Its interactions (body and lid) open and close the lid: a callback of our own on each (not copied with it).
            foreach (var si in go.GetComponentsInChildren<SimpleInteraction>(true))
            {
                if (si == null) continue;
                if (si.m_EventEntries == null) si.m_EventEntries = new Il2CppSystem.Collections.Generic.List<InteractionEventEntry>();
                si.AddEventCallback(InteractionEventType.PerformInteraction, OnInteract);
                bin.Handles.Add(si);
            }
            SetHoverText(bin);
            bins[go.Pointer] = bin;
            Main.Log.Msg($"[Bin] '{go.name}' is a firewood bin: hinge '{(hinge != null ? hinge.name : "none")}', game {lidGameAngle:0} deg " +
                         $"around {FireZone.V(lidAxis)}, opens {Math.Max(WideAngle, lidGameAngle):0}; inside {FireZone.V(bin.InsideMin)}.." +
                         $"{FireZone.V(bin.InsideMax)}, opening {FireZone.V(bin.OpenMin)}..{FireZone.V(bin.OpenMax)}, open lid " +
                         $"{FireZone.V(bin.LidOpenMin)}..{FireZone.V(bin.LidOpenMax)}; {bin.Handles.Count} interaction(s); scale {FireZone.V(root.lossyScale)}");
        }

        private static void LocalBox(List<MeshFilter> parts, Transform space, out Vector3 min, out Vector3 max)
        {
            bool any = false;
            min = max = Vector3.zero;
            foreach (var mf in parts)
            {
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = space.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { min = max = p; any = true; }
                    else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                }
            }
        }

        private static bool BodyBox(GameObject go, Transform lid, out Vector3 min, out Vector3 max)
        {
            bool any = false;
            min = max = Vector3.zero;
            var root = go.transform;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf == null || mf.sharedMesh == null || (lid != null && mf.transform.IsChildOf(lid))) continue;
                string name = mf.name;
                int lod = name.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
                if (lod >= 0 && !name.Substring(lod).StartsWith("_LOD0", StringComparison.OrdinalIgnoreCase)) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { min = max = p; any = true; }
                    else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                }
            }
            return any;
        }

        // --- the lid ---

        private static void Toggle(Bin bin) => SetWant(bin, !bin.Want, false);

        /// <summary>The lid is to open (or close): its sound, and the hover text of its interactions.</summary>
        private static void SetWant(Bin bin, bool want, bool auto)
        {
            bin.Want = want;
            bin.AutoOpened = want && auto;
            string sound = want ? openAudio : closeAudio;
            if (!string.IsNullOrEmpty(sound) && bin.Root != null) GameAudioManager.PlaySound(sound, bin.Root.gameObject);
            SetHoverText(bin);
        }

        private static void SetHoverText(Bin bin)
        {
            string id = bin.Want ? "IMFI_BIN_CLOSE" : "IMFI_BIN_OPEN";
            string text = Localization.Get(id);
            foreach (var si in bin.Handles)
            {
                if (si == null) continue;
                // The game takes the hover text from the default one when it shows the interaction.
                if (si.m_DefaultHoverText == null) si.m_DefaultHoverText = new LocalizedString();
                si.m_DefaultHoverText.m_LocalizationID = id;
                si._HoverText_k__BackingField = text;
            }
        }

        /// <summary>Every LateUpdate: lids turn towards open or closed.</summary>
        public static void LateUpdate()
        {
            foreach (var bin in bins.Values)
            {
                if (bin.Lid == null || bin.Root == null) continue;
                if (bin.AutoOpened && bin.Want && Time.time > bin.AutoUntil) SetWant(bin, false, false);
                // Both follow the door closing speed setting; opening a little slower than closing (user).
                float seconds = (bin.Want ? OpenSlower : 1f) * 0.3f * Main.Settings.DoorTurnSlowDown;
                float target = bin.Want ? 1f : 0f;
                // Set every frame, moving or not: the game's own (relative) lid tween must not turn it any further.
                bin.T = Mathf.MoveTowards(bin.T, target, Time.deltaTime / seconds);
                if (!bin.Want) bin.OpenedAt = -1f;
                else if (bin.T >= 1f && bin.OpenedAt < 0f) bin.OpenedAt = Time.time;
                float t = bin.T * bin.T * (3f - 2f * bin.T);
                bin.Lid.localRotation = Quaternion.Slerp(bin.Closed, bin.Wide, t);
            }
        }

        /// <summary>The bin an item lies in (its centre inside the walls, up to a little above the rim), or null.</summary>
        private static Bin Holding(Vector3 point)
        {
            foreach (var bin in bins.Values)
            {
                // Standing in the world (not in the inventory or a container, nor being put down).
                if (!bin.Active || IsBeingPlaced(bin)) continue;
                if (Contains(bin, point)) return bin;
            }
            return null;
        }

        private static bool Contains(Bin bin, Vector3 point)
        {
            var p = bin.Root.InverseTransformPoint(point);
            return p.x >= bin.InsideMin.x && p.x <= bin.InsideMax.x && p.y >= bin.InsideMin.y && p.y <= bin.InsideMax.y &&
                   p.z >= bin.InsideMin.z && p.z <= bin.InsideMax.z;
        }

        public static bool InBin(Vector3 point) => Holding(point) != null;

        /// <summary>
        /// The game picks what is under the crosshairs, or near them: what lies in a bin with its lid shut (or nearly)
        /// can't be seen — the lid is picked instead (user: aiming at the lid showed the coal inside).
        /// </summary>
        [HarmonyLib.HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.GetInteractiveObjectUnderCrosshairs))]
        private static class UnderCrosshairs
        {
            private static void Postfix(ref GameObject __result)
            {
                try
                {
                    if (__result == null || bins.Count == 0) return;
                    var gi = __result.GetComponentInParent<GearItem>();
                    if (gi == null) return;
                    var point = gi.transform.position;
                    foreach (var c in gi.GetComponentsInChildren<Collider>(false))
                        if (c != null && c.enabled && !c.isTrigger) { point = c.bounds.center; break; }
                    foreach (var bin in bins.Values)
                    {
                        if (!bin.Active || bin.T >= 0.5f || !Contains(bin, point)) continue;
                        SimpleInteraction lid = null;
                        foreach (var si in bin.Handles)
                            if (si != null && (lid == null || (bin.Lid != null && si.transform.IsChildOf(bin.Lid)))) lid = si;
                        if (lid != null) __result = lid.gameObject;
                        return;
                    }
                }
                catch (Exception e)
                {
                    Once.Warn("bin:aim", "Firewood bin aim: " + e.Message);
                }
            }
        }

        /// <summary>
        /// In a bin (standing, or being moved by the game): fuel there is the bin's. A fire's own zones leave it alone
        /// (a bin may stand in one), auto-feed takes it from the bin (lid first, the way out).
        /// </summary>
        public static bool Inside(Vector3 point)
        {
            foreach (var bin in bins.Values)
                if (bin.Active && Contains(bin, point)) return true;
            return false;
        }

        /// <summary>
        /// How far towards the lid's side of its bin an item lies (m from the hinge line; more = nearer the lid's free
        /// edge). In a full bin those go first: the room they leave lets the ones at the back out (user).
        /// </summary>
        public static float TowardsLid(Vector3 point)
        {
            foreach (var bin in bins.Values)
                if (bin.Active && Contains(bin, point))
                    return Vector3.Dot(bin.Root.InverseTransformPoint(point) - bin.HingeAt, bin.ToLid);
            return 0f;
        }

        /// <summary>The bin an item lies in (its root), also one being moved; null if none.</summary>
        public static Transform BinOf(Vector3 point)
        {
            foreach (var bin in bins.Values)
                if (bin.Active && Contains(bin, point)) return bin.Root;
            return null;
        }

        /// <summary>In a bin the game is moving right now: it goes along with it, nothing takes it from there.</summary>
        public static bool BeingMoved(Vector3 point)
        {
            foreach (var bin in bins.Values)
                if (bin.Active && IsBeingPlaced(bin) && Contains(bin, point)) return true;
            return false;
        }

        /// <summary>The insides of the bins within <paramref name="reach"/> of a point, as world boxes (for a physics query).</summary>
        public static IEnumerable<(Vector3 centre, Vector3 half, Quaternion rotation)> Near(Vector3 point, float reach)
        {
            var list = new List<(Vector3, Vector3, Quaternion)>();
            foreach (var bin in bins.Values)
            {
                if (!bin.Active || IsBeingPlaced(bin) || (bin.Root.position - point).sqrMagnitude > reach * reach) continue;
                var mid = (bin.InsideMin + bin.InsideMax) / 2f;
                var half = Vector3.Scale((bin.InsideMax - bin.InsideMin) / 2f, bin.Root.lossyScale);
                list.Add((bin.Root.TransformPoint(mid), half, bin.Root.rotation));
            }
            return list;
        }

        /// <summary>The bin an item lies in (to leave out of obstacle checks), or null.</summary>
        public static Transform RootOf(Vector3 point) => Holding(point)?.Root;

        /// <summary>
        /// The way out by the bin's boxes (the fallback of the search, FirewoodBins.Exit): along inside at its own height to under
        /// the opening and clear of the open lid's box, straight up until its bottom is over the rim, then out over the
        /// wall on the fire's side — over the open lid's top when that way goes through it.
        /// </summary>
        private static List<Vector3> ExitByBoxes(Bin bin, Vector3 item, Vector3 toward, Vector3 half)
        {
            var root = bin.Root;
            var p = root.InverseTransformPoint(item);
            var dir = root.InverseTransformDirection(toward - item);
            dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.back;
            float rim = bin.BodyMax.y;
            // The item's half size along the bin's own axes.
            var h = new Vector3(Along(root.right, half), Along(root.up, half), Along(root.forward, half));
            h = new Vector3(h.x / Safe(root.lossyScale.x), h.y / Safe(root.lossyScale.y), h.z / Safe(root.lossyScale.z));

            // Inside the bin first, at its own height (above the floor), to under the opening (the fixed parts of the top
            // are not in the way then) and clear of where the open lid stands, then straight up until its bottom is over
            // the rim.
            float ux = Into(p.x, bin.OpenMin.x + h.x, bin.OpenMax.x - h.x);
            float uz = Into(p.z, bin.OpenMin.z + h.z, bin.OpenMax.z - h.z);
            if (bin.HasLid) ClearOfLid(bin, h, ref ux, ref uz);
            var up = new Vector3(ux, rim + h.y + RimClearance, uz);
            // Towards the fire, out over the wall: where the line leaves the body's box, then until its near side is clear.
            float tx = dir.x > 0f ? (bin.BodyMax.x - up.x) / dir.x : dir.x < 0f ? (bin.BodyMin.x - up.x) / dir.x : float.MaxValue;
            float tz = dir.z > 0f ? (bin.BodyMax.z - up.z) / dir.z : dir.z < 0f ? (bin.BodyMin.z - up.z) / dir.z : float.MaxValue;
            float t = Math.Max(0f, Math.Min(tx, tz)) + Math.Abs(dir.x) * h.x + Math.Abs(dir.z) * h.z + WallClearance;
            var outside = up + dir * t;
            // That way goes through the open lid: over its top.
            var margin = new Vector3(h.x + LidClearance, 0f, h.z + LidClearance);
            bool overLid = bin.HasLid && Crosses(up, outside, bin.LidOpenMin - margin, bin.LidOpenMax + margin);
            float height = overLid ? Math.Max(up.y, bin.LidOpenMax.y + h.y + RimClearance) : up.y;
            outside.y = height;
            var under = new Vector3(up.x, Math.Max(p.y, bin.InsideMin.y + h.y), up.z);
            var path = new List<Vector3>();
            if ((new Vector2(under.x - p.x, under.z - p.z)).sqrMagnitude > 0.0025f) path.Add(root.TransformPoint(under));
            path.Add(root.TransformPoint(up));
            if (overLid) path.Add(root.TransformPoint(new Vector3(up.x, height, up.z)));
            path.Add(root.TransformPoint(outside));
            return path;
        }

        /// <summary>
        /// Moves a rising item's place (x, z: its middle; <paramref name="h"/>: its half size) out from under the open lid,
        /// the shortest way that keeps it in the opening (or just the shortest way, if none does).
        /// </summary>
        private static void ClearOfLid(Bin bin, Vector3 h, ref float x, ref float z)
        {
            float x0 = bin.LidOpenMin.x - LidClearance - h.x, x1 = bin.LidOpenMax.x + LidClearance + h.x;
            float z0 = bin.LidOpenMin.z - LidClearance - h.z, z1 = bin.LidOpenMax.z + LidClearance + h.z;
            if (x <= x0 || x >= x1 || z <= z0 || z >= z1) return; // clear of it already
            float loX = bin.OpenMin.x + h.x, hiX = bin.OpenMax.x - h.x, loZ = bin.OpenMin.z + h.z, hiZ = bin.OpenMax.z - h.z;
            var ways = new[]
            {
                (cost: x - x0, fits: x0 >= loX, alongX: true, to: x0),
                (cost: x1 - x, fits: x1 <= hiX, alongX: true, to: x1),
                (cost: z - z0, fits: z0 >= loZ, alongX: false, to: z0),
                (cost: z1 - z, fits: z1 <= hiZ, alongX: false, to: z1),
            };
            var best = ways[0];
            foreach (var w in ways)
                if ((w.fits && !best.fits) || (w.fits == best.fits && w.cost < best.cost)) best = w;
            if (best.alongX) x = best.to;
            else z = best.to;
        }

        /// <summary>The flat (x, z) line from <paramref name="a"/> to <paramref name="b"/> passes through the box (Liang-Barsky).</summary>
        private static bool Crosses(Vector3 a, Vector3 b, Vector3 min, Vector3 max)
        {
            float t0 = 0f, t1 = 1f, dx = b.x - a.x, dz = b.z - a.z;
            return Clip(-dx, a.x - min.x, ref t0, ref t1) && Clip(dx, max.x - a.x, ref t0, ref t1) &&
                   Clip(-dz, a.z - min.z, ref t0, ref t1) && Clip(dz, max.z - a.z, ref t0, ref t1) && t0 <= t1;
        }

        private static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Math.Abs(p) < 1e-6f) return q >= 0f;
            float r = q / p;
            if (p < 0f)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }

        /// <summary>Half the size of a world box <paramref name="half"/> along a direction.</summary>
        private static float Along(Vector3 axis, Vector3 half) =>
            Math.Abs(axis.x) * half.x + Math.Abs(axis.y) * half.y + Math.Abs(axis.z) * half.z;

        /// <summary><paramref name="v"/> clamped into lo..hi, or the middle when the range is empty (too narrow).</summary>
        private static float Into(float v, float lo, float hi) => lo <= hi ? Mathf.Clamp(v, lo, hi) : (lo + hi) / 2f;

        private static float Safe(float v) => Math.Abs(v) < 0.0001f ? 1f : v;

        /// <summary>
        /// Auto-feed wants this item: true when it can fly (not in a bin, or the bin is open). A closed bin is opened
        /// first (and closes again after); false until it has been all the way open for a moment (user: first the lid
        /// opens, then the fuel comes out).
        /// </summary>
        public static bool ReadyFor(Vector3 point)
        {
            var bin = Holding(point);
            if (bin == null) return true;
            if (!bin.Want) SetWant(bin, true, true);
            if (bin.AutoOpened) bin.AutoUntil = Time.time + AutoCloseAfter;
            return bin.OpenedAt >= 0f && Time.time - bin.OpenedAt >= PauseWhenOpen;
        }

        /// <summary>The game is putting this bin down or moving it (its placement ghost).</summary>
        private static bool IsBeingPlaced(Bin bin)
        {
            var pm = GameManager.GetPlayerManagerComponent();
            var obj = pm != null && pm.IsInPlacementMode() ? pm.m_ObjectToPlace : null;
            return obj != null && obj.Pointer == bin.Root.gameObject.Pointer;
        }

        internal static string Describe(GameObject go)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"'{go.name}' layer {LayerMask.LayerToName(go.layer)}:");
            Tree(sb, go.transform, go.transform, 0);
            return sb.ToString();
        }

        private static void Tree(StringBuilder sb, Transform root, Transform t, int depth)
        {
            if (depth > 5) return;
            var parts = new StringBuilder();
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null) continue;
                string type = c.GetIl2CppType().Name;
                if (type == "Transform") continue;
                parts.Append(type);
                var col = c.TryCast<Collider>();
                if (col != null) parts.Append($"({(col.isTrigger ? "trigger " : "")}{LayerMask.LayerToName(col.gameObject.layer)} {FireZone.V(col.bounds.size)})");
                var mf = c.TryCast<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) parts.Append($"({mf.sharedMesh.name} {FireZone.V(mf.sharedMesh.bounds.size)})");
                parts.Append(' ');
            }
            sb.AppendLine($"{new string(' ', depth * 2 + 2)}{t.name} at {FireZone.V(root.InverseTransformPoint(t.position))}: {parts}");
            for (int i = 0; i < t.childCount; i++) Tree(sb, root, t.GetChild(i), depth + 1);
        }
    }
}
