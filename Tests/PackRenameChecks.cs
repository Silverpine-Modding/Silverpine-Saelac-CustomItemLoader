using SilverpineMods.CustomItemLoader;

internal static class PackRenameChecks
{
    internal static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "cil-rename-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            bool Refuses(Action action)
            {
                try { action(); return false; }
                catch (IOException) { return true; }
                catch (InvalidDataException) { return true; }
            }
            var draft = new PackWorkspace();
            string draftFolder = draft.GetFolder(root, "my.item.pack");
            Directory.CreateDirectory(Path.Combine(draftFolder, "assets"));
            File.WriteAllText(Path.Combine(draftFolder, "assets", "image.png"), "original image bytes");
            string cache = AssetFiles.CachePath(draftFolder, "my.item.pack", "plant", "__addon_growth_3");
            AssetFiles.WriteCache(cache, new byte[] { 1, 2, 3 }, "render-key");
            check(draft.GetFolder(root, "herb.seeds.pack") == draftFolder,
                "changing a draft ID retains its original artwork folder");
            check(AssetFiles.CachePath(draftFolder, "herb.seeds.pack", "plant", "__addon_growth_3") == cache &&
                File.ReadAllText(Path.ChangeExtension(cache, ".key")) == "render-key",
                "renaming retains GLB-free artwork and keys for arbitrary addon variants");
            string path = draft.Save(root, "", "herb.seeds.pack", null,
                destination => PackFileStore.Save(destination, "first save", null));
            string finalFolder = Path.Combine(root, "herb.seeds.pack");
            check(path == Path.Combine(finalFolder, "items.json") && draft.Folder == finalFolder && !Directory.Exists(draftFolder),
                "first Save moves the pinned draft into its chosen pack folder");
            check(File.ReadAllText(Path.Combine(finalFolder, "assets", "image.png")) == "original image bytes" &&
                File.ReadAllBytes(AssetFiles.CachePath(finalFolder, "herb.seeds.pack", "plant", "__addon_growth_3")).SequenceEqual(new byte[] { 1, 2, 3 }),
                "renaming moves PNG assets and opaque caches together without changing their bytes");
            var loaded = new PackWorkspace(path, "herb.seeds.pack");
            loaded.GetFolder(root, "final.pack");
            string finalPath = loaded.Save(root, path, "final.pack", "first save",
                destination => PackFileStore.Save(destination, "second save", "first save"));
            check(File.ReadAllText(finalPath) == "second save" && File.ReadAllText(finalPath + ".bak") == "first save" && !File.Exists(path),
                "saved-pack renaming preserves atomic JSON backup behavior");
            string renamedCache = AssetFiles.CachePath(loaded.Folder, "final.pack", "plant", "__addon_growth_3");
            AssetFiles.WriteCache(renamedCache, new byte[] { 9, 8, 7 }, "new-render-key");
            loaded.GetFolder(root, "my.item.pack");
            check(AssetFiles.CachePath(loaded.Folder, "my.item.pack", "plant", "__addon_growth_3") == renamedCache &&
                File.ReadAllText(Path.ChangeExtension(renamedCache, ".key")) == "new-render-key",
                "renaming back to an earlier ID cannot resurrect stale cached art");
            check(AssetFiles.FurnitureName("my.item.pack", "plant") != AssetFiles.FurnitureName("final.pack", "plant"),
                "cache aliases do not alter furniture or save identities");

            string before = loaded.Folder;
            check(Refuses(() => loaded.Save(root, finalPath, "rollback.pack", "second save", _ => throw new IOException("simulated disk error"))) &&
                loaded.Folder == before && File.ReadAllText(finalPath) == "second save" && !Directory.Exists(Path.Combine(root, "rollback.pack")),
                "a failed save rolls the folder back and preserves the original JSON");
            File.WriteAllText(finalPath, "external editor change");
            check(Refuses(() => loaded.Save(root, finalPath, "stale.pack", "second save", _ => throw new Exception("must not write"))) &&
                File.ReadAllText(finalPath) == "external editor change" && !Directory.Exists(Path.Combine(root, "stale.pack")),
                "external JSON edits are rejected before moving any folder");
            Directory.CreateDirectory(Path.Combine(root, "taken.pack"));
            File.WriteAllText(Path.Combine(root, "taken.pack", "sentinel"), "untouched");
            check(Refuses(() => loaded.Destination(root, finalPath, "taken.pack", "external editor change")) &&
                File.ReadAllText(Path.Combine(root, "taken.pack", "sentinel")) == "untouched",
                "an occupied destination is never merged or overwritten");
            Directory.CreateDirectory(Path.Combine(root, "empty.pack"));
            check(Refuses(() => loaded.Destination(root, finalPath, "empty.pack", "external editor change")),
                "even an empty existing destination requires a different pack ID");
            File.WriteAllText(Path.Combine(root, "file.pack"), "not a directory");
            check(Refuses(() => loaded.Destination(root, finalPath, "file.pack", "external editor change")),
                "a file occupying the target folder name blocks renaming");
            File.WriteAllText(Path.Combine(before, "other.json"), "another pack");
            check(Refuses(() => loaded.Destination(root, finalPath, "shared.pack", "external editor change")),
                "folders containing another JSON document cannot be moved automatically");
            File.Delete(Path.Combine(before, "other.json"));
            Directory.CreateDirectory(Path.Combine(before, "nested"));
            File.WriteAllText(Path.Combine(before, "nested", "items.json"), "nested pack");
            check(Refuses(() => loaded.Destination(root, finalPath, "nested.pack", "external editor change")),
                "nested packs also prevent automatic folder renaming");
            File.Delete(Path.Combine(before, "nested", "items.json"));
            check(Refuses(() => loaded.Destination(Path.Combine(root, "restricted"), finalPath, "escape.pack", "external editor change")),
                "source folders outside the configured root cannot be moved");

