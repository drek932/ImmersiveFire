using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using ImmersiveShared;
using UnityEngine;

namespace ImmersiveFire
{
    /// <summary>
    /// Fuel put into the zone of a burning fire goes into the fire, as if added from the game's "Add fuel" menu, with the
    /// same rules: not in strong wind, not past the longest burn time, coal only once the fire is old enough, unread skill
    /// books per setting. A stack goes in at once, as many pieces as the fire takes.
    ///  - Fuel the player is moving goes in straight from the hands, checked every frame; the move goes on with what is
    ///    left, and is cancelled only when the fire stops taking it while something is still held.
    ///  - Fuel lying in the zone goes in once it rests there; what can't go in yet stays and is tried again.
    ///    Fuel that already lay there while the fire was out is left alone (until moved out and back in).
    /// </summary>
    internal static partial class FuelFeeder
    {
        private const float ScanEvery = 0.1f;       // s, items lying in the zones
        private const float FiresEvery = 3f;        // s, look for new / removed fires
        private const float NearPlayer = 25f;       // m, fires farther away are not watched
        private const float SettledMove = 0.01f;    // m between two scans: the item came to rest

        private sealed class Watched
        {
            public FireplaceInteraction Fi;
            public Fire Fire;
            public FireKind Kind;
            public FireZone Zone;
            public FireZone DoorZone;   // fuel coming into it opens the stove door (null: no door handled)
            public FireZone Funnel;     // fuel on the move is drawn in from here (null: Zone)
            public FireZone[] Sides;    // beside a stove, left and right: fuel on the move is drawn in from there too
            public WoodStove Stove;

            /// <summary>Fuel on the move (held, carried, thrown) at this point goes in: funnel or sides, else the fire's zone.</summary>
            public bool DrawsIn(Vector3 p)
            {
                if (Funnel == null) return Zone.Contains(p);
                if (Funnel.Contains(p)) return true;
                if (Sides != null) foreach (var s in Sides) if (s.Contains(p)) return true;
                return false;
            }

            public readonly HashSet<IntPtr> LeftAlone = new();              // lay there while the fire was out
            public readonly Dictionary<IntPtr, Vector3> LastSeen = new();
            public readonly HashSet<IntPtr> Asked = new();                  // unread book: question shown
            public readonly Dictionary<IntPtr, string> Told = new();        // last message shown per item

            public bool Burning => Fire.GetFireState() == FireState.FullBurn;

            /// <summary>Burning, and a stove with a door handled here has it open (it swallows fuel only through it).</summary>
            public bool Takes => Burning && (DoorZone == null || Stove == null || Stove.m_Open);
        }

        private static readonly List<Watched> fires = new();
        private static float nextScan, nextFires;
        private static int gearMask = -1;

        public static void Reset()
        {
            fires.Clear();
            nextScan = nextFires = 0f;
            heldKey = IntPtr.Zero;
            thrown.Clear();
            StoveDoors.Reset();
            FuelFlight.Reset();
            StoveLayout.Reset();
        }

        public static void Update()
        {
            var player = GameManager.GetPlayerTransform();
            if (player == null) return;
            float now = Time.time;
            if (now >= nextFires)
            {
                nextFires = now + FiresEvery;
                RefreshFires();
            }

            try
            {
                FuelFlight.Update();
                UpdateDoors(player.position);
                if (Main.Settings.AutoOn) AutoFeedTick(player.position);
                if (Main.Settings.PullOn) CheckHeld(player.position);
                if (thrown.Count > 0) { if (Main.Settings.PullOn) CheckThrown(player.position); else thrown.Clear(); }
            }
            catch (Exception e)
            {
                Once.Warn("held:" + e.GetType().Name, "Checking the item being moved or thrown failed: " + e);
            }

            if (now < nextScan || !Main.Settings.PullOn) return;
            nextScan = now + ScanEvery;
            if (gearMask < 0) gearMask = 1 << LayerMask.NameToLayer("Gear");
            foreach (var w in fires)
            {
                try
                {
                    if (Watching(w, player.position) && PullsIn(w.Fi)) Scan(w);
                }
                catch (Exception e)
                {
                    Once.Warn("scan:" + e.GetType().Name, "Watching a fire failed: " + e);
                }
            }
        }

