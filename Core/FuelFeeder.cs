#define VERBOSE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using ImmersiveShared;
using ModData;
using UnityEngine;

namespace ImmersiveFire;

internal static class FuelFeeder
{
	[HarmonyPatch(typeof(SaveGameSystem), "SaveSceneData")]
	private static class SaveSceneData
	{
		private static void Postfix(string sceneSaveName)
		{
			try
			{
				SaveAuto(sceneSaveName);
			}
			catch (Exception ex)
			{
				Main.Log.Error("Saving auto-feed failed: " + ex);
			}
		}
	}

	[HarmonyPatch(typeof(SaveGameSystem), "LoadSceneData")]
	private static class LoadSceneData
	{
		private static void Postfix(string sceneSaveName)
		{
			try
			{
				LoadAuto(sceneSaveName);
			}
			catch (Exception ex)
			{
				Main.Log.Error("Loading auto-feed failed: " + ex);
			}
		}
	}

	private sealed class Watched
	{
		public FireplaceInteraction Fi;

		public Fire Fire;

		public FireKind Kind;

		public FireZone Zone;

		public FireZone DoorZone;

		public FireZone Funnel;

		public FireZone[] Sides;

		public WoodStove Stove;

		public readonly HashSet<IntPtr> LeftAlone = new HashSet<IntPtr>();

		public readonly Dictionary<IntPtr, Vector3> LastSeen = new Dictionary<IntPtr, Vector3>();

		public readonly HashSet<IntPtr> Asked = new HashSet<IntPtr>();

		public readonly Dictionary<IntPtr, string> Told = new Dictionary<IntPtr, string>();

		public bool Burning => Fire.GetFireState() == FireState.FullBurn;

		public bool Takes
		{
			get
			{
				if (Burning)
				{
					if (DoorZone != null && !(Stove == null))
					{
						return Stove.m_Open;
					}
					return true;
				}
				return false;
			}
		}

		public bool DrawsIn(Vector3 p)
		{
			if (Funnel == null)
			{
				return Zone.Contains(p);
			}
			if (Funnel.Contains(p))
			{
				return true;
			}
			if (Sides != null)
			{
				FireZone[] sides = Sides;
				for (int i = 0; i < sides.Length; i++)
				{
					if (sides[i].Contains(p))
					{
						return true;
					}
				}
			}
			return false;
		}
	}

	public const int AutoOff = 0;

	private const float AutoEvery = 0.5f;

	private static readonly Dictionary<string, int> autoModes = new Dictionary<string, int>();

	private static float nextAuto;

	private const float BinLayer = 0.12f;

	private const int BinTries = 3;

	private const float BinReach = 10f;

	private static readonly ModDataManager Data = new ModDataManager("ImmersiveFire", false);

	private const string AutoTag = "autofeed_@_";

	private const float ScanEvery = 0.1f;

	private const float FiresEvery = 3f;

	private const float NearPlayer = 25f;

	private const float SettledMove = 0.01f;

	private static readonly List<Watched> fires = new List<Watched>();

	private static float nextScan;

	private static float nextFires;

	private static int gearMask = -1;

	private static int nearFrame = -1;

	private static float nearDistance;

	private static bool nearResult;

	private static IntPtr heldKey;

	private static bool hoverFed;

	private const float ThrownWatch = 4f;

	private static readonly List<(GearItem item, float until, bool told)> thrown = new List<(GearItem, float, bool)>();

	private static int tinderFrame = -1;

	private static bool tinderIsFuel;

	private static int soundOnArrivalFrame = -1;

	private const float StepOut = 0.7f;

	private const float LineUp = 0.35f;

	private const float Inside = 0.3f;

	private static float lastSound = -1f;

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

	private static string KeyOf(FireplaceInteraction fi)
	{
		Vector3 position = fi.transform.position;
		return string.Format(CultureInfo.InvariantCulture, "{0:0.00},{1:0.00},{2:0.00}", position.x, position.y, position.z);
	}

	public static int AutoMode(FireplaceInteraction fi)
	{
		if (!(fi != null) || !autoModes.TryGetValue(KeyOf(fi), out var value))
		{
			return 0;
		}
		return value;
	}

	public static void SetAutoMode(FireplaceInteraction fi, int mode)
	{
		if (!(fi == null))
		{
			string text = KeyOf(fi);
			if (mode == 0)
			{
				autoModes.Remove(text);
			}
			else
			{
				autoModes[text] = mode;
			}
			Main.Log.Msg($"Auto-feed of '{fi.gameObject.name}' at {text}: {Describe(mode)}");
		}
	}

	public static string Describe(int mode)
	{
		return mode switch
		{
			0 => "off", 
			_ => $"below {mode} min left", 
		};
	}

	public static FireplaceInteraction FireplaceOf(Fire fire)
	{
		if (fire == null)
		{
			return null;
		}
		foreach (Watched fire2 in fires)
		{
			if (fire2.Fire != null && fire2.Fire.Pointer == fire.Pointer)
			{
				return fire2.Fi;
			}
		}
		return fire.GetComponentInParent<FireplaceInteraction>();
	}

