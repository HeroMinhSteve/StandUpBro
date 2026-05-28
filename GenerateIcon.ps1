# GenerateIcon.ps1
# Creates a simple 32x32 icon for StandUpBro using System.Drawing.
# Run: powershell -ExecutionPolicy Bypass -File GenerateIcon.ps1

Add-Type -AssemblyName System.Drawing

$size = 32
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(108, 99, 255))  # #6C63FF

# Draw a white person silhouette (simple)
$brush = [System.Drawing.Brushes]::White
# Head
$g.FillEllipse($brush, 11, 2, 10, 10)
# Body
$g.FillRectangle($brush, 13, 12, 6, 12)
# Legs
$g.FillRectangle($brush, 13, 24, 3, 7)
$g.FillRectangle($brush, 16, 24, 3, 7)

$g.Dispose()

# Save as icon
$iconDir = Join-Path $PSScriptRoot "StandUpBro\Resources"
if (-not (Test-Path $iconDir)) { New-Item -ItemType Directory -Path $iconDir -Force | Out-Null }
$iconPath = Join-Path $iconDir "icon.ico"

# Convert bitmap to icon and save
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$fs = [System.IO.File]::Create($iconPath)
$icon.Save($fs)
$fs.Close()
$icon.Dispose()
$bmp.Dispose()

Write-Host "Icon created at: $iconPath"
