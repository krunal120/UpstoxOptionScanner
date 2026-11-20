@echo off
title NiftyQuant Mobile Tunnel
echo ========================================================
echo   Sharing NiftyQuant to Mobile Browser via Free Tunnel
echo ========================================================
echo Ensure start-all.bat is running on your machine.
echo Generating temporary public HTTPS link for mobile access...
echo.
npx localtunnel --port 3000
pause
