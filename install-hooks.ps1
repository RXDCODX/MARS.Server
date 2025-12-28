#!/usr/bin/env pwsh
#
# PowerShell скрипт для установки Git pre-commit hook для форматирования MARS.Server
#

$ErrorActionPreference = "Stop"

Write-Host "🔧 Установка Git pre-commit hook для MARS.Server..." -ForegroundColor Cyan

# Определяем пути
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$GitHooksSource = Join-Path $ScriptDir ".githooks"
$GitDir = Join-Path $ProjectRoot ".git"
$GitHooksDir = Join-Path $GitDir "hooks"

# Проверяем, что мы в Git репозитории
if (-not (Test-Path $GitDir)) {
    Write-Host "❌ Ошибка: .git директория не найдена в $ProjectRoot" -ForegroundColor Red
    Write-Host "Убедитесь, что вы находитесь в корне Git репозитория MARS.Server" -ForegroundColor Yellow
    exit 1
}

# Создаем директорию hooks если её нет
if (-not (Test-Path $GitHooksDir)) {
    New-Item -ItemType Directory -Path $GitHooksDir -Force | Out-Null
    Write-Host "✅ Создана директория: $GitHooksDir" -ForegroundColor Green
}

# Копируем pre-commit hook
$SourceHook = Join-Path $GitHooksSource "pre-commit"
$TargetHook = Join-Path $GitHooksDir "pre-commit"

if (-not (Test-Path $SourceHook)) {
    Write-Host "❌ Ошибка: Файл pre-commit не найден в $GitHooksSource" -ForegroundColor Red
    exit 1
}

Copy-Item -Path $SourceHook -Destination $TargetHook -Force
Write-Host "✅ Pre-commit hook скопирован в $TargetHook" -ForegroundColor Green

# Для Windows Git Bash делаем файл исполняемым (если установлен WSL/Git Bash)
if (Get-Command "bash" -ErrorAction SilentlyContinue) {
    try {
        bash -c "chmod +x '$($TargetHook -replace '\\', '/')'"
        Write-Host "✅ Установлены права на выполнение для pre-commit hook" -ForegroundColor Green
    } catch {
        Write-Host "⚠️  Не удалось установить права на выполнение (это нормально для Windows)" -ForegroundColor Yellow
    }
}

# Проверяем установлен ли CSharpier
Write-Host "`n🔍 Проверка установки CSharpier..." -ForegroundColor Cyan
$CSharpierInstalled = dotnet tool list -g | Select-String "csharpier"

if (-not $CSharpierInstalled) {
    Write-Host "⚠️  CSharpier не установлен глобально" -ForegroundColor Yellow
    Write-Host "Хотите установить CSharpier сейчас? (Y/N): " -NoNewline -ForegroundColor Yellow
    $response = Read-Host
    
    if ($response -eq "Y" -or $response -eq "y") {
        Write-Host "📦 Установка CSharpier..." -ForegroundColor Cyan
        dotnet tool install -g csharpier
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ CSharpier успешно установлен" -ForegroundColor Green
        } else {
            Write-Host "❌ Ошибка установки CSharpier" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "⚠️  Установите CSharpier вручную: dotnet tool install -g csharpier" -ForegroundColor Yellow
    }
} else {
    Write-Host "✅ CSharpier уже установлен" -ForegroundColor Green
}

Write-Host "`n✨ Установка завершена!" -ForegroundColor Green
Write-Host "`nТеперь при каждом коммите проект MARS.Server будет автоматически форматироваться." -ForegroundColor Cyan
Write-Host "Для ручного форматирования используйте: dotnet csharpier MARS.Projects/MARS.Server" -ForegroundColor Cyan
