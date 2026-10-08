#define VERBOSE
using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using ImmersiveShared;
using UnityEngine;

namespace ImmersiveFire;

internal static class FireMenu
{
	[HarmonyPatch(typeof(Panel_ActionPicker), "AddFeedFireActions")]
	private static class AddFeedFireActions
	{
		private static void Postfix(Panel_ActionPicker __instance, Fire __0)
		{
			try
			{
				Add(__instance, __0);
			}
			catch (System.Exception ex)
			{
				Once.Warn("menu", "Auto-feed in the fire menu: " + ex);
			}
		}
	}

	private static readonly int[] Thresholds = new int[3] { 15, 30, 60 };

	private const int DefaultThreshold = 30;

	private static FireplaceInteraction menuFire;

	private static int lastChosen = 30;

	private static int highlight = -1;

	private static bool keyWasHeld;

	private static bool rebuildPending;

	private static string Label(int mode)
	{
		if (mode != 0)
		{
			return "IMFI_AUTO_ON";
		}
		return "IMFI_AUTO_OFF";
	}

	private static string Status(int mode)
	{
		return mode switch
		{
			0 => "IMFI_AUTO_OFF", 
			15 => "IMFI_AUTO_15", 
			30 => "IMFI_AUTO_30", 
			_ => "IMFI_AUTO_60", 
		};
	}

	private static string ChoiceLabel(int mode)
	{
		return mode switch
		{
			15 => "IMFI_MIN_15", 
			30 => "IMFI_MIN_30", 
			_ => "IMFI_MIN_60", 
		};
	}

	private static void Add(Panel_ActionPicker picker, Fire fire)
	{
		highlight = -1;
		FireplaceInteraction fireplaceInteraction = FuelFeeder.FireplaceOf(fire);
		List<ActionPickerItemData> actionPickerItemDataList = picker.m_ActionPickerItemDataList;
		if (fireplaceInteraction == null || actionPickerItemDataList == null || !Main.Settings.AutoOn)
		{
			return;
		}
		menuFire = fireplaceInteraction;
		bool flag = ((fireplaceInteraction.TryCast<Campfire>() != null) ? Main.Settings.autoFeedReplaceCampfire : Main.Settings.autoFeedReplace);
		int num = FuelFeeder.AutoMode(fireplaceInteraction);
		int num2 = -1;
		for (int i = 0; i < actionPickerItemDataList.Count; i++)
		{
			if (actionPickerItemDataList[i] != null && actionPickerItemDataList[i].m_LocID == Panel_ActionPicker.LOCID_FEEDFIRE)
			{
				num2 = i;
			}
		}
		ActionPickerItemData actionPickerItemData = Item(Label(num), Toggle);
		int num3;
		if (flag && num2 >= 0)
		{
			actionPickerItemDataList[num2] = actionPickerItemData;
			num3 = num2 + 1;
		}
		else
		{
			num3 = ((num2 >= 0) ? (num2 + 1) : actionPickerItemDataList.Count);
			actionPickerItemDataList.Insert(num3, actionPickerItemData);
			num3++;
		}
		if (Input.GetKey(Main.Settings.autoFeedKey))
		{
			int[] thresholds = Thresholds;
			foreach (int num4 in thresholds)
			{
				if (num4 == num)
				{
					highlight = num3;
				}
				int chosen = num4;
				actionPickerItemDataList.Insert(num3++, Item(ChoiceLabel(num4), delegate
				{
					Choose(chosen);
				}));
			}
		}
		while (picker.m_ActionPickerItemList != null && actionPickerItemDataList.Count > picker.m_ActionPickerItemList.Length && AddButton(picker))
		{
		}
	}

	private static ActionPickerItemData Item(string label, System.Action callback)
	{
		return new ActionPickerItemData(Panel_ActionPicker.SPRITE_FEEDFIRE, label, DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(callback));
	}

	private static void Toggle()
	{
		if (!(menuFire == null))
		{
			Set((FuelFeeder.AutoMode(menuFire) == 0) ? lastChosen : 0);
		}
	}

	private static void Choose(int threshold)
	{
		lastChosen = threshold;
		Set(threshold);
	}

	private static void Set(int mode)
	{
		FuelFeeder.SetAutoMode(menuFire, mode);
		HUDMessage.AddMessage(Localization.Get(Status(mode)), 2f, highPriority: false, ignoreOverlayActive: true);
		rebuildPending = true;
	}

	public static void Update()
	{
		bool key = Input.GetKey(Main.Settings.autoFeedKey);
		bool flag = key != keyWasHeld;
		keyWasHeld = key;
		if (flag || rebuildPending)
		{
			rebuildPending = false;
			if (Open(out var picker))
			{
				picker.RebuildActionPicker();
				picker.EnableWithCurrentList();
			}
		}
	}

	public static void LateUpdate()
	{
		if (highlight < 0 || !Open(out var picker))
		{
			return;
		}
		Il2CppReferenceArray<ActionPickerItem> actionPickerItemList = picker.m_ActionPickerItemList;
		if (actionPickerItemList == null)
		{
			return;
		}
		foreach (ActionPickerItem item in actionPickerItemList)
		{
			if (item != null && item.m_Index == highlight && item.gameObject.activeInHierarchy && item.m_HoverSprite != null)
			{
				item.m_HoverSprite.enabled = true;
				item.m_HoverSprite.alpha = 1f;
			}
		}
	}

	private static bool Open(out Panel_ActionPicker picker)
	{
		if (!InterfaceManager.TryGetPanel<Panel_ActionPicker>(out picker) || picker == null)
		{
			return false;
		}
		if (!picker.gameObject.activeInHierarchy)
		{
			return false;
		}
		if (picker.m_ObjectInteractedWith != null)
		{
			return menuFire != null;
		}
		return false;
	}

	private static bool AddButton(Panel_ActionPicker picker)
	{
		Il2CppReferenceArray<ActionPickerItem> actionPickerItemList = picker.m_ActionPickerItemList;
		if (actionPickerItemList == null || actionPickerItemList.Length == 0 || actionPickerItemList[actionPickerItemList.Length - 1] == null)
		{
			return false;
		}
		ActionPickerItem actionPickerItem = actionPickerItemList[actionPickerItemList.Length - 1];
		GameObject gameObject = UnityEngine.Object.Instantiate(actionPickerItem.gameObject, actionPickerItem.transform.parent);
		gameObject.name = actionPickerItem.gameObject.name + "_ImmersiveFire";
		ActionPickerItem component = gameObject.GetComponent<ActionPickerItem>();
		if (component == null)
		{
			UnityEngine.Object.Destroy(gameObject);
			return false;
		}
		component.m_ActionPicker = picker;
		component.m_Index = actionPickerItemList.Length;
		gameObject.SetActive(value: false);
		Il2CppReferenceArray<ActionPickerItem> il2CppReferenceArray = new Il2CppReferenceArray<ActionPickerItem>(actionPickerItemList.Length + 1);
		for (int i = 0; i < actionPickerItemList.Length; i++)
		{
			il2CppReferenceArray[i] = actionPickerItemList[i];
		}
		il2CppReferenceArray[actionPickerItemList.Length] = component;
		picker.m_ActionPickerItemList = il2CppReferenceArray;
		Verbose.Log($"Fire menu: added a button ({il2CppReferenceArray.Length} now)");
		return true;
	}
}
