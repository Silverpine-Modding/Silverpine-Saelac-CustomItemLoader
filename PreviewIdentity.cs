#nullable enable
using System;

namespace SilverpineMods.CustomItemLoader;

// Async rendering may finish after the editor has moved on to another item.
internal sealed class PreviewIdentity
{
    private readonly object? document;
    private readonly object item;
    private readonly string packId;
    private readonly string? path;
    private readonly string definition;
    private readonly int generation;

    internal PreviewIdentity(object? document, object item, string packId,
        string? path, string definition, int generation)
    {
        this.document = document; this.item = item; this.packId = packId;
        this.path = path; this.definition = definition; this.generation = generation;
    }

    internal bool Matches(object? currentDocument, object? currentItem, string? currentPackId,
        string? currentPath, string currentDefinition, int currentGeneration) =>
        generation == currentGeneration && ReferenceEquals(document, currentDocument) &&
        ReferenceEquals(item, currentItem) && packId == currentPackId &&
        path == currentPath && definition == currentDefinition;
}
