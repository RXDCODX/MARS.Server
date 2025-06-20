@echo off
setlocal disabledelayedexpansion
chcp 65001 >nul

cd ..
dotnet ef database update --verbose --context LoggerDbContext
pause