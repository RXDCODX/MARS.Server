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

:: Проверяем, существует ли служба "!ZYZ"
sc query "!ZYZ" >nul 2>&1
if %errorlevel% neq 0 (
    echo Служба "!ZYZ" не существует.
    pause
    exit /b 1
)

:: Останавливаем службу, если запущена
sc query "!ZYZ" | find "RUNNING" >nul 2>&1
if %errorlevel% equ 0 (
    echo Остановка службы "!ZYZ"...
    sc stop "!ZYZ"
    if %errorlevel% equ 0 (
        echo Служба "!ZYZ" успешно остановлена.
    ) else (
        echo Ошибка при остановке службы "!ZYZ".
        pause
        exit /b 1
    )
    timeout /t 3 /nobreak >nul
)

:: Удаляем службу "!ZYZ"
echo Удаление службы "!ZYZ"...
sc delete "!ZYZ"
if %errorlevel% equ 0 (
    echo Запрос на удаление службы "!ZYZ" отправлен.
) else (
    echo Ошибка при удалении службы "!ZYZ".
    pause
    exit /b 1
)

:: Ждём полного удаления службы (включая стадию "marked for deletion")
echo Ожидание полного удаления службы...
:waitDelete
timeout /t 2 /nobreak >nul
sc query "!ZYZ" >nul 2>&1
if %errorlevel% equ 0 (
    echo Служба ещё существует (marked for deletion). Ожидание...
    goto waitDelete
)
echo Служба "!ZYZ" полностью удалена.

:: Удаляем переменную окружения ZYZ_SERVICE_PATH
echo Удаление переменной окружения ZYZ_SERVICE_PATH...
reg delete "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v ZYZ_SERVICE_PATH /f >nul 2>&1
if %errorlevel% equ 0 (
    echo Переменная окружения ZYZ_SERVICE_PATH успешно удалена.
) else (
    echo Переменная окружения ZYZ_SERVICE_PATH не найдена или не была удалена.
)

pause
