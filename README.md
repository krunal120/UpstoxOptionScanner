# NiftyQuant: NIFTY 50 Options Quantitative Trading System

NiftyQuant is a production-quality quantitative options scanner, analysis engine, and simulated paper trading system designed for **NIFTY 50 Index Options (CE & PE buying)** using the official **Upstox API v2/v3** and **.NET 10 LTS**.

---

## Architecture Overview

```
UpstoxOptionScanner/
│
├── NiftyQuant.sln
│
├── src/
│   ├── NiftyQuant.Domain/           # Entities, Enums, Value Objects, Domain Rules
│   ├── NiftyQuant.Application/      # Use cases, Interfaces, DTOs, Configuration Options
│   ├── NiftyQuant.Infrastructure/   # Upstox REST/WS, EF Core SQL Server Persistence, Protobuf
│   ├── NiftyQuant.Worker/           # Background tick ingestion, candle aggregation, signal generator
│   └── NiftyQuant.Api/              # ASP.NET Core REST API, SignalR Hubs, Swagger UI, Health Checks
│
└── tests/
    ├── NiftyQuant.UnitTests/        # Calculation & domain unit tests
    ├── NiftyQuant.IntegrationTests/ # EF Core & SQL Server integration tests
    └── NiftyQuant.StrategyTests/    # Quantitative strategy tests
```

---

## Prerequisites

- **.NET 10.0 SDK** or later
- **SQL Server 2022** (or LocalDB / SQL Server Express)
- **Node.js v20+** (for the frontend dashboard)
- **Upstox Developer Account** with Analytics Token

---

## Configuration & Secret Management

In accordance with strict security standards, API tokens must **never** be hardcoded or committed to git.

### Set Upstox Access Token in Development:
```powershell
dotnet user-secrets set "Upstox:AccessToken" "<YOUR_UPSTOX_ACCESS_TOKEN>" --project src/NiftyQuant.Api/NiftyQuant.Api.csproj
dotnet user-secrets set "Upstox:AccessToken" "<YOUR_UPSTOX_ACCESS_TOKEN>" --project src/NiftyQuant.Worker/NiftyQuant.Worker.csproj
```

### Database Connection String:
Configured in `src/NiftyQuant.Api/appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLSERVER2022_2;Database=NiftyQuantDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
}
```

---

## Running the Application

### 1. Run Unit & Integration Tests:
```powershell
dotnet test
```

### 2. Apply Database Migrations:
```powershell
dotnet ef database update --project src/NiftyQuant.Infrastructure/NiftyQuant.Infrastructure.csproj --startup-project src/NiftyQuant.Api/NiftyQuant.Api.csproj
```

### 3. Launch the REST API:
```powershell
dotnet run --project src/NiftyQuant.Api/NiftyQuant.Api.csproj
```
- Swagger UI: `http://localhost:5000/swagger`
- Liveness Check: `http://localhost:5000/health`
- Readiness Check: `http://localhost:5000/health/ready`

---

## Project Status

- [x] **Phase 1: Project Foundation** (Solution, Clean Architecture, EF Core SQL Server, Serilog, Swagger, Health Checks, Test Fixtures)
- [ ] **Phase 2: Upstox API Integration** (Instrument master, Option chain, Historical candles, Market quotes)
- [ ] **Phase 3: WebSocket Feed V3 & Protobuf Decoding**
- [ ] **Phase 4: Market Data Storage & Snapshot Engine**
- [ ] **Phase 5: Technical Indicator Engine**
- [ ] **Phase 6: Options Intelligence Engine**
- [ ] **Phase 7: Strategy Engine & Multi-Factor Scoring**
- [ ] **Phase 8: Risk Management Engine**
- [ ] **Phase 9: Paper Trading Engine**
- [ ] **Phase 10: Backtesting Framework**
- [ ] **Phase 11: Real-Time Web Dashboard**
- [ ] **Phase 12: Production Readiness & Hardening**
