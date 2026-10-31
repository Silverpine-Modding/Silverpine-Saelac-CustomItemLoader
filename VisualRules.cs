namespace SilverpineMods.CustomItemLoader;

internal static class VisualRules
{
    internal static bool Changed(float previous, float parsed) =>
        !float.IsNaN(parsed) && !float.IsInfinity(parsed) && !previous.Equals(parsed);

    // An explicitly untextured glTF material uses its base-color factor. A
    // texture belonging to another material must never be substituted for it.
    internal static bool AllowTextureFallback(int sourceMaterialIndex) => sourceMaterialIndex < 0;

    internal static bool HalfSizeWorldSprite(bool glbVisual, bool pickupable, bool gold) =>
        !glbVisual && !pickupable && !gold;
}
