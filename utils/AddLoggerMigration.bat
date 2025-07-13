@echo off
setlocal disabledelayedexpansion
chcp 65001 >nul

cd ..
set /p migration_name=Enter migration name: 
dotnet ef migrations add %migration_name% --context LoggerDbContext --output-dir Migrations/LoggerMigrations
pause