        public static IEnumerable<(FireplaceInteraction fi, FireKind kind, FireZone zone, FireZone door, FireZone funnel, FireZone[] sides)> Fires()
        {
            foreach (var w in fires)
                if (w.Fi != null) yield return (w.Fi, w.Kind, w.Zone, w.DoorZone, w.Funnel, w.Sides);
        }

        private static bool Watching(Watched w, Vector3 playerPos) =>
            w.Fi != null && w.Fire != null && Enabled(w.Kind) &&
            (w.Fi.transform.position - playerPos).sqrMagnitude <= NearPlayer * NearPlayer;

        private static bool Enabled(FireKind kind) => kind switch
        {
            FireKind.Campfire => Main.Settings.campfires,
            FireKind.Fireplace => Main.Settings.fireplaces,
            FireKind.Stove => Main.Settings.stoves,
            FireKind.Forge => Main.Settings.forges,
            _ => false,
        };

        /// <summary>Settings changed (funnel size): zones of the watched fires are built again, what lies there is kept.</summary>
        public static void RebuildZones()
        {
            foreach (var w in fires)
            {
                if (w.Fi == null) continue;
                w.Zone = FireZone.For(w.Fi, w.Kind);
                w.DoorZone = FireZone.DoorTrigger(w.Fi);
                w.Funnel = FireZone.Funnel(w.Fi);
                w.Sides = FireZone.Sides(w.Fi);
            }
        }

        private static void RefreshFires()
        {
            var found = UnityEngine.Object.FindObjectsOfType<FireplaceInteraction>();
            var keep = new List<Watched>();
            foreach (var fi in found)
            {
                if (fi == null || fi.Fire == null) continue;
                var known = fires.Find(w => w.Fi != null && w.Fi.Pointer == fi.Pointer);
                if (known == null)
                {
                    var kind = FireZone.KindOf(fi);
                    known = new Watched
                    {
                        Fi = fi, Fire = fi.Fire, Kind = kind, Zone = FireZone.For(fi, kind),
                        DoorZone = FireZone.DoorTrigger(fi), Funnel = FireZone.Funnel(fi), Sides = FireZone.Sides(fi), Stove = fi.TryCast<WoodStove>(),
                    };
                    Verbose.Log($"Watching {kind} '{fi.gameObject.name}': {known.Zone}");
                    // The ammo workbench has a fire of its own (AmmoWorkBench.m_Fire): its make-up, to fit its zones.
                    var bench = StoveLayout.ModelRoot(fi.transform).GetComponentInChildren<AmmoWorkBench>(true);
                    if (bench != null)
                        Main.Log.Msg($"Ammo workbench fire '{fi.gameObject.name}' ({kind}, door {(known.Stove != null && known.Stove.m_ObjectAnim != null ? "animated" : "none")}), " +
                                     $"the bench: {FirewoodBins.Describe(bench.gameObject)}");
                }
                keep.Add(known);
            }
            fires.Clear();
            fires.AddRange(keep);
        }

        private static int nearFrame = -1;
        private static float nearDistance;
        private static bool nearResult;

        /// <summary>Is any fire (lit or not, any kind) within this distance of the player? Answered once per frame.</summary>
        public static bool FireNearPlayer(float distance)
        {
            if (nearFrame == Time.frameCount && nearDistance == distance) return nearResult;
            nearFrame = Time.frameCount;
            nearDistance = distance;
            nearResult = false;
            var player = GameManager.GetPlayerTransform();
            if (player == null) return false;
            foreach (var w in fires)
                if (w.Fi != null && (w.Fi.transform.position - player.position).sqrMagnitude <= distance * distance)
                {
                    nearResult = true;
                    break;
                }
            return nearResult;
        }

        // --- stove doors ---

        /// <summary>Fuel coming near a burning stove (held, carried in a group or thrown) opens its door wide.</summary>
        private static void UpdateDoors(Vector3 playerPos)
        {
            List<Vector3> points = null;
            foreach (var w in fires)
            {
                if (w.DoorZone == null || w.Stove == null || !Watching(w, playerPos)) continue;
                if (points == null)
                {
                    points = Main.Settings.PullOn ? FuelOnTheMove() : new List<Vector3>();
                    // Fuel already flying in (auto-feed from a zone or a bin) opens the door on its way, see below.
                    // (fuel flying in opens the door itself, see below)
                }
                bool near = false;
                if (PullsIn(w.Fi)) foreach (var p in points)
                    // In the funnel counts too: fuel there is on its way in.
                    if (w.DoorZone.Contains(p) || w.DrawsIn(p)) { near = true; break; }
                // Fuel flying into this stove, past part of its way: the door opens before it arrives.
                if (FuelFlight.Approaching(w.Stove.Pointer)) near = true;
                StoveDoors.Mark(w.Stove, near, w.Burning);
            }
        }

