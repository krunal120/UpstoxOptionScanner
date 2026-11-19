import React, { useState } from 'react';
import {
  Activity,
  ArrowDown,
  ArrowUp,
  BarChart2,
  CheckCircle2,
  Clock,
  Layers,
  RefreshCw,
  Sliders,
  TrendingDown,
  TrendingUp,
} from 'lucide-react';
import type { CprResultDto, IndicatorSnapshotDto, MultiTimeframeStateDto } from '../types';

interface IndicatorsTabProps {
  mtfState: MultiTimeframeStateDto | null;
  onRefresh: () => void;
}

export const IndicatorsTab: React.FC<IndicatorsTabProps> = ({ mtfState, onRefresh }) => {
  const [selectedTf, setSelectedTf] = useState<'1m' | '5m' | '15m'>('5m');
  const [isRefreshing, setIsRefreshing] = useState(false);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await onRefresh();
    setIsRefreshing(false);
  };

  const getSnapshot = (tf: '1m' | '5m' | '15m'): IndicatorSnapshotDto | undefined => {
    switch (tf) {
      case '1m':
        return mtfState?.timeframe1m;
      case '5m':
        return mtfState?.timeframe5m;
      case '15m':
        return mtfState?.timeframe15m;
    }
  };

  const currentSnapshot = getSnapshot(selectedTf);
  const cpr = mtfState?.dailyCpr;

  const getRsiColor = (rsi?: number) => {
    if (!rsi) return 'var(--text-muted)';
    if (rsi >= 70) return 'var(--color-red)';
    if (rsi <= 30) return 'var(--color-green)';
    if (rsi >= 60) return '#0ecb81';
    if (rsi <= 40) return '#f6465d';
    return 'var(--text-primary)';
  };

  const getAdxColor = (adx?: number) => {
    if (!adx) return 'var(--text-muted)';
    if (adx >= 40) return 'var(--color-purple)';
    if (adx >= 25) return 'var(--color-green)';
    return 'var(--text-muted)';
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Top Banner: Multi-Timeframe Alignment & CPR Quick Stats */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
          gap: '16px',
        }}
      >
        {/* Trend Alignment Card */}
        <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>MULTI-TIMEFRAME ALIGNMENT</span>
            <Layers size={16} color="var(--color-blue)" />
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            {mtfState?.isTrendAlignedBullish ? (
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                  padding: '6px 12px',
                  borderRadius: '6px',
                  backgroundColor: 'rgba(14, 203, 129, 0.15)',
                  color: 'var(--color-green)',
                  fontWeight: 700,
                  fontSize: '0.9rem',
                }}
              >
                <ArrowUp size={16} /> Bullish Alignment (1m + 5m + 15m)
              </div>
            ) : mtfState?.isTrendAlignedBearish ? (
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                  padding: '6px 12px',
                  borderRadius: '6px',
                  backgroundColor: 'rgba(246, 70, 93, 0.15)',
                  color: 'var(--color-red)',
                  fontWeight: 700,
                  fontSize: '0.9rem',
                }}
              >
                <ArrowDown size={16} /> Bearish Alignment (1m + 5m + 15m)
              </div>
            ) : (
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '6px',
                  padding: '6px 12px',
                  borderRadius: '6px',
                  backgroundColor: 'rgba(245, 158, 11, 0.15)',
                  color: 'var(--color-amber)',
                  fontWeight: 700,
                  fontSize: '0.9rem',
                }}
              >
                <Activity size={16} /> Mixed / Transitional Alignment
              </div>
            )}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
            Instrument: <strong style={{ color: 'var(--text-primary)' }}>{mtfState?.instrumentKey || 'NSE_INDEX|Nifty 50'}</strong>
          </div>
        </div>

        {/* CPR Summary Card */}
        <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>DAILY CPR & RANGE</span>
            {cpr?.isNarrowCpr && (
              <span
                style={{
                  fontSize: '0.7rem',
                  fontWeight: 700,
                  padding: '2px 8px',
                  borderRadius: '4px',
                  backgroundColor: 'rgba(168, 85, 247, 0.2)',
                  color: 'var(--color-purple)',
                  border: '1px solid var(--color-purple)',
                }}
              >
                NARROW CPR (Trending Expected)
              </span>
            )}
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '16px' }}>
            <div>
              <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Pivot (P)</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, fontFamily: 'JetBrains Mono' }}>
                ₹{cpr ? cpr.pivot.toFixed(1) : '-'}
              </div>
            </div>
            <div>
              <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>TC - BC Width</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-blue)' }}>
                {cpr ? `${cpr.cprWidthPercent.toFixed(2)}%` : '-'}
              </div>
            </div>
            <div>
              <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>CPR Range</div>
              <div style={{ fontSize: '0.85rem', fontFamily: 'JetBrains Mono', color: 'var(--text-secondary)' }}>
                {cpr ? `₹${cpr.bottomCentralPivot.toFixed(0)} - ₹${cpr.topCentralPivot.toFixed(0)}` : '-'}
              </div>
            </div>
          </div>
        </div>

        {/* Timeframe Switcher & Manual Refresh */}
        <div className="card" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>ACTIVE TIMEFRAME</span>
            <button
              onClick={handleRefresh}
              disabled={isRefreshing}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '6px',
                padding: '4px 8px',
                fontSize: '0.75rem',
                backgroundColor: 'transparent',
                border: '1px solid var(--border-color)',
                borderRadius: '4px',
                color: 'var(--text-secondary)',
                cursor: 'pointer',
              }}
            >
              <RefreshCw size={13} className={isRefreshing ? 'spin' : ''} /> Refresh
            </button>
          </div>
          <div style={{ display: 'flex', gap: '8px', marginTop: '10px' }}>
            {(['1m', '5m', '15m'] as const).map((tf) => (
              <button
                key={tf}
                onClick={() => setSelectedTf(tf)}
                style={{
                  flex: 1,
                  padding: '8px 12px',
                  borderRadius: '6px',
                  fontWeight: 700,
                  fontSize: '0.85rem',
                  cursor: 'pointer',
                  border: selectedTf === tf ? '1px solid var(--color-blue)' : '1px solid var(--border-color)',
                  backgroundColor: selectedTf === tf ? 'rgba(59, 130, 246, 0.15)' : 'var(--bg-secondary)',
                  color: selectedTf === tf ? 'var(--color-blue)' : 'var(--text-secondary)',
                }}
              >
                {tf.toUpperCase()}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Main Indicator Breakdown Grid */}
      {currentSnapshot ? (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
            gap: '16px',
          }}
        >
          {/* Moving Averages (EMAs) */}
          <div className="card">
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 600 }}>
                <TrendingUp size={16} color="var(--color-blue)" />
                Exponential Moving Averages
              </div>
              <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>TF: {selectedTf}</span>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>EMA 9 (Fast)</div>
                <div style={{ fontSize: '1.2rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-green)' }}>
                  ₹{currentSnapshot.ema9 ? currentSnapshot.ema9.toFixed(2) : '-'}
                </div>
              </div>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>EMA 21 (Medium)</div>
                <div style={{ fontSize: '1.2rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-blue)' }}>
                  ₹{currentSnapshot.ema21 ? currentSnapshot.ema21.toFixed(2) : '-'}
                </div>
              </div>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>EMA 50 (Trend)</div>
                <div style={{ fontSize: '1.2rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-amber)' }}>
                  ₹{currentSnapshot.ema50 ? currentSnapshot.ema50.toFixed(2) : '-'}
                </div>
              </div>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>EMA 200 (Baseline)</div>
                <div style={{ fontSize: '1.2rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-purple)' }}>
                  ₹{currentSnapshot.ema200 ? currentSnapshot.ema200.toFixed(2) : '-'}
                </div>
              </div>
            </div>
            <div style={{ marginTop: '12px', fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
              Stack Order:{' '}
              {currentSnapshot.ema9 && currentSnapshot.ema21 && currentSnapshot.ema50 && (
                <span
                  style={{
                    fontWeight: 600,
                    color:
                      currentSnapshot.ema9 > currentSnapshot.ema21 && currentSnapshot.ema21 > currentSnapshot.ema50
                        ? 'var(--color-green)'
                        : currentSnapshot.ema9 < currentSnapshot.ema21 && currentSnapshot.ema21 < currentSnapshot.ema50
                        ? 'var(--color-red)'
                        : 'var(--color-amber)',
                  }}
                >
                  {currentSnapshot.ema9 > currentSnapshot.ema21 && currentSnapshot.ema21 > currentSnapshot.ema50
                    ? 'Bullish Stack (9 > 21 > 50)'
                    : currentSnapshot.ema9 < currentSnapshot.ema21 && currentSnapshot.ema21 < currentSnapshot.ema50
                    ? 'Bearish Stack (9 < 21 < 50)'
                    : 'Mixed / Consolidating'}
                </span>
              )}
            </div>
          </div>

          {/* VWAP & Supertrend */}
          <div className="card">
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 600 }}>
                <Activity size={16} color="var(--color-amber)" />
                VWAP & Supertrend (10, 3.0)
              </div>
              <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Intraday Benchmarks</span>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {/* VWAP */}
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  padding: '12px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div>
                  <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Session VWAP</div>
                  <div style={{ fontSize: '1.25rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-blue)' }}>
                    ₹{currentSnapshot.vwap ? currentSnapshot.vwap.toFixed(2) : '-'}
                  </div>
                </div>
                <span
                  style={{
                    padding: '4px 10px',
                    borderRadius: '4px',
                    fontSize: '0.75rem',
                    fontWeight: 600,
                    backgroundColor: 'rgba(59, 130, 246, 0.1)',
                    color: 'var(--color-blue)',
                  }}
                >
                  Anchor Level
                </span>
              </div>

              {/* Supertrend */}
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  padding: '12px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div>
                  <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Supertrend (10, 3.0)</div>
                  <div style={{ fontSize: '1.25rem', fontWeight: 700, fontFamily: 'JetBrains Mono' }}>
                    ₹{currentSnapshot.supertrend ? currentSnapshot.supertrend.toFixed(2) : '-'}
                  </div>
                </div>
                <div
                  style={{
                    padding: '4px 10px',
                    borderRadius: '4px',
                    fontSize: '0.8rem',
                    fontWeight: 700,
                    backgroundColor: currentSnapshot.supertrendIsBullish
                      ? 'rgba(14, 203, 129, 0.15)'
                      : 'rgba(246, 70, 93, 0.15)',
                    color: currentSnapshot.supertrendIsBullish ? 'var(--color-green)' : 'var(--color-red)',
                  }}
                >
                  {currentSnapshot.supertrendIsBullish ? 'BULLISH' : 'BEARISH'}
                </div>
              </div>
            </div>
          </div>

          {/* RSI (14) & Relative Volume */}
          <div className="card">
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 600 }}>
                <Sliders size={16} color="var(--color-green)" />
                RSI (14 Wilder) & Volume
              </div>
              <span
                style={{
                  fontSize: '0.85rem',
                  fontWeight: 700,
                  fontFamily: 'JetBrains Mono',
                  color: getRsiColor(currentSnapshot.rsi14),
                }}
              >
                {currentSnapshot.rsi14 ? currentSnapshot.rsi14.toFixed(1) : '-'}
              </span>
            </div>

            {/* Visual RSI Gauge Bar */}
            <div style={{ marginBottom: '16px' }}>
              <div
                style={{
                  position: 'relative',
                  height: '10px',
                  borderRadius: '5px',
                  background: 'linear-gradient(to right, #0ecb81 0%, #2b313a 30%, #2b313a 70%, #f6465d 100%)',
                  overflow: 'hidden',
                }}
              >
                {currentSnapshot.rsi14 && (
                  <div
                    style={{
                      position: 'absolute',
                      top: 0,
                      bottom: 0,
                      left: `${Math.min(100, Math.max(0, currentSnapshot.rsi14))}%`,
                      width: '4px',
                      backgroundColor: '#fff',
                      boxShadow: '0 0 6px rgba(255,255,255,0.8)',
                    }}
                  />
                )}
              </div>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  fontSize: '0.7rem',
                  color: 'var(--text-muted)',
                  marginTop: '4px',
                }}
              >
                <span>30 Oversold</span>
                <span>50 Neutral</span>
                <span>70 Overbought</span>
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Relative Volume (RVOL)</div>
                <div
                  style={{
                    fontSize: '1.1rem',
                    fontWeight: 700,
                    fontFamily: 'JetBrains Mono',
                    color: (currentSnapshot.relativeVolume ?? 1) >= 1.5 ? 'var(--color-green)' : 'var(--text-primary)',
                  }}
                >
                  {currentSnapshot.relativeVolume ? `${currentSnapshot.relativeVolume.toFixed(2)}x` : '-'}
                </div>
              </div>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>ATR (14)</div>
                <div style={{ fontSize: '1.1rem', fontWeight: 700, fontFamily: 'JetBrains Mono' }}>
                  {currentSnapshot.atr14 ? `₹${currentSnapshot.atr14.toFixed(2)}` : '-'}
                </div>
              </div>
            </div>
          </div>

          {/* MACD (12, 26, 9) */}
          <div className="card">
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 600 }}>
                <BarChart2 size={16} color="var(--color-blue)" />
                MACD (12, 26, 9)
              </div>
              <span
                style={{
                  fontSize: '0.8rem',
                  fontWeight: 600,
                  color: (currentSnapshot.macdHistogram ?? 0) >= 0 ? 'var(--color-green)' : 'var(--color-red)',
                }}
              >
                {(currentSnapshot.macdHistogram ?? 0) >= 0 ? 'Histogram Positive' : 'Histogram Negative'}
              </span>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px' }}>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>MACD Line</div>
                <div style={{ fontSize: '1.05rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-blue)' }}>
                  {currentSnapshot.macd ? currentSnapshot.macd.toFixed(2) : '-'}
                </div>
              </div>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Signal Line</div>
                <div style={{ fontSize: '1.05rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-amber)' }}>
                  {currentSnapshot.macdSignal ? currentSnapshot.macdSignal.toFixed(2) : '-'}
                </div>
              </div>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Histogram</div>
                <div
                  style={{
                    fontSize: '1.05rem',
                    fontWeight: 700,
                    fontFamily: 'JetBrains Mono',
                    color: (currentSnapshot.macdHistogram ?? 0) >= 0 ? 'var(--color-green)' : 'var(--color-red)',
                  }}
                >
                  {currentSnapshot.macdHistogram ? currentSnapshot.macdHistogram.toFixed(2) : '-'}
                </div>
              </div>
            </div>
          </div>

          {/* ADX & Directional Movement */}
          <div className="card">
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 600 }}>
                <TrendingDown size={16} color="var(--color-purple)" />
                ADX (14) & Trend Strength
              </div>
              <span
                style={{
                  fontSize: '0.85rem',
                  fontWeight: 700,
                  fontFamily: 'JetBrains Mono',
                  color: getAdxColor(currentSnapshot.adx14),
                }}
              >
                ADX: {currentSnapshot.adx14 ? currentSnapshot.adx14.toFixed(1) : '-'}
              </span>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px' }}>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>+DI (Bullish)</div>
                <div style={{ fontSize: '1.05rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-green)' }}>
                  {currentSnapshot.plusDi ? currentSnapshot.plusDi.toFixed(1) : '-'}
                </div>
              </div>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>-DI (Bearish)</div>
                <div style={{ fontSize: '1.05rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-red)' }}>
                  {currentSnapshot.minusDi ? currentSnapshot.minusDi.toFixed(1) : '-'}
                </div>
              </div>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Strength</div>
                <div style={{ fontSize: '0.85rem', fontWeight: 700, marginTop: '4px' }}>
                  {(currentSnapshot.adx14 ?? 0) >= 25 ? 'Trending' : 'Non-Trending'}
                </div>
              </div>
            </div>
          </div>

          {/* Bollinger Bands (20, 2.0) */}
          <div className="card">
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 600 }}>
                <Layers size={16} color="var(--color-amber)" />
                Bollinger Bands (20, 2.0)
              </div>
              <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Volatility Envelope</span>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px' }}>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Upper Band</div>
                <div style={{ fontSize: '1.05rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-red)' }}>
                  ₹{currentSnapshot.bollingerUpper ? currentSnapshot.bollingerUpper.toFixed(1) : '-'}
                </div>
              </div>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Middle (20 SMA)</div>
                <div style={{ fontSize: '1.05rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-amber)' }}>
                  ₹{currentSnapshot.bollingerMiddle ? currentSnapshot.bollingerMiddle.toFixed(1) : '-'}
                </div>
              </div>
              <div
                style={{
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Lower Band</div>
                <div style={{ fontSize: '1.05rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-green)' }}>
                  ₹{currentSnapshot.bollingerLower ? currentSnapshot.bollingerLower.toFixed(1) : '-'}
                </div>
              </div>
            </div>
          </div>
        </div>
      ) : (
        <div className="card" style={{ textAlign: 'center', padding: '40px', color: 'var(--text-secondary)' }}>
          No indicator snapshot data available for {selectedTf} timeframe. Ensure market data feed is active.
        </div>
      )}

      {/* CPR Level Reference Matrix */}
      {cpr && (
        <div className="card">
          <div style={{ fontSize: '0.9rem', fontWeight: 700, marginBottom: '14px', color: 'var(--text-primary)' }}>
            Daily CPR & Pivot Support / Resistance Levels
          </div>
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))',
              gap: '12px',
            }}
          >
            <div style={{ padding: '10px', borderRadius: '6px', backgroundColor: 'var(--bg-secondary)', textAlign: 'center' }}>
              <div style={{ fontSize: '0.75rem', color: 'var(--color-red)' }}>Resistance 3 (R3)</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, fontFamily: 'JetBrains Mono' }}>₹{cpr.r3.toFixed(1)}</div>
            </div>
            <div style={{ padding: '10px', borderRadius: '6px', backgroundColor: 'var(--bg-secondary)', textAlign: 'center' }}>
              <div style={{ fontSize: '0.75rem', color: 'var(--color-red)' }}>Resistance 2 (R2)</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, fontFamily: 'JetBrains Mono' }}>₹{cpr.r2.toFixed(1)}</div>
            </div>
            <div style={{ padding: '10px', borderRadius: '6px', backgroundColor: 'var(--bg-secondary)', textAlign: 'center' }}>
              <div style={{ fontSize: '0.75rem', color: 'var(--color-red)' }}>Resistance 1 (R1)</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, fontFamily: 'JetBrains Mono' }}>₹{cpr.r1.toFixed(1)}</div>
            </div>
            <div
              style={{
                padding: '10px',
                borderRadius: '6px',
                backgroundColor: 'rgba(59, 130, 246, 0.15)',
                border: '1px solid var(--color-blue)',
                textAlign: 'center',
              }}
            >
              <div style={{ fontSize: '0.75rem', color: 'var(--color-blue)' }}>Central Pivot (P)</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-blue)' }}>
                ₹{cpr.pivot.toFixed(1)}
              </div>
            </div>
            <div style={{ padding: '10px', borderRadius: '6px', backgroundColor: 'var(--bg-secondary)', textAlign: 'center' }}>
              <div style={{ fontSize: '0.75rem', color: 'var(--color-green)' }}>Support 1 (S1)</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, fontFamily: 'JetBrains Mono' }}>₹{cpr.s1.toFixed(1)}</div>
            </div>
            <div style={{ padding: '10px', borderRadius: '6px', backgroundColor: 'var(--bg-secondary)', textAlign: 'center' }}>
              <div style={{ fontSize: '0.75rem', color: 'var(--color-green)' }}>Support 2 (S2)</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, fontFamily: 'JetBrains Mono' }}>₹{cpr.s2.toFixed(1)}</div>
            </div>
            <div style={{ padding: '10px', borderRadius: '6px', backgroundColor: 'var(--bg-secondary)', textAlign: 'center' }}>
              <div style={{ fontSize: '0.75rem', color: 'var(--color-green)' }}>Support 3 (S3)</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, fontFamily: 'JetBrains Mono' }}>₹{cpr.s3.toFixed(1)}</div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
