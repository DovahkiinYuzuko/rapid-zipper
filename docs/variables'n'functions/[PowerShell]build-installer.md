---
source_file: "rapid-zipper/build-installer.ps1"
language: "PowerShell"
description: "WiX Toolset を用いて MSI インストーラーと EXE セットアップブートストラッパーを一括ビルドする自動化スクリプトの仕様定義。"
tags:
  - "@Build"
  - "@Installer"
exports:
  - build-installer.ps1
imports:
  - "rapid-zipper/Product.wxs"
  - "rapid-zipper/Bundle.wxs"
---

# [PowerShell] build-installer 変数・関数仕様書

本ドキュメントは、[build-installer.ps1](file:///c:/Users/rikui/Documents/VSCode/%E3%83%A9%E3%83%94%E3%83%83%E3%83%89%E5%9C%A7%E7%B8%AE%E2%9C%A7%E5%B1%95%E9%96%8B/rapid-zipper/build-installer.ps1)におけるステップ定義と使用される主要な変数の役割を記述します。

## 1. 概要
アプリケーション本体のシングルファイルパブリッシュ（`dotnet publish`）と、WiX Toolset v5 によるセットアップウィザード付き MSI パッケージのコンパイル（`wix build`）を順次実行し、エラーチェックを自動で行う一括ビルド自動化スクリプトです。

---

## 2. 変数定義

### `$ErrorActionPreference`
- **行**: 1
- **型**: `string`
- **値**: `"Stop"`
- **役割**: スクリプト実行中にエラーが発生した場合、直ちにスクリプトの実行を停止（例外スロー）する PowerShell 共通変数。

### `$ScriptDir`
- **行**: 3
- **型**: `string`
- **値**: `Split-Path -Parent $MyInvocation.MyCommand.Definition`
- **役割**: 現在実行されている `build-installer.ps1` ファイル自体の存在する親ディレクトリの絶対パス。
- **動作**: 取得に成功した場合、カレントディレクトリをスクリプトの場所へ自動的に変更（`Set-Location`）し、相対パス参照が常に正しく解決されるようにする。

### `$DestDir`
- **行**: 13
- **型**: `string`
- **値**: `"bin\x64\Release"`
- **役割**: MSI インストーラーを出力するディレクトリの相対パス。
- **動作**: フォルダが存在しない場合、`New-Item -ItemType Directory` によって動的にディレクトリを新規作成する。

### `$MsiPath`
- **行**: 30
- **型**: `string`
- **値**: `Resolve-Path "$DestDir\rapid-zipper.msi"`
- **役割**: 生成されたインストーラー `rapid-zipper.msi` の最終的な絶対パス。

### `$ExePath`
- **行**: 31
- **型**: `string`
- **値**: `Resolve-Path "$DestDir\RapidZipperSetup.exe"`
- **役割**: 生成された EXE セットアップブートストラッパー `RapidZipperSetup.exe` の最終的な絶対パス。

### `$WixBalDll`
- **行**: 27
- **型**: `string`
- **役割**: WiX v5 の仕様/バグに対応するため、`WixToolset.Bal.wixext` の実体である `WixToolset.BootstrapperApplications.wixext.dll` のパスをローカルおよび GHA 上で動的に検索して格納する。

### `$WixBundleExitCode`
- **行**: 38
- **型**: `int`
- **役割**: bundle ビルド終了時のステータスコードを保持し、後続の例外制御で使用する。

---

## 3. 主要実行ステップ

### (Step 1) `dotnet publish`
- **行**: 9
- **動作**: `rapid-zipper.csproj` を Release ビルドでシングルファイル構成にて発行する。
- **パラメータ説明**:
  - `-c Release`: リリース構成でビルドする。
  - `-r win-x64`: Windows 64bit をターゲットプラットフォームにする。
  - `--self-contained true`: ターゲット環境に .NET ランタイムがなくても動くようにランタイムを内包する。
  - `-p:PublishSingleFile=true`: アプリケーションを単一の実行可能ファイル（exe）に統合する。
  - `-p:PublishReadyToRun=false`: ビルド速度を最適化するために ReadyToRun（事前コンパイル）をオフにする。
  - `-p:IncludeNativeLibrariesForSelfExtract=true`: 依存するネイティブライブラリを自己展開用に含める。
- **例外制御**: コマンド終了ステータス `$LASTEXITCODE` が `0` でない場合は、`throw` して中断する。

### (Step 2) `wix build`
- **行**: 19
- **動作**: `Product.wxs` をソースとして、インストーラー（MSI）をコンパイル・リンクする。
- **パラメータ説明**:
  - `-arch x64`: アーキテクチャを x64 に指定する。
  - `-culture ja-JP`: WixUI の文言やメッセージを日本語ローカライズ版でビルドする。
  - `-ext WixToolset.UI.wixext`: セットアップウィザード画面用の WiX 拡張をロードする。
  - `-ext WixToolset.Util.wixext`: インストール完了時のアプリ起動（WixShellExec）などのユーティリティアクションを有効化する。
  - `-o "$DestDir\rapid-zipper.msi"`: 指定した出力パスに msi を出力する。
- **例外制御**: `$LASTEXITCODE` が `0` でない場合は、`throw` して中断する。

### (Step 3 Bundle) `wix build`
- **行**: 24
- **動作**: `Bundle.wxs` をソースとして、MSIを内包する EXE セットアップブートストラッパー（`RapidZipperSetup.exe`）をコンパイル・リンクする。
- **パラメータ説明**:
  - `-arch x64`: アーキテクチャを x64 に指定する。
  - `-ext "$WixBalDll"`: 動的に解決した WiX Bal 拡張 DLL の絶対パスを指定し、標準のセットアップブートストラッパーUI（RTFライセンス画面）を使用するための WiX 拡張をロードする。
  - `-o "$DestDir\RapidZipperSetup.exe"`: 指定した出力パスに RapidZipperSetup.exe を出力する。
- **例外制御**: `$WixBundleExitCode` が `0` でない場合は、`throw` して中断する。

---

## 4. 依存関係マッピング (Dependency Mapping)

```mermaid
graph TD
    build_script --> dotnet_publish
    dotnet_publish --> rapid_zipper_exe
    build_script --> wix_build_msi
    rapid_zipper_exe --> wix_build_msi
    wix_build_msi --> rapid_zipper_msi
    build_script --> wix_build_bundle
    rapid_zipper_msi --> wix_build_bundle
    wix_build_bundle --> setup_exe
```
