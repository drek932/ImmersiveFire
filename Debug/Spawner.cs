#if VERBOSE
using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImmersiveFire
{
    /// <summary>
    /// TEST ONLY (remove before release; Russian only, for the author): F8 opens a small menu on the right to put down a
    /// fire for testing. The wheel picks one, a ghost of it (the game's green placement tint) shows where it goes, in
    /// front of the player and facing them; the left mouse button puts a real one there. F8 again closes the menu.
    /// The objects are not saved (gone after reloading). Fireplaces and kitchen ranges are built into the scenes, they
    /// can't be spawned: visit them in the world.
    /// </summary>
    internal static class Spawner
    {
        public const KeyCode Key = KeyCode.F8;
        private const float Reach = 8f;

        private static readonly (string name, string asset)[] Choices =
        {
            ("Бочка", "Assets/Prefabs/Interactive/INTERACTIVE_FireBarrel.prefab"),
            ("Металлическая печь", "Assets/Prefabs/Interactive/INTERACTIVE_StoveMetalA.prefab"),
            ("Горн", "Assets/Prefabs/Interactive/INTERACTIVE_Forge.prefab"),
            ("Буржуйка", "Assets/Prefabs/Interactive/INTERACTIVE_PotBellyStove.prefab"),
            ("Верстак для патронов", "Assets/Prefabs/Interactive/INTERACTIVE_AmmoWorkBench.prefab"),
            ("Ящик для дров (в инвентарь)", Furniture),
        };

        // Not put down here: the game's own furniture goes into the inventory, the game puts it down (customizing).
        private const string Furniture = "furniture:firewood bin";

        private static bool open;
        private static int selected;
        private static GameObject ghost;
        private static int ghostFor = -1;
        private static float lift;
        private static bool placeable;

        public static void Update()
        {
            if (Input.GetKeyDown(Key) && !InterfaceManager.IsOverlayActiveImmediate()) Toggle();
            if (!open) return;

            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0f)
            {
                selected = (selected + (wheel < 0f ? 1 : -1) + Choices.Length) % Choices.Length;
                DropGhost();
            }
            if (Choices[selected].asset == Furniture)
            {
                DropGhost();
                if (Input.GetMouseButtonDown(0) && !InterfaceManager.IsOverlayActiveImmediate()) GiveBin();
                return;
            }
            if (ghostFor != selected) MakeGhost();
            MoveGhost();
            if (placeable && Input.GetMouseButtonDown(0) && !InterfaceManager.IsOverlayActiveImmediate()) Spawn();
        }

        /// <summary>A firewood bin (the furniture) into the inventory, the way the game adds furniture there.</summary>
        private static void GiveBin()
        {
            var pm = GameManager.GetPlayerManagerComponent();
            var template = FirewoodBins.Template();
            if (pm == null || template == null)
            {
                HUDMessage.AddMessage("Ящик для дров не готов (см. лог)", 2f, false, true);
                return;
            }
            var di = pm.InstantiateDecorationItemInPlayerInventory(template, PlayerManager.InventoryInstantiateFlags.None);
            HUDMessage.AddMessage(di != null ? "В инвентаре: Ящик для дров" : "Не вышло (см. лог)", 2f, false, true);
        }

        public static void Close()
        {
            open = false;
            DropGhost();
        }

        private static void Toggle()
        {
            if (open) Close();
            else open = true;
        }

        // --- the menu ---

        private static GUIStyle box, line, chosen;

        public static void OnGUI()
        {
            if (!open) return;
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box);
                line = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                line.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
                chosen = new GUIStyle(line) { fontStyle = FontStyle.Bold };
                chosen.normal.textColor = new Color(1f, 0.75f, 0.3f);
            }
            float w = 260f, h = 70f + Choices.Length * 24f;
            float x = Screen.width - w - 20f, y = Screen.height / 2f - h / 2f;
            GUI.Box(new Rect(x, y, w, h), "", box);
            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, 22f), "Поставить огонь (тест)", chosen);
            for (int i = 0; i < Choices.Length; i++)
                GUI.Label(new Rect(x + 12f, y + 36f + i * 24f, w - 24f, 22f),
                    (i == selected ? "> " : "   ") + Choices[i].name, i == selected ? chosen : line);
            GUI.Label(new Rect(x + 12f, y + h - 28f, w - 24f, 22f), "Колесо — выбор, ЛКМ — поставить, F8 — закрыть", line);
        }

        // --- the ghost ---

        private static void MakeGhost()
        {
            DropGhost();
            ghostFor = selected;
            GameObject prefab;
            try
            {
                prefab = AssetHelper.SafeLoadAssetAsync<GameObject>(Choices[selected].asset, false).WaitForCompletion();
            }
            catch (Exception e)
            {
                Main.Log.Error($"[Spawner] Loading {Choices[selected].asset} failed: {e.Message}");
                return;
            }
            if (prefab == null) return;

            ghost = new GameObject("ImmersiveFire spawn ghost");
            var pm = GameManager.GetPlayerManagerComponent();
            var tint = pm != null ? pm.m_ValidTintMaterialPropertyBlock : null;
            float minY = float.MaxValue;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                string name = mf.name;
                int lod = name.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
                if (lod >= 0 && !name.Substring(lod).StartsWith("_LOD0", StringComparison.OrdinalIgnoreCase)) continue;

                var part = new GameObject(name);
                part.transform.SetParent(ghost.transform, false);
                part.transform.localPosition = prefab.transform.InverseTransformPoint(mf.transform.position);
                part.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * mf.transform.rotation;
                part.transform.localScale = mf.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = part.AddComponent<MeshRenderer>();
                r.sharedMaterials = mr.sharedMaterials;
                if (tint != null) r.SetPropertyBlock(tint);

                // The lowest point, to stand it on the ground (some pivots are not at the bottom).
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    minY = Math.Min(minY, prefab.transform.InverseTransformPoint(mf.transform.TransformPoint(corner)).y);
                }
            }
            lift = minY == float.MaxValue ? 0f : -minY;
        }

        private static void DropGhost()
        {
            if (ghost != null) Object.Destroy(ghost);
            ghost = null;
            ghostFor = -1;
            placeable = false;
        }

        /// <summary>Where the player looks, on the ground, facing the player (its front, local -z, towards them).</summary>
        private static void MoveGhost()
        {
            if (ghost == null) return;
            var cam = GameManager.GetVpFPSCamera();
            if (cam == null) return;
            var view = cam.transform;
            placeable = Physics.Raycast(view.position, view.forward, out var hit, Reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            ghost.SetActive(placeable);
            if (!placeable) return;
            var flat = new Vector3(view.forward.x, 0f, view.forward.z);
            if (flat.sqrMagnitude < 0.01f) flat = new Vector3(view.up.x, 0f, view.up.z);
            ghost.transform.SetPositionAndRotation(hit.point + Vector3.up * lift, Quaternion.LookRotation(flat.normalized, Vector3.up));
        }

        private static void Spawn()
        {
            var (name, asset) = Choices[selected];
            GameObject go;
            try
            {
                go = AssetHelper.SafeInstantiateAssetAsync(asset, null, false, false).WaitForCompletion();
            }
            catch (Exception e)
            {
                Main.Log.Error($"[Spawner] {asset}: {e.Message}");
                return;
            }
            if (go == null) return;
            go.transform.SetPositionAndRotation(ghost.transform.position, ghost.transform.rotation);
            // Prefab ids may repeat ids of objects already in the scene.
            foreach (var og in go.GetComponentsInChildren<ObjectGuid>(true))
                ObjectGuid.MaybeAttachObjectGuidAddOrReplace(og.gameObject, Guid.NewGuid().ToString());
            Main.Log.Msg($"[Spawner] Put {go.name} at {FireZone.V(go.transform.position)}");
            HUDMessage.AddMessage("Поставлено: " + name, 2f, false, true);
        }
    }
}
#endif
