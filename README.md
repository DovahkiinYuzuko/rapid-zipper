# ラピッド圧縮✧展開 (RapidZipper)

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET 10](https://img.shields.io/badge/.NET%2010-512BD4?style=for-the-badge&logo=.net&logoColor=white)
![Windows](https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![WiX Toolset v5](https://img.shields.io/badge/WiX%20Toolset%20v5-FF6F00?style=for-the-badge&logo=windows-terminal&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)

- **English**: A simple, secure, and portable drag-and-drop file compression/extraction utility with Windows Explorer context menu integration.
- **日本語**: Windows用の、シンプルで安全、かつポータブルなドラッグ＆ドロップおよび右クリックメニューから使える圧縮・展開ユーティリティ。

[日本語](#日本語) | [English](#english)

---

## 日本語

### 1. 対応フォーマット（対応拡張子）

| 処理 | 対応アーカイブ形式 |
|---|---|
| **圧縮 (Compression)** | ZIP, 7Z, TAR, TGZ (tar.gz) |
| **展開 (Extraction)** | ZIP, 7Z, RAR, TAR, GZ, TGZ |

---

### 2. アプリの使い方

#### A. ドラッグ＆ドロップによる操作
- **展開（解凍）**: アーカイブファイル（.zip, .7z など）をメインウィンドウにドロップすると、展開先フォルダ選択ダイアログが表示され、指定された場所に安全に解凍されます。
- **圧縮**: フォルダまたはファイルをウィンドウにドロップすると、現在選択されている圧縮フォーマットで圧縮アーカイブが作成されます。7Z フォーマットを選択している場合は、圧縮開始前に「低・普通・高」のレベル選択ダイアログが起動します。

#### B. 右クリック（コンテキスト）メニューによる操作
- ファイル、フォルダ、またはフォルダ内の背景を右クリック（Windows 11 では「その他のオプションを表示」を選択）し、**「ラピッド圧縮✧展開で処理 / Process with RapidZipper」** を選択します。
- アプリが起動して自動的に圧縮または展開が実行されます。処理が完了し、ダイアログを閉じるとアプリケーションは自動で終了するため、右クリックから手軽に処理を行えます。

---

### 3. 開発者向けのセットアップ・ビルド手順

#### 動作環境要件
- Windows 10 / 11 (x64)
- .NET 10.0 SDK
- WiX Toolset v5 CLI

#### リソース（7z.dll）の自動埋め込み仕様
本アプリは `7z.dll` をアセンブリ内に埋め込み（ポータブル動作）して、Unicode パスバグを回避するために起動時に特殊文字を含まない一時ディレクトリへ動的展開してロードする仕様になっています。

C# プロジェクトファイル (`rapid-zipper.csproj`) にて、NuGet パッケージの `7z.Libs` からビルド時に自動的に `7z.dll` が抽出されてアセンブリ内に埋め込まれるよう設定されているため、**手動で DLL を配置する作業は一切不要です**。

#### WiX v5 拡張機能の登録
インストーラー（MSI）で使用しているセットアップウィザードやユーティリティをビルドするために、事前に WiX CLI に以下の拡張機能パッケージを登録する必要があります。PowerShell 等で以下を実行してください。
```powershell
wix extension add WixToolset.UI.wixext/5.0.2
wix extension add WixToolset.Util.wixext/5.0.2
```

#### アプリケーションおよびインストーラーのビルド手順
同梱されている `build-installer.ps1` を実行することで、シングルファイルパブリッシュ（dotnet publish）から MSI インストーラーのパッケージング（wix build）までを自動で一括実行できます。
```powershell
cd rapid-zipper
.\build-installer.ps1
```
ビルドが成功すると、`rapid-zipper/bin/x64/Release/rapid-zipper.msi` にセットアップウィザード付きのインストーラーが生成されます。

---

### 4. 安全設計とセキュリティ対策
- **ロールバック安全策（安全置換）**: 既存フォルダを上書き展開する際、一時フォルダに展開を完了してから安全に元のフォルダと置換し、失敗時は元ファイルを保護する仕組みを導入しています。
- **重要フォルダ保護**: システムディレクトリやドライブのルートなど、誤削除や誤上書きが危険なパスへの書き込みを検知してブロックします。
- **Zip Slip 脆弱性対策**: 展開時にファイル名パスを正規化し、解凍先ディレクトリの外へのファイル書き出し（ディレクトリトラバーサル攻撃）をブロックします。
- **Zip Bomb 脆弱性対策**: 展開前にアーカイブ内のファイルサイズと数を検証し、上限（100GB、50万ファイル）を超える場合は処理を事前に中断してシステムリソースを守ります。
- **ジャンクション攻撃（VULN-001）防止**: 動的ロードする一時ディレクトリ名に予測不可能なGUIDを組み込むことで、競合状態を狙ったジャンクション攻撃を防いでいます。

---

### 5. ライセンス
- **アプリ本体**: MIT License (詳細は [LICENSE](LICENSE) を参照)
- **サードパーティライセンス**: LGPL v2.1 / v3 および MIT License (詳細は [NOTICE.md](NOTICE.md) を参照)
  - 7z.Libs (LGPL v2.1)
  - SharpCompress (MIT)
  - SevenZipSharp (LGPL v3)

---
---

## English

### 1. Supported Formats

| Action | Supported Archive Formats |
|---|---|
| **Compression** | ZIP, 7Z, TAR, TGZ (tar.gz) |
| **Extraction** | ZIP, 7Z, RAR, TAR, GZ, TGZ |

---

### 2. How to Use

#### A. Drag & Drop Operations
- **Extraction**: Drop any archive file (e.g., .zip, .7z) onto the application window. A folder browser dialog will appear for you to select the destination, and the files will be safely extracted there.
- **Compression**: Drop folders or files onto the window to pack them. They will be compressed into the format currently selected in the dropdown. For 7Z compression, a dialog prompt ("Low", "Normal", "High") will appear before starting.

#### B. Explorer Context Menu Operations
- Right-click any file, folder, or the background directory inside Explorer (For Windows 11, select "Show more options" first), and choose **"ラピッド圧縮✧展開で処理 / Process with RapidZipper"**.
- The app will launch and automatically execute the compression or extraction. Once finished and the dialog is closed, the app exits automatically.

---

### 3. Developer Setup & Build Instructions

#### Prerequisites
- Windows 10 / 11 (x64)
- .NET 10.0 SDK
- WiX Toolset v5 CLI

#### 7z.dll Automatic Resource Embedding
This application embeds `7z.dll` inside the assembly for standalone portability and unpacks it dynamically to a safe temp folder on startup to prevent Unicode path bugs.

In the C# project file (`rapid-zipper.csproj`), it is configured to automatically resolve and embed `7z.dll` from the NuGet package `7z.Libs` during the build process, so **no manual placement of DLLs is required**.

#### Resolving WiX v5 Extensions
To compile the setup wizard and utilities, register the required extension packages to the WiX CLI:
```powershell
wix extension add WixToolset.UI.wixext/5.0.2
wix extension add WixToolset.Util.wixext/5.0.2
```

#### Building the Application and MSI Installer
Run the automated script `build-installer.ps1` to publish the C# application as a standalone executable and package it into an MSI installer:
```powershell
cd rapid-zipper
.\build-installer.ps1
```
The output package will be generated at `rapid-zipper/bin/x64/Release/rapid-zipper.msi`.

---

### 4. Security & Safety Features
- **Rollback-Safe Replacement**: When overwriting directories during extraction, files are unpacked to a temporary location first and only swapped upon success. The original directory is protected if any error occurs.
- **System Directory Guard**: Blocks writes to root drives and system/user profiles (e.g., Windows directory, Desktop, Downloads).
- **Zip Slip Prevention**: Sanitizes extraction paths using `Path.GetFullPath` to block directory traversal attacks.
- **Zip Bomb Defense**: Scans archive headers before unpacking to enforce safety limits (100 GB max size, 500,000 max file count).
- **Junction Attack (VULN-001) Prevention**: Employs unpredictable GUID subfolders for dynamic loading to prevent malicious link redirections.

---

### 5. License
- **Application**: MIT License (See [LICENSE](LICENSE) for details)
- **Third-party Libraries**: LGPL v2.1 / v3 and MIT License (See [NOTICE.md](NOTICE.md) for details)
  - 7z.Libs (LGPL v2.1)
  - SharpCompress (MIT)
  - SevenZipSharp (LGPL v3)
