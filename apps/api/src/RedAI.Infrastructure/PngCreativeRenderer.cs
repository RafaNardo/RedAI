using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using RedAI.Application;
using StbImageSharp;

namespace RedAI.Infrastructure;

/// <summary>
/// Dependency-free 1080x1350 PNG renderer intended for demo and container environments.
/// Its layout is deterministic: image assets can affect only the background treatment,
/// while all copy and logo marks are rasterized here.
/// </summary>
public sealed class PngCreativeRenderer(IAssetStorage storage) : IDeterministicCreativeRenderer
{
    public async Task<RenderedCreative> RenderPngAsync(DeterministicCreativeRenderRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();
        RasterCanvas canvas;
        if (string.IsNullOrWhiteSpace(request.Layout.BackgroundAssetKey))
        {
            canvas = new RasterCanvas(1080, 1350, request.Layout.Palette.Background);
        }
        else
        {
            await using var background = await storage.OpenReadAsync(request.Layout.BackgroundAssetKey, cancellationToken);
            var decoded = ImageResult.FromStream(background, ColorComponents.RedGreenBlueAlpha);
            canvas = RasterCanvas.FromRgba(decoded.Data, decoded.Width, decoded.Height, 1080, 1350);
        }
        PaintTemplate(canvas, request.Layout);
        var png = canvas.ToPng();
        await using var input = new MemoryStream(png, writable: false);
        await storage.PutAsync(input, request.StorageKey, "image/png", cancellationToken);
        return new RenderedCreative(request.StorageKey, "image/png", png);
    }

    private static void PaintTemplate(RasterCanvas c, CreativeLayout l)
    {
        var primary = l.Palette.Primary; var accent = l.Palette.Accent;
        // BackgroundAssetKey is composited before this method. This method paints only
        // deterministic brand text and layout elements over the image.
        c.Rect(68, 68, 944, 1214, accent, 2);
        switch (l.Template)
        {
            case "editorial-bold": c.Text(l.Headline.Text, 96, 190, 14, primary, 850, false); break;
            case "minimal-center": c.Text(l.Headline.Text, 540, 440, 11, primary, 840, true); break;
            case "split-image": c.Rect(650, 0, 430, 1350, accent, 255); c.Text(l.Headline.Text, 96, 220, 11, primary, 480, false); break;
            case "statement": c.Text(l.Headline.Text, 540, 500, 15, primary, 850, true); break;
            case "educational": c.Text(l.Headline.Text, 96, 180, 10, primary, 850, false); c.Rect(96, 680, 888, 8, accent, 255); break;
            case "promotional": c.Rect(96, 155, 888, 370, accent, 32); c.Text(l.Headline.Text, 120, 230, 12, primary, 760, false); break;
        }
        if (!string.IsNullOrWhiteSpace(l.SupportingText)) c.Text(l.SupportingText!, l.Headline.Alignment == "center" ? 540 : 96, 820, 5, primary, 820, l.Headline.Alignment == "center");
        if (!string.IsNullOrWhiteSpace(l.Cta)) c.Text(l.Cta!, l.Headline.Alignment == "center" ? 540 : 96, 1120, 5, accent, 820, l.Headline.Alignment == "center");
        c.Text($"RED AI · {l.Logo.Position}", 96, 1230, 4, primary, 720, false);
    }
}

