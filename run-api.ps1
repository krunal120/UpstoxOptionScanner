Write-Host "Checking for existing processes on port 5000 / 5001..." -ForegroundColor Cyan
$existing = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in 5000, 5001 }
if ($existing) {
    Write-Host "Freeing ports 5000/5001 from background process..." -ForegroundColor Yellow
    foreach ($conn in $existing) {
        Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 1
}

Write-Host "Starting NiftyQuant.Api on http://localhost:5000..." -ForegroundColor Green
dotnet run --project src/NiftyQuant.Api/NiftyQuant.Api.csproj
