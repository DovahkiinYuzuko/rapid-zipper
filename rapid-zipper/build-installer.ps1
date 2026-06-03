$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ($ScriptDir) {
    Set-Location $ScriptDir
}

Write-Host "1. Starting dotnet publish (single file)..." -ForegroundColor Cyan
dotnet publish rapid-zipper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=false -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Write-Host "2. Building MSI installer using WiX Toolset..." -ForegroundColor Cyan
$DestDir = "bin\x64\Release"
if (-not (Test-Path $DestDir)) {
    New-Item -ItemType Directory -Path $DestDir | Out-Null
}

# 直接 wix build を実行 (-arch x64 を明示することで正常に動作)
wix build Product.wxs -arch x64 -o "$DestDir\rapid-zipper.msi"
if ($LASTEXITCODE -ne 0) { throw "wix build failed with exit code $LASTEXITCODE" }

Write-Host "3. Done!" -ForegroundColor Green
$MsiPath = Resolve-Path "$DestDir\rapid-zipper.msi"
Write-Host "MSI installer created at: $MsiPath" -ForegroundColor Green
