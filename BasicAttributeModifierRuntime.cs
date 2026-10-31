#nullable enable
using HarmonyLib;

namespace SilverpineMods.CustomItemLoader;

internal static class BasicAttributeModifierRuntime
{
    internal static void Apply(Item item, int direction)
    {
        Player? player = Player.Instance;
        if (player == null ||
            string.IsNullOrWhiteSpace(item.spriteName) ||
            EquippedItemState.Get(item) is not { } snapshot)
            return;

        AttributeModifiersDefinition values = snapshot.Basic;
        player.damagable.MaxHP += direction * values.maxHealth;
        player.energy.ChangeMax(direction * values.maxEnergy);
        player.worldInventory.inventory.maxBulk += direction * values.carryCapacity;
        player.damagable.armorValues[DamageType.Normal] += direction * values.normalArmor;
        player.damagable.armorValues[DamageType.Fire] += direction * values.fireArmor;
        player.damagable.armorValues[DamageType.Frost] += direction * values.frostArmor;
        player.worldInventory.inventory.UpdateEncumbered();
        if (InventoryUI.Instance != null) InventoryUI.Instance.UpdateBulkBar();
    }
}

[HarmonyPatch(typeof(Item), nameof(Item.OnEquipped))]
internal static class CustomItemAttributeEquippedPatch
{
    private static void Prefix(Item __instance) => EquippedItemState.BeginEquip(__instance);
    private static void Postfix(Item __instance) => BasicAttributeModifierRuntime.Apply(__instance, 1);
}

[HarmonyPatch(typeof(Item), nameof(Item.OnUnequipped))]
internal static class CustomItemAttributeUnequippedPatch
{
    private static void Postfix(Item __instance)
    {
        BasicAttributeModifierRuntime.Apply(__instance, -1);
        EquippedItemState.EndUnequip(__instance);
    }
}
