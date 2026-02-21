@echo off
REM Обновляет глобальный dotnet tool для Entity Framework Core до последней версии

echo ======================================
echo Обновление dotnet-ef tool (глобально)
echo ======================================
echo.

dotnet tool update --global dotnet-ef

echo.
echo ======================================
echo Готово!
echo ======================================
echo.

pause
