namespace DXFReaderCore.Models;

public readonly record struct DxfBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    public static DxfBounds Empty { get; } = new(
        double.PositiveInfinity,
        double.PositiveInfinity,
        double.NegativeInfinity,
        double.NegativeInfinity);

    public bool IsEmpty =>
        double.IsPositiveInfinity(MinX)
        || double.IsPositiveInfinity(MinY)
        || double.IsNegativeInfinity(MaxX)
        || double.IsNegativeInfinity(MaxY);

    public double Width => IsEmpty ? 0 : MaxX - MinX;

    public double Height => IsEmpty ? 0 : MaxY - MinY;

    public DxfBounds Include(DxfPoint point)
    {
        if (IsEmpty)
        {
            return new DxfBounds(point.X, point.Y, point.X, point.Y);
        }

        return new DxfBounds(
            Math.Min(MinX, point.X),
            Math.Min(MinY, point.Y),
            Math.Max(MaxX, point.X),
            Math.Max(MaxY, point.Y));
    }
}
