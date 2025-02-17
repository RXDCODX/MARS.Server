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

:: ���������, ���������� �� ������ "!ZYZ"
sc query "!ZYZ" >nul 2>&1
if %errorlevel% neq 0 (
    echo ������ "!ZYZ" �� ����������.
    pause
    exit /b 1
)

:: ������� ������ "!ZYZ"
echo �������� ������ "!ZYZ"...
sc delete "!ZYZ"
if %errorlevel% equ 0 (
    echo ������ "!ZYZ" ������� �������.
) else (
    echo ������ ��� �������� ������ "!ZYZ".
    pause
    exit /b 1
)

:: ������� ���������� ��������� ZYZ_SERVICE_PATH
echo �������� ���������� ��������� ZYZ_SERVICE_PATH...
reg delete "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v ZYZ_SERVICE_PATH /f >nul 2>&1
if %errorlevel% equ 0 (
    echo ���������� ��������� ZYZ_SERVICE_PATH ������� �������.
) else (
    echo ���������� ��������� ZYZ_SERVICE_PATH �� ������� ��� �� ���� �������.
)

pause