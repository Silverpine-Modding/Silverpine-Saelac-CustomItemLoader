#nullable enable
using System;
using System.IO;

namespace SilverpineMods.CustomItemLoader;

// A document owns one working folder until Save commits a rename. In particular,
// changing an unsaved pack ID must not change where imported artwork is found.
internal sealed class PackWorkspace
{
    internal string Folder { get; private set; } = "";
    private string? artIdentity;

    internal PackWorkspace(string path = "", string? packId = null)
    {
        if (!string.IsNullOrEmpty(path)) Folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        artIdentity = packId;
    }

    internal string GetFolder(string root, string packId)
    {
        PackFileStore.NewPackPath(root, packId); // Validate IDs before writing anything.
        if (Folder.Length == 0)
        {
            string draft = Path.Combine(Path.GetFullPath(root), ".cil-draft-" + Guid.NewGuid().ToString("N"));
            CheckLocation(root, draft);
            Directory.CreateDirectory(draft);
            Folder = draft;
        }
        CheckLocation(root, Folder);
        if (!Directory.Exists(Folder))
            throw new IOException("The pack folder was moved or removed outside the editor. Reload it before editing artwork.");
        artIdentity ??= packId;
        PackCacheIdentity.Bind(Folder, packId, artIdentity);
        return Folder;
    }

    internal string Destination(string root, string sourcePath, string packId, string? expectedText)
    {
        string fullRoot = Full(root);
        string sourceFolder = sourcePath.Length == 0 ? Folder : Path.GetDirectoryName(Path.GetFullPath(sourcePath))!;
        string parent = sourceFolder.Length == 0 || sourcePath.Length == 0
            ? fullRoot : Path.GetDirectoryName(sourceFolder)!;
        // Legacy loose JSON documents do not own the pack root: never move it.
        bool looseDocument = sourcePath.Length != 0 && Same(sourceFolder, fullRoot);
        string destination = looseDocument ? Path.GetFullPath(sourcePath)
            : Path.Combine(Path.GetDirectoryName(PackFileStore.NewPackPath(parent, packId))!,
                sourcePath.Length == 0 ? "items.json" : Path.GetFileName(sourcePath));
        PackFileStore.NewPackPath(fullRoot, packId);
        CheckLocation(fullRoot, Path.GetDirectoryName(destination)!);
        if (sourceFolder.Length != 0) CheckLocation(fullRoot, sourceFolder);
        CheckSource(sourcePath, expectedText);
        string targetFolder = Path.GetDirectoryName(destination)!;
        bool sameFolder = sourceFolder.Length != 0 && Same(sourceFolder, targetFolder);
        if (!sameFolder && (Directory.Exists(targetFolder) || File.Exists(targetFolder)))
            throw new IOException("The destination pack folder already exists. Choose a different pack ID; folders are never merged or overwritten.");
        if (expectedText == null && File.Exists(destination))
            throw new IOException("A pack already exists at this location. Select that pack to edit it.");
        if (sourceFolder.Length != 0 && !string.Equals(Full(sourceFolder), Full(targetFolder), StringComparison.Ordinal))
            CheckDedicatedFolder(sourceFolder, sourcePath);
        return destination;
    }

    internal string Save(string root, string sourcePath, string packId, string? expectedText, Action<string> write)
    {
        string destination = Destination(root, sourcePath, packId, expectedText);
        string targetFolder = Path.GetDirectoryName(destination)!;
        string sourceFolder = Folder;
        bool moved = sourceFolder.Length != 0 && !string.Equals(Full(sourceFolder), Full(targetFolder), StringComparison.Ordinal);
        try
        {
            if (moved) MoveFolder(root, targetFolder);
            write(destination);
            Folder = targetFolder;
            artIdentity ??= packId;
            return destination;
        }
        catch (Exception error)
        {
            if (moved && !string.Equals(Folder, sourceFolder, StringComparison.Ordinal))
            {
                try { MoveFolder(root, sourceFolder); }
                catch (Exception rollback)
                {
                    throw new IOException("Save failed and the folder could not be moved back. Your files remain at '" +
                        Folder + "'. Reload that folder before retrying. " + rollback.Message, error);
                }
            }
            throw;
        }
    }

    private void MoveFolder(string root, string destination)
    {
        CheckLocation(root, Folder);
        CheckLocation(root, destination);
        if (Same(Folder, root) || Same(destination, root))
            throw new IOException("The item-pack root cannot be renamed.");
        if (Same(Folder, destination))
        {
            // Windows requires an intermediate name for a case-only rename.
            string temporary = Path.Combine(Path.GetDirectoryName(Folder)!, ".cil-rename-" + Guid.NewGuid().ToString("N"));
            Directory.Move(Folder, temporary);
            Folder = temporary;
        }
        Directory.Move(Folder, destination);
        Folder = destination;
    }

    private static void CheckSource(string path, string? expectedText)
    {
        if (expectedText != null && (path.Length == 0 || !File.Exists(path) ||
            !string.Equals(File.ReadAllText(path), expectedText, StringComparison.Ordinal)))
            throw new IOException("The pack changed on disk. Your edits are still open; reload or reconcile the other changes before saving.");
    }

    private static void CheckDedicatedFolder(string folder, string sourcePath)
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(folder))
        {
            FileAttributes attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Pack folders containing links cannot be renamed automatically.");
            if ((attributes & FileAttributes.Directory) != 0) CheckDedicatedFolder(entry, sourcePath);
            else if (Path.GetExtension(entry).Equals(".json", StringComparison.OrdinalIgnoreCase) && !Same(entry, sourcePath))
                throw new IOException("This folder contains another JSON document. Give the pack a dedicated folder before renaming it.");
        }
    }

    private static void CheckLocation(string root, string folder)
    {
        string fullRoot = Full(root), current = Full(folder);
        if (!Same(fullRoot, current) && !current.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The pack folder must stay inside the item-pack directory.");
        while (true)
        {
            if ((Directory.Exists(current) || File.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked pack folders cannot be edited or renamed automatically.");
            if (Same(current, fullRoot)) break;
            current = Path.GetDirectoryName(current)!;
        }
    }

    private static string Full(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    private static bool Same(string a, string b) => a.Length != 0 && b.Length != 0 && string.Equals(Full(a), Full(b), StringComparison.OrdinalIgnoreCase);
}
