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

:: Проверяем, существует ли служба "!ZYZ"
sc query "!ZYZ" >nul 2>&1
if %errorlevel% neq 0 (
    echo Служба "!ZYZ" не существует. Используйте !create_service.bat для создания.
    pause
    exit /b 1
)

:: === Этап 1: Удаление существующей службы ===

:: Останавливаем, если запущена
sc query "!ZYZ" | find "RUNNING" >nul 2>&1
if %errorlevel% equ 0 (
    echo Остановка службы "!ZYZ"...
    sc stop "!ZYZ" >nul 2>&1
    timeout /t 3 /nobreak >nul
)

:: Удаляем службу
echo Удаление службы "!ZYZ"...
sc delete "!ZYZ" >nul 2>&1

:: Ждём полного удаления (включая "marked for deletion")
echo Ожидание полного удаления службы...
set /a attempts=0
:waitDeleteRestart
timeout /t 2 /nobreak >nul
sc query "!ZYZ" >nul 2>&1
if %errorlevel% neq 0 goto serviceDeletedRestart
set /a attempts+=1
if %attempts% geq 15 (
    echo Служба "!ZYZ" всё ещё отмечена для удаления. Проверьте её вручную.
    pause
    exit /b 1
)
echo Служба ещё существует (marked for deletion). Ожидание...
goto waitDeleteRestart
:serviceDeletedRestart
echo Служба "!ZYZ" полностью удалена.

:: === Этап 2: Создание службы заново ::

:: Добавляем переменную окружения ZYZ_SERVICE_PATH
echo Добавление переменной окружения ZYZ_SERVICE_PATH...
setx /m ZYZ_SERVICE_PATH "%currentDir%" >nul 2>&1
if %errorlevel% equ 0 (
    echo Переменная окружения ZYZ_SERVICE_PATH успешно добавлена: %currentDir%
) else (
    echo Ошибка при добавлении переменной окружения ZYZ_SERVICE_PATH.
    pause
    exit /b 1
)

:: Создаём службу "!ZYZ"
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

echo.
echo Перезапуск службы "!ZYZ" завершён успешно.
pause
