param([string]$TextureDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Mod/Textures/ZenithFrame/Frames/Debug/TestFrame'))
$ErrorActionPreference = 'Stop'

# Development masks only: paint each layer's dominant flat panel tone and keep outlines fixed.
# The original RGBA canvas is read pixel-for-pixel and is never changed.
Add-Type -ReferencedAssemblies @('System.Collections', 'System.Drawing.Common', 'System.Drawing.Primitives', 'System.Private.Windows.GdiPlus', 'System.Private.Windows.Core') -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class ZenithDebugMaskWriter
{
    public static void Write(string sourcePath)
    {
        string name = Path.GetFileNameWithoutExtension(sourcePath);
        string part = name.Split('_')[0];
        using (var source = new Bitmap(sourcePath))
        using (var mask = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            if (source.Width != 1024 || source.Height != 1024)
                throw new InvalidOperationException("Expected a 1024x1024 Frame canvas: " + name);
            var rect = new Rectangle(0, 0, source.Width, source.Height);
            var sourceData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var maskData = mask.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(sourceData.Stride);
                byte[] pixels = new byte[stride * source.Height];
                byte[] result = new byte[pixels.Length];
                Marshal.Copy(sourceData.Scan0, pixels, 0, pixels.Length);
                int top = source.Height, bottom = -1;
                var frequencies = new Dictionary<int, int>();
                for (int y = 0; y < source.Height; y++)
                    for (int x = 0; x < source.Width; x++)
                    {
                        int i = y * stride + x * 4;
                        if (pixels[i + 3] != 0)
                        {
                            top = Math.Min(top, y);
                            bottom = Math.Max(bottom, y);
                        }
                        if (pixels[i + 3] < 200) continue;
                        int rgb = pixels[i] | (pixels[i + 1] << 8) | (pixels[i + 2] << 16);
                        frequencies[rgb] = frequencies.TryGetValue(rgb, out int count) ? count + 1 : 1;
                    }
                if (bottom < top) throw new InvalidOperationException("Empty source: " + name);
                int dominant = 0, largest = 0;
                foreach (var entry in frequencies)
                    if (entry.Value > largest) { dominant = entry.Key; largest = entry.Value; }
                byte dominantB = (byte)dominant, dominantG = (byte)(dominant >> 8), dominantR = (byte)(dominant >> 16);
                for (int y = 0; y < source.Height; y++)
                {
                    float relativeY = (float)(y - top) / Math.Max(1, bottom - top);
                    for (int x = 0; x < source.Width; x++)
                    {
                        int i = y * stride + x * 4;
                        byte alpha = pixels[i + 3];
                        if (alpha == 0) continue;
                        result[i + 3] = alpha;
                        if (Math.Abs(pixels[i] - dominantB) > 12 ||
                            Math.Abs(pixels[i + 1] - dominantG) > 12 ||
                            Math.Abs(pixels[i + 2] - dominantR) > 12) continue;
                        bool secondary = part == "Helmet" ? relativeY < 0.20f :
                            part == "Torso" ? relativeY > 0.62f :
                            part == "Head" ? relativeY > 0.65f : relativeY > 0.55f;
                        result[i + (secondary ? 1 : 2)] = 255; // BGRA: green or red
                    }
                }
                Marshal.Copy(result, 0, maskData.Scan0, result.Length);
            }
            finally
            {
                source.UnlockBits(sourceData);
                mask.UnlockBits(maskData);
            }
            mask.Save(Path.Combine(Path.GetDirectoryName(sourcePath), name + "_m.png"), ImageFormat.Png);
        }
    }
}
'@

$sources = @(Get-ChildItem -LiteralPath $TextureDir -File -Filter '*.png' |
    Where-Object { $_.BaseName -match '^(Torso|Head|Helmet|Shoulder)_(South|Sides|North)$' })
if ($sources.Count -ne 12) { throw "Expected 12 source layers, found $($sources.Count)" }
foreach ($source in $sources) { [ZenithDebugMaskWriter]::Write($source.FullName) }
Write-Host "Generated $($sources.Count) aligned development livery masks"
