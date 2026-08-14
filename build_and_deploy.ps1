<#
.SYNOPSIS
    Immersive AI 一鍵編譯與自動部署腳本 (支援遊戲目錄與 Vortex Staging 同步)
#>

$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot
$GameModuleDir = "C:\Game\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ImmersiveAI"
$BinTargetDir = Join-Path $GameModuleDir "bin\Win64_Shipping_Client"
$VortexStagingRoot = "$env:APPDATA\Vortex\mountandblade2bannerlord\mods"

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

Write-Host "`n[2/3] 檢查目標部署目錄..." -ForegroundColor Yellow

if (-not (Test-Path $BinTargetDir)) {
    New-Item -ItemType Directory -Force -Path $BinTargetDir | Out-Null
}

# 找出 Vortex Staging 中的 ImmersiveAI 資料夾 (若有)
$vortexTargets = @()
if (Test-Path $VortexStagingRoot) {
    Get-ChildItem -Path $VortexStagingRoot -Directory | Where-Object { $_.Name -like "*ImmersiveAI*" } | ForEach-Object {
        $vortexTargets += $_.FullName
    }
}

# 2. 複製產出檔案
Write-Host "`n[3/3] 部署模組檔案至遊戲目錄..." -ForegroundColor Yellow

$releaseBin = Join-Path $ScriptDir "src\ImmersiveAI.Module\bin\Release"

function Deploy-ToTarget($destRoot) {
    $binDir = Join-Path $destRoot "bin\Win64_Shipping_Client"
    if (-not (Test-Path $binDir)) { New-Item -ItemType Directory -Force -Path $binDir | Out-Null }

    Get-ChildItem -Path $releaseBin -Filter "*.dll" | ForEach-Object {
        Copy-Item $_.FullName -Destination $binDir -Force
        Write-Host "  -> 複製 $($_.Name) 至 $binDir" -ForegroundColor Green
    }
    Get-ChildItem -Path $releaseBin -Filter "*.pdb" | ForEach-Object {
        Copy-Item $_.FullName -Destination $binDir -Force
    }

    $submoduleSrc = Join-Path $ScriptDir "module\SubModule.xml"
    if (Test-Path $submoduleSrc) {
        Copy-Item $submoduleSrc -Destination $destRoot -Force
        Write-Host "  -> 更新 SubModule.xml 至 $destRoot" -ForegroundColor Green
    }

    $guiSrc = Join-Path $ScriptDir "module\GUI"
    if (Test-Path $guiSrc) {
        Copy-Item -Path $guiSrc -Destination $destRoot -Recurse -Force
        Write-Host "  -> 同步 GUI 介面資源 至 $destRoot" -ForegroundColor Green
    }
}

# 部署至遊戲主目錄
Deploy-ToTarget $GameModuleDir

# 部署至 Vortex Staging (防止 Vortex 再次部署時覆蓋成舊版)
if ($vortexTargets.Count -gt 0) {
    Write-Host "`n[+] 同步更新至 Vortex Staging 目錄 (防止被 Vortex 還原)..." -ForegroundColor Yellow
    foreach ($vt in $vortexTargets) {
        Deploy-ToTarget $vt
    }
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  編譯與部署完成！現在可以啟動 Bannerlord 進入遊戲測試！" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
