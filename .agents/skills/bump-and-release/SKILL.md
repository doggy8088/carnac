---
name: bump-and-release
description: >-
  執行 Carnac 新版本的版號標記與完整發佈流程，包含觸發 GitHub Release 建置、提交 WinGet (doggy8088.Carnac) PR，以及更新並推送 Chocolatey (carnac) 套件。當使用者要求發佈新版本、建立 Release、更新 WinGet 或發佈 Chocolatey 時使用此技能。
---

# Carnac 版本發佈流程 (`bump-and-release`)

本技能記錄 Carnac 從建立版本標籤到發佈至 **GitHub Releases**、**WinGet** 與 **Chocolatey** 的標準作業程序。

---

## 0. 前置檢查 (Pre-flight Checks)

1. **確認目標版號**：版號格式必須為 `X.Y.Z`（例如 `2.5.0`），對應的 Git tag 為 `vX.Y.Z`（例如 `v2.5.0`）。
2. **確認 Git 狀態**：
   - 確保目前位於 `dev` 分支，工作目錄乾淨，並與遠端 `origin/dev` 同步：
     ```powershell
     git status -sb
     git pull --ff-only origin dev
     ```
3. **確認工具與認證**：
   - 確認 `gh auth status` 已登入且具備 `doggy8088/carnac` 與提交 `microsoft/winget-pkgs` PR 的權限。

---

## 1. 建立並發佈 GitHub Release

GitHub Release 由 `.github/workflows/release.yml` 在推送 `v*.*.*` 標籤時自動觸發。該工作流程會自動將版號寫入 `AssemblyInfo.cs`、編譯 Release 組態、執行單元測試、透過 Inno Setup (`installer/Carnac.iss`) 打包安裝檔、建立可攜版壓縮檔 (`installer/New-PortableZip.ps1`)，並發佈至 GitHub Releases。

1. **建立 Annotated Tag 並推送至遠端**：
   ```powershell
   $version = "<Version>" # 例如 2.5.0
   git tag -a "v$version" -m "Carnac $version"
   git push origin "v$version"
   ```
2. **監控 `Release` 工作流程直到完成**：
   ```powershell
   Start-Sleep -Seconds 3
   $runId = gh run list --workflow=release.yml -L 1 --json databaseId --jq '.[0].databaseId'
   gh run watch $runId --exit-status --compact
   ```
3. **確認發佈資產**：
   - 確認 `https://github.com/doggy8088/carnac/releases/tag/v<Version>` 已產生以下檔案：
     - `Carnac-<Version>-Setup.exe`
     - `Carnac-<Version>-portable.zip`
     - `sha256sums.txt`
   - **注意**：若使用者需要先對 `Carnac-<Version>-Setup.exe` 進行手動程式碼簽章並替換 GitHub Release 上的檔案，請先等待替換完成後再執行步驟 2 與步驟 3，以確保安裝檔的 SHA256 雜湊值為最終版本。

---

## 2. 發佈至 WinGet (`doggy8088.Carnac`)

WinGet 套件 ID 為 `doggy8088.Carnac`，透過 `wingetcreate` 將新版本的 manifest PR 提交至 `microsoft/winget-pkgs`。

### 重要注意事項
1. **`wingetcreate` 版本檢查**：
   - 執行 `wingetcreate --version` 確認版本。若本機安裝的版本過舊（例如舊版 `0.1.0.1` 不支援新參數與 schema），請先下載最新版執行檔至暫存目錄使用：
     ```powershell
     $wc = Join-Path $env:TEMP 'wingetcreate.exe'
     Invoke-WebRequest -Uri 'https://aka.ms/wingetcreate/latest' -OutFile $wc -UseBasicParsing
     ```
2. **架構覆寫 (`|x64`)**：
   - Inno Setup 產生的 `Carnac-<Version>-Setup.exe` 預設會被 `wingetcreate` 偵測為 `x86`，但 `winget-pkgs` 上既有的 `doggy8088.Carnac.installer.yaml` 定義為 `Architecture: x64`。
   - 因此傳遞 `--urls` 參數時**必須在 URL 尾端加上 `|x64`**，否則會因無法配對既有 installer 節點而失敗（`Multiple matches found for X86 Inno installer`）。

