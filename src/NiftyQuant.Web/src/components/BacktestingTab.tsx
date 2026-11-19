import React, { useState } from 'react';
import {
  Activity,
  AlertTriangle,
  ArrowDownRight,
  ArrowUpRight,
  BarChart2,
  Calendar,
  CheckCircle2,
  LineChart,
  Play,
  RotateCcw,
  Shield,
  TrendingDown,
  TrendingUp,
} from 'lucide-react';
import { api } from '../services/api';
import type { BacktestRequestDto, BacktestResultDto } from '../types';

export const BacktestingTab: React.FC = () => {
  const today = new Date();
  const past30Days = new Date(today);
  past30Days.setDate(today.getDate() - 30);

  const [form, setForm] = useState<BacktestRequestDto>({
    underlyingKey: 'NSE_INDEX|Nifty 50',
    fromDate: past30Days.toISOString().split('T')[0],
    toDate: today.toISOString().split('T')[0],
    initialCapital: 50000,
    riskPerTradePercent: 0.5,
    maxDailyLossPercent: 1.5,
    maxDailyTrades: 6,
    maxConsecutiveLosses: 2,
    minRewardRiskRatio: 2.0,
    simulateSlippage: true,
    slippagePercent: 0.2,
    enableTrailingStop: true,
    targetStrategy: undefined,
  });

  const [isRunning, setIsRunning] = useState(false);
  const [result, setResult] = useState<BacktestResultDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleRun = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsRunning(true);
    setError(null);

    try {
      const res = await api.runBacktest(form);
      setResult(res);
    } catch (err: any) {
      setError(err.message || 'Backtest failed to run');
    } finally {
      setIsRunning(false);
    }
  };

  // Helper for rendering SVG Equity Curve
  const renderEquityCurve = (equityCurve: BacktestResultDto['equityCurve']) => {
    if (!equityCurve || equityCurve.length < 2) {
      return (
        <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
          Not enough equity data points to plot curve.
        </div>
      );
    }

    const width = 800;
    const height = 260;
    const padding = 40;

    const values = equityCurve.map((p) => p.capital);
    const minVal = Math.min(...values) * 0.98;
    const maxVal = Math.max(...values) * 1.02;
    const range = maxVal - minVal || 1;

    const points = equityCurve.map((p, idx) => {
      const x = padding + (idx / (equityCurve.length - 1)) * (width - 2 * padding);
      const y = height - padding - ((p.capital - minVal) / range) * (height - 2 * padding);
      return `${x},${y}`;
    });

    const isProfitable = (result?.totalNetPnL ?? 0) >= 0;
    const strokeColor = isProfitable ? '#0ecb81' : '#f6465d';
    const gradientId = isProfitable ? 'greenGrad' : 'redGrad';

    // Build fill path
    const firstX = padding;
    const lastX = width - padding;
    const bottomY = height - padding;
    const fillPath = `M ${points[0]} L ${points.join(' L ')} L ${lastX},${bottomY} L ${firstX},${bottomY} Z`;

    return (
      <div style={{ width: '100%', overflowX: 'auto' }}>
        <svg viewBox={`0 0 ${width} ${height}`} style={{ width: '100%', height: 'auto', display: 'block' }}>
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={strokeColor} stopOpacity="0.3" />
              <stop offset="100%" stopColor={strokeColor} stopOpacity="0.0" />
            </linearGradient>
          </defs>

          {/* Grid lines */}
          <line
            x1={padding}
            y1={padding}
            x2={width - padding}
            y2={padding}
            stroke="var(--border-color)"
            strokeDasharray="4 4"
          />
          <line
            x1={padding}
            y1={(height - 2 * padding) / 2 + padding}
            x2={width - padding}
            y2={(height - 2 * padding) / 2 + padding}
            stroke="var(--border-color)"
            strokeDasharray="4 4"
          />
          <line
            x1={padding}
            y1={height - padding}
            x2={width - padding}
            y2={height - padding}
            stroke="var(--border-color)"
          />

          {/* Area Fill */}
          <path d={fillPath} fill={`url(#${gradientId})`} />

          {/* Line Chart */}
          <polyline fill="none" stroke={strokeColor} strokeWidth="2.5" strokeLinecap="round" points={points.join(' ')} />

          {/* Start Point */}
          <circle
            cx={padding}
            cy={height - padding - ((equityCurve[0].capital - minVal) / range) * (height - 2 * padding)}
            r="4"
            fill="#3b82f6"
          />

          {/* End Point */}
          <circle
            cx={width - padding}
            cy={height - padding - ((equityCurve[equityCurve.length - 1].capital - minVal) / range) * (height - 2 * padding)}
            r="5"
            fill={strokeColor}
          />

          {/* Labels */}
          <text x={padding} y={padding - 10} fill="var(--text-muted)" fontSize="11" fontFamily="JetBrains Mono">
            ₹{maxVal.toFixed(0)}
          </text>
          <text x={padding} y={height - padding + 20} fill="var(--text-muted)" fontSize="11" fontFamily="JetBrains Mono">
            ₹{minVal.toFixed(0)} (Min)
          </text>
          <text
            x={width - padding}
            y={height - padding + 20}
            fill={strokeColor}
            fontSize="11"
            fontFamily="JetBrains Mono"
            textAnchor="end"
            fontWeight="bold"
          >
            ₹{equityCurve[equityCurve.length - 1].capital.toFixed(2)}
          </text>
        </svg>
      </div>
    );
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Parameter Control Form Card */}
      <div className="card">
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 700, fontSize: '1rem' }}>
            <Calendar size={18} color="var(--color-blue)" />
            Backtest Simulation Engine
          </div>
          <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Multi-Day Historical Simulation</span>
        </div>

        <form onSubmit={handleRun}>
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
              gap: '14px',
            }}
          >
            {/* From Date */}
            <div>
              <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '4px' }}>
                From Date
              </label>
              <input
                type="date"
                value={form.fromDate}
                onChange={(e) => setForm({ ...form, fromDate: e.target.value })}
                required
                style={{
                  width: '100%',
                  padding: '8px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                  color: 'var(--text-primary)',
                  fontSize: '0.85rem',
                }}
              />
            </div>

            {/* To Date */}
            <div>
              <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '4px' }}>
                To Date
              </label>
              <input
                type="date"
                value={form.toDate}
                onChange={(e) => setForm({ ...form, toDate: e.target.value })}
                required
                style={{
                  width: '100%',
                  padding: '8px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                  color: 'var(--text-primary)',
                  fontSize: '0.85rem',
                }}
              />
            </div>

            {/* Initial Capital */}
            <div>
              <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '4px' }}>
                Initial Capital (₹)
              </label>
              <input
                type="number"
                value={form.initialCapital}
                onChange={(e) => setForm({ ...form, initialCapital: parseFloat(e.target.value) || 50000 })}
                style={{
                  width: '100%',
                  padding: '8px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                  color: 'var(--text-primary)',
                  fontSize: '0.85rem',
                  fontFamily: 'JetBrains Mono',
                }}
              />
            </div>

            {/* Risk Per Trade */}
            <div>
              <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '4px' }}>
                Risk / Trade (%)
              </label>
              <input
                type="number"
                step="0.1"
                value={form.riskPerTradePercent}
                onChange={(e) => setForm({ ...form, riskPerTradePercent: parseFloat(e.target.value) || 0.5 })}
                style={{
                  width: '100%',
                  padding: '8px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                  color: 'var(--text-primary)',
                  fontSize: '0.85rem',
                  fontFamily: 'JetBrains Mono',
                }}
              />
            </div>

            {/* Max Daily Loss */}
            <div>
              <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '4px' }}>
                Max Daily Loss (%)
              </label>
              <input
                type="number"
                step="0.1"
                value={form.maxDailyLossPercent}
                onChange={(e) => setForm({ ...form, maxDailyLossPercent: parseFloat(e.target.value) || 1.5 })}
                style={{
                  width: '100%',
                  padding: '8px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                  color: 'var(--text-primary)',
                  fontSize: '0.85rem',
                  fontFamily: 'JetBrains Mono',
                }}
              />
            </div>

            {/* Strategy Filter */}
            <div>
              <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '4px' }}>
                Target Strategy
              </label>
              <select
                value={form.targetStrategy ?? ''}
                onChange={(e) =>
                  setForm({
                    ...form,
                    targetStrategy: e.target.value ? parseInt(e.target.value) : undefined,
                  })
                }
                style={{
                  width: '100%',
                  padding: '8px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                  color: 'var(--text-primary)',
                  fontSize: '0.85rem',
                }}
              >
                <option value="">All Strategies (Ensemble)</option>
                <option value="1">Trend Following</option>
                <option value="2">Breakout</option>
                <option value="3">Reversal</option>
              </select>
            </div>
          </div>

          {/* Checkboxes & Action Button */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              flexWrap: 'wrap',
              gap: '16px',
              marginTop: '16px',
              paddingTop: '16px',
              borderTop: '1px solid var(--border-color)',
            }}
          >
            <div style={{ display: 'flex', gap: '20px', alignItems: 'center' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '0.8rem', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={form.simulateSlippage}
                  onChange={(e) => setForm({ ...form, simulateSlippage: e.target.checked })}
                />
                Simulate 0.2% Slippage
              </label>
              <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '0.8rem', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={form.enableTrailingStop}
                  onChange={(e) => setForm({ ...form, enableTrailingStop: e.target.checked })}
                />
                Enable Trailing Stop (Breakeven @ 1.5R)
              </label>
            </div>

            <button
              type="submit"
              disabled={isRunning}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                padding: '10px 24px',
                borderRadius: '6px',
                backgroundColor: 'var(--color-blue)',
                border: 'none',
                color: '#fff',
                fontWeight: 700,
                fontSize: '0.85rem',
                cursor: isRunning ? 'not-allowed' : 'pointer',
                opacity: isRunning ? 0.7 : 1,
              }}
            >
              <Play size={16} /> {isRunning ? 'Running Backtest...' : 'Execute Backtest'}
            </button>
          </div>
        </form>
      </div>

      {/* Error Message */}
      {error && (
        <div
          style={{
            padding: '12px 16px',
            borderRadius: '8px',
            display: 'flex',
            alignItems: 'center',
            gap: '10px',
            backgroundColor: 'rgba(246, 70, 93, 0.15)',
            border: '1px solid var(--color-red)',
            color: 'var(--color-red)',
            fontSize: '0.85rem',
          }}
        >
          <AlertTriangle size={18} />
          <span>{error}</span>
        </div>
      )}

      {/* Results Section */}
      {result && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          {/* Key Scorecard Cards */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
              gap: '14px',
            }}
          >
            {/* Total Net PnL */}
            <div className="card">
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>NET RETURN</div>
              <div
                style={{
                  fontSize: '1.4rem',
                  fontWeight: 800,
                  fontFamily: 'JetBrains Mono',
                  marginTop: '4px',
                  color: result.totalNetPnL >= 0 ? 'var(--color-green)' : 'var(--color-red)',
                }}
              >
                {result.totalNetPnL >= 0 ? '+' : ''}₹{result.totalNetPnL.toFixed(2)} (
                {result.totalReturnPercent.toFixed(2)}%)
              </div>
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                Final Capital: ₹{result.finalCapital.toFixed(2)}
              </div>
            </div>

            {/* Win Rate */}
            <div className="card">
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>WIN RATE</div>
              <div
                style={{
                  fontSize: '1.4rem',
                  fontWeight: 800,
                  fontFamily: 'JetBrains Mono',
                  marginTop: '4px',
                  color: result.winRatePercent >= 50 ? 'var(--color-green)' : 'var(--color-amber)',
                }}
              >
                {result.winRatePercent.toFixed(1)}%
              </div>
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                Wins: {result.winningTrades} / Losses: {result.losingTrades} (Total: {result.totalTrades})
              </div>
            </div>

            {/* Profit Factor */}
            <div className="card">
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>PROFIT FACTOR</div>
              <div
                style={{
                  fontSize: '1.4rem',
                  fontWeight: 800,
                  fontFamily: 'JetBrains Mono',
                  marginTop: '4px',
                  color: result.profitFactor >= 1.5 ? 'var(--color-green)' : 'var(--color-amber)',
                }}
              >
                {result.profitFactor.toFixed(2)}
              </div>
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                Avg Win/Loss: ₹{result.averageWin.toFixed(0)} / ₹{result.averageLoss.toFixed(0)}
              </div>
            </div>

            {/* Sharpe & Sortino */}
            <div className="card">
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>RISK-ADJUSTED METRICS</div>
              <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px', marginTop: '4px' }}>
                <span style={{ fontSize: '1.3rem', fontWeight: 800, fontFamily: 'JetBrains Mono', color: 'var(--color-blue)' }}>
                  {result.sharpeRatio.toFixed(2)}
                </span>
                <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>Sharpe</span>
                <span
                  style={{
                    fontSize: '1.3rem',
                    fontWeight: 800,
                    fontFamily: 'JetBrains Mono',
                    color: 'var(--color-purple)',
                    marginLeft: '8px',
                  }}
                >
                  {result.sortinoRatio.toFixed(2)}
                </span>
                <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>Sortino</span>
              </div>
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                Expectancy: ₹{result.mathematicalExpectancy.toFixed(2)}
              </div>
            </div>

            {/* Max Drawdown */}
            <div className="card">
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>MAX DRAWDOWN</div>
              <div
                style={{
                  fontSize: '1.4rem',
                  fontWeight: 800,
                  fontFamily: 'JetBrains Mono',
                  marginTop: '4px',
                  color: 'var(--color-red)',
                }}
              >
                -₹{result.maxDrawdownAmount.toFixed(2)} ({result.maxDrawdownPercent.toFixed(1)}%)
              </div>
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                Breaches: {result.dailyLossLimitBreaches} | Days: {result.totalTradingDays}
              </div>
            </div>
          </div>

          {/* Equity Curve SVG Chart */}
          <div className="card">
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '14px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 700 }}>
                <LineChart size={18} color="var(--color-blue)" />
                Portfolio Equity Curve
              </div>
              <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                {result.equityCurve.length} Simulation Checkpoints
              </span>
            </div>
            {renderEquityCurve(result.equityCurve)}
          </div>

          {/* Trade Log Table */}
          <div className="card" style={{ overflowX: 'auto' }}>
            <div style={{ fontSize: '0.9rem', fontWeight: 700, marginBottom: '12px' }}>
              Simulated Execution Trade Log ({result.trades.length} Trades)
            </div>
            {result.trades.length === 0 ? (
              <div style={{ textAlign: 'center', padding: '24px', color: 'var(--text-secondary)' }}>
                No trades met the multi-factor entry filter during this period.
              </div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)' }}>
                    <th style={{ padding: '8px' }}>#</th>
                    <th style={{ padding: '8px' }}>Strategy</th>
                    <th style={{ padding: '8px' }}>Dir</th>
                    <th style={{ padding: '8px' }}>Entry Time</th>
                    <th style={{ padding: '8px' }}>Entry</th>
                    <th style={{ padding: '8px' }}>Exit</th>
                    <th style={{ padding: '8px' }}>Net PnL</th>
                    <th style={{ padding: '8px' }}>Return</th>
                    <th style={{ padding: '8px' }}>Exit Reason</th>
                    <th style={{ padding: '8px' }}>Capital</th>
                  </tr>
                </thead>
                <tbody>
                  {result.trades.map((t) => {
                    const isWin = t.netPnL >= 0;
                    return (
                      <tr key={t.tradeNumber} style={{ borderBottom: '1px solid var(--border-color)' }}>
                        <td style={{ padding: '8px', color: 'var(--text-muted)' }}>{t.tradeNumber}</td>
                        <td style={{ padding: '8px', fontWeight: 600 }}>{t.strategyFamily}</td>
                        <td style={{ padding: '8px' }}>
                          <span
                            style={{
                              padding: '2px 6px',
                              borderRadius: '4px',
                              fontSize: '0.75rem',
                              fontWeight: 700,
                              backgroundColor: t.direction.includes('Ce')
                                ? 'rgba(14, 203, 129, 0.2)'
                                : 'rgba(246, 70, 93, 0.2)',
                              color: t.direction.includes('Ce') ? 'var(--color-green)' : 'var(--color-red)',
                            }}
                          >
                            {t.direction.includes('Ce') ? 'CE' : 'PE'}
                          </span>
                        </td>
                        <td style={{ padding: '8px', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                          {new Date(t.entryTimeUtc).toLocaleDateString()}{' '}
                          {new Date(t.entryTimeUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                        </td>
                        <td style={{ padding: '8px', fontFamily: 'JetBrains Mono' }}>₹{t.entryPrice.toFixed(1)}</td>
                        <td style={{ padding: '8px', fontFamily: 'JetBrains Mono' }}>₹{t.exitPrice.toFixed(1)}</td>
                        <td
                          style={{
                            padding: '8px',
                            fontFamily: 'JetBrains Mono',
                            fontWeight: 700,
                            color: isWin ? 'var(--color-green)' : 'var(--color-red)',
                          }}
                        >
                          {isWin ? '+' : ''}₹{t.netPnL.toFixed(1)}
                        </td>
                        <td
                          style={{
                            padding: '8px',
                            fontFamily: 'JetBrains Mono',
                            fontWeight: 600,
                            color: isWin ? 'var(--color-green)' : 'var(--color-red)',
                          }}
                        >
                          {isWin ? '+' : ''}
                          {t.returnPercent.toFixed(1)}%
                        </td>
                        <td style={{ padding: '8px', fontSize: '0.75rem' }}>{t.exitReason}</td>
                        <td style={{ padding: '8px', fontFamily: 'JetBrains Mono', color: 'var(--text-secondary)' }}>
                          ₹{t.runningCapital.toFixed(0)}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
