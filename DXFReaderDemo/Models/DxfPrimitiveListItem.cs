using DXFReaderCore.Models;

namespace DXFReaderDemo.Models;

public sealed class DxfPrimitiveListItem
{
    public required DxfPrimitive Primitive { get; init; }

    public required int Index { get; init; }

    public required string Title { get; init; }

    public required string Detail { get; init; }

    public required string CoordinateText { get; init; }
}