### 執行步驟
1. **更新並提交 WinGet PR**：
   ```powershell
   $version = "<Version>"
   $token = gh auth token
   & $wc update doggy8088.Carnac `
     --version $version `
     --urls "https://github.com/doggy8088/carnac/releases/download/v$version/Carnac-$version-Setup.exe|x64" `
     --submit `
     --no-open `
     --token $token
   ```
2. **清理暫存的 `manifests` 目錄**：
   - `wingetcreate` 會在當前工作目錄產生 `manifests/d/doggy8088/Carnac/<Version>`，提交成功後請將其移除以保持儲存庫乾淨：
     ```powershell
     Remove-Item -Path .\manifests -Recurse -Force -ErrorAction SilentlyContinue
     ```
3. **記錄 PR 連結**：
   - 記錄終端機輸出的 `https://github.com/microsoft/winget-pkgs/pull/<PR_NUMBER>` 連結並回報給使用者。

---

## 3. 發佈至 Chocolatey (`carnac`)

Chocolatey 套件定義位於 `src/Chocolatey`，並透過 `.github/workflows/chocolatey.yml` 自動打包、驗證 SHA256、測試安裝與解除安裝，最後使用 `CHOCOLATEY_API_KEY` repository secret 推送至 `https://push.chocolatey.org/`。

### 執行步驟
1. **執行 `Update-Package.ps1` 更新版號與 SHA256**：
   - 建議使用 PowerShell 7 (`pwsh`) 執行，以避免舊版 Windows PowerShell 模組載入問題或寫入 UTF-8 BOM：
     ```powershell
     $version = "<Version>"
     pwsh -NoProfile -File .\src\Chocolatey\Update-Package.ps1 -Version $version
     ```
   - 此腳本會自動下載 `Carnac-<Version>-Setup.exe` 計算 SHA256，並更新：
     - `src/Chocolatey/carnac.nuspec`（`<version>` 與 `<releaseNotes>`）
     - `src/Chocolatey/tools/chocolateyinstall.ps1`（`url` 與 `checksum`）
2. **檢視差異、提交並推送至 `dev` 分支**：
   ```powershell
   git diff src/Chocolatey
   git add src/Chocolatey/carnac.nuspec src/Chocolatey/tools/chocolateyinstall.ps1
   git commit -m "chore(chocolatey): update package to $version"
   git push origin dev
   ```
3. **觸發 `Chocolatey` 工作流程進行正式推送**：
   - 推送至 `dev` 僅會執行打包與安裝驗證，不會推送到 Chocolatey 伺服器；必須透過 `workflow_dispatch` 且將 `push` 設為 `true` 才會正式發佈：
     ```powershell
     gh workflow run chocolatey.yml --ref dev -f push=true
     ```
4. **監控 `Chocolatey` 工作流程直到完成**：
   ```powershell
   Start-Sleep -Seconds 3
   $chocoRunId = gh run list --workflow=chocolatey.yml --event=workflow_dispatch -L 1 --json databaseId --jq '.[0].databaseId'
   gh run watch $chocoRunId --exit-status --compact
   ```
   - **故障排除**：若 `https://push.chocolatey.org/` 偶發回傳 `503 (Service Unavailable)` 導致推送步驟失敗，可直接重試失敗的工作階段：
     ```powershell
     gh run rerun $chocoRunId --failed
     ```

---

## 4. 發佈結果回報清單

完成上述所有步驟後，向使用者彙整回報以下資訊：
- **GitHub Release**：`https://github.com/doggy8088/carnac/releases/tag/v<Version>` 與安裝檔 SHA256
- **WinGet PR**：`https://github.com/microsoft/winget-pkgs/pull/<PR_NUMBER>`
- **Chocolatey 套件審核頁面**：`https://community.chocolatey.org/packages/carnac/<Version>`
