param([Parameter(Mandatory = $true)][string]$OutputRoot)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Original geometric sprites for the distributable build. No source pixels from UF Heavy Industries.
function New-Canvas([int]$size) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    return @{ Bitmap = $bitmap; Graphics = $graphics }
}
function Color([int]$a, [int]$r, [int]$g, [int]$b) {
    return [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}
function Point([float]$x, [float]$y) { return [System.Drawing.PointF]::new($x, $y) }
function Fill-Polygon($graphics, $color, $points) {
    $brush = [System.Drawing.SolidBrush]::new($color)
    try { $graphics.FillPolygon($brush, [System.Drawing.PointF[]]$points) }
    finally { $brush.Dispose() }
}
function Fill-Ellipse($graphics, $color, [float]$x, [float]$y, [float]$w, [float]$h) {
    $brush = [System.Drawing.SolidBrush]::new($color)
    try { $graphics.FillEllipse($brush, $x, $y, $w, $h) }
    finally { $brush.Dispose() }
}
function Stroke($graphics, $color, [float]$width, [float]$x1, [float]$y1, [float]$x2, [float]$y2) {
    $pen = [System.Drawing.Pen]::new($color, $width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    try { $graphics.DrawLine($pen, $x1, $y1, $x2, $y2) }
    finally { $pen.Dispose() }
}
function Save-Canvas($canvas, [string]$relative) {
    $path = Join-Path $OutputRoot $relative
    [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($path))
    try { $canvas.Bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png) }
    finally { $canvas.Graphics.Dispose(); $canvas.Bitmap.Dispose() }
}

$c = New-Canvas 256
$g = $c.Graphics
Fill-Polygon $g (Color 160 35 200 235) @((Point 98 170),(Point 114 235),(Point 128 210),(Point 142 235),(Point 158 170))
Fill-Polygon $g (Color 255 27 39 48) @((Point 128 20),(Point 151 62),(Point 158 171),(Point 143 196),(Point 113 196),(Point 98 171),(Point 105 62))
Fill-Polygon $g (Color 255 91 111 124) @((Point 128 27),(Point 146 65),(Point 147 170),(Point 135 181),(Point 121 181),(Point 109 170),(Point 110 65))
Fill-Polygon $g (Color 255 198 218 220) @((Point 128 24),(Point 143 64),(Point 113 64))
Fill-Polygon $g (Color 255 29 39 50) @((Point 110 130),(Point 73 165),(Point 110 168))
Fill-Polygon $g (Color 255 29 39 50) @((Point 146 130),(Point 183 165),(Point 146 168))
Stroke $g (Color 255 25 225 238) 8 128 85 128 172
Fill-Ellipse $g (Color 255 233 172 57) 119 180 18 19
Save-Canvas $c 'Textures/Things/Projectile/ChimeraMissile.png'

$c = New-Canvas 256; $g = $c.Graphics
Fill-Polygon $g (Color 255 28 35 43) @((Point 128 20),(Point 157 59),(Point 157 199),(Point 142 229),(Point 114 229),(Point 99 199),(Point 99 59))
Fill-Polygon $g (Color 255 105 114 125) @((Point 128 29),(Point 149 66),(Point 149 193),(Point 138 216),(Point 118 216),(Point 107 193),(Point 107 66))
Stroke $g (Color 255 230 178 77) 9 112 98 144 98
Stroke $g (Color 255 230 178 77) 9 112 171 144 171
Fill-Polygon $g (Color 255 222 225 222) @((Point 128 29),(Point 148 67),(Point 108 67))
Save-Canvas $c 'Textures/Things/Projectile/ChimeraCannonShell.png'

$c = New-Canvas 256; $g = $c.Graphics
Fill-Ellipse $g (Color 100 0 208 240) 75 23 106 210
Fill-Polygon $g (Color 255 209 252 255) @((Point 128 12),(Point 149 102),(Point 140 212),(Point 116 212),(Point 107 102))
Fill-Polygon $g (Color 255 20 175 219) @((Point 128 34),(Point 138 105),(Point 133 199),(Point 123 199),(Point 118 105))
Stroke $g (Color 200 35 216 250) 7 78 218 105 184
Stroke $g (Color 200 35 216 250) 7 178 218 151 184
Save-Canvas $c 'Textures/Things/Projectile/ChimeraRailSlug.png'

$c = New-Canvas 128; $g = $c.Graphics
Fill-Ellipse $g (Color 170 24 180 215) 12 12 104 104
Fill-Ellipse $g (Color 255 15 29 39) 19 19 90 90
foreach ($x in @(44, 64, 84)) {
    Fill-Polygon $g (Color 255 201 213 214) @((Point $x 30),(Point ($x+10) 52),(Point ($x+8) 84),(Point ($x-8) 84),(Point ($x-10) 52))
    Stroke $g (Color 255 35 221 239) 4 $x 52 $x 77
}
Save-Canvas $c 'Textures/UI/ChimeraMissileSalvo.png'

foreach ($frame in 1..10) {
    $c = New-Canvas 1000; $g = $c.Graphics
    $fade = 1.0 - (($frame - 1) / 10.0)
    $radius = [float](70 + $frame * 29)
    $outer = [int](110 * $fade)
    $inner = [int](240 * $fade)
    Fill-Ellipse $g (Color $outer 255 106 27) (500-$radius) (500-$radius) ($radius*2) ($radius*2)
    Fill-Ellipse $g (Color $inner 255 190 55) (500-$radius*0.48) (500-$radius*0.48) ($radius*0.96) ($radius*0.96)
    Fill-Ellipse $g (Color ([int](255*$fade)) 255 245 196) (500-$radius*0.22) (500-$radius*0.22) ($radius*0.44) ($radius*0.44)
    foreach ($ray in 0..7) {
        $angle = $ray * [math]::PI / 4
        $length = $radius * (1.4 + (($ray % 3) * 0.12))
        Stroke $g (Color ([int](200*$fade)) 255 163 47) ([float](22*$fade+4)) 500 500 ([float](500+[math]::Cos($angle)*$length)) ([float](500+[math]::Sin($angle)*$length))
    }
    Save-Canvas $c ('Textures/Things/Mote/ChimeraCannonFlash{0:00}.png' -f $frame)
}
Write-Output 'Generated 14 original geometric Chimera PNG assets.'