        private static List<Vector3> FuelOnTheMove()
        {
            var points = new List<Vector3>();
            var pm = GameManager.GetPlayerManagerComponent();
            var held = pm != null && pm.IsInPlacementMode() ? pm.m_ObjectToPlaceGearItem : null;
            if (held != null && IsFuel(held))
            {
                points.Add(PointOf(held));
                foreach (var go in PerfectPlacementBridge.Carried())
                {
                    var gi = go.GetComponent<GearItem>();
                    if (gi != null && IsFuel(gi)) points.Add(PointOf(gi));
                }
            }
            foreach (var (gi, _, _) in thrown)
                if (gi != null) points.Add(PointOf(gi));
            // Fuel going into a bin near a stove doesn't open its door.
            points.RemoveAll(FirewoodBins.Inside);
            return points;
        }

        // --- the item the player is moving (and its PerfectPlacement group) ---

        private static IntPtr heldKey;      // the held item a refusal was told for (no message again while it stays)
        private static bool hoverFed;       // something went in during this move over a fire

        /// <summary>
        /// Pieces go in straight from the hands, checked every frame. With a PerfectPlacement group, the carried items in
        /// the zone go in first, one by one; the held one goes last, once nothing else is carried. The move goes on with
        /// what is left; it ends when nothing is left, and is cancelled when the fire stops taking fuel after some went in
        /// (what is left goes back where it came from). When nothing could go in at all, the player keeps moving (e.g. past
        /// a fire that is full).
        /// </summary>
        private static void CheckHeld(Vector3 playerPos)
        {
            var pm = GameManager.GetPlayerManagerComponent();
            var held = pm != null && pm.IsInPlacementMode() ? pm.m_ObjectToPlaceGearItem : null;
            if (held == null || !IsFuel(held))
            {
                heldKey = IntPtr.Zero;
                hoverFed = false;
                return;
            }

            bool anyInZone = false;
            int membersLeft = 0;
            foreach (var go in PerfectPlacementBridge.Carried())
            {
                var gi = go.GetComponent<GearItem>();
                if (gi == null || !IsFuel(gi)) continue;
                membersLeft++;
                var w = BurningZoneAt(PointOf(gi), playerPos);
                if (w == null) continue;
                anyInZone = true;
                // Taken out of the group before its last piece burns: PerfectPlacement moves its members every frame.
                int before = Units(gi);
                string goName = go.name;
                if (CanTakeNow(w, gi)) Pull(w, gi);
                int fed = FeedPieces(w, gi, () => PerfectPlacementBridge.LetGo(go), out string refusal, out bool book);
                if (fed > 0)
                {
                    hoverFed = true;
                    Verbose.Log($"{fed} x {goName} went into the {w.Kind} from the carried group");
                }
                if (fed >= before) membersLeft--; // all of it burnt
                if (refusal != null)
                {
                    Refused(pm, held, refusal);
                    return;
                }
                if (book) continue; // an unread book in a group just stays in it
            }

            var at = BurningZoneAt(PointOf(held), playerPos);
            if (at == null)
            {
                if (!anyInZone)
                {
                    heldKey = IntPtr.Zero; // away from every fire: a full fire may say so again next time
                    hoverFed = false;
                }
                return;
            }
            if (membersLeft > 0) return; // the held one goes last, it ends the move

            if (IsUnreadBook(held) && Main.Settings.Books != BookRule.Burn)
            {
                if (Main.Settings.Books == BookRule.Ask && heldKey != held.Pointer)
                {
                    EndMove(pm, held.name);
                    AskAboutBook(at, held, true);
                }
                heldKey = held.Pointer;
                return;
            }

            string name = held.name;
            // The last piece: the move has to end first, the held object is going away.
            if (CanTakeNow(at, held)) Pull(at, held);
            int fedHeld = FeedPieces(at, held, () => { EndMove(pm, name); return true; }, out string heldRefusal, out _);
            if (fedHeld > 0)
            {
                hoverFed = true;
                Verbose.Log($"{fedHeld} x {name} went into the {at.Kind} from the hands");
            }
            if (heldRefusal != null) Refused(pm, held, heldRefusal);
        }

