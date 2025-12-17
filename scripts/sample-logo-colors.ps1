$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$paths = @(
    'd:\Work\GitHub\Revela\assets\Spectara_Original.png',
    'd:\Work\GitHub\Revela\assets\Spectara.png'
)

function Get-Rgba {
    param(
        [Parameter(Mandatory)] [System.Drawing.Bitmap] $Bitmap,
        [Parameter(Mandatory)] [int] $X,
        [Parameter(Mandatory)] [int] $Y
    )

    $c = $Bitmap.GetPixel($X, $Y)
    return @($c.R, $c.G, $c.B, $c.A)
}

function Is-UsefulPixel {
    param([int[]] $Rgba)

    if ($Rgba[3] -lt 10) { return $false }   # transparent
    if (($Rgba[0] + $Rgba[1] + $Rgba[2]) -lt 30) { return $false } # near-black
    return $true
}

function To-Hex {
    param([int[]] $Rgba)
    '{0:X2}{1:X2}{2:X2}' -f $Rgba[0], $Rgba[1], $Rgba[2]
}

foreach ($path in $paths) {
    $bmp = [System.Drawing.Bitmap]::new($path)
    try {
        $w = $bmp.Width
        $h = $bmp.Height
        $cx = [int]($w / 2)
        $cy = [int]($h / 2)

        Write-Host "`n$([IO.Path]::GetFileName($path)) ${w}x${h}"

        # Sample along diagonal
        $samples = @()
        foreach ($i in 0..40) {
            $t = $i / 40.0
            $x = [int]($t * ($w - 1))
            $y = [int]($t * ($h - 1))
            $rgba = Get-Rgba -Bitmap $bmp -X $x -Y $y
            if (-not (Is-UsefulPixel $rgba)) { continue }
            $samples += ,@($t, $rgba)
        }

        $samples = $samples | Sort-Object { [double]$_[0] }

        function Pick([double] $p) {
            if ($samples.Count -eq 0) { return $null }
            $idx = [Math]::Min($samples.Count - 1, [Math]::Max(0, [int]($p * ($samples.Count - 1))))
            return $samples[$idx][1]
        }

        $c0 = Pick 0.0
        $c30 = Pick 0.3
        $c60 = Pick 0.6
        $c100 = Pick 1.0

        Write-Host ("diag picks: {0} | {1} | {2} | {3}" -f (To-Hex $c0), (To-Hex $c30), (To-Hex $c60), (To-Hex $c100))

        # Sample ring around center
        $radius = [int]([Math]::Min($w, $h) * 0.35)
        $ring = @()
        foreach ($deg in (0..345 | Where-Object { $_ % 15 -eq 0 })) {
            $rad = $deg * [Math]::PI / 180
            $x = [int]($cx + [Math]::Cos($rad) * $radius)
            $y = [int]($cy + [Math]::Sin($rad) * $radius)
            if ($x -lt 0 -or $x -ge $w -or $y -lt 0 -or $y -ge $h) { continue }
            $rgba = Get-Rgba -Bitmap $bmp -X $x -Y $y
            if (-not (Is-UsefulPixel $rgba)) { continue }
            $ring += ,@($deg, $rgba)
        }

        function PinkScore([int[]] $rgba) { return $rgba[0] + $rgba[2] - $rgba[1] }
        function BlueScore([int[]] $rgba) { return $rgba[2] - $rgba[0] }

        if ($ring.Count -gt 0) {
            $pink = $ring | Sort-Object { PinkScore $_[1] } -Descending | Select-Object -First 1
            $blue = $ring | Sort-Object { BlueScore $_[1] } -Descending | Select-Object -First 1

            Write-Host ("ring max pink: deg {0} hex {1}" -f $pink[0], (To-Hex $pink[1]))
            Write-Host ("ring max blue: deg {0} hex {1}" -f $blue[0], (To-Hex $blue[1]))
        }
    }
    finally {
        $bmp.Dispose()
    }
}
