using DXFReaderCore.Models;

namespace DXFReaderCore.Internal;

internal readonly record struct DxfAffineTransform(
    double M11,
    double M12,
    double M21,
    double M22,
    double OffsetX,
    double OffsetY)
{
    public static DxfAffineTransform Identity { get; } = new(1, 0, 0, 1, 0, 0);

    public static DxfAffineTransform CreateTranslation(double x, double y) => new(1, 0, 0, 1, x, y);

    public static DxfAffineTransform CreateScale(double scaleX, double scaleY) => new(scaleX, 0, 0, scaleY, 0, 0);

    public static DxfAffineTransform CreateRotation(double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new(cos, -sin, sin, cos, 0, 0);
    }

    public DxfAffineTransform Then(DxfAffineTransform next) => new(
        next.M11 * M11 + next.M12 * M21,
        next.M11 * M12 + next.M12 * M22,
        next.M21 * M11 + next.M22 * M21,
        next.M21 * M12 + next.M22 * M22,
        next.M11 * OffsetX + next.M12 * OffsetY + next.OffsetX,
        next.M21 * OffsetX + next.M22 * OffsetY + next.OffsetY);

    public DxfPoint Transform(DxfPoint point) => new(
        M11 * point.X + M12 * point.Y + OffsetX,
        M21 * point.X + M22 * point.Y + OffsetY,
        point.Z);
}
