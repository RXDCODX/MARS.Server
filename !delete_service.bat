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
    echo Служба "!ZYZ" не существует. Удаление отменено.
    pause
    exit /b 1
)

:: Останавливаем службу, если запущена
sc query "!ZYZ" | find "RUNNING" >nul 2>&1
if %errorlevel% neq 0 goto notRunning
echo Остановка службы "!ZYZ"...
sc.exe stop "!ZYZ"
if %errorlevel% neq 0 (
    echo Ошибка при остановке службы "!ZYZ".
    pause
    exit /b 1
)
echo Служба "!ZYZ" успешно остановлена.
ping -n 4 127.0.0.1 >nul
:notRunning

:: Удаляем службу "!ZYZ"
echo Удаление службы "!ZYZ"...
sc.exe delete "!ZYZ"
if %errorlevel% neq 0 (
    echo Ошибка при удалении службы "!ZYZ".
    pause
    exit /b 1
)
echo Запрос на удаление службы "!ZYZ" отправлен.

:: Ждём полного удаления службы (включая стадию "marked for deletion")
echo Ожидание полного удаления службы...
set /a attempts=0
:waitDelete
ping -n 3 127.0.0.1 >nul
sc query "!ZYZ" >nul 2>&1
if %errorlevel% neq 0 goto serviceDeleted
set /a attempts+=1
if %attempts% geq 15 (
    echo Служба "!ZYZ" всё ещё отмечена для удаления. Проверьте её вручную.
    pause
    exit /b 1
)
echo Служба ещё существует (marked for deletion). Ожидание...
goto waitDelete
:serviceDeleted
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