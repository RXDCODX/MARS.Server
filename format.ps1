#!/usr/bin/env pwsh
#
# Скрипт для ручного форматирования проекта MARS.Server
#

param(
    [switch]$Check,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = $ScriptDir

Write-Host "🎨 CSharpier Formatter для MARS.Server" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Cyan

# Проверяем установлен ли CSharpier
Write-Host "`n🔍 Проверка CSharpier..." -ForegroundColor Yellow
$CSharpierInstalled = dotnet tool list -g | Select-String "csharpier"

if (-not $CSharpierInstalled) {
    Write-Host "❌ CSharpier не установлен глобально" -ForegroundColor Red
    Write-Host "Установите: dotnet tool install -g csharpier" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ CSharpier найден" -ForegroundColor Green

# Определяем режим работы
if ($Check) {
    Write-Host "`n🔍 Режим проверки (без изменения файлов)..." -ForegroundColor Cyan
    $command = "dotnet csharpier --check `"$ProjectDir`""
} else {
    Write-Host "`n🔧 Форматирование проекта..." -ForegroundColor Cyan
    $command = "dotnet csharpier `"$ProjectDir`""
}

if ($Verbose) {
    $command += " --verbose"
}

# Выполняем команду
Write-Host "Команда: $command" -ForegroundColor DarkGray
Write-Host ""

try {
    Invoke-Expression $command
    
    if ($LASTEXITCODE -eq 0) {
        if ($Check) {
            Write-Host "`n✅ Проверка завершена: все файлы отформатированы корректно" -ForegroundColor Green
        } else {
            Write-Host "`n✅ Форматирование завершено успешно!" -ForegroundColor Green
        }
    } else {
        if ($Check) {
            Write-Host "`n⚠️  Найдены файлы, требующие форматирования" -ForegroundColor Yellow
            Write-Host "Запустите без параметра -Check для форматирования" -ForegroundColor Yellow
        } else {
            Write-Host "`n❌ Ошибка форматирования" -ForegroundColor Red
        }
        exit $LASTEXITCODE
    }
} catch {
    Write-Host "`n❌ Ошибка выполнения: $_" -ForegroundColor Red
    exit 1
}

Write-Host "`n" + ("=" * 50) -ForegroundColor Cyan
Write-Host "Готово!" -ForegroundColor Green