        /// <summary>The fire can't take more: say it once; if some went in during this move, the move is cancelled.</summary>
        private static void Refused(PlayerManager pm, GearItem held, string refusal)
        {
            if (heldKey != held.Pointer) Tell(refusal);
            heldKey = held.Pointer;
            if (hoverFed) EndMove(pm, held.name);
            hoverFed = false;
        }

        private static Watched BurningZoneAt(Vector3 point, Vector3 playerPos)
        {
            // Being put into a bin that stands in a fire's zone: it goes into the bin.
            if (FirewoodBins.Inside(point)) return null;
            foreach (var w in fires)
                if (Watching(w, playerPos) && w.Takes && PullsIn(w.Fi) && w.DrawsIn(point)) return w;
            return null;
        }

        /// <summary>
        /// As many pieces as the fire takes, each checked by the game's rules. <paramref name="beforeLast"/> runs before the
        /// last piece (the item goes away with it); if it returns false, the last piece stays.
        /// </summary>
        private static int FeedPieces(Watched w, GearItem gi, Func<bool> beforeLast, out string refusal, out bool unreadBook)
        {
            refusal = null;
            unreadBook = false;
            int fed = 0;
            for (int guard = 0; guard < 200; guard++)
            {
                refusal = Refusal(w, gi);
                if (refusal != null) break;
                if (IsUnreadBook(gi) && Main.Settings.Books != BookRule.Burn)
                {
                    unreadBook = true;
                    break;
                }
                bool last = Units(gi) <= 1;
                if (last && beforeLast != null && !beforeLast()) break;
                Feed(w, gi);
                fed++;
                if (last) break;
            }
            return fed;
        }

        private static void EndMove(PlayerManager pm, string name)
        {
            if (pm == null || !pm.IsInPlacementMode()) return;
            pm.CancelPlacementMode();
            Verbose.Log($"Move of {name} ended at the fire");
        }

        // --- thrown items ---

        private const float ThrownWatch = 4f; // s, long enough to fly, bounce and roll

        private static readonly List<(GearItem item, float until, bool told)> thrown = new();

        /// <summary>Called when the player throws fuel (a torch): it is watched for a few seconds, every frame.</summary>
        public static void Thrown(GearItem gi) => thrown.Add((gi, Time.time + ThrownWatch, false));

        /// <summary>Thrown fuel goes in as soon as it is in a zone, still flying or bouncing.</summary>
        private static void CheckThrown(Vector3 playerPos)
        {
            for (int i = thrown.Count - 1; i >= 0; i--)
            {
                var (gi, until, told) = thrown[i];
                if (gi == null || Time.time > until || gi.m_InPlayerInventory || gi.m_InsideContainer)
                {
                    thrown.RemoveAt(i);
                    continue;
                }
                var point = PointOf(gi);
                if (FirewoodBins.Inside(point)) continue; // landed in a bin: the bin's
                foreach (var w in fires)
                {
                    if (!Watching(w, playerPos) || !w.Takes || !PullsIn(w.Fi) || !w.DrawsIn(point)) continue;
                    string refusal = Refusal(w, gi);
                    if (refusal != null)
                    {
                        // Lying there it is tried again by the scan; say it once.
                        if (!told) Tell(refusal);
                        thrown[i] = (gi, until, true);
                        break;
                    }
                    thrown.RemoveAt(i);
                    Verbose.Log($"Thrown {gi.name} caught by the {w.Kind}");
                    if (CanTakeNow(w, gi)) Pull(w, gi);
                    FeedLying(w, gi);
                    break;
                }
            }
        }

        // --- items lying in a zone ---

        private static void Scan(Watched w)
        {
            var inZone = FuelInZone(w);
            // Forget items that left the zone: put back in, they count as new.
            Forget(w.LeftAlone, inZone);
            Forget(w.Asked, inZone);
            foreach (var key in new List<IntPtr>(w.LastSeen.Keys)) if (!inZone.ContainsKey(key)) w.LastSeen.Remove(key);
            foreach (var key in new List<IntPtr>(w.Told.Keys)) if (!inZone.ContainsKey(key)) w.Told.Remove(key);

            bool burning = w.Burning;
            foreach (var (key, gi) in inZone)
            {
                if (!burning)
                {
                    w.LeftAlone.Add(key);
                    continue;
                }
                if (w.LeftAlone.Contains(key) || !w.Takes) continue; // a closed stove door: wait

                // Only items at rest: not one flying through or still rolling.
                var pos = gi.transform.position;
                bool settled = w.LastSeen.TryGetValue(key, out var last) && (pos - last).sqrMagnitude < SettledMove * SettledMove;
                w.LastSeen[key] = pos;
                if (!settled) continue;

                if (FeedLying(w, gi)) break;
            }
        }

