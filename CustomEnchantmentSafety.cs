#nullable enable
using System;
using HarmonyLib;

namespace SilverpineMods.CustomItemLoader;

[HarmonyPatch(typeof(ItemComponent_Enchantment), nameof(ItemComponent_Enchantment.GetMultiplier))]
internal static class CustomEnchantmentValuePatch
{
    private static bool Prefix(ItemComponent_Enchantment __instance, ref float __result)
    {
        Item? item = __instance.item;
        if (item?.value != 0 || item.spriteName?.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) != true)
            return true;
        // Native code divides 60 by the item's base price. Zero is a supported
        // custom price, so leave it valueless instead of throwing in inventory.
        __result = 1f;
        return false;
    }
}
