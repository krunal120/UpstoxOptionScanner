import React, { useEffect, useState } from 'react';
import { Activity, ShieldAlert, Wifi, WifiOff } from 'lucide-react';
import type { MarketOverview, MarketRegimeResultDto } from '../types';

interface HeaderProps {
  overview: MarketOverview | null;
  regime: MarketRegimeResultDto | null;
  isSignalRConnected: boolean;
  onRefresh: () => void;
}

export const Header: React.FC<HeaderProps> = ({
  overview,
  regime,
  isSignalRConnected,
}) => {
  const [currentTime, setCurrentTime] = useState<string>('');

  useEffect(() => {
    const updateClock = () => {
      const now = new Date();
      // Format as IST
      const istString = now.toLocaleTimeString('en-IN', {
        timeZone: 'Asia/Kolkata',
        hour12: false,
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
      });
      setCurrentTime(istString);
    };

    updateClock();
    const timer = setInterval(updateClock, 1000);
    return () => clearInterval(timer);
  }, []);

  const spot = overview?.niftySpot;
  const isUp = (spot?.change ?? 0) >= 0;

  const bankSpot = overview?.bankNiftySpot;
  const isBankUp = (bankSpot?.change ?? 0) >= 0;

  const sensexSpot = overview?.sensexSpot;
  const isSensexUp = (sensexSpot?.change ?? 0) >= 0;

  const getRegimeColor = (regimeType?: string) => {
    switch (regimeType) {
      case 'StrongBullish':
        return { bg: 'rgba(14, 203, 129, 0.15)', text: '#0ecb81', border: '#0ecb81' };
      case 'StrongBearish':
        return { bg: 'rgba(246, 70, 93, 0.15)', text: '#f6465d', border: '#f6465d' };
      case 'Sideways':
        return { bg: 'rgba(245, 158, 11, 0.15)', text: '#f59e0b', border: '#f59e0b' };
      case 'HighVolatility':
        return { bg: 'rgba(168, 85, 247, 0.15)', text: '#a855f7', border: '#a855f7' };
      default:
        return { bg: 'rgba(154, 165, 190, 0.15)', text: '#9aa5be', border: '#5e6b88' };
    }
  };

  const regStyle = getRegimeColor(regime?.regime);

  return (
    <header
      style={{
        backgroundColor: 'var(--bg-secondary)',
        borderBottom: '1px solid var(--border-color)',
        padding: '12px 24px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexWrap: 'wrap',
        gap: '16px',
      }}
    >
      {/* Brand & Market Spot */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '20px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <div
            style={{
              width: '36px',
              height: '36px',
              borderRadius: '8px',
              backgroundColor: 'rgba(59, 130, 246, 0.2)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'var(--color-blue)',
            }}
          >
            <Activity size={20} />
          </div>
          <div>
            <div style={{ fontWeight: 800, fontSize: '1.1rem', letterSpacing: '-0.5px' }}>
              NIFTY<span style={{ color: 'var(--color-blue)' }}>QUANT</span>
            </div>
            <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>
              Quantitative Options Terminal
            </div>
          </div>
        </div>

        {/* NIFTY 50 Spot */}
        <div
          style={{
            display: 'flex',
            alignItems: 'baseline',
            gap: '8px',
            padding: '5px 12px',
            backgroundColor: 'var(--bg-card)',
            borderRadius: '6px',
            border: '1px solid var(--border-color)',
          }}
        >
          <span style={{ fontSize: '0.75rem', fontWeight: 600, color: 'var(--text-secondary)' }}>NIFTY</span>
          <span
            style={{
              fontSize: '1.1rem',
              fontWeight: 700,
              fontFamily: 'JetBrains Mono',
              color: isUp ? 'var(--color-green)' : 'var(--color-red)',
            }}
          >
            {spot ? spot.lastPrice.toFixed(2) : 'Loading...'}
          </span>
          {spot && (
            <span
              style={{
                fontSize: '0.75rem',
                fontWeight: 600,
                color: isUp ? 'var(--color-green)' : 'var(--color-red)',
              }}
            >
              {isUp ? '+' : ''}
              {spot.change.toFixed(1)} ({isUp ? '+' : ''}
              {spot.changePercent.toFixed(2)}%)
            </span>
          )}
        </div>

        {/* BANK NIFTY Spot */}
        {bankSpot && (
          <div
            style={{
              display: 'flex',
              alignItems: 'baseline',
              gap: '8px',
              padding: '5px 12px',
              backgroundColor: 'var(--bg-card)',
              borderRadius: '6px',
              border: '1px solid var(--border-color)',
            }}
          >
            <span style={{ fontSize: '0.75rem', fontWeight: 600, color: 'var(--text-secondary)' }}>BANKNIFTY</span>
            <span
              style={{
                fontSize: '1.1rem',
                fontWeight: 700,
                fontFamily: 'JetBrains Mono',
                color: isBankUp ? 'var(--color-green)' : 'var(--color-red)',
              }}
            >
              {bankSpot.lastPrice.toFixed(2)}
            </span>
            <span
              style={{
                fontSize: '0.75rem',
                fontWeight: 600,
                color: isBankUp ? 'var(--color-green)' : 'var(--color-red)',
              }}
            >
              {isBankUp ? '+' : ''}
              {bankSpot.change.toFixed(1)} ({isBankUp ? '+' : ''}
              {bankSpot.changePercent.toFixed(2)}%)
            </span>
          </div>
        )}

        {/* SENSEX Spot */}
        {sensexSpot && (
          <div
            style={{
              display: 'flex',
              alignItems: 'baseline',
              gap: '8px',
              padding: '5px 12px',
              backgroundColor: 'var(--bg-card)',
              borderRadius: '6px',
              border: '1px solid var(--border-color)',
            }}
          >
            <span style={{ fontSize: '0.75rem', fontWeight: 600, color: 'var(--text-secondary)' }}>SENSEX</span>
            <span
              style={{
                fontSize: '1.1rem',
                fontWeight: 700,
                fontFamily: 'JetBrains Mono',
                color: isSensexUp ? 'var(--color-green)' : 'var(--color-red)',
              }}
            >
              {sensexSpot.lastPrice.toFixed(2)}
            </span>
            <span
              style={{
                fontSize: '0.75rem',
                fontWeight: 600,
                color: isSensexUp ? 'var(--color-green)' : 'var(--color-red)',
              }}
            >
              {isSensexUp ? '+' : ''}
              {sensexSpot.change.toFixed(1)} ({isSensexUp ? '+' : ''}
              {sensexSpot.changePercent.toFixed(2)}%)
            </span>
          </div>
        )}

        {/* India VIX */}
        {overview?.indiaVix && (
          <div
            style={{
              display: 'flex',
              alignItems: 'baseline',
              gap: '8px',
              padding: '5px 10px',
              backgroundColor: 'var(--bg-card)',
              borderRadius: '6px',
              border: '1px solid var(--border-color)',
            }}
          >
            <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>VIX</span>
            <span
              style={{
                fontSize: '0.95rem',
                fontWeight: 700,
                fontFamily: 'JetBrains Mono',
                color: overview.indiaVix.lastPrice >= 20 ? 'var(--color-purple)' : 'var(--text-primary)',
              }}
            >
              {overview.indiaVix.lastPrice.toFixed(2)}
            </span>
          </div>
        )}
      </div>

      {/* Market Regime Badge & Status */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
        {regime && (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
              padding: '6px 12px',
              borderRadius: '6px',
              backgroundColor: regStyle.bg,
              border: `1px solid ${regStyle.border}`,
              color: regStyle.text,
              fontSize: '0.8rem',
              fontWeight: 600,
            }}
          >
            <ShieldAlert size={15} />
            <span>Regime: {regime.regime}</span>
            <span style={{ opacity: 0.75 }}>({regime.confidence}%)</span>
          </div>
        )}

        {/* IST Clock */}
        <div
          style={{
            padding: '6px 12px',
            backgroundColor: 'var(--bg-card)',
            border: '1px solid var(--border-color)',
            borderRadius: '6px',
            fontSize: '0.8rem',
            fontFamily: 'JetBrains Mono',
            color: 'var(--text-secondary)',
          }}
        >
          {currentTime || '00:00:00'} IST
        </div>

        {/* SignalR Connection Status */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '6px',
            padding: '6px 10px',
            borderRadius: '6px',
            backgroundColor: isSignalRConnected ? 'rgba(14, 203, 129, 0.1)' : 'rgba(245, 158, 11, 0.1)',
            border: `1px solid ${isSignalRConnected ? 'var(--color-green)' : 'var(--color-amber)'}`,
            color: isSignalRConnected ? 'var(--color-green)' : 'var(--color-amber)',
            fontSize: '0.75rem',
            fontWeight: 600,
          }}
          title={isSignalRConnected ? 'Connected to SignalR live stream' : 'Disconnected - Polling active'}
        >
          {isSignalRConnected ? <Wifi size={14} /> : <WifiOff size={14} />}
          <span>{isSignalRConnected ? 'LIVE FEED' : 'POLLING'}</span>
        </div>
      </div>
    </header>
  );
};