	private static void AutoFeedTick(Vector3 playerPos)
	{
		if (autoModes.Count == 0 || Time.time < nextAuto)
		{
			return;
		}
		nextAuto = Time.time + 0.5f;
		foreach (Watched fire in fires)
		{
			try
			{
				if (!Watching(fire, playerPos) || !fire.Burning)
				{
					continue;
				}
				string key = KeyOf(fire.Fi);
				if (!autoModes.TryGetValue(key, out var value))
				{
					continue;
				}
				bool flag;
				switch (value)
				{
				case 0:
					goto end_IL_005e;
				default:
					flag = fire.Fire.GetRemainingLifeTimeSeconds() / 60f < (float)value;
					break;
				}
				if (!flag)
				{
					continue;
				}
				GearItem gearItem = ZoneFuel(fire);
				FirewoodBins.ExitPlan found = null;
				if (!(gearItem == null))
				{
					goto IL_015d;
				}
				List<GearItem> list = BinFuel(fire);
				if (list.Count != 0)
				{
					if (FirewoodBins.ReadyFor(PointOf(list[0])))
					{
						(gearItem, found) = WayOutOfBin(fire, list);
						goto IL_015d;
					}
					nextAuto = Math.Min(nextAuto, Time.time + 0.1f);
				}
				goto end_IL_005e;
				IL_015d:
				Pull(fire, gearItem, found);
				Feed(fire, gearItem);
				Verbose.Log($"Auto-feed ({Describe(value)}): {gearItem.name} into the {fire.Kind} '{fire.Fi.gameObject.name}', now {fire.Fire.GetRemainingLifeTimeHours():0.00} h left");
				end_IL_005e:;
			}
			catch (Exception ex)
			{
				Once.Warn("auto:" + ex.GetType().Name, "Auto-feed failed: " + ex);
			}
		}
	}

	private static bool GoesBefore(Vector3 p, Vector3 than, Vector3 player)
	{
		if (Math.Abs(p.y - than.y) > 0.01f)
		{
			return p.y > than.y;
		}
		return (p - player).sqrMagnitude > (than - player).sqrMagnitude;
	}

	private static List<GearItem> BinFuel(Watched w)
	{
		if (gearMask < 0)
		{
			gearMask = 1 << LayerMask.NameToLayer("Gear");
		}
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		List<(GearItem, Vector3)> list = new List<(GearItem, Vector3)>();
		HashSet<IntPtr> hashSet = new HashSet<IntPtr>();
		foreach (var (center, halfExtents, orientation) in FirewoodBins.Near(w.Fi.transform.position, 10f))
		{
			foreach (Collider item3 in Physics.OverlapBox(center, halfExtents, orientation, gearMask, QueryTriggerInteraction.Collide))
			{
				GearItem gearItem = ((item3 != null) ? item3.GetComponentInParent<GearItem>() : null);
				if (!(gearItem == null) && hashSet.Add(gearItem.Pointer) && IsLooseFuel(gearItem, playerManagerComponent))
				{
					Vector3 vector = PointOf(gearItem);
					if (FirewoodBins.InBin(vector) && CanTakeNow(w, gearItem))
					{
						list.Add((gearItem, vector));
					}
				}
			}
		}
		float top = float.MinValue;
		foreach (var item4 in list)
		{
			Vector3 item = item4.Item2;
			top = Math.Max(top, item.y);
		}
		list.Sort(delegate((GearItem gi, Vector3 at) a, (GearItem gi, Vector3 at) b)
		{
			int num = Layer(a.at).CompareTo(Layer(b.at));
			return (num == 0) ? FirewoodBins.TowardsLid(b.at).CompareTo(FirewoodBins.TowardsLid(a.at)) : num;
		});
		List<GearItem> list2 = new List<GearItem>();
		Transform transform = ((list.Count > 0) ? FirewoodBins.BinOf(list[0].Item2) : null);
		foreach (var item5 in list)
		{
			var (item2, _) = item5;
			if (FirewoodBins.BinOf(item5.Item2) == transform)
			{
				list2.Add(item2);
			}
		}
		return list2;
		int Layer(Vector3 at)
		{
			return (int)Math.Floor((top - at.y) / 0.12f);
		}
	}

	private static (GearItem gi, FirewoodBins.ExitPlan exit) WayOutOfBin(Watched w, List<GearItem> inBin)
	{
		Vector3 toward = TowardOf(w);
		for (int i = 0; i < Math.Min(3, inBin.Count); i++)
		{
			(Vector3 centre, Quaternion rotation, Vector3 half) tuple = BoxOf(inBin[i]);
			Vector3 item = tuple.centre;
			Quaternion item2 = tuple.rotation;
			Vector3 item3 = tuple.half;
			FirewoodBins.ExitPlan exitPlan = FirewoodBins.ExitPath(item, item2, item3, toward);
			if (exitPlan != null && exitPlan.Found)
			{
				if (i > 0)
				{
					Verbose.Log($"[Bin] Piece {i + 1} in the bin's order taken: the ones before it had no clear way out");
				}
				return (gi: inBin[i], exit: exitPlan);
			}
		}
		return (gi: inBin[0], exit: null);
	}

