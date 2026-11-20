Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Starting NiftyQuant Quantitative Trading Engine" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# 1. Clean ports 5000/5001 if occupied
$existing = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in 5000, 5001 }
if ($existing) {
    Write-Host "Freeing ports 5000/5001..." -ForegroundColor Yellow
    foreach ($conn in $existing) {
        Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 1
}

# 2. Launch API & Autonomous Engine
Write-Host "[1/2] Launching NiftyQuant API & Autonomous Engine..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --project '$PSScriptRoot/src/NiftyQuant.Api/NiftyQuant.Api.csproj'"

# 3. Wait 3 seconds
Start-Sleep -Seconds 3

# 4. Launch React Web Dashboard
Write-Host "[2/2] Launching React Web Dashboard..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$PSScriptRoot/src/NiftyQuant.Web'; npm run dev"

# 5. Open browser
Start-Sleep -Seconds 3
Start-Process "http://localhost:3000"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  System running! Dashboard: http://localhost:3000" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan
