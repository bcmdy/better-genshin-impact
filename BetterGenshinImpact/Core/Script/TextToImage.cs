using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace BetterGenshinImpact.Core.Script;

public class TextToImage
{
    private static readonly Dictionary<string, FontFamily> FontFamilies = new();
    private static readonly string[] AvailableFonts = ["HYW", "Fgi-Regular", "MiSans-Regular", "deluge-led"];

    private const int DefaultFontSize = 24;
    private const float DefaultPaddingX = 3;
    private const float DefaultPaddingY = 2;

    static TextToImage()
    {
        // 尝试从嵌入式资源加载字体
        var assembly = typeof(TextToImage).Assembly;
        var tempFontDir = Path.Combine(Path.GetTempPath(), "BetterGI_Fonts");
        Directory.CreateDirectory(tempFontDir);

        // 输出调试信息
        var allResources = assembly.GetManifestResourceNames();
        System.Diagnostics.Debug.WriteLine($"[TextToImage] 嵌入式资源数量: {allResources.Length}");

        foreach (var fontName in AvailableFonts)
        {
            try
            {
                var resourceName = $"Resources.Fonts.{fontName}.ttf";
                System.Diagnostics.Debug.WriteLine($"[TextToImage] 尝试加载资源: {resourceName}");

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    var tempFontPath = Path.Combine(tempFontDir, $"{fontName}.ttf");
                    // 检查临时文件是否存在，不存在则创建
                    if (!File.Exists(tempFontPath))
                    {
                        using var fileStream = File.Create(tempFontPath);
                        stream.CopyTo(fileStream);
                        System.Diagnostics.Debug.WriteLine($"[TextToImage] 已保存字体到: {tempFontPath}");
                    }
                    // 从临时文件加载字体
                    var collection = new FontCollection();
                    collection.Add(tempFontPath);
                    var family = collection.Families.FirstOrDefault();
                    if (family != null)
                    {
                        FontFamilies[fontName] = family;
                        System.Diagnostics.Debug.WriteLine($"[TextToImage] 成功加载字体: {fontName}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[TextToImage] 资源流为空: {resourceName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TextToImage] 加载失败 {fontName}: {ex.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine($"[TextToImage] 共加载字体: {FontFamilies.Count}");
    }

    /// <summary>
    /// 生成文字图片，返回PNG格式的二进制图片数据
    /// </summary>
    /// <param name="text">要生成的文字</param>
    /// <param name="fontName">字体名称，默认Fgi-Regular</param>
    /// <param name="fontSize">字体大小，默认24</param>
    /// <param name="textColor">文字颜色(hex格式，如#505769)，默认#505769</param>
    /// <param name="bgColor">背景颜色(hex格式，如#F5F6F7)，默认#F5F6F7</param>
    /// <returns>PNG格式的二进制图片数据</returns>
    public byte[] Generate(string text, string fontName = "HYW", float fontSize = DefaultFontSize,
        string textColor = "#505769", string bgColor = "#F5F6F7")
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<byte>();

        var textColorRgb = ParseHexColor(textColor);
        var bgColorRgb = ParseHexColor(bgColor);

        var fontFamily = FontFamilies.TryGetValue(fontName, out var family) ? family : FontFamilies.GetValueOrDefault("HYW");
        if (fontFamily == null)
            fontFamily = SystemFonts.Get("Arial");

        var font = fontFamily.CreateFont(fontSize, FontStyle.Regular);

        var textWidth = MeasureTextWidth(text, font);
        var width = (int)Math.Ceiling(textWidth + DefaultPaddingX * 2 + 2);
        var height = (int)Math.Ceiling(fontSize + DefaultPaddingY * 2);

        using var img = new Image<Rgba32>(width, height);
        img.Mutate(ctx => ctx.Fill(bgColorRgb));

        var renderOptions = new RichTextOptions(font)
        {
            Origin = new PointF(DefaultPaddingX, (height - fontSize) / 2),
            Dpi = 72,
        };

        img.Mutate(ctx => ctx.DrawText(renderOptions, text, textColorRgb));

        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static float MeasureTextWidth(string text, Font font)
    {
        var textOptions = new TextOptions(font)
        {
            Dpi = 72,
        };
        var measured = TextMeasurer.MeasureSize(text, textOptions);
        return measured.Width;
    }

    private static Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            var r = Convert.ToByte(hex[..2], 16);
            var g = Convert.ToByte(hex[2..4], 16);
            var b = Convert.ToByte(hex[4..6], 16);
            return Color.FromRgb(r, g, b);
        }
        return Color.FromRgb(80, 87, 105);
    }

    /// <summary>
    /// 获取可用的字体列表
    /// </summary>
    public string[] GetAvailableFonts() => AvailableFonts;

    /// <summary>
    /// 从字体文件生成文字图片（使用外部字体文件）
    /// </summary>
    /// <param name="text">要生成的文字</param>
    /// <param name="fontFilePath">字体文件路径(.ttf)</param>
    /// <param name="fontSize">字体大小，默认24</param>
    /// <param name="textColor">文字颜色(hex格式，如#505769)，默认#505769</param>
    /// <param name="bgColor">背景颜色(hex格式，如#F5F6F7)，默认#F5F6F7</param>
    /// <returns>PNG格式的二进制图片数据</returns>
    public byte[] GenerateFromFontFile(string text, string fontFilePath, float fontSize = DefaultFontSize,
        string textColor = "#505769", string bgColor = "#F5F6F7")
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(fontFilePath) || !File.Exists(fontFilePath))
            return Array.Empty<byte>();

        try
        {
            var collection = new FontCollection();
            collection.Add(fontFilePath);
            var fontFamily = collection.Families.FirstOrDefault();
            if (fontFamily == null)
                return Array.Empty<byte>();

            return GenerateWithFont(text, fontFamily, fontSize, textColor, bgColor);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    /// <summary>
    /// 从字体字节数据生成文字图片（使用内存中的字体数据）
    /// </summary>
    /// <param name="text">要生成的文字</param>
    /// <param name="fontBytes">字体字节数据</param>
    /// <param name="fontSize">字体大小，默认24</param>
    /// <param name="textColor">文字颜色(hex格式，如#505769)，默认#505769</param>
    /// <param name="bgColor">背景颜色(hex格式，如#F5F6F7)，默认#F5F6F7</param>
    /// <returns>PNG格式的二进制图片数据</returns>
    public byte[] GenerateFromFontBytes(string text, byte[] fontBytes, float fontSize = DefaultFontSize,
        string textColor = "#505769", string bgColor = "#F5F6F7")
    {
        if (string.IsNullOrEmpty(text) || fontBytes == null || fontBytes.Length == 0)
            return Array.Empty<byte>();

        try
        {
            // 将字节数据写入临时文件
            var tempFontDir = Path.Combine(Path.GetTempPath(), "BetterGI_Fonts");
            Directory.CreateDirectory(tempFontDir);
            var tempFontPath = Path.Combine(tempFontDir, $"custom_{Guid.NewGuid():N}.ttf");
            File.WriteAllBytes(tempFontPath, fontBytes);

            try
            {
                var collection = new FontCollection();
                collection.Add(tempFontPath);
                var fontFamily = collection.Families.FirstOrDefault();
                if (fontFamily == null)
                    return Array.Empty<byte>();

                return GenerateWithFont(text, fontFamily, fontSize, textColor, bgColor);
            }
            finally
            {
                // 延迟删除临时文件
                try { File.Delete(tempFontPath); } catch { }
            }
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    /// <summary>
    /// 从字体文件生成文字图片，返回OpenCvSharp.Mat
    /// </summary>
    public Mat GenerateMatFromFontFile(string text, string fontFilePath, float fontSize = DefaultFontSize,
        string textColor = "#505769", string bgColor = "#F5F6F7")
    {
        var pngData = GenerateFromFontFile(text, fontFilePath, fontSize, textColor, bgColor);
        if (pngData == null || pngData.Length == 0)
            return new Mat();

        try
        {
            return Mat.ImDecode(pngData, ImreadModes.Color);
        }
        catch
        {
            return new Mat();
        }
    }

    /// <summary>
    /// 从字体字节数据生成文字图片，返回OpenCvSharp.Mat
    /// </summary>
    public Mat GenerateMatFromFontBytes(string text, byte[] fontBytes, float fontSize = DefaultFontSize,
        string textColor = "#505769", string bgColor = "#F5F6F7")
    {
        var pngData = GenerateFromFontBytes(text, fontBytes, fontSize, textColor, bgColor);
        if (pngData == null || pngData.Length == 0)
            return new Mat();

        try
        {
            return Mat.ImDecode(pngData, ImreadModes.Color);
        }
        catch
        {
            return new Mat();
        }
    }

    private byte[] GenerateWithFont(string text, FontFamily fontFamily, float fontSize, string textColor, string bgColor)
    {
        var textColorRgb = ParseHexColor(textColor);
        var bgColorRgb = ParseHexColor(bgColor);

        var font = fontFamily.CreateFont(fontSize, FontStyle.Regular);

        var textWidth = MeasureTextWidth(text, font);
        var width = (int)Math.Ceiling(textWidth + DefaultPaddingX * 2 + 2);
        var height = (int)Math.Ceiling(fontSize + DefaultPaddingY * 2);

        using var img = new Image<Rgba32>(width, height);
        img.Mutate(ctx => ctx.Fill(bgColorRgb));

        var renderOptions = new RichTextOptions(font)
        {
            Origin = new PointF(DefaultPaddingX, (height - fontSize) / 2),
            Dpi = 72,
        };

        img.Mutate(ctx => ctx.DrawText(renderOptions, text, textColorRgb));

        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// 生成文字图片，返回OpenCvSharp.Mat用于模板匹配
    /// </summary>
    /// <param name="text">要生成的文字</param>
    /// <param name="fontName">字体名称，默认HYW</param>
    /// <param name="fontSize">字体大小，默认24</param>
    /// <param name="textColor">文字颜色(hex格式，如#505769)，默认#505769</param>
    /// <param name="bgColor">背景颜色(hex格式，如#F5F6F7)，默认#F5F6F7</param>
    /// <returns>OpenCvSharp.Mat图片</returns>
    public Mat GenerateMat(string text, string fontName = "HYW", float fontSize = DefaultFontSize,
        string textColor = "#505769", string bgColor = "#F5F6F7")
    {
        var pngData = Generate(text, fontName, fontSize, textColor, bgColor);
        if (pngData == null || pngData.Length == 0)
            return new Mat();

        try
        {
            return Mat.ImDecode(pngData, ImreadModes.Color);
        }
        catch
        {
            return new Mat();
        }
    }
}