using System.Collections.ObjectModel;
using System.IO;
using DXFReaderCore.Internal;
using DXFReaderCore.Models;
using netDxf;
using netDxf.Entities;
using netDxf.Units;

namespace DXFReaderCore;

using DxfPointEntity = Point;

public sealed class DxfParser
{
    public DxfDrawing Parse(string filePath, DxfParseOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("DXF file does not exist.", filePath);
        }

        options ??= new DxfParseOptions();
        var insertArrays = DxfInsertArrayReader.Read(filePath);

        var document = DxfDocument.Load(filePath)
            ?? throw new InvalidOperationException("Failed to load DXF file.");
        var unitsText = GetDrawingUnitsText(document);

        var primitives = new List<DxfPrimitive>();
        foreach (var entity in document.Entities.All)
        {
            AppendEntity(
                entity,
                primitives,
                DxfAffineTransform.Identity,
                insertArrays,
                options,
                depth: 0,
                inheritedLayerName: null,
                inheritedColorHex: null,
                inheritedLineweightMillimeters: null);
        }

        if (primitives.Count == 0)
        {
            return DxfDrawing.Empty(filePath);
        }

        var contours = DxfContourAnalyzer.Build(primitives);

        var bounds = DxfBounds.Empty;
        foreach (var primitive in primitives)
        {
            foreach (var point in primitive.Points)
            {
                bounds = bounds.Include(point);
            }
        }

        var layerEntityCounts = new ReadOnlyDictionary<string, int>(
            primitives
                .GroupBy(static primitive => primitive.LayerName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase));

        var sourceTypeCounts = new ReadOnlyDictionary<string, int>(
            primitives
                .GroupBy(static primitive => primitive.SourceType, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase));

