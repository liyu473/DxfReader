using System.IO;
using System.Globalization;

namespace DXFReaderCore.Internal;

internal static class DxfInsertArrayReader
{
    public static IReadOnlyDictionary<string, DxfInsertArrayInfo> Read(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var result = new Dictionary<string, DxfInsertArrayInfo>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i + 1 < lines.Length; i += 2)
        {
            var code = lines[i].Trim();
            var value = lines[i + 1].Trim();

            if (!string.Equals(code, "0", StringComparison.Ordinal) ||
                !string.Equals(value, "INSERT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? handle = null;
            var columnCount = 1;
            var rowCount = 1;
            var columnSpacing = 0d;
            var rowSpacing = 0d;

            var j = i + 2;
            for (; j + 1 < lines.Length; j += 2)
            {
                var itemCode = lines[j].Trim();
                var itemValue = lines[j + 1].Trim();

                if (string.Equals(itemCode, "0", StringComparison.Ordinal))
                {
                    break;
                }

                switch (itemCode)
                {
                    case "5":
                        handle = itemValue;
                        break;
                    case "70":
                        if (int.TryParse(itemValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedColumnCount))
                        {
                            columnCount = Math.Max(parsedColumnCount, 1);
                        }
                        break;
                    case "71":
                        if (int.TryParse(itemValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedRowCount))
                        {
                            rowCount = Math.Max(parsedRowCount, 1);
                        }
                        break;
                    case "44":
                        if (double.TryParse(itemValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedColumnSpacing))
                        {
                            columnSpacing = parsedColumnSpacing;
                        }
                        break;
                    case "45":
                        if (double.TryParse(itemValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedRowSpacing))
                        {
                            rowSpacing = parsedRowSpacing;
                        }
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(handle) && (columnCount > 1 || rowCount > 1))
            {
                result[handle] = new DxfInsertArrayInfo(columnCount, rowCount, columnSpacing, rowSpacing);
            }

            i = j - 2;
        }

        return result;
    }
}
