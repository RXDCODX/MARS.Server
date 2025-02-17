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

:: Получаем путь к текущей директории
set "currentDir=%~dp0"

:: Проверяем наличие файла Telegramus.exe
if not exist "%currentDir%Telegramus.exe" (
    echo Файл Telegramus.exe не найден в текущей директории.
    pause
    exit /b 1
)

:: Проверяем, существует ли уже служба "!ZYZ"
sc query "!ZYZ" >nul 2>&1
if %errorlevel% equ 0 (
    echo Служба "!ZYZ" уже существует.
    pause
    exit /b 1
)

:: Добавляем переменную окружения ZYZ_SERVICE_PATH
echo Добавление переменной окружения ZYZ_SERVICE_PATH...
setx ZYZ_SERVICE_PATH "%currentDir%" /M >nul 2>&1
if %errorlevel% equ 0 (
    echo Переменная окружения ZYZ_SERVICE_PATH успешно добавлена: %currentDir%
) else (
    echo Ошибка при добавлении переменной окружения ZYZ_SERVICE_PATH.
)

:: Создаем службу "!ZYZ"
echo Создание службы "!ZYZ"...
sc create "!ZYZ" binPath= "%currentDir%Telegramus.exe" DisplayName= "!ZYZ" start= auto obj= "LocalSystem" description= "Telegramus служба"
if %errorlevel% equ 0 (
    echo Служба "!ZYZ" успешно создана.
) else (
    echo Ошибка при создании службы "!ZYZ".
    pause
    exit /b 1
)

pause