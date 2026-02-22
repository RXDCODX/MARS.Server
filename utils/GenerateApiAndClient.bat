@echo off
chcp 65001 > nul
setlocal

set "ROOT_DIR=%~dp0..\..\..\"
set "CLIENT_DIR=%ROOT_DIR%MARS.Projects\mars.client"
pushd "%ROOT_DIR%" > nul

echo ============================================================
echo Полная генерация API: server swagger + client codegen
echo ============================================================
echo.

echo [1/2] Генерация OpenAPI схем...
call dotnet run --project "%ROOT_DIR%MARS.Projects\MARS.Server\MARS.Server.csproj" --generate-openapi
if errorlevel 1 (
    echo.
    echo ОШИБКА: Не удалось сгенерировать OpenAPI схемы.
    popd > nul
    exit /b 1
)

echo.
echo [2/2] Генерация TypeScript API клиента (yarn build:api)...
if not exist "%CLIENT_DIR%\package.json" (
    echo.
    echo ОШИБКА: Не найден package.json клиента.
    echo Ожидался путь: %CLIENT_DIR%\package.json
    popd > nul
    exit /b 1
)

where corepack > nul 2>&1
if errorlevel 1 (
    echo.
    echo ОШИБКА: Не найден corepack. Для проекта требуется Yarn 4.
    echo Установите Node.js 16.9+ и выполните: corepack enable
    popd > nul
    exit /b 1
)

call corepack enable > nul 2>&1
call corepack yarn --cwd "%CLIENT_DIR%" build:api
if errorlevel 1 (
    echo.
    echo ОШИБКА: Не удалось выполнить corepack yarn build:api.
    popd > nul
    exit /b 1
)

echo.
echo ============================================================
echo Готово: OpenAPI и клиентские API-файлы обновлены.
echo ============================================================

popd > nul
exit /b 0
