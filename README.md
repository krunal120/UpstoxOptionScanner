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
│   ├── NiftyQuant.Infrastructure/   # Upstox REST/WS, EF Core SQL Server Persistence, Resilience
│   ├── NiftyQuant.Worker/           # Background tick ingestion, candle aggregation, signal generator
│   └── NiftyQuant.Api/              # ASP.NET Core REST API, SignalR Hubs, Swagger UI, Health Checks
│
├── tests/
│   ├── NiftyQuant.UnitTests/        # Calculation & domain unit tests
│   ├── NiftyQuant.IntegrationTests/ # EF Core SQL Server & Upstox live integration tests
│   └── NiftyQuant.StrategyTests/    # Quantitative strategy tests
│
└── postman/
    └── NiftyQuant_API.postman_collection.json # Postman collection for all API endpoints
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

### 1. Run Unit & Live Integration Tests:
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

## Endpoints Implemented

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/health` | Application liveness check |
| `GET` | `/health/ready` | Readiness check (SQL Server connectivity) |
| `GET` | `/api/market/overview` | Real-time spot price for NIFTY 50 and India VIX |
| `GET` | `/api/market/quote?instrumentKey=...` | Single instrument live market quote |
| `GET` | `/api/market/quotes?keys=...` | Batch market quotes |
| `GET` | `/api/market/expiries` | Active NIFTY option expiry dates |
| `GET` | `/api/market/option-chain?expiryDate=...` | Live option chain with real-time Greeks and PCR |
| `GET` | `/api/market/candles?interval=1&days=2` | Historical 1-minute OHLCV candles |
| `POST` | `/api/market/sync-contracts` | Synchronizes active NIFTY option contracts to SQL Server |
| `GET` | `/api/market/stream-status` | WebSocket connection health, active subscriptions, and tick stats |
| `GET` | `/api/market/latest-snapshot` | Retrieves most recent persisted option-chain snapshot with quotes |
| `POST` | `/api/market/capture-snapshot` | Triggers immediate option-chain snapshot, Max Pain, and intraday delta calculation |
| `GET` | `/api/market/live-candle` | Retrieves real-time in-flight forming candle (1m, 5m, 15m) |
| `GET` | `/api/indicators/snapshot` | Multi-indicator snapshot (EMAs, VWAP, Supertrend, RSI, MACD, ADX, ATR, BB) |
| `GET` | `/api/indicators/multi-timeframe` | Multi-timeframe trend, Supertrend, and VWAP alignment across 1m, 5m, 15m |
| `GET` | `/api/indicators/cpr` | Daily CPR (Pivot, TC, BC, Width %) & Classic Pivot Points (R1-R3, S1-S3) |
| `GET` | `/api/options/intelligence` | Full options intelligence report (Sentiment score, PCR, Max Pain, Key levels, Buildup) |
| `GET` | `/api/options/select-contract` | Dynamic optimal CE/PE contract selection for options buying (Delta, Liquidity, Spread) |
| `GET` | `/api/options/pcr` | Put-Call Ratio (OI & Volume), Trend (Rising/Falling/Flat), and Sentiment Zone |
| `GET` | `/api/options/levels` | Major and immediate option support/resistance walls and expected trading range |
| `POST` | `/api/strategy/evaluate` | Evaluates live multi-timeframe context, classifies regime, and generates executable option strategy signal |
| `GET` | `/api/strategy/latest-signal` | Retrieves latest persisted strategy signal with explainable scoring breakdown and risk parameters |
| `GET` | `/api/strategy/history` | Fetches recent strategy signals with pagination |
| `GET` | `/api/strategy/regime` | Classifies current market regime (Strong Bullish, Strong Bearish, Sideways, High Volatility, Transitional) |
| `GET` | `/api/risk/status` | Daily risk metrics, remaining loss budget, consecutive loss count, and circuit breaker status |
| `GET` | `/api/risk/account` | Virtual paper trading account balance, initial capital, utilized margin, and PnL |
| `POST` | `/api/risk/validate-signal` | Hard pre-trade risk validation across 10 critical checks before order routing |
| `GET` | `/api/risk/position-size` | Position sizing, margin requirement, per-share risk, and estimated round-trip charges |
| `POST` | `/api/risk/reset-daily-limits` | Resets daily loss circuit breakers and locks for testing or new trading session |

---

## Project Status

- [x] **Phase 1: Project Foundation** (Solution, Clean Architecture, EF Core SQL Server, Serilog, Swagger, Health Checks, Test Fixtures)
- [x] **Phase 2: Upstox API Integration** (Authentication, Typed DTOs, Resilience/Retry, Quotes, Option Chain, Historical Candles, Postman collection)
- [x] **Phase 3: WebSocket Feed V3 & Protobuf Decoding** (Market Data Feed V3, dynamic ATM subscription, Protobuf decoding, resilience & reconnection)
- [x] **Phase 4: Market Data Storage & Snapshot Engine** (1m/5m/15m IST-aligned tick aggregation, Option chain snapshots, Max Pain calculation, Intraday OI delta tracking)
- [x] **Phase 5: Technical Indicator Engine** (Pure C# high-precision EMA 9/21/50/200, Session VWAP, Supertrend 10/3, RSI 14, MACD 12/26/9, ADX 14 with +DI/-DI, Bollinger Bands, Daily CPR & Pivots, Multi-Timeframe Alignment)
- [x] **Phase 6: Options Intelligence Engine** (Strike-by-strike OI buildup classification: Long/Short Buildup & Covering/Unwinding, PCR trend analysis, Dynamic CE/PE contract selector, Options Sentiment -100 to +100)
- [x] **Phase 7: Strategy Engine & Multi-Factor Scoring** (Market Regime Classifier, Trend Following, Breakout, and Reversal evaluators, Multi-factor composite scoring [0-100], Time-of-day & High Volatility risk vetoes, Delta-weighted option premium target & stop-loss calculations, SQL Server signal persistence)
- [x] **Phase 8: Risk Management Engine** (Daily loss limit [1.5% = ₹750], Risk per trade [0.5% = ₹250], Max concurrent positions [1], Max consecutive losses [2], Max daily trades [6], Averaging down / Martingale prohibition, Pre-trade risk validation, Circuit breaker enforcement, Estimated round-trip charges)
- [ ] **Phase 9: Paper Trading Engine**
- [ ] **Phase 10: Backtesting Framework**
- [ ] **Phase 11: Real-Time Web Dashboard**
- [ ] **Phase 12: Production Readiness & Hardening**
