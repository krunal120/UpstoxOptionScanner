@echo off
setlocal
set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
cd /d "%ROOT%"

title NiftyQuant Trading System Launcher
echo ======================================================================
echo          NIFTYQUANT AUTONOMOUS TRADING ENGINE LAUNCHER
echo ======================================================================
echo Working Directory: %ROOT%
echo.

:: Step 1: Free ports (3000, 3001, 5000, 5001) and terminate any stuck instances
echo [1/3] Terminating any old instances and freeing ports...
taskkill /F /IM NiftyQuant.Api.exe >nul 2>&1
taskkill /F /IM NiftyQuant.Worker.exe >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in 3000, 3001, 5000, 5001 } | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }"
timeout /t 1 /nobreak >nul

:: Step 2: Start API Server & Autonomous Trading Engine
echo [2/3] Starting NiftyQuant API & Autonomous Trading Engine on http://localhost:5000...
start "NiftyQuant API Engine [Port 5000]" cmd /k "cd /d ""%ROOT%"" && title NiftyQuant API Engine [Port 5000] && dotnet run --project src\NiftyQuant.Api\NiftyQuant.Api.csproj --launch-profile http"

:: Wait 4 seconds for API and database initialization
echo [*] Waiting for API engine initialization...
timeout /t 4 /nobreak >nul

:: Step 3: Start React Web Dashboard
echo [3/3] Starting React Web Dashboard on http://localhost:3000...
start "NiftyQuant Web Dashboard [Port 3000]" cmd /k "cd /d ""%ROOT%\src\NiftyQuant.Web"" && title NiftyQuant Web Dashboard [Port 3000] && npm run dev"

:: Step 4: Launch Browser
timeout /t 3 /nobreak >nul
echo.
echo ======================================================================
echo   All systems active!
echo   Dashboard UI : http://localhost:3000
echo   API Scalar   : http://localhost:5000/scalar/v1
echo   API Swagger  : http://localhost:5000/swagger
echo ======================================================================
start http://localhost:3000
