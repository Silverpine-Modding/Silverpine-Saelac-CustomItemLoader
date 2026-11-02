#nullable enable
using System;
using System.IO;
using System.Text;

namespace SilverpineMods.CustomItemLoader;

internal static class PackFileStore
{
    internal static string NewPackPath(string root, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id == "." || id == ".." ||
            id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            id.EndsWith(".", StringComparison.Ordinal))
            throw new InvalidDataException("Choose a valid pack ID that does not end in a dot.");
        foreach (char character in id)
            if (!char.IsLetterOrDigit(character) && character != '.' && character != '_' && character != '-')
                throw new InvalidDataException("Pack IDs may contain only letters, numbers, dots, underscores, and hyphens.");
        string device = id.Split('.')[0].ToUpperInvariant();
        if (device == "CON" || device == "PRN" || device == "AUX" || device == "NUL" ||
            (device.Length == 4 && (device.StartsWith("COM", StringComparison.Ordinal) || device.StartsWith("LPT", StringComparison.Ordinal)) &&
             "123456789¹²³".IndexOf(device[3]) >= 0))
            throw new InvalidDataException("Choose a pack ID that is not a reserved Windows device name.");
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string folder = Path.GetFullPath(Path.Combine(fullRoot, id));
        if (!folder.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The pack folder must stay inside the item-pack directory.");
        return Path.Combine(folder, "items.json");
    }

    // null means a new document: never replace an existing pack. For edits,
    // check the original disk text to protect changes made by another tool.
    internal static void Save(string path, string text, string? expectedText)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(folder);
        string temporary = Path.Combine(folder, ".cil-save-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            if (expectedText == null)
            {
                // Move is atomic and fails if another pack already owns this path.
                File.Move(temporary, path);
            }
            else
            {
                if (!File.Exists(path) || !string.Equals(File.ReadAllText(path), expectedText, StringComparison.Ordinal))
                    throw new IOException("The pack changed on disk. Your edits are still open; reload or reconcile the other changes before saving.");
                File.Replace(temporary, path, path + ".bak");
            }
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