        /// <summary>As many pieces of the item as the fire takes. True if any went in.</summary>
        private static bool FeedLying(Watched w, GearItem gi)
        {
            int fed = 0;
            string name = gi.name;
            for (int guard = 0; guard < 200; guard++)
            {
                string refusal = Refusal(w, gi);
                if (refusal != null)
                {
                    // The same message once per item while it lies there (it is tried again every scan).
                    if (!w.Told.TryGetValue(gi.Pointer, out var told) || told != refusal) Tell(refusal);
                    w.Told[gi.Pointer] = refusal;
                    break;
                }
                if (IsUnreadBook(gi) && Main.Settings.Books != BookRule.Burn)
                {
                    if (Main.Settings.Books == BookRule.Ask) AskAboutBook(w, gi, false);
                    break;
                }

                bool last = Units(gi) <= 1;
                Feed(w, gi);
                fed++;
                if (last) break;
            }
            if (fed > 1) Verbose.Log($"{fed} x {name} went into the {w.Kind} at once");
            return fed > 0;
        }

        private static void Forget(HashSet<IntPtr> set, Dictionary<IntPtr, GearItem> inZone) =>
            set.RemoveWhere(key => !inZone.ContainsKey(key));

        /// <summary>Fuel lying in the zone, by object pointer.</summary>
        private static Dictionary<IntPtr, GearItem> FuelInZone(Watched w)
        {
            var result = new Dictionary<IntPtr, GearItem>();
            var hits = Physics.OverlapSphere(w.Zone.Center, w.Zone.Reach, gearMask, QueryTriggerInteraction.Collide);
            var pm = GameManager.GetPlayerManagerComponent();
            // Items PerfectPlacement is moving as a group are handled with the held one (CheckHeld).
            HashSet<IntPtr> carried = null;
            if (pm != null && pm.IsInPlacementMode())
            {
                carried = new HashSet<IntPtr>();
                foreach (var go in PerfectPlacementBridge.Carried()) carried.Add(go.Pointer);
            }
            foreach (var c in hits)
            {
                var gi = c != null ? c.GetComponentInParent<GearItem>() : null;
                if (gi == null || result.ContainsKey(gi.Pointer) || !IsLooseFuel(gi, pm)) continue;
                if (carried != null && carried.Contains(gi.gameObject.Pointer)) continue;
                var p = PointOf(gi);
                if (!w.Zone.Contains(p) || FirewoodBins.Inside(p)) continue;
                result[gi.Pointer] = gi;
            }
            return result;
        }

        /// <summary>The item's middle: its collider's box centre (pivots may sit at the bottom or off to one side).</summary>
        private static Vector3 PointOf(GearItem gi)
        {
            foreach (var c in gi.GetComponentsInChildren<Collider>(false))
                if (c != null && c.enabled && !c.isTrigger) return c.bounds.center;
            return gi.transform.position;
        }

        /// <summary>
        /// The item's box as it lies: its collider's own box turned with it (a box, a capsule or a mesh's bounds; else
        /// its world box), half its size in its own axes. 10 cm when it has none.
        /// </summary>
        private static (Vector3 centre, Quaternion rotation, Vector3 half) BoxOf(GearItem gi)
        {
            foreach (var c in gi.GetComponentsInChildren<Collider>(false))
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                var t = c.transform;
                var scale = t.lossyScale;
                scale = new Vector3(Math.Abs(scale.x), Math.Abs(scale.y), Math.Abs(scale.z));
                var box = c.TryCast<BoxCollider>();
                if (box != null) return (t.TransformPoint(box.center), t.rotation, Vector3.Scale(box.size * 0.5f, scale));
                var capsule = c.TryCast<CapsuleCollider>();
                if (capsule != null)
                {
                    var h = Vector3.one * capsule.radius;
                    h[capsule.direction] = Math.Max(capsule.height * 0.5f, capsule.radius);
                    return (t.TransformPoint(capsule.center), t.rotation, Vector3.Scale(h, scale));
                }
                var mesh = c.TryCast<MeshCollider>();
                if (mesh != null && mesh.sharedMesh != null)
                {
                    var b = mesh.sharedMesh.bounds;
                    return (t.TransformPoint(b.center), t.rotation, Vector3.Scale(b.extents, scale));
                }
                return (c.bounds.center, Quaternion.identity, c.bounds.extents);
            }
            return (gi.transform.position, Quaternion.identity, Vector3.one * 0.1f);
        }