	private static GearItem ZoneFuel(Watched w)
	{
		if (gearMask < 0)
		{
			gearMask = 1 << LayerMask.NameToLayer("Gear");
		}
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		Transform playerTransform = GameManager.GetPlayerTransform();
		Vector3 player = ((playerTransform != null) ? playerTransform.position : w.Fi.transform.position);
		List<FireZone> list = new List<FireZone> { w.Zone };
		if (w.Funnel != null)
		{
			list.Add(w.Funnel);
		}
		if (w.Sides != null)
		{
			list.AddRange(w.Sides);
		}
		GearItem gearItem = null;
		Vector3 than = Vector3.zero;
		HashSet<IntPtr> hashSet = new HashSet<IntPtr>();
		foreach (FireZone item in list)
		{
			foreach (Collider item2 in Physics.OverlapSphere(item.Center, item.Reach, gearMask, QueryTriggerInteraction.Collide))
			{
				GearItem gearItem2 = ((item2 != null) ? item2.GetComponentInParent<GearItem>() : null);
				if (!(gearItem2 == null))
				{
					Vector3 vector = PointOf(gearItem2);
					if (!FirewoodBins.Inside(vector) && hashSet.Add(gearItem2.Pointer) && IsLooseFuel(gearItem2, playerManagerComponent) && (item.Contains(vector) || w.DrawsIn(vector) || w.Zone.Contains(vector)) && CanTakeNow(w, gearItem2) && (gearItem == null || GoesBefore(vector, than, player)))
					{
						gearItem = gearItem2;
						than = vector;
					}
				}
			}
		}
		return gearItem;
	}

	private static void SaveAuto(string sceneSaveName)
	{
		Data.Save(JsonSerializer.Serialize(autoModes), "autofeed_@_" + sceneSaveName);
		if (autoModes.Count > 0)
		{
			Main.Log.Msg($"Saved auto-feed of {autoModes.Count} fire(s) in {sceneSaveName}");
		}
	}

	private static void LoadAuto(string sceneSaveName)
	{
		autoModes.Clear();
		string text = Data.Load("autofeed_@_" + sceneSaveName);
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		try
		{
			Dictionary<string, int> dictionary = JsonSerializer.Deserialize<Dictionary<string, int>>(text);
			if (dictionary != null)
			{
				foreach (KeyValuePair<string, int> item in dictionary)
				{
					autoModes[item.Key] = ((item.Value < 0) ? 30 : item.Value);
				}
			}
			if (autoModes.Count > 0)
			{
				Main.Log.Msg($"Auto-feed of {autoModes.Count} fire(s) back in {sceneSaveName}");
			}
		}
		catch (Exception ex)
		{
			Main.Log.Error("Saved auto-feed of " + sceneSaveName + " unreadable: " + ex.Message);
		}
	}

	public static void Reset()
	{
		fires.Clear();
		nextScan = (nextFires = 0f);
		heldKey = IntPtr.Zero;
		thrown.Clear();
		StoveDoors.Reset();
		FuelFlight.Reset();
		StoveLayout.Reset();
	}

	public static void Update()
	{
		Transform playerTransform = GameManager.GetPlayerTransform();
		if (playerTransform == null)
		{
			return;
		}
		float time = Time.time;
		if (time >= nextFires)
		{
			nextFires = time + 3f;
			RefreshFires();
		}
		try
		{
			FuelFlight.Update();
			UpdateDoors(playerTransform.position);
			if (Main.Settings.AutoOn)
			{
				AutoFeedTick(playerTransform.position);
			}
			if (Main.Settings.PullOn)
			{
				CheckHeld(playerTransform.position);
			}
			if (thrown.Count > 0)
			{
				if (Main.Settings.PullOn)
				{
					CheckThrown(playerTransform.position);
				}
				else
				{
					thrown.Clear();
				}
			}
		}
		catch (Exception ex)
		{
			Once.Warn("held:" + ex.GetType().Name, "Checking the item being moved or thrown failed: " + ex);
		}
		if (time < nextScan || !Main.Settings.PullOn)
		{
			return;
		}
		nextScan = time + 0.1f;
		if (gearMask < 0)
		{
			gearMask = 1 << LayerMask.NameToLayer("Gear");
		}
		foreach (Watched fire in fires)
		{
			try
			{
				if (Watching(fire, playerTransform.position))
				{
					Scan(fire);
				}
			}
			catch (Exception ex2)
			{
				Once.Warn("scan:" + ex2.GetType().Name, "Watching a fire failed: " + ex2);
			}
		}
	}

	public static IEnumerable<(FireplaceInteraction fi, FireKind kind, FireZone zone, FireZone door, FireZone funnel, FireZone[] sides)> Fires()
	{
		foreach (Watched fire in fires)
		{
			if (fire.Fi != null)
			{
				yield return (fi: fire.Fi, kind: fire.Kind, zone: fire.Zone, door: fire.DoorZone, funnel: fire.Funnel, sides: fire.Sides);
			}
		}
	}

	private static bool Watching(Watched w, Vector3 playerPos)
	{
		if (w.Fi != null && w.Fire != null && Enabled(w.Kind))
		{
			return (w.Fi.transform.position - playerPos).sqrMagnitude <= 625f;
		}
		return false;
	}

	private static bool Enabled(FireKind kind)
	{
		return kind switch
		{
			FireKind.Campfire => Main.Settings.campfires, 
			FireKind.Fireplace => Main.Settings.fireplaces, 
			FireKind.Stove => Main.Settings.stoves, 
			FireKind.Forge => Main.Settings.forges, 
			_ => false, 
		};
	}

