// Headless contracts for the narrowly linked production classes. These tests
// exercise our logic, not Unity rendering or the native Harmony dispatcher.
using SilverpineMods.CustomItemLoader;
using SilverpineMods.AttributeExpansion;

public enum DamageType { Normal, True, Fire, Frost }
public enum Quality { Normal, Good, Great }
public enum EquipmentSlotType { NotEquipable, Head, Ring, Neck }
public sealed class Damagable
{
    public int MaxHP = 100;
    public Dictionary<DamageType, int> armorValues = Enum.GetValues<DamageType>().ToDictionary(t => t, _ => 0);
}
public class Item
{
    public string name = "Test", spriteName = "custom:test:ring";
    public int value;
    public Inventory inventory = null!;
    public EquipmentSlotType Slot = EquipmentSlotType.Ring;
    public List<object> Components = new();
    public EquipmentSlotType GetEquipmentSlotType() => Slot;
    public List<T> GetItemComponents<T>() => Components.OfType<T>().ToList();
    public T? GetItemComponent<T>() where T : class => Components.OfType<T>().FirstOrDefault();
    public void OnEquipped()
    {
        EquippedItemState.BeginEquip(this);
        BasicAttributeModifierRuntime.Apply(this, 1);
        ArmorPersistence.Clamp(Player.Instance.damagable);
    }
    public void OnUnequipped()
    {
        BasicAttributeModifierRuntime.Apply(this, -1);
        EquippedItemState.EndUnequip(this);
        ArmorPersistence.Clamp(Player.Instance.damagable);
    }
}
public sealed class ItemComponent_Clothing
{
    public Item item = null!;
    public Quality Quality;
    public Dictionary<DamageType, int> Configured = Enum.GetValues<DamageType>().ToDictionary(t => t, _ => 0);
    private Dictionary<DamageType, int> GetArmorValues() => Configured;
    private int GetArmorQualityMultiplied(int armor) => armor;
}
public sealed class ItemComponent_RangedWeapon
{
    public Item item = null!;
    private string ammunitionItemName = "Arrows";
    public void OnUse() { }
}
public sealed class ItemComponent_Charges { public int Count = 1; public bool HasCharge() => Count > 0; }
public sealed class ItemComponent_Enchantment
{
    public Item item = null!;
    public float GetMultiplier() => 1 + 60 / item.value;
}
public sealed class Inventory
{
    public int maxBulk = 100;
    public List<Item> items = new();
    public int Capacity = int.MaxValue;
    public void MoveAllItemsTo(Inventory target, Predicate<Item> predicate)
    {
        foreach (Item item in items.Where(i => predicate(i)).ToArray())
            if (target.items.Count < target.Capacity) { items.Remove(item); target.items.Add(item); }
    }
    public List<Item> GetItemsWithPredicate(Predicate<Item> predicate) => items.FindAll(predicate);
    public void UpdateEncumbered() { }
    public Item? GetItemByName(string name) => items.FirstOrDefault(i => i.name == name);
}
public sealed class WorldInventory { public Inventory inventory = new(); }
public sealed class Resource { public int Max = 100; public void ChangeMax(int change) => Max += change; }
public sealed class Player
{
    public static Player Instance = new();
    public Damagable damagable = new();
    public Resource energy = new();
    public WorldInventory worldInventory = new();
    public Dictionary<EquipmentSlotType, Item> equippedItems = new();
    private void UpdateClothedSprite() { }
}
public sealed class InventoryUI { public static InventoryUI? Instance; public void UpdateBulkBar() { } }
public sealed class UpperNotificationUI { public static UpperNotificationUI Instance = new(); public void OneOff(string message) { } }
public static class ItemLibrary { public static List<Item> Items = new(); }
public sealed class MainMenuUI { }
public sealed class ActionQueue
{
    public static ActionQueue Instance = new();
    public Queue<Action> Pending = new();
    public void DoAfterXFrames(int frames, Action action) => Pending.Enqueue(action);
    public void Flush() { while (Pending.Count > 0) Pending.Dequeue()(); }
}
public sealed class VersionSafeStorage
{
    private Dictionary<string, object> storage = new();
    public BinaryWriter CreateBinaryWriter() => new(new MemoryStream());
    public void AddToStorage(string key, BinaryWriter writer) => storage.Add(key, ((MemoryStream)writer.BaseStream).ToArray());
    public bool TryCreateBinaryReader(string key, out BinaryReader reader)
    { reader = new(new MemoryStream((byte[])storage[key])); return true; }
    public void Serialize(BinaryWriter writer)
    {
        writer.Write(storage.Count);
        foreach (var pair in storage) { writer.Write(pair.Key); var b = (byte[])pair.Value; writer.Write(b.Length); writer.Write(b); }
    }
    public void Deserialize(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++) { string key = reader.ReadString(); storage.Add(key, reader.ReadBytes(reader.ReadInt32())); }
    }
}
namespace UnityEngine
{
    public static class Mathf { public static int RoundToInt(float x) => (int)Math.Round(x); }
    public class Object
    {
        public bool Destroyed;
        public static bool operator ==(Object? a, Object? b)
        {
            bool aNull = ReferenceEquals(a, null) || a.Destroyed;
            bool bNull = ReferenceEquals(b, null) || b.Destroyed;
            return aNull || bNull ? aNull && bNull : ReferenceEquals(a, b);
        }
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public sealed class Material : Object { }
    public sealed class SpriteRenderer : Object
    {
        public Material? sharedMaterial;
        public string color = "white";
    }
    public sealed class GameObject : Object
    {
        private readonly Dictionary<Type, Object> components = new();
        public T GetComponent<T>() where T : Object => components.TryGetValue(typeof(T), out var value) ? (T)value : null!;
        public T AddComponent<T>() where T : Object, new() { var value = new T(); components[typeof(T)] = value; return value; }
    }
    public static class Resources
    {
        public static Material? Diffuse;
        public static int LoadCalls;
        public static string? LastPath;
        public static T? Load<T>(string path) where T : Object
        { LoadCalls++; LastPath = path; return Diffuse as T; }
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { }
    public enum LoadSceneMode { Single, Additive }
    public static class SceneManager
    {
        public static event Action<Scene, LoadSceneMode>? sceneLoaded;
        public static int SubscriberCount => sceneLoaded?.GetInvocationList().Length ?? 0;
        public static void Load() => sceneLoaded?.Invoke(new Scene(), LoadSceneMode.Single);
        public static void Reset() => sceneLoaded = null;
    }
}
namespace SilverpineMods.AttributeExpansion
{
    internal static class Plugin { internal static SilverpineMods.CustomItemLoader.TestLog Log = new(); }
}
namespace HarmonyLib
{
    // The bundled Harmony targets Unity Mono and cannot run under .NET 9.
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
    public static class AccessTools
    {
        public static System.Reflection.MethodInfo Method(Type type, string name) =>
            type.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance)!;
        public static System.Reflection.FieldInfo Field(Type type, string name) =>
            type.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance)!;
    }
    public sealed class CodeInstruction
    {
        public System.Reflection.Emit.OpCode opcode; public object? operand;
        public CodeInstruction(System.Reflection.Emit.OpCode op, object? value = null) { opcode = op; operand = value; }
        public CodeInstruction(CodeInstruction other) { opcode = other.opcode; operand = other.operand; }
        public bool Calls(System.Reflection.MethodInfo method) => Equals(operand, method);
    }
}
namespace Silverpine.ModdingTools
{
    public sealed class ModRegistration : IDisposable
    {
        private Action? onDispose;
        public ModRegistration(Action action) => onDispose = action;
        public void Dispose() { var action = onDispose; onDispose = null; action?.Invoke(); }
    }
    public sealed class ModSaveDataDefinition
    {
        public string Id = ""; public int CurrentVersion;
        public Func<string> Capture = null!; public Action<string> Restore = null!; public Action Reset = null!;
    }
    public static class ModSaveData
    {
        public static ModSaveDataDefinition Definition = null!;
        public static event Action? Loaded;
        public static int RegisterCalls, ActiveRegistrations;
        public static int LoadedSubscribers => Loaded?.GetInvocationList().Length ?? 0;
        public static ModRegistration Register(string owner, ModSaveDataDefinition definition)
        {
            RegisterCalls++; ActiveRegistrations++; Definition = definition;
            return new(() => ActiveRegistrations--);
        }
        public static void NotifyLoaded() => Loaded?.Invoke();
    }
}
namespace SilverpineMods.CustomItemLoader
{
    internal sealed class AttributeModifiersDefinition
    { public int maxHealth, maxEnergy, carryCapacity, normalArmor, fireArmor, frostArmor; }
    internal static class Plugin
    {
        public const string PluginGuid = "regression.tests";
        public static TestLog Log = new();
        public static Dictionary<string, AttributeModifiersDefinition> AttributeModifiers = new();
        public static Action ResolveVisuals = () => { };
        internal static void ResolveAvailableInheritedVisuals() => ResolveVisuals();
    }
    public sealed class CustomItemInfo { public Item Template = new(); }
    public static partial class CustomItemApi
    {
        private static readonly object Sync = new();
        internal static readonly List<CustomItemInfo> TestItems = new();
        public static IReadOnlyList<CustomItemInfo> GetRegisteredItems() => TestItems.ToArray();
    }
    internal sealed class TestLog
    {
        public List<string> Messages = new();
        public void LogInfo(string message) => Messages.Add(message);
        public void LogWarning(string message) => Messages.Add(message);
        public void LogError(string message) => Messages.Add(message);
    }
}
