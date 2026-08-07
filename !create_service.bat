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

:: Проверяем, что служба ещё не создана
sc query "!ZYZ" >nul 2>&1
if %errorlevel% equ 0 (
    echo Служба "!ZYZ" уже существует. Создание отменено.
    pause
    exit /b 1
)

:: Проверяем, что переменная окружения ещё не установлена
reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v ZYZ_SERVICE_PATH >nul 2>&1
if %errorlevel% equ 0 (
    echo Переменная окружения ZYZ_SERVICE_PATH уже существует. Создание отменено.
    pause
    exit /b 1
)

:: Создаем службу "!ZYZ"
echo Создание службы "!ZYZ"...
sc.exe create "!ZYZ" binPath= "%currentDir%MARS.Server.exe" start= delayed-auto
if %errorlevel% neq 0 (
    echo Ошибка при создании службы "!ZYZ".
    pause
    exit /b 1
)
echo Служба "!ZYZ" успешно создана.

:: Добавляем переменную окружения ZYZ_SERVICE_PATH
echo Добавление переменной окружения ZYZ_SERVICE_PATH...
setx /m ZYZ_SERVICE_PATH "%currentDir%" >nul 2>&1
if %errorlevel% neq 0 (
    echo Ошибка при добавлении переменной окружения ZYZ_SERVICE_PATH.
    echo Откат: удаление службы "!ZYZ"...
    sc.exe delete "!ZYZ" >nul 2>&1
    pause
    exit /b 1
)
echo Переменная окружения ZYZ_SERVICE_PATH успешно добавлена: %currentDir%

:: Запускаем службу "!ZYZ"
echo Запуск службы "!ZYZ"...
sc.exe start "!ZYZ" >nul 2>&1

:: Ожидаем перехода службы в RUNNING (SCM может вернуть 1053, если старт занимает более 30 секунд)
set /a attempts=0
:waitStart
timeout /t 2 /nobreak >nul
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
