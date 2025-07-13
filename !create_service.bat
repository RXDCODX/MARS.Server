@echo off
setlocal disabledelayedexpansion
chcp 65001 >nul

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

:: Проверяем наличие файла MARS.Server.exe
if not exist "%currentDir%MARS.Server.exe" (
    echo Файл MARS.Server.exe не найден в текущей директории.
    pause
    exit /b 1
)

:: Проверяем, существует ли уже служба "!ZYZ"
sc query "!ZYZ" >nul 2>&1
if %errorlevel% equ 0 (
    echo Служба "!ZYZ" уже существует.
    
    :: Проверяем, запущена ли служба
    sc query "!ZYZ" | find "RUNNING" >nul 2>&1
    if %errorlevel% equ 0 (
        echo Служба запущена. Останавливаем для перезапуска...
        sc stop "!ZYZ"
        if %errorlevel% equ 0 (
            echo Служба "!ZYZ" успешно остановлена.
        ) else (
            echo Ошибка при остановке службы "!ZYZ".
            pause
            exit /b 1
        )
        :: Ждем немного для корректного завершения
        timeout /t 3 /nobreak >nul
    )
    
    :: Удаляем существующую службу
    echo Удаление существующей службы "!ZYZ"...
    sc delete "!ZYZ"
    if %errorlevel% equ 0 (
        echo Существующая служба "!ZYZ" успешно удалена.
    ) else (
        echo Ошибка при удалении существующей службы "!ZYZ".
        pause
        exit /b 1
    )
)

:: Добавляем переменную окружения ZYZ_SERVICE_PATH
echo Добавление переменной окружения ZYZ_SERVICE_PATH...
setx /m ZYZ_SERVICE_PATH %currentDir% >nul 2>&1
if %errorlevel% equ 0 (
    echo Переменная окружения ZYZ_SERVICE_PATH успешно добавлена: %currentDir%
) else (
    echo Ошибка при добавлении переменной окружения ZYZ_SERVICE_PATH.
    pause
    exit /b 1
)

:: Создаем службу "!ZYZ"
echo Создание службы "!ZYZ"...
sc.exe create "!ZYZ" binPath= "%currentDir%MARS.Server.exe" start= delayed-auto
if %errorlevel% equ 0 (
    echo Служба "!ZYZ" успешно создана.
) else (
    echo Ошибка при создании службы "!ZYZ".
    pause
    exit /b 1
)

:: Запускаем службу
echo Запуск службы "!ZYZ"...
sc.exe start "!ZYZ"
if %errorlevel% equ 0 (
    echo Служба "!ZYZ" успешно запущена.
) else (
    echo Ошибка при запуске службы "!ZYZ".
    pause
    exit /b 1
)

pause