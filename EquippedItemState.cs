#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using Silverpine.ModdingTools;
using UnityEngine;

namespace SilverpineMods.CustomItemLoader;

// Native saves already contain the applied totals. Keep the exact contributions
// alongside them; never replay OnEquipped during load.
internal static class EquippedItemState
{
    internal sealed class ArmorSnapshot
    {
        public Dictionary<DamageType, int> Values = new();
        public Quality Quality;
    }
    internal sealed class Snapshot
    {
        public int Slot;
        public string SpriteKey = "";
        public AttributeModifiersDefinition Basic = new();
        public List<ArmorSnapshot> Armor = new();
    }
    private static readonly Dictionary<Item, Snapshot> equipped = new();
    private static List<Snapshot> loaded = new();
    private static ModRegistration? registration;
    private static int generation;
    private static readonly System.Reflection.MethodInfo armorValues =
        AccessTools.Method(typeof(ItemComponent_Clothing), "GetArmorValues");

    internal static void Register()
    {
        if (registration != null)
            return;
        registration = ModSaveData.Register(Plugin.PluginGuid, new ModSaveDataDefinition
        {
            Id = Plugin.PluginGuid + ".equipment", CurrentVersion = 1,
            Capture = Capture,
            Restore = RestorePayload,
            Reset = () => { generation++; equipped.Clear(); loaded.Clear(); }
        });
        ModSaveData.Loaded += OnLoaded;
    }

    internal static void Dispose()
    {
        ModSaveData.Loaded -= OnLoaded;
        registration?.Dispose();
        registration = null;
    }

    internal static Snapshot? Get(Item item) =>
        equipped.TryGetValue(item, out var saved) ? saved : null;

    internal static void BeginEquip(Item item)
    {
        if (!IsCustom(item)) return;
        equipped.Remove(item);
        equipped[item] = MakeSnapshot(item);
    }

    internal static void EndUnequip(Item item) => equipped.Remove(item);

    private static void RestorePayload(string json)
    {
        var records = JsonConvert.DeserializeObject<List<Snapshot>>(json)
            ?? throw new System.IO.InvalidDataException("Missing equipment snapshot list.");
        if (records.Count > 10000 || records.Any(s => s == null ||
                string.IsNullOrWhiteSpace(s.SpriteKey) || s.Basic == null || s.Armor == null ||
                s.Armor.Any(a => a == null || a.Values == null || !Enum.IsDefined(typeof(Quality), a.Quality))) ||
            records.Select(s => s.Slot).Distinct().Count() != records.Count)
            throw new System.IO.InvalidDataException("Invalid equipment snapshot data.");
        loaded = records;
    }

    private static bool IsCustom(Item item) =>
        item?.spriteName?.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) == true;

    private static Snapshot MakeSnapshot(Item item)
    {
        var result = new Snapshot { SpriteKey = item.spriteName };
        if (Plugin.AttributeModifiers.TryGetValue(item.spriteName, out var values))
            result.Basic = JsonConvert.DeserializeObject<AttributeModifiersDefinition>(
                JsonConvert.SerializeObject(values))!;
        foreach (var clothing in item.GetItemComponents<ItemComponent_Clothing>())
            result.Armor.Add(new ArmorSnapshot
            {
                Values = new Dictionary<DamageType, int>(
                    (Dictionary<DamageType, int>)armorValues.Invoke(clothing, null)!),
                Quality = clothing.Quality
            });
        return result;
    }

    internal static bool TryGetArmor(ItemComponent_Clothing clothing, out ArmorSnapshot saved)
    {
        saved = null!;
        if (clothing.item == null || !equipped.TryGetValue(clothing.item, out var snapshot)) return false;
        int index = clothing.item.GetItemComponents<ItemComponent_Clothing>().IndexOf(clothing);
        if (index < 0 || index >= snapshot.Armor.Count) return false;
        saved = snapshot.Armor[index];
        return true;
    }

    private static string Capture()
    {
        var records = new List<Snapshot>();
        if (Player.Instance != null)
            foreach (var pair in Player.Instance.equippedItems)
            {
                if (!IsCustom(pair.Value)) continue;
                if (!equipped.TryGetValue(pair.Value, out var saved))
                    equipped[pair.Value] = saved = MakeSnapshot(pair.Value);
                saved.Slot = (int)pair.Key;
                records.Add(saved);
            }
        return JsonConvert.SerializeObject(records);
    }

    private static void OnLoaded()
    {
        // Player.Deserialize resolves inventory items in a one-frame callback.
        // Run after that queue has populated the saved slot map.
        int expected = generation;
        Player player = Player.Instance;
        if (player == null || ActionQueue.Instance == null) return;
        ActionQueue.Instance.DoAfterXFrames(2, () =>
        {
            if (expected != generation || player != Player.Instance) return;
            RestoreEquipment(player);
        });
    }

    private static void RestoreEquipment(Player player)
    {
        var entries = player.equippedItems.ToArray();
        foreach (var pair in entries)
        {
            Item item = pair.Value;
            if (!IsCustom(item)) continue;
            var saved = loaded.FirstOrDefault(s => s.Slot == (int)pair.Key &&
                string.Equals(s.SpriteKey, item.spriteName, StringComparison.OrdinalIgnoreCase));
            equipped[item] = saved ?? MakeSnapshot(item);
            if (saved == null)
                Plugin.Log.LogWarning("No historical equipment snapshot for '" + item.name +
                    "'; assuming its current modifiers were applied in this older save.");
        }
        loaded.Clear();

        // Prefer occupants whose slot did not change. Displaced items remain in
        // inventory and have their saved contributions removed exactly once.
        player.equippedItems.Clear();
        var seen = new HashSet<Item>();
        var displaced = new List<Item>();
        foreach (var pair in entries.OrderBy(p => p.Value != null &&
                     p.Key == p.Value.GetEquipmentSlotType() ? 0 : 1))
        {
            Item item = pair.Value;
            if (item == null || !seen.Add(item)) continue;
            EquipmentSlotType slot = item.GetEquipmentSlotType();
            if (slot == EquipmentSlotType.NotEquipable || player.equippedItems.ContainsKey(slot))
                displaced.Add(item);
            else player.equippedItems.Add(slot, item);
        }
        foreach (Item item in displaced)
        {
            item.OnUnequipped();
            Plugin.Log.LogWarning("Unequipped '" + item.name +
                "' because its saved slot changed or is occupied; it remains in inventory.");
        }
        AccessTools.Method(typeof(Player), "UpdateClothedSprite").Invoke(player, null);
    }
}

[HarmonyPatch(typeof(ItemComponent_Clothing), "GetArmorQualityMultiplied")]
internal static class SavedClothingQualityPatch
{
    private static bool Prefix(ItemComponent_Clothing __instance, int armor, ref int __result)
    {
        if (!EquippedItemState.TryGetArmor(__instance, out var saved)) return true;
        __result = Mathf.RoundToInt(armor * (1f + (float)saved.Quality * 0.3f));
        return false;
    }
}
