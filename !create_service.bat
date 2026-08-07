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

:: Создаём службу, только если её ещё нет (уже существующую не трогаем)
sc query "!ZYZ" >nul 2>&1
if %errorlevel% equ 0 goto serviceExists

echo Создание службы "!ZYZ"...
sc.exe create "!ZYZ" binPath= "%currentDir%MARS.Server.exe" start= delayed-auto
if %errorlevel% neq 0 (
    echo Ошибка при создании службы "!ZYZ".
    pause
    exit /b 1
)
echo Служба "!ZYZ" успешно создана.
goto varSetup

:serviceExists
echo Служба "!ZYZ" уже существует. Пересоздание не выполняется.

:varSetup
:: Устанавливаем переменную окружения (reg add вместо setx - setx ломает значение, заканчивающееся на "\")
echo Установка переменной окружения ZYZ_SERVICE_PATH...
set "servicePath=%currentDir:~0,-1%"
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v ZYZ_SERVICE_PATH /t REG_SZ /d "%servicePath%" /f >nul 2>&1
if %errorlevel% neq 0 (
    echo Ошибка при установке переменной окружения ZYZ_SERVICE_PATH.
    pause
    exit /b 1
)
echo Переменная окружения ZYZ_SERVICE_PATH успешно установлена: %servicePath%

:: Запускаем службу, если она ещё не запущена
sc query "!ZYZ" | find "RUNNING" >nul 2>&1
if %errorlevel% equ 0 (
    echo Служба "!ZYZ" уже запущена.
    pause
    exit /b 0
)

echo Запуск службы "!ZYZ"...
sc.exe start "!ZYZ" >nul 2>&1

:: Ожидаем перехода службы в RUNNING (SCM может вернуть 1053, если старт занимает более 30 секунд)
set /a attempts=0
:waitStart
ping -n 3 127.0.0.1 >nul
sc query "!ZYZ" | find "RUNNING" >nul 2>&1
if %errorlevel% equ 0 goto serviceRunning
set /a attempts+=1
if %attempts% geq 90 (
    echo Служба "!ZYZ" не перешла в состояние RUNNING в течение 3 минут.
    echo Проверьте журнал событий Windows для диагностики.
    pause
    exit /b 1
)
echo Ожидание запуска службы "!ZYZ"... (%attempts%)
goto waitStart
:serviceRunning
echo Служба "!ZYZ" успешно запущена.

pause