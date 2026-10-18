using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using HarmonyLib;
using Il2Cpp;
using ImmersiveShared;
using ModData;
using UnityEngine;

namespace ImmersiveFire
{
    /// <summary>
    /// Auto-feed, switched on per fire from its menu (FireMenu): when the fire has less than the chosen time left to burn,
    /// it takes one piece of the fuel lying in its own zones (inside the stones / the ball around a campfire; the funnel and
    /// sides of a stove, which opens its door for it; the ball around a barrel), the highest first, with the same
    /// flight and the game's rules. The player lays fuel there beforehand, lights the fire and it keeps itself going.
    /// Saved with the game per scene (ModData). Works while the player is in the scene (elsewhere the game runs fires).
    /// </summary>
    internal static partial class FuelFeeder
    {
        public const int AutoOff = 0;

        private const float AutoEvery = 0.5f;   // s between checks

        private static readonly Dictionary<string, int> autoModes = new();
        private static float nextAuto;

        /// <summary>The fire's place, which stays the same between saves (fires don't move).</summary>
        private static string KeyOf(FireplaceInteraction fi)
        {
            var p = fi.transform.position;
            return string.Format(CultureInfo.InvariantCulture, "{0:0.00},{1:0.00},{2:0.00}", p.x, p.y, p.z);
        }

        public static int AutoMode(FireplaceInteraction fi) =>
            fi != null && autoModes.TryGetValue(KeyOf(fi), out int m) ? m : AutoOff;

        public static void SetAutoMode(FireplaceInteraction fi, int mode)
        {
            if (fi == null) return;
            string key = KeyOf(fi);
            if (mode == AutoOff) autoModes.Remove(key);
            else autoModes[key] = mode;
            Main.Log.Msg($"Auto-feed of '{fi.gameObject.name}' at {key}: {Describe(mode)}");
        }

        // --- pulling fuel in, switched per fire from its menu (FireMenu) ---

        private static readonly HashSet<string> pullOff = new();

        /// <summary>This fire pulls fuel in: the mod's mode allows it and it wasn't switched off at this fire.</summary>
        public static bool PullsIn(FireplaceInteraction fi) =>
            Main.Settings.PullOn && (fi == null || !pullOff.Contains(KeyOf(fi)));

        public static void SetPull(FireplaceInteraction fi, bool on)
        {
            if (fi == null) return;
            string key = KeyOf(fi);
            if (on) pullOff.Remove(key);
            else pullOff.Add(key);
            Main.Log.Msg($"Pulling fuel in at '{fi.gameObject.name}' at {key}: {(on ? "on" : "off")}");
        }

        public static string Describe(int mode) =>
            mode == AutoOff ? "off" : $"below {mode} min left";

        /// <summary>The watched fire of a FireplaceInteraction or a Fire (the fire menu passes the Fire).</summary>
        public static FireplaceInteraction FireplaceOf(Fire fire)
        {
            if (fire == null) return null;
            foreach (var w in fires)
                if (w.Fire != null && w.Fire.Pointer == fire.Pointer) return w.Fi;
            return fire.GetComponentInParent<FireplaceInteraction>();
        }

        private static void AutoFeedTick(Vector3 playerPos)
        {
            if (autoModes.Count == 0 || Time.time < nextAuto) return;
            nextAuto = Time.time + AutoEvery;
            foreach (var w in fires)
            {
                try
                {
                    if (!Watching(w, playerPos) || !w.Burning) continue;
                    string key = KeyOf(w.Fi);
                    if (!autoModes.TryGetValue(key, out int mode) || mode == AutoOff) continue;

                    if (w.Fire.GetRemainingLifeTimeSeconds() / 60f >= mode) continue;

                    var gi = ZoneFuel(w);
                    FirewoodBins.ExitPlan exit = null;
                    if (gi == null)
                    {
                        // Nothing in the fire's own zones: from a firewood bin nearby, by the bin's rules.
                        var inBin = BinFuel(w);
                        if (inBin.Count == 0) continue;
                        // Its lid opens first (looked at again soon, to go as soon as it is open).
                        if (!FirewoodBins.ReadyFor(PointOf(inBin[0])))
                        {
                            nextAuto = Math.Min(nextAuto, Time.time + 0.1f);
                            continue;
                        }
                        (gi, exit) = WayOutOfBin(w, inBin);
                    }
                    // A stove opens its door as the flying fuel comes near (UpdateDoors follows the flights).
                    Pull(w, gi, exit);
                    Feed(w, gi);
                    Verbose.Log($"Auto-feed ({Describe(mode)}): {gi.name} into the {w.Kind} '{w.Fi.gameObject.name}', " +
                                $"now {w.Fire.GetRemainingLifeTimeHours():0.00} h left");
                }
                catch (Exception e)
                {
                    Once.Warn("auto:" + e.GetType().Name, "Auto-feed failed: " + e);
                }
            }
        }

        /// <summary>
        /// Which lying fuel goes first, as in ImmersiveCooking / ImmersiveCrafting: the highest (the top of a pile), then the
        /// farthest from the player (heights within a centimetre count as the same).
        /// </summary>
        private static bool GoesBefore(Vector3 p, Vector3 than, Vector3 player)
        {
            if (Math.Abs(p.y - than.y) > 0.01f) return p.y > than.y;
            return (p - player).sqrMagnitude > (than - player).sqrMagnitude;
        }

        /// <summary>A bin's pile in layers from its top, this thick (a log is about 13 cm thick).</summary>
        private const float BinLayer = 0.12f;

        /// <summary>
        /// The fuel in bins within reach of the fire that it takes right now, in the bins' order: the highest layer first,
        /// and in a layer the nearest the lid's side first (user: a full bin's top layer at the back has no room to get
        /// out until the front of it is gone). Those of the first one's bin only (its lid is the one opened).
        /// </summary>
        private static List<GearItem> BinFuel(Watched w)
        {
            if (gearMask < 0) gearMask = 1 << LayerMask.NameToLayer("Gear");
            var pm = GameManager.GetPlayerManagerComponent();
            var found = new List<(GearItem gi, Vector3 at)>();
            var seen = new HashSet<IntPtr>();
            foreach (var (centre, half, rotation) in FirewoodBins.Near(w.Fi.transform.position, BinReach))
                foreach (var c in Physics.OverlapBox(centre, half, rotation, gearMask, QueryTriggerInteraction.Collide))
                {
                    var gi = c != null ? c.GetComponentInParent<GearItem>() : null;
                    if (gi == null || !seen.Add(gi.Pointer) || !IsLooseFuel(gi, pm)) continue;
                    var p = PointOf(gi);
                    if (!FirewoodBins.InBin(p) || !CanTakeNow(w, gi)) continue;
                    found.Add((gi, p));
                }
            float top = float.MinValue;
            foreach (var (_, at) in found) top = Math.Max(top, at.y);
            int Layer(Vector3 at) => (int)Math.Floor((top - at.y) / BinLayer);
            found.Sort((a, b) =>
            {
                int byLayer = Layer(a.at).CompareTo(Layer(b.at));
                return byLayer != 0 ? byLayer : FirewoodBins.TowardsLid(b.at).CompareTo(FirewoodBins.TowardsLid(a.at));
            });
            var list = new List<GearItem>();
            var bin = found.Count > 0 ? FirewoodBins.BinOf(found[0].at) : null;
            foreach (var (gi, at) in found)
                if (FirewoodBins.BinOf(at) == bin) list.Add(gi);
            return list;
        }

        private const int BinTries = 3;

        /// <summary>
        /// The first of the bin's top pieces (in order) with a clear way out, and that way; if none of the first few has
        /// one, the first piece (its way: the rough one).
        /// </summary>
        private static (GearItem gi, FirewoodBins.ExitPlan exit) WayOutOfBin(Watched w, List<GearItem> inBin)
        {
            var toward = TowardOf(w);
            for (int i = 0; i < Math.Min(BinTries, inBin.Count); i++)
            {
                var (centre, rotation, half) = BoxOf(inBin[i]);
                var exit = FirewoodBins.ExitPath(centre, rotation, half, toward);
                if (exit != null && exit.Found)
                {
                    if (i > 0) Verbose.Log($"[Bin] Piece {i + 1} in the bin's order taken: the ones before it had no clear way out");
                    return (inBin[i], exit);
                }
            }
            return (inBin[0], null);
        }

        /// <summary>The fuel lying in the fire's own zones that the fire takes right now, highest first (GoesBefore).</summary>
        private static GearItem ZoneFuel(Watched w)
        {
            if (gearMask < 0) gearMask = 1 << LayerMask.NameToLayer("Gear");
            var pm = GameManager.GetPlayerManagerComponent();
            var player = GameManager.GetPlayerTransform();
            var playerPos = player != null ? player.position : w.Fi.transform.position;
            var zones = new List<FireZone> { w.Zone };
            if (w.Funnel != null) zones.Add(w.Funnel);
            if (w.Sides != null) zones.AddRange(w.Sides);

            GearItem best = null;
            var bestAt = Vector3.zero;
            var seen = new HashSet<IntPtr>();
            foreach (var zone in zones)
                foreach (var c in Physics.OverlapSphere(zone.Center, zone.Reach, gearMask, QueryTriggerInteraction.Collide))
                {
                    var gi = c != null ? c.GetComponentInParent<GearItem>() : null;
                    if (gi == null) continue;
                    var p = PointOf(gi);
                    // In a bin, even one standing in the zone: the bin's, taken by the bins' turn (BinFuel) by the bin's rules
                    // (lid first, the way out).
                    if (FirewoodBins.Inside(p) || !seen.Add(gi.Pointer) || !IsLooseFuel(gi, pm)) continue;
                    if (!zone.Contains(p) && !w.DrawsIn(p) && !w.Zone.Contains(p)) continue;
                    if (!CanTakeNow(w, gi)) continue;
                    if (best == null || GoesBefore(p, bestAt, playerPos))
                    {
                        best = gi;
                        bestAt = p;
                    }
                }
            return best;
        }

        /// <summary>How far from a fire a firewood bin may stand and still feed it (one room, about).</summary>
        private const float BinReach = 10f;

        // --- saved with the game, per scene (ModData) ---

        private static readonly ModDataManager Data = new("ImmersiveFire", false);
        private const string AutoTag = "autofeed_@_";

        private const string PullOffTag = "pulloff_@_";

        private static void SaveAuto(string sceneSaveName)
        {
            Data.Save(JsonSerializer.Serialize(new List<string>(pullOff)), PullOffTag + sceneSaveName);
            Data.Save(JsonSerializer.Serialize(autoModes), AutoTag + sceneSaveName);
            if (autoModes.Count > 0) Main.Log.Msg($"Saved auto-feed of {autoModes.Count} fire(s) in {sceneSaveName}");
        }

        private static void LoadAuto(string sceneSaveName)
        {
            autoModes.Clear();
            pullOff.Clear();
            try
            {
                var off = Data.Load(PullOffTag + sceneSaveName);
                var list = string.IsNullOrEmpty(off) ? null : JsonSerializer.Deserialize<List<string>>(off);
                if (list != null) foreach (var k in list) pullOff.Add(k);
                if (pullOff.Count > 0) Main.Log.Msg($"Pulling fuel in off at {pullOff.Count} fire(s) in {sceneSaveName}");
            }
            catch (Exception e)
            {
                Main.Log.Error($"Saved pulling switches of {sceneSaveName} unreadable: {e.Message}");
            }
            var text = Data.Load(AutoTag + sceneSaveName);
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                var saved = JsonSerializer.Deserialize<Dictionary<string, int>>(text);
                // The old test mode "every 5 min" was saved as -5: it comes back as 30 min.
                if (saved != null) foreach (var kv in saved) autoModes[kv.Key] = kv.Value < 0 ? 30 : kv.Value;
                if (autoModes.Count > 0) Main.Log.Msg($"Auto-feed of {autoModes.Count} fire(s) back in {sceneSaveName}");
            }
            catch (Exception e)
            {
                Main.Log.Error($"Saved auto-feed of {sceneSaveName} unreadable: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.SaveSceneData))]
        private static class SaveSceneData
        {
            private static void Postfix(string sceneSaveName)
            {
                try
                {
                    SaveAuto(sceneSaveName);
                }
                catch (Exception e)
                {
                    Main.Log.Error("Saving auto-feed failed: " + e);
                }
            }
        }

        [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.LoadSceneData))]
        private static class LoadSceneData
        {
            private static void Postfix(string sceneSaveName)
            {
                try
                {
                    LoadAuto(sceneSaveName);
                }
                catch (Exception e)
                {
                    Main.Log.Error("Loading auto-feed failed: " + e);
                }
            }
        }
    }
}
