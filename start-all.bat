@echo off
title NiftyQuant Trading System Launcher
echo ========================================================
echo   Starting NiftyQuant Quantitative Trading Engine
echo ========================================================

:: Step 1: Free ports if occupied
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in 5000, 5001 } | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }"

:: Step 2: Start API Server in background window
echo [1/2] Starting NiftyQuant API & Autonomous Trading Engine...
start "NiftyQuant API Engine" cmd /k "dotnet run --project src\NiftyQuant.Api\NiftyQuant.Api.csproj"

:: Step 3: Wait 3 seconds for API initialization
timeout /t 3 /nobreak >nul

:: Step 4: Start Frontend Web Dashboard
echo [2/2] Starting React Web Dashboard...
start "NiftyQuant Web Dashboard" cmd /k "cd src\NiftyQuant.Web && npm run dev"

:: Step 5: Launch Default Web Browser
timeout /t 3 /nobreak >nul
start http://localhost:3000

echo ========================================================
echo   All systems active! Dashboard: http://localhost:3000
echo   API Swagger: http://localhost:5000/swagger
echo ========================================================
