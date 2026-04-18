using DXFReaderCore.Models;

namespace DXFReaderCore;

/// <summary>
/// DXF 解析服务接口
/// </summary>
public interface IDxfParserService
{
    /// <summary>
    /// 解析 DXF 文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">解析选项</param>
    /// <returns>DXF 绘图对象</returns>
    DxfDrawing Parse(string filePath, DxfParseOptions? options = null);
}