	public static void RebuildZones()
	{
		foreach (Watched fire in fires)
		{
			if (!(fire.Fi == null))
			{
				fire.Zone = FireZone.For(fire.Fi, fire.Kind);
				fire.DoorZone = FireZone.DoorTrigger(fire.Fi);
				fire.Funnel = FireZone.Funnel(fire.Fi);
				fire.Sides = FireZone.Sides(fire.Fi);
			}
		}
	}

	private static void RefreshFires()
	{
		Il2CppArrayBase<FireplaceInteraction> il2CppArrayBase = UnityEngine.Object.FindObjectsOfType<FireplaceInteraction>();
		List<Watched> list = new List<Watched>();
		foreach (FireplaceInteraction fi in il2CppArrayBase)
		{
			if (fi == null || fi.Fire == null)
			{
				continue;
			}
			Watched watched = fires.Find((Watched w) => w.Fi != null && w.Fi.Pointer == fi.Pointer);
			if (watched == null)
			{
				FireKind fireKind = FireZone.KindOf(fi);
				watched = new Watched
				{
					Fi = fi,
					Fire = fi.Fire,
					Kind = fireKind,
					Zone = FireZone.For(fi, fireKind),
					DoorZone = FireZone.DoorTrigger(fi),
					Funnel = FireZone.Funnel(fi),
					Sides = FireZone.Sides(fi),
					Stove = fi.TryCast<WoodStove>()
				};
				Verbose.Log($"Watching {fireKind} '{fi.gameObject.name}': {watched.Zone}");
				AmmoWorkBench componentInChildren = StoveLayout.ModelRoot(fi.transform).GetComponentInChildren<AmmoWorkBench>(includeInactive: true);
				if (componentInChildren != null)
				{
					Main.Log.Msg($"Ammo workbench fire '{fi.gameObject.name}' ({fireKind}, door {((watched.Stove != null && watched.Stove.m_ObjectAnim != null) ? "animated" : "none")}), the bench: {FirewoodBins.Describe(componentInChildren.gameObject)}");
				}
			}
			list.Add(watched);
		}
		fires.Clear();
		fires.AddRange(list);
	}

	public static bool FireNearPlayer(float distance)
	{
		if (nearFrame == Time.frameCount && nearDistance == distance)
		{
			return nearResult;
		}
		nearFrame = Time.frameCount;
		nearDistance = distance;
		nearResult = false;
		Transform playerTransform = GameManager.GetPlayerTransform();
		if (playerTransform == null)
		{
			return false;
		}
		foreach (Watched fire in fires)
		{
			if (fire.Fi != null && (fire.Fi.transform.position - playerTransform.position).sqrMagnitude <= distance * distance)
			{
				nearResult = true;
				break;
			}
		}
		return nearResult;
	}

	private static void UpdateDoors(Vector3 playerPos)
	{
		List<Vector3> list = null;
		foreach (Watched fire in fires)
		{
			if (fire.DoorZone == null || fire.Stove == null || !Watching(fire, playerPos))
			{
				continue;
			}
			if (list == null)
			{
				list = (Main.Settings.PullOn ? FuelOnTheMove() : new List<Vector3>());
			}
			bool fuelNear = false;
			foreach (Vector3 item in list)
			{
				if (fire.DoorZone.Contains(item) || fire.DrawsIn(item))
				{
					fuelNear = true;
					break;
				}
			}
			if (FuelFlight.Approaching(fire.Stove.Pointer))
			{
				fuelNear = true;
			}
			StoveDoors.Mark(fire.Stove, fuelNear, fire.Burning);
		}
	}

	private static List<Vector3> FuelOnTheMove()
	{
		List<Vector3> list = new List<Vector3>();
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		GearItem gearItem = ((playerManagerComponent != null && playerManagerComponent.IsInPlacementMode()) ? playerManagerComponent.m_ObjectToPlaceGearItem : null);
		if (gearItem != null && IsFuel(gearItem))
		{
			list.Add(PointOf(gearItem));
			foreach (GameObject item2 in PerfectPlacementBridge.Carried())
			{
				GearItem component = item2.GetComponent<GearItem>();
				if (component != null && IsFuel(component))
				{
					list.Add(PointOf(component));
				}
			}
		}
		foreach (var item3 in thrown)
		{
			GearItem item = item3.item;
			if (item != null)
			{
				list.Add(PointOf(item));
			}
		}
		list.RemoveAll(FirewoodBins.Inside);
		return list;
	}

