using System;

namespace SilverpineMods.CustomItemLoader;

// Logical IMGUI units, before ModTools applies the screen scale. Keeping this
// budget explicit prevents long labels from redistributing adjacent columns.
internal static class EditorFormLayout
{
    internal const float CanvasWidth = 1920f;
    internal const float OuterInset = 12f;
    internal const float SidebarWidth = 340f;
    internal const float PreviewWidth = 460f;
    internal const float PanelGap = 12f;
    internal const float PanelInset = 18f;
    internal const float ScrollbarReserve = 28f;
    internal const float ColumnGap = 24f;
    internal const float MinimumColumnWidth = 300f;
    internal const float FieldGap = 12f;
    internal const float ControlHeight = 44f;
    internal const float ChoiceSpacing = 28f; // Two 6-unit gaps plus control margins.
    internal const float DetailsWidth = CanvasWidth - 2 * OuterInset - SidebarWidth - PreviewWidth - 2 * PanelGap;
    internal const float ContentWidth = DetailsWidth - 2 * PanelInset - ScrollbarReserve;

    internal static bool UseColumns(float width) => width >= 2 * MinimumColumnWidth + ColumnGap;
    internal static float ColumnWidth(float width) => Math.Max(0, (width - ColumnGap) / 2);
    internal static float ChoiceWidth(float available, float measured, float arrowWidth) =>
        Math.Max(0, Math.Min(measured, available - 2 * arrowWidth - ChoiceSpacing));

    internal static float ChoiceHeight(float measuredHeight) =>
        Math.Max(ControlHeight, (float)Math.Ceiling(measuredHeight + 2f));

    internal static float TextInputWidth(float availableWidth, float horizontalMargin, float reservedWidth = 0f) =>
        Math.Max(1f, availableWidth - horizontalMargin - reservedWidth);

    internal static float TextInputHeight(float measuredHeight, float minimumHeight) =>
        Math.Max(Math.Max(ControlHeight, minimumHeight), (float)Math.Ceiling(measuredHeight + 2f));

    // Measure a final empty line with a visible glyph. Some native fonts omit
    // trailing blank lines from CalcHeight; the placeholder is never drawn or saved.
    internal static string TextMeasurement(string value) =>
        value.Length == 0 || value.EndsWith("\n", StringComparison.Ordinal) || value.EndsWith("\r", StringComparison.Ordinal)
            ? value + "M" : value;
}
