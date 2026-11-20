Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Starting NiftyQuant Quantitative Trading Engine" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# 1. Clean ports 3000, 3001, 5000, 5001 if occupied
$existing = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in 3000, 3001, 5000, 5001 }
if ($existing) {
    Write-Host "Freeing ports 3000, 3001, 5000, 5001..." -ForegroundColor Yellow
    foreach ($conn in $existing) {
        Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 1
}

# 2. Terminate any previous API executables
Get-Process -Name "NiftyQuant.Api", "NiftyQuant.Worker" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# 3. Launch API & Autonomous Engine
Write-Host "[1/2] Launching NiftyQuant API & Autonomous Engine on http://localhost:5000..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$PSScriptRoot'; dotnet run --project src/NiftyQuant.Api/NiftyQuant.Api.csproj --launch-profile http"

# 4. Wait 4 seconds
Start-Sleep -Seconds 4

# 5. Launch React Web Dashboard
Write-Host "[2/2] Launching React Web Dashboard on http://localhost:3000..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$PSScriptRoot/src/NiftyQuant.Web'; npm run dev"

# 6. Open browser
Start-Sleep -Seconds 3
Start-Process "http://localhost:3000"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  System running! Dashboard: http://localhost:3000" -ForegroundColor Green
Write-Host "  API Scalar Documentation : http://localhost:5000/scalar/v1" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan
