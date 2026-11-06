# Records and encodes the SHIFTLING trailer.
#   1. builds the Windows player with the Unity CLI (skip with -NoBuild)
#   2. runs it in trailer mode, which writes frames + soundtrack.wav to Trailer/
#   3. encodes Trailer/Shiftling-Trailer.mp4 with ffmpeg
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$unity = "$env:LOCALAPPDATA\Unity\bin\unity.exe"
$exe = Join-Path $root 'Builds\Windows\Shiftling.exe'
$out = Join-Path $root 'Trailer'

if (-not $NoBuild) {
    & $unity build . --target StandaloneWindows64 --execute-method BuildScript.BuildWindows `
        --output-path Builds/Windows/Shiftling.exe --allow-dirty-build --no-tail --non-interactive --no-banner -l Logs/build-win.log
    if ($LASTEXITCODE -ne 0) { throw "Unity build failed (see Logs/build-win.log)" }
}

New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item "$out\*.jpg", "$out\soundtrack.wav" -ErrorAction SilentlyContinue
$p = Start-Process $exe -ArgumentList @('-trailer', $out, '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0', '-logFile', "$root\Logs\trailer-player.log") -PassThru
if (-not $p.WaitForExit(15 * 60 * 1000)) { $p.Kill(); throw "Trailer recording timed out" }
if (-not (Test-Path "$out\soundtrack.wav")) { throw "Recording did not finish (see Logs/trailer-player.log)" }

ffmpeg -y -loglevel error -framerate 30 -i "$out\f%05d.jpg" -i "$out\soundtrack.wav" `
    -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k -shortest -movflags +faststart `
    "$out\Shiftling-Trailer.mp4"
Write-Host "Trailer written to $out\Shiftling-Trailer.mp4"
