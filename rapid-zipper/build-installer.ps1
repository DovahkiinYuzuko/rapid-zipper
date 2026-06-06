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

# 直接 wix build を実行 (-arch x64 を明示し、日本語カルチャーと拡張機能を指定)
wix build Product.wxs -arch x64 -culture ja-JP -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -o "$DestDir\rapid-zipper.msi"
if ($LASTEXITCODE -ne 0) { throw "wix build failed with exit code $LASTEXITCODE" }

Write-Host "3. Building EXE Bootstrapper setup using WiX Toolset..." -ForegroundColor Cyan
# WiXの相対パスバグ回避のため、一時的にMSIファイルをカレントディレクトリにコピー
Copy-Item "$DestDir\rapid-zipper.msi" -Destination "rapid-zipper.msi" -Force

# wix build を実行して setup.exe を生成 (拡張機能 WixToolset.Bal.wixext を指定)
wix build Bundle.wxs -arch x64 -ext WixToolset.Bal.wixext -o "$DestDir\setup.exe"
$WixBundleExitCode = $LASTEXITCODE

# 一時コピーしたファイルを削除
if (Test-Path "rapid-zipper.msi") {
    Remove-Item "rapid-zipper.msi" -Force
}

if ($WixBundleExitCode -ne 0) { throw "wix build (bundle) failed with exit code $WixBundleExitCode" }

Write-Host "4. Done!" -ForegroundColor Green
$MsiPath = Resolve-Path "$DestDir\rapid-zipper.msi"
$ExePath = Resolve-Path "$DestDir\setup.exe"
Write-Host "MSI installer created at: $MsiPath" -ForegroundColor Green
Write-Host "EXE installer created at: $ExePath" -ForegroundColor Green
