import type {
  BacktestRequestDto,
  BacktestResultDto,
  DailyRiskStatusDto,
  MarketOverview,
  MarketRegimeResultDto,
  MultiTimeframeStateDto,
  OptionsReportDto,
  PaperAccountDto,
  PaperOrderDto,
  PaperPortfolioSummaryDto,
  PaperPositionDto,
  PaperTradeDto,
  StrategySignalDto,
} from '../types';

const API_BASE = '/api';

export const api = {
  // Market Data
  getMarketOverview: async (): Promise<MarketOverview> => {
    const res = await fetch(`${API_BASE}/market/overview`);
    if (!res.ok) throw new Error('Failed to fetch market overview');
    return res.json();
  },

  // Options Intelligence
  getOptionsIntelligence: async (underlyingKey = 'NSE_INDEX|Nifty 50'): Promise<OptionsReportDto> => {
    const res = await fetch(`${API_BASE}/options/intelligence?underlyingKey=${encodeURIComponent(underlyingKey)}`);
    if (!res.ok) throw new Error('Failed to fetch options intelligence');
    return res.json();
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
    return res.json();
  },

  getLatestSignal: async (underlyingKey = 'NSE_INDEX|Nifty 50'): Promise<StrategySignalDto> => {
    const res = await fetch(`${API_BASE}/strategy/latest-signal?underlyingKey=${encodeURIComponent(underlyingKey)}`);
    if (!res.ok) throw new Error('No recorded strategy signal available');
    return res.json();
  },

  getSignalHistory: async (underlyingKey = 'NSE_INDEX|Nifty 50', limit = 20): Promise<StrategySignalDto[]> => {
    const res = await fetch(`${API_BASE}/strategy/history?underlyingKey=${encodeURIComponent(underlyingKey)}&limit=${limit}`);
    if (!res.ok) throw new Error('Failed to fetch signal history');
    return res.json();
  },

  getMarketRegime: async (underlyingKey = 'NSE_INDEX|Nifty 50'): Promise<MarketRegimeResultDto> => {
    const res = await fetch(`${API_BASE}/strategy/regime?underlyingKey=${encodeURIComponent(underlyingKey)}`);
    if (!res.ok) throw new Error('Failed to fetch market regime');
    return res.json();
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
    return res.json();
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
    return res.json();
  },
};
