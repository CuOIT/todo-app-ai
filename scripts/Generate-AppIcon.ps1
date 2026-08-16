param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\AiTaskTracker\Assets\AiTaskTracker.ico"),
    [string]$PackageAssetsDirectory = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$output = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($output)) | Out-Null

$bitmap = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$blue = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 47, 128, 237))
$border = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 88, 166, 255), 9)
$check = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 25)
$check.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$check.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$check.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

function Save-PackageAsset {
    param(
        [System.Drawing.Bitmap]$Source,
        [int]$CanvasWidth,
        [int]$CanvasHeight,
        [int]$LogoSize,
        [string]$Path
    )

    $canvas = [System.Drawing.Bitmap]::new(
        $CanvasWidth,
        $CanvasHeight,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $canvasGraphics = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $canvasGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $canvasGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $canvasGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $canvasGraphics.Clear([System.Drawing.Color]::Transparent)
        $left = [Math]::Floor(($CanvasWidth - $LogoSize) / 2)
        $top = [Math]::Floor(($CanvasHeight - $LogoSize) / 2)
        $canvasGraphics.DrawImage($Source, $left, $top, $LogoSize, $LogoSize)
        $canvas.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $canvasGraphics.Dispose()
        $canvas.Dispose()
    }
}

try {
    $graphics.FillEllipse($blue, 10, 10, 236, 236)
    $graphics.DrawEllipse($border, 14, 14, 228, 228)
    $graphics.DrawLines($check, [System.Drawing.Point[]]@(
        [System.Drawing.Point]::new(65, 132),
        [System.Drawing.Point]::new(109, 177),
        [System.Drawing.Point]::new(194, 87)
    ))

    if (-not [string]::IsNullOrWhiteSpace($PackageAssetsDirectory)) {
        $assetsDirectory = [System.IO.Path]::GetFullPath($PackageAssetsDirectory)
        [System.IO.Directory]::CreateDirectory($assetsDirectory) | Out-Null
        Save-PackageAsset $bitmap 44 44 40 (Join-Path $assetsDirectory "Square44x44Logo.png")
        Save-PackageAsset $bitmap 150 150 136 (Join-Path $assetsDirectory "Square150x150Logo.png")
        Save-PackageAsset $bitmap 50 50 46 (Join-Path $assetsDirectory "StoreLogo.png")
        Save-PackageAsset $bitmap 310 150 128 (Join-Path $assetsDirectory "Wide310x150Logo.png")
    }

    $rect = [System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height)
    $bitmapData = $bitmap.LockBits(
        $rect,
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $pixelBytes = [byte[]]::new($bitmapData.Stride * $bitmapData.Height)
        [System.Runtime.InteropServices.Marshal]::Copy(
            $bitmapData.Scan0,
            $pixelBytes,
            0,
            $pixelBytes.Length)
    }
    finally {
        $bitmap.UnlockBits($bitmapData)
    }

    $xorSize = 256 * 256 * 4
    $maskStride = 32
    $maskSize = $maskStride * 256
    $imageSize = 40 + $xorSize + $maskSize
    $stream = [System.IO.File]::Create($output)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]1)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$imageSize)
        $writer.Write([UInt32]22)

        $writer.Write([UInt32]40)
        $writer.Write([Int32]256)
        $writer.Write([Int32]512)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]$xorSize)
        $writer.Write([Int32]0)
        $writer.Write([Int32]0)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]0)

        for ($y = 255; $y -ge 0; $y--) {
            $writer.Write($pixelBytes, $y * $bitmapData.Stride, 256 * 4)
        }

        for ($y = 255; $y -ge 0; $y--) {
            $maskRow = [byte[]]::new($maskStride)
            for ($x = 0; $x -lt 256; $x++) {
                $alpha = $pixelBytes[($y * $bitmapData.Stride) + ($x * 4) + 3]
                if ($alpha -lt 128) {
                    $byteIndex = [Math]::Floor($x / 8)
                    $bit = 7 - ($x % 8)
                    $maskRow[$byteIndex] = [byte]($maskRow[$byteIndex] -bor (1 -shl $bit))
                }
            }
            $writer.Write($maskRow)
        }
    }
    finally {
        $writer.Dispose()
    }
}
finally {
    $check.Dispose()
    $border.Dispose()
    $blue.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Output $output
