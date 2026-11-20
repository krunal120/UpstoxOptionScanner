@echo off
setlocal
set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
cd /d "%ROOT%"
title Stop NiftyQuant Trading System

echo ========================================================
echo   Stopping NiftyQuant Services...
echo ========================================================

taskkill /F /IM NiftyQuant.Api.exe >nul 2>&1
taskkill /F /IM NiftyQuant.Worker.exe >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in 3000, 3001, 5000, 5001 } | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }"

echo.
echo All NiftyQuant services and ports have been stopped cleanly.
echo ========================================================
timeout /t 2 /nobreak >nul
