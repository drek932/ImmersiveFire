#define VERBOSE
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.AddressableAssets;
using Il2CppTLD.Gear;
using Il2CppTLD.Interactions;
using Il2CppTLD.PDID;
using Il2CppTLD.Placement;
using ImmersiveShared;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;

namespace ImmersiveFire;

internal static class FirewoodBins
{
	private sealed class Bin
	{
		public Transform Root;

		public Transform Lid;

		public Quaternion Closed;

		public Quaternion Wide;

		public float T;

		public bool Want;

		public bool AutoOpened;

		public float AutoUntil;

		public float OpenedAt = -1f;

		public Vector3 InsideMin;

		public Vector3 InsideMax;

		public Vector3 BodyMin;

		public Vector3 BodyMax;

		public Vector3 OpenMin;

		public Vector3 OpenMax;

		public Vector3 LidOpenMin;

		public Vector3 LidOpenMax;

		public bool HasLid;

		public Vector3 HingeAxis = Vector3.right;

		public Vector3 HingeAt;

		public Vector3 ToLid;

		public int Mask;

		public readonly System.Collections.Generic.List<SimpleInteraction> Handles = new System.Collections.Generic.List<SimpleInteraction>();

		public bool Active
		{
			get
			{
				if (Root != null)
				{
					return Root.gameObject.activeInHierarchy;
				}
				return false;
			}
		}
	}

	public sealed class ExitPlan
	{
		public System.Collections.Generic.List<Vector3> Points;

		public Quaternion Rotation;

		public int TurnPoints;

		public bool Found;
	}

	[HarmonyPatch(typeof(TextureCache), "GetAddressableTexture", new Type[] { typeof(AssetReferenceTexture2D) })]
	private static class IconTexture
	{
		private static bool Prefix(AssetReferenceTexture2D assetReference, ref Texture2D __result)
		{
			if (icon == null || assetReference == null || ((AssetReference)assetReference).AssetGUID != "8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6c")
			{
				return true;
			}
			__result = icon;
			return false;
		}
	}

	[HarmonyPatch(typeof(TextureCache), "GetAddressableTexture", new Type[] { typeof(string) })]
	private static class CraftIconTexture
	{
		private static bool Prefix(string name, ref Texture2D __result)
		{
			if (craftIcon == null || name == null || name.IndexOf("DECO_FirewoodBin_IMFI", StringComparison.Ordinal) < 0)
			{
				return true;
			}
			__result = craftIcon;
			return false;
		}
	}

	[HarmonyPatch(typeof(BlueprintData), "GetCraftingIcon")]
	private static class CraftingIcon
	{
		private static void Postfix(BlueprintData __instance, ref Texture2D __result)
		{
			if (craftIcon != null && IsOurs(__instance))
			{
				__result = craftIcon;
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "AddStackedGearItemsToChain")]
	private static class CarryInside
	{
		private static void Postfix(DecorationItem item, Il2CppSystem.Collections.Generic.List<PlayerManager.DecorationItemPlacementInfo> chain)
		{
			try
			{
				if (item == null || chain == null || !bins.TryGetValue(item.gameObject.Pointer, out var value) || value.Root == null)
				{
					return;
				}
				HashSet<IntPtr> hashSet = new HashSet<IntPtr>();
				for (int i = 0; i < chain.Count; i++)
				{
					Transform itemTransform = chain[i].m_ItemTransform;
					if (itemTransform != null)
					{
						hashSet.Add(itemTransform.Pointer);
					}
				}
				if (gearLayerMask < 0)
				{
					gearLayerMask = 1 << LayerMask.NameToLayer("Gear");
				}
				Vector3 position = (value.InsideMin + value.InsideMax) / 2f;
				Vector3 halfExtents = Vector3.Scale((value.InsideMax - value.InsideMin) / 2f, value.Root.lossyScale);
				int num = 0;
				foreach (Collider item2 in Physics.OverlapBox(value.Root.TransformPoint(position), halfExtents, value.Root.rotation, gearLayerMask, QueryTriggerInteraction.Collide))
				{
					GearItem gearItem = ((item2 != null) ? item2.GetComponentInParent<GearItem>() : null);
					if (!(gearItem == null) && !hashSet.Contains(gearItem.transform.Pointer) && Contains(value, item2.bounds.center))
					{
						chain.Add(new PlayerManager.DecorationItemPlacementInfo(gearItem, item));
						hashSet.Add(gearItem.transform.Pointer);
						num++;
					}
				}
				Main.Log.Msg($"[Bin] Moving a firewood bin: {num} more item(s) lying inside go along (the game's chain had {chain.Count - num})");
			}
			catch (Exception ex)
			{
				Once.Warn("bin:carry", "Firewood bin contents: " + ex);
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "InstantiateCraftedItemInPlayerInventory")]
	private static class CraftedIntoInventory
	{
		private static bool Prefix(ICraftingResult result, PlayerManager.InventoryInstantiateFlags flags, ref ICraftingResult __result)
		{
			if (!IsTemplate(result))
			{
				return true;
			}
			try
			{
				DecorationItem decorationItem = NewFurniture(GameManager.GetPlayerTransform());
				if (decorationItem == null)
				{
					return true;
				}
				GameManager.GetInventoryComponent().AddDecoration(decorationItem, flags);
				__result = new ICraftingResult(decorationItem.Pointer);
				Main.Log.Msg($"[Bin] Crafted: a firewood bin in the inventory ({flags})");
				return false;
			}
			catch (Exception ex)
			{
				Main.Log.Error("[Bin] Crafting a firewood bin into the inventory failed: " + ex);
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "InstantiateDecorationItemInPlayerInventory")]
	private static class IntoInventory
	{
		private static bool Prefix(DecorationItem decorationItem, PlayerManager.InventoryInstantiateFlags flags, ref DecorationItem __result)
		{
			if (!IsTemplate(decorationItem))
			{
				return true;
			}
			try
			{
				DecorationItem decorationItem2 = NewFurniture(GameManager.GetPlayerTransform());
				if (decorationItem2 == null)
				{
					return true;
				}
				GameManager.GetInventoryComponent().AddDecoration(decorationItem2, flags);
				__result = decorationItem2;
				Main.Log.Msg($"[Bin] A firewood bin into the inventory ({flags})");
				return false;
			}
			catch (Exception ex)
			{
				Main.Log.Error("[Bin] A firewood bin into the inventory failed: " + ex);
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(Container), "InstantiateCraftedItem")]
	private static class CraftedIntoContainer
	{
		private static bool Prefix(Container __instance, ICraftingResult craftedItem, ref ICraftingResult __result)
		{
			if (!IsTemplate(craftedItem))
			{
				return true;
			}
			try
			{
				DecorationItem decorationItem = NewFurniture(__instance.transform);
				if (decorationItem == null)
				{
					return true;
				}
				if (string.IsNullOrEmpty(ObjectGuid.GetGuidFromGameObject(decorationItem.gameObject, warnIfNoPDID: false)))
				{
					ObjectGuid.MaybeAttachObjectGuidAndRegister(decorationItem.gameObject, PdidTable.GenerateNewID());
				}
				__instance.AddDecorationItem(decorationItem);
				__result = new ICraftingResult(decorationItem.Pointer);
				Main.Log.Msg("[Bin] Crafted: a firewood bin in '" + __instance.name + "'");
				return false;
			}
			catch (Exception ex)
			{
				Main.Log.Error("[Bin] Crafting a firewood bin into a container failed: " + ex);
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(Placeable), "FindOrCreateAndDeserialize")]
	private static class Restore
	{
		private static bool Prefix(string guid, PlaceableSaveData data, ref Placeable __result)
		{
			try
			{
				AssetReferencePlaceable assetReferencePlaceable = data?.m_Addressable;
				if (assetReferencePlaceable == null || ((AssetReference)assetReferencePlaceable).AssetGUID != "8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6b")
				{
					return true;
				}
				if (!string.IsNullOrEmpty(guid) && PdidTable.GetGameObject(guid) != null)
				{
					return true;
				}
				DecorationItem decorationItem = NewFurniture(null);
				if (decorationItem == null)
				{
					Main.Log.Error("[Bin] Saved firewood bin " + guid + " can't be made");
					return true;
				}
				Placeable component = decorationItem.GetComponent<Placeable>();
				GameObject gameObject = PlaceableManager.FindOrCreateCategoryRoot();
				if (gameObject != null)
				{
					decorationItem.transform.parent = gameObject.transform;
				}
				if (string.IsNullOrEmpty(guid))
				{
					guid = PdidTable.GenerateNewID();
				}
				component.Deserialize(guid, data);
				__result = component;
				Main.Log.Msg("[Bin] Firewood bin " + guid + " restored at " + FireZone.V(decorationItem.transform.position) + (decorationItem.gameObject.activeSelf ? "" : " (not active: inventory or container)"));
				return false;
			}
			catch (Exception ex)
			{
				Main.Log.Error("[Bin] Restoring a firewood bin failed: " + ex);
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(DecorationItem), "Awake")]
	private static class Awake
	{
		private static void Postfix(DecorationItem __instance)
		{
			try
			{
				if (IsFurniture(__instance))
				{
					Make(__instance.gameObject);
				}
			}
			catch (Exception ex)
			{
				Once.Warn("bin:awake", "Firewood bin: " + ex);
			}
		}
	}

	[HarmonyPatch(typeof(BlueprintData), "GetDisplayedNameWithCount")]
	private static class BlueprintName
	{
		private static void Postfix(BlueprintData __instance, ref string __result)
		{
			if (IsOurs(__instance))
			{
				__result = Localization.Get("IMFI_BIN_NAME");
			}
		}
	}

	private const float WideAngle = 110f;

	private const float AutoCloseAfter = 1.5f;

	private const float OpenSlower = 1.5f;

	private const float PauseWhenOpen = 0.25f;

	private const float RimClearance = 0.15f;

	private const float WallClearance = 0.35f;

	private const float LidClearance = 0.08f;

	private static readonly System.Collections.Generic.Dictionary<IntPtr, Bin> bins = new System.Collections.Generic.Dictionary<IntPtr, Bin>();

	private const float Lift = 0.05f;

	private const float Margin = 0.03f;

	private const float Cell = 0.05f;

	private const float NearStart = 0.15f;

	private const int MaxExpansions = 4000;

	private static string lastHit;

	private static readonly HashSet<IntPtr> profiled = new HashSet<IntPtr>();

	private static readonly (int x, int y, int z)[] Steps = new(int, int, int)[14]
	{
		(1, 0, 0),
		(-1, 0, 0),
		(0, 1, 0),
		(0, -1, 0),
		(0, 0, 1),
		(0, 0, -1),
		(1, 0, 1),
		(1, 0, -1),
		(-1, 0, 1),
		(-1, 0, -1),
		(1, 1, 0),
		(-1, 1, 0),
		(0, 1, 1),
		(0, 1, -1)
	};

	private const string BinAsset = "Assets/Prefabs/Containers/CONTAINER_FirewoodBin.prefab";

	private const string BoxAsset = "Assets/Prefabs/Containers/CONTAINER_Rustic_Storage_Box.prefab";

	private const string SourceBlueprint = "BLUEPRINT_DECO_StorageBox";

	private const string OurBlueprint = "BLUEPRINT_DECO_FirewoodBin_IMFI";

	private const string FurnitureName = "DECO_FirewoodBin_IMFI";

	private const string FurnitureKey = "8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6b";

	private const int Softwood = 6;

	private static GameObject templates;

	private static DecorationItem template;

	private static bool noTemplate;

	private static Vector3 lidAxis = Vector3.right;

	private static float lidGameAngle;

	private static string openAudio;

	private static string closeAudio;

	private const string IconKey = "8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6c";

	private const string CraftIconKey = "8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6d";

	private const int IconSize = 256;

	private const int DrawnIconSize = 512;

	private const int CraftIconSize = 256;

	private static Texture2D icon;

	private static Texture2D craftIcon;

	private static int gearLayerMask = -1;

	private static UnityAction<BaseInteraction> onInteract;

	private static float nextBlueprintCheck;

	private static bool noBlueprint;

	private static float nextScan;

	private static UnityAction<BaseInteraction> OnInteract => onInteract ?? (onInteract = DelegateSupport.ConvertDelegate<UnityAction<BaseInteraction>>(new Action<BaseInteraction>(Interacted)));

	public static void Reset()
	{
		System.Collections.Generic.List<IntPtr> list = new System.Collections.Generic.List<IntPtr>();
		foreach (System.Collections.Generic.KeyValuePair<IntPtr, Bin> bin in bins)
		{
			if (bin.Value.Root == null)
			{
				list.Add(bin.Key);
			}
		}
		foreach (IntPtr item in list)
		{
			bins.Remove(item);
		}
	}

	private static void Make(GameObject go)
	{
		if (go == null || bins.ContainsKey(go.Pointer))
		{
			return;
		}
		Transform transform = go.transform;
		Transform transform2 = transform.Find("Hinge");
		Bin bin = new Bin
		{
			Root = transform,
			Lid = transform2
		};
		if (transform2 != null)
		{
			bin.Closed = transform2.localRotation;
			bin.Wide = bin.Closed * Quaternion.AngleAxis(Math.Max(110f, lidGameAngle), lidAxis);
		}
		if (BodyBox(go, transform2, out var min, out var max))
		{
			bin.BodyMin = min;
			bin.BodyMax = max;
			bin.InsideMin = min + new Vector3(0.05f, 0.03f, 0.05f);
			bin.InsideMax = max - new Vector3(0.05f, -0.1f, 0.05f);
			bin.OpenMin = bin.InsideMin;
			bin.OpenMax = bin.InsideMax;
		}
		if (transform2 != null)
		{
			System.Collections.Generic.List<MeshFilter> list = new System.Collections.Generic.List<MeshFilter>();
			foreach (MeshFilter componentsInChild in transform2.GetComponentsInChildren<MeshFilter>(includeInactive: true))
			{
				if (componentsInChild != null && componentsInChild.sharedMesh != null)
				{
					list.Add(componentsInChild);
				}
			}
			if (list.Count > 0)
			{
				LocalBox(list, transform, out var min2, out var max2);
				bin.OpenMin = new Vector3(Math.Max(min2.x, bin.InsideMin.x), 0f, Math.Max(min2.z, bin.InsideMin.z));
				bin.OpenMax = new Vector3(Math.Min(max2.x, bin.InsideMax.x), 0f, Math.Min(max2.z, bin.InsideMax.z));
				Vector3 vector = transform.InverseTransformPoint(transform2.position);
				Quaternion quaternion = Quaternion.Inverse(transform.rotation) * transform2.parent.rotation * (bin.Wide * Quaternion.Inverse(bin.Closed)) * Quaternion.Inverse(transform2.parent.rotation) * transform.rotation;
				for (int i = 0; i < 8; i++)
				{
					Vector3 vector2 = new Vector3(((i & 1) == 0) ? min2.x : max2.x, ((i & 2) == 0) ? min2.y : max2.y, ((i & 4) == 0) ? min2.z : max2.z);
					Vector3 vector3 = vector + quaternion * (vector2 - vector);
					bin.LidOpenMin = ((i == 0) ? vector3 : Vector3.Min(bin.LidOpenMin, vector3));
					bin.LidOpenMax = ((i == 0) ? vector3 : Vector3.Max(bin.LidOpenMax, vector3));
				}
				bin.HasLid = true;
				Vector3 vector4 = transform.InverseTransformDirection(transform2.parent.TransformDirection(bin.Closed * lidAxis));
				vector4.y = 0f;
				if (vector4.sqrMagnitude > 0.0001f)
				{
					bin.HingeAxis = vector4.normalized;
				}
				Vector3 vector5 = (min2 + max2) / 2f - vector;
				vector5.y = 0f;
				if (vector5.sqrMagnitude > 0.0001f)
				{
					bin.ToLid = vector5.normalized;
				}
				bin.HingeAt = vector;
			}
		}
		foreach (Collider componentsInChild2 in go.GetComponentsInChildren<Collider>(includeInactive: true))
		{
			if (componentsInChild2 != null && !componentsInChild2.isTrigger)
			{
				bin.Mask |= 1 << componentsInChild2.gameObject.layer;
			}
		}
		foreach (SimpleInteraction componentsInChild3 in go.GetComponentsInChildren<SimpleInteraction>(includeInactive: true))
		{
			if (!(componentsInChild3 == null))
			{
				if (componentsInChild3.m_EventEntries == null)
				{
					componentsInChild3.m_EventEntries = new Il2CppSystem.Collections.Generic.List<InteractionEventEntry>();
				}
				componentsInChild3.AddEventCallback(InteractionEventType.PerformInteraction, OnInteract);
				bin.Handles.Add(componentsInChild3);
			}
		}
		SetHoverText(bin);
		bins[go.Pointer] = bin;
		Main.Log.Msg($"[Bin] '{go.name}' is a firewood bin: hinge '{((transform2 != null) ? transform2.name : "none")}', game {lidGameAngle:0} deg around {FireZone.V(lidAxis)}, opens {Math.Max(110f, lidGameAngle):0}; inside {FireZone.V(bin.InsideMin)}..{FireZone.V(bin.InsideMax)}, opening {FireZone.V(bin.OpenMin)}..{FireZone.V(bin.OpenMax)}, open lid {FireZone.V(bin.LidOpenMin)}..{FireZone.V(bin.LidOpenMax)}; {bin.Handles.Count} interaction(s); scale {FireZone.V(transform.lossyScale)}");
	}

