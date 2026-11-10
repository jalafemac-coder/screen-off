<#
.SYNOPSIS
    Draws ScreenOff.ico: a monitor outline with a power glyph.
.DESCRIPTION
    Renders each icon size natively instead of downscaling one bitmap, so the
    small sizes stay legible, then packs the frames as uncompressed DIB entries.
    PNG-compressed entries are legal in an ICO but are not read reliably by
    System.Drawing.Icon or by the compiler's resource embedder, so plain DIB
    keeps the icon usable everywhere.
#>
[CmdletBinding()]
param(
    # Resolved in the body rather than here: $PSScriptRoot is not populated
    # while an advanced script's parameter defaults are evaluated, so a
    # Join-Path default fails outright under "powershell -File".
    [string] $OutputPath,

    # 256 is left out on purpose: an uncompressed 256x256 frame costs more than
    # the whole program, and the shell scales the 128 frame for that view.
    [int[]]  $Sizes = @(16, 24, 32, 48, 64, 128)
)

# Without this, a GDI+ failure mid-render would let the loop carry on and write
# an ICO whose directory entries no longer match its frames.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $OutputPath) {
    $OutputPath = Join-Path $root 'ScreenOff.ico'
}

# WriteAllBytes resolves a relative path against the process working directory
# rather than the shell's location, so the path is made absolute up front.
$OutputPath = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($OutputPath)

Add-Type -AssemblyName System.Drawing

$screenFill = [System.Drawing.Color]::FromArgb(255, 31, 36, 48)
$screenEdge = [System.Drawing.Color]::FromArgb(255, 140, 148, 166)
$glyphColor = [System.Drawing.Color]::FromArgb(255, 76, 141, 246)

function New-RoundedPath {
    param([float] $X, [float] $Y, [float] $Width, [float] $Height, [float] $Radius)

    $d = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $Width - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $Width - $d, $Y + $Height - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $Height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

# Returns the 32bpp bottom-up pixel rows for one frame, in DIB byte order.
function New-FramePixels {
    param([int] $Size)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [float] $Size
    $edgeWidth = [Math]::Max(1.0, $s * 0.055)

    $bodyX = $s * 0.075
    $bodyY = $s * 0.145
    $bodyW = $s * 0.850
    $bodyH = $s * 0.580
    $radius = [Math]::Max(1.5, $s * 0.085)

    $body = New-RoundedPath -X $bodyX -Y $bodyY -Width $bodyW -Height $bodyH -Radius $radius
    $fillBrush = New-Object System.Drawing.SolidBrush($screenFill)
    $edgePen = New-Object System.Drawing.Pen($screenEdge, $edgeWidth)
    $edgePen.Alignment = [System.Drawing.Drawing2D.PenAlignment]::Center
    $g.FillPath($fillBrush, $body)
    $g.DrawPath($edgePen, $body)

    $standBrush = New-Object System.Drawing.SolidBrush($screenEdge)
    $g.FillRectangle($standBrush, $s * 0.430, $s * 0.725, $s * 0.140, $s * 0.105)
    $baseH = [Math]::Max(1.0, $s * 0.070)
    $baseRect = New-RoundedPath -X ($s * 0.270) -Y ($s * 0.825) -Width ($s * 0.460) `
                                -Height $baseH -Radius ([Math]::Max(0.5, $baseH / 2))
    $g.FillPath($standBrush, $baseRect)

    # Power glyph inside the screen: a broken ring plus an upward stroke.
    $cx = $s * 0.500
    $cy = $bodyY + $bodyH * 0.470
    $r = $s * 0.150
    $glyphWidth = [Math]::Max(1.0, $s * 0.070)

    $glyphPen = New-Object System.Drawing.Pen($glyphColor, $glyphWidth)
    $glyphPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $glyphPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($glyphPen, $cx - $r, $cy - $r, $r * 2, $r * 2, -55, 290)
    $g.DrawLine($glyphPen, $cx, $cy - $r * 1.10, $cx, $cy - $r * 0.10)

    $g.Dispose()

    $rect = New-Object System.Drawing.Rectangle(0, 0, $Size, $Size)
    $locked = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $locked.Stride
    $raw = New-Object byte[] ($stride * $Size)
    [System.Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $raw, 0, $raw.Length)
    $bitmap.UnlockBits($locked)
    $bitmap.Dispose()

    foreach ($disposable in @($body, $fillBrush, $edgePen, $standBrush, $baseRect, $glyphPen)) {
        $disposable.Dispose()
    }

    # GDI+ hands back top-down rows; a DIB stores them bottom-up.
    $rowBytes = $Size * 4
    $pixels = New-Object byte[] ($rowBytes * $Size)
    for ($y = 0; $y -lt $Size; $y++) {
        [Array]::Copy($raw, $y * $stride, $pixels, ($Size - 1 - $y) * $rowBytes, $rowBytes)
    }

    return , $pixels
}

# One ICO frame: BITMAPINFOHEADER, the colour rows, then an empty AND mask.
function New-FrameDib {
    param([int] $Size, [byte[]] $Pixels)

    $maskRow = [int][Math]::Floor(($Size + 31) / 32) * 4
    $stream = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter($stream)

    $w.Write([uint32] 40)              # biSize
    $w.Write([int32] $Size)            # biWidth
    $w.Write([int32] ($Size * 2))      # biHeight: colour rows plus mask rows
    $w.Write([uint16] 1)               # biPlanes
    $w.Write([uint16] 32)              # biBitCount
    $w.Write([uint32] 0)               # biCompression: BI_RGB
    $w.Write([uint32] ($Pixels.Length + $maskRow * $Size))
    $w.Write([int32] 0)                # biXPelsPerMeter
    $w.Write([int32] 0)                # biYPelsPerMeter
    $w.Write([uint32] 0)               # biClrUsed
    $w.Write([uint32] 0)               # biClrImportant

    $w.Write($Pixels, 0, $Pixels.Length)

    # The alpha channel carries transparency, so the mask stays all zeros.
    $w.Write((New-Object byte[] ($maskRow * $Size)), 0, $maskRow * $Size)

    $w.Flush()
    $bytes = $stream.ToArray()
    $w.Dispose()
    $stream.Dispose()

    return , $bytes
}

$frames = @()
foreach ($size in $Sizes) {
    $pixels = New-FramePixels -Size $size
    $frames += [pscustomobject]@{ Size = $size; Data = (New-FrameDib -Size $size -Pixels $pixels) }
}

$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($out)

$writer.Write([uint16] 0)              # reserved
$writer.Write([uint16] 1)              # type: icon
$writer.Write([uint16] $frames.Count)

$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
    $dim = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
    $data = [byte[]] $frame.Data

    $writer.Write([byte] $dim)         # width
    $writer.Write([byte] $dim)         # height
    $writer.Write([byte] 0)            # palette entries
    $writer.Write([byte] 0)            # reserved
    $writer.Write([uint16] 1)          # colour planes
    $writer.Write([uint16] 32)         # bits per pixel
    $writer.Write([uint32] $data.Length)
    $writer.Write([uint32] $offset)
    $offset += $data.Length
}

foreach ($frame in $frames) {
    $data = [byte[]] $frame.Data
    $writer.Write($data, 0, $data.Length)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($OutputPath, $out.ToArray())
$writer.Dispose()
$out.Dispose()

Write-Host ("wrote {0} ({1} bytes, {2} frames)" -f $OutputPath,
    (Get-Item $OutputPath).Length, $frames.Count)
