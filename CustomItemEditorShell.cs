#nullable enable
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SilverpineMods.CustomItemLoader;

internal sealed partial class CustomItemEditorUI
{
    private int editorTab;
    private int previewTab = 1;
    private bool packPickerOpen;
    private bool packSettingsOpen;
    private bool assetLibraryOpen;
    private bool indexingArt;
    private bool removeConfirmation;
    private string librarySearch = "";
    private int libraryPage;
    private Vector2 libraryScroll, previewScroll;
    private GUISkin? editorSkin;
    private GUIStyle? editorHeading, editorMuted, editorSelected, editorAction, editorFieldLabel, editorFoldout, editorValue;
    private const float SidebarWidth = EditorFormLayout.SidebarWidth;
    private const float PreviewPanelWidth = EditorFormLayout.PreviewWidth;
    private float formWidth = EditorFormLayout.ContentWidth;
    private readonly List<Texture2D> skinTextures = new();
    private readonly List<SharedArtLibrary.Entry> libraryEntries = new();
    private readonly Dictionary<string, Sprite> libraryThumbnails = new();
    private readonly HashSet<string> failedLibraryThumbnails = new();
    private ItemDefinition? draggedItem;
    private int draggedDirection, offsetControl;
    private Vector2 dragMouse, dragStart;
    private float dragTileSize;
    private Texture2D? cursorDot;

