#nullable enable
using System;
using System.IO;

namespace SilverpineMods.CustomItemLoader;

// World units, independent of render resolution and placement scale. Null
// directions mean zero, never another direction's mutable object.
internal sealed class PlacementOffset
{
    public float x;
    public float y;
    internal static float ShiftPivot(float pivotPixels, float extentPixels, float pixelsPerUnit, float scale, float offset) =>
        (pivotPixels - offset * pixelsPerUnit / scale) / extentPixels;
    internal void Validate()
    {
        if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) ||
            float.IsInfinity(y) || Math.Abs(x) > 100 || Math.Abs(y) > 100)
            throw new InvalidDataException("Placement offsets must be finite and between -100 and 100 tiles.");
    }
}

internal sealed class PlacementOffsets
{
    public PlacementOffset? front;
    public PlacementOffset? right;
    public PlacementOffset? back;
    public PlacementOffset? left;
    internal PlacementOffset Get(int direction) => direction switch
    {
        1 => right ?? new PlacementOffset(),
        2 => back ?? new PlacementOffset(),
        3 => left ?? new PlacementOffset(),
        _ => front ?? new PlacementOffset()
    };
    internal void Set(int direction, float x, float y)
    {
        var value = new PlacementOffset { x = x, y = y };
        value.Validate();
        switch (direction)
        {
            case 1: right = value; break;
            case 2: back = value; break;
            case 3: left = value; break;
            default: front = value; break;
        }
    }
    internal void Validate() { front?.Validate(); right?.Validate(); back?.Validate(); left?.Validate(); }
}
