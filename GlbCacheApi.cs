#nullable enable
using System;
using System.IO;
using System.Linq;
namespace SilverpineMods.CustomItemLoader;
/// <summary>CIL-specific rendered-model cache migration for add-ons; not the shared art library.</summary>
public static class GlbCacheApi
{
    /// <summary>Reads both legacy and current CIL cache names without changing the cache.</summary>
    public static string GetReadableGlbCachePath(string folder, string packId, string itemId, string suffix) =>
        ItemPackLoader.FindReadableGlbCachePath(folder, packId, itemId, suffix);

    /// <summary>Preserves an old stage cache under a new immutable identity; leaves originals intact.</summary>
    public static void CopyGlbCache(string folder, string packId, string itemId, string oldSuffix, string newSuffix)
    {
        string source = GetReadableGlbCachePath(folder, packId, itemId, oldSuffix);
        if (!File.Exists(source)) return;
        string destination = CustomItemApi.GetGlbSpriteCachePath(folder, packId, itemId, newSuffix);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        // Never replace an unrelated or already published cache.
        if (File.Exists(destination))
        {
            if (!File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(destination)))
                throw new IOException("Stage cache identity collision.");
            return;
        }
        string key = Path.ChangeExtension(source, ".key");
        if (File.Exists(key)) File.Copy(key, Path.ChangeExtension(destination, ".key"), false);
        File.Copy(source, destination, false);
    }
}
