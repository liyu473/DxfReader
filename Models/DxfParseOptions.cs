namespace DXFReaderCore.Models;

public sealed class DxfParseOptions
{
    public int CurvePrecision { get; init; } = 64;

    public int SplinePrecision { get; init; } = 96;

    public int MaxInsertDepth { get; init; } = 8;

    public bool IncludePoints { get; init; } = true;
}