	private static void CheckHeld(Vector3 playerPos)
	{
		PlayerManager pm = GameManager.GetPlayerManagerComponent();
		GearItem gearItem = ((pm != null && pm.IsInPlacementMode()) ? pm.m_ObjectToPlaceGearItem : null);
		if (gearItem == null || !IsFuel(gearItem))
		{
			heldKey = IntPtr.Zero;
			hoverFed = false;
			return;
		}
		bool flag = false;
		int num = 0;
		foreach (GameObject go in PerfectPlacementBridge.Carried())
		{
			GearItem component = go.GetComponent<GearItem>();
			if (component == null || !IsFuel(component))
			{
				continue;
			}
			num++;
			Watched watched = BurningZoneAt(PointOf(component), playerPos);
			if (watched != null)
			{
				flag = true;
				int num2 = Units(component);
				string name2 = go.name;
				if (CanTakeNow(watched, component))
				{
					Pull(watched, component);
				}
				string refusal;
				bool unreadBook;
				int num3 = FeedPieces(watched, component, () => PerfectPlacementBridge.LetGo(go), out refusal, out unreadBook);
				if (num3 > 0)
				{
					hoverFed = true;
					Verbose.Log($"{num3} x {name2} went into the {watched.Kind} from the carried group");
				}
				if (num3 >= num2)
				{
					num--;
				}
				if (refusal != null)
				{
					Refused(pm, gearItem, refusal);
					return;
				}
			}
		}
		Watched watched2 = BurningZoneAt(PointOf(gearItem), playerPos);
		if (watched2 == null)
		{
			if (!flag)
			{
				heldKey = IntPtr.Zero;
				hoverFed = false;
			}
		}
		else
		{
			if (num > 0)
			{
				return;
			}
			if (IsUnreadBook(gearItem) && Main.Settings.Books != BookRule.Burn)
			{
				if (Main.Settings.Books == BookRule.Ask && heldKey != gearItem.Pointer)
				{
					EndMove(pm, gearItem.name);
					AskAboutBook(watched2, gearItem, held: true);
				}
				heldKey = gearItem.Pointer;
				return;
			}
			string name = gearItem.name;
			if (CanTakeNow(watched2, gearItem))
			{
				Pull(watched2, gearItem);
			}
			string refusal2;
			bool unreadBook2;
			int num4 = FeedPieces(watched2, gearItem, delegate
			{
				EndMove(pm, name);
				return true;
			}, out refusal2, out unreadBook2);
			if (num4 > 0)
			{
				hoverFed = true;
				Verbose.Log($"{num4} x {name} went into the {watched2.Kind} from the hands");
			}
			if (refusal2 != null)
			{
				Refused(pm, gearItem, refusal2);
			}
		}
	}

	private static void Refused(PlayerManager pm, GearItem held, string refusal)
	{
		if (heldKey != held.Pointer)
		{
			Tell(refusal);
		}
		heldKey = held.Pointer;
		if (hoverFed)
		{
			EndMove(pm, held.name);
		}
		hoverFed = false;
	}

	private static Watched BurningZoneAt(Vector3 point, Vector3 playerPos)
	{
		if (FirewoodBins.Inside(point))
		{
			return null;
		}
		foreach (Watched fire in fires)
		{
			if (Watching(fire, playerPos) && fire.Takes && fire.DrawsIn(point))
			{
				return fire;
			}
		}
		return null;
	}

	private static int FeedPieces(Watched w, GearItem gi, Func<bool> beforeLast, out string refusal, out bool unreadBook)
	{
		refusal = null;
		unreadBook = false;
		int num = 0;
		for (int i = 0; i < 200; i++)
		{
			refusal = Refusal(w, gi);
			if (refusal != null)
			{
				break;
			}
			if (IsUnreadBook(gi) && Main.Settings.Books != BookRule.Burn)
			{
				unreadBook = true;
				break;
			}
			bool flag = Units(gi) <= 1;
			if (flag && beforeLast != null && !beforeLast())
			{
				break;
			}
			Feed(w, gi);
			num++;
			if (flag)
			{
				break;
			}
		}
		return num;
	}

	private static void EndMove(PlayerManager pm, string name)
	{
		if (!(pm == null) && pm.IsInPlacementMode())
		{
			pm.CancelPlacementMode();
			Verbose.Log("Move of " + name + " ended at the fire");
		}
	}

	public static void Thrown(GearItem gi)
	{
		thrown.Add((gi, Time.time + 4f, false));
	}

	private static void CheckThrown(Vector3 playerPos)
	{
		for (int num = thrown.Count - 1; num >= 0; num--)
		{
			var (gearItem, num2, flag) = thrown[num];
			if (gearItem == null || Time.time > num2 || gearItem.m_InPlayerInventory || gearItem.m_InsideContainer)
			{
				thrown.RemoveAt(num);
			}
			else
			{
				Vector3 vector = PointOf(gearItem);
				if (!FirewoodBins.Inside(vector))
				{
					foreach (Watched fire in fires)
					{
						if (!Watching(fire, playerPos) || !fire.Takes || !fire.DrawsIn(vector))
						{
							continue;
						}
						string text = Refusal(fire, gearItem);
						if (text != null)
						{
							if (!flag)
							{
								Tell(text);
							}
							thrown[num] = (gearItem, num2, true);
							break;
						}
						thrown.RemoveAt(num);
						Verbose.Log($"Thrown {gearItem.name} caught by the {fire.Kind}");
						if (CanTakeNow(fire, gearItem))
						{
							Pull(fire, gearItem);
						}
						FeedLying(fire, gearItem);
						break;
					}
				}
			}
		}
	}

	private static void Scan(Watched w)
	{
		Dictionary<IntPtr, GearItem> dictionary = FuelInZone(w);
		Forget(w.LeftAlone, dictionary);
		Forget(w.Asked, dictionary);
		foreach (IntPtr item in new List<IntPtr>(w.LastSeen.Keys))
		{
			if (!dictionary.ContainsKey(item))
			{
				w.LastSeen.Remove(item);
			}
		}
		foreach (IntPtr item2 in new List<IntPtr>(w.Told.Keys))
		{
			if (!dictionary.ContainsKey(item2))
			{
				w.Told.Remove(item2);
			}
		}
		bool burning = w.Burning;
		foreach (var (intPtr2, gearItem2) in dictionary)
		{
			if (!burning)
			{
				w.LeftAlone.Add(intPtr2);
			}
			else if (!w.LeftAlone.Contains(intPtr2) && w.Takes)
			{
				Vector3 position = gearItem2.transform.position;
				Vector3 value;
				bool num = w.LastSeen.TryGetValue(intPtr2, out value) && (position - value).sqrMagnitude < 0.0001f;
				w.LastSeen[intPtr2] = position;
				if (num && FeedLying(w, gearItem2))
				{
					break;
				}
			}
		}
	}

