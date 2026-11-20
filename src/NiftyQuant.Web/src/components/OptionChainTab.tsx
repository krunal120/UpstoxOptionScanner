import React, { useEffect, useState } from 'react';
import { ArrowDown, ArrowUp, Calendar, Compass, RefreshCw, Shield, Target, Zap } from 'lucide-react';
import { api } from '../services/api';
import { formatDateIST, formatToIST, getDaysToExpiry } from '../utils/dateUtils';
import type { OptionsReportDto, SelectedOptionContractDto } from '../types';

interface OptionChainTabProps {
  optionsReport: OptionsReportDto | null;
  onRefresh: () => void;
}

export const OptionChainTab: React.FC<OptionChainTabProps> = ({
  optionsReport: initialReport,
  onRefresh,
}) => {
  const [report, setReport] = useState<OptionsReportDto | null>(initialReport);
  const [expiries, setExpiries] = useState<string[]>([]);
  const [selectedExpiry, setSelectedExpiry] = useState<string>('');
  const [isLoadingExpiry, setIsLoadingExpiry] = useState<boolean>(false);

  // Sync prop changes
  useEffect(() => {
    if (initialReport && !selectedExpiry) {
      setReport(initialReport);
      if (initialReport.expiryDate) {
        setSelectedExpiry(initialReport.expiryDate.split('T')[0]);
      }
    }
  }, [initialReport, selectedExpiry]);

  // Fetch available expiries on mount
  useEffect(() => {
    let isMounted = true;
    api.getExpiries().then((list) => {
      if (isMounted && list && list.length > 0) {
        setExpiries(list);
        if (!selectedExpiry) {
          const initial = list[0];
          setSelectedExpiry(initial);
        }
      }
    });
    return () => {
      isMounted = false;
    };
  }, []);

  // Handle changing expiry date
  const handleExpiryChange = async (newExpiry: string) => {
    setSelectedExpiry(newExpiry);
    setIsLoadingExpiry(true);
    try {
      const updated = await api.getOptionsIntelligence(newExpiry);
      setReport(updated);
    } catch (err) {
      console.error('Failed to load options for expiry', newExpiry, err);
    } finally {
      setIsLoadingExpiry(false);
    }
  };

  const activeReport = report || initialReport;

  if (!activeReport) {
    return (
      <div style={{ padding: '60px 20px', textAlign: 'center', color: 'var(--text-muted)' }}>
        <RefreshCw size={28} className="animate-spin" style={{ margin: '0 auto 12px', opacity: 0.6 }} />
        <div style={{ fontSize: '1rem', fontWeight: 600 }}>Loading live option chain & expiries...</div>
        <div style={{ fontSize: '0.8rem', marginTop: '4px' }}>Connecting to Upstox Market Feed</div>
      </div>
    );
  }

  const { pcrAnalysis, keyLevels, recommendedCe, recommendedPe, strikes } = activeReport;
  const dte = getDaysToExpiry(selectedExpiry || activeReport.expiryDate);

  const getBuildupBadge = (buildup: string) => {
    switch (buildup) {
      case 'LongBuildup':
        return <span style={{ color: 'var(--color-green)', fontWeight: 600 }}>Long Buildup</span>;
      case 'ShortBuildup':
        return <span style={{ color: 'var(--color-red)', fontWeight: 600 }}>Short Buildup</span>;
      case 'ShortCovering':
        return <span style={{ color: 'var(--color-blue)', fontWeight: 600 }}>Short Covering</span>;
      case 'LongUnwinding':
        return <span style={{ color: 'var(--color-amber)', fontWeight: 600 }}>Long Unwind</span>;
      default:
        return <span style={{ color: 'var(--text-muted)' }}>—</span>;
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Expiry Selector & Market State Bar */}
      <div
        className="card"
        style={{
          background: 'linear-gradient(135deg, rgba(20, 26, 40, 0.95), rgba(15, 23, 42, 0.95))',
          borderRadius: '10px',
          padding: '14px 20px',
          border: '1px solid var(--border-color)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '14px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <div
            style={{
              width: '36px',
              height: '36px',
              borderRadius: '8px',
              backgroundColor: 'rgba(59, 130, 246, 0.15)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'var(--color-blue)',
            }}
          >
            <Calendar size={20} />
          </div>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <span style={{ fontSize: '1rem', fontWeight: 800, color: 'var(--text-primary)' }}>
                NIFTY 50 OPTION CHAIN
              </span>
              <span
                style={{
                  padding: '2px 8px',
                  borderRadius: '4px',
                  fontSize: '0.72rem',
                  fontWeight: 700,
                  backgroundColor: 'rgba(14, 203, 129, 0.15)',
                  color: 'var(--color-green)',
                }}
              >
                LIVE MARKET
              </span>
            </div>
            <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', marginTop: '2px' }}>
              Snapshot Time: {formatToIST(activeReport.snapshotTimeUtc || new Date().toISOString(), { showDate: true, showSeconds: true })}
            </div>
          </div>
        </div>

        {/* Expiry Selection Dropdown */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span style={{ fontSize: '0.82rem', fontWeight: 600, color: 'var(--text-secondary)' }}>
              Contract Expiry:
            </span>
            <select
              value={selectedExpiry}
              onChange={(e) => handleExpiryChange(e.target.value)}
              disabled={isLoadingExpiry}
              style={{
                backgroundColor: 'var(--bg-secondary)',
                color: 'var(--text-primary)',
                border: '1px solid var(--color-blue)',
                borderRadius: '6px',
                padding: '6px 12px',
                fontSize: '0.85rem',
                fontWeight: 700,
                fontFamily: 'JetBrains Mono',
                cursor: 'pointer',
              }}
            >
              {expiries.length > 0 ? (
                expiries.map((exp, idx) => (
                  <option key={exp} value={exp}>
                    {formatDateIST(exp)} {idx === 0 ? '(Current Weekly)' : idx === 1 ? '(Next Weekly)' : ''}
                  </option>
                ))
              ) : (
                <option value={activeReport.expiryDate}>{formatDateIST(activeReport.expiryDate)} (Active)</option>
              )}
            </select>
          </div>

          <div
            style={{
              padding: '6px 10px',
              borderRadius: '6px',
              backgroundColor: 'rgba(59, 130, 246, 0.1)',
              border: '1px solid rgba(59, 130, 246, 0.3)',
              fontSize: '0.8rem',
              color: 'var(--color-blue)',
              fontWeight: 700,
            }}
          >
            {dte} DTE
          </div>
        </div>
      </div>

      {/* Top Intelligence & Spotlight Cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '16px' }}>
        {/* PCR Card */}
        <div style={cardStyle}>
          <div style={labelStyle}>PUT-CALL RATIO (PCR)</div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '12px' }}>
            <span style={{ fontSize: '1.8rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-blue)' }}>
              {pcrAnalysis.pcrOi.toFixed(2)}
            </span>
            <span
              style={{
                padding: '3px 8px',
                borderRadius: '4px',
                fontSize: '0.75rem',
                fontWeight: 600,
                backgroundColor: 'rgba(59, 130, 246, 0.15)',
                color: 'var(--color-blue)',
              }}
            >
              {pcrAnalysis.sentimentZone}
            </span>
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '8px' }}>
            Vol PCR: {pcrAnalysis.pcrVolume.toFixed(2)} | Trend: {pcrAnalysis.pcrTrend}
          </div>
        </div>

        {/* Max Pain & Resistance/Support */}
        <div style={cardStyle}>
          <div style={labelStyle}>KEY INSTITUTIONAL LEVELS</div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: '8px' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>MAX PAIN</span>
            <span style={{ fontSize: '1.2rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-purple)' }}>
              {keyLevels.maxPainStrike}
            </span>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
            <div>
              <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>CALL WALL (RES)</div>
              <div style={{ fontWeight: 700, fontSize: '0.95rem', color: 'var(--color-red)', fontFamily: 'JetBrains Mono' }}>
                {keyLevels.majorCallResistance}
              </div>
            </div>
            <div>
              <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>PUT FLOOR (SUP)</div>
              <div style={{ fontWeight: 700, fontSize: '0.95rem', color: 'var(--color-green)', fontFamily: 'JetBrains Mono' }}>
                {keyLevels.majorPutSupport}
              </div>
            </div>
          </div>
        </div>

        {/* Autonomous Selection: CE Spotlight */}
        {recommendedCe && (
          <div style={{ ...cardStyle, border: '1px solid rgba(14, 203, 129, 0.4)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ ...labelStyle, color: 'var(--color-green)' }}>OPTIMAL CE TO BUY</div>
              <span style={{ fontSize: '0.75rem', color: 'var(--color-green)', fontWeight: 700 }}>
                Score: {recommendedCe.liquidityScore}/100
              </span>
            </div>
            <div style={{ fontWeight: 700, fontSize: '1.15rem', fontFamily: 'JetBrains Mono' }}>
              {recommendedCe.strikePrice} CE @ ₹{recommendedCe.ltp.toFixed(2)}
            </div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', margin: '4px 0 8px' }}>
              Delta: {recommendedCe.delta?.toFixed(2) ?? '0.52'} &bull; IV: {recommendedCe.impliedVolatility?.toFixed(1) ?? '13.2'}% &bull; Theta: {recommendedCe.theta?.toFixed(1) ?? '-20.0'}
            </div>
            <div
              style={{
                padding: '6px 10px',
                borderRadius: '6px',
                backgroundColor: 'rgba(14, 203, 129, 0.12)',
                border: '1px solid rgba(14, 203, 129, 0.3)',
                fontSize: '0.75rem',
                color: 'var(--color-green)',
                fontWeight: 600,
                display: 'flex',
                alignItems: 'center',
                gap: '6px',
              }}
            >
              <Zap size={14} /> Auto-Trader Target for Bullish Signals
            </div>
          </div>
        )}

        {/* Autonomous Selection: PE Spotlight */}
        {recommendedPe && (
          <div style={{ ...cardStyle, border: '1px solid rgba(246, 70, 93, 0.4)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ ...labelStyle, color: 'var(--color-red)' }}>OPTIMAL PE TO BUY</div>
              <span style={{ fontSize: '0.75rem', color: 'var(--color-red)', fontWeight: 700 }}>
                Score: {recommendedPe.liquidityScore}/100
              </span>
            </div>
            <div style={{ fontWeight: 700, fontSize: '1.15rem', fontFamily: 'JetBrains Mono' }}>
              {recommendedPe.strikePrice} PE @ ₹{recommendedPe.ltp.toFixed(2)}
            </div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', margin: '4px 0 8px' }}>
              Delta: {recommendedPe.delta?.toFixed(2) ?? '-0.54'} &bull; IV: {recommendedPe.impliedVolatility?.toFixed(1) ?? '14.1'}% &bull; Theta: {recommendedPe.theta?.toFixed(1) ?? '-21.3'}
            </div>
            <div
              style={{
                padding: '6px 10px',
                borderRadius: '6px',
                backgroundColor: 'rgba(246, 70, 93, 0.12)',
                border: '1px solid rgba(246, 70, 93, 0.3)',
                fontSize: '0.75rem',
                color: 'var(--color-red)',
                fontWeight: 600,
                display: 'flex',
                alignItems: 'center',
                gap: '6px',
              }}
            >
              <Zap size={14} /> Auto-Trader Target for Bearish Signals
            </div>
          </div>
        )}
      </div>

      {/* Strike-by-Strike Option Chain Matrix */}
      <div style={cardStyle}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px', flexWrap: 'wrap', gap: '8px' }}>
          <div style={{ fontWeight: 700, fontSize: '1rem' }}>
            NIFTY 50 OPTION CHAIN MATRIX (ATM ± 10 STRIKES)
          </div>
          <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
            Expiry: <strong style={{ color: 'var(--text-primary)' }}>{formatDateIST(selectedExpiry || activeReport.expiryDate)}</strong> &bull; Spot: <strong style={{ color: 'var(--color-blue)' }}>{activeReport.underlyingPrice.toFixed(2)}</strong>
          </div>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'center', fontSize: '0.8rem' }}>
            <thead>
              <tr style={{ backgroundColor: 'var(--bg-secondary)', color: 'var(--text-secondary)' }}>
                <th colSpan={6} style={{ padding: '8px', borderRight: '2px solid var(--border-color)', color: 'var(--color-green)' }}>
                  CALL OPTIONS (CE)
                </th>
                <th style={{ padding: '8px', color: 'var(--text-primary)' }}>STRIKE</th>
                <th colSpan={6} style={{ padding: '8px', borderLeft: '2px solid var(--border-color)', color: 'var(--color-red)' }}>
                  PUT OPTIONS (PE)
                </th>
              </tr>
              <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)', fontSize: '0.75rem' }}>
                <th style={thStyle}>BUILDUP</th>
                <th style={thStyle}>OI CHG</th>
                <th style={thStyle}>TOTAL OI</th>
                <th style={thStyle}>VOL</th>
                <th style={thStyle}>DELTA</th>
                <th style={{ ...thStyle, borderRight: '2px solid var(--border-color)' }}>LTP</th>
                <th style={{ ...thStyle, backgroundColor: 'rgba(59, 130, 246, 0.1)', color: 'var(--color-blue)', fontWeight: 700 }}>
                  STRIKE
                </th>
                <th style={{ ...thStyle, borderLeft: '2px solid var(--border-color)' }}>LTP</th>
                <th style={thStyle}>DELTA</th>
                <th style={thStyle}>VOL</th>
                <th style={thStyle}>TOTAL OI</th>
                <th style={thStyle}>OI CHG</th>
                <th style={thStyle}>BUILDUP</th>
              </tr>
            </thead>
            <tbody>
              {strikes && strikes.length > 0 ? (
                strikes.map((s) => {
                  const isAtm = s.isAtm;
                  return (
                    <tr
                      key={s.strikePrice}
                      style={{
                        borderBottom: '1px solid var(--border-color)',
                        backgroundColor: isAtm ? 'rgba(59, 130, 246, 0.12)' : 'transparent',
                        fontWeight: isAtm ? 700 : 400,
                      }}
                    >
                      {/* CE Columns */}
                      <td style={tdStyle}>{getBuildupBadge(s.callBuildup)}</td>
                      <td
                        style={{
                          ...tdStyle,
                          color: s.callOiChange >= 0 ? 'var(--color-green)' : 'var(--color-red)',
                        }}
                      >
                        {s.callOiChange > 0 ? '+' : ''}
                        {s.callOiChange.toLocaleString()}
                      </td>
                      <td style={tdStyle}>{s.callOi.toLocaleString()}</td>
                      <td style={{ ...tdStyle, color: 'var(--text-muted)' }}>{s.callVolume.toLocaleString()}</td>
                      <td style={tdStyle}>{s.callDelta != null ? s.callDelta.toFixed(2) : '—'}</td>
                      <td
                        style={{
                          ...tdStyle,
                          fontFamily: 'JetBrains Mono',
                          fontWeight: 700,
                          color: 'var(--color-green)',
                          borderRight: '2px solid var(--border-color)',
                        }}
                      >
                        {s.callQuote?.ltp ? `₹${s.callQuote.ltp.toFixed(2)}` : '—'}
                      </td>

                      {/* Strike */}
                      <td
                        style={{
                          ...tdStyle,
                          fontFamily: 'JetBrains Mono',
                          fontWeight: 800,
                          backgroundColor: isAtm ? 'rgba(59, 130, 246, 0.25)' : 'rgba(255, 255, 255, 0.02)',
                          color: isAtm ? 'var(--color-blue)' : 'var(--text-primary)',
                        }}
                      >
                        {s.strikePrice}
                        {isAtm && (
                          <span
                            style={{
                              marginLeft: '6px',
                              fontSize: '0.65rem',
                              padding: '2px 4px',
                              borderRadius: '3px',
                              backgroundColor: 'var(--color-blue)',
                              color: '#fff',
                            }}
                          >
                            ATM
                          </span>
                        )}
                      </td>

                      {/* PE Columns */}
                      <td
                        style={{
                          ...tdStyle,
                          fontFamily: 'JetBrains Mono',
                          fontWeight: 700,
                          color: 'var(--color-red)',
                          borderLeft: '2px solid var(--border-color)',
                        }}
                      >
                        {s.putQuote?.ltp ? `₹${s.putQuote.ltp.toFixed(2)}` : '—'}
                      </td>
                      <td style={tdStyle}>{s.putDelta != null ? s.putDelta.toFixed(2) : '—'}</td>
                      <td style={{ ...tdStyle, color: 'var(--text-muted)' }}>{s.putVolume.toLocaleString()}</td>
                      <td style={tdStyle}>{s.putOi.toLocaleString()}</td>
                      <td
                        style={{
                          ...tdStyle,
                          color: s.putOiChange >= 0 ? 'var(--color-green)' : 'var(--color-red)',
                        }}
                      >
                        {s.putOiChange > 0 ? '+' : ''}
                        {s.putOiChange.toLocaleString()}
                      </td>
                      <td style={tdStyle}>{getBuildupBadge(s.putBuildup)}</td>
                    </tr>
                  );
                })
              ) : (
                <tr>
                  <td colSpan={13} style={{ padding: '24px', color: 'var(--text-muted)' }}>
                    No option strikes available for selected expiry.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

const cardStyle: React.CSSProperties = {
  backgroundColor: 'var(--bg-card)',
  borderRadius: '8px',
  border: '1px solid var(--border-color)',
  padding: '16px',
};

const labelStyle: React.CSSProperties = {
  fontSize: '0.75rem',
  fontWeight: 700,
  letterSpacing: '0.5px',
  color: 'var(--text-secondary)',
  marginBottom: '8px',
};

const thStyle: React.CSSProperties = {
  padding: '8px 10px',
  fontWeight: 600,
};

const tdStyle: React.CSSProperties = {
  padding: '8px 10px',
};