internal sealed class RasterCanvas
{
    private readonly byte[] _pixels;
    private readonly int _width, _height;
    public RasterCanvas(int width, int height, string background) { _width = width; _height = height; _pixels = new byte[width * height * 4]; Fill(background); }
    public static RasterCanvas FromRgba(byte[] source, int sourceWidth, int sourceHeight, int width, int height)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || source.Length < sourceWidth * sourceHeight * 4) throw new ArgumentException("Invalid RGBA background image.");
        var canvas = new RasterCanvas(width, height, "#000000");
        // Cover crop: a generated visual is used only as a background, never as a text layer.
        var scale = Math.Max((double)width / sourceWidth, (double)height / sourceHeight);
        var cropWidth = width / scale; var cropHeight = height / scale;
        var originX = (sourceWidth - cropWidth) / 2d; var originY = (sourceHeight - cropHeight) / 2d;
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var sx = Math.Clamp((int)(originX + x / scale), 0, sourceWidth - 1); var sy = Math.Clamp((int)(originY + y / scale), 0, sourceHeight - 1);
            var from = (sy * sourceWidth + sx) * 4; var to = (y * width + x) * 4;
            canvas._pixels[to] = source[from]; canvas._pixels[to + 1] = source[from + 1]; canvas._pixels[to + 2] = source[from + 2]; canvas._pixels[to + 3] = source[from + 3];
        }
        return canvas;
    }
    private void Fill(string color) { var (r, g, b) = Color(color); for (var i = 0; i < _pixels.Length; i += 4) { _pixels[i] = r; _pixels[i + 1] = g; _pixels[i + 2] = b; _pixels[i + 3] = 255; } }
    public void BackgroundTexture(string assetKey, string accent)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(assetKey)); var (r, g, b) = Color(accent);
        for (var y = 0; y < _height; y += 36) if ((hash[(y / 36) % hash.Length] & 1) == 1) Rect(0, y, _width, 18, $"#{r:X2}{g:X2}{b:X2}", 18);
    }
    public void Rect(int x, int y, int w, int h, string color, int alpha) { var (r, g, b) = Color(color); for (var yy = Math.Max(0, y); yy < Math.Min(_height, y + h); yy++) for (var xx = Math.Max(0, x); xx < Math.Min(_width, x + w); xx++) Blend(xx, yy, r, g, b, alpha); }
    public void Text(string value, int x, int y, int scale, string color, int maxWidth, bool center)
    {
        var lines = Wrap(value.ToUpperInvariant(), Math.Max(1, maxWidth / (6 * scale))); var cy = y;
        foreach (var line in lines) { var width = line.Length * 6 * scale; var cx = center ? x - width / 2 : x; foreach (var ch in line) { Glyph(ch, cx, cy, scale, color); cx += 6 * scale; } cy += 8 * scale; }
    }
    private void Glyph(char ch, int x, int y, int scale, string color)
    {
        var bits = Font.TryGetValue(ch, out var glyph) ? glyph : Font['?'];
        for (var row = 0; row < 7; row++) for (var col = 0; col < 5; col++) if ((bits[row] & (1 << (4 - col))) != 0) Rect(x + col * scale, y + row * scale, scale, scale, color, 255);
    }
    public byte[] ToPng()
    {
        using var result = new MemoryStream(); result.Write([137, 80, 78, 71, 13, 10, 26, 10]); Chunk(result, "IHDR", [.. Int32(_width), .. Int32(_height), 8, 6, 0, 0, 0]);
        using var raw = new MemoryStream(); for (var y = 0; y < _height; y++) { raw.WriteByte(0); raw.Write(_pixels, y * _width * 4, _width * 4); }
        using var compressed = new MemoryStream(); using (var zip = new ZLibStream(compressed, CompressionLevel.SmallestSize, true)) raw.WriteTo(zip); Chunk(result, "IDAT", compressed.ToArray()); Chunk(result, "IEND", []); return result.ToArray();
    }
    private static void Chunk(Stream stream, string type, byte[] data) { stream.Write(Int32(data.Length)); var name = Encoding.ASCII.GetBytes(type); stream.Write(name); stream.Write(data); var all = name.Concat(data).ToArray(); stream.Write(Int32(unchecked((int)Crc32(all)))); }
    private static byte[] Int32(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
    private void Blend(int x, int y, byte r, byte g, byte b, int a) { var p = (y * _width + x) * 4; _pixels[p] = (byte)((_pixels[p] * (255 - a) + r * a) / 255); _pixels[p + 1] = (byte)((_pixels[p + 1] * (255 - a) + g * a) / 255); _pixels[p + 2] = (byte)((_pixels[p + 2] * (255 - a) + b * a) / 255); _pixels[p + 3] = 255; }
    private static (byte r, byte g, byte b) Color(string hex) { hex = hex.Trim().TrimStart('#'); return hex.Length == 6 && byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) && byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) && byte.TryParse(hex[4..], System.Globalization.NumberStyles.HexNumber, null, out var b) ? (r, g, b) : ((byte)245, (byte)242, (byte)234); }
    private static string[] Wrap(string text, int columns) { var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries); var lines = new List<string>(); var current = ""; foreach (var word in words) { if (current.Length > 0 && current.Length + word.Length + 1 > columns) { lines.Add(current); current = word; } else current = current.Length == 0 ? word : $"{current} {word}"; } if (current.Length > 0) lines.Add(current); return lines.Count == 0 ? [""] : [.. lines]; }
    private static uint Crc32(byte[] bytes) { uint crc = 0xffffffff; foreach (var b in bytes) { crc ^= b; for (var i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320 : 0); } return ~crc; }
    private static readonly Dictionary<char, byte[]> Font = new()
    {
        ['A']=[14,17,17,31,17,17,17],['B']=[30,17,17,30,17,17,30],['C']=[14,17,16,16,16,17,14],['D']=[30,17,17,17,17,17,30],['E']=[31,16,16,30,16,16,31],['F']=[31,16,16,30,16,16,16],['G']=[14,17,16,23,17,17,14],['H']=[17,17,17,31,17,17,17],['I']=[31,4,4,4,4,4,31],['J']=[7,2,2,2,18,18,12],['K']=[17,18,20,24,20,18,17],['L']=[16,16,16,16,16,16,31],['M']=[17,27,21,17,17,17,17],['N']=[17,25,21,19,17,17,17],['O']=[14,17,17,17,17,17,14],['P']=[30,17,17,30,16,16,16],['Q']=[14,17,17,17,21,18,13],['R']=[30,17,17,30,20,18,17],['S']=[15,16,16,14,1,1,30],['T']=[31,4,4,4,4,4,4],['U']=[17,17,17,17,17,17,14],['V']=[17,17,17,17,17,10,4],['W']=[17,17,17,17,21,27,17],['X']=[17,17,10,4,10,17,17],['Y']=[17,17,10,4,4,4,4],['Z']=[31,1,2,4,8,16,31],['0']=[14,17,19,21,25,17,14],['1']=[4,12,4,4,4,4,14],['2']=[14,17,1,2,4,8,31],['3']=[30,1,1,14,1,1,30],['4']=[2,6,10,18,31,2,2],['5']=[31,16,16,30,1,1,30],['6']=[14,16,16,30,17,17,14],['7']=[31,1,2,4,8,8,8],['8']=[14,17,17,14,17,17,14],['9']=[14,17,17,15,1,1,14],[' ']=[0,0,0,0,0,0,0],['?']=[14,17,1,2,4,0,4],['!']=[4,4,4,4,4,0,4],['·']=[0,0,0,0,0,4,0],['-']=[0,0,0,31,0,0,0]
    };
}