	private static void LocalBox(System.Collections.Generic.List<MeshFilter> parts, Transform space, out Vector3 min, out Vector3 max)
	{
		bool flag = false;
		min = (max = Vector3.zero);
		foreach (MeshFilter part in parts)
		{
			Bounds bounds = part.sharedMesh.bounds;
			for (int i = 0; i < 8; i++)
			{
				Vector3 position = new Vector3(((i & 1) == 0) ? bounds.min.x : bounds.max.x, ((i & 2) == 0) ? bounds.min.y : bounds.max.y, ((i & 4) == 0) ? bounds.min.z : bounds.max.z);
				Vector3 vector = space.InverseTransformPoint(part.transform.TransformPoint(position));
				if (!flag)
				{
					min = (max = vector);
					flag = true;
				}
				else
				{
					min = Vector3.Min(min, vector);
					max = Vector3.Max(max, vector);
				}
			}
		}
	}

	private static bool BodyBox(GameObject go, Transform lid, out Vector3 min, out Vector3 max)
	{
		bool flag = false;
		min = (max = Vector3.zero);
		Transform transform = go.transform;
		foreach (MeshFilter componentsInChild in go.GetComponentsInChildren<MeshFilter>(includeInactive: false))
		{
			if (componentsInChild == null || componentsInChild.sharedMesh == null || (lid != null && componentsInChild.transform.IsChildOf(lid)))
			{
				continue;
			}
			string name = componentsInChild.name;
			int num = name.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
			if (num >= 0 && !name.Substring(num).StartsWith("_LOD0", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			Bounds bounds = componentsInChild.sharedMesh.bounds;
			for (int i = 0; i < 8; i++)
			{
				Vector3 position = new Vector3(((i & 1) == 0) ? bounds.min.x : bounds.max.x, ((i & 2) == 0) ? bounds.min.y : bounds.max.y, ((i & 4) == 0) ? bounds.min.z : bounds.max.z);
				Vector3 vector = transform.InverseTransformPoint(componentsInChild.transform.TransformPoint(position));
				if (!flag)
				{
					min = (max = vector);
					flag = true;
				}
				else
				{
					min = Vector3.Min(min, vector);
					max = Vector3.Max(max, vector);
				}
			}
		}
		return flag;
	}

	private static void Toggle(Bin bin)
	{
		SetWant(bin, !bin.Want, auto: false);
	}

	private static void SetWant(Bin bin, bool want, bool auto)
	{
		bin.Want = want;
		bin.AutoOpened = want && auto;
		string text = (want ? openAudio : closeAudio);
		if (!string.IsNullOrEmpty(text) && bin.Root != null)
		{
			GameAudioManager.PlaySound(text, bin.Root.gameObject);
		}
		SetHoverText(bin);
	}

	private static void SetHoverText(Bin bin)
	{
		string text = (bin.Want ? "IMFI_BIN_CLOSE" : "IMFI_BIN_OPEN");
		string hoverText_k__BackingField = Localization.Get(text);
		foreach (SimpleInteraction handle in bin.Handles)
		{
			if (!(handle == null))
			{
				if (handle.m_DefaultHoverText == null)
				{
					handle.m_DefaultHoverText = new LocalizedString();
				}
				handle.m_DefaultHoverText.m_LocalizationID = text;
				handle._HoverText_k__BackingField = hoverText_k__BackingField;
			}
		}
	}

	public static void LateUpdate()
	{
		foreach (Bin value in bins.Values)
		{
			if (!(value.Lid == null) && !(value.Root == null))
			{
				if (value.AutoOpened && value.Want && Time.time > value.AutoUntil)
				{
					SetWant(value, want: false, auto: false);
				}
				float num = (value.Want ? 1.5f : 1f) * 0.3f * Main.Settings.DoorTurnSlowDown;
				float target = (value.Want ? 1f : 0f);
				value.T = Mathf.MoveTowards(value.T, target, Time.deltaTime / num);
				if (!value.Want)
				{
					value.OpenedAt = -1f;
				}
				else if (value.T >= 1f && value.OpenedAt < 0f)
				{
					value.OpenedAt = Time.time;
				}
				float t = value.T * value.T * (3f - 2f * value.T);
				value.Lid.localRotation = Quaternion.Slerp(value.Closed, value.Wide, t);
			}
		}
	}

	private static Bin Holding(Vector3 point)
	{
		foreach (Bin value in bins.Values)
		{
			if (value.Active && !IsBeingPlaced(value) && Contains(value, point))
			{
				return value;
			}
		}
		return null;
	}

	private static bool Contains(Bin bin, Vector3 point)
	{
		Vector3 vector = bin.Root.InverseTransformPoint(point);
		if (vector.x >= bin.InsideMin.x && vector.x <= bin.InsideMax.x && vector.y >= bin.InsideMin.y && vector.y <= bin.InsideMax.y && vector.z >= bin.InsideMin.z)
		{
			return vector.z <= bin.InsideMax.z;
		}
		return false;
	}

	public static bool InBin(Vector3 point)
	{
		return Holding(point) != null;
	}

	public static bool Inside(Vector3 point)
	{
		foreach (Bin value in bins.Values)
		{
			if (value.Active && Contains(value, point))
			{
				return true;
			}
		}
		return false;
	}

	public static float TowardsLid(Vector3 point)
	{
		foreach (Bin value in bins.Values)
		{
			if (value.Active && Contains(value, point))
			{
				return Vector3.Dot(value.Root.InverseTransformPoint(point) - value.HingeAt, value.ToLid);
			}
		}
		return 0f;
	}

	public static Transform BinOf(Vector3 point)
	{
		foreach (Bin value in bins.Values)
		{
			if (value.Active && Contains(value, point))
			{
				return value.Root;
			}
		}
		return null;
	}

	public static bool BeingMoved(Vector3 point)
	{
		foreach (Bin value in bins.Values)
		{
			if (value.Active && IsBeingPlaced(value) && Contains(value, point))
			{
				return true;
			}
		}
		return false;
	}

	public static System.Collections.Generic.IEnumerable<(Vector3 centre, Vector3 half, Quaternion rotation)> Near(Vector3 point, float reach)
	{
		System.Collections.Generic.List<(Vector3, Vector3, Quaternion)> list = new System.Collections.Generic.List<(Vector3, Vector3, Quaternion)>();
		foreach (Bin value in bins.Values)
		{
			if (value.Active && !IsBeingPlaced(value) && !((value.Root.position - point).sqrMagnitude > reach * reach))
			{
				Vector3 position = (value.InsideMin + value.InsideMax) / 2f;
				Vector3 item = Vector3.Scale((value.InsideMax - value.InsideMin) / 2f, value.Root.lossyScale);
				list.Add((value.Root.TransformPoint(position), item, value.Root.rotation));
			}
		}
		return list;
	}

	public static Transform RootOf(Vector3 point)
	{
		return Holding(point)?.Root;
	}

	private static System.Collections.Generic.List<Vector3> ExitByBoxes(Bin bin, Vector3 item, Vector3 toward, Vector3 half)
	{
		Transform root = bin.Root;
		Vector3 vector = root.InverseTransformPoint(item);
		Vector3 vector2 = root.InverseTransformDirection(toward - item);
		vector2.y = 0f;
		vector2 = ((vector2.sqrMagnitude > 0.0001f) ? vector2.normalized : Vector3.back);
		float y = bin.BodyMax.y;
		Vector3 vector3 = new Vector3(Along(root.right, half), Along(root.up, half), Along(root.forward, half));
		vector3 = new Vector3(vector3.x / Safe(root.lossyScale.x), vector3.y / Safe(root.lossyScale.y), vector3.z / Safe(root.lossyScale.z));
		float x = Into(vector.x, bin.OpenMin.x + vector3.x, bin.OpenMax.x - vector3.x);
		float z = Into(vector.z, bin.OpenMin.z + vector3.z, bin.OpenMax.z - vector3.z);
		if (bin.HasLid)
		{
			ClearOfLid(bin, vector3, ref x, ref z);
		}
		Vector3 vector4 = new Vector3(x, y + vector3.y + 0.15f, z);
		float val = ((vector2.x > 0f) ? ((bin.BodyMax.x - vector4.x) / vector2.x) : ((vector2.x < 0f) ? ((bin.BodyMin.x - vector4.x) / vector2.x) : float.MaxValue));
		float val2 = ((vector2.z > 0f) ? ((bin.BodyMax.z - vector4.z) / vector2.z) : ((vector2.z < 0f) ? ((bin.BodyMin.z - vector4.z) / vector2.z) : float.MaxValue));
		float num = Math.Max(0f, Math.Min(val, val2)) + Math.Abs(vector2.x) * vector3.x + Math.Abs(vector2.z) * vector3.z + 0.35f;
		Vector3 vector5 = vector4 + vector2 * num;
		Vector3 vector6 = new Vector3(vector3.x + 0.08f, 0f, vector3.z + 0.08f);
		int num2;
		float y2;
		if (bin.HasLid)
		{
			num2 = (Crosses(vector4, vector5, bin.LidOpenMin - vector6, bin.LidOpenMax + vector6) ? 1 : 0);
			if (num2 != 0)
			{
				y2 = Math.Max(vector4.y, bin.LidOpenMax.y + vector3.y + 0.15f);
				goto IL_02eb;
			}
		}
		else
		{
			num2 = 0;
		}
		y2 = vector4.y;
		goto IL_02eb;
		IL_02eb:
		float y3 = (vector5.y = y2);
		Vector3 position = new Vector3(vector4.x, Math.Max(vector.y, bin.InsideMin.y + vector3.y), vector4.z);
		System.Collections.Generic.List<Vector3> list = new System.Collections.Generic.List<Vector3>();
		if (new Vector2(position.x - vector.x, position.z - vector.z).sqrMagnitude > 0.0025f)
		{
			list.Add(root.TransformPoint(position));
		}
		list.Add(root.TransformPoint(vector4));
		if (num2 != 0)
		{
			list.Add(root.TransformPoint(new Vector3(vector4.x, y3, vector4.z)));
		}
		list.Add(root.TransformPoint(vector5));
		return list;
	}

	private static void ClearOfLid(Bin bin, Vector3 h, ref float x, ref float z)
	{
		float num = bin.LidOpenMin.x - 0.08f - h.x;
		float num2 = bin.LidOpenMax.x + 0.08f + h.x;
		float num3 = bin.LidOpenMin.z - 0.08f - h.z;
		float num4 = bin.LidOpenMax.z + 0.08f + h.z;
		if (x <= num || x >= num2 || z <= num3 || z >= num4)
		{
			return;
		}
		float num5 = bin.OpenMin.x + h.x;
		float num6 = bin.OpenMax.x - h.x;
		float num7 = bin.OpenMin.z + h.z;
		float num8 = bin.OpenMax.z - h.z;
		(float, bool, bool, float)[] obj = new(float, bool, bool, float)[4]
		{
			(x - num, num >= num5, true, num),
			(num2 - x, num2 <= num6, true, num2),
			(z - num3, num3 >= num7, false, num3),
			(num4 - z, num4 <= num8, false, num4)
		};
		(float, bool, bool, float) tuple = obj[0];
		(float, bool, bool, float)[] array = obj;
		for (int i = 0; i < array.Length; i++)
		{
			(float, bool, bool, float) tuple2 = array[i];
			if ((tuple2.Item2 && !tuple.Item2) || (tuple2.Item2 == tuple.Item2 && tuple2.Item1 < tuple.Item1))
			{
				tuple = tuple2;
			}
		}
		if (tuple.Item3)
		{
			x = tuple.Item4;
		}
		else
		{
			z = tuple.Item4;
		}
	}

	private static bool Crosses(Vector3 a, Vector3 b, Vector3 min, Vector3 max)
	{
		float t = 0f;
		float t2 = 1f;
		float num = b.x - a.x;
		float num2 = b.z - a.z;
		if (Clip(0f - num, a.x - min.x, ref t, ref t2) && Clip(num, max.x - a.x, ref t, ref t2) && Clip(0f - num2, a.z - min.z, ref t, ref t2) && Clip(num2, max.z - a.z, ref t, ref t2))
		{
			return t <= t2;
		}
		return false;
	}

	private static bool Clip(float p, float q, ref float t0, ref float t1)
	{
		if (Math.Abs(p) < 1E-06f)
		{
			return q >= 0f;
		}
		float num = q / p;
		if (p < 0f)
		{
			if (num > t1)
			{
				return false;
			}
			if (num > t0)
			{
				t0 = num;
			}
		}
		else
		{
			if (num < t0)
			{
				return false;
			}
			if (num < t1)
			{
				t1 = num;
			}
		}
		return true;
	}

	private static float Along(Vector3 axis, Vector3 half)
	{
		return Math.Abs(axis.x) * half.x + Math.Abs(axis.y) * half.y + Math.Abs(axis.z) * half.z;
	}

	private static float Into(float v, float lo, float hi)
	{
		if (!(lo <= hi))
		{
			return (lo + hi) / 2f;
		}
		return Mathf.Clamp(v, lo, hi);
	}

	private static float Safe(float v)
	{
		if (!(Math.Abs(v) < 0.0001f))
		{
			return v;
		}
		return 1f;
	}

	public static bool ReadyFor(Vector3 point)
	{
		Bin bin = Holding(point);
		if (bin == null)
		{
			return true;
		}
		if (!bin.Want)
		{
			SetWant(bin, want: true, auto: true);
		}
		if (bin.AutoOpened)
		{
			bin.AutoUntil = Time.time + 1.5f;
		}
		if (bin.OpenedAt >= 0f)
		{
			return Time.time - bin.OpenedAt >= 0.25f;
		}
		return false;
	}

	private static bool IsBeingPlaced(Bin bin)
	{
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		GameObject gameObject = ((playerManagerComponent != null && playerManagerComponent.IsInPlacementMode()) ? playerManagerComponent.m_ObjectToPlace : null);
		if (gameObject != null)
		{
			return gameObject.Pointer == bin.Root.gameObject.Pointer;
		}
		return false;
	}

	internal static string Describe(GameObject go)
	{
		StringBuilder stringBuilder;
		StringBuilder stringBuilder2 = (stringBuilder = new StringBuilder());
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(10, 2, stringBuilder);
		handler.AppendLiteral("'");
		handler.AppendFormatted(go.name);
		handler.AppendLiteral("' layer ");
		handler.AppendFormatted(LayerMask.LayerToName(go.layer));
		handler.AppendLiteral(":");
		stringBuilder.AppendLine(ref handler);
		Tree(stringBuilder2, go.transform, go.transform, 0);
		return stringBuilder2.ToString();
	}

	private static void Tree(StringBuilder sb, Transform root, Transform t, int depth)
	{
		if (depth > 5)
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler;
		foreach (Component component in t.GetComponents<Component>())
		{
			if (component == null)
			{
				continue;
			}
			string name = component.GetIl2CppType().Name;
			if (!(name == "Transform"))
			{
				stringBuilder.Append(name);
				Collider collider = component.TryCast<Collider>();
				if (collider != null)
				{
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder3 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(3, 3, stringBuilder2);
					handler.AppendLiteral("(");
					handler.AppendFormatted(collider.isTrigger ? "trigger " : "");
					handler.AppendFormatted(LayerMask.LayerToName(collider.gameObject.layer));
					handler.AppendLiteral(" ");
					handler.AppendFormatted(FireZone.V(collider.bounds.size));
					handler.AppendLiteral(")");
					stringBuilder3.Append(ref handler);
				}
				MeshFilter meshFilter = component.TryCast<MeshFilter>();
				if (meshFilter != null && meshFilter.sharedMesh != null)
				{
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder4 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(3, 2, stringBuilder2);
					handler.AppendLiteral("(");
					handler.AppendFormatted(meshFilter.sharedMesh.name);
					handler.AppendLiteral(" ");
					handler.AppendFormatted(FireZone.V(meshFilter.sharedMesh.bounds.size));
					handler.AppendLiteral(")");
					stringBuilder4.Append(ref handler);
				}
				stringBuilder.Append(' ');
			}
		}
		stringBuilder2 = sb;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(6, 4, stringBuilder2);
		handler.AppendFormatted(new string(' ', depth * 2 + 2));
		handler.AppendFormatted(t.name);
		handler.AppendLiteral(" at ");
		handler.AppendFormatted(FireZone.V(root.InverseTransformPoint(t.position)));
		handler.AppendLiteral(": ");
		handler.AppendFormatted(stringBuilder);
		stringBuilder5.AppendLine(ref handler);
		for (int i = 0; i < t.childCount; i++)
		{
			Tree(sb, root, t.GetChild(i), depth + 1);
		}
	}

	public static ExitPlan ExitPath(Vector3 centre, Quaternion rotation, Vector3 half, Vector3 toward)
	{
		Bin bin = Holding(centre);
		if (bin == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			Physics.SyncTransforms();
			Profile(bin);
			Transform root = bin.Root;
			Vector3 vector = root.InverseTransformDirection(toward - centre);
			vector.y = 0f;
			vector = ((vector.sqrMagnitude > 0.0001f) ? vector.normalized : Vector3.back);
			Vector3 vector2 = centre + root.up * 0.05f;
			System.Collections.Generic.List<(string, Quaternion)> list = new System.Collections.Generic.List<(string, Quaternion)> { ("as it lies", rotation) };
			Quaternion quaternion = AlongHinge(bin, rotation, half);
			if (Quaternion.Angle(quaternion, rotation) > 10f)
			{
				list.Add(("along the hinge", quaternion));
			}
			list.Add(("upright", Upright(rotation, half)));
			foreach (var item3 in list)
			{
				string item = item3.Item1;
				Quaternion item2 = item3.Item2;
				Vector3 vector3 = vector2;
				int num = 0;
				if (Quaternion.Angle(item2, rotation) > 1f)
				{
					float num2 = Math.Max(0.12f, Extent(root.up, item2, half) - Extent(root.up, rotation, half) + 0.05f);
					vector3 = vector2 + root.up * num2;
					if (!ClearAlong(bin, vector2, vector3, vector3, rotation, item2, Vector3.Max(half - Vector3.one * 0.03f, half * 0.5f)))
					{
						StringBuilder stringBuilder2 = stringBuilder;
						StringBuilder stringBuilder3 = stringBuilder2;
						StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(22, 2, stringBuilder2);
						handler.AppendLiteral("; ");
						handler.AppendFormatted(item);
						handler.AppendLiteral(": no room to turn (");
						handler.AppendFormatted(lastHit);
						handler.AppendLiteral(")");
						stringBuilder3.Append(ref handler);
						continue;
					}
					num = 2;
				}
				string result;
				System.Collections.Generic.List<Vector3> list2 = Search(bin, vector3, item2, half, vector, out result);
				if (list2 == null)
				{
					StringBuilder stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder4 = stringBuilder2;
					StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(4, 2, stringBuilder2);
					handler.AppendLiteral("; ");
					handler.AppendFormatted(item);
					handler.AppendLiteral(": ");
					handler.AppendFormatted(result);
					stringBuilder4.Append(ref handler);
					continue;
				}
				System.Collections.Generic.List<Vector3> list3 = new System.Collections.Generic.List<Vector3> { vector2 };
				if (num > 0)
				{
					list3.Add(vector3);
				}
				list3.AddRange(list2);
				Verbose.Log($"[Bin] Way out {item}: {result}, {list3.Count} points{stringBuilder}");
				return new ExitPlan
				{
					Points = list3,
					Rotation = item2,
					TurnPoints = num,
					Found = true
				};
			}
		}
		catch (Exception ex)
		{
			Once.Warn("bin:exit", "Firewood bin way out: " + ex);
		}
		if (bin.Root.InverseTransformPoint(centre).y >= bin.BodyMax.y - 0.1f)
		{
			Verbose.Log("[Bin] " + FireZone.V(bin.Root.InverseTransformPoint(centre)) + " is on top of the bin: no way out needed");
			return new ExitPlan
			{
				Points = new System.Collections.Generic.List<Vector3>(),
				Rotation = rotation,
				Found = true
			};
		}
		Main.Log.Msg($"[Bin] No clear way out (item half {FireZone.V(half)}){stringBuilder}: the way by the bin's boxes");
		Vector3 half2 = new Vector3(Extent(Vector3.right, rotation, half), Extent(Vector3.up, rotation, half), Extent(Vector3.forward, rotation, half));
		return new ExitPlan
		{
			Points = ExitByBoxes(bin, centre, toward, half2),
			Rotation = rotation
		};
	}

	private static Quaternion AlongHinge(Bin bin, Quaternion rotation, Vector3 half)
	{
		Vector3 vector = rotation * Longest(half);
		Vector3 vector2 = bin.Root.TransformDirection(bin.HingeAxis);
		return Quaternion.FromToRotation(vector, (Vector3.Dot(vector, vector2) >= 0f) ? vector2 : (-vector2)) * rotation;
	}

	private static Quaternion Upright(Quaternion rotation, Vector3 half)
	{
		Vector3 fromDirection = rotation * Longest(half);
		return Quaternion.FromToRotation(toDirection: (fromDirection.y >= 0f) ? Vector3.up : Vector3.down, fromDirection: fromDirection) * rotation;
	}

	private static Vector3 Longest(Vector3 half)
	{
		if (!(half.x >= half.y) || !(half.x >= half.z))
		{
			if (!(half.y >= half.z))
			{
				return Vector3.forward;
			}
			return Vector3.up;
		}
		return Vector3.right;
	}

	private static float Extent(Vector3 axis, Quaternion rotation, Vector3 half)
	{
		return Math.Abs(Vector3.Dot(axis, rotation * Vector3.right)) * half.x + Math.Abs(Vector3.Dot(axis, rotation * Vector3.up)) * half.y + Math.Abs(Vector3.Dot(axis, rotation * Vector3.forward)) * half.z;
	}

	private static long Key(int x, int y, int z)
	{
		return ((long)(x + 4096) << 26) | ((long)(y + 4096) << 13) | (z + 4096);
	}

	private static System.Collections.Generic.List<Vector3> Search(Bin bin, Vector3 from, Quaternion q, Vector3 half, Vector3 dir, out string result)
	{
		Transform root = bin.Root;
		float s = 0.05f / Safe(root.lossyScale.x);
		Vector3 o = root.InverseTransformPoint(from);
		Vector3 safe = half + Vector3.one * 0.03f;
		float num = Extent(root.TransformDirection(dir), q, half) / Safe(root.lossyScale.x);
		float num2 = Extent(root.up, q, half) / Safe(root.lossyScale.y);
		Vector3 lo = new Vector3(bin.BodyMin.x - num - 0.6f, bin.InsideMin.y, bin.BodyMin.z - num - 0.6f);
		Vector3 hi = new Vector3(bin.BodyMax.x + num + 0.6f, Math.Max(bin.BodyMax.y, bin.HasLid ? bin.LidOpenMax.y : 0f) + num2 + 0.5f, bin.BodyMax.z + num + 0.6f);
		Vector3 mid = (bin.BodyMin + bin.BodyMax) / 2f;
		float goal = Math.Abs(dir.x) * (bin.BodyMax.x - bin.BodyMin.x) / 2f + Math.Abs(dir.z) * (bin.BodyMax.z - bin.BodyMin.z) / 2f + num + 0.35f;
		float rimY = bin.BodyMax.y + num2;
		System.Collections.Generic.Dictionary<long, bool> free = new System.Collections.Generic.Dictionary<long, bool>();
		PriorityQueue<(int, int, int), float> priorityQueue = new PriorityQueue<(int, int, int), float>();
		System.Collections.Generic.Dictionary<long, float> dictionary = new System.Collections.Generic.Dictionary<long, float>();
		System.Collections.Generic.Dictionary<long, long> dictionary2 = new System.Collections.Generic.Dictionary<long, long>();
		System.Collections.Generic.Dictionary<long, (int, int, int)> dictionary3 = new System.Collections.Generic.Dictionary<long, (int, int, int)>();
		long num3 = Key(0, 0, 0);
		dictionary[num3] = 0f;
		dictionary3[num3] = (0, 0, 0);
		priorityQueue.Enqueue((0, 0, 0), Guess(o));
		HashSet<long> hashSet = new HashSet<long>();
		int num4 = 0;
		long num5 = -1L;
		while (priorityQueue.Count > 0 && num4 < 4000)
		{
			(int, int, int) tuple = priorityQueue.Dequeue();
			long num6 = Key(tuple.Item1, tuple.Item2, tuple.Item3);
			if (!hashSet.Add(num6))
			{
				continue;
			}
			Vector3 l2 = Local(tuple.Item1, tuple.Item2, tuple.Item3);
			num4++;
			if (ToGo(l2) <= 0f)
			{
				num5 = num6;
				break;
			}
			float num7 = dictionary[num6];
			(int, int, int)[] steps = Steps;
			for (int i = 0; i < steps.Length; i++)
			{
				(int, int, int) tuple2 = steps[i];
				int item = tuple2.Item1;
				int item2 = tuple2.Item2;
				int item3 = tuple2.Item3;
				int num8 = tuple.Item1 + item;
				int num9 = tuple.Item2 + item2;
				int num10 = tuple.Item3 + item3;
				long key2 = Key(num8, num9, num10);
				if (Free(num8, num9, num10, key2))
				{
					float num11 = num7 + s * (float)Math.Sqrt(item * item + item2 * item2 + item3 * item3);
					if (!dictionary.TryGetValue(key2, out var value) || !(value <= num11))
					{
						dictionary[key2] = num11;
						dictionary2[key2] = num6;
						dictionary3[key2] = (num8, num9, num10);
						priorityQueue.Enqueue((num8, num9, num10), num11 + Guess(Local(num8, num9, num10)));
					}
				}
			}
		}
		if (num5 < 0)
		{
			result = $"no way in {num4} cells ({free.Count} looked at; {lastHit})";
			return null;
		}
		System.Collections.Generic.List<Vector3> list = new System.Collections.Generic.List<Vector3>();
		long num12 = num5;
		while (true)
		{
			var (x2, y2, z2) = dictionary3[num12];
			list.Add(root.TransformPoint(Local(x2, y2, z2)));
			if (num12 == num3)
			{
				break;
			}
			num12 = dictionary2[num12];
		}
		list.Reverse();
		System.Collections.Generic.List<Vector3> list2 = new System.Collections.Generic.List<Vector3>();
		int num13 = 0;
		while (num13 < list.Count - 1)
		{
			int num14 = list.Count - 1;
			while (num14 > num13 + 1 && !ClearSweep(bin, list[num13], list[num14], num13 == 0, half, safe, q, o))
			{
				num14--;
			}
			list2.Add(list[num14]);
			num13 = num14;
		}
		result = $"{num4} cells searched, {list.Count} on the way, {list2.Count} straight legs";
		return list2;
		bool Free(int x, int y, int z, long key)
		{
			if (free.TryGetValue(key, out var value2))
			{
				return value2;
			}
			Vector3 vector = Local(x, y, z);
			bool flag = vector.x >= lo.x && vector.x <= hi.x && vector.y >= lo.y && vector.y <= hi.y && vector.z >= lo.z && vector.z <= hi.z && !OverlapsBin(bin, root.TransformPoint(vector), Near(vector, o) ? half : safe, q);
			free[key] = flag;
			return flag;
		}
		float Guess(Vector3 l)
		{
			return Math.Max(0f, ToGo(l)) + 0.5f * Math.Max(0f, rimY - l.y);
		}
		Vector3 Local(int x, int y, int z)
		{
			return o + new Vector3(x, y, z) * s;
		}
		float ToGo(Vector3 l)
		{
			return goal - ((l.x - mid.x) * dir.x + (l.z - mid.z) * dir.z);
		}
	}

	private static bool Near(Vector3 l, Vector3 start)
	{
		return (l - start).sqrMagnitude < 0.0225f;
	}

	private static bool ClearSweep(Bin bin, Vector3 a, Vector3 b, bool fromStart, Vector3 half, Vector3 safe, Quaternion q, Vector3 start)
	{
		Transform root = bin.Root;
		bool flag = Near(root.InverseTransformPoint(a), start) || Near(root.InverseTransformPoint(b), start);
		return !HitsBin(bin, a, b, flag ? half : safe, q, fromStart);
	}

	private static bool ClearAlong(Bin bin, Vector3 start, Vector3 at, Vector3 end, Quaternion from, Quaternion to, Vector3 half)
	{
		float num = Vector3.Distance(start, at);
		float num2 = num + Vector3.Distance(at, end);
		int num3 = Math.Max(1, Mathf.CeilToInt(num2 / 0.04f));
		for (int i = 1; i <= num3; i++)
		{
			float num4 = num2 * (float)i / (float)num3;
			Vector3 at2 = ((num4 <= num) ? Vector3.Lerp(start, at, (num > 0f) ? (num4 / num) : 1f) : Vector3.Lerp(at, end, (num4 - num) / Math.Max(0.0001f, num2 - num)));
			if (OverlapsBin(bin, at2, half, Quaternion.Slerp(from, to, (float)i / (float)num3)))
			{
				return false;
			}
		}
		return true;
	}

	private static bool HitsBin(Bin bin, Vector3 from, Vector3 to, Vector3 half, Quaternion rotation, bool startClear)
	{
		Vector3 vector = to - from;
		float magnitude = vector.magnitude;
		if (magnitude < 0.001f)
		{
			return false;
		}
		foreach (RaycastHit item in Physics.BoxCastAll(from, half, vector / magnitude, rotation, magnitude, bin.Mask, QueryTriggerInteraction.Ignore))
		{
			if (!(item.collider == null) && item.collider.transform.IsChildOf(bin.Root) && (!startClear || !(item.distance <= 0f)))
			{
				lastHit = "'" + item.collider.name + "' at " + FireZone.V(bin.Root.InverseTransformPoint(item.point));
				return true;
			}
		}
		return false;
	}

	private static bool OverlapsBin(Bin bin, Vector3 at, Vector3 half, Quaternion rotation)
	{
		foreach (Collider item in Physics.OverlapBox(at, half, rotation, bin.Mask, QueryTriggerInteraction.Ignore))
		{
			if (item != null && item.transform.IsChildOf(bin.Root))
			{
				lastHit = "'" + item.name + "' touched at " + FireZone.V(bin.Root.InverseTransformPoint(at));
				return true;
			}
		}
		return false;
	}

	private static void Profile(Bin bin)
	{
		if (!profiled.Add(bin.Root.gameObject.Pointer))
		{
			return;
		}
		Transform root = bin.Root;
		StringBuilder stringBuilder = new StringBuilder("[Bin] Shape (along vertical lines: v = faces up met going down, ^ = faces down met going up; L = lid), lid " + ((bin.T >= 1f) ? "open" : "not open") + ":");
		float[] array = new float[2] { 0f, 0.6f };
		foreach (float num in array)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder2);
			handler.AppendLiteral("\n  x ");
			handler.AppendFormatted(num, "0.0");
			handler.AppendLiteral(":");
			stringBuilder3.Append(ref handler);
			for (float num2 = bin.BodyMin.z - 0.05f; num2 <= bin.BodyMax.z + 0.051f; num2 += 0.05f)
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(4, 1, stringBuilder2);
				handler.AppendLiteral(" z");
				handler.AppendFormatted(num2, "0.00");
				handler.AppendLiteral("[v");
				stringBuilder4.Append(ref handler);
				March(bin, root.TransformPoint(new Vector3(num, 3f, num2)), -root.up, stringBuilder);
				stringBuilder.Append(" ^");
				March(bin, root.TransformPoint(new Vector3(num, -0.2f, num2)), root.up, stringBuilder);
				stringBuilder.Append(']');
			}
		}
		Main.Log.Msg(stringBuilder.ToString());
	}

