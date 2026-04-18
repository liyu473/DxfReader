using DXFReaderCore.Models;

namespace DXFReaderCore;

/// <summary>
/// DXF 解析服务实现
/// </summary>
public class DxfParserService : IDxfParserService
{
    private readonly DxfParser _parser;

    public DxfParserService()
    {
        _parser = new DxfParser();
    }

    public DxfDrawing Parse(string filePath, DxfParseOptions? options = null)
    {
        return _parser.Parse(filePath, options);
    }
}
