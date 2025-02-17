@echo off
setlocal enabledelayedexpansion

:: �������� ���� ��������������
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ���� ������ ������� ������� �� ����� ��������������.
    echo ������ � ����������� �������...
    PowerShell Start-Process '%~f0' -Verb RunAs
    exit /b
)

:: �������� ���� � ������� ����������
set "currentDir=%~dp0"

:: ��������� ������� ����� Telegramus.exe
if not exist "%currentDir%Telegramus.exe" (
    echo ���� Telegramus.exe �� ������ � ������� ����������.
    pause
    exit /b 1
)

:: ���������, ���������� �� ��� ������ "!ZYZ"
sc query "!ZYZ" >nul 2>&1
if %errorlevel% equ 0 (
    echo ������ "!ZYZ" ��� ����������.
    pause
    exit /b 1
)

:: ��������� ���������� ��������� ZYZ_SERVICE_PATH
echo ���������� ���������� ��������� ZYZ_SERVICE_PATH...
setx ZYZ_SERVICE_PATH "%currentDir%" /M >nul 2>&1
if %errorlevel% equ 0 (
    echo ���������� ��������� ZYZ_SERVICE_PATH ������� ���������: %currentDir%
) else (
    echo ������ ��� ���������� ���������� ��������� ZYZ_SERVICE_PATH.
)

:: ������� ������ "!ZYZ"
echo �������� ������ "!ZYZ"...
sc create "!ZYZ" binPath= "%currentDir%Telegramus.exe" DisplayName= "!ZYZ" start= auto obj= "LocalSystem" description= "Telegramus ������"
if %errorlevel% equ 0 (
    echo ������ "!ZYZ" ������� �������.
) else (
    echo ������ ��� �������� ������ "!ZYZ".
    pause
    exit /b 1
)

pause