        /// <summary>
        /// Fuel the game's "Add fuel" menu would offer: a FuelSourceItem, not tinder — unless another mod lets tinder burn
        /// (More_Fuels_Accelerants, OtherMods). Also anything another mod made fuel (animal fat gets a FuelSourceItem).
        /// </summary>
        public static bool IsFuel(GearItem gi) =>
            gi.m_FuelSourceItem != null && (!gi.m_FuelSourceItem.m_IsTinder || TinderIsFuel);

        private static int tinderFrame = -1;
        private static bool tinderIsFuel;

        private static bool TinderIsFuel
        {
            get
            {
                if (tinderFrame != Time.frameCount)
                {
                    tinderFrame = Time.frameCount;
                    tinderIsFuel = OtherMods.TinderIsFuel;
                }
                return tinderIsFuel;
            }
        }

        /// <summary>Fuel lying loose in the world: not carried, in a container, on a cooking spot, being moved or in hands.</summary>
        private static bool IsLooseFuel(GearItem gi, PlayerManager pm)
        {
            if (!IsFuel(gi)) return false;
            if (gi.m_InPlayerInventory || gi.m_InsideContainer || !gi.gameObject.activeInHierarchy) return false;
            if (gi.IsAttachedToPlacePoint()) return false;
            if (pm != null && (Same(pm.m_ObjectToPlaceGearItem, gi) || Same(pm.m_ItemInHands, gi))) return false;
            return true;
        }

        private static bool Same(GearItem a, GearItem b) => a != null && b != null && a.Pointer == b.Pointer;

        private static int Units(GearItem gi) => gi.m_StackableItem != null ? gi.m_StackableItem.m_Units : 1;

        // --- the game's rules (Panel_FeedFire.OnFeedFire) ---

        /// <summary>The game's message if the fire can't take this piece now, else null.</summary>
        private static string Refusal(Watched w, GearItem gi)
        {
            var fuel = gi.m_FuelSourceItem;
            var fire = w.Fire;
            bool inForge = w.Kind == FireKind.Forge;

            if (fire.FireShouldBlowOutFromWind()) return Localization.Get("GAMEPLAY_TooWindyToAddFuel");

            // Coal in a forge isn't limited by the longest burn time (as in the game).
            if (!inForge || fuel.m_FireAgeMinutesBeforeAdding <= 0f)
            {
                float hours = fuel.GetModifiedBurnDurationHours(gi.GetNormalizedCondition());
                float total = hours + fire.GetRemainingLifeTimeSeconds() / 3600f;
                var manager = GameManager.GetFireManagerComponent();
                if (manager != null && total > manager.m_MaxDurationHoursOfFire) return Localization.Get("GAMEPLAY_CannotAddMoreFuel");
            }

            int minutes = Mathf.CeilToInt(fuel.m_FireAgeMinutesBeforeAdding - fire.m_ElapsedOnTODSeconds / 60f);
            if (minutes >= 1) return Localization.Get("GAMEPLAY_BurnTimeNeededForCoal").Replace("{minutes}", minutes.ToString());
            return null;
        }

        /// <summary>At least one piece would go in right now (the fire takes it, not a book held back by the setting).</summary>
        private static bool CanTakeNow(Watched w, GearItem gi) =>
            Refusal(w, gi) == null && !(IsUnreadBook(gi) && Main.Settings.Books != BookRule.Burn);

