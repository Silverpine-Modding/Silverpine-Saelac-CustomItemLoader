#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SilverpineMods.CustomItemLoader;

internal static class AssetFiles
{
    // Length-delimited tuple: neither an item ID nor an addon suffix can alias
    // another cache entry. Keep the full digest, not a truncated display hash.
    internal static string CachePath(string folder, string pack, string item, string variant)
        => Path.Combine(folder, ".cache", "v2-" + IdentityHash(pack, item, variant) + ".png");

    internal static string FurnitureName(string pack, string item) =>
        "cil_generated_furniture__v2_" + IdentityHash(pack, item, "furniture");

    private static string IdentityHash(string pack, string item, string variant)
    {
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
        {
            writer.Write(pack.ToLowerInvariant());
            writer.Write(item.ToLowerInvariant());
            writer.Write(variant.ToLowerInvariant());
        }
        return Hash(bytes.ToArray());
    }

    internal static string ImportImage(string source, string assets)
    {
        byte[] bytes = File.ReadAllBytes(source);
        Directory.CreateDirectory(assets);
        string destination = Path.Combine(assets,
            Hash(bytes) + Path.GetExtension(source).ToLowerInvariant());
        if (File.Exists(destination))
        {
            if (!Equal(bytes, File.ReadAllBytes(destination)))
                throw new IOException("The content-addressed asset already exists with different contents: " + destination);
            return destination;
        }
        WriteNewFile(destination, bytes);
        return destination;
    }

    internal static void CopyCache(string source, string destination)
    {
        if (!File.Exists(source)) return;
        WriteNewFile(destination, File.ReadAllBytes(source));
        string key = Path.ChangeExtension(source, ".key");
        if (File.Exists(key)) WriteNewFile(Path.ChangeExtension(destination, ".key"), File.ReadAllBytes(key));
    }

    private static void WriteNewFile(string destination, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
        {
            if (!Equal(bytes, File.ReadAllBytes(destination)))
                throw new IOException("An asset already exists with different contents: " + destination);
            return;
        }
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static void WriteCache(string imagePath, byte[] png, string key)
    {
        // Commit complete images, never truncate the only distributed visual.
        // Image first: an interrupted key write merely triggers a re-render
        // when the source model is available; cache-only readers keep valid art.
        ReplaceGeneratedFile(imagePath, png);
        ReplaceGeneratedFile(Path.ChangeExtension(imagePath, ".key"), Encoding.UTF8.GetBytes(key));
    }

    private static void ReplaceGeneratedFile(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static bool Equal(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
}