	private static bool FeedLying(Watched w, GearItem gi)
	{
		int num = 0;
		string name = gi.name;
		for (int i = 0; i < 200; i++)
		{
			string text = Refusal(w, gi);
			if (text != null)
			{
				if (!w.Told.TryGetValue(gi.Pointer, out var value) || value != text)
				{
					Tell(text);
				}
				w.Told[gi.Pointer] = text;
				break;
			}
			if (IsUnreadBook(gi) && Main.Settings.Books != BookRule.Burn)
			{
				if (Main.Settings.Books == BookRule.Ask)
				{
					AskAboutBook(w, gi, held: false);
				}
				break;
			}
			bool num2 = Units(gi) <= 1;
			Feed(w, gi);
			num++;
			if (num2)
			{
				break;
			}
		}
		if (num > 1)
		{
			Verbose.Log($"{num} x {name} went into the {w.Kind} at once");
		}
		return num > 0;
	}

	private static void Forget(HashSet<IntPtr> set, Dictionary<IntPtr, GearItem> inZone)
	{
		set.RemoveWhere((IntPtr key) => !inZone.ContainsKey(key));
	}

	private static Dictionary<IntPtr, GearItem> FuelInZone(Watched w)
	{
		Dictionary<IntPtr, GearItem> dictionary = new Dictionary<IntPtr, GearItem>();
		Il2CppReferenceArray<Collider> il2CppReferenceArray = Physics.OverlapSphere(w.Zone.Center, w.Zone.Reach, gearMask, QueryTriggerInteraction.Collide);
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		HashSet<IntPtr> hashSet = null;
		if (playerManagerComponent != null && playerManagerComponent.IsInPlacementMode())
		{
			hashSet = new HashSet<IntPtr>();
			foreach (GameObject item in PerfectPlacementBridge.Carried())
			{
				hashSet.Add(item.Pointer);
			}
		}
		foreach (Collider item2 in il2CppReferenceArray)
		{
			GearItem gearItem = ((item2 != null) ? item2.GetComponentInParent<GearItem>() : null);
			if (!(gearItem == null) && !dictionary.ContainsKey(gearItem.Pointer) && IsLooseFuel(gearItem, playerManagerComponent) && (hashSet == null || !hashSet.Contains(gearItem.gameObject.Pointer)))
			{
				Vector3 vector = PointOf(gearItem);
				if (w.Zone.Contains(vector) && !FirewoodBins.Inside(vector))
				{
					dictionary[gearItem.Pointer] = gearItem;
				}
			}
		}
		return dictionary;
	}

	private static Vector3 PointOf(GearItem gi)
	{
		foreach (Collider componentsInChild in gi.GetComponentsInChildren<Collider>(includeInactive: false))
		{
			if (componentsInChild != null && componentsInChild.enabled && !componentsInChild.isTrigger)
			{
				return componentsInChild.bounds.center;
			}
		}
		return gi.transform.position;
	}

	private static (Vector3 centre, Quaternion rotation, Vector3 half) BoxOf(GearItem gi)
	{
		foreach (Collider componentsInChild in gi.GetComponentsInChildren<Collider>(includeInactive: false))
		{
			if (!(componentsInChild == null) && componentsInChild.enabled && !componentsInChild.isTrigger)
			{
				Transform transform = componentsInChild.transform;
				Vector3 lossyScale = transform.lossyScale;
				lossyScale = new Vector3(Math.Abs(lossyScale.x), Math.Abs(lossyScale.y), Math.Abs(lossyScale.z));
				BoxCollider boxCollider = componentsInChild.TryCast<BoxCollider>();
				if (boxCollider != null)
				{
					return (centre: transform.TransformPoint(boxCollider.center), rotation: transform.rotation, half: Vector3.Scale(boxCollider.size * 0.5f, lossyScale));
				}
				CapsuleCollider capsuleCollider = componentsInChild.TryCast<CapsuleCollider>();
				if (capsuleCollider != null)
				{
					Vector3 a = Vector3.one * capsuleCollider.radius;
					a[capsuleCollider.direction] = Math.Max(capsuleCollider.height * 0.5f, capsuleCollider.radius);
					return (centre: transform.TransformPoint(capsuleCollider.center), rotation: transform.rotation, half: Vector3.Scale(a, lossyScale));
				}
				MeshCollider meshCollider = componentsInChild.TryCast<MeshCollider>();
				if (meshCollider != null && meshCollider.sharedMesh != null)
				{
					Bounds bounds = meshCollider.sharedMesh.bounds;
					return (centre: transform.TransformPoint(bounds.center), rotation: transform.rotation, half: Vector3.Scale(bounds.extents, lossyScale));
				}
				return (centre: componentsInChild.bounds.center, rotation: Quaternion.identity, half: componentsInChild.bounds.extents);
			}
		}
		return (centre: gi.transform.position, rotation: Quaternion.identity, half: Vector3.one * 0.1f);
	}