	private static void March(Bin bin, Vector3 origin, Vector3 dir, StringBuilder sb)
	{
		Transform root = bin.Root;
		float num = 3.4f;
		for (int i = 0; i < 10; i++)
		{
			if (!(num > 0f))
			{
				break;
			}
			if (!Physics.Raycast(origin, dir, out var hitInfo, num, bin.Mask, QueryTriggerInteraction.Ignore))
			{
				break;
			}
			if (hitInfo.collider != null && hitInfo.collider.transform.IsChildOf(root))
			{
				bool flag = bin.Lid != null && hitInfo.collider.transform.IsChildOf(bin.Lid);
				sb.Append(' ').Append(flag ? "L" : "").Append(root.InverseTransformPoint(hitInfo.point).y.ToString("0.00"));
			}
			float num2 = hitInfo.distance + 0.002f;
			origin += dir * num2;
			num -= num2;
		}
	}

	public static DecorationItem Template()
	{
		if (template != null || noTemplate)
		{
			return template;
		}
		try
		{
			GameObject gameObject = AssetHelper.SafeLoadAssetAsync<GameObject>("Assets/Prefabs/Containers/CONTAINER_Rustic_Storage_Box.prefab", false).WaitForCompletion();
			GameObject gameObject2 = AssetHelper.SafeLoadAssetAsync<GameObject>("Assets/Prefabs/Containers/CONTAINER_FirewoodBin.prefab", false).WaitForCompletion();
			if (gameObject == null || gameObject2 == null || gameObject.GetComponent<DecorationItem>() == null || gameObject2.GetComponent<MeshFilter>() == null)
			{
				noTemplate = true;
				Main.Log.Error("[Bin] " + ((gameObject == null || gameObject.GetComponent<DecorationItem>() == null) ? "Assets/Prefabs/Containers/CONTAINER_Rustic_Storage_Box.prefab" : "Assets/Prefabs/Containers/CONTAINER_FirewoodBin.prefab") + " not found: no firewood bin furniture");
				return null;
			}
			templates = new GameObject("ImmersiveFire furniture");
			templates.SetActive(value: false);
			UnityEngine.Object.DontDestroyOnLoad(templates);
			GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject, templates.transform);
			GameObject gameObject4 = UnityEngine.Object.Instantiate(gameObject2, templates.transform);
			try
			{
				Build(gameObject3, gameObject4, gameObject.transform.localScale);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(gameObject4);
			}
			template = gameObject3.GetComponent<DecorationItem>();
			Probe(template);
		}
		catch (Exception ex)
		{
			noTemplate = true;
			Main.Log.Error("[Bin] Making the firewood bin furniture failed: " + ex);
		}
		return template;
	}

