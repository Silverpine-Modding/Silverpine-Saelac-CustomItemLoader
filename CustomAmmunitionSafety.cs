#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace SilverpineMods.CustomItemLoader;

internal static class CustomAmmunitionSafety
{
    private static readonly HashSet<string> warned = new(StringComparer.OrdinalIgnoreCase);
    private static readonly System.Reflection.FieldInfo ammunitionName =
        AccessTools.Field(typeof(ItemComponent_RangedWeapon), "ammunitionItemName");

    internal static string GetName(ItemComponent_RangedWeapon weapon) =>
        (string?)ammunitionName.GetValue(weapon) ?? "";

    internal static bool IsCustom(Item? item) =>
        item?.spriteName?.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) == true;

    // Run after all packs/plugins have registered, so forward references work.
    internal static void ValidateRegisteredWeapons()
    {
        foreach (Item item in ItemLibrary.Items)
        {
            if (!IsCustom(item)) continue;
            foreach (var weapon in item.GetItemComponents<ItemComponent_RangedWeapon>())
            {
                string name = GetName(weapon);
                Item? ammo = ItemLibrary.Items.FirstOrDefault(i => i != null && i.name == name);
                if (ammo?.GetItemComponent<ItemComponent_Charges>() != null) continue;
                if (warned.Add(item.spriteName + "|" + name))
                    Plugin.Log.LogError("Custom ranged item '" + item.name + "' (" + item.spriteName +
                        ") references invalid ammunition '" + name +
                        "'. Ammunition must name a registered item with ItemComponent_Charges. " +
                        "The weapon cannot fire until usable ammunition is available.");
            }
        }
    }
}

[HarmonyPatch(typeof(ItemComponent_RangedWeapon), nameof(ItemComponent_RangedWeapon.OnUse))]
internal static class CustomRangedUseSafetyPatch
{
    private static bool Prefix(ItemComponent_RangedWeapon __instance)
    {
        if (!CustomAmmunitionSafety.IsCustom(__instance.item)) return true;
        string name = CustomAmmunitionSafety.GetName(__instance);
        Item? ammo = string.IsNullOrWhiteSpace(name) ? null : __instance.item.inventory?.GetItemByName(name);
        ItemComponent_Charges? charges = ammo?.GetItemComponent<ItemComponent_Charges>();
        if (charges != null && charges.HasCharge()) return true;
        // Native OnUse dereferences the ammo even in its missing-ammo branch.
        if (UpperNotificationUI.Instance != null)
            UpperNotificationUI.Instance.OneOff("No usable " + (string.IsNullOrWhiteSpace(name) ? "ammunition" : name) + ".");
        return false;
    }
}
