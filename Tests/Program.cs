using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mono.Cecil;
using Silverpine.ModdingTools;
using SilverpineMods.CustomItemLoader;
using SilverpineMods.AttributeExpansion;
using Plugin = SilverpineMods.CustomItemLoader.Plugin;

int passed = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception("FAIL: " + message);
    Console.WriteLine("PASS: " + message); passed++;
}

float panelBudget = 2 * EditorFormLayout.OuterInset + EditorFormLayout.SidebarWidth +
    EditorFormLayout.PreviewWidth + EditorFormLayout.DetailsWidth + 2 * EditorFormLayout.PanelGap;
Check(panelBudget == EditorFormLayout.CanvasWidth, "editor panels and explicit gutters fit the ModTools logical canvas");
Check(EditorFormLayout.ContentWidth + 2 * EditorFormLayout.PanelInset + EditorFormLayout.ScrollbarReserve == EditorFormLayout.DetailsWidth,
    "form reserves panel padding and scrollbar space before assigning columns");
float columnWidth = EditorFormLayout.ColumnWidth(EditorFormLayout.ContentWidth);
Check(2 * columnWidth + EditorFormLayout.ColumnGap == EditorFormLayout.ContentWidth && columnWidth >= EditorFormLayout.MinimumColumnWidth,
    "compact fields receive equal bounded columns instead of content-weighted widths");
Check(!EditorFormLayout.UseColumns(623) && EditorFormLayout.UseColumns(624), "narrow form regions stack fields before columns become cramped");
Check(EditorFormLayout.ChoiceHeight(38) == 44, "single-line selector values retain the standard control height");
Check(EditorFormLayout.ChoiceHeight(61.3f) == 64, "wrapped selector height includes all measured lines and rounding slack");
Check(EditorFormLayout.ChoiceHeight(110) == 112, "long addon selectors are not clipped to a one-line height");
Check(EditorFormLayout.TextInputHeight(38, 0) == 44, "short text inputs retain a usable single-line minimum");
Check(EditorFormLayout.TextInputHeight(38, 70) == 70, "description inputs retain their multiline minimum");
Check(EditorFormLayout.TextInputHeight(105.3f, 70) == 108, "wrapped text expands beyond the description minimum with rounding slack");
Check(EditorFormLayout.TextInputHeight(600, 70) == 602, "long descriptions are not capped to a one-line or fixed-height box");
Check(EditorFormLayout.TextInputHeight(60, 70) < EditorFormLayout.TextInputHeight(160, 70), "text boxes shrink again when text is removed");
Check(EditorFormLayout.TextMeasurement("") == "M", "empty inputs still measure one font line");
Check(EditorFormLayout.TextMeasurement("First line\n\n") == "First line\n\nM", "trailing blank lines are included in multiline height measurement");
Check(EditorFormLayout.TextMeasurement("First line\r\n") == "First line\r\nM", "pasted Windows line endings retain a measurable final line");
Check(EditorFormLayout.TextMeasurement("Ordinary text") == "Ordinary text", "nonempty lines are measured without extra wrapping characters");
foreach (float width in new[] { 300f, columnWidth, EditorFormLayout.ContentWidth })
{
    float choiceWidth = EditorFormLayout.ChoiceWidth(width, 2000, 56);
    Check(choiceWidth + 112 + EditorFormLayout.ChoiceSpacing <= width && choiceWidth >= 0,
        "long choice names stay inside their field width " + width);
    float inputWidth = EditorFormLayout.TextInputWidth(width, 8, 120);
    Check(inputWidth + 8 + 120 == width && inputWidth > 0,
        "wrapped input reserves margins and adjacent buttons within width " + width);
}
Check(EditorFormLayout.TextInputWidth(EditorFormLayout.SidebarWidth - 2 * EditorFormLayout.PanelInset, 8) == 296,
    "pack ID input uses the sidebar width rather than the details panel width");
Check(EditorFormLayout.TextInputWidth(20, 8, 120) == 1, "text measurement width remains positive for exhausted rows");
foreach (var screen in new[] { (1280, 720), (1920, 1080), (2560, 1080), (3636, 2037) })
{
    double scale = Math.Min(screen.Item1 / 1920d, screen.Item2 / 1080d);
    Check(panelBudget * scale <= screen.Item1 + .001,
        $"logical panel budget fits the scaled {screen.Item1}x{screen.Item2} viewport");
}

var raw = new CappedArmorValue(240);
Check(raw.Observe(260) == 254 && raw.Raw == 260, "armor cap retains overflow");
Check(raw.Observe(234) == 240, "240 + 20 capped then -20 returns to 240");
raw = new CappedArmorValue(400);
Check(raw.Observe(234) == 254 && raw.Raw == 380, "stacked overflow survives partial unequip");
Check(raw.Observe(54) == 180, "remaining overflow is consumed correctly");
raw = new CappedArmorValue(5);
Check(raw.Observe(-5) == 0 && raw.Observe(10) == 5, "negative armor bonus can be removed without gaining armor");

byte[] SaveArmor(Damagable target)
{
    using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
    ArmorPersistence.Write(new VersionSafeStorage(), writer, target);
    return stream.ToArray();
}
void LoadArmor(Damagable target, byte[] data)
{
    ArmorPersistence.BeforeLoad(target);
    using var reader = new BinaryReader(new MemoryStream(data));
    ArmorPersistence.Read(new VersionSafeStorage(), reader, target);
    ArmorPersistence.AfterLoad(target);
}

EquippedItemState.Register();
Player.Instance = new Player();
Player.Instance.damagable.armorValues[DamageType.Normal] = 240;
ArmorPersistence.Clamp(Player.Instance.damagable);
var item = new Item { inventory = Player.Instance.worldInventory.inventory };
Plugin.AttributeModifiers[item.spriteName] = new()
{ maxHealth = 20, maxEnergy = 20, carryCapacity = 20, normalArmor = 20, fireArmor = 5 };
Player.Instance.equippedItems.Add(EquipmentSlotType.Ring, item);
item.OnEquipped();
Check(Player.Instance.damagable.MaxHP == 120 && Player.Instance.damagable.armorValues[DamageType.Normal] == 254,
    "actual basic modifier path adds health and caps armor");
string equipmentSave = ModSaveData.Definition.Capture();
byte[] armorSave = SaveArmor(Player.Instance.damagable);
Plugin.AttributeModifiers[item.spriteName].maxHealth = 10;
Plugin.AttributeModifiers[item.spriteName].maxEnergy = 10;
Plugin.AttributeModifiers[item.spriteName].carryCapacity = 10;
Plugin.AttributeModifiers[item.spriteName].normalArmor = 10;

// Model native load: totals and inventory arrive before delayed equipment links.
ModSaveData.Definition.Reset();
Player.Instance = new Player();
Player.Instance.damagable.MaxHP = 120;
Player.Instance.energy.Max = 120;
Player.Instance.worldInventory.inventory.maxBulk = 120;
Player.Instance.damagable.armorValues[DamageType.Normal] = 254;
Player.Instance.damagable.armorValues[DamageType.Fire] = 5;
LoadArmor(Player.Instance.damagable, armorSave);
ModSaveData.Definition.Restore(equipmentSave);
ModSaveData.NotifyLoaded();
item = new Item { Slot = EquipmentSlotType.Head, inventory = Player.Instance.worldInventory.inventory };
Player.Instance.equippedItems.Add(EquipmentSlotType.Ring, item);
ActionQueue.Instance.Flush();
Check(Player.Instance.equippedItems[EquipmentSlotType.Head] == item && !Player.Instance.equippedItems.ContainsKey(EquipmentSlotType.Ring),
    "load rekeys a changed slot after native delayed equipment linking");
Check(Player.Instance.damagable.MaxHP == 120 && EquippedItemState.Get(item)!.Basic.maxHealth == 20,
    "load attaches saved contributions without applying them twice");
Player.Instance.equippedItems.Clear(); item.OnUnequipped();
Check(Player.Instance.damagable.MaxHP == 100 && Player.Instance.energy.Max == 100 &&
    Player.Instance.worldInventory.inventory.maxBulk == 100 && Player.Instance.damagable.armorValues[DamageType.Normal] == 240 &&
    Player.Instance.damagable.armorValues[DamageType.Fire] == 0,
    "changed pack bonuses and capped armor both undo exactly after save/load");
