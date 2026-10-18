using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using ImmersiveShared;
using UnityEngine;

namespace ImmersiveFire
{
    /// <summary>
    /// Auto-feed in the menu of a burning fire (Panel_ActionPicker.AddFeedFireActions, as ImmersiveCooking adds "Cook"):
    /// a button "Auto-feed: off / 30 min" that switches it for this fire — next to "Add fuel", or instead of it (settings,
    /// separate for campfires). While the key (Shift) is held the menu also shows 15 / 30 / 60 min; the chosen one keeps
    /// the white hover frame.
    /// </summary>
    internal static class FireMenu
    {
        private static readonly int[] Thresholds = { 15, 30, 60 };
        private const int DefaultThreshold = 30;

        private static FireplaceInteraction menuFire;
        private static int lastChosen = DefaultThreshold;
        private static int highlight = -1;      // menu entry (data index) of the chosen time, -1: none
        private static bool keyWasHeld;
        private static bool rebuildPending;

        /// <summary>The button: on or off only (the time is shown with the key held, user).</summary>
        private static string Label(int mode) => mode == FuelFeeder.AutoOff ? "IMFI_AUTO_OFF" : "IMFI_AUTO_ON";

        /// <summary>The message when it is switched or a time chosen: with the time.</summary>
        private static string Status(int mode) => mode switch
        {
            FuelFeeder.AutoOff => "IMFI_AUTO_OFF",
            15 => "IMFI_AUTO_15",
            30 => "IMFI_AUTO_30",
            _ => "IMFI_AUTO_60",
        };

        private static string ChoiceLabel(int mode) => mode switch
        {
            15 => "IMFI_MIN_15",
            30 => "IMFI_MIN_30",
            _ => "IMFI_MIN_60",
        };

        [HarmonyPatch(typeof(Panel_ActionPicker), nameof(Panel_ActionPicker.AddFeedFireActions))]
        private static class AddFeedFireActions
        {
            private static void Postfix(Panel_ActionPicker __instance, Fire __0)
            {
                try
                {
                    Add(__instance, __0);
                }
                catch (Exception e)
                {
                    Once.Warn("menu", "Auto-feed in the fire menu: " + e);
                }
            }
        }

        private static void Add(Panel_ActionPicker picker, Fire fire)
        {
            highlight = -1;
            var fi = FuelFeeder.FireplaceOf(fire);
            var actions = picker.m_ActionPickerItemDataList;
            if (fi == null || actions == null || (!Main.Settings.AutoOn && !Main.Settings.PullOn)) return;
            menuFire = fi;

            bool campfire = fi.TryCast<Campfire>() != null;
            bool replace = campfire ? Main.Settings.autoFeedReplaceCampfire : Main.Settings.autoFeedReplace;
            int mode = FuelFeeder.AutoMode(fi);

            int feed = -1;
            for (int i = 0; i < actions.Count; i++)
                if (actions[i] != null && actions[i].m_LocID == Panel_ActionPicker.LOCID_FEEDFIRE) feed = i;

            int at = feed >= 0 ? feed + 1 : actions.Count;
            if (Main.Settings.AutoOn)
            {
                var main = Item(Label(mode), Toggle);
                if (replace && feed >= 0) actions[feed] = main;
                else actions.Insert(at++, main);

                if (Input.GetKey(Main.Settings.autoFeedKey))
                    foreach (int t in Thresholds)
                    {
                        if (t == mode) highlight = at;
                        int chosen = t;
                        actions.Insert(at++, Item(ChoiceLabel(t), () => Choose(chosen)));
                    }
            }

            // Right of the auto-feed button (and its times): pulling fuel in at this fire on / off, with the game's
            // "light the fire" icon (user).
            if (Main.Settings.PullOn)
                actions.Insert(at++, new ActionPickerItemData(Panel_ActionPicker.SPRITE_STARTFIRE,
                    FuelFeeder.PullsIn(fi) ? "IMFI_PULL_ON" : "IMFI_PULL_OFF", DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(TogglePull))));

