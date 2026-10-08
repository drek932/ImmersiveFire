using System;
using Il2Cpp;
using Il2CppTLD.AddressableAssets;
using UnityEngine;

namespace ImmersiveFire;

internal static class Spawner
{
	public const KeyCode Key = KeyCode.F8;

	private const float Reach = 8f;

	private static readonly (string name, string asset)[] Choices = new(string, string)[6]
	{
		("Бочка", "Assets/Prefabs/Interactive/INTERACTIVE_FireBarrel.prefab"),
		("Металлическая печь", "Assets/Prefabs/Interactive/INTERACTIVE_StoveMetalA.prefab"),
		("Горн", "Assets/Prefabs/Interactive/INTERACTIVE_Forge.prefab"),
		("Буржуйка", "Assets/Prefabs/Interactive/INTERACTIVE_PotBellyStove.prefab"),
		("Верстак для патронов", "Assets/Prefabs/Interactive/INTERACTIVE_AmmoWorkBench.prefab"),
		("Ящик для дров (в инвентарь)", "furniture:firewood bin")
	};

	private const string Furniture = "furniture:firewood bin";

	private static bool open;

	private static int selected;

	private static GameObject ghost;

	private static int ghostFor = -1;

	private static float lift;

	private static bool placeable;

	private static GUIStyle box;

	private static GUIStyle line;

	private static GUIStyle chosen;

	public static void Update()
	{
		if (Input.GetKeyDown(KeyCode.F8) && !InterfaceManager.IsOverlayActiveImmediate())
		{
			Toggle();
		}
		if (!open)
		{
			return;
		}
		float y = Input.mouseScrollDelta.y;
		if (y != 0f)
		{
			selected = (selected + ((y < 0f) ? 1 : (-1)) + Choices.Length) % Choices.Length;
			DropGhost();
		}
		if (Choices[selected].asset == "furniture:firewood bin")
		{
			DropGhost();
			if (Input.GetMouseButtonDown(0) && !InterfaceManager.IsOverlayActiveImmediate())
			{
				GiveBin();
			}
			return;
		}
		if (ghostFor != selected)
		{
			MakeGhost();
		}
		MoveGhost();
		if (placeable && Input.GetMouseButtonDown(0) && !InterfaceManager.IsOverlayActiveImmediate())
		{
			Spawn();
		}
	}

	private static void GiveBin()
	{
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		DecorationItem decorationItem = FirewoodBins.Template();
		if (playerManagerComponent == null || decorationItem == null)
		{
			HUDMessage.AddMessage("Ящик для дров не готов (см. лог)", 2f, highPriority: false, ignoreOverlayActive: true);
		}
		else
		{
			HUDMessage.AddMessage((playerManagerComponent.InstantiateDecorationItemInPlayerInventory(decorationItem, PlayerManager.InventoryInstantiateFlags.None) != null) ? "В инвентаре: Ящик для дров" : "Не вышло (см. лог)", 2f, highPriority: false, ignoreOverlayActive: true);
		}
	}

	public static void Close()
	{
		open = false;
		DropGhost();
	}

	private static void Toggle()
	{
		if (open)
		{
			Close();
		}
		else
		{
			open = true;
		}
	}

	public static void OnGUI()
	{
		if (open)
		{
			if (box == null)
			{
				box = new GUIStyle(GUI.skin.box);
				line = new GUIStyle(GUI.skin.label)
				{
					fontSize = 16
				};
				line.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
				chosen = new GUIStyle(line)
				{
					fontStyle = FontStyle.Bold
				};
				chosen.normal.textColor = new Color(1f, 0.75f, 0.3f);
			}
			float num = 260f;
			float num2 = 70f + (float)Choices.Length * 24f;
			float num3 = (float)Screen.width - num - 20f;
			float num4 = (float)Screen.height / 2f - num2 / 2f;
			GUI.Box(new Rect(num3, num4, num, num2), "", box);
			GUI.Label(new Rect(num3 + 12f, num4 + 8f, num - 24f, 22f), "Поставить огонь (тест)", chosen);
			for (int i = 0; i < Choices.Length; i++)
			{
				GUI.Label(new Rect(num3 + 12f, num4 + 36f + (float)i * 24f, num - 24f, 22f), ((i == selected) ? "> " : "   ") + Choices[i].name, (i == selected) ? chosen : line);
			}
			GUI.Label(new Rect(num3 + 12f, num4 + num2 - 28f, num - 24f, 22f), "Колесо — выбор, ЛКМ — поставить, F8 — закрыть", line);
		}
	}