	public static bool IsFuel(GearItem gi)
	{
		if (gi.m_FuelSourceItem != null)
		{
			if (gi.m_FuelSourceItem.m_IsTinder)
			{
				return TinderIsFuel;
			}
			return true;
		}
		return false;
	}

	private static bool IsLooseFuel(GearItem gi, PlayerManager pm)
	{
		if (!IsFuel(gi))
		{
			return false;
		}
		if (gi.m_InPlayerInventory || gi.m_InsideContainer || !gi.gameObject.activeInHierarchy)
		{
			return false;
		}
		if (gi.IsAttachedToPlacePoint())
		{
			return false;
		}
		if (pm != null && (Same(pm.m_ObjectToPlaceGearItem, gi) || Same(pm.m_ItemInHands, gi)))
		{
			return false;
		}
		return true;
	}

	private static bool Same(GearItem a, GearItem b)
	{
		if (a != null && b != null)
		{
			return a.Pointer == b.Pointer;
		}
		return false;
	}

	private static int Units(GearItem gi)
	{
		if (!(gi.m_StackableItem != null))
		{
			return 1;
		}
		return gi.m_StackableItem.m_Units;
	}

	private static string Refusal(Watched w, GearItem gi)
	{
		FuelSourceItem fuelSourceItem = gi.m_FuelSourceItem;
		Fire fire = w.Fire;
		bool flag = w.Kind == FireKind.Forge;
		if (fire.FireShouldBlowOutFromWind())
		{
			return Localization.Get("GAMEPLAY_TooWindyToAddFuel");
		}
		if (!flag || fuelSourceItem.m_FireAgeMinutesBeforeAdding <= 0f)
		{
			float num = fuelSourceItem.GetModifiedBurnDurationHours(gi.GetNormalizedCondition()) + fire.GetRemainingLifeTimeSeconds() / 3600f;
			FireManager fireManagerComponent = GameManager.GetFireManagerComponent();
			if (fireManagerComponent != null && num > fireManagerComponent.m_MaxDurationHoursOfFire)
			{
				return Localization.Get("GAMEPLAY_CannotAddMoreFuel");
			}
		}
		int num2 = Mathf.CeilToInt(fuelSourceItem.m_FireAgeMinutesBeforeAdding - fire.m_ElapsedOnTODSeconds / 60f);
		if (num2 >= 1)
		{
			return Localization.Get("GAMEPLAY_BurnTimeNeededForCoal").Replace("{minutes}", num2.ToString());
		}
		return null;
	}

	private static bool CanTakeNow(Watched w, GearItem gi)
	{
		if (Refusal(w, gi) == null)
		{
			if (IsUnreadBook(gi))
			{
				return Main.Settings.Books == BookRule.Burn;
			}
			return true;
		}
		return false;
	}

	private static void Pull(Watched w, GearItem gi, FirewoodBins.ExitPlan found = null)
	{
		Vector3 vector = PointOf(gi);
		(Vector3 centre, Quaternion rotation, Vector3 half) tuple = BoxOf(gi);
		Vector3 item = tuple.centre;
		Quaternion item2 = tuple.rotation;
		Vector3 item3 = tuple.half;
		FirewoodBins.ExitPlan exitPlan = found ?? (FirewoodBins.InBin(vector) ? FirewoodBins.ExitPath(item, item2, item3, TowardOf(w)) : null);
		if (exitPlan?.Points != null && exitPlan.Points.Count > 0)
		{
			vector = exitPlan.Points[exitPlan.Points.Count - 1];
		}
		Quaternion? turnTo = ((exitPlan != null && exitPlan.TurnPoints > 0) ? new Quaternion?(exitPlan.Rotation * Quaternion.Inverse(item2) * gi.transform.rotation) : null);
		if (w.Funnel != null && w.Funnel.IsCone)
		{
			Fly(gi, w, exitPlan, turnTo, StovePath(w.Funnel, vector), 2);
		}
		else if (FireZone.IsBarrel(w.Fi))
		{
			Fly(gi, w, exitPlan, turnTo, BarrelPath(w.Fi.transform, vector), 2);
		}
		else if (w.Kind == FireKind.Campfire)
		{
			Vector3 vector2 = w.Zone.Frame.position + Vector3.up * 0.25f;
			Vector3 item4 = (vector + vector2) / 2f;
			item4.y = Math.Max(vector.y, vector2.y) + 0.4f;
			Fly(gi, w, exitPlan, turnTo, new List<Vector3> { item4, vector2 }, 1);
		}
		else
		{
			Vector3 center = w.Zone.Center;
			Fly(gi, w, exitPlan, turnTo, new List<Vector3>
			{
				(vector + center) / 2f + Vector3.up * 0.3f,
				center
			}, 1);
		}
	}

	private static Vector3 TowardOf(Watched w)
	{
		if (w.Funnel == null)
		{
			return w.Zone.Center;
		}
		return w.Funnel.Center;
	}

