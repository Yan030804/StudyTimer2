$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$sourcePath = Join-Path $PSScriptRoot "..\src\StudyTimer.App\Assets\study-timer-icon-source.png"
$pngPath = Join-Path $PSScriptRoot "..\src\StudyTimer.App\Assets\study-timer-icon.png"
$icoPath = Join-Path $PSScriptRoot "..\src\StudyTimer.App\Assets\study-timer.ico"
$previewPath = Join-Path $PSScriptRoot "..\artifacts\study-timer-icon-preview.png"

$source = [Drawing.Bitmap]::FromFile($sourcePath)
$transparent = [Drawing.Bitmap]::new($source.Width, $source.Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)

for ($y = 0; $y -lt $source.Height; $y++) {
    for ($x = 0; $x -lt $source.Width; $x++) {
        $pixel = $source.GetPixel($x, $y)
        $distance = [Math]::Sqrt(
            ($pixel.R * $pixel.R) +
            ((255 - $pixel.G) * (255 - $pixel.G)) +
            ($pixel.B * $pixel.B))

        if ($distance -le 18) {
            $alpha = 0
        } elseif ($distance -ge 125) {
            $alpha = 255
        } else {
            $alpha = [int](255 * (($distance - 18) / 107))
        }

        $despilledGreen = [Math]::Min([int]$pixel.G, [Math]::Max([int]$pixel.R, [int]$pixel.B) + 8)
        $transparent.SetPixel($x, $y, [Drawing.Color]::FromArgb($alpha, $pixel.R, $despilledGreen, $pixel.B))
    }
}

$source.Dispose()
$transparent.Save($pngPath, [Drawing.Imaging.ImageFormat]::Png)

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$pngStreams = [Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) {
    $bitmap = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.DrawImage($transparent, 0, 0, $size, $size)

    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $pngStreams.Add($stream.ToArray())
    $stream.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

$file = [IO.File]::Open($icoPath, [IO.FileMode]::Create, [IO.FileAccess]::Write)
$writer = [IO.BinaryWriter]::new($file)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
for ($index = 0; $index -lt $sizes.Count; $index++) {
    $size = $sizes[$index]
    $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
    $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$pngStreams[$index].Length)
    $writer.Write([uint32]$offset)
    $offset += $pngStreams[$index].Length
}

foreach ($bytes in $pngStreams) {
    $writer.Write($bytes)
}

$writer.Dispose()
$file.Dispose()

$preview = [Drawing.Bitmap]::new(512, 160, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
$previewGraphics = [Drawing.Graphics]::FromImage($preview)
$previewGraphics.Clear([Drawing.Color]::FromArgb(245, 247, 251))
$previewGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$previewSizes = @(16, 24, 32, 48, 64, 128)
$xPosition = 20
foreach ($size in $previewSizes) {
    $previewGraphics.DrawImage($transparent, $xPosition, 16, $size, $size)
    $xPosition += $size + 24
}
$preview.Save($previewPath, [Drawing.Imaging.ImageFormat]::Png)
$previewGraphics.Dispose()
$preview.Dispose()
$transparent.Dispose()

Write-Output "PNG=$pngPath"
Write-Output "ICO=$icoPath"
Write-Output "Preview=$previewPath"
