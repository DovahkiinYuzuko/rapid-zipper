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

# WixToolset.Bal.wixext の DLL パスを動的解決 (WiX v5 の拡張 DLL 名不一致バグ回避策)
$WixBalDll = "$env:USERPROFILE\.wix\extensions\WixToolset.Bal.wixext\5.0.2\wixext5\WixToolset.BootstrapperApplications.wixext.dll"
if (-not (Test-Path $WixBalDll)) {
    $WixBalDll = Get-ChildItem "$env:USERPROFILE\.wix\extensions\WixToolset.Bal.wixext\*\wixext5\WixToolset.BootstrapperApplications.wixext.dll" | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $WixBalDll) {
    throw "WixToolset.Bal.wixext (WixToolset.BootstrapperApplications.wixext.dll) not found."
}

# wix build を実行して RapidZipperSetup.exe を生成 (拡張機能の実体 DLL を直接指定)
wix build Bundle.wxs -arch x64 -ext "$WixBalDll" -o "$DestDir\RapidZipperSetup.exe"
$WixBundleExitCode = $LASTEXITCODE

# 一時コピーしたファイルを削除
if (Test-Path "rapid-zipper.msi") {
    Remove-Item "rapid-zipper.msi" -Force
}

if ($WixBundleExitCode -ne 0) { throw "wix build (bundle) failed with exit code $WixBundleExitCode" }

Write-Host "4. Done!" -ForegroundColor Green
$MsiPath = Resolve-Path "$DestDir\rapid-zipper.msi"
$ExePath = Resolve-Path "$DestDir\RapidZipperSetup.exe"
Write-Host "MSI installer created at: $MsiPath" -ForegroundColor Green
Write-Host "EXE installer created at: $ExePath" -ForegroundColor Green
