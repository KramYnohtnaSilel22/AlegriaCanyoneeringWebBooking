@echo off
setlocal enabledelayedexpansion
cls

echo =====================================
echo   Alegria System Launcher (SMART)
echo =====================================

set "ROOT=C:\Users\Darius Cumpio\source\repos\AlegriaCanyoneeringWebBooking\AlegriaCanyoneeringWebBooking\AlegriaCanyoneeringWebBooking"
set "FLAG=%TEMP%\dotnet_installed.flag"

:: =========================
:: CHECK DOTNET
:: =========================
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ .NET SDK NOT FOUND
    echo.

    if exist "%FLAG%" (
        echo ⚠ Installer already run once.
        echo 👉 Please restart PC or reopen CMD then run again.
        pause
        exit
    )

    echo 🔄 Installing .NET SDK 8...

    set "URL=https://download.visualstudio.microsoft.com/download/pr/latest/dotnet-sdk-8-win-x64.exe"
    set "FILE=%TEMP%\dotnet-sdk-8.exe"

    powershell -Command "Invoke-WebRequest '%URL%' -OutFile '%FILE%'"

    start /wait "" "%FILE%" /quiet /norestart

    echo done > "%FLAG%"

    echo.
    echo ⚠ Installation finished.
    echo 🔁 PLEASE RESTART CMD or PC then re-run this file.
    pause
    exit
)

:: =========================
:: DOTNET READY
:: =========================
echo ✔ .NET detected

if exist "%FLAG%" del "%FLAG%"

cd /d "%ROOT%"

echo Restoring packages...
dotnet restore

echo Starting application...
start cmd /k "dotnet run"

timeout /t 5 >nul
start http://localhost:5045