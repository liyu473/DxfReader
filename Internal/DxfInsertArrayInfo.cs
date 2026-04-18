namespace DXFReaderCore.Internal;

internal readonly record struct DxfInsertArrayInfo(
    int ColumnCount,
    int RowCount,
    double ColumnSpacing,
    double RowSpacing)
{
    public static DxfInsertArrayInfo Default { get; } = new(1, 1, 0d, 0d);

    public bool IsArray => ColumnCount > 1 || RowCount > 1;
}