	private static void Build(GameObject go, GameObject bin, Vector3 boxScale)
	{
		//IL_04c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cc: Expected O, but got Unknown
		go.name = "DECO_FirewoodBin_IMFI";
		Transform transform = go.transform;
		for (int num = transform.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.DestroyImmediate(transform.GetChild(num).gameObject);
		}
		foreach (Collider component8 in go.GetComponents<Collider>())
		{
			UnityEngine.Object.DestroyImmediate(component8);
		}
		transform.localScale = bin.transform.localScale;
		MeshFilter component = bin.GetComponent<MeshFilter>();
		MeshRenderer component2 = bin.GetComponent<MeshRenderer>();
		MeshFilter obj = go.GetComponent<MeshFilter>() ?? go.AddComponent<MeshFilter>();
		MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>() ?? go.AddComponent<MeshRenderer>();
		obj.sharedMesh = component.sharedMesh;
		if (component2 != null)
		{
			meshRenderer.sharedMaterials = component2.sharedMaterials;
			meshRenderer.shadowCastingMode = component2.shadowCastingMode;
			meshRenderer.receiveShadows = component2.receiveShadows;
		}
		MeshCollider meshCollider = go.AddComponent<MeshCollider>();
		meshCollider.convex = false;
		meshCollider.sharedMesh = component.sharedMesh;
		ObjectAnim component3 = bin.GetComponent<ObjectAnim>();
		if (component3 != null && StoveDoors.OpenTween(component3, out var axis, out var angle))
		{
			lidAxis = axis;
			lidGameAngle = angle;
		}
		Transform transform2 = ((component3 != null && component3.m_Target != null) ? component3.m_Target.transform : null);
		if (transform2 == null)
		{
			foreach (Transform componentsInChild in bin.GetComponentsInChildren<Transform>(includeInactive: true))
			{
				if (componentsInChild.name.IndexOf("hinge", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					transform2 = componentsInChild;
					break;
				}
			}
		}
		Container component4 = bin.GetComponent<Container>();
		if (component4 != null)
		{
			openAudio = component4.m_OpenAudio;
			closeAudio = component4.m_CloseAudio;
		}
		if (transform2 != null && transform2 != bin.transform)
		{
			transform2.SetParent(transform, worldPositionStays: false);
			transform2.name = "Hinge";
			foreach (iTweenEvent component9 in transform2.GetComponents<iTweenEvent>())
			{
				UnityEngine.Object.DestroyImmediate(component9);
			}
			foreach (MeshFilter componentsInChild2 in transform2.GetComponentsInChildren<MeshFilter>(includeInactive: true))
			{
				if (componentsInChild2 == null || componentsInChild2.sharedMesh == null)
				{
					continue;
				}
				foreach (Collider component10 in componentsInChild2.GetComponents<Collider>())
				{
					UnityEngine.Object.DestroyImmediate(component10);
				}
				MeshCollider meshCollider2 = componentsInChild2.gameObject.AddComponent<MeshCollider>();
				meshCollider2.convex = false;
				meshCollider2.sharedMesh = componentsInChild2.sharedMesh;
				componentsInChild2.gameObject.layer = go.layer;
				AddInteraction(componentsInChild2.gameObject);
			}
		}
		else
		{
			Main.Log.Warning("[Bin] The firewood bin model has no hinge: its lid won't open");
		}
		GameObject gameObject = new GameObject("Interaction");
		gameObject.layer = go.layer;
		gameObject.transform.SetParent(transform, worldPositionStays: false);
		AddInteraction(gameObject);
		DecorationItem component5 = go.GetComponent<DecorationItem>();
		component5.m_DisplayName = new LocalizedString
		{
			m_LocalizationID = "IMFI_BIN_NAME"
		};
		component5.m_Description = new LocalizedString
		{
			m_LocalizationID = "IMFI_BIN_DESC"
		};
		component5.m_DecorationPrefab = new AssetReferenceDecorationItem("8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6b");
		Placeable component6 = go.GetComponent<Placeable>();
		if (component6 != null)
		{
			component6.m_Addressable = new AssetReferencePlaceable("8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6b");
		}
		BreakDown component7 = go.GetComponent<BreakDown>();
		if (component7 != null)
		{
			component7.m_LocalizedDisplayName = new LocalizedString
			{
				m_LocalizationID = "IMFI_BIN_NAME"
			};
		}
		try
		{
			(icon, craftIcon) = BinIcon.Draw(transform, FrontOf(transform, transform2), 512, 256);
			if (icon != null)
			{
				Main.Log.Msg($"[Bin] Icons drawn from the model ({512} px, white {256} px)");
			}
			SaveForLook(icon, "bin_icon.png");
			SaveForLook(craftIcon, "bin_craft_icon.png");
		}
		catch (Exception ex)
		{
			Main.Log.Warning("[Bin] Drawing the icon failed: " + ex);
		}
		if ((object)icon == null)
		{
			icon = MakeIcon(component2);
		}
		if (icon != null)
		{
			component5.m_IconReference = new AssetReferenceTexture2D("8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6c");
		}
		Main.Log.Msg($"[Bin] Furniture prefab: storage box duplicate (its scale {FireZone.V(boxScale)}) with the bin's model (scale {FireZone.V(transform.localScale)}), lid sounds '{openAudio}' / '{closeAudio}'");
	}

	private static void SaveForLook(Texture2D tex, string file)
	{
		if (tex == null)
		{
			return;
		}
		try
		{
			string text = Path.Combine(MelonEnvironment.UserDataDirectory, "ImmersiveFire");
			Directory.CreateDirectory(text);
			Il2CppStructArray<byte> il2CppStructArray = ImageConversion.EncodeToPNG(tex);
			byte[] array = new byte[il2CppStructArray.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = il2CppStructArray[i];
			}
			File.WriteAllBytes(Path.Combine(text, file), array);
		}
		catch (Exception ex)
		{
			Main.Log.Warning("[Bin] Saving the icon to look at: " + ex.Message);
		}
	}

	private static Vector3 FrontOf(Transform root, Transform hinge)
	{
		if (hinge == null)
		{
			return Vector3.back;
		}
		Vector3 vector = root.InverseTransformPoint(hinge.position);
		Vector3 zero = Vector3.zero;
		int num = 0;
		foreach (MeshFilter componentsInChild in hinge.GetComponentsInChildren<MeshFilter>(includeInactive: true))
		{
			if (!(componentsInChild == null) && !(componentsInChild.sharedMesh == null))
			{
				zero += root.InverseTransformPoint(componentsInChild.transform.TransformPoint(componentsInChild.sharedMesh.bounds.center));
				num++;
			}
		}
		if (num == 0)
		{
			return Vector3.back;
		}
		float num2 = zero.z / (float)num - vector.z;
		if (!(Math.Abs(num2) < 0.05f))
		{
			if (!(num2 < 0f))
			{
				return Vector3.forward;
			}
			return Vector3.back;
		}
		return Vector3.back;
	}

	private static Texture2D MakeIcon(Renderer r)
	{
		try
		{
			Material material = ((r != null) ? r.sharedMaterial : null);
			if (material == null)
			{
				return null;
			}
			Texture texture = material.mainTexture;
			string value = "main";
			if (texture == null)
			{
				foreach (string texturePropertyName in material.GetTexturePropertyNames())
				{
					texture = material.GetTexture(texturePropertyName);
					value = texturePropertyName;
					if (texture != null)
					{
						break;
					}
				}
			}
			if (texture == null)
			{
				Main.Log.Warning("[Bin] No texture on '" + material.name + "' for the icon");
				return null;
			}
			RenderTexture temporary = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			RenderTexture active = RenderTexture.active;
			Graphics.Blit(texture, temporary);
			RenderTexture.active = temporary;
			Texture2D texture2D = new Texture2D(256, 256, TextureFormat.RGB24, mipChain: false);
			texture2D.ReadPixels(new Rect(0f, 0f, 256f, 256f), 0, 0);
			texture2D.Apply();
			RenderTexture.active = active;
			RenderTexture.ReleaseTemporary(temporary);
			texture2D.name = "ico_FirewoodBin_IMFI";
			texture2D.hideFlags = HideFlags.DontUnloadUnusedAsset;
			Main.Log.Msg($"[Bin] Icon from '{texture.name}' ({value} texture of '{material.name}', {texture.width} x {texture.height})");
			return texture2D;
		}
		catch (Exception ex)
		{
			Main.Log.Warning("[Bin] Icon: " + ex.Message);
			return null;
		}
	}

	private static void AddInteraction(GameObject go)
	{
		SimpleInteraction simpleInteraction = go.AddComponent<SimpleInteraction>();
		simpleInteraction.m_DefaultHoverText = new LocalizedString
		{
			m_LocalizationID = "IMFI_BIN_OPEN"
		};
		simpleInteraction.m_CanInteract = true;
		simpleInteraction.m_EnableInSafehouseEditMode = false;
		if (simpleInteraction.m_EventEntries == null)
		{
			simpleInteraction.m_EventEntries = new Il2CppSystem.Collections.Generic.List<InteractionEventEntry>();
		}
	}

	private static bool IsFurniture(DecorationItem deco)
	{
		AssetReferenceDecorationItem assetReferenceDecorationItem = ((deco != null) ? deco.m_DecorationPrefab : null);
		if (assetReferenceDecorationItem != null)
		{
			return ((AssetReference)assetReferenceDecorationItem).AssetGUID == "8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6b";
		}
		return false;
	}

	private static bool IsTemplate(Il2CppObjectBase o)
	{
		if (o != null && template != null)
		{
			return o.Pointer == template.Pointer;
		}
		return false;
	}

	private static DecorationItem NewFurniture(Transform parent)
	{
		DecorationItem decorationItem = Template();
		if (decorationItem == null)
		{
			return null;
		}
		GameObject obj = ((parent != null) ? UnityEngine.Object.Instantiate(decorationItem.gameObject, parent) : UnityEngine.Object.Instantiate(decorationItem.gameObject));
		obj.name = "DECO_FirewoodBin_IMFI";
		return obj.GetComponent<DecorationItem>();
	}

	private static void Interacted(BaseInteraction interaction)
	{
		try
		{
			DecorationItem decorationItem = ((interaction != null) ? interaction.GetComponentInParent<DecorationItem>() : null);
			if (decorationItem != null && bins.TryGetValue(decorationItem.gameObject.Pointer, out var value))
			{
				Toggle(value);
			}
		}
		catch (Exception ex)
		{
			Once.Warn("bin:interact", "Firewood bin lid: " + ex);
		}
	}

	private static void EnsureBlueprint()
	{
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Expected O, but got Unknown
		if (noBlueprint || Time.time < nextBlueprintCheck)
		{
			return;
		}
		nextBlueprintCheck = Time.time + 5f;
		BlueprintManager instance = BlueprintManager.Instance;
		Il2CppSystem.Collections.Generic.List<BlueprintData> list = instance?.m_AllBlueprints;
		if (list == null || list.Count == 0)
		{
			return;
		}
		BlueprintData blueprintData = null;
		for (int i = 0; i < list.Count; i++)
		{
			BlueprintData blueprintData2 = list[i];
			if (!(blueprintData2 == null))
			{
				if (blueprintData2.name == "BLUEPRINT_DECO_FirewoodBin_IMFI")
				{
					return;
				}
				if (blueprintData2.name == "BLUEPRINT_DECO_StorageBox")
				{
					blueprintData = blueprintData2;
				}
			}
		}
		GearItem gearItem = GearItem.LoadGearItemPrefab("GEAR_Softwood");
		DecorationItem decorationItem = Template();
		if (blueprintData == null || gearItem == null || decorationItem == null)
		{
			noBlueprint = true;
			Main.Log.Warning("[Bin] " + ((blueprintData == null) ? "BLUEPRINT_DECO_StorageBox" : ((gearItem == null) ? "GEAR_Softwood" : "furniture")) + " not found: the firewood bin can't be crafted");
			return;
		}
		BlueprintData blueprintData3 = UnityEngine.Object.Instantiate(blueprintData);
		blueprintData3.name = "BLUEPRINT_DECO_FirewoodBin_IMFI";
		Il2CppReferenceArray<BlueprintData.RequiredGearItem> il2CppReferenceArray = new Il2CppReferenceArray<BlueprintData.RequiredGearItem>(1L);
		il2CppReferenceArray[0] = new BlueprintData.RequiredGearItem
		{
			m_Item = gearItem,
			m_Count = 6,
			m_Units = BlueprintData.RequiredGearItem.Units.Count
		};
		blueprintData3.m_RequiredGear = il2CppReferenceArray;
		blueprintData3.m_CraftedResultDecoration = decorationItem;
		if (craftIcon != null)
		{
			blueprintData3.m_CraftingIcon = new AssetReferenceTexture2D("8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6d");
		}
		blueprintData3.m_Locked = false;
		blueprintData3._Locked_k__BackingField = false;
		list.Add(blueprintData3);
		instance.m_AddressableBlueprints?.Add(blueprintData3);
		if (instance.m_UnlockedBlueprints != null && !instance.m_UnlockedBlueprints.Contains("BLUEPRINT_DECO_FirewoodBin_IMFI"))
		{
			instance.m_UnlockedBlueprints.Add("BLUEPRINT_DECO_FirewoodBin_IMFI");
		}
		Main.Log.Msg($"[Bin] Blueprint added: {6} x GEAR_Softwood at {blueprintData3.m_RequiredCraftingLocation}, {blueprintData3.m_DurationMinutes} min, tool {((blueprintData3.m_RequiredTool != null) ? blueprintData3.m_RequiredTool.name : "none")}, result '{decorationItem.name}'");
	}

	private static bool IsOurs(BlueprintData bp)
	{
		if (bp != null)
		{
			return bp.name == "BLUEPRINT_DECO_FirewoodBin_IMFI";
		}
		return false;
	}

	private static void Probe(DecorationItem deco)
	{
		if (deco == null)
		{
			return;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(89, 5, stringBuilder2);
			handler.AppendLiteral("[Bin] Firewood bin furniture: category ");
			handler.AppendFormatted(deco.m_Category);
			handler.AppendLiteral(", weight ");
			handler.AppendFormatted(deco.m_Weight.ToStringMetric());
			handler.AppendLiteral(", ");
			handler.AppendLiteral("allowed in the inventory ");
			handler.AppendFormatted(deco.m_AllowInInventory);
			handler.AppendLiteral(", rules ");
			handler.AppendFormatted(deco.m_PlacementRules);
			handler.AppendLiteral(", key ");
			AssetReferenceDecorationItem decorationPrefab = deco.m_DecorationPrefab;
			handler.AppendFormatted((decorationPrefab != null) ? ((AssetReference)decorationPrefab).AssetGUID : null);
			stringBuilder3.Append(ref handler);
			BreakDown component = deco.GetComponent<BreakDown>();
			if (component != null)
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(66, 4, stringBuilder2);
				handler.AppendLiteral("; break down ");
				handler.AppendFormatted(component.m_TimeCostHours, "0.##");
				handler.AppendLiteral(" h (customizing ");
				handler.AppendFormatted(component.m_EditModeDestroyTimeCostHours, "0.##");
				handler.AppendLiteral(" h), ");
				handler.AppendLiteral("customizing only ");
				handler.AppendFormatted(component.m_EditModeDestroyOnly);
				handler.AppendLiteral(", tool ");
				handler.AppendFormatted(component.m_RequiresTool);
				handler.AppendLiteral(", yields");
				stringBuilder4.Append(ref handler);
				Il2CppReferenceArray<GameObject> yieldObject = component.m_YieldObject;
				Il2CppStructArray<int> yieldObjectUnits = component.m_YieldObjectUnits;
				if (yieldObject != null)
				{
					for (int i = 0; i < yieldObject.Length; i++)
					{
						stringBuilder2 = stringBuilder;
						StringBuilder stringBuilder5 = stringBuilder2;
						handler = new StringBuilder.AppendInterpolatedStringHandler(4, 2, stringBuilder2);
						handler.AppendLiteral(" ");
						handler.AppendFormatted((yieldObjectUnits == null || i >= yieldObjectUnits.Length) ? 1 : yieldObjectUnits[i]);
						handler.AppendLiteral(" x ");
						handler.AppendFormatted((yieldObject[i] != null) ? yieldObject[i].name : "?");
						stringBuilder5.Append(ref handler);
					}
				}
			}
			stringBuilder.Append('\n').Append(Describe(deco.gameObject));
			Main.Log.Msg(stringBuilder.ToString());
		}
		catch (Exception ex)
		{
			Main.Log.Warning("[Bin] Furniture probe: " + ex.Message);
		}
	}

	private static void Scan()
	{
		if (Time.time < nextScan)
		{
			return;
		}
		nextScan = Time.time + 2f;
		Il2CppSystem.Collections.Generic.List<DecorationItem> s_DecorationItems = DecorationItem.s_DecorationItems;
		if (s_DecorationItems != null)
		{
			for (int i = 0; i < s_DecorationItems.Count; i++)
			{
				DecorationItem decorationItem = s_DecorationItems[i];
				if (decorationItem != null && !bins.ContainsKey(decorationItem.gameObject.Pointer) && IsFurniture(decorationItem))
				{
					Make(decorationItem.gameObject);
				}
			}
		}
		Reset();
	}

	public static void Update()
	{
		try
		{
			EnsureBlueprint();
			Scan();
		}
		catch (Exception ex)
		{
			Once.Warn("bin:update", "Firewood bins: " + ex);
		}
	}
}
