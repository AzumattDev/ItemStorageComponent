using System;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace ItemStorageComponent;

[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Start))]
static class FejdStartup_Start_Patch
{
    static void Postfix()
    {
        if (!ItemStorageComponentPlugin.modEnabled.Value)
            return;
        ItemStorageComponentPlugin.LoadDataFromDisk();
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
static class InventoryGui_Awake_Patch
{
    static void Postfix(InventoryGrid ___m_playerGrid)
    {
        if (!ItemStorageComponentPlugin.modEnabled.Value)
            return;

        ___m_playerGrid.m_onSelected =
            (Action<InventoryGrid, ItemDrop.ItemData, Vector2i, InventoryGrid.Modifier>)Delegate.Combine(
                ___m_playerGrid.m_onSelected,
                new Action<InventoryGrid, ItemDrop.ItemData, Vector2i, InventoryGrid.Modifier>(
                    ItemStorageComponentPlugin.OnSelectedItem));
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
static class InventoryGui_OnSelectedItem_Patch
{
    static bool Prefix(ItemDrop.ItemData item, InventoryGrid.Modifier mod)
    {
        var result = !ItemStorageComponentPlugin.modEnabled.Value || !ItemStorageComponentPlugin.CanBeContainer(item) ||
                     mod != InventoryGrid.Modifier.Split;
        //Dbgl("result " + result + " " + !modEnabled.Value + " " + !CanBeContainer(item) + " "+(mod != InventoryGrid.Modifier.Split));
        return result;
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
static class InventoryGui_Update_Patch
{
    static void Postfix(Animator ___m_animator, ref Container ___m_currentContainer, ItemDrop.ItemData ___m_dragItem)
    {
        if (!ItemStorageComponentPlugin.modEnabled.Value || ___m_animator.GetBool("visible") ||
            ItemStorageComponentPlugin.playerContainer == null || ItemStorageComponentPlugin.itemStorage == null)
            return;

        ItemStorageComponentPlugin.CloseContainer();
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.GetTotalWeight))]
[HarmonyPriority(Priority.Last)]
static class GetTotalWeight_Patch
{
    static void Postfix(Inventory __instance, ref float __result)
    {
        if (!ItemStorageComponentPlugin.modEnabled.Value || !ItemStorageComponentPlugin.playerContainer ||
            !Player.m_localPlayer)
            return;
        if (__instance == Player.m_localPlayer.GetInventory())
        {
            if (new StackFrame(2).ToString().IndexOf("OverrideGetTotalWeight") > -1)
            {
                return;
            }

            foreach (ItemDrop.ItemData item in __instance.GetAllItems())
            {
                if (item.m_crafterName.Contains("_") &&
                    ItemStorageComponentPlugin.itemStorageDict.ContainsKey(item.m_crafterName.Split('_')[1]))
                {
                    __result +=
                        ItemStorageComponentPlugin.itemStorageDict[item.m_crafterName.Split('_')[1]].inventory
                            .GetTotalWeight() * ItemStorageComponentPlugin
                            .itemStorageDict[item.m_crafterName.Split('_')[1]].meta.weightMult;
                }
            }
        }
    }
}

[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
    new Type[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool) })]
static class GetTooltip_Patch
{
    static void Postfix(ItemDrop.ItemData item, ref string __result)
    {
        if (!ItemStorageComponentPlugin.modEnabled.Value || !item.m_crafterName.Contains("_") ||
            item.m_crafterName.Split('_')[item.m_crafterName.Split('_').Length - 1].Length != 36)
            return;

        __result = __result.Replace(item.m_crafterName, item.m_crafterName.Split('_')[0]);
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), new Type[] { typeof(GameObject), typeof(int) })]
static class CanAddItem_Patch1
{
    static bool Prefix(ref bool __result, Inventory __instance, GameObject prefab)
    {
        if (!ItemStorageComponentPlugin.modEnabled.Value)
            return true;

        if (!ItemStorageComponentPlugin.ItemIsAllowed(__instance.GetName(), prefab.name))
        {
            __result = false;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), new Type[] { typeof(ItemDrop.ItemData), typeof(int) })]
static class CanAddItem_Patch2
{
    static bool Prefix(ref bool __result, Inventory __instance, ItemDrop.ItemData item)
    {
        if (!ItemStorageComponentPlugin.modEnabled.Value || item.m_dropPrefab == null)
            return true;

        if (!ItemStorageComponentPlugin.ItemIsAllowed(__instance.GetName(), item.m_dropPrefab.name))
        {
            __result = false;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new Type[] { typeof(ItemDrop.ItemData) })]
static class AddItem_Patch1
{
    static bool Prefix(ref bool __result, Inventory __instance, ItemDrop.ItemData item)
    {
        if (!ItemStorageComponentPlugin.modEnabled.Value || item.m_dropPrefab == null)
            return true;

        if (!ItemStorageComponentPlugin.ItemIsAllowed(__instance.GetName(), item.m_dropPrefab.name))
        {
            __result = false;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem),
    new Type[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int) })]
static class AddItem_Patch2
{
    static bool Prefix(ref bool __result, Inventory __instance, ItemDrop.ItemData item)
    {
        if (!ItemStorageComponentPlugin.modEnabled.Value || item.m_dropPrefab == null)
            return true;

        if (!ItemStorageComponentPlugin.ItemIsAllowed(__instance.GetName(), item.m_dropPrefab.name))
        {
            __result = false;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem),
    new Type[]
    {
        typeof(string), typeof(int), typeof(float), typeof(Vector2i), typeof(bool), typeof(int), typeof(int),
        typeof(long), typeof(string)
    })]
static class AddItem_Patch3
{
    static bool Prefix(ref bool __result, Inventory __instance, string name)
    {
        if (!ItemStorageComponentPlugin.modEnabled.Value)
            return true;

        if (!ItemStorageComponentPlugin.ItemIsAllowed(__instance.GetName(), name))
        {
            __result = false;
            return false;
        }

        return true;
    }
}