    private void EnsureEditorSkin()
    {
        if (editorSkin != null) return;
        editorSkin = Instantiate(GUI.skin);
        Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply();
            skinTextures.Add(texture); return texture;
        }
        Color text = new(.90f, .92f, .90f);
        Texture2D panel = Solid(new Color(.098f, .129f, .145f));
        Texture2D input = Solid(new Color(.063f, .086f, .098f));
        Texture2D button = Solid(new Color(.153f, .196f, .22f));
        Texture2D selected = Solid(new Color(.188f, .251f, .224f));
        Texture2D hover = Solid(new Color(.204f, .259f, .286f));
        foreach (GUIStyle style in new[] { editorSkin.label, editorSkin.button, editorSkin.box,
                     editorSkin.textArea, editorSkin.textField, editorSkin.toggle })
        {
            style.fontSize = 20; style.wordWrap = true;
            style.normal.textColor = text; style.hover.textColor = text;
            style.active.textColor = text; style.focused.textColor = text;
            style.onNormal.textColor = text; style.onHover.textColor = text;
            style.onActive.textColor = text; style.onFocused.textColor = text;
        }
        editorSkin.box.normal.background = panel;
        editorSkin.box.border = new RectOffset();
        editorSkin.box.margin = new RectOffset();
        editorSkin.box.padding = new RectOffset(18, 18, 18, 18);
        editorSkin.label.padding = new RectOffset(2, 2, 6, 6);
        editorSkin.button.normal.background = button;
        editorSkin.button.hover.background = hover;
        editorSkin.button.active.background = selected;
        editorSkin.button.focused.background = hover;
        editorSkin.button.onNormal.background = selected;
        editorSkin.button.onHover.background = selected;
        editorSkin.button.onActive.background = selected;
        editorSkin.button.onFocused.background = selected;
        editorSkin.button.padding = new RectOffset(12, 12, 10, 10);
        editorSkin.button.border = new RectOffset();
        foreach (GUIStyle style in new[] { editorSkin.textArea, editorSkin.textField })
        {
            style.normal.background = input; style.focused.background = input;
            style.hover.background = input; style.active.background = input;
            style.padding = new RectOffset(10, 10, 9, 9);
            style.border = new RectOffset();
        }
        editorSkin.textField.wordWrap = false;
        editorAction = new GUIStyle(editorSkin.button) { wordWrap = false, fixedHeight = 0f };
        editorFieldLabel = new GUIStyle(editorSkin.label)
        {
            wordWrap = true, alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(0, 0, 2, 6), margin = new RectOffset()
        };
        editorFoldout = new GUIStyle(editorSkin.button)
        {
            alignment = TextAnchor.MiddleLeft, wordWrap = true, fontSize = 21,
            padding = new RectOffset(12, 12, 10, 10), margin = new RectOffset(0, 0, 12, 10)
        };
        editorValue = new GUIStyle(editorSkin.textArea)
        {
            alignment = TextAnchor.MiddleLeft, wordWrap = true,
            // The native skin can impose a one-line text-area height. Read-only
            // values must grow when wrapped rather than clip their second line.
            fixedWidth = 0f, fixedHeight = 0f, stretchHeight = false,
            contentOffset = Vector2.zero
        };
        editorHeading = new GUIStyle(editorSkin.label) { fontSize = 27 };
        editorMuted = new GUIStyle(editorSkin.label) { fontSize = 18 }; editorMuted.normal.textColor = new Color(.65f, .69f, .68f);
        editorSelected = new GUIStyle(editorSkin.button); editorSelected.normal.background = selected;
        wrappedTextAreaStyle = null;
        cursorDot = new Texture2D(16, 16, TextureFormat.ARGB32, false);
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            cursorDot.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f)) < 7f
                ? new Color(1f, .34f, .34f) : Color.clear);
        cursorDot.Apply();
    }

    private bool DrawActionButton(string text, float minimumWidth = 0f) =>
        GUILayout.Button(text, editorAction!,
            GUILayout.Width(GetActionButtonWidth(text, minimumWidth)),
            GUILayout.MinHeight(44f));

    private float GetActionButtonWidth(string text, float minimumWidth = 0f) =>
        Mathf.Max(minimumWidth, editorAction!.CalcSize(new GUIContent(text)).x + 12f);

    private float GetActionButtonReservation(string text, float minimumWidth = 0f) =>
        GetActionButtonWidth(text, minimumWidth) + editorAction!.margin.horizontal;

    private void DrawFieldLabel(string text) =>
        GUILayout.Label(text, editorFieldLabel!, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));

    private void BeginField(string label)
    {
        GUILayout.BeginVertical(GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
        DrawFieldLabel(label);
    }

    private static void EndField()
    {
        GUILayout.Space(EditorFormLayout.FieldGap);
        GUILayout.EndVertical();
    }

    private void DrawHelp(string text)
    {
        GUILayout.Label(text, editorMuted!, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
        GUILayout.Space(8);
    }

    private void DrawLibrarySidebar()
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(SidebarWidth));
        GUILayout.Label("CURRENT PACK", editorMuted!);
        if (GUILayout.Button((document?.Pack.packId ?? "Choose item pack") + "  …")) packPickerOpen = !packPickerOpen;
        if (packPickerOpen)
        {
            packScroll = GUILayout.BeginScrollView(packScroll, GUILayout.MaxHeight(260));
            foreach (PackDocument candidate in documents.ToArray())
                if (GUILayout.Button((candidate.Pack.packId ?? Path.GetFileName(candidate.Path)) +
                        (candidate.Pack.enabled ? "" : " (disabled)")))
                { SelectDocument(candidate); packPickerOpen = false; assetLibraryOpen = false; }
            GUILayout.EndScrollView();
            if (GUILayout.Button("New pack")) { CreateNewPack(); packPickerOpen = false; }
        }
        if (document != null)
        {
            bool enabled = GUILayout.Toggle(document.Pack.enabled, "Enabled at startup");
            if (enabled != document.Pack.enabled) document.Pack.enabled = enabled;
            if (DrawFoldout("Pack settings", ref packSettingsOpen))
            {
                GUILayout.Label("Pack ID");
                document.Pack.packId = DrawWrappedTextInput(document.Pack.packId ?? "",
                    availableWidth: SidebarWidth - 2 * EditorFormLayout.PanelInset);
                GUILayout.Label("Save renames this pack's dedicated folder to match. Keep released IDs stable: they identify items in saves.", editorMuted!);
            }
        }
        GUILayout.Space(14);
        GUILayout.Label("ITEMS  " + (document?.Pack.items?.Count ?? 0), editorMuted!);
        itemScroll = GUILayout.BeginScrollView(itemScroll);
        if (document?.Pack.items != null)
            for (int i = 0; i < document.Pack.items.Count; i++)
            {
                ItemDefinition? value = document.Pack.items[i];
                string title = value?.name ?? value?.id ?? "Invalid item";
                string subtitle = value?.clone ?? value?.category ?? "Item";
                if (GUILayout.Button(title + "\n" + subtitle, i == selectedItem ? editorSelected! : GUI.skin.button))
                { SelectItem(i); assetLibraryOpen = false; removeConfirmation = false; }
            }
        GUILayout.EndScrollView();
        GUI.enabled = document != null;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add")) AddItem();
        GUI.enabled = CurrentItem != null;
        if (GUILayout.Button("Duplicate")) DuplicateItem();
        GUILayout.EndHorizontal();
        GUI.enabled = CurrentItem != null && document!.Pack.items!.Count > 1;
        if (GUILayout.Button("Remove selected")) removeConfirmation = true;
        GUI.enabled = true;
        if (removeConfirmation)
        {
            GUILayout.Label("Remove this item from the pack?", editorMuted!);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Remove") && CurrentItem != null)
            {
                document!.Pack.items!.RemoveAt(selectedItem);
                selectedItem = Mathf.Clamp(selectedItem, 0, document.Pack.items.Count - 1);
                removeConfirmation = false; SelectionChanged();
            }
            if (GUILayout.Button("Cancel")) removeConfirmation = false;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private int ActiveOffsetDirection => CurrentItem?.furniture?.turnable == true &&
        IsGeneratedPrefabPlacement(CurrentItem) ? furniturePreviewRotation : 0;

    private void DrawPersistentPreview()
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(PreviewPanelWidth));
        float previousWidth = formWidth;
        formWidth = PreviewPanelWidth - 2 * EditorFormLayout.PanelInset - EditorFormLayout.ScrollbarReserve;
        previewScroll = GUILayout.BeginScrollView(previewScroll);
        GUILayout.Label("Preview", editorHeading!);
        int next = GUILayout.Toolbar(previewTab, new[] { "Inventory", "World" }, editorAction!, GUILayout.MinHeight(44));
        if (next != previewTab) { previewTab = next; CancelOffsetDrag(); }
        GUILayout.Space(10);
        DrawPreviewBox(previewTab == 0 ? preview : placementPreview, previewTab == 1);
        ItemDefinition? item = CurrentItem;
        if (previewTab == 1 && item != null)
        {
            if (IsGeneratedPrefabPlacement(item) && item.furniture?.turnable == true)
                DrawChoice("View", FurnitureRotationNames[furniturePreviewRotation], FurnitureRotationNames, value =>
                { CancelOffsetDrag(); furniturePreviewRotation = IndexOf(FurnitureRotationNames, value); RefreshPlacementPreview(); });
            if (!IsClonedPrefabPlacement(item))
            {
                int direction = ActiveOffsetDirection;
                PlacementOffset position = item.placementOffsets?.Get(direction) ?? new PlacementOffset();
                GUILayout.Label("●  Placement cursor", editorMuted!);
                GUILayout.Label("Drag this view. The red cursor and terrain stay fixed.", editorMuted!);
                GUILayout.Label($"{FurnitureRotationNames[direction]} offset  X {position.x:0.###} · Y {position.y:0.###} tiles");
                if (GUILayout.Button("Reset " + FurnitureRotationNames[direction] + " offset"))
                { CancelOffsetDrag(); SetOffset(item, direction, 0, 0); }
                GUILayout.Label("Arrow keys adjust by 0.01 tile; Shift by 0.1. Escape cancels a drag.", editorMuted!);
            }
            else GUILayout.Label("Cloned prefab keeps its native placement; offsets are not applied.", editorMuted!);
        }
        if (placementPreview != null && previewTab == 1)
            GUILayout.Label($"Terrain: 1 × 1 tile\nSprite: {placementPreview.rect.width / placementPreview.pixelsPerUnit:0.###} × " +
                $"{placementPreview.rect.height / placementPreview.pixelsPerUnit:0.###} tiles\nPlacement scale: {GetPlacementPreviewScale():0.###}×", editorMuted!);
        if (preview != null) GUILayout.Label($"Source: {preview.rect.width:0} × {preview.rect.height:0} px", editorMuted!);
        if (previewTab == 1 && (item == null || !IsClonedPrefabPlacement(item))) GUILayout.Label(placementPreviewNote, editorMuted!);
        GUILayout.EndScrollView();
        GUILayout.Space(12);
        if (GUILayout.Button("Refresh preview", editorAction!, GUILayout.MinHeight(EditorFormLayout.ControlHeight))) RefreshPreview();
        GUILayout.EndVertical();
        formWidth = previousWidth;
    }

    private void DrawWorldPlacement(Sprite sprite, Rect rect)
    {
        float ppu = Mathf.Max(.001f, sprite.pixelsPerUnit);
        float width = sprite.rect.width / ppu, height = sprite.rect.height / ppu;
        float tile = TerrainTileScreenSize * Mathf.Min(1f, rect.width * .86f / (Mathf.Max(1, width) * TerrainTileScreenSize),
            rect.height * .72f / (Mathf.Max(1, height) * TerrainTileScreenSize));
        Vector2 tileCenter = new(rect.center.x, rect.center.y + rect.height * .12f);
        var tileRect = new Rect(tileCenter.x - tile / 2, tileCenter.y - tile / 2, tile, tile);
        ItemDefinition? item = CurrentItem;
        Vector2 cursor = tileCenter;
        if (item != null && IsGeneratedPrefabPlacement(item) && item.furniture != null && item.furniture.snappingOffset != -1)
            cursor.y -= item.furniture.snappingOffset / 32f * tile;
        int direction = ActiveOffsetDirection;
        PlacementOffset position = item?.placementOffsets?.Get(direction) ?? new PlacementOffset();
        if (item != null && IsClonedPrefabPlacement(item)) position = new PlacementOffset();
        Rect art = new(cursor.x - sprite.pivot.x / ppu * tile + position.x * tile,
            cursor.y - (sprite.rect.height - sprite.pivot.y) / ppu * tile - position.y * tile,
            width * tile, height * tile);
        // Clip displaced art to the preview; the reference scale never changes while dragging.
        GUI.BeginGroup(rect);
        DrawGrassTerrainTile(new Rect(tileRect.x - rect.x, tileRect.y - rect.y, tileRect.width, tileRect.height));
        DrawSpriteTexture(sprite, new Rect(art.x - rect.x, art.y - rect.y, art.width, art.height));
        GUI.DrawTexture(new Rect(cursor.x - rect.x - 6, cursor.y - rect.y - 6, 12, 12), cursorDot!);
        GUI.EndGroup();
        int control = GUIUtility.GetControlID("cil-placement-drag".GetHashCode(), FocusType.Keyboard, rect);
        Event e = Event.current;
        if (item == null || IsClonedPrefabPlacement(item)) return;
        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition) && art.Contains(e.mousePosition))
        {
            draggedItem = item; draggedDirection = direction; dragMouse = e.mousePosition;
            dragStart = new Vector2(position.x, position.y); dragTileSize = tile;
            offsetControl = control; GUIUtility.hotControl = control; GUIUtility.keyboardControl = control; e.Use();
        }
        if (draggedItem != null && GUIUtility.hotControl == control)
        {
            if (e.type == EventType.MouseDrag)
            {
                Vector2 delta = (e.mousePosition - dragMouse) / dragTileSize;
                SetOffset(draggedItem, draggedDirection, dragStart.x + delta.x, dragStart.y - delta.y); e.Use();
            }
            if (e.type == EventType.MouseUp && e.button == 0) { draggedItem = null; GUIUtility.hotControl = 0; e.Use(); }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { CancelOffsetDrag(); e.Use(); }
        }
        if (GUIUtility.keyboardControl == control && e.type == EventType.KeyDown)
        {
            float step = e.shift ? .1f : .01f;
            Vector2 delta = e.keyCode switch { KeyCode.LeftArrow => Vector2.left, KeyCode.RightArrow => Vector2.right,
                KeyCode.UpArrow => Vector2.up, KeyCode.DownArrow => Vector2.down, _ => Vector2.zero };
            if (delta != Vector2.zero) { SetOffset(item, direction, position.x + delta.x * step, position.y + delta.y * step); e.Use(); }
        }
    }

    private void SetOffset(ItemDefinition item, int direction, float x, float y)
    {
        item.placementOffsets ??= new PlacementOffsets();
        item.placementOffsets.Set(direction, (float)Math.Round(Mathf.Clamp(x, -100, 100), 3),
            (float)Math.Round(Mathf.Clamp(y, -100, 100), 3));
        status = FurnitureRotationNames[direction] + " offset updated. Save pack and restart to apply in game.";
    }
    private void CancelOffsetDrag()
    {
        if (draggedItem == null) return;
        SetOffset(draggedItem, draggedDirection, dragStart.x, dragStart.y);
        draggedItem = null;
        if (GUIUtility.hotControl == offsetControl) GUIUtility.hotControl = 0;
    }

    private void OpenArtLibrary()
    {
        CancelOffsetDrag(); assetLibraryOpen = true; libraryPage = 0;
        ReloadArtLibrary();
        if (libraryEntries.Count == 0) IndexExistingArt();
    }
    private void ReloadArtLibrary()
    {
        ClearLibraryThumbnails(); libraryEntries.Clear();
        try { libraryEntries.AddRange(SharedArtLibrary.ReadEntries(message => Plugin.Log.LogWarning("Art library: " + message))); }
        catch (Exception error) { status = "Cannot read shared library: " + error.Message; }
    }
    private void DrawArtLibrary()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal(); GUILayout.Label("Shared asset library", editorHeading!);
        if (DrawActionButton("Back to editor", 190)) assetLibraryOpen = false;
        GUILayout.EndHorizontal();
        GUILayout.Label("BepInEx/shared/ArtLibrary · reusable cached views · original object names", editorMuted!);
        string search = DrawWrappedTextInput(librarySearch,
            availableWidth: DesignWidth - 2 * EditorFormLayout.OuterInset - SidebarWidth -
                EditorFormLayout.PanelGap - 2 * EditorFormLayout.PanelInset);
        if (search != librarySearch) { librarySearch = search; libraryPage = 0; ClearLibraryThumbnails(); }
        GUILayout.BeginHorizontal();
        GUI.enabled = !indexingArt;
        if (GUILayout.Button(indexingArt ? "Indexing pack art…" : "Index existing pack art")) IndexExistingArt();
        GUI.enabled = preview != null;
        if (GUILayout.Button("Add current art")) PublishCurrentArt();
        GUI.enabled = true;
        if (GUILayout.Button("Refresh library")) ReloadArtLibrary();
        GUILayout.EndHorizontal();
        var visible = libraryEntries.Where(entry => (entry.originalName + " " + entry.sourceFileName)
            .IndexOf(librarySearch, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        int pages = Math.Max(1, (visible.Length + 7) / 8); libraryPage = Mathf.Clamp(libraryPage, 0, pages - 1);
        libraryScroll = GUILayout.BeginScrollView(libraryScroll);
        if (visible.Length == 0) GUILayout.Label("No cached art yet. Index existing packs, or render/import art and add it here.");
        for (int row = libraryPage * 8; row < Math.Min(visible.Length, libraryPage * 8 + 8); row += 2)
        {
            GUILayout.BeginHorizontal();
            for (int column = row; column < Math.Min(visible.Length, row + 2); column++) DrawArtCard(visible[column]);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.BeginHorizontal();
        if (DrawActionButton("◀", 40)) { libraryPage = (libraryPage - 1 + pages) % pages; ClearLibraryThumbnails(); }
        GUILayout.Label($"Page {libraryPage + 1} / {pages} · {visible.Length} assets", GUILayout.Width(290));
        if (DrawActionButton("▶", 40)) { libraryPage = (libraryPage + 1) % pages; ClearLibraryThumbnails(); }
        GUILayout.EndHorizontal(); GUILayout.EndVertical();
    }
    private void DrawArtCard(SharedArtLibrary.Entry entry)
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width((DesignWidth - 24 - 290 - 80) / 2));
        GUILayout.Label(entry.originalName, editorHeading!);
        GUILayout.Label(entry.sourceFileName + " · " + entry.views.Length + " view(s)", editorMuted!);
        Rect rect = GUILayoutUtility.GetRect(180, 180, GUILayout.ExpandWidth(true));
        try
        {
            if (failedLibraryThumbnails.Contains(entry.id)) throw new InvalidDataException("Refresh the library after repairing this image.");
            if (!libraryThumbnails.TryGetValue(entry.id, out Sprite thumbnail))
            {
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                try
                {
                    if (!texture.LoadImage(File.ReadAllBytes(SharedArtLibrary.Resolve(entry.views[0])), false))
                        throw new InvalidDataException("Image cannot be decoded.");
                    texture.filterMode = FilterMode.Point;
                    thumbnail = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 32);
                    libraryThumbnails.Add(entry.id, thumbnail);
                }
                catch { Destroy(texture); throw; }
            }
            DrawSpriteTexture(thumbnail, FitRect(rect, thumbnail.rect.width, thumbnail.rect.height));
        }
        catch (Exception error) { failedLibraryThumbnails.Add(entry.id); GUI.Label(rect, "Preview unavailable: " + error.Message); }
        GUI.enabled = CurrentItem != null && !failedLibraryThumbnails.Contains(entry.id);
        if (GUILayout.Button("Use cached art")) { UseLibraryEntry(entry); GUIUtility.ExitGUI(); }
        GUI.enabled = true; GUILayout.EndVertical();
    }
    private void UseLibraryEntry(SharedArtLibrary.Entry entry)
    {
        ItemDefinition? item = CurrentItem; if (item == null) return;
        try
        {
            string[] paths = SharedArtLibrary.CopyToPack(entry, GetPackFolder());
            item.image = paths[0]; item.model = null; item.useCloneVisuals = false;
            item.artAssetId = entry.id; item.artOriginalName = entry.originalName;
            item.cachedArtRotations = paths.Length == 4
                ? new FurnitureRotationSpritesDefinition { right = paths[1], back = paths[2], left = paths[3] } : null;
            if (item.furniture != null)
            {
                item.furniture.glbRotations = null;
                item.furniture.rotationSprites = !item.furniture.turnable || item.cachedArtRotations == null ? null :
                    JsonConvert.DeserializeObject<FurnitureRotationSpritesDefinition>(JsonConvert.SerializeObject(item.cachedArtRotations));
            }
            assetLibraryOpen = false; editorTab = 1; RefreshPreview();
            status = "Cached art copied into this pack. Save to keep it; no source GLB is required.";
        }
        catch (Exception error) { status = "Cannot use cached art: " + error.Message; }
    }
    private void PublishCurrentArt()
    {
        if (CurrentItem == null || preview == null) return;
        try
        {
            PublishItemArt(CurrentItem, GetPackFolder(), document?.Pack.packId ?? "");
            ReloadArtLibrary(); status = "Art added to the shared library; pack files are unchanged.";
        }
        catch (Exception error) { status = "Cannot publish art: " + error.Message; }
    }
    private void PublishItemArt(ItemDefinition item, string folder, string packId)
    {
        if (item.useCloneVisuals) throw new InvalidDataException("Base-game visuals are inherited, not exported to the art library.");
        bool model = !string.IsNullOrWhiteSpace(item.model);
        string? modelPath = model ? ItemPackLoader.ResolveModelSourcePath(folder, item.model!, false) : null;
        string source = model ? item.model! : item.image ?? "";
        var pngs = new List<byte[]>();
        int count = item.furniture?.turnable == true ? 4 : 1;
        string main = model ? ItemPackLoader.FindReadableGlbCachePath(folder, packId, item.id ?? "") : ResolveAsset(folder, source);
        for (int i = 0; i < count; i++)
        {
            string? directional = item.furniture?.rotationSprites?.GetPath(i);
            string path = i == 0 ? main : model
                ? ItemPackLoader.FindReadableGlbCachePath(folder, packId, item.id ?? "", "__furniture_" + FurnitureRotationNames[i].ToLowerInvariant())
                : string.IsNullOrWhiteSpace(directional) ? main : ResolveAsset(folder, directional!);
            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path), false)) throw new InvalidDataException("Cannot decode " + path);
                pngs.Add(texture.EncodeToPNG());
            }
            finally { Destroy(texture); }
        }
        SharedArtLibrary.Publish(item.artOriginalName ?? (model ? SharedArtLibrary.OriginalModelName(modelPath) : Path.GetFileNameWithoutExtension(source)),
            Path.GetFileName(source), pngs, modelPath != null && File.Exists(modelPath) ? SharedArtLibrary.FileHash(modelPath) : null);
    }
    private void IndexExistingArt()
    {
        if (indexingArt) return;
        indexingArt = true;
        StartCoroutine(IndexArt(documents.ToArray()));
    }

    private IEnumerator IndexArt(PackDocument[] packs)
    {
        // StartCoroutine runs synchronously until its first yield. Leave OnGUI
        // before doing IO, and release temporary textures between items.
        yield return null;
        int count = 0, skipped = 0;
        foreach (PackDocument pack in packs)
            foreach (ItemDefinition? item in (pack.Pack.items ?? new List<ItemDefinition?>()).ToArray())
            {
                if (item == null || item.useCloneVisuals) continue;
                try
                {
                    string folder = pack.Workspace.GetFolder(Plugin.DefinitionDirectory, pack.Pack.packId ?? "");
                    PublishItemArt(item, folder, pack.Pack.packId ?? ""); count++;
                }
                catch (Exception error) { skipped++; Plugin.Log.LogWarning("Art index skipped '" + item.id + "': " + error.Message); }
                status = $"Indexing art: {count} indexed; {skipped} skipped.";
                yield return null;
            }
        indexingArt = false;
        ReloadArtLibrary(); status = $"Indexed {count} item(s); {skipped} missing or unsupported sources skipped. Pack files are unchanged.";
    }
    private void ClearLibraryThumbnails()
    {
        foreach (Sprite sprite in libraryThumbnails.Values) { if (sprite != null) { Destroy(sprite.texture); Destroy(sprite); } }
        libraryThumbnails.Clear();
        failedLibraryThumbnails.Clear();
    }
    protected override void OnDestroy()
    {
        ClearPreview(); ClearLibraryThumbnails();
        foreach (Texture2D texture in skinTextures) if (texture != null) Destroy(texture);
        if (cursorDot != null) Destroy(cursorDot);
        if (editorSkin != null) Destroy(editorSkin);
        base.OnDestroy();
    }
}
