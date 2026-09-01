# Packs a published win-x64 folder into an unsigned SlickDash.msix (Windows SDK makeappx).
param(
    [Parameter(Mandatory = $true)][string]$Payload,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$IdentityName = $env:STORE_IDENTITY_NAME,
    [string]$Publisher = $env:STORE_PUBLISHER
)

$ErrorActionPreference = "Stop"
if (-not $IdentityName) { $IdentityName = "SlickDash" }
if (-not $Publisher) { $Publisher = "CN=SlickDash" }

function Convert-ToMsixVersion([string]$tag) {
    $t = $tag.Trim()
    if ($t.StartsWith("v") -or $t.StartsWith("V")) { $t = $t.Substring(1) }
    if ($t -match '^(\d+)\.(\d+)\.(\d+)$') { return "$($Matches[1]).$($Matches[2]).$($Matches[3]).0" }
    if ($t -match '^(\d+)\.(\d+)\.(\d+)\.(\d+)$') { return $t }
    throw "Version '$tag' must look like 0.9.2 or v0.9.2"
}

$msixVersion = Convert-ToMsixVersion $Version
$root = Split-Path -Parent $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }
$packaging = $PSScriptRoot
$payloadIn = (Resolve-Path $Payload).Path
$outPath = $Out
if (-not [System.IO.Path]::IsPathRooted($outPath)) { $outPath = Join-Path (Get-Location) $outPath }
$outDir = Split-Path -Parent $outPath
if ($outDir) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("slickdash-msix-" + [guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Force -Path $stage | Out-Null
try {
    Copy-Item -Path (Join-Path $payloadIn "*") -Destination $stage -Recurse -Force
    Get-ChildItem $stage -Recurse -Include *.pdb, *.xml | Where-Object { $_.Name -ne "AppxManifest.xml" } | Remove-Item -Force

    $manifest = Get-Content -Raw (Join-Path $packaging "AppxManifest.xml")
    $manifest = $manifest -replace 'Name="SlickDash"', ('Name="' + $IdentityName + '"')
    $manifest = $manifest -replace 'Publisher="CN=SlickDash"', ('Publisher="' + $Publisher + '"')
    $manifest = $manifest -replace 'Version="0.0.0.0"', ('Version="' + $msixVersion + '"')
    Set-Content -Path (Join-Path $stage "AppxManifest.xml") -Value $manifest -Encoding utf8

    $imagesDest = Join-Path $stage "Images"
    New-Item -ItemType Directory -Force -Path $imagesDest | Out-Null
    Copy-Item (Join-Path $packaging "Images\*") $imagesDest -Force

    $exe = Join-Path $stage "GranTurismoTelemetry.exe"
    if (-not (Test-Path $exe)) { throw "Payload is missing GranTurismoTelemetry.exe" }

    $sdkBin = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        Select-Object -First 1
    if (-not $sdkBin) { throw "Windows SDK not found (need makeappx.exe on windows-latest)" }
    $makeappx = Join-Path $sdkBin.FullName "x64\makeappx.exe"
    $makepri = Join-Path $sdkBin.FullName "x64\makepri.exe"
    if (-not (Test-Path $makeappx)) { throw "makeappx.exe not at $makeappx" }

    if (Test-Path $makepri) {
        $priConfig = Join-Path $stage "priconfig.xml"
        & $makepri createconfig /cf $priConfig /dq en-US /o | Out-Host
        & $makepri new /pr $stage /cf $priConfig /of (Join-Path $stage "resources.pri") /o | Out-Host
        Remove-Item $priConfig -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path $outPath) { Remove-Item $outPath -Force }
    & $makeappx pack /d $stage /p $outPath /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit $LASTEXITCODE" }
    Write-Host "Packed $outPath (identity $IdentityName $msixVersion)"
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
