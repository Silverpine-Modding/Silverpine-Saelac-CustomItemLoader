using SilverpineMods.CustomItemLoader;
using UnityEngine;

internal static class FurnitureLightingChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var gameObject = new GameObject();
        var nativeDiffuse = new Material();
        Resources.Diffuse = nativeDiffuse;
        var renderer = FurnitureSpriteLighting.GetOrCreateRenderer(gameObject);
        check(ReferenceEquals(renderer.sharedMaterial, nativeDiffuse) && Resources.LastPath == "Materials/sprite-diffuse",
            "new generated furniture uses the exact native shared diffuse material");
        var second = FurnitureSpriteLighting.GetOrCreateRenderer(new GameObject());
        check(ReferenceEquals(second.sharedMaterial, nativeDiffuse) && Resources.LoadCalls == 1,
            "furniture shares one cached native material without per-object copies");

        var highlightMaterial = new Material();
        renderer.sharedMaterial = highlightMaterial;
        renderer.color = "green";
        check(ReferenceEquals(FurnitureSpriteLighting.GetOrCreateRenderer(gameObject), renderer) &&
            ReferenceEquals(renderer.sharedMaterial, highlightMaterial) && renderer.color == "green",
            "sprite refresh preserves an existing renderer's native pickup highlight and tint");
        FurnitureSpriteLighting.ApplyDiffuse(renderer);
        check(ReferenceEquals(renderer.sharedMaterial, nativeDiffuse) && renderer.color == "green",
            "explicit placement/load initialization restores diffuse without changing tint");

        renderer.Destroyed = true;
        var replacement = FurnitureSpriteLighting.GetOrCreateRenderer(gameObject);
        check(!ReferenceEquals(replacement, renderer) && ReferenceEquals(replacement.sharedMaterial, nativeDiffuse),
            "a Unity-destroyed renderer is recreated with diffuse lighting");
        nativeDiffuse.Destroyed = true;
        Resources.Diffuse = new Material();
        FurnitureSpriteLighting.ApplyDiffuse(replacement);
        check(ReferenceEquals(replacement.sharedMaterial, Resources.Diffuse) && Resources.LoadCalls == 2,
            "a Unity-destroyed cached material is loaded again on initialization");

        Resources.Diffuse.Destroyed = true;
        Resources.Diffuse = null;
        replacement.sharedMaterial = highlightMaterial;
        int warningsBefore = Plugin.Log.Messages.Count;
        FurnitureSpriteLighting.ApplyDiffuse(replacement);
        FurnitureSpriteLighting.ApplyDiffuse(replacement);
        check(ReferenceEquals(replacement.sharedMaterial, highlightMaterial) && Plugin.Log.Messages.Count == warningsBefore + 1,
            "missing native material leaves current art usable and logs only once");
        Resources.Diffuse = new Material();
        FurnitureSpriteLighting.ApplyDiffuse(replacement);
        check(ReferenceEquals(replacement.sharedMaterial, Resources.Diffuse),
            "missing material can recover on a later initialization");
        int loadsBefore = Resources.LoadCalls;
        FurnitureSpriteLighting.ApplyDiffuse(null!);
        replacement.Destroyed = true;
        FurnitureSpriteLighting.ApplyDiffuse(replacement);
        check(Resources.LoadCalls == loadsBefore, "missing or destroyed renderers are ignored safely");
    }
}