        /// <summary>
        /// The look-alike of the item flies into the fire (FuelFlight): into a stove around its body and its open door,
        /// straight in through the opening (StovePath); onto a campfire tossed in an arc (0.4 m over the higher end) down
        /// into the flames; other fires in a smaller arc.
        /// </summary>
        private static void Pull(Watched w, GearItem gi, FirewoodBins.ExitPlan found = null)
        {
            var from = PointOf(gi);
            // Out of a firewood bin: the way out found against its shape (FirewoodBins.Exit; already found by auto-feed).
            var (centre, rotation, half) = BoxOf(gi);
            var exit = found ?? (FirewoodBins.InBin(from) ? FirewoodBins.ExitPath(centre, rotation, half, TowardOf(w)) : null);
            if (exit?.Points != null && exit.Points.Count > 0) from = exit.Points[exit.Points.Count - 1];
            // The look-alike turns as the item's box turns on the way out of the bin.
            var turnTo = exit != null && exit.TurnPoints > 0 ? exit.Rotation * Quaternion.Inverse(rotation) * gi.transform.rotation : (Quaternion?)null;
            if (w.Funnel != null && w.Funnel.IsCone)
            {
                // Through the door: its last two legs (up to the door and inside) go into the stove itself.
                Fly(gi, w, exit, turnTo, StovePath(w.Funnel, from), 2);
                return;
            }
            if (FireZone.IsBarrel(w.Fi))
            {
                Fly(gi, w, exit, turnTo, BarrelPath(w.Fi.transform, from), 2);
                return;
            }
            if (w.Kind == FireKind.Campfire)
            {
                var flames = w.Zone.Frame.position + Vector3.up * 0.25f;
                var via = (from + flames) / 2f;
                via.y = Math.Max(from.y, flames.y) + 0.4f;
                Fly(gi, w, exit, turnTo, new List<Vector3> { via, flames }, 1);
                return;
            }
            var to = w.Zone.Center;
            Fly(gi, w, exit, turnTo, new List<Vector3> { (from + to) / 2f + Vector3.up * 0.3f, to }, 1);
        }

        /// <summary>Where fuel flies to in this fire (its funnel, else its zone): what a way out of a bin heads for.</summary>
        private static Vector3 TowardOf(Watched w) => w.Funnel != null ? w.Funnel.Center : w.Zone.Center;

        /// <summary>
        /// The flight: out of a firewood bin first when the fuel lies in one, then the path into the fire, around anything
        /// in the way except its own bin, the fire's door leaf and the last <paramref name="freeEnd"/> legs (into the fire).
        /// </summary>
        private static void Fly(GearItem gi, Watched w, FirewoodBins.ExitPlan exit, Quaternion? turnTo, List<Vector3> path, int freeEnd)
        {
            var all = new List<Vector3>();
            int exitPoints = exit?.Points?.Count ?? 0;
            if (exitPoints > 0) all.AddRange(exit.Points);
            all.AddRange(path);
            var anim = w.Stove != null ? w.Stove.m_ObjectAnim : null;
            var doorLeaf = anim != null && anim.m_Target != null ? anim.m_Target.transform : null;
            // The "add fuel" sound plays when it lands (FuelFlight), not when the fire takes it now (Feed, this frame).
            if (FuelFlight.Start(gi, all, Main.Settings.PullSpeed, w.Stove != null ? w.Stove.Pointer : IntPtr.Zero, w.Fi.gameObject,
                    freeEnd, exitPoints, turnTo, turnTo.HasValue ? exit.TurnPoints : 0, FirewoodBins.RootOf(PointOf(gi)), doorLeaf))
                soundOnArrivalFrame = Time.frameCount;
        }

        /// <summary>Set when a look-alike flies in this frame: the pieces fed now make no sound, the landing does.</summary>
        private static int soundOnArrivalFrame = -1;

        /// <summary>The game's "add fuel" sound at the fire, once per 0.2 s (a whole stack going in sounds once).</summary>
        public static void FeedSound(GameObject at)
        {
            if (at == null || Time.time - lastSound <= 0.2f) return;
            if (!InterfaceManager.TryGetPanel<Panel_FeedFire>(out var feed) || feed == null || string.IsNullOrEmpty(feed.m_FeedFireAudio)) return;
            GameAudioManager.PlaySound(feed.m_FeedFireAudio, at);
            lastSound = Time.time;
        }

        /// <summary>
        /// Into a fire barrel from above, tossed: up over the rim on the fuel's side, then down inside it near the wall,
        /// clear of the cooking spots around the middle of the grill (F6: within 0.15 m of the centre).
        /// </summary>
        private static List<Vector3> BarrelPath(Transform barrel, Vector3 from)
        {
            var centre = barrel.position;
            var side = new Vector3(from.x - centre.x, 0f, from.z - centre.z);
            side = side.sqrMagnitude > 0.0001f ? side.normalized : barrel.forward;
            var up = Vector3.up;
            return new List<Vector3>
            {
                centre + side * 0.45f + up * (FireZone.BarrelRim + 0.35f),     // over the rim, outside
                centre + side * 0.27f + up * (FireZone.BarrelRim + 0.15f),     // above the opening, by the wall
                centre + side * 0.22f + up * 0.55f,                            // down into the fire
            };
        }

