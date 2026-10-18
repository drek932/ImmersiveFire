using System;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTLD.AddressableAssets;
using Il2CppTLD.Gear;
using Il2CppTLD.Interactions;
using Il2CppTLD.PDID;
using Il2CppTLD.Placement;
using ImmersiveShared;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace ImmersiveFire
{
    /// <summary>
    /// The firewood bin as a piece of the game's own furniture ("decoration"), made the way the game makes its own:
    ///  - A furniture prefab of our own, built once from two of the game's assets: a full duplicate of the storage box
    ///    furniture (CONTAINER_Rustic_Storage_Box: DecorationItem, Placeable, BreakDown with all their settings) with the
    ///    firewood bin's look and shape (CONTAINER_FirewoodBin: base, hinge, lid, at the bin's own size). Laid out like
    ///    the game's furniture workbench: the collider on the root, an interaction on a child (here: open / close the
    ///    lid; the lid has one too). Not a container.
    ///  - Its own asset key (FurnitureKey) in the decoration and placeable references: the game saves it with the bin.
    ///  - The game makes furniture from such a key in four places only, and for our key they make it from our prefab:
    ///    crafting into the inventory or into a container, adding to the inventory, and restoring any saved placeable
    ///    (scene, inventory, containers, recovery camp). Everything else — placing, moving (with the logs inside, the
    ///    game carries what lies on a piece), taking, breaking down, saving — is the game's own.
    ///  - Crafted at the furniture workbench from 6 softwood: a copy of the storage box blueprint making our furniture.
    /// </summary>
    internal static partial class FirewoodBins
    {
        private const string BinAsset = "Assets/Prefabs/Containers/CONTAINER_FirewoodBin.prefab";
        private const string BoxAsset = "Assets/Prefabs/Containers/CONTAINER_Rustic_Storage_Box.prefab";
        private const string SourceBlueprint = "BLUEPRINT_DECO_StorageBox";
        private const string OurBlueprint = "BLUEPRINT_DECO_FirewoodBin_IMFI";
        private const string FurnitureName = "DECO_FirewoodBin_IMFI";
        /// <summary>Our furniture's asset key, saved by the game with every bin (in no catalog: this mod makes it).</summary>
        private const string FurnitureKey = "8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6b";
        private const int Softwood = 6;

        private static GameObject templates;
        private static DecorationItem template;
        private static bool noTemplate;

        // The model's lid, the same for every bin: the axis and angle of the game's own lid tween, its sounds.
        private static Vector3 lidAxis = Vector3.right;
        private static float lidGameAngle;
        private static string openAudio, closeAudio;

        // --- our furniture prefab ---

        /// <summary>Our furniture prefab (inactive, kept for the whole session), made on first use; null if it can't be.</summary>
        public static DecorationItem Template()
        {
            if (template != null || noTemplate) return template;
            try
            {
                var box = AssetHelper.SafeLoadAssetAsync<GameObject>(BoxAsset, false).WaitForCompletion();
                var model = AssetHelper.SafeLoadAssetAsync<GameObject>(BinAsset, false).WaitForCompletion();
                if (box == null || model == null || box.GetComponent<DecorationItem>() == null || model.GetComponent<MeshFilter>() == null)
                {
                    noTemplate = true;
                    Main.Log.Error($"[Bin] {(box == null || box.GetComponent<DecorationItem>() == null ? BoxAsset : BinAsset)} not found: no firewood bin furniture");
                    return null;
                }
                // Under an inactive parent: no Awake, nothing registered, until the game makes a copy.
                templates = new GameObject("ImmersiveFire furniture");
                templates.SetActive(false);
                Object.DontDestroyOnLoad(templates);
                var go = Object.Instantiate(box, templates.transform);
                var bin = Object.Instantiate(model, templates.transform);
                try
                {
                    Build(go, bin, box.transform.localScale);
                }
                finally
                {
                    Object.DestroyImmediate(bin);
                }
                template = go.GetComponent<DecorationItem>();
                Probe(template);
            }
            catch (Exception e)
            {
                noTemplate = true;
                Main.Log.Error("[Bin] Making the firewood bin furniture failed: " + e);
            }
            return template;
        }

        /// <summary>The storage box duplicate <paramref name="go"/> takes the look and shape of the bin <paramref name="bin"/>.</summary>
        private static void Build(GameObject go, GameObject bin, Vector3 boxScale)
        {
            go.name = FurnitureName;
            var root = go.transform;
            // The storage box's own parts go: its lid (its container) and its box collider.
            for (int i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);
            foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
            root.localScale = bin.transform.localScale;

            // The bin's base: its mesh and materials on the root's renderer, its exact (hollow) shape as the root's
            // collider, so logs go inside — the game's furniture workbench has its collider on its root too.
            var baseMf = bin.GetComponent<MeshFilter>();
            var baseMr = bin.GetComponent<MeshRenderer>();
            var mf = go.GetComponent<MeshFilter>() ?? go.AddComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>() ?? go.AddComponent<MeshRenderer>();
            mf.sharedMesh = baseMf.sharedMesh;
            if (baseMr != null)
            {
                mr.sharedMaterials = baseMr.sharedMaterials;
                mr.shadowCastingMode = baseMr.shadowCastingMode;
                mr.receiveShadows = baseMr.receiveShadows;
            }
            var body = go.AddComponent<MeshCollider>();
            body.convex = false;
            body.sharedMesh = baseMf.sharedMesh;

            // The lid on its hinge, from the bin, and how the game's own tween turns it (around that axis we open it).
            var anim = bin.GetComponent<ObjectAnim>();
            if (anim != null && StoveDoors.OpenTween(anim, out var axis, out float angle))
            {
                lidAxis = axis;
                lidGameAngle = angle;
            }
            Transform hinge = anim != null && anim.m_Target != null ? anim.m_Target.transform : null;
            if (hinge == null)
                foreach (var t in bin.GetComponentsInChildren<Transform>(true))
                    if (t.name.IndexOf("hinge", StringComparison.OrdinalIgnoreCase) >= 0) { hinge = t; break; }
            var container = bin.GetComponent<Container>();
            if (container != null)
            {
                openAudio = container.m_OpenAudio;
                closeAudio = container.m_CloseAudio;
            }
            if (hinge != null && hinge != bin.transform)
            {
                hinge.SetParent(root, false);
                hinge.name = "Hinge";
                foreach (var tween in hinge.GetComponents<iTweenEvent>()) Object.DestroyImmediate(tween);
                foreach (var lid in hinge.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (lid == null || lid.sharedMesh == null) continue;
                    foreach (var c in lid.GetComponents<Collider>()) Object.DestroyImmediate(c);
                    // A solid box as big as the lid (it is thin): the mesh's own shape let the aim slip through the gaps
                    // between its boards to what lies inside (user).
                    var lc = lid.gameObject.AddComponent<BoxCollider>();
                    lc.center = lid.sharedMesh.bounds.center;
                    lc.size = lid.sharedMesh.bounds.size;
                    lid.gameObject.layer = go.layer;
                    AddInteraction(lid.gameObject);
                }
            }
            else Main.Log.Warning("[Bin] The firewood bin model has no hinge: its lid won't open");

            // Clicking the body opens and closes the lid too (the workbench's crafting interaction sits on a child as well).
            var handle = new GameObject("Interaction");
            handle.layer = go.layer;
            handle.transform.SetParent(root, false);
            AddInteraction(handle);

            // Who it is: name, description, and its own key (what the game saves).
            var deco = go.GetComponent<DecorationItem>();
            deco.m_DisplayName = new LocalizedString { m_LocalizationID = "IMFI_BIN_NAME" };
            deco.m_Description = new LocalizedString { m_LocalizationID = "IMFI_BIN_DESC" };
            deco.m_DecorationPrefab = new AssetReferenceDecorationItem(FurnitureKey);
            var placeable = go.GetComponent<Placeable>();
            if (placeable != null) placeable.m_Addressable = new AssetReferencePlaceable(FurnitureKey);
            var breakDown = go.GetComponent<BreakDown>();
            if (breakDown != null) breakDown.m_LocalizedDisplayName = new LocalizedString { m_LocalizationID = "IMFI_BIN_NAME" };
            // Its icon: the bin itself drawn like the game's icons (else just its texture), under a key of its own
            // (TextureCache gives it out, IconTexture).
            try
            {
                (icon, craftIcon) = BinIcon.Draw(root, FrontOf(root, hinge), DrawnIconSize, CraftIconSize);
                if (icon != null) Main.Log.Msg($"[Bin] Icons drawn from the model ({DrawnIconSize} px, white {CraftIconSize} px)");
#if VERBOSE
                SaveForLook(icon, "bin_icon.png");
                SaveForLook(craftIcon, "bin_craft_icon.png");
#endif
            }
            catch (Exception e)
            {
                Main.Log.Warning("[Bin] Drawing the icon failed: " + e);
            }
            icon ??= MakeIcon(baseMr);
            if (icon != null) deco.m_IconReference = new AssetReferenceTexture2D(IconKey);
            Main.Log.Msg($"[Bin] Furniture prefab: storage box duplicate (its scale {FireZone.V(boxScale)}) with the bin's model " +
                         $"(scale {FireZone.V(root.localScale)}), lid sounds '{openAudio}' / '{closeAudio}'");
        }

        // --- its icon ---

        private const string IconKey = "8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6c";
        private const string CraftIconKey = "8c3f1e2a5b6d4f7e9a0b1c2d3e4f5a6d";
        private const int IconSize = 256;
        private const int DrawnIconSize = 512;
        private const int CraftIconSize = 256;
        private static Texture2D icon;          // coloured: inventory, crafting preview
        private static Texture2D craftIcon;     // white: the recipe list, like the game's ico_CraftItem__ icons

#if VERBOSE
        /// <summary>Verbose builds: a drawn icon in UserData\ImmersiveFire, to look at outside the game.</summary>
        private static void SaveForLook(Texture2D tex, string file)
        {
            if (tex == null) return;
            try
            {
                var dir = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "ImmersiveFire");
                System.IO.Directory.CreateDirectory(dir);
                var png = ImageConversion.EncodeToPNG(tex);
                var bytes = new byte[png.Length];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = png[i];
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, file), bytes);
            }
            catch (Exception e)
            {
                Main.Log.Warning("[Bin] Saving the icon to look at: " + e.Message);
            }
        }
