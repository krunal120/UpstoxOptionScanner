Write-Host "Starting NiftyQuant Web Dashboard on http://localhost:3000..." -ForegroundColor Green
Set-Location "$PSScriptRoot/src/NiftyQuant.Web"
npm run dev -- --host 127.0.0.1 --port 3000