        // Stove path, in the stove's frame from the door (the funnel's apex), forward = away from the stove:
        private const float StepOut = 0.7f;     // fuel beside or close to the stove first moves out this far in front
        private const float LineUp = 0.35f;     // then lines up with the door this far in front of it
        private const float Inside = 0.3f;      // and ends this far inside, past the opening

        /// <summary>
        /// Into a stove without touching it: fuel beside or close to the stove first moves out in front (and a little up),
        /// clear of the body and of the open door (it swings to the side and a bit back), then lines up with the door on
        /// its axis at door height, goes through the opening and ends inside the firebox.
        /// </summary>
        private static List<Vector3> StovePath(FireZone funnel, Vector3 from)
        {
            var yaw = funnel.Yaw;
            var origin = funnel.Frame.position;
            var door = funnel.Offset;                                   // local: (0, 0.74, -0.25) on the pot belly
            var forward = new Vector3(funnel.Axis.x, 0f, funnel.Axis.z).normalized;
            Vector3 World(Vector3 local) => origin + yaw * local;

            var start = funnel.Local(from);
            var path = new List<Vector3>();
            // How far in front of the door the fuel is, and how far off its axis.
            float ahead = Vector3.Dot(start - door, forward);
            var side = Vector3.Cross(Vector3.up, forward);
            float off = Vector3.Dot(start - door, side);
            if (ahead < StepOut || Math.Abs(off) > 0.25f)
            {
                var stepped = start + forward * Math.Max(0f, StepOut - ahead);
                stepped.y = Math.Max(start.y, door.y - 0.2f);
                path.Add(World(stepped));
            }
            path.Add(World(door + forward * LineUp));
            path.Add(World(door));
            path.Add(World(door - forward * Inside));
            return path;
        }

        private static bool IsUnreadBook(GearItem gi)
        {
            var book = gi.m_ResearchItem;
            return book != null && book.m_ElapsedHours < book.GetTimeRequirementHoursPrecise();
        }

        private static void Tell(string message)
        {
            if (Main.Settings.messages) HUDMessage.AddMessage(message, false, false);
        }

        private static void AskAboutBook(Watched w, GearItem gi, bool held)
        {
            if (!w.Asked.Add(gi.Pointer)) return; // asked already; put it in again to be asked again
            if (!InterfaceManager.TryGetPanel<Panel_Confirmation>(out var panel) || panel == null) return;
            Action confirmed = () =>
            {
                try
                {
                    // Still burning, and (if it was lying) still there?
                    if (gi == null || w.Fi == null || !w.Burning) return;
                    // A book held over the fire went back to where it came from: burn it from there.
                    if (!held && (!IsLooseFuel(gi, GameManager.GetPlayerManagerComponent()) || !w.Zone.Contains(PointOf(gi)))) return;
                    if (Refusal(w, gi) is string refusal) Tell(refusal);
                    else Feed(w, gi);
                }
                catch (Exception e)
                {
                    Once.Warn("book", "Burning the book failed: " + e);
                }
            };
            panel.ShowBurnResearchNotification(DelegateSupport.ConvertDelegate<Panel_Confirmation.CallbackDelegate>(confirmed));
            Verbose.Log($"Asked about burning the unread book {gi.name}");
        }

        private static float lastSound = -1f;

        /// <summary>One piece into the fire, used up like from the game's menu.</summary>
        private static void Feed(Watched w, GearItem gi)
        {
            // Flying in: the sound comes when it lands in the fire, not now.
            if (soundOnArrivalFrame != Time.frameCount) FeedSound(w.Fi.gameObject);

            w.Fire.AddFuel(gi, w.Kind == FireKind.Forge);

            int units = Units(gi);
            bool carried = gi.m_InPlayerInventory;
            Verbose.Log($"{gi.name} went into the {w.Kind} '{w.Fi.gameObject.name}' ({units} piece(s), {(carried ? "carried" : "in the world")}); " +
                        $"fire now {w.Fire.GetRemainingLifeTimeHours():0.00} h");
            if (carried) GameManager.GetPlayerManagerComponent().ConsumeUnitFromInventory(gi.gameObject);
            else if (units > 1) gi.m_StackableItem.m_Units = units - 1;
            else GearManager.DestroyGearObject(gi);
        }
    }
}