item.OnEquipped();
Check(Player.Instance.damagable.MaxHP == 110 && Player.Instance.damagable.armorValues[DamageType.Normal] == 250,
    "re-equipping uses newly configured bonuses");
item.OnUnequipped();

var clothing = new ItemComponent_Clothing { item = item, Quality = Quality.Good };
clothing.Configured[DamageType.Normal] = 20;
item.Components.Add(clothing); item.OnEquipped();
clothing.Configured[DamageType.Normal] = 2; clothing.Quality = Quality.Great;
Check(EquippedItemState.TryGetArmor(clothing, out var frozen) && frozen.Values[DamageType.Normal] == 20 && frozen.Quality == Quality.Good,
    "clothing defense and quality snapshots do not mutate with definitions");
item.OnUnequipped(); item.OnEquipped();
Check(EquippedItemState.TryGetArmor(clothing, out frozen) && frozen.Values[DamageType.Normal] == 2,
    "clothing snapshot refreshes on re-equip");
Player.Instance.equippedItems.Add(EquipmentSlotType.Ring, item);
equipmentSave = ModSaveData.Definition.Capture();
var occupant = new Item { name = "Native hat", spriteName = "hat", Slot = EquipmentSlotType.Head };
Player.Instance.equippedItems.Add(EquipmentSlotType.Head, occupant);
item.Slot = EquipmentSlotType.Head;
ModSaveData.Definition.Reset(); ModSaveData.Definition.Restore(equipmentSave);
ModSaveData.NotifyLoaded(); ActionQueue.Instance.Flush();
Check(Player.Instance.equippedItems.Count == 1 && Player.Instance.equippedItems[EquipmentSlotType.Head] == occupant &&
    Player.Instance.damagable.MaxHP == 100 && item.inventory != null,
    "slot collisions preserve the unchanged occupant and remove displaced bonuses");

item.Slot = EquipmentSlotType.Ring; item.OnEquipped();
Player.Instance.equippedItems.Add(EquipmentSlotType.Ring, item);
equipmentSave = ModSaveData.Definition.Capture(); item.Slot = EquipmentSlotType.NotEquipable;
ModSaveData.Definition.Reset(); ModSaveData.Definition.Restore(equipmentSave);
ModSaveData.NotifyLoaded(); ActionQueue.Instance.Flush();
Check(!Player.Instance.equippedItems.ContainsValue(item) && Player.Instance.damagable.MaxHP == 100,
    "a now non-equipable saved item is safely unequipped");

var npc = new Damagable(); npc.armorValues[DamageType.Normal] = 300; ArmorPersistence.Clamp(npc);
byte[] npcSave = SaveArmor(npc);
var restoredNpc = new Damagable(); restoredNpc.armorValues[DamageType.Normal] = 254;
LoadArmor(restoredNpc, npcSave); restoredNpc.armorValues[DamageType.Normal] -= 60; ArmorPersistence.Clamp(restoredNpc);
Check(restoredNpc.armorValues[DamageType.Normal] == 240, "non-player armor overflow also survives serialization");
var legacy = new Damagable(); legacy.armorValues[DamageType.Normal] = 200;
using (var bytes = new MemoryStream())
{
    new VersionSafeStorage().Serialize(new BinaryWriter(bytes));
    LoadArmor(legacy, bytes.ToArray());
}
Check(legacy.armorValues[DamageType.Normal] == 200, "old saves without overflow extensions retain native armor");
using (var bytes = new MemoryStream())
{
    var storage = new VersionSafeStorage();
    using var badPayload = storage.CreateBinaryWriter();
    badPayload.Write(9999);
    storage.AddToStorage("AttributeExpansion.RawArmor.v1", badPayload);
    storage.Serialize(new BinaryWriter(bytes));
    LoadArmor(legacy, bytes.ToArray());
}
Check(legacy.armorValues[DamageType.Normal] == 200, "invalid optional armor payload cannot block native entity loading");

bool CanUse(ItemComponent_RangedWeapon weapon) => (bool)typeof(CustomRangedUseSafetyPatch)
    .GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { weapon })!;
