# Draws the Duejoro icon (a calendar page with a check mark on a deep orange tile; no letters) and writes src\Duejoro.App\Assets\Duejoro.ico and Duejoro.png.
# Original artwork: made entirely from shapes drawn here. Run: powershell -ExecutionPolicy Bypass -File scripts\make-icon.ps1
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\Duejoro.App\Assets\Duejoro.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

function RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, 2 * $r, 2 * $r, 180, 90); $p.AddArc($x + $w - 2 * $r, $y, 2 * $r, 2 * $r, 270, 90)
    $p.AddArc($x + $w - 2 * $r, $y + $h - 2 * $r, 2 * $r, 2 * $r, 0, 90); $p.AddArc($x, $y + $h - 2 * $r, 2 * $r, 2 * $r, 90, 90); $p.CloseFigure()
    return $p
}

function Draw([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
    $s = [single]$size
    # the tile: a deep orange that stays clear of the Tusivo site's teal
    $tile = RoundRect 0 0 $s $s ($s * 0.22)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new($s, $s)), ([System.Drawing.Color]::FromArgb(255, 154, 52, 18)), ([System.Drawing.Color]::FromArgb(255, 249, 115, 22))
    $g.FillPath($grad, $tile)
    # the calendar sheet
    $sheet = RoundRect ($s * 0.2) ($s * 0.24) ($s * 0.6) ($s * 0.58) ($s * 0.06)
    $g.FillPath([System.Drawing.Brushes]::White, $sheet)
    # the header band of the sheet
    $band = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = $s * 0.06
    $band.AddArc($s * 0.2, $s * 0.24, 2 * $r, 2 * $r, 180, 90); $band.AddArc($s * 0.8 - 2 * $r, $s * 0.24, 2 * $r, 2 * $r, 270, 90)
    $band.AddLine($s * 0.8, $s * 0.24 + $s * 0.14, $s * 0.2, $s * 0.24 + $s * 0.14); $band.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 124, 45, 18))), $band)
    # the two rings
    $ring = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([single]($s * 0.055))
    $ring.StartCap = 'Round'; $ring.EndCap = 'Round'
    foreach ($x in 0.34, 0.66) { $g.DrawLine($ring, $s * $x, $s * 0.15, $s * $x, $s * 0.31) }
    # the check mark
    $check = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 194, 65, 12)), ([single]($s * 0.085))
    $check.StartCap = 'Round'; $check.EndCap = 'Round'; $check.LineJoin = 'Round'
    $g.DrawLines($check, @([System.Drawing.PointF]::new($s * 0.35, $s * 0.58), [System.Drawing.PointF]::new($s * 0.46, $s * 0.69), [System.Drawing.PointF]::new($s * 0.66, $s * 0.46)))
    $g.Dispose()
    return $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = foreach ($n in $sizes) {
    $bmp = Draw $n; $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    , @($n, $ms.ToArray())
}
$w = New-Object System.IO.BinaryWriter ([System.IO.File]::Create($out))
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $n = $f[0]; $bytes = $f[1]
    $w.Write([byte]$(if ($n -ge 256) { 0 } else { $n })); $w.Write([byte]$(if ($n -ge 256) { 0 } else { $n }))
    $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$bytes.Length); $w.Write([uint32]$offset); $offset += $bytes.Length
}
foreach ($f in $frames) { $w.Write($f[1]) }
$w.Close()
# a crisp 256-pixel picture for the window itself (an .ico lets WPF pick its smallest frame)
$big = Draw 256; $big.Save((Join-Path (Split-Path $out) 'Duejoro.png'), [System.Drawing.Imaging.ImageFormat]::Png); $big.Dispose()
"wrote $out"
