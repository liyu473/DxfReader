using DXFReaderCore.Models;

namespace DXFReaderCore.Internal;

internal static class DxfContourAnalyzer
{
    private const double PointTolerance = 1e-6;

    public static IReadOnlyList<DxfContour> Build(IReadOnlyList<DxfPrimitive> primitives)
    {
        if (primitives.Count == 0)
        {
            return [];
        }

        var remaining = new List<DxfPrimitive>(primitives.Where(static primitive => primitive.Points.Count > 0));
        var contours = new List<DxfContour>(remaining.Count);
        var contourIndex = 0;

        while (remaining.Count > 0)
        {
            var seed = remaining[0];
            remaining.RemoveAt(0);

            var contourPrimitives = new List<DxfPrimitive> { seed };
            var contourStart = GetStart(seed);
            var contourEnd = GetEnd(seed);

            if (!seed.IsClosed && seed.Kind != DxfPrimitiveKind.Point)
            {
                var foundNext = true;
                while (foundNext)
                {
                    foundNext = false;

                    for (var i = 0; i < remaining.Count; i++)
                    {
                        var candidate = remaining[i];

                        if (candidate.Kind == DxfPrimitiveKind.Point)
                        {
                            continue;
                        }

                        if (TryAttachToTail(candidate, contourEnd, out var orientedTail))
                        {
                            contourPrimitives.Add(orientedTail);
                            contourEnd = GetEnd(orientedTail);
                            remaining.RemoveAt(i);
                            foundNext = true;
                            break;
                        }

                        if (TryAttachToHead(candidate, contourStart, out var orientedHead))
                        {
                            contourPrimitives.Insert(0, orientedHead);
                            contourStart = GetStart(orientedHead);
                            remaining.RemoveAt(i);
                            foundNext = true;
                            break;
                        }
                    }
                }
            }

            var bounds = DxfBounds.Empty;
            foreach (var primitive in contourPrimitives)
            {
                foreach (var point in primitive.Points)
                {
                    bounds = bounds.Include(point);
                }
            }

            var isClosed = contourPrimitives.Any(static primitive => primitive.IsClosed)
                || PointsEqual(contourStart, contourEnd);

            contours.Add(new DxfContour(
                contourIndex++,
                DxfContour.FreezePrimitives(contourPrimitives),
                bounds,
                isClosed));
        }

        return contours;
    }

    private static bool TryAttachToTail(DxfPrimitive candidate, DxfPoint contourEnd, out DxfPrimitive oriented)
    {
        if (PointsEqual(GetStart(candidate), contourEnd))
        {
            oriented = candidate;
            return true;
        }

        if (PointsEqual(GetEnd(candidate), contourEnd))
        {
            oriented = Reverse(candidate);
            return true;
        }

        oriented = candidate;
        return false;
    }

    private static bool TryAttachToHead(DxfPrimitive candidate, DxfPoint contourStart, out DxfPrimitive oriented)
    {
        if (PointsEqual(GetEnd(candidate), contourStart))
        {
            oriented = candidate;
            return true;
        }

        if (PointsEqual(GetStart(candidate), contourStart))
        {
            oriented = Reverse(candidate);
            return true;
        }

        oriented = candidate;
        return false;
    }

    private static DxfPrimitive Reverse(DxfPrimitive primitive) => new(
        primitive.Kind,
        DxfPrimitive.FreezePoints(primitive.Points.Reverse()),
        primitive.IsClosed,
        primitive.LayerName,
        primitive.ColorHex,
        primitive.SourceType,
        primitive.LineweightMillimeters);

    private static DxfPoint GetStart(DxfPrimitive primitive) => primitive.Points[0];

    private static DxfPoint GetEnd(DxfPrimitive primitive) => primitive.Points[^1];

    private static bool PointsEqual(DxfPoint left, DxfPoint right)
        => Math.Abs(left.X - right.X) <= PointTolerance
           && Math.Abs(left.Y - right.Y) <= PointTolerance
           && Math.Abs(left.Z - right.Z) <= PointTolerance;
}