            string casePath = loaded.Save(root, finalPath, "FINAL.PACK", "external editor change",
                destination => PackFileStore.Save(destination, "case save", "external editor change"));
            check(Path.GetFileName(loaded.Folder) == "FINAL.PACK" && File.ReadAllText(casePath) == "case save" &&
                !Directory.EnumerateDirectories(root, ".cil-rename-*").Any(),
                "case-only folder renames finish without leaving temporary folders");
            check(Refuses(() => loaded.Save(root, casePath, "final.pack", "case save", _ => throw new IOException("failed case-only save"))) &&
                Path.GetFileName(loaded.Folder) == "FINAL.PACK" && File.ReadAllText(casePath) == "case save" &&
                !Directory.EnumerateDirectories(root, ".cil-rename-*").Any(),
                "case-only renames also roll back safely when writing fails");

            string recoverySource = Path.Combine(root, "recovery.source");
            Directory.CreateDirectory(recoverySource);
            string recoveryPath = Path.Combine(recoverySource, "items.json");
            File.WriteAllText(recoveryPath, "recoverable pack");
            var recovery = new PackWorkspace(recoveryPath, "recovery.source");
            string recoveryError = "";
            try
            {
                recovery.Save(root, recoveryPath, "recovery.target", "recoverable pack", _ =>
                {
                    Directory.CreateDirectory(recoverySource);
                    File.WriteAllText(Path.Combine(recoverySource, "sentinel"), "external owner");
                    throw new IOException("simulated concurrent folder creation");
                });
            }
            catch (IOException error) { recoveryError = error.Message; }
            check(recovery.Folder == Path.Combine(root, "recovery.target") && recoveryError.Contains(recovery.Folder) &&
                File.ReadAllText(Path.Combine(recovery.Folder, "items.json")) == "recoverable pack" &&
                File.ReadAllText(Path.Combine(recoverySource, "sentinel")) == "external owner",
                "a blocked rollback reports the recoverable location without overwriting the new folder owner");

            string loosePath = Path.Combine(root, "loose.json");
            File.WriteAllText(loosePath, "loose pack");
            var loose = new PackWorkspace(loosePath, "loose.pack");
            string looseSaved = loose.Save(root, loosePath, "changed.loose.pack", "loose pack",
                destination => PackFileStore.Save(destination, "loose updated", "loose pack"));
            check(looseSaved == loosePath && Directory.Exists(root) && Directory.Exists(loaded.Folder),
                "loose root-level JSON saves never rename the shared pack root");
            string group = Path.Combine(root, "group", "old.name");
            Directory.CreateDirectory(group);
            string customJson = Path.Combine(group, "custom.json");
            File.WriteAllText(customJson, "grouped pack");
            var grouped = new PackWorkspace(customJson, "old.name");
            string groupedPath = grouped.Save(root, customJson, "new.name", "grouped pack",
                destination => PackFileStore.Save(destination, "grouped updated", "grouped pack"));
            check(groupedPath == Path.Combine(root, "group", "new.name", "custom.json"),
                "folder renaming preserves parent groups and non-default JSON filenames");

            string repairFolder = Path.Combine(root, "default.name");
            Directory.CreateDirectory(repairFolder);
            string repairPath = Path.Combine(repairFolder, "items.json");
            File.WriteAllText(repairPath, "stable identity");
            var repair = new PackWorkspace(repairPath, "stable.pack");
            string repaired = repair.Save(root, repairPath, "stable.pack", "stable identity",
                destination => PackFileStore.Save(destination, "stable identity", "stable identity"));
            check(Path.GetFileName(Path.GetDirectoryName(repaired)) == "stable.pack" && File.ReadAllText(repaired) == "stable identity",
                "an old default folder is corrected without changing the pack ID or JSON");

            check(new[] { "CON", "nul.pack", "COM1", "LPT9.foo", "COM¹", "a..", "../outside", "" }
                .All(id => Refuses(() => PackFileStore.NewPackPath(root, id))),
                "unsafe IDs and reserved Windows device names are rejected");
            string map = Path.Combine(loaded.Folder, ".cache", "pack-identities.map");
            File.WriteAllText(map, "{ broken map");
            check(Refuses(() => AssetFiles.CachePath(loaded.Folder, "final.pack", "plant", "")),
                "corrupt cache identity metadata fails explicitly instead of selecting unrelated art");
            File.WriteAllText(map, "{\"a\":\"b\",\"b\":\"a\"}");
            check(Refuses(() => PackCacheIdentity.Resolve(loaded.Folder, "a")),
                "cache identity cycles are rejected without hanging");
            File.WriteAllText(map, "{\"a\":\"original\",\"A\":\"different\"}");
            check(Refuses(() => PackCacheIdentity.Resolve(loaded.Folder, "a")),
                "case-insensitive duplicate cache identities are rejected");
        }
        finally { Directory.Delete(root, true); }
    }
}