	private static void Fly(GearItem gi, Watched w, FirewoodBins.ExitPlan exit, Quaternion? turnTo, List<Vector3> path, int freeEnd)
	{
		List<Vector3> list = new List<Vector3>();
		int valueOrDefault = (exit?.Points?.Count).GetValueOrDefault();
		if (valueOrDefault > 0)
		{
			list.AddRange(exit.Points);
		}
		list.AddRange(path);
		ObjectAnim objectAnim = ((w.Stove != null) ? w.Stove.m_ObjectAnim : null);
		Transform transform = ((objectAnim != null && objectAnim.m_Target != null) ? objectAnim.m_Target.transform : null);
		if (FuelFlight.Start(gi, list, Main.Settings.PullSpeed, (w.Stove != null) ? w.Stove.Pointer : IntPtr.Zero, w.Fi.gameObject, freeEnd, valueOrDefault, turnTo, turnTo.HasValue ? exit.TurnPoints : 0, FirewoodBins.RootOf(PointOf(gi)), transform))
		{
			soundOnArrivalFrame = Time.frameCount;
		}
	}

	public static void FeedSound(GameObject at)
	{
		if (!(at == null) && !(Time.time - lastSound <= 0.2f) && InterfaceManager.TryGetPanel<Panel_FeedFire>(out var panel) && !(panel == null) && !string.IsNullOrEmpty(panel.m_FeedFireAudio))
		{
			GameAudioManager.PlaySound(panel.m_FeedFireAudio, at);
			lastSound = Time.time;
		}
	}

	private static List<Vector3> BarrelPath(Transform barrel, Vector3 from)
	{
		Vector3 position = barrel.position;
		Vector3 vector = new Vector3(from.x - position.x, 0f, from.z - position.z);
		vector = ((vector.sqrMagnitude > 0.0001f) ? vector.normalized : barrel.forward);
		Vector3 up = Vector3.up;
		return new List<Vector3>
		{
			position + vector * 0.45f + up * 1.35f,
			position + vector * 0.27f + up * 1.15f,
			position + vector * 0.22f + up * 0.55f
		};
	}

	private static List<Vector3> StovePath(FireZone funnel, Vector3 from)
	{
		Quaternion yaw = funnel.Yaw;
		Vector3 origin = funnel.Frame.position;
		Vector3 offset = funnel.Offset;
		Vector3 normalized = new Vector3(funnel.Axis.x, 0f, funnel.Axis.z).normalized;
		Vector3 vector = funnel.Local(from);
		List<Vector3> list = new List<Vector3>();
		float num = Vector3.Dot(vector - offset, normalized);
		Vector3 rhs = Vector3.Cross(Vector3.up, normalized);
		float value = Vector3.Dot(vector - offset, rhs);
		if (num < 0.7f || Math.Abs(value) > 0.25f)
		{
			Vector3 local2 = vector + normalized * Math.Max(0f, 0.7f - num);
			local2.y = Math.Max(vector.y, offset.y - 0.2f);
			list.Add(World(local2));
		}
		list.Add(World(offset + normalized * 0.35f));
		list.Add(World(offset));
		list.Add(World(offset - normalized * 0.3f));
		return list;
		Vector3 World(Vector3 local)
		{
			return origin + yaw * local;
		}
	}

	private static bool IsUnreadBook(GearItem gi)
	{
		ResearchItem researchItem = gi.m_ResearchItem;
		if (researchItem != null)
		{
			return researchItem.m_ElapsedHours < researchItem.GetTimeRequirementHoursPrecise();
		}
		return false;
	}

	private static void Tell(string message)
	{
		if (Main.Settings.messages)
		{
			HUDMessage.AddMessage(message);
		}
	}

	private static void AskAboutBook(Watched w, GearItem gi, bool held)
	{
		if (!w.Asked.Add(gi.Pointer) || !InterfaceManager.TryGetPanel<Panel_Confirmation>(out var panel) || panel == null)
		{
			return;
		}
		Action @delegate = delegate
		{
			try
			{
				if (!(gi == null) && !(w.Fi == null) && w.Burning && (held || (IsLooseFuel(gi, GameManager.GetPlayerManagerComponent()) && w.Zone.Contains(PointOf(gi)))))
				{
					string text = Refusal(w, gi);
					if (text != null)
					{
						Tell(text);
					}
					else
					{
						Feed(w, gi);
					}
				}
			}
			catch (Exception ex)
			{
				Once.Warn("book", "Burning the book failed: " + ex);
			}
		};
		panel.ShowBurnResearchNotification(DelegateSupport.ConvertDelegate<Panel_Confirmation.CallbackDelegate>(@delegate));
		Verbose.Log("Asked about burning the unread book " + gi.name);
	}

	private static void Feed(Watched w, GearItem gi)
	{
		if (soundOnArrivalFrame != Time.frameCount)
		{
			FeedSound(w.Fi.gameObject);
		}
		w.Fire.AddFuel(gi, w.Kind == FireKind.Forge);
		int num = Units(gi);
		bool inPlayerInventory = gi.m_InPlayerInventory;
		Verbose.Log($"{gi.name} went into the {w.Kind} '{w.Fi.gameObject.name}' ({num} piece(s), {(inPlayerInventory ? "carried" : "in the world")}); fire now {w.Fire.GetRemainingLifeTimeHours():0.00} h");
		if (inPlayerInventory)
		{
			GameManager.GetPlayerManagerComponent().ConsumeUnitFromInventory(gi.gameObject);
		}
		else if (num > 1)
		{
			gi.m_StackableItem.m_Units = num - 1;
		}
		else
		{
			GearManager.DestroyGearObject(gi);
		}
	}
}
