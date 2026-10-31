#nullable enable
using UnityEngine;

namespace SilverpineMods.CustomItemLoader;

internal static class FurnitureSpriteLighting
{
    private static Material? diffuseMaterial;
    private static bool warnedMissingMaterial;

    internal static SpriteRenderer GetOrCreateRenderer(GameObject gameObject)
    {
        SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = gameObject.AddComponent<SpriteRenderer>();
            ApplyDiffuse(renderer);
        }
        return renderer;
    }

    internal static void ApplyDiffuse(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;
        // Use the game's material, including its shader/settings. Do not create
        // per-object materials or modify/destroy the shared resource.
        if (diffuseMaterial == null)
            diffuseMaterial = Resources.Load<Material>("Materials/sprite-diffuse");
        if (diffuseMaterial != null)
        {
            renderer.sharedMaterial = diffuseMaterial;
            return;
        }

        // Leave the renderer usable if a game update removes the resource.
        // A later initialization can retry; avoid one warning per furniture item.
        if (!warnedMissingMaterial)
        {
            warnedMissingMaterial = true;
            Plugin.Log.LogWarning("Furniture material 'Materials/sprite-diffuse' is unavailable; " +
                "generated furniture will keep its current material.");
        }
    }
}