var bow = new Item { name = "Custom Bow", spriteName = "custom:test:bow", inventory = new Inventory() };
var ranged = new ItemComponent_RangedWeapon { item = bow }; bow.Components.Add(ranged);
Check(!CanUse(ranged), "missing ammunition safely blocks a custom ranged attack");
var ammo = new Item { name = "Arrows" }; bow.inventory.items.Add(ammo);
Check(!CanUse(ranged), "an item with the ammo name but no charges is rejected");
var charges = new ItemComponent_Charges { Count = 0 }; ammo.Components.Add(charges);
Check(!CanUse(ranged), "empty ammunition is rejected without a native null dereference");
charges.Count = 3;
Check(CanUse(ranged), "valid charged ammunition reaches native ranged use");
ItemLibrary.Items.Add(bow); ItemLibrary.Items.Add(ammo);
int warnings = Plugin.Log.Messages.Count; CustomAmmunitionSafety.ValidateRegisteredWeapons();
Check(Plugin.Log.Messages.Count == warnings, "post-registration validation accepts forward ammunition references");
bow.inventory = null!;
Check(!CanUse(ranged), "unowned ranged items cannot dereference a missing inventory");

bool NativeEnchantment(Item value, out float multiplier)
{
    object[] arguments = { new ItemComponent_Enchantment { item = value }, 99f };
    bool native = (bool)typeof(CustomEnchantmentValuePatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!
        .Invoke(null, arguments)!;
    multiplier = (float)arguments[1]; return native;
}
Check(!NativeEnchantment(new Item { value = 0 }, out float enchantmentMultiplier) && enchantmentMultiplier == 1,
    "zero-value custom enchantments bypass native division by zero without inflating price");
Check(NativeEnchantment(new Item { value = 60 }, out _) && NativeEnchantment(new Item { value = 0, spriteName = "ring" }, out _),
    "priced custom and native enchantments retain native valuation");
Check(!VisualRules.Changed(1, 1) && VisualRules.Changed(1, 2), "unchanged numeric controls do not rebuild preview textures");
Check(!VisualRules.Changed(1, float.NaN) && !VisualRules.Changed(1, float.PositiveInfinity), "non-finite numeric edits cannot reach preview geometry");
Check(!VisualRules.AllowTextureFallback(0) && VisualRules.AllowTextureFallback(-1), "known solid-color materials cannot inherit another material's texture");
Check(!VisualRules.HalfSizeWorldSprite(true, false, false), "GLB world size is independent of live model availability");
Check(VisualRules.HalfSizeWorldSprite(false, false, false) && !VisualRules.HalfSizeWorldSprite(false, true, false) &&
    !VisualRules.HalfSizeWorldSprite(false, false, true), "static sprites, pickupables and gold retain native sizing rules");

var stock = new Inventory(); var crate = new Inventory { Capacity = 1 };
var excludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Custom Ore" };
stock.items.AddRange(new[] { new Item { name = "Custom Ore" }, new Item { name = "Custom Ore" }, new Item { name = "Turnip" } });
Check(MarketStockRecovery.ReturnToCrate(stock, crate, excludedNames) == 1 && crate.items.Count == 1 && stock.items.Count == 2,
    "market exclusion returns unsold stock and preserves overflow when the crate is full");
crate.Capacity = 10;
Check(MarketStockRecovery.ReturnToCrate(stock, crate, excludedNames) == 0 && crate.items.Count == 2 && stock.items.Single().name == "Turnip",
    "market stock recovery retries without touching included goods");

string temporary = Path.Combine(Path.GetTempPath(), "cil-regression-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
try
{
    string first = Path.Combine(temporary, "first"), second = Path.Combine(temporary, "second"), assets = Path.Combine(temporary, "assets");
    Directory.CreateDirectory(first); Directory.CreateDirectory(second);
    string a = Path.Combine(first, "preview.png"), b = Path.Combine(second, "preview.png");
    File.WriteAllBytes(a, new byte[] { 1, 2 }); File.WriteAllBytes(b, new byte[] { 3, 4 });
    string importedA = AssetFiles.ImportImage(a, assets), importedB = AssetFiles.ImportImage(b, assets);
    Check(importedA != importedB && File.ReadAllBytes(importedA)[0] == 1, "same image filename cannot overwrite an older asset");
    Check(AssetFiles.ImportImage(a, assets) == importedA, "identical imported image bytes reuse an asset");
    File.WriteAllBytes(importedA, new byte[] { 5 });
    bool refused = false; try { AssetFiles.ImportImage(a, assets); } catch (IOException) { refused = true; }
    Check(refused && File.ReadAllBytes(importedA)[0] == 5, "an altered hash-named asset is never silently overwritten");
    Check(AssetFiles.CachePath(temporary, "pack", "chair__furniture_right", "") !=
        AssetFiles.CachePath(temporary, "pack", "chair", "__furniture_right"), "main cache cannot collide with a directional cache");
    Check(AssetFiles.CachePath(temporary, "a__b", "c", "") != AssetFiles.CachePath(temporary, "a", "b__c", ""),
        "pack/item delimiters cannot alias cache identities");
    Check(AssetFiles.CachePath(temporary, "p", "i", "/") != AssetFiles.CachePath(temporary, "p", "i", "\\"),
        "addon suffixes remain distinct instead of being filename-sanitized together");
    Check(AssetFiles.FurnitureName("a__b", "c") != AssetFiles.FurnitureName("a", "b__c"),
        "generated furniture identifiers cannot collide across pack/item boundaries");
    Check(AssetFiles.FurnitureName("PACK", "CHAIR") == AssetFiles.FurnitureName("pack", "chair"),
        "generated furniture names preserve case-insensitive identity");
    string copied = AssetFiles.CachePath(temporary, "pack", "copy", "");
    File.WriteAllText(Path.ChangeExtension(a, ".key"), "render-settings");
    AssetFiles.CopyCache(a, copied);
    Check(File.ReadAllBytes(copied).SequenceEqual(File.ReadAllBytes(a)) && File.ReadAllText(Path.ChangeExtension(copied, ".key")) == "render-settings",
        "cache-only duplication copies both artwork and its render key");
    refused = false; try { AssetFiles.CopyCache(b, copied); } catch (IOException) { refused = true; }
    Check(refused && File.ReadAllBytes(copied)[0] == 1, "duplicating artwork never overwrites a different cache");
    AssetFiles.WriteCache(copied, new byte[] { 9, 8 }, "updated-render");
    Check(File.ReadAllBytes(copied).SequenceEqual(new byte[] { 9, 8 }) && File.ReadAllText(Path.ChangeExtension(copied, ".key")) == "updated-render",
        "render cache replaces complete images and matching keys atomically per file");

    string packFile = PackFileStore.NewPackPath(temporary, "test.pack");
    PackFileStore.Save(packFile, "original", null);
    refused = false; try { PackFileStore.Save(packFile, "accidental new pack", null); } catch (IOException) { refused = true; }
    Check(refused && File.ReadAllText(packFile) == "original", "new-pack save cannot replace an existing pack");
    PackFileStore.Save(packFile, "updated", "original");
    Check(File.ReadAllText(packFile) == "updated" && File.ReadAllText(packFile + ".bak") == "original", "pack saves replace atomically and retain the previous version");
    refused = false; try { PackFileStore.Save(packFile, "stale edit", "original"); } catch (IOException) { refused = true; }
    Check(refused && File.ReadAllText(packFile) == "updated", "stale editor saves preserve external pack edits");
    PackFileStore.Save(packFile, "third", "updated");
    Check(File.ReadAllText(packFile + ".bak") == "updated", "repeated saves update the recovery backup");
    foreach (string unsafeId in new[] { ".", "..", "../outside", "bad.", ".. " })
    {
        refused = false; try { PackFileStore.NewPackPath(temporary, unsafeId); } catch (InvalidDataException) { refused = true; }
        Check(refused, "new pack path rejects unsafe ID: " + unsafeId);
    }
    Check(!Directory.GetFiles(temporary, "*.tmp", SearchOption.AllDirectories).Any(), "successful and failed atomic writes clean their temporary files");
}
finally { Directory.Delete(temporary, recursive: true); } // Only the fresh GUID test directory.

var offsets = new PlacementOffsets();
offsets.Set(0, .25f, .5f); offsets.Set(2, -.3f, -.4f);
Check(offsets.Get(0).x == .25f && offsets.Get(2).x == -.3f && offsets.Get(1).x == 0,
    "Front and Back have independent offsets; missing directions default to zero");
offsets.Set(0, 0, 0);
Check(offsets.Get(2).x == -.3f, "resetting Front preserves Back");
var copiedOffsets = Newtonsoft.Json.JsonConvert.DeserializeObject<PlacementOffsets>(
    Newtonsoft.Json.JsonConvert.SerializeObject(offsets))!;
copiedOffsets.Set(2, .9f, 0);
Check(offsets.Get(2).x == -.3f && copiedOffsets.Get(2).x == .9f,
    "JSON round trip and duplicate keep directional offsets independent");
bool invalidOffset = false;
try { offsets.Set(1, float.NaN, 0); } catch (InvalidDataException) { invalidOffset = true; }
Check(invalidOffset && offsets.Get(1).x == 0, "invalid offsets are rejected before mutating item data");
invalidOffset = false;
try { offsets.Set(1, 101, 0); } catch (InvalidDataException) { invalidOffset = true; }
Check(invalidOffset, "offset bounds reject excessive world displacement");
float shiftedPivot = PlacementOffset.ShiftPivot(16, 32, 32, .5f, .25f);
Check(shiftedPivot == 0 && (16f / 32 - shiftedPivot) * 32 / (32 / .5f) == .25f,
    "sprite pivot produces the requested world-space offset independently of scale");

string artTest = Path.Combine(Path.GetTempPath(), "cil-art-regression-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(artTest);
try
{
    SharedArtLibrary.Root = Path.Combine(artTest, "shared", "ArtLibrary");
    byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aE3sAAAAASUVORK5CYII=");
    var entry = SharedArtLibrary.Publish("Skeleton Holding Sword", "skeleton.glb", new[] { png, png, png, png });
    var repeated = SharedArtLibrary.Publish("Different item name", "another.glb", new[] { png, png, png, png });
    Check(entry.id == repeated.id && repeated.originalName == "Skeleton Holding Sword",
        "shared art is content-addressed across items and preserves its original name");
    Check(Directory.GetFiles(Path.Combine(SharedArtLibrary.Root, "images")).Length == 1,
        "identical directional images share a single immutable blob");
    var single = SharedArtLibrary.Publish("Single PNG", "single.png", new[] { png });
    Check(single.id != entry.id && SharedArtLibrary.ReadEntries().Count() == 2,
        "one-image art and four-direction art remain distinct reusable entries");
    string pack = Path.Combine(artTest, "pack");
    string[] paths = SharedArtLibrary.CopyToPack(entry, pack);
    Check(paths.Length == 4 && paths.All(p => File.Exists(Path.Combine(pack, p))) && !Directory.GetFiles(pack, "*.glb", SearchOption.AllDirectories).Any(),
        "cached art copies into distributable packs without a GLB");
    string[] secondCopy = SharedArtLibrary.CopyToPack(entry, pack);
    Check(secondCopy.SequenceEqual(paths), "reusing cached art does not duplicate or overwrite existing assets");
    bool escaped = false;
    try { SharedArtLibrary.Resolve("../../outside.png"); } catch (InvalidDataException) { escaped = true; }
    Check(escaped, "shared art rejects paths outside its universal folder");
    File.WriteAllText(Path.Combine(SharedArtLibrary.Root, "entries", "bad.json"), "{ broken");
    int badWarnings = 0;
    Check(SharedArtLibrary.ReadEntries(_ => badWarnings++).Count() == 2 && badWarnings == 1,
        "a corrupt library entry is skipped without hiding valid entries");
    string glb = Path.Combine(artTest, "test.glb");
    byte[] json = System.Text.Encoding.UTF8.GetBytes("{\"nodes\":[{\"name\":\"Scene\"},{\"name\":\"Original mesh object\"}]}");
    using (var writer = new BinaryWriter(File.Create(glb)))
    { writer.Write(0x46546C67u); writer.Write(2u); writer.Write((uint)(20 + json.Length)); writer.Write((uint)json.Length); writer.Write(0x4E4F534Au); writer.Write(json); }
    Check(SharedArtLibrary.OriginalModelName(glb) == "Original mesh object", "library extracts the original GLB object name");
}
finally { Directory.Delete(artTest, recursive: true); } // Only this fresh GUID test directory.

var instructions = new[] { new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(VersionSafeStorage), "Serialize")) };
var patched = ArmorPersistence.ReplaceStorageCall(instructions, "Serialize", "Write").ToArray();
Check(patched.Length == 2 && patched[0].opcode == OpCodes.Ldarg_0 &&
    ((MethodInfo)patched[1].operand!).Name == "Write", "armor serialization transpiler passes the target instance to the storage wrapper");

object document = new(), selected = new();
var previewIdentity = new PreviewIdentity(document, selected, "pack", "items.json", "settings-one", 1);
Check(previewIdentity.Matches(document, selected, "pack", "items.json", "settings-one", 1), "an unchanged preview can publish");
Check(!previewIdentity.Matches(document, new object(), "pack", "items.json", "settings-one", 1), "switching items rejects a pending preview");
Check(!previewIdentity.Matches(new object(), selected, "pack", "items.json", "settings-one", 1), "switching documents rejects a pending preview");
Check(!previewIdentity.Matches(document, selected, "pack", "items.json", "settings-two", 1), "editing an item or render settings rejects a pending preview");
Check(!previewIdentity.Matches(document, selected, "renamed-pack", "items.json", "settings-one", 1), "changing pack identity rejects a pending preview");
Check(!previewIdentity.Matches(document, selected, "pack", "moved.json", "settings-one", 1), "changing pack destination rejects a pending preview");
Check(!previewIdentity.Matches(document, selected, "pack", "items.json", "settings-one", 2), "clear or close invalidates a pending preview generation");

if (args.Length == 1)
{
    using var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(args[0], "Silverpine_Data", "Managed", "Assembly-CSharp.dll"));
    var damage = assembly.MainModule.Types.Single(t => t.Name == "Damagable");
    foreach (string method in new[] { "Serialize", "Deserialize" })
        Check(damage.Methods.Single(m => m.Name == method).Body.Instructions.Count(i =>
            i.Operand is MethodReference reference && reference.DeclaringType.Name == "VersionSafeStorage" && reference.Name == method) == 1,
            "installed game has the exact Damagable." + method + " storage hook expected by the transpiler");
    foreach (var target in new[] { ("Item", "OnEquipped"), ("Item", "OnUnequipped"), ("StatusEffect_Resist", "OnExpire"),
        ("Racial_ThickSkin", "OnExpire"), ("ItemComponent_Clothing", "GetArmorQualityMultiplied"), ("ItemComponent_RangedWeapon", "OnUse"),
        ("ItemComponent_Enchantment", "GetMultiplier"), ("Market", "SellStock"), ("WorldItem", "GetSprite") })
        Check(assembly.MainModule.Types.Single(t => t.Name == target.Item1).Methods.Any(m => m.Name == target.Item2),
            "installed game patch target exists: " + target.Item1 + "." + target.Item2);

    using var pluginAssembly = AssemblyDefinition.ReadAssembly(Path.Combine(args[0], "Plugin Development", "CustomItemLoader", "bin", "Release", "CustomItemLoader.dll"));
    var editor = pluginAssembly.MainModule.Types.Single(t => t.Name == "CustomItemEditorUI");
    bool Calls(MethodDefinition method, string target) => method.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == target);
    var actionButton = editor.Methods.Single(m => m.Name == "DrawActionButton");
    Check(Calls(actionButton, "GetActionButtonWidth") && Calls(editor.Methods.Single(m => m.Name == "GetActionButtonWidth"), "CalcSize") &&
        Calls(actionButton, "Width") && Calls(actionButton, "MinHeight"),
        "short editor actions measure their text and reserve sufficient control height");
    var wrappedInput = editor.Methods.Single(m => m.Name == "DrawWrappedTextInput");
    Check(Calls(wrappedInput, "CalcHeight") && Calls(wrappedInput, "TextInputWidth") && Calls(wrappedInput, "TextInputHeight") &&
        Calls(wrappedInput, "TextMeasurement") && Calls(wrappedInput, "Width") && Calls(wrappedInput, "Height") && Calls(wrappedInput, "TextArea"),
        "wrapped text inputs measure their bounded width and assign an explicit growing height");
    Check(Calls(wrappedInput, "set_alignment") && Calls(wrappedInput, "set_contentOffset") &&
        Calls(wrappedInput, "set_fixedHeight") && Calls(wrappedInput, "set_stretchHeight"),
        "multiline inputs clear native one-line sizing and align text at the top");
    Check(new[] { "DrawAssetPath", "DrawCloneChoice" }.All(name =>
        Calls(editor.Methods.Single(m => m.Name == name), "GetActionButtonReservation")) &&
        Calls(editor.Methods.Single(m => m.Name == "GetActionButtonReservation"), "GetActionButtonWidth"),
        "inline text fields reserve the same measured widths used by their adjacent actions");
    var wrappedValue = editor.Methods.Single(m => m.Name == "DrawWrappedValue");
    Check(Calls(editor.Methods.Single(m => m.Name == "DrawCloneChoice"), "DrawWrappedValue") &&
        Calls(wrappedValue, "CalcHeight") && Calls(wrappedValue, "Height"),
        "long selected clone names also expand their value boxes");
    Check(new[] { "DrawInt", "DrawFloatInline" }.All(name =>
        Calls(editor.Methods.Single(m => m.Name == name), "TextField") &&
        !Calls(editor.Methods.Single(m => m.Name == name), "DrawWrappedTextInput")),
        "numeric fields remain single-line controls rather than multiline text areas");
    var fieldLabel = editor.Methods.Single(m => m.Name == "DrawFieldLabel");
    Check(Calls(fieldLabel, "MinWidth") && !Calls(fieldLabel, "Width"),
        "field labels wrap above controls without reserving a competing horizontal column");
    Check(new[] { "DrawText", "DrawInt", "DrawFloat", "DrawBool", "DrawChoice", "DrawAssetPath", "DrawCloneChoice", "DrawEquipmentSlotChoice" }
        .All(name => Calls(editor.Methods.Single(m => m.Name == name), "BeginField") && Calls(editor.Methods.Single(m => m.Name == name), "EndField")),
        "all primary form controls share the stacked label and spaced field layout");
    var columnPair = editor.Methods.Single(m => m.Name == "DrawColumnPair");
    Check(Calls(columnPair, "UseColumns") && Calls(columnPair, "ColumnWidth") && Calls(columnPair, "Width"),
        "rendered column groups use the tested width budget and stacking threshold");
    var choiceValue = editor.Methods.Single(m => m.Name == "DrawChoiceValue");
    Check(Calls(choiceValue, "CalcSize") && Calls(choiceValue, "CalcHeight") && Calls(choiceValue, "Height") &&
        !Calls(choiceValue, "get_skin"), "selector text uses its drawing style for both width and explicit wrapped height");
    Check(new[] { "DrawChoice", "DrawEquipmentSlotChoice" }.All(name =>
        Calls(editor.Methods.Single(m => m.Name == name), "DrawChoiceValue") &&
        !Calls(editor.Methods.Single(m => m.Name == name), "CalcSize")),
        "ordinary and equipment selectors share one measurement/rendering path");
    var previewPanel = editor.Methods.Single(m => m.Name == "DrawPersistentPreview");
    var previewCalls = previewPanel.Body.Instructions.Where(i => i.Operand is MethodReference).Select(i => ((MethodReference)i.Operand).Name).ToList();
    Check(previewCalls.LastIndexOf("Button") > previewCalls.LastIndexOf("EndScrollView"),
        "Refresh preview is outside the scrolling preview content");
    Check(!Calls(editor.Methods.Single(m => m.Name == "SaveCurrent"), "RefreshDocuments"), "saving one pack cannot reload and discard other open documents");
    Check(Calls(editor.Methods.Single(m => m.Name == "Open"), "set_EscapeHandler") &&
        !Calls(editor.Methods.Single(m => m.Name == "Update"), "GetKeyDown"), "editor Escape is exclusively owned by its ModTools session");
    Check(Calls(pluginAssembly.MainModule.Types.Single(t => t.Name == "GeneratedFurnitureFactory").Methods.Single(m => m.Name == "Attach"), "RegisterAlias"),
        "collision-safe furniture registration retains legacy save aliases");
    var light = pluginAssembly.MainModule.Types.Single(t => t.Name == "CustomItemAlwaysOnLightAdapter");
    Check(Calls(light.Methods.Single(m => m.Name == "Start"), "Apply"), "light settings reconcile after native deserialization instead of only in Awake");
}
EquippedItemState.Dispose();
Console.WriteLine($"{passed} regression checks passed. Unity rendering and live Harmony dispatch require in-game testing.");
