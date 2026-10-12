param([string]$TextureDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Mod/Textures/ZenithFrame/Frames/Debug/TestFrame'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Explicit development-art coordinates on the existing shared 1024 canvas.
# Future chassis artists author these transparent overlays by hand.
$regions = @{
    Head = @{
        South = '422,207,180,24'
        Sides = '620,207,150,24'
        North = '422,207,180,24'
    }
    Helmet = @{
        South = '400,68,225,22'
        Sides = '615,65,165,24'
        North = '400,68,225,22'
    }
    Shoulder = @{
        South = '122,430,75,24;830,430,75,24'
        Sides = '348,425,75,24'
        North = '122,430,75,24;830,430,75,24'
    }
}

foreach ($part in @('Head', 'Helmet', 'Shoulder')) {
    foreach ($direction in @('South', 'Sides', 'North')) {
        $name = "${part}_${direction}"
        $source = [System.Drawing.Bitmap]::new((Join-Path $TextureDir "$name.png"))
        $overlay = [System.Drawing.Bitmap]::new(1024, 1024, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            if ($source.Width -ne 1024 -or $source.Height -ne 1024) { throw "Wrong source canvas: $name" }
            foreach ($region in $regions[$part][$direction].Split(';')) {
                $x0, $y0, $width, $height = @($region.Split(',') | ForEach-Object { [int]$_ })
                for ($y = $y0; $y -lt $y0 + $height; $y++) {
                    for ($x = $x0; $x -lt $x0 + $width; $x++) {
                        $alpha = $source.GetPixel($x, $y).A
                        if ($alpha -gt 0) {
                            $overlay.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, 255, 255, 255))
                        }
                    }
                }
            }
            $overlay.Save((Join-Path $TextureDir "${name}_v.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $source.Dispose(); $overlay.Dispose() }
    }
}
Write-Host 'Generated 9 explicitly placed Debug visor overlays on the unchanged 1024 canvas'