            while (picker.m_ActionPickerItemList != null && actions.Count > picker.m_ActionPickerItemList.Length)
                if (!AddButton(picker)) break;
        }

        private static ActionPickerItemData Item(string label, Action callback) =>
            new(Panel_ActionPicker.SPRITE_FEEDFIRE, label, DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(callback));

        private static void Toggle()
        {
            if (menuFire == null) return;
            int mode = FuelFeeder.AutoMode(menuFire);
            Set(mode == FuelFeeder.AutoOff ? lastChosen : FuelFeeder.AutoOff);
        }

        private static void TogglePull()
        {
            if (menuFire == null) return;
            bool on = !FuelFeeder.PullsIn(menuFire);
            FuelFeeder.SetPull(menuFire, on);
            HUDMessage.AddMessage(Localization.Get(on ? "IMFI_PULL_ON_MSG" : "IMFI_PULL_OFF_MSG"), 2f, false, true);
            rebuildPending = true;
        }

        private static void Choose(int threshold)
        {
            lastChosen = threshold;
            Set(threshold);
        }

        private static void Set(int mode)
        {
            FuelFeeder.SetAutoMode(menuFire, mode);
            HUDMessage.AddMessage(Localization.Get(Status(mode)), 2f, false, true);
            rebuildPending = true; // new label and frame, if the menu stays open
        }

        /// <summary>Every frame: the key shows or hides the times; a choice is shown at once.</summary>
        public static void Update()
        {
            bool held = Input.GetKey(Main.Settings.autoFeedKey);
            bool changed = held != keyWasHeld;
            keyWasHeld = held;
            if (!changed && !rebuildPending) return;
            rebuildPending = false;
            if (!Open(out var picker)) return;
            picker.RebuildActionPicker();
            picker.EnableWithCurrentList();
        }

        /// <summary>After the game drew the menu: the chosen time keeps the white frame.</summary>
        public static void LateUpdate()
        {
            if (highlight < 0 || !Open(out var picker)) return;
            var buttons = picker.m_ActionPickerItemList;
            if (buttons == null) return;
            foreach (var b in buttons)
                if (b != null && b.m_Index == highlight && b.gameObject.activeInHierarchy && b.m_HoverSprite != null)
                {
                    b.m_HoverSprite.enabled = true;
                    b.m_HoverSprite.alpha = 1f;
                }
        }

        private static bool Open(out Panel_ActionPicker picker)
        {
            if (!InterfaceManager.TryGetPanel<Panel_ActionPicker>(out picker) || picker == null) return false;
            if (!picker.gameObject.activeInHierarchy) return false;
            var target = picker.m_ObjectInteractedWith;
            return target != null && menuFire != null;
        }

        /// <summary>
        /// Adds a copy of the last button to the action menu (as ImmersiveCooking does): the menu has a fixed set of
        /// buttons and lays them out by their count, so an extra one is placed like the others.
        /// </summary>
        private static bool AddButton(Panel_ActionPicker picker)
        {
            var buttons = picker.m_ActionPickerItemList;
            if (buttons == null || buttons.Length == 0 || buttons[buttons.Length - 1] == null) return false;
            var last = buttons[buttons.Length - 1];
            var copy = UnityEngine.Object.Instantiate(last.gameObject, last.transform.parent);
            copy.name = last.gameObject.name + "_ImmersiveFire";
            var item = copy.GetComponent<ActionPickerItem>();
            if (item == null)
            {
                UnityEngine.Object.Destroy(copy);
                return false;
            }
            item.m_ActionPicker = picker;
            item.m_Index = buttons.Length;
            copy.SetActive(false);
            var extended = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<ActionPickerItem>(buttons.Length + 1);
            for (int i = 0; i < buttons.Length; i++) extended[i] = buttons[i];
            extended[buttons.Length] = item;
            picker.m_ActionPickerItemList = extended;
            Verbose.Log($"Fire menu: added a button ({extended.Length} now)");
            return true;
        }
    }
}
