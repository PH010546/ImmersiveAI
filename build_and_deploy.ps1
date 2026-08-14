<#
.SYNOPSIS
    Immersive AI 一鍵編譯與自動部署腳本
.DESCRIPTION
    1. 自動識別 .NET SDK 8.0
    2. 編譯 ImmersiveAI 專案 (Release 模式)
    3. 自動將產出的 DLL、Harmony、SubModule.xml、GUI 複製到騎砍 Modules 目錄
#>

$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot
$GameModuleDir = "C:\Game\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ImmersiveAI"
$BinTargetDir = Join-Path $GameModuleDir "bin\Win64_Shipping_Client"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       Immersive AI 一鍵編譯與自動部署至遊戲目錄" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. 檢查 dotnet 指令
$userDotnet = "$env:USERPROFILE\.dotnet"
$dotnetPath = ""

if (Test-Path "$userDotnet\dotnet.exe") {
    $env:PATH = "$userDotnet;" + $env:PATH
    $env:DOTNET_ROOT = $userDotnet
    $dotnetPath = "$userDotnet\dotnet.exe"
} elseif (Test-Path "C:\Program Files\dotnet\dotnet.exe") {
    $dotnetPath = "C:\Program Files\dotnet\dotnet.exe"
    $env:PATH = "C:\Program Files\dotnet;" + $env:PATH
} else {
    $cmd = Get-Command "dotnet" -ErrorAction SilentlyContinue
    if ($cmd) { $dotnetPath = $cmd.Source }
}

if (-not $dotnetPath) {
    Write-Error "找不到 dotnet SDK！請確認 .NET SDK 是否已安裝。"
    return
}

Write-Host "[1/3] 使用 dotnet ($dotnetPath) 開始編譯專案 (Release 模式)..." -ForegroundColor Yellow

Push-Location $ScriptDir
try {
    & $dotnetPath build -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "編譯失敗！請檢查上述錯誤訊息。"
        return
    }
} finally {
    Pop-Location
}

Write-Host "`n[2/3] 檢查目標遊戲模組目錄..." -ForegroundColor Yellow

if (-not (Test-Path $BinTargetDir)) {
    New-Item -ItemType Directory -Force -Path $BinTargetDir | Out-Null
}

# 2. 複製產出檔案
Write-Host "`n[3/3] 部署模組檔案至 $GameModuleDir ..." -ForegroundColor Yellow

$releaseBin = Join-Path $ScriptDir "src\ImmersiveAI.Module\bin\Release"

# 複製所有 Release DLL 與 PDB
Get-ChildItem -Path $releaseBin -Filter "*.dll" | ForEach-Object {
    Copy-Item $_.FullName -Destination $BinTargetDir -Force
    Write-Host "  -> 複製 $($_.Name)" -ForegroundColor Green
}
Get-ChildItem -Path $releaseBin -Filter "*.pdb" | ForEach-Object {
    Copy-Item $_.FullName -Destination $BinTargetDir -Force
}

# 複製 SubModule.xml
$submoduleSrc = Join-Path $ScriptDir "module\SubModule.xml"
if (Test-Path $submoduleSrc) {
    Copy-Item $submoduleSrc -Destination $GameModuleDir -Force
    Write-Host "  -> 更新 SubModule.xml" -ForegroundColor Green
}

# 複製 GUI 資料夾 (若存在)
$guiSrc = Join-Path $ScriptDir "module\GUI"
$guiDest = Join-Path $GameModuleDir "GUI"
if (Test-Path $guiSrc) {
    Copy-Item -Path $guiSrc -Destination $GameModuleDir -Recurse -Force
    Write-Host "  -> 同步 GUI 介面資源" -ForegroundColor Green
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  編譯與部署完成！現在可以啟動 Bannerlord 進入遊戲測試！" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
