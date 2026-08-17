@echo off
setlocal

set "ROOT_DIR=%~dp0..\..\..\"
set "PROJECT=%ROOT_DIR%MARS.Projects\MARS.Server\MARS.Server.csproj"
set "CONFIG=Release"
set "SKIP_CLIENT=false"
set "SKIP_TESTS=false"

:parse_args
if "%~1"=="" goto run_publish
if /i "%~1"=="--skip-client" set "SKIP_CLIENT=true"
if /i "%~1"=="--skip-tests" set "SKIP_TESTS=true"
if /i "%~1"=="-h" goto show_help
if /i "%~1"=="--help" goto show_help
shift
goto parse_args

:show_help
echo Usage: PublishServer.bat [options]
echo.
echo Options:
echo   --skip-client   Skip frontend build (yarn build)
echo   --skip-tests    Skip tests before publish
echo   -h, --help      Show this help
echo.
echo Default: Release config, tests and frontend are built.
echo Output:  bin\Release\net10.0\publish\
exit /b 0

:run_publish
echo ============================================================
echo Publishing MARS.Server [%CONFIG%]
echo ============================================================
echo.

if "%SKIP_TESTS%"=="true" (
    if "%SKIP_CLIENT%"=="true" (
        dotnet publish "%PROJECT%" --configuration %CONFIG% --self-contained false -p:UseLocalYoutubeReExplode=true -p:RunTestsOnPublish=false -p:SkipBuildClient=true
    ) else (
        dotnet publish "%PROJECT%" --configuration %CONFIG% --self-contained false -p:UseLocalYoutubeReExplode=true -p:RunTestsOnPublish=false
    )
) else (
    if "%SKIP_CLIENT%"=="true" (
        dotnet publish "%PROJECT%" --configuration %CONFIG% --self-contained false -p:UseLocalYoutubeReExplode=true -p:SkipBuildClient=true
    ) else (
        dotnet publish "%PROJECT%" --configuration %CONFIG% --self-contained false -p:UseLocalYoutubeReExplode=true
    )
)

if errorlevel 1 (
    echo.
    echo ERROR: Publish failed.
    exit /b 1
)

echo.
echo ============================================================
echo Publish complete: bin\Release\net10.0\publish\
echo ============================================================

exit /b 0