#endif

        /// <summary>
        /// The bin's front, in its own axes: the closed lid reaches from its hinge (at the back) towards the front.
        /// </summary>
        private static Vector3 FrontOf(Transform root, Transform hinge)
        {
            if (hinge == null) return Vector3.back;
            var hingeAt = root.InverseTransformPoint(hinge.position);
            var sum = Vector3.zero;
            int n = 0;
            foreach (var mf in hinge.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                sum += root.InverseTransformPoint(mf.transform.TransformPoint(mf.sharedMesh.bounds.center));
                n++;
            }
            if (n == 0) return Vector3.back;
            float dz = sum.z / n - hingeAt.z;
            return Math.Abs(dz) < 0.05f ? Vector3.back : dz < 0f ? Vector3.back : Vector3.forward;
        }

        /// <summary>The plain icon (fallback): the bin's own texture, copied square (without its alpha, which may hold other data).</summary>
        private static Texture2D MakeIcon(Renderer r)
        {
            try
            {
                var mat = r != null ? r.sharedMaterial : null;
                if (mat == null) return null;
                Texture src = mat.mainTexture;
                string from = "main";
                if (src == null)
                    foreach (var name in mat.GetTexturePropertyNames())
                    {
                        src = mat.GetTexture(name);
                        from = name;
                        if (src != null) break;
                    }
                if (src == null)
                {
                    Main.Log.Warning($"[Bin] No texture on '{mat.name}' for the icon");
                    return null;
                }
                var rt = RenderTexture.GetTemporary(IconSize, IconSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var active = RenderTexture.active;
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
                tex.Apply();
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt);
                tex.name = "ico_FirewoodBin_IMFI";
                tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Main.Log.Msg($"[Bin] Icon from '{src.name}' ({from} texture of '{mat.name}', {src.width} x {src.height})");
                return tex;
            }
            catch (Exception e)
            {
                Main.Log.Warning("[Bin] Icon: " + e.Message);
                return null;
            }
        }

        /// <summary>Every icon the game shows by reference (inventory, crafting result) comes from here.</summary>
        [HarmonyPatch(typeof(TextureCache), nameof(TextureCache.GetAddressableTexture), typeof(AssetReferenceTexture2D))]
        private static class IconTexture
        {
            private static bool Prefix(AssetReferenceTexture2D assetReference, ref Texture2D __result)
            {
                if (icon == null || assetReference == null || assetReference.AssetGUID != IconKey) return true;
                __result = icon;
                return false;
            }
        }

        /// <summary>
        /// The recipe's white icon. The recipe list (BlueprintDisplayItem.Setup) and GetCraftingIcon load the recipe's
        /// m_CraftingIcon, and when it doesn't load (ours: a key of no asset) "ico_CraftItem__" + the result's name
        /// (GEAR_ replaced) from TextureCache by name: our furniture's name is answered here.
        /// </summary>
        [HarmonyPatch(typeof(TextureCache), nameof(TextureCache.GetAddressableTexture), typeof(string))]
        private static class CraftIconTexture
        {
            private static bool Prefix(string name, ref Texture2D __result)
            {
                if (craftIcon == null || name == null || name.IndexOf(FurnitureName, StringComparison.Ordinal) < 0) return true;
                __result = craftIcon;
                return false;
            }
        }

        [HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.GetCraftingIcon))]
        private static class CraftingIcon
        {
            private static void Postfix(BlueprintData __instance, ref Texture2D __result)
            {
                if (craftIcon != null && IsOurs(__instance)) __result = craftIcon;
            }
        }

        // --- what lies in a bin moves with it ---

        private static int gearLayerMask = -1;

        /// <summary>
        /// The game moves with a piece of furniture the gear "standing on" it: found in a box over it, each kept only if a
        /// line down from it hits the piece itself (PlayerManager.AddStackedGearItemsToChain). Logs piled in a bin mostly
        /// rest on other logs, so only a few went along (user); now everything lying inside goes along too.
        /// </summary>
        [HarmonyPatch(typeof(PlayerManager), "AddStackedGearItemsToChain")]
        private static class CarryInside
        {
            private static void Postfix(DecorationItem item, Il2CppSystem.Collections.Generic.List<PlayerManager.DecorationItemPlacementInfo> chain)
            {
                try
                {
                    if (item == null || chain == null || !bins.TryGetValue(item.gameObject.Pointer, out var bin) || bin.Root == null) return;
                    var already = new System.Collections.Generic.HashSet<IntPtr>();
                    for (int i = 0; i < chain.Count; i++)
                    {
                        var t = chain[i].m_ItemTransform;
                        if (t != null) already.Add(t.Pointer);
                    }
                    if (gearLayerMask < 0) gearLayerMask = 1 << LayerMask.NameToLayer("Gear");
                    var mid = (bin.InsideMin + bin.InsideMax) / 2f;
                    var half = Vector3.Scale((bin.InsideMax - bin.InsideMin) / 2f, bin.Root.lossyScale);
                    int added = 0;
                    foreach (var c in Physics.OverlapBox(bin.Root.TransformPoint(mid), half, bin.Root.rotation, gearLayerMask, QueryTriggerInteraction.Collide))
                    {
                        var gi = c != null ? c.GetComponentInParent<GearItem>() : null;
                        if (gi == null || already.Contains(gi.transform.Pointer) || !Contains(bin, c.bounds.center)) continue;
                        chain.Add(new PlayerManager.DecorationItemPlacementInfo(gi, item));
                        already.Add(gi.transform.Pointer);
                        added++;
                    }
                    Main.Log.Msg($"[Bin] Moving a firewood bin: {added} more item(s) lying inside go along (the game's chain had {chain.Count - added})");
                }
                catch (Exception e)
                {
                    Once.Warn("bin:carry", "Firewood bin contents: " + e);
                }
            }
        }

        private static void AddInteraction(GameObject go)
        {
            var si = go.AddComponent<SimpleInteraction>();
            si.m_DefaultHoverText = new LocalizedString { m_LocalizationID = "IMFI_BIN_OPEN" };
            si.m_CanInteract = true;
            si.m_EnableInSafehouseEditMode = false;   // while customizing, the game's furniture actions
            if (si.m_EventEntries == null) si.m_EventEntries = new Il2CppSystem.Collections.Generic.List<InteractionEventEntry>();
        }

        /// <summary>A piece of our furniture (its own key, which copies and restored pieces keep).</summary>
        private static bool IsFurniture(DecorationItem deco)
        {
            var key = deco != null ? deco.m_DecorationPrefab : null;
            return key != null && key.AssetGUID == FurnitureKey;
        }

        private static bool IsTemplate(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o) => o != null && template != null && o.Pointer == template.Pointer;

        /// <summary>A new piece of our furniture under <paramref name="parent"/> (Awake makes it a bin), or null.</summary>
        private static DecorationItem NewFurniture(Transform parent)
        {
            var t = Template();
            if (t == null) return null;
            var go = parent != null ? Object.Instantiate(t.gameObject, parent) : Object.Instantiate(t.gameObject);
            go.name = FurnitureName;
            return go.GetComponent<DecorationItem>();
        }

        // --- the four places where the game makes furniture from its key ---

        /// <summary>Crafted (at the workbench, the result goes to the inventory unless the bench keeps it).</summary>
        [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.InstantiateCraftedItemInPlayerInventory))]
        private static class CraftedIntoInventory
        {
            private static bool Prefix(ICraftingResult result, PlayerManager.InventoryInstantiateFlags flags, ref ICraftingResult __result)
            {
                if (!IsTemplate(result)) return true;
                try
                {
                    var di = NewFurniture(GameManager.GetPlayerTransform());
                    if (di == null) return true;
                    GameManager.GetInventoryComponent().AddDecoration(di, flags);
                    __result = new ICraftingResult(di.Pointer);
                    Main.Log.Msg($"[Bin] Crafted: a firewood bin in the inventory ({flags})");
                    return false;
                }
                catch (Exception e)
                {
                    Main.Log.Error("[Bin] Crafting a firewood bin into the inventory failed: " + e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.InstantiateDecorationItemInPlayerInventory))]
        private static class IntoInventory
        {
            private static bool Prefix(DecorationItem decorationItem, PlayerManager.InventoryInstantiateFlags flags, ref DecorationItem __result)
            {
                if (!IsTemplate(decorationItem)) return true;
                try
                {
                    var di = NewFurniture(GameManager.GetPlayerTransform());
                    if (di == null) return true;
                    GameManager.GetInventoryComponent().AddDecoration(di, flags);
                    __result = di;
                    Main.Log.Msg($"[Bin] A firewood bin into the inventory ({flags})");
                    return false;
                }
                catch (Exception e)
                {
                    Main.Log.Error("[Bin] A firewood bin into the inventory failed: " + e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Container), nameof(Container.InstantiateCraftedItem))]
        private static class CraftedIntoContainer
        {
            private static bool Prefix(Container __instance, ICraftingResult craftedItem, ref ICraftingResult __result)
            {
                if (!IsTemplate(craftedItem)) return true;
                try
                {
                    var di = NewFurniture(__instance.transform);
                    if (di == null) return true;
                    if (string.IsNullOrEmpty(ObjectGuid.GetGuidFromGameObject(di.gameObject, false)))
                        ObjectGuid.MaybeAttachObjectGuidAndRegister(di.gameObject, PdidTable.GenerateNewID());
                    __instance.AddDecorationItem(di);
                    __result = new ICraftingResult(di.Pointer);
                    Main.Log.Msg($"[Bin] Crafted: a firewood bin in '{__instance.name}'");
                    return false;
                }
                catch (Exception e)
                {
                    Main.Log.Error("[Bin] Crafting a firewood bin into a container failed: " + e);
                    return true;
                }
            }
        }

        /// <summary>Every saved placeable comes back through here (scene, inventory, containers, recovery camp).</summary>
        [HarmonyPatch(typeof(Placeable), nameof(Placeable.FindOrCreateAndDeserialize))]
        private static class Restore
        {
            private static bool Prefix(string guid, PlaceableSaveData data, ref Placeable __result)
            {
                try
                {
                    var key = data != null ? data.m_Addressable : null;
                    if (key == null || key.AssetGUID != FurnitureKey) return true;
                    // Already there (the same scene): the game only puts it where it was saved.
                    if (!string.IsNullOrEmpty(guid) && PdidTable.GetGameObject(guid) != null) return true;
                    var di = NewFurniture(null);
                    if (di == null)
                    {
                        Main.Log.Error($"[Bin] Saved firewood bin {guid} can't be made");
                        return true;
                    }
                    var placeable = di.GetComponent<Placeable>();
                    var category = PlaceableManager.FindOrCreateCategoryRoot();
                    if (category != null) di.transform.parent = category.transform;
                    if (string.IsNullOrEmpty(guid)) guid = PdidTable.GenerateNewID();
                    placeable.Deserialize(guid, data);
                    __result = placeable;
                    Main.Log.Msg($"[Bin] Firewood bin {guid} restored at {FireZone.V(di.transform.position)}" +
                                 (di.gameObject.activeSelf ? "" : " (not active: inventory or container)"));
                    return false;
                }
                catch (Exception e)
                {
                    Main.Log.Error("[Bin] Restoring a firewood bin failed: " + e);
                    return true;
                }
            }
        }

        /// <summary>Every new piece of our furniture (crafted, restored, its placement ghost) becomes a bin at once.</summary>
        [HarmonyPatch(typeof(DecorationItem), "Awake")]
        private static class Awake
        {
            private static void Postfix(DecorationItem __instance)
            {
                try
                {
                    if (IsFurniture(__instance)) Make(__instance.gameObject);
                }
                catch (Exception e)
                {
                    Once.Warn("bin:awake", "Firewood bin: " + e);
                }
            }
        }

        // --- the lid's interactions ---

        private static UnityAction<BaseInteraction> onInteract;

        private static UnityAction<BaseInteraction> OnInteract =>
            onInteract ??= DelegateSupport.ConvertDelegate<UnityAction<BaseInteraction>>(new Action<BaseInteraction>(Interacted));

        private static void Interacted(BaseInteraction interaction)
        {
            try
            {
                var deco = interaction != null ? interaction.GetComponentInParent<DecorationItem>() : null;
                if (deco != null && bins.TryGetValue(deco.gameObject.Pointer, out var bin)) Toggle(bin);
            }
            catch (Exception e)
            {
                Once.Warn("bin:interact", "Firewood bin lid: " + e);
            }
        }

        // --- crafting ---

        private static float nextBlueprintCheck;
        private static bool noBlueprint;

        /// <summary>Every few seconds until it is there: our blueprint next to the game's (the list may be rebuilt on load).</summary>
        private static void EnsureBlueprint()
        {
            if (noBlueprint || Time.time < nextBlueprintCheck) return;
            nextBlueprintCheck = Time.time + 5f;
            var manager = BlueprintManager.Instance;
            var all = manager?.m_AllBlueprints;
            if (all == null || all.Count == 0) return;
            BlueprintData source = null;
            for (int i = 0; i < all.Count; i++)
            {
                var bp = all[i];
                if (bp == null) continue;
                if (bp.name == OurBlueprint) return; // there already
                if (bp.name == SourceBlueprint) source = bp;
            }
            var softwood = GearItem.LoadGearItemPrefab("GEAR_Softwood");
            var furniture = Template();
            if (source == null || softwood == null || furniture == null)
            {
                noBlueprint = true;
                Main.Log.Warning($"[Bin] {(source == null ? SourceBlueprint : softwood == null ? "GEAR_Softwood" : "furniture")} " +
                                 "not found: the firewood bin can't be crafted");
                return;
            }

            var ours = Object.Instantiate(source);
            ours.name = OurBlueprint;
            var gear = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<BlueprintData.RequiredGearItem>(1);
            gear[0] = new BlueprintData.RequiredGearItem
            {
                m_Item = softwood,
                m_Count = Softwood,
                m_Units = BlueprintData.RequiredGearItem.Units.Count,
            };
            ours.m_RequiredGear = gear;
            ours.m_CraftedResultDecoration = furniture;
            // Its list icon: a key of no asset, so the game asks TextureCache for it by name (CraftIconTexture).
            if (craftIcon != null) ours.m_CraftingIcon = new AssetReferenceTexture2D(CraftIconKey);
            // Open from the start: both the asset's flag and the runtime one.
            ours.m_Locked = false;
            ours._Locked_k__BackingField = false;
            all.Add(ours);
            // Also among the game's own (addressable) recipes: CraftCategories counts every other recipe as a mod's and shows
            // those only in its own category ("Other mods"), not with the furniture.
            manager.m_AddressableBlueprints?.Add(ours);
            if (manager.m_UnlockedBlueprints != null && !manager.m_UnlockedBlueprints.Contains(OurBlueprint))
                manager.m_UnlockedBlueprints.Add(OurBlueprint);
            Main.Log.Msg($"[Bin] Blueprint added: {Softwood} x GEAR_Softwood at {ours.m_RequiredCraftingLocation}, " +
                         $"{ours.m_DurationMinutes} min, tool {(ours.m_RequiredTool != null ? ours.m_RequiredTool.name : "none")}, " +
                         $"result '{furniture.name}'");
        }

        private static bool IsOurs(BlueprintData bp) => bp != null && bp.name == OurBlueprint;

        [HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.GetDisplayedNameWithCount))]
        private static class BlueprintName
        {
            private static void Postfix(BlueprintData __instance, ref string __result)
            {
                if (IsOurs(__instance)) __result = Localization.Get("IMFI_BIN_NAME");
            }
        }

        /// <summary>Our furniture in the log: its settings (from the storage box) and its parts.</summary>
        private static void Probe(DecorationItem deco)
        {
            if (deco == null) return;
            try
            {
                var sb = new StringBuilder();
                sb.Append($"[Bin] Firewood bin furniture: category {deco.m_Category}, weight {deco.m_Weight.ToStringMetric(2)}, " +
                          $"allowed in the inventory {deco.m_AllowInInventory}, rules {deco.m_PlacementRules}, key {deco.m_DecorationPrefab?.AssetGUID}");
                var bd = deco.GetComponent<BreakDown>();
                if (bd != null)
                {
                    sb.Append($"; break down {bd.m_TimeCostHours:0.##} h (customizing {bd.m_EditModeDestroyTimeCostHours:0.##} h), " +
                              $"customizing only {bd.m_EditModeDestroyOnly}, tool {bd.m_RequiresTool}, yields");
                    var yields = bd.m_YieldObject;
                    var units = bd.m_YieldObjectUnits;
                    if (yields != null)
                        for (int i = 0; i < yields.Length; i++)
                            sb.Append($" {(units != null && i < units.Length ? units[i] : 1)} x {(yields[i] != null ? yields[i].name : "?")}");
                }
                sb.Append('\n').Append(Describe(deco.gameObject));
                Main.Log.Msg(sb.ToString());
            }
            catch (Exception e)
            {
                Main.Log.Warning("[Bin] Furniture probe: " + e.Message);
            }
        }

        // --- every frame ---

        private static float nextScan;

        /// <summary>Any piece of our furniture not yet a bin (should Awake have been missed) becomes one; gone ones go.</summary>
        private static void Scan()
        {
            if (Time.time < nextScan) return;
            nextScan = Time.time + 2f;
            var all = DecorationItem.s_DecorationItems;
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                {
                    var deco = all[i];
                    if (deco != null && !bins.ContainsKey(deco.gameObject.Pointer) && IsFurniture(deco)) Make(deco.gameObject);
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
            catch (Exception e)
            {
                Once.Warn("bin:update", "Firewood bins: " + e);
            }
        }
    }
}
