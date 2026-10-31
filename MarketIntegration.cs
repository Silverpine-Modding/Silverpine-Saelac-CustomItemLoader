#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SilverpineMods.CustomItemLoader;

/// <summary>
/// Reconciles custom market choices after Silverpine creates or restores the
/// save-specific market list. Running after Market.Start also makes changed
/// pack settings apply to existing saves.
/// </summary>
[HarmonyPatch(typeof(Market), "Start")]
internal static class CustomItemMarketStartPatch
{
    [HarmonyPostfix]
    private static void Postfix(Market __instance)
    {
        if (__instance.marketItems == null)
            return;

        IReadOnlyList<CustomItemInfo> customItems =
            CustomItemApi.GetRegisteredItems();
        CustomMarketStockRecovery.ReturnExcludedStock(__instance, warnIfFull: true);
        int included = 0;
        int excluded = 0;
        foreach (CustomItemInfo customItem in customItems)
        {
            bool shouldInclude = ShouldInclude(customItem);

            if (!shouldInclude)
            {
                int removed = __instance.marketItems.RemoveAll(entry =>
                    IsEntryFor(entry, customItem.DisplayName));
                excluded += removed;
                continue;
            }

            if (__instance.marketItems.Exists(entry =>
                    IsEntryFor(entry, customItem.DisplayName)))
                continue;

            Item item = customItem.Template;
            __instance.marketItems.Add(new MarketItem(
                item.name,
                item.value * 0.25f,
                item.value * 2f,
                item.value * 0.5f));
            included++;
        }

        if (included > 0 || excluded > 0)
            Plugin.Log.LogInfo(
                $"Reconciled custom market entries: added {included}, " +
                $"removed {excluded}.");
    }

    private static bool IsAutomaticallyIncluded(Item item) =>
        item.spriteName.ContainsAnyWordIgnoreCase("herb", "ore") ||
        item.name == "Turnip";

    internal static bool ShouldInclude(CustomItemInfo item) => item.MarketBehavior switch
    {
        CustomItemMarketBehavior.Include => true,
        CustomItemMarketBehavior.Exclude => false,
        _ => IsAutomaticallyIncluded(item.Template)
    };

    private static bool IsEntryFor(MarketItem? entry, string itemName) =>
        entry != null &&
        string.Equals(
            entry.itemName,
            itemName,
            StringComparison.OrdinalIgnoreCase);
}

[HarmonyPatch(typeof(Market), "SellStock")]
internal static class CustomMarketStockRecovery
{
    private static readonly FieldInfo Stock = AccessTools.Field(typeof(Market), "marketInventory");
    private static readonly FieldInfo Crate = AccessTools.Field(typeof(Market), "worldInventory");

    private static void Prefix(Market __instance) => ReturnExcludedStock(__instance, warnIfFull: false);

    internal static void ReturnExcludedStock(Market market, bool warnIfFull)
    {
        if (Stock.GetValue(market) is not Inventory source ||
            Crate.GetValue(market) is not WorldInventory world || world.inventory == null) return;
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CustomItemInfo item in CustomItemApi.GetRegisteredItems())
            if (!CustomItemMarketStartPatch.ShouldInclude(item)) excluded.Add(item.DisplayName);
        if (excluded.Count == 0) return;
        int remaining = MarketStockRecovery.ReturnToCrate(source, world.inventory, excluded);
        if (remaining > 0 && warnIfFull)
            Plugin.Log.LogWarning($"{remaining} excluded custom market item(s) are waiting for room in the market crate. They are preserved and will be returned on the next sale check with available space.");
    }
}
