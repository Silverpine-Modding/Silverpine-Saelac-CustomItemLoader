#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SilverpineMods.CustomItemLoader;

// Cache keys also serve addon-defined views whose suffixes are not known here.
// Keep their original namespace when an author changes a draft's pack ID. This
// file travels with .cache; generated furniture/save IDs still use the real ID.
internal static class PackCacheIdentity
{
    private static string MapPath(string folder)
    {
        string cache = Path.Combine(folder, ".cache");
        string path = Path.Combine(cache, "pack-identities.map");
        foreach (string entry in new[] { cache, path })
            if ((File.Exists(entry) || Directory.Exists(entry)) && (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked cache folders or identity maps cannot be used for pack renaming.");
        return path;
    }

    internal static string Resolve(string folder, string packId) => Resolve(Read(folder), packId);

    internal static void Bind(string folder, string packId, string originalId)
    {
        if (string.Equals(packId, originalId, StringComparison.OrdinalIgnoreCase)) return;
        string path = MapPath(folder);
        string? previous = File.Exists(path) ? File.ReadAllText(path) : null;
        var identities = Parse(previous);
        string stable = Resolve(identities, originalId);
        if (string.Equals(Resolve(identities, packId), stable, StringComparison.OrdinalIgnoreCase)) return;
        if (identities.ContainsKey(packId))
            throw new IOException("This pack ID is already associated with different cached artwork. Choose a different ID.");
        identities.Add(packId, stable);
        PackFileStore.Save(path, JsonConvert.SerializeObject(identities, Formatting.Indented), previous);
    }

    private static Dictionary<string, string> Read(string folder)
    {
        string path = MapPath(folder);
        return Parse(File.Exists(path) ? File.ReadAllText(path) : null);
    }

    private static Dictionary<string, string> Parse(string? text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (text == null) return result;
        try
        {
            if (text.Length > 1024 * 1024) throw new InvalidDataException("Cache identity map is too large.");
            var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(text)
                ?? throw new InvalidDataException("Empty cache identity map.");
            foreach (var pair in values)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    throw new InvalidDataException("Empty cache identity.");
                result.Add(pair.Key, pair.Value);
            }
            foreach (string key in result.Keys) Resolve(result, key);
            return result;
        }
        catch (Exception error)
        {
            throw new InvalidDataException("The pack's .cache/pack-identities.map is invalid. Restore it from backup to keep cached artwork accessible.", error);
        }
    }

    private static string Resolve(Dictionary<string, string> identities, string packId)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (identities.TryGetValue(packId, out string? next))
        {
            if (!visited.Add(packId)) throw new InvalidDataException("Cache identities contain a cycle.");
            packId = next;
        }
        return packId;
    }
}