        return new DxfDrawing(
            filePath,
            primitives.AsReadOnly(),
            contours,
            bounds,
            layerEntityCounts,
            sourceTypeCounts,
            unitsText);
    }

    private static void AppendEntity(
        EntityObject entity,
        ICollection<DxfPrimitive> primitives,
        DxfAffineTransform transform,
        IReadOnlyDictionary<string, DxfInsertArrayInfo> insertArrays,
        DxfParseOptions options,
        int depth,
        string? inheritedLayerName,
        string? inheritedColorHex,
        double? inheritedLineweightMillimeters)
    {
        if (depth > options.MaxInsertDepth)
        {
            return;
        }

        var layerName = ResolveLayerName(entity, inheritedLayerName);
        var colorHex = ResolveColorHex(entity, inheritedLayerName, inheritedColorHex);
        var lineweightMillimeters = ResolveLineweightMillimeters(entity, inheritedLayerName, inheritedLineweightMillimeters);
        var sourceType = entity.GetType().Name;

        switch (entity)
        {
            case Line line:
                AddPolyline(
                    primitives,
                    [
                        ToPoint(line.StartPoint, transform),
                        ToPoint(line.EndPoint, transform),
                    ],
                    isClosed: false,
                    layerName,
                    colorHex,
                    sourceType,
                    lineweightMillimeters);
                return;

            case Circle circle:
                AddPolyline(
                    primitives,
                    circle.PolygonalVertexes(options.CurvePrecision)
                        .Select(point => ToPoint(Translate(point, circle.Center), transform)),
                    isClosed: true,
                    layerName,
                    colorHex,
                    sourceType,
                    lineweightMillimeters);
                return;

            case Arc arc:
                AddPolyline(
                    primitives,
                    arc.PolygonalVertexes(options.CurvePrecision)
                        .Select(point => ToPoint(Translate(point, arc.Center), transform)),
                    isClosed: false,
                    layerName,
                    colorHex,
                    sourceType,
                    lineweightMillimeters);
                return;

            case Ellipse ellipse:
                AddPolyline(
                    primitives,
                    ellipse.PolygonalVertexes(options.CurvePrecision).Select(point => ToPoint(point, transform)),
                    IsEllipseClosed(ellipse),
                    layerName,
                    colorHex,
                    sourceType,
                    lineweightMillimeters);
                return;

            case Polyline2D polyline2D:
                AddPolyline(
                    primitives,
                    polyline2D.PolygonalVertexes(options.CurvePrecision).Select(point => ToPoint(point, transform)),
                    polyline2D.IsClosed,
                    layerName,
                    colorHex,
                    sourceType,
                    lineweightMillimeters);
                return;

            case Polyline3D polyline3D:
                AddPolyline(
                    primitives,
                    polyline3D.PolygonalVertexes(options.CurvePrecision).Select(point => ToPoint(point, transform)),
                    polyline3D.IsClosed,
                    layerName,
                    colorHex,
                    sourceType,
                    lineweightMillimeters);
                return;

            case Spline spline:
                AddPolyline(
                    primitives,
                    spline.PolygonalVertexes(options.SplinePrecision).Select(point => ToPoint(point, transform)),
                    spline.IsClosed,
                    layerName,
                    colorHex,
                    sourceType,
                    lineweightMillimeters);
                return;

            case DxfPointEntity pointEntity when options.IncludePoints:
                AddPoint(primitives, ToPoint(pointEntity.Position, transform), layerName, colorHex, sourceType, lineweightMillimeters);
                return;

            case Insert insert:
                AppendInsert(insert, primitives, transform, insertArrays, options, depth, layerName, colorHex, lineweightMillimeters);
                return;
        }
    }

    private static void AppendInsert(
        Insert insert,
        ICollection<DxfPrimitive> primitives,
        DxfAffineTransform parentTransform,
        IReadOnlyDictionary<string, DxfInsertArrayInfo> insertArrays,
        DxfParseOptions options,
        int depth,
        string insertLayerName,
        string insertColorHex,
        double insertLineweightMillimeters)
    {
        var block = insert.Block;
        if (block is null)
        {
            return;
        }

        var arrayInfo = ResolveInsertArrayInfo(insert, insertArrays);
        if (arrayInfo.IsArray)
        {
            AppendInsertArray(
                insert,
                primitives,
                parentTransform,
                insertArrays,
                options,
                depth,
                insertLayerName,
                insertColorHex,
                insertLineweightMillimeters,
                arrayInfo);
            return;
        }

        var explodedEntities = ExplodeInsert(insert);
        if (explodedEntities is not null)
        {
            foreach (var entity in explodedEntities)
            {
                AppendEntity(
                    entity,
                    primitives,
                    parentTransform,
                    insertArrays,
                    options,
                    depth + 1,
                    insertLayerName,
                    insertColorHex,
                    insertLineweightMillimeters);
            }

            return;
        }

        AppendInsertSingle(insert, primitives, parentTransform, insertArrays, options, depth, insertLayerName, insertColorHex, insertLineweightMillimeters);
    }

    private static void AppendInsertArray(
        Insert insert,
        ICollection<DxfPrimitive> primitives,
        DxfAffineTransform parentTransform,
        IReadOnlyDictionary<string, DxfInsertArrayInfo> insertArrays,
        DxfParseOptions options,
        int depth,
        string insertLayerName,
        string insertColorHex,
        double insertLineweightMillimeters,
        DxfInsertArrayInfo arrayInfo)
    {
        var block = insert.Block;
        if (block is null)
        {
            return;
        }

        var blockOrigin = block.Origin;
        for (var row = 0; row < arrayInfo.RowCount; row++)
        {
            for (var column = 0; column < arrayInfo.ColumnCount; column++)
            {
                var insertTransform = DxfAffineTransform.Identity
                    .Then(DxfAffineTransform.CreateTranslation(-blockOrigin.X, -blockOrigin.Y))
                    .Then(DxfAffineTransform.CreateTranslation(column * arrayInfo.ColumnSpacing, row * arrayInfo.RowSpacing))
                    .Then(DxfAffineTransform.CreateScale(insert.Scale.X, insert.Scale.Y))
                    .Then(DxfAffineTransform.CreateRotation(insert.Rotation))
                    .Then(DxfAffineTransform.CreateTranslation(insert.Position.X, insert.Position.Y));

                var combinedTransform = parentTransform.Then(insertTransform);
                foreach (var entity in block.Entities)
                {
                    AppendEntity(
                        entity,
                        primitives,
                        combinedTransform,
                        insertArrays,
                        options,
                        depth + 1,
                        insertLayerName,
                        insertColorHex,
                        insertLineweightMillimeters);
                }
            }
        }
    }

    private static void AppendInsertSingle(
        Insert insert,
        ICollection<DxfPrimitive> primitives,
        DxfAffineTransform parentTransform,
        IReadOnlyDictionary<string, DxfInsertArrayInfo> insertArrays,
        DxfParseOptions options,
        int depth,
        string insertLayerName,
        string insertColorHex,
        double insertLineweightMillimeters)
    {
        var block = insert.Block;
        if (block is null)
        {
            return;
        }

        var blockOrigin = block.Origin;
        var insertTransform = DxfAffineTransform.Identity
            .Then(DxfAffineTransform.CreateTranslation(-blockOrigin.X, -blockOrigin.Y))
            .Then(DxfAffineTransform.CreateScale(insert.Scale.X, insert.Scale.Y))
            .Then(DxfAffineTransform.CreateRotation(insert.Rotation))
            .Then(DxfAffineTransform.CreateTranslation(insert.Position.X, insert.Position.Y));

        var combinedTransform = parentTransform.Then(insertTransform);
        foreach (var entity in block.Entities)
        {
            AppendEntity(
                entity,
                primitives,
                combinedTransform,
                insertArrays,
                options,
                depth + 1,
                insertLayerName,
                insertColorHex,
                insertLineweightMillimeters);
        }
    }

    private static IReadOnlyList<EntityObject>? ExplodeInsert(Insert insert)
    {
        try
        {
            var exploded = insert.Explode();
            if (exploded is { Count: > 0 })
            {
                return exploded;
            }
        }
        catch
        {
            // Fall back to manual traversal.
        }

        return null;
    }

    private static DxfInsertArrayInfo ResolveInsertArrayInfo(
        Insert insert,
        IReadOnlyDictionary<string, DxfInsertArrayInfo> insertArrays)
    {
        var handle = insert.Handle;
        if (!string.IsNullOrWhiteSpace(handle) && insertArrays.TryGetValue(handle, out var arrayInfo))
        {
            return arrayInfo;
        }

        return DxfInsertArrayInfo.Default;
    }

    private static void AddPolyline(
        ICollection<DxfPrimitive> primitives,
        IEnumerable<DxfPoint> points,
        bool isClosed,
        string layerName,
        string colorHex,
        string sourceType,
        double lineweightMillimeters)
    {
        var frozenPoints = DxfPrimitive.FreezePoints(points);
        if (frozenPoints.Count < 2)
        {
            return;
        }

        primitives.Add(new DxfPrimitive(
            DxfPrimitiveKind.Polyline,
            frozenPoints,
            isClosed,
            layerName,
            colorHex,
            sourceType,
            lineweightMillimeters));
    }

    private static void AddPoint(
        ICollection<DxfPrimitive> primitives,
        DxfPoint point,
        string layerName,
        string colorHex,
        string sourceType,
        double lineweightMillimeters)
    {
        primitives.Add(new DxfPrimitive(
            DxfPrimitiveKind.Point,
            DxfPrimitive.FreezePoints([point]),
            isClosed: false,
            layerName,
            colorHex,
            sourceType,
            lineweightMillimeters));
    }

    private static DxfPoint ToPoint(Vector2 point, DxfAffineTransform transform)
        => transform.Transform(new DxfPoint(point.X, point.Y));

    private static DxfPoint ToPoint(Vector3 point, DxfAffineTransform transform)
        => transform.Transform(new DxfPoint(point.X, point.Y, point.Z));

    private static Vector2 Translate(Vector2 point, Vector3 offset)
        => new(point.X + offset.X, point.Y + offset.Y);

    private static string ResolveLayerName(EntityObject entity, string? inheritedLayerName)
    {
        var entityLayerName = entity.Layer?.Name;
        if (!string.IsNullOrWhiteSpace(inheritedLayerName)
            && string.Equals(entityLayerName, "0", StringComparison.OrdinalIgnoreCase))
        {
            return inheritedLayerName;
        }

        return string.IsNullOrWhiteSpace(entityLayerName)
            ? inheritedLayerName ?? "0"
            : entityLayerName;
    }

    private static string ResolveColorHex(
        EntityObject entity,
        string? inheritedLayerName,
        string? inheritedColorHex)
    {
        var color = entity.Color;

        if (color.IsByBlock && !string.IsNullOrWhiteSpace(inheritedColorHex))
        {
            return inheritedColorHex;
        }

        if (color.IsByLayer)
        {
            var entityLayerName = entity.Layer?.Name;
            if (!string.IsNullOrWhiteSpace(inheritedColorHex)
                && !string.IsNullOrWhiteSpace(inheritedLayerName)
                && string.Equals(entityLayerName, "0", StringComparison.OrdinalIgnoreCase))
            {
                return inheritedColorHex;
            }

            if (entity.Layer is { Color: { } layerColor })
            {
                return ToColorHex(layerColor);
            }
        }

        return ToColorHex(color);
    }

    private static string ToColorHex(AciColor color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static double ResolveLineweightMillimeters(
        EntityObject entity,
        string? inheritedLayerName,
        double? inheritedLineweightMillimeters)
    {
        var lineweight = entity.Lineweight;

        if (lineweight == netDxf.Lineweight.ByBlock && inheritedLineweightMillimeters.HasValue)
        {
            return inheritedLineweightMillimeters.Value;
        }

        if (lineweight == netDxf.Lineweight.ByLayer || lineweight == netDxf.Lineweight.Default)
        {
            var entityLayerName = entity.Layer?.Name;
            if (inheritedLineweightMillimeters.HasValue
                && !string.IsNullOrWhiteSpace(inheritedLayerName)
                && string.Equals(entityLayerName, "0", StringComparison.OrdinalIgnoreCase))
            {
                return inheritedLineweightMillimeters.Value;
            }

            if (entity.Layer is { Lineweight: var layerLineweight })
            {
                var resolvedLayerWeight = ToLineweightMillimeters(layerLineweight);
                if (resolvedLayerWeight > 0d)
                {
                    return resolvedLayerWeight;
                }
            }
        }

        return ToLineweightMillimeters(lineweight);
    }

    private static double ToLineweightMillimeters(netDxf.Lineweight lineweight)
    {
        var value = (int)lineweight;
        return value >= 0 ? value / 100d : 0d;
    }

    private static bool IsEllipseClosed(Ellipse ellipse)
    {
        var angle = NormalizeAngle(ellipse.EndAngle - ellipse.StartAngle);
        return Math.Abs(angle) < 0.0001d || Math.Abs(angle - 360d) < 0.0001d;
    }

    private static string GetDrawingUnitsText(DxfDocument document)
    {
        var units = document.DrawingVariables?.InsUnits ?? DrawingUnits.Unitless;
        return units switch
        {
            DrawingUnits.Inches => "Inches",
            DrawingUnits.Feet => "Feet",
            DrawingUnits.Miles => "Miles",
            DrawingUnits.Millimeters => "Millimeters (mm)",
            DrawingUnits.Centimeters => "Centimeters (cm)",
            DrawingUnits.Meters => "Meters (m)",
            DrawingUnits.Kilometers => "Kilometers (km)",
            DrawingUnits.Microinches => "Microinches",
            DrawingUnits.Mils => "Mils",
            DrawingUnits.Yards => "Yards",
            DrawingUnits.Angstroms => "Angstroms",
            DrawingUnits.Nanometers => "Nanometers",
            DrawingUnits.Microns => "Microns",
            DrawingUnits.Decimeters => "Decimeters",
            DrawingUnits.Decameters => "Decameters",
            DrawingUnits.Hectometers => "Hectometers",
            DrawingUnits.Gigameters => "Gigameters",
            DrawingUnits.AstronomicalUnits => "Astronomical Units",
            DrawingUnits.LightYears => "Light Years",
            DrawingUnits.Parsecs => "Parsecs",
            DrawingUnits.USSurveyFeet => "US Survey Feet",
            DrawingUnits.USSurveyInches => "US Survey Inches",
            DrawingUnits.USSurveyYards => "US Survey Yards",
            DrawingUnits.USSurveyMiles => "US Survey Miles",
            _ => "Unitless / Unspecified",
        };
    }

    private static double NormalizeAngle(double angle)
    {
        var normalized = angle % 360d;
        if (normalized < 0)
        {
            normalized += 360d;
        }

        return normalized;
    }
}
