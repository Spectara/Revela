<#
.SYNOPSIS
    Generates a test JPEG image with EXIF metadata using ExifTool.

.DESCRIPTION
    Creates a sample JPEG image with customizable dimensions and embedded EXIF data.
    Uses System.Drawing for image creation and ExifTool for proper EXIF embedding.

.PARAMETER OutPath
    Output path for the generated JPEG file.

.PARAMETER Width
    Image width in pixels (default: 1920).

.PARAMETER Height
    Image height in pixels (default: 1080).

.PARAMETER CameraMake
    Camera manufacturer (e.g., Canon, Sony, Nikon).

.PARAMETER CameraModel
    Camera model name.

.PARAMETER ISO
    ISO sensitivity value.

.PARAMETER FNumber
    Aperture f-number (e.g., 2.8, 5.6, 8).

.PARAMETER ExposureTime
    Shutter speed as fraction string (e.g., "1/125", "1/500").

.PARAMETER FocalLength
    Focal length in mm.

.EXAMPLE
    .\generate-test-image.ps1 -OutPath "test.jpg" -CameraMake Canon -CameraModel "EOS R5" -ISO 100

.EXAMPLE
    .\generate-test-image.ps1 -OutPath "portrait.jpg" -Width 1280 -Height 1920 -CameraMake Sony -CameraModel "A7 IV" -ISO 400
#>
param(
    [string]$OutPath = (Join-Path $PWD "sample.jpg"),
    [int]$Width = 1920,
    [int]$Height = 1080,

    # Camera EXIF data
    [string]$CameraMake  = "Canon",
    [string]$CameraModel = "EOS 5D Mark IV",
    [int]$ISO            = 400,
    [double]$FNumber     = 5.6,
    [string]$ExposureTime = "1/125",
    [int]$FocalLength    = 50,
    [switch]$FlashFired
)

# --- Check for ExifTool ---
$exiftool = Get-Command exiftool -ErrorAction SilentlyContinue
if (-not $exiftool) {
    Write-Error "ExifTool not found. Please install it: winget install exiftool"
    exit 1
}

# --- Load System.Drawing ---
function Ensure-SystemDrawing {
    try {
        Add-Type -AssemblyName System.Drawing -ErrorAction Stop
    } catch {
        try {
            Add-Type -AssemblyName System.Drawing.Common -ErrorAction Stop
        } catch {
            throw "System.Drawing could not be loaded. This script requires Windows with .NET System.Drawing."
        }
    }
}

Ensure-SystemDrawing

# --- Create sample image ---
$bmp = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$gfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

# Background gradient (varies by camera make for visual distinction)
$colorMap = @{
    "Canon" = @([System.Drawing.Color]::FromArgb(255, 220, 220), [System.Drawing.Color]::White)  # Reddish
    "Sony"  = @([System.Drawing.Color]::FromArgb(255, 200, 150), [System.Drawing.Color]::White)  # Orange
    "Nikon" = @([System.Drawing.Color]::FromArgb(255, 255, 200), [System.Drawing.Color]::White)  # Yellow
}
$colors = $colorMap[$CameraMake]
if (-not $colors) { $colors = @([System.Drawing.Color]::LightSteelBlue, [System.Drawing.Color]::White) }

$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.RectangleF(0, 0, $Width, $Height)),
    $colors[0],
    $colors[1],
    [System.Drawing.Drawing2D.LinearGradientMode]::Vertical
)
$gfx.FillRectangle($bgBrush, 0, 0, $Width, $Height)
$bgBrush.Dispose()

# Decorative elements
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(100, 80, 80, 80), 4)
$gfx.DrawEllipse($pen, 40, 40, [Math]::Min($Width, $Height) - 80, [Math]::Min($Width, $Height) - 80)
$pen.Dispose()

# Text overlay
$fontTitle = New-Object System.Drawing.Font("Segoe UI", 32, [System.Drawing.FontStyle]::Bold)
$fontSub   = New-Object System.Drawing.Font("Segoe UI", 14)
$brush     = [System.Drawing.Brushes]::DarkSlateGray

$gfx.DrawString("$CameraMake $CameraModel", $fontTitle, $brush, 60, 60)
$gfx.DrawString("ISO $ISO | f/$FNumber | $ExposureTime s | ${FocalLength}mm", $fontSub, $brush, 64, 110)
$gfx.DrawString("Test Image - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')", $fontSub, $brush, 64, 140)

$fontTitle.Dispose()
$fontSub.Dispose()
$gfx.Dispose()

# --- Save as JPEG (without EXIF - ExifTool will add it) ---
$jpegCodec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
    Where-Object { $_.MimeType -eq 'image/jpeg' }

$encParams = New-Object System.Drawing.Imaging.EncoderParameters 1
$qualityEncoder = [System.Drawing.Imaging.Encoder]::Quality
$encParams.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter($qualityEncoder, [int64]90)

# Create output directory if needed
$dir = Split-Path -Parent $OutPath
if ($dir -and -not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

$bmp.Save($OutPath, $jpegCodec, $encParams)
$bmp.Dispose()

# --- Add EXIF data with ExifTool ---
$exifDate = Get-Date -Format "yyyy:MM:dd HH:mm:ss"
$flash = if ($FlashFired) { 1 } else { 0 }

$exifArgs = @(
    "-overwrite_original"
    "-Make=$CameraMake"
    "-Model=$CameraModel"
    "-ISO=$ISO"
    "-FNumber=$FNumber"
    "-ExposureTime=$ExposureTime"
    "-FocalLength=$FocalLength"
    "-Flash=$flash"
    "-DateTimeOriginal=$exifDate"
    "-CreateDate=$exifDate"
    "-ModifyDate=$exifDate"
    "-Software=Revela Test Image Generator"
    "-ImageWidth=$Width"
    "-ImageHeight=$Height"
    $OutPath
)

& exiftool @exifArgs 2>&1 | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Warning "ExifTool returned exit code $LASTEXITCODE"
}

# --- Output ---
Write-Host "JPEG created: $OutPath"
Write-Host "Dimensions:   $Width x $Height"
Write-Host "EXIF:         $CameraMake / $CameraModel, ISO $ISO, f/$FNumber, $ExposureTime s, ${FocalLength}mm"
