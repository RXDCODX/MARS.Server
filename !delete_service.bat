@echo off
setlocal enabledelayedexpansion

:: Проверка прав администратора
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Этот скрипт требует запуска от имени администратора.
    echo Запуск с повышенными правами...
    PowerShell Start-Process '%~f0' -Verb RunAs
    exit /b
)

:: Проверяем, существует ли служба "!ZYZ"
sc query "!ZYZ" >nul 2>&1
if %errorlevel% neq 0 (
    echo Служба "!ZYZ" не существует.
    pause
    exit /b 1
)

:: Удаляем службу "!ZYZ"
echo Удаление службы "!ZYZ"...
sc delete "!ZYZ"
if %errorlevel% equ 0 (
    echo Служба "!ZYZ" успешно удалена.
) else (
    echo Ошибка при удалении службы "!ZYZ".
    pause
    exit /b 1
)

:: Удаляем переменную окружения ZYZ_SERVICE_PATH
echo Удаление переменной окружения ZYZ_SERVICE_PATH...
reg delete "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v ZYZ_SERVICE_PATH /f >nul 2>&1
if %errorlevel% equ 0 (
    echo Переменная окружения ZYZ_SERVICE_PATH успешно удалена.
) else (
    echo Переменная окружения ZYZ_SERVICE_PATH не найдена или не была удалена.
)

pause