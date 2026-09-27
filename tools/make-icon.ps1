# Draws the app icon (orange rounded square with a white usage gauge) and writes a
# multi-size .ico, by default to assets\app.ico (the copy the build embeds).
# Run it after changing the design here: build.ps1 -RegenerateIcon, or this script directly.
param([string]$OutFile = (Join-Path $PSScriptRoot '..\assets\app.ico'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$OutFile = [System.IO.Path]::GetFullPath($OutFile)
New-Item -ItemType Directory -Force (Split-Path $OutFile) | Out-Null

$accent = [System.Drawing.Color]::FromArgb(217, 119, 87)   # same orange as the tray badge
$sizes = 16, 20, 24, 32, 40, 48, 64, 256   # Windows scales 256 down for anything larger

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $d = 2 * $r
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Get-IconBitmap([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = 'AntiAlias'
        $g.PixelOffsetMode = 'HighQuality'
        $g.Clear([System.Drawing.Color]::Transparent)

        $path = New-RoundedRect 0 0 $s $s ($s * 0.22)
        $bg = New-Object System.Drawing.SolidBrush $accent
        $g.FillPath($bg, $path)

        # Gauge: a 270-degree track with about two thirds filled, opening at the bottom.
        $stroke = [Math]::Max(1.5, $s * 0.12)
        $inset = $s * 0.22
        $rect = New-Object System.Drawing.RectangleF $inset, $inset, ($s - 2 * $inset), ($s - 2 * $inset)
        $track = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(110, 255, 255, 255)), $stroke
        $fill = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $stroke
        foreach ($pen in $track, $fill) { $pen.StartCap = 'Round'; $pen.EndCap = 'Round' }
        $g.DrawArc($track, $rect, 135, 270)
        $g.DrawArc($fill, $rect, 135, 180)

        # Centre dot for the gauge hub (skipped where it would just be a smudge).
        if ($s -ge 32) {
            $dot = $s * 0.12
            $g.FillEllipse([System.Drawing.Brushes]::White, ($s - $dot) / 2, ($s - $dot) / 2, $dot, $dot)
        }

    }
    finally {
        $g.Dispose()
    }
    return $bmp
}

# 256px is stored as PNG (standard for that size); smaller sizes as classic 32-bit DIBs,
# which every Windows version and tool (including .NET's Icon class) can read.
function Get-IconImage([int]$s) {
    $bmp = Get-IconBitmap $s
    try {
        $ms = New-Object System.IO.MemoryStream
        if ($s -ge 256) {
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            return , $ms.ToArray()
        }

        $maskStride = [int][Math]::Floor(($s + 31) / 32) * 4
        $w = New-Object System.IO.BinaryWriter $ms
        # BITMAPINFOHEADER; height is doubled to cover the (unused) AND mask.
        $w.Write([UInt32]40); $w.Write([Int32]$s); $w.Write([Int32](2 * $s))
        $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]0)
        $w.Write([UInt32]($s * $s * 4 + $maskStride * $s))
        $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)

        # Pixels are BGRA, bottom-up.
        $data = $bmp.LockBits((New-Object System.Drawing.Rectangle 0, 0, $s, $s), 'ReadOnly',
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $row = New-Object byte[] ($s * 4)
            for ($y = $s - 1; $y -ge 0; $y--) {
                [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + $y * $data.Stride), $row, 0, $row.Length)
                $w.Write($row)
            }
        }
        finally { $bmp.UnlockBits($data) }

        $w.Write((New-Object byte[] ($maskStride * $s)))   # AND mask: all zero, alpha does the work
        $w.Flush()
        return , $ms.ToArray()
    }
    finally {
        $bmp.Dispose()
    }
}

# ICO container: 6-byte header, a 16-byte directory entry per image, then the image data.
$images = foreach ($s in $sizes) { , (Get-IconImage $s) }
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }   # 0 means 256
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$images[$i].Length); $w.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
[System.IO.File]::WriteAllBytes($OutFile, $out.ToArray())