	private static void MakeGhost()
	{
		DropGhost();
		ghostFor = selected;
		GameObject gameObject;
		try
		{
			gameObject = AssetHelper.SafeLoadAssetAsync<GameObject>(Choices[selected].asset, false).WaitForCompletion();
		}
		catch (Exception ex)
		{
			Main.Log.Error("[Spawner] Loading " + Choices[selected].asset + " failed: " + ex.Message);
			return;
		}
		if (gameObject == null)
		{
			return;
		}
		ghost = new GameObject("ImmersiveFire spawn ghost");
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		MaterialPropertyBlock materialPropertyBlock = ((playerManagerComponent != null) ? playerManagerComponent.m_ValidTintMaterialPropertyBlock : null);
		float num = float.MaxValue;
		foreach (MeshFilter componentsInChild in gameObject.GetComponentsInChildren<MeshFilter>(includeInactive: false))
		{
			if (componentsInChild == null || componentsInChild.sharedMesh == null)
			{
				continue;
			}
			MeshRenderer component = componentsInChild.GetComponent<MeshRenderer>();
			if (component == null || !component.enabled)
			{
				continue;
			}
			string name = componentsInChild.name;
			int num2 = name.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
			if (num2 < 0 || name.Substring(num2).StartsWith("_LOD0", StringComparison.OrdinalIgnoreCase))
			{
				GameObject gameObject2 = new GameObject(name);
				gameObject2.transform.SetParent(ghost.transform, worldPositionStays: false);
				gameObject2.transform.localPosition = gameObject.transform.InverseTransformPoint(componentsInChild.transform.position);
				gameObject2.transform.localRotation = Quaternion.Inverse(gameObject.transform.rotation) * componentsInChild.transform.rotation;
				gameObject2.transform.localScale = componentsInChild.transform.lossyScale;
				gameObject2.AddComponent<MeshFilter>().sharedMesh = componentsInChild.sharedMesh;
				MeshRenderer meshRenderer = gameObject2.AddComponent<MeshRenderer>();
				meshRenderer.sharedMaterials = component.sharedMaterials;
				if (materialPropertyBlock != null)
				{
					meshRenderer.SetPropertyBlock(materialPropertyBlock);
				}
				Bounds bounds = componentsInChild.sharedMesh.bounds;
				for (int i = 0; i < 8; i++)
				{
					Vector3 position = new Vector3(((i & 1) == 0) ? bounds.min.x : bounds.max.x, ((i & 2) == 0) ? bounds.min.y : bounds.max.y, ((i & 4) == 0) ? bounds.min.z : bounds.max.z);
					num = Math.Min(num, gameObject.transform.InverseTransformPoint(componentsInChild.transform.TransformPoint(position)).y);
				}
			}
		}
		lift = ((num == float.MaxValue) ? 0f : (0f - num));
	}

	private static void DropGhost()
	{
		if (ghost != null)
		{
			UnityEngine.Object.Destroy(ghost);
		}
		ghost = null;
		ghostFor = -1;
		placeable = false;
	}

	private static void MoveGhost()
	{
		if (ghost == null)
		{
			return;
		}
		vp_FPSCamera vpFPSCamera = GameManager.GetVpFPSCamera();
		if (vpFPSCamera == null)
		{
			return;
		}
		Transform transform = vpFPSCamera.transform;
		placeable = Physics.Raycast(transform.position, transform.forward, out var hitInfo, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
		ghost.SetActive(placeable);
		if (placeable)
		{
			Vector3 vector = new Vector3(transform.forward.x, 0f, transform.forward.z);
			if (vector.sqrMagnitude < 0.01f)
			{
				vector = new Vector3(transform.up.x, 0f, transform.up.z);
			}
			ghost.transform.SetPositionAndRotation(hitInfo.point + Vector3.up * lift, Quaternion.LookRotation(vector.normalized, Vector3.up));
		}
	}

	private static void Spawn()
	{
		var (text, text2) = Choices[selected];
		GameObject gameObject;
		try
		{
			gameObject = AssetHelper.SafeInstantiateAssetAsync(text2, (Transform)null, false, false).WaitForCompletion();
		}
		catch (Exception ex)
		{
			Main.Log.Error("[Spawner] " + text2 + ": " + ex.Message);
			return;
		}
		if (gameObject == null)
		{
			return;
		}
		gameObject.transform.SetPositionAndRotation(ghost.transform.position, ghost.transform.rotation);
		foreach (ObjectGuid componentsInChild in gameObject.GetComponentsInChildren<ObjectGuid>(includeInactive: true))
		{
			ObjectGuid.MaybeAttachObjectGuidAddOrReplace(((Component)(object)componentsInChild).gameObject, Guid.NewGuid().ToString());
		}
		Main.Log.Msg("[Spawner] Put " + gameObject.name + " at " + FireZone.V(gameObject.transform.position));
		HUDMessage.AddMessage("Поставлено: " + text, 2f, highPriority: false, ignoreOverlayActive: true);
	}
}
