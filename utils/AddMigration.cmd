@echo off
cd ..
set /p migration_name=Enter migration name: 
dotnet ef migrations add %migration_name% --context AppDbContext
pause