import type {
  AutonomousTradingStatusDto,
  BacktestRequestDto,
  BacktestResultDto,
  DailyRiskStatusDto,
  MarketOverview,
  MarketRegimeResultDto,
  MultiTimeframeStateDto,
  OptionType,
  OptionsReportDto,
  PaperAccountDto,
  PaperOrderDto,
  PaperPortfolioSummaryDto,
  PaperPositionDto,
  PaperTradeDto,
  StrategySignalDto,
} from '../types';

const API_BASE = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/+$/, '') || '/api';

const regimeNames = ['Transitional', 'StrongBullish', 'StrongBearish', 'Sideways', 'HighVolatility'] as const;
const directionNames = ['Wait', 'NoTrade', 'BuyCe', 'StrongBuyCe', 'BuyPe', 'StrongBuyPe'] as const;
const strategyNames = ['None', 'TrendFollowing', 'Breakout', 'Reversal'] as const;
const buildupNames = ['Neutral', 'LongBuildup', 'ShortBuildup', 'ShortCovering', 'LongUnwinding'] as const;

function normalizeSignal(data: any): StrategySignalDto {
  if (!data) return data;
  if (typeof data.marketRegime === 'number') data.marketRegime = regimeNames[data.marketRegime] ?? 'Transitional';
  if (typeof data.direction === 'number') data.direction = directionNames[data.direction] ?? 'Wait';
  if (typeof data.strategyFamily === 'number') data.strategyFamily = strategyNames[data.strategyFamily] ?? 'None';
  if (typeof data.selectedOptionType === 'number') data.selectedOptionType = data.selectedOptionType === 1 ? 'CE' : 'PE';
  return data;
}

export const api = {
  // Market Data
  getMarketOverview: async (): Promise<MarketOverview> => {
    const res = await fetch(`${API_BASE}/market/overview`);
    if (!res.ok) throw new Error('Failed to fetch market overview');
    const data = await res.json();

    if (data.niftySpot) {
      return data;
    }

    const lastPrice = data.lastPrice ?? data.ohlc?.close ?? 0;
    const netChange = data.netChange ?? 0;
    const prevClose = lastPrice - netChange;
    const changePercent = prevClose !== 0 ? (netChange / prevClose) * 100 : 0;

    return {
      niftySpot: {
        lastPrice,
        change: netChange,
        changePercent,
        open: data.ohlc?.open ?? lastPrice,
        high: data.ohlc?.high ?? lastPrice,
        low: data.ohlc?.low ?? lastPrice,
        close: data.ohlc?.close ?? lastPrice,
        timestamp: data.timestamp,
      },
      indiaVix: data.indiaVix
        ? {
            lastPrice: data.indiaVix,
            change: data.vixChange ?? 0,
            changePercent: 0,
          }
        : undefined,
      timestampUtc: data.timestamp || new Date().toISOString(),
    };
  },

  // Options Intelligence & Expiries
  getExpiries: async (underlyingKey = 'NSE_INDEX|Nifty 50'): Promise<string[]> => {
    try {
      const res = await fetch(`${API_BASE}/market/expiries?underlyingKey=${encodeURIComponent(underlyingKey)}`);
      if (!res.ok) return [];
      return res.json();
    } catch {
      return [];
    }
  },

  getOptionChainRaw: async (expiryDate?: string, underlyingKey = 'NSE_INDEX|Nifty 50'): Promise<any> => {
    const url = expiryDate
      ? `${API_BASE}/market/option-chain?underlyingKey=${encodeURIComponent(underlyingKey)}&expiryDate=${encodeURIComponent(expiryDate)}`
      : `${API_BASE}/market/option-chain?underlyingKey=${encodeURIComponent(underlyingKey)}`;
    const res = await fetch(url);
    if (!res.ok) throw new Error('Failed to fetch option chain');
    return res.json();
  },

  getOptionsIntelligence: async (expiryDate?: string, underlyingKey = 'NSE_INDEX|Nifty 50'): Promise<OptionsReportDto> => {
    const url = expiryDate
      ? `${API_BASE}/options/intelligence?underlyingKey=${encodeURIComponent(underlyingKey)}&expiryDate=${encodeURIComponent(expiryDate)}`
      : `${API_BASE}/options/intelligence?underlyingKey=${encodeURIComponent(underlyingKey)}`;
    const res = await fetch(url);
    if (!res.ok) throw new Error('Failed to fetch options intelligence');
    const data = await res.json();

    if (data.recommendedCe && typeof data.recommendedCe.optionType === 'number') {
      data.recommendedCe.optionType = data.recommendedCe.optionType === 1 ? 'CE' : 'PE';
    }
    if (data.recommendedPe && typeof data.recommendedPe.optionType === 'number') {
      data.recommendedPe.optionType = data.recommendedPe.optionType === 1 ? 'CE' : 'PE';
    }

    if (Array.isArray(data.strikes)) {
      data.strikes.forEach((s: any) => {
        if (typeof s.callBuildup === 'number') s.callBuildup = buildupNames[s.callBuildup] ?? 'Neutral';
        if (typeof s.putBuildup === 'number') s.putBuildup = buildupNames[s.putBuildup] ?? 'Neutral';
        if (s.callQuote && typeof s.callQuote.optionType === 'number') {
          s.callQuote.optionType = 'CE';
        }
        if (s.putQuote && typeof s.putQuote.optionType === 'number') {
          s.putQuote.optionType = 'PE';
        }
      });
    }

    return data;
  },

  // Indicators
  getMultiTimeframeState: async (instrumentKey = 'NSE_INDEX|Nifty 50'): Promise<MultiTimeframeStateDto> => {
    const res = await fetch(`${API_BASE}/indicators/multi-timeframe?instrumentKey=${encodeURIComponent(instrumentKey)}`);
    if (!res.ok) throw new Error('Failed to fetch multi-timeframe indicators');
    return res.json();
  },

  // Strategy
  evaluateStrategy: async (underlyingKey = 'NSE_INDEX|Nifty 50'): Promise<StrategySignalDto> => {
    const res = await fetch(`${API_BASE}/strategy/evaluate?underlyingKey=${encodeURIComponent(underlyingKey)}`, {
      method: 'POST',
    });
    if (!res.ok) throw new Error('Failed to evaluate strategy');
    const data = await res.json();
    return normalizeSignal(data);
  },

  getLatestSignal: async (underlyingKey = 'NSE_INDEX|Nifty 50'): Promise<StrategySignalDto | null> => {
    try {
      const res = await fetch(`${API_BASE}/strategy/latest-signal?underlyingKey=${encodeURIComponent(underlyingKey)}`);
      if (!res.ok) return null;
      const data = await res.json();
      return normalizeSignal(data);
    } catch {
      return null;
    }
  },

  getSignalHistory: async (underlyingKey = 'NSE_INDEX|Nifty 50', limit = 20): Promise<StrategySignalDto[]> => {
    try {
      const res = await fetch(`${API_BASE}/strategy/history?underlyingKey=${encodeURIComponent(underlyingKey)}&limit=${limit}`);
      if (!res.ok) return [];
      const data = await res.json();
      return Array.isArray(data) ? data.map(normalizeSignal) : [];
    } catch {
      return [];
    }
  },

  getMarketRegime: async (underlyingKey = 'NSE_INDEX|Nifty 50'): Promise<MarketRegimeResultDto> => {
    const res = await fetch(`${API_BASE}/strategy/regime?underlyingKey=${encodeURIComponent(underlyingKey)}`);
    if (!res.ok) throw new Error('Failed to fetch market regime');
    const data = await res.json();
    if (typeof data.regime === 'number') {
      data.regime = regimeNames[data.regime] ?? 'Transitional';
    }
    return data;
  },

  // Risk Management
  getDailyRiskStatus: async (): Promise<DailyRiskStatusDto> => {
    const res = await fetch(`${API_BASE}/risk/status`);
    if (!res.ok) throw new Error('Failed to fetch daily risk status');
    return res.json();
  },

  getPaperAccount: async (): Promise<PaperAccountDto> => {
    const res = await fetch(`${API_BASE}/risk/account`);
    if (!res.ok) throw new Error('Failed to fetch paper account');
    return res.json();
  },

  resetDailyLimits: async (): Promise<{ message: string; status: DailyRiskStatusDto }> => {
    const res = await fetch(`${API_BASE}/risk/reset-daily-limits`, { method: 'POST' });
    if (!res.ok) throw new Error('Failed to reset daily limits');
    return res.json();
  },

  // Paper Trading
  executeSignal: async (signal?: StrategySignalDto, underlyingKey = 'NSE_INDEX|Nifty 50'): Promise<any> => {
    const res = await fetch(`${API_BASE}/papertrading/execute-signal?underlyingKey=${encodeURIComponent(underlyingKey)}&simulateSlippage=true`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: signal ? JSON.stringify(signal) : undefined,
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Failed to execute paper order');
    return data;
  },

  getOpenPositions: async (): Promise<PaperPositionDto[]> => {
    const res = await fetch(`${API_BASE}/papertrading/positions`);
    if (!res.ok) throw new Error('Failed to fetch open positions');
    const data = await res.json();
    if (Array.isArray(data)) {
      data.forEach((p: any) => {
        if (typeof p.optionType === 'number') {
          p.optionType = p.optionType === 1 ? 'CE' : 'PE';
        }
      });
    }
    return data;
  },

  getTradeHistory: async (limit = 50): Promise<PaperTradeDto[]> => {
    const res = await fetch(`${API_BASE}/papertrading/trades?limit=${limit}`);
    if (!res.ok) throw new Error('Failed to fetch trade history');
    return res.json();
  },

  getOrderHistory: async (limit = 50): Promise<PaperOrderDto[]> => {
    const res = await fetch(`${API_BASE}/papertrading/orders?limit=${limit}`);
    if (!res.ok) throw new Error('Failed to fetch order history');
    return res.json();
  },

  closePosition: async (positionId: string, exitReason = 'ManualClose'): Promise<any> => {
    const res = await fetch(`${API_BASE}/papertrading/close-position`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ positionId, exitReason }),
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Failed to close position');
    return data;
  },

  squareOffAll: async (): Promise<any> => {
    const res = await fetch(`${API_BASE}/papertrading/square-off-all?reason=ManualSquareoff`, { method: 'POST' });
    if (!res.ok) throw new Error('Failed to square off all positions');
    return res.json();
  },

  getPortfolioSummary: async (): Promise<PaperPortfolioSummaryDto> => {
    const res = await fetch(`${API_BASE}/papertrading/portfolio`);
    if (!res.ok) throw new Error('Failed to fetch portfolio summary');
    return res.json();
  },

  // Autonomous Trading
  getAutoTradingStatus: async (): Promise<AutonomousTradingStatusDto> => {
    const res = await fetch(`${API_BASE}/papertrading/auto-trade/status`);
    if (!res.ok) throw new Error('Failed to fetch auto-trading status');
    return res.json();
  },

  toggleAutoTrading: async (req: { enabled?: boolean; minConfidence?: number; marketHoursOnly?: boolean }): Promise<AutonomousTradingStatusDto> => {
    const res = await fetch(`${API_BASE}/papertrading/auto-trade/toggle`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
    if (!res.ok) throw new Error('Failed to update auto-trading settings');
    return res.json();
  },

  testExecuteOrder: async (optionType: 'CE' | 'PE', strikePrice?: number, confidence = 75): Promise<any> => {
    const res = await fetch(`${API_BASE}/papertrading/auto-trade/test-execute`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ optionType, strikePrice, confidence }),
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Failed to trigger test order execution');
    return data;
  },

  // Backtesting
  runBacktest: async (req: BacktestRequestDto): Promise<BacktestResultDto> => {
    const res = await fetch(`${API_BASE}/backtest/run`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.message || 'Backtest failed to execute');
    }
    const data = await res.json();
    if (Array.isArray(data.trades)) {
      data.trades.forEach((t: any) => {
        if (typeof t.strategyFamily === 'number') t.strategyFamily = strategyNames[t.strategyFamily] ?? 'None';
        if (typeof t.direction === 'number') t.direction = directionNames[t.direction] ?? 'Wait';
        if (typeof t.marketRegime === 'number') t.marketRegime = regimeNames[t.marketRegime] ?? 'Transitional';
      });
    }
    return data;
  },
};
