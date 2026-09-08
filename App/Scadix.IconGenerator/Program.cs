using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Scadix.IconGenerator;

/// <summary>
/// Converts a PNG/BMP image to a multi-size .ico file.
/// Also can generate the built-in Scadix Designer icon programmatically.
/// 
/// Usage:
///   dotnet run                          → generates built-in Scadix Designer icon
///   dotnet run -- input.png output.ico  → converts a PNG to ICO
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        if (args.Length >= 2)
        {
            // Mode: Convert input image to ICO
            string inputPath  = args[0];
            string outputPath = args[1];
            ConvertToIco(inputPath, outputPath);
        }
        else
        {
            // Mode: Generate built-in Scadix Designer icon
            string outputIco = args.Length == 1
                ? args[0]
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScadixDesigner.ico");

            string outputPng = Path.ChangeExtension(outputIco, ".png");

            GenerateScadixDesignerIcon(outputIco, outputPng);
        }
    }

    // ── Convert any PNG/BMP/JPG → ICO ────────────────────────────────────────

    static void ConvertToIco(string inputPath, string outputPath)
    {
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Input file not found: {inputPath}");
            return;
        }

        Console.WriteLine($"Converting: {inputPath}");

        int[] sizes = { 256, 128, 64, 48, 32, 16 };
        using var source = Image.FromFile(inputPath);

        var bitmaps = new Bitmap[sizes.Length];
        for (int i = 0; i < sizes.Length; i++)
        {
            int sz = sizes[i];
            var bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            g.DrawImage(source, 0, 0, sz, sz);
            bitmaps[i] = bmp;
        }

        SaveAsIco(bitmaps, outputPath);
        Console.WriteLine($"✅ ICO saved: {outputPath}");
    }

    // ── Generate the built-in Scadix Designer icon ────────────────────────────

    static void GenerateScadixDesignerIcon(string outputIco, string outputPng)
    {
        Console.WriteLine("Generating Scadix Designer icon...");

        int[] sizes = { 256, 128, 64, 48, 32, 16 };
        var bitmaps = new Bitmap[sizes.Length];

        for (int i = 0; i < sizes.Length; i++)
            bitmaps[i] = DrawDesignerIcon(sizes[i]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputIco)!);
        SaveAsIco(bitmaps, outputIco);
        Console.WriteLine($"✅ ICO saved: {outputIco}");

        // Also export a 512px PNG for use in Avalonia assets
        var big = DrawDesignerIcon(512);
        big.Save(outputPng, ImageFormat.Png);
        Console.WriteLine($"✅ PNG saved: {outputPng}");
    }

    // ── Draw the Scadix Designer icon at any size ─────────────────────────────

    static Bitmap DrawDesignerIcon(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode      = SmoothingMode.AntiAlias;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float s = size;

        // ── Background circle ──
        using var bgBrush = new LinearGradientBrush(
            new PointF(s * 0.1f, s * 0.1f), new PointF(s * 0.9f, s * 0.9f),
            Color.FromArgb(255, 30, 45, 110), Color.FromArgb(255, 6, 11, 26));
        using var bgPath = RoundedRect(1, 1, s - 2, s - 2, s / 2f);
        g.FillPath(bgBrush, bgPath);

        // ── Artboard window ──
        float wx = s * 0.14f, wy = s * 0.20f;
        float ww = s * 0.72f, wh = s * 0.55f;
        float cr = s * 0.03f;

        using var winBrush = new LinearGradientBrush(
            new PointF(wx, wy), new PointF(wx + ww, wy + wh),
            Color.FromArgb(255, 22, 32, 64), Color.FromArgb(255, 12, 22, 40));
        FillRoundedRect(g, winBrush, wx, wy, ww, wh, cr);

        // Title bar
        using var titleBrush = new SolidBrush(Color.FromArgb(255, 15, 25, 55));
        FillRoundedRect(g, titleBrush, wx, wy, ww, wh * 0.18f, cr);

        // Traffic light dots (only for sizes ≥ 32)
        if (size >= 32)
        {
            float dotR = s * 0.025f;
            float dotY = wy + wh * 0.09f;
            g.FillEllipse(new SolidBrush(Color.FromArgb(255, 255, 95,  87)), wx + ww * 0.08f - dotR, dotY - dotR, dotR * 2, dotR * 2);
            g.FillEllipse(new SolidBrush(Color.FromArgb(255, 254, 188, 46)), wx + ww * 0.17f - dotR, dotY - dotR, dotR * 2, dotR * 2);
            g.FillEllipse(new SolidBrush(Color.FromArgb(255,  40, 200, 64)), wx + ww * 0.26f - dotR, dotY - dotR, dotR * 2, dotR * 2);
        }

        // ── Header bar (purple gradient) ──
        float headerY = wy + wh * 0.22f;
        float headerH = wh * 0.14f;
        using var headerBrush = new LinearGradientBrush(
            new PointF(wx + ww * 0.08f, headerY), new PointF(wx + ww * 0.92f, headerY + headerH),
            Color.FromArgb(220, 79, 70, 229), Color.FromArgb(220, 124, 58, 237));
        FillRoundedRect(g, headerBrush, wx + ww * 0.08f, headerY, ww * 0.84f, headerH, cr * 0.5f);

        // ── Cyan content block ──
        float cyanX = wx + ww * 0.08f;
        float cyanY = wy + wh * 0.42f;
        float cyanW = ww * 0.40f;
        float cyanH = wh * 0.40f;
        using var cyanBrush = new LinearGradientBrush(
            new PointF(cyanX, cyanY), new PointF(cyanX + cyanW, cyanY + cyanH),
            Color.FromArgb(200, 0, 212, 255), Color.FromArgb(200, 0, 144, 192));
        FillRoundedRect(g, cyanBrush, cyanX, cyanY, cyanW, cyanH, cr * 0.5f);

        // ── Purple block ──
        float purpX = cyanX + cyanW + ww * 0.06f;
        using var purpBrush = new LinearGradientBrush(
            new PointF(purpX, cyanY), new PointF(purpX + ww * 0.36f, cyanY + cyanH),
            Color.FromArgb(200, 139, 92, 246), Color.FromArgb(200, 109, 40, 217));
        FillRoundedRect(g, purpBrush, purpX, cyanY, ww * 0.36f, cyanH, cr * 0.5f);

        // ── Selection handles (around cyan block) ──
        if (size >= 32)
        {
            float pad      = s * 0.012f;
            float penWidth = Math.Max(1f, s * 0.006f);
            using var selPen = new Pen(Color.FromArgb(220, 0, 212, 255), penWidth);
            selPen.DashStyle = DashStyle.Dash;
            g.DrawRectangle(selPen, cyanX - pad, cyanY - pad, cyanW + pad * 2, cyanH + pad * 2);

            float hSz = s * 0.025f;
            using var handleBrush = new SolidBrush(Color.FromArgb(255, 0, 212, 255));
            DrawHandle(g, handleBrush, cyanX - pad - hSz / 2,        cyanY - pad - hSz / 2,        hSz);
            DrawHandle(g, handleBrush, cyanX + cyanW + pad - hSz / 2, cyanY - pad - hSz / 2,       hSz);
            DrawHandle(g, handleBrush, cyanX - pad - hSz / 2,        cyanY + cyanH + pad - hSz / 2, hSz);
            DrawHandle(g, handleBrush, cyanX + cyanW + pad - hSz / 2, cyanY + cyanH + pad - hSz / 2, hSz);
        }

        // ── Cursor arrow ──
        if (size >= 32)
            DrawCursor(g, cyanX + cyanW - s * 0.02f, cyanY + cyanH - s * 0.04f, s * 0.12f);

        // ── Bottom cards (size ≥ 48) ──
        if (size >= 48)
        {
            float cardY  = wy + wh * 0.86f;
            float cardH2 = wh * 0.10f;
            var colors = new[] {
                Color.FromArgb(180, 30,  64, 175),
                Color.FromArgb(180, 139, 92, 246),
                Color.FromArgb(180, 14, 165, 233)
            };
            for (int ci = 0; ci < 3; ci++)
            {
                float cardX = wx + ww * 0.08f + ci * (ww * 0.30f + ww * 0.02f);
                using var cardBrush = new SolidBrush(colors[ci]);
                FillRoundedRect(g, cardBrush, cardX, cardY, ww * 0.28f, cardH2, cr * 0.3f);
            }
        }

        // ── Outer ring ──
        using var ringPen = new Pen(Color.FromArgb(50, 0, 212, 255), Math.Max(1f, s * 0.004f));
        g.DrawEllipse(ringPen, 2, 2, s - 4, s - 4);

        return bmp;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void FillRoundedRect(Graphics g, Brush brush, float x, float y, float w, float h, float r)
    {
        using var path = RoundedRect(x, y, w, h, r);
        g.FillPath(brush, path);
    }

    static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath();
        path.AddArc(x,             y,             r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y,             r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2,   0, 90);
        path.AddArc(x,             y + h - r * 2, r * 2, r * 2,  90, 90);
        path.CloseAllFigures();
        return path;
    }

    static void DrawHandle(Graphics g, Brush brush, float x, float y, float sz)
    {
        float r = sz * 0.3f;
        FillRoundedRect(g, brush, x, y, sz, sz, r);
    }

    static void DrawCursor(Graphics g, float x, float y, float s)
    {
        var pts = new PointF[]
        {
            new(x,              y),
            new(x,              y + s),
            new(x + s * 0.30f, y + s * 0.70f),
            new(x + s * 0.50f, y + s * 1.10f),
            new(x + s * 0.65f, y + s * 1.05f),
            new(x + s * 0.45f, y + s * 0.65f),
            new(x + s * 0.75f, y + s * 0.65f)
        };
        using var whiteBrush = new SolidBrush(Color.White);
        using var borderPen  = new Pen(Color.FromArgb(180, 79, 70, 229), Math.Max(1, s * 0.08f));
        g.FillPolygon(whiteBrush, pts);
        g.DrawPolygon(borderPen, pts);
    }

    // ── ICO file writer ───────────────────────────────────────────────────────

    static void SaveAsIco(Bitmap[] bitmaps, string path)
    {
        using var stream = new FileStream(path, FileMode.Create);
        using var writer = new BinaryWriter(stream);

        // ICONDIR header
        writer.Write((short)0);               // Reserved
        writer.Write((short)1);               // Type: ICO
        writer.Write((short)bitmaps.Length);  // Count

        // Encode each frame as PNG
        var frames = new MemoryStream[bitmaps.Length];
        int offset = 6 + bitmaps.Length * 16; // header + directory entries

        for (int i = 0; i < bitmaps.Length; i++)
        {
            var ms = new MemoryStream();
            bitmaps[i].Save(ms, ImageFormat.Png);
            frames[i] = ms;

            int sz = bitmaps[i].Width <= 255 ? bitmaps[i].Width : 0;
            writer.Write((byte)sz);    // Width  (0 = 256)
            writer.Write((byte)sz);    // Height (0 = 256)
            writer.Write((byte)0);     // Color count
            writer.Write((byte)0);     // Reserved
            writer.Write((short)1);    // Planes
            writer.Write((short)32);   // Bit depth
            writer.Write((int)ms.Length);
            writer.Write(offset);
            offset += (int)ms.Length;
        }

        // Write PNG data
        foreach (var ms in frames)
        {
            ms.Position = 0;
            ms.CopyTo(stream);
            ms.Dispose();
        }
    }
}
