import React from 'react';
import { ArrowDown, ArrowUp, Compass, Play, Shield, Target } from 'lucide-react';
import { api } from '../services/api';
import type { OptionsReportDto, SelectedOptionContractDto } from '../types';

interface OptionChainTabProps {
  optionsReport: OptionsReportDto | null;
  onRefresh: () => void;
}

export const OptionChainTab: React.FC<OptionChainTabProps> = ({
  optionsReport,
  onRefresh,
}) => {
  const [executingContract, setExecutingContract] = React.useState<string | null>(null);

  if (!optionsReport) {
    return (
      <div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
        No option chain snapshot available. Click Refresh or ensure market data feed is active.
      </div>
    );
  }

  const { pcrAnalysis, keyLevels, recommendedCe, recommendedPe, strikes } = optionsReport;

  const handleExecuteRecommended = async (contract: SelectedOptionContractDto) => {
    setExecutingContract(contract.instrumentKey);
    try {
      await api.executeSignal({
        id: '00000000-0000-0000-0000-000000000000',
        signalTimeUtc: new Date().toISOString(),
        underlyingKey: optionsReport.underlyingKey,
        underlyingPrice: optionsReport.underlyingPrice,
        direction: contract.optionType === 'CE' ? 'BuyCe' : 'BuyPe',
        strategyFamily: 'TrendFollowing',
        marketRegime: 'Transitional',
        bullishScore: 75,
        bearishScore: 75,
        confidence: 75,
        selectedInstrumentKey: contract.instrumentKey,
        selectedStrike: contract.strikePrice,
        selectedOptionType: contract.optionType,
        selectedExpiry: contract.expiryDate,
        entryPremium: contract.ltp,
        stopLossPremium: Math.max(1, contract.ltp - 15),
        target1Premium: contract.ltp + 30,
        target2Premium: contract.ltp + 45,
        recommendedQuantity: 65,
        lotSize: 65,
        riskAmount: 15 * 65,
        rewardToRiskRatio: 2.0,
        isExecuted: false,
        reasons: [{ category: 'OptionChain', scoreContribution: 30, description: contract.selectionReason }],
      });
      alert(`Paper trade executed for ${contract.instrumentKey}!`);
      onRefresh();
    } catch (err: any) {
      alert(`Execution failed: ${err.message}`);
    } finally {
      setExecutingContract(null);
    }
  };

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
          <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginTop: '6px' }}>
            Volume PCR: {pcrAnalysis.pcrVolume.toFixed(2)} | Trend: {pcrAnalysis.pcrTrend}
          </div>
        </div>

        {/* Key Option Levels */}
        <div style={cardStyle}>
          <div style={labelStyle}>KEY OPTION WALLS</div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', marginTop: '4px' }}>
            <div>
              <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>MAX PAIN</div>
              <div style={{ fontWeight: 700, fontSize: '1rem', color: 'var(--color-amber)', fontFamily: 'JetBrains Mono' }}>
                {keyLevels.maxPainStrike}
              </div>
            </div>
            <div>
              <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>CALL WALL (RES)</div>
              <div style={{ fontWeight: 700, fontSize: '1rem', color: 'var(--color-red)', fontFamily: 'JetBrains Mono' }}>
                {keyLevels.majorCallResistance}
              </div>
            </div>
            <div>
              <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>PUT FLOOR (SUP)</div>
              <div style={{ fontWeight: 700, fontSize: '1rem', color: 'var(--color-green)', fontFamily: 'JetBrains Mono' }}>
                {keyLevels.majorPutSupport}
              </div>
            </div>
            <div>
              <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>EXPECTED RANGE</div>
              <div style={{ fontWeight: 600, fontSize: '0.85rem', color: 'var(--text-secondary)', fontFamily: 'JetBrains Mono' }}>
                {keyLevels.expectedTradingRangeLow} - {keyLevels.expectedTradingRangeHigh}
              </div>
            </div>
          </div>
        </div>

        {/* Recommended CE Spotlight */}
        {recommendedCe && (
          <div style={{ ...cardStyle, border: '1px solid rgba(14, 203, 129, 0.4)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ ...labelStyle, color: 'var(--color-green)' }}>BEST CE TO BUY</div>
              <span style={{ fontSize: '0.75rem', color: 'var(--color-green)', fontWeight: 600 }}>
                Score: {recommendedCe.liquidityScore}/100
              </span>
            </div>
            <div style={{ fontWeight: 700, fontSize: '1.1rem', fontFamily: 'JetBrains Mono' }}>
              {recommendedCe.strikePrice} CE @ ₹{recommendedCe.ltp.toFixed(2)}
            </div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', margin: '4px 0 10px' }}>
              Delta: {recommendedCe.delta?.toFixed(2) ?? '0.50'} | Theta: {recommendedCe.theta?.toFixed(1) ?? '—'}
            </div>
            <button
              onClick={() => handleExecuteRecommended(recommendedCe)}
              disabled={executingContract === recommendedCe.instrumentKey}
              style={{
                width: '100%',
                padding: '6px 12px',
                borderRadius: '6px',
                backgroundColor: 'rgba(14, 203, 129, 0.2)',
                color: 'var(--color-green)',
                border: '1px solid var(--color-green)',
                fontWeight: 600,
                fontSize: '0.8rem',
                cursor: 'pointer',
              }}
            >
              {executingContract === recommendedCe.instrumentKey ? 'Executing...' : 'Buy 1 Lot CE'}
            </button>
          </div>
        )}

        {/* Recommended PE Spotlight */}
        {recommendedPe && (
          <div style={{ ...cardStyle, border: '1px solid rgba(246, 70, 93, 0.4)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ ...labelStyle, color: 'var(--color-red)' }}>BEST PE TO BUY</div>
              <span style={{ fontSize: '0.75rem', color: 'var(--color-red)', fontWeight: 600 }}>
                Score: {recommendedPe.liquidityScore}/100
              </span>
            </div>
            <div style={{ fontWeight: 700, fontSize: '1.1rem', fontFamily: 'JetBrains Mono' }}>
              {recommendedPe.strikePrice} PE @ ₹{recommendedPe.ltp.toFixed(2)}
            </div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', margin: '4px 0 10px' }}>
              Delta: {recommendedPe.delta?.toFixed(2) ?? '-0.50'} | Theta: {recommendedPe.theta?.toFixed(1) ?? '—'}
            </div>
            <button
              onClick={() => handleExecuteRecommended(recommendedPe)}
              disabled={executingContract === recommendedPe.instrumentKey}
              style={{
                width: '100%',
                padding: '6px 12px',
                borderRadius: '6px',
                backgroundColor: 'rgba(246, 70, 93, 0.2)',
                color: 'var(--color-red)',
                border: '1px solid var(--color-red)',
                fontWeight: 600,
                fontSize: '0.8rem',
                cursor: 'pointer',
              }}
            >
              {executingContract === recommendedPe.instrumentKey ? 'Executing...' : 'Buy 1 Lot PE'}
            </button>
          </div>
        )}
      </div>

      {/* Strike-by-Strike Option Chain Matrix */}
      <div style={cardStyle}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
          <div style={{ fontWeight: 700, fontSize: '1rem' }}>
            NIFTY 50 OPTION CHAIN MATRIX (ATM ± 10 STRIKES)
          </div>
          <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
            Expiry: {new Date(optionsReport.expiryDate).toLocaleDateString()}
          </span>
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

                <th style={{ ...thStyle, backgroundColor: 'rgba(255,255,255,0.02)' }}>STRIKE</th>

                <th style={{ ...thStyle, borderLeft: '2px solid var(--border-color)' }}>LTP</th>
                <th style={thStyle}>DELTA</th>
                <th style={thStyle}>VOL</th>
                <th style={thStyle}>TOTAL OI</th>
                <th style={thStyle}>OI CHG</th>
                <th style={thStyle}>BUILDUP</th>
              </tr>
            </thead>
            <tbody>
              {strikes.map((s) => {
                const isMaxPain = s.strikePrice === keyLevels.maxPainStrike;
                const isCallWall = s.strikePrice === keyLevels.majorCallResistance;
                const isPutFloor = s.strikePrice === keyLevels.majorPutSupport;

                return (
                  <tr
                    key={s.strikePrice}
                    style={{
                      borderBottom: '1px solid var(--border-color)',
                      backgroundColor: s.isAtm ? 'rgba(59, 130, 246, 0.08)' : 'transparent',
                    }}
                  >
                    {/* Call Side */}
                    <td style={tdStyle}>{getBuildupBadge(s.callBuildup)}</td>
                    <td
                      style={{
                        ...tdStyle,
                        color: s.callOiChange >= 0 ? 'var(--color-green)' : 'var(--color-red)',
                      }}
                    >
                      {s.callOiChange >= 0 ? '+' : ''}
                      {s.callOiChange.toLocaleString()}
                    </td>
                    <td style={tdStyle}>{s.callOi.toLocaleString()}</td>
                    <td style={{ ...tdStyle, color: 'var(--text-muted)' }}>{s.callVolume.toLocaleString()}</td>
                    <td style={tdStyle}>{s.callDelta?.toFixed(2) ?? '—'}</td>
                    <td
                      style={{
                        ...tdStyle,
                        borderRight: '2px solid var(--border-color)',
                        fontWeight: 700,
                        color: 'var(--color-green)',
                      }}
                    >
                      ₹{s.callQuote?.ltp.toFixed(2) ?? '—'}
                    </td>

                    {/* Center Strike */}
                    <td
                      style={{
                        ...tdStyle,
                        fontWeight: 800,
                        backgroundColor: s.isAtm ? 'rgba(59, 130, 246, 0.2)' : 'rgba(255, 255, 255, 0.02)',
                        color: isCallWall
                          ? 'var(--color-red)'
                          : isPutFloor
                          ? 'var(--color-green)'
                          : isMaxPain
                          ? 'var(--color-amber)'
                          : 'var(--text-primary)',
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '4px' }}>
                        <span>{s.strikePrice}</span>
                        {s.isAtm && (
                          <span style={{ fontSize: '0.65rem', padding: '1px 4px', backgroundColor: 'var(--color-blue)', color: '#fff', borderRadius: '3px' }}>
                            ATM
                          </span>
                        )}
                      </div>
                    </td>

                    {/* Put Side */}
                    <td
                      style={{
                        ...tdStyle,
                        borderLeft: '2px solid var(--border-color)',
                        fontWeight: 700,
                        color: 'var(--color-red)',
                      }}
                    >
                      ₹{s.putQuote?.ltp.toFixed(2) ?? '—'}
                    </td>
                    <td style={tdStyle}>{s.putDelta?.toFixed(2) ?? '—'}</td>
                    <td style={{ ...tdStyle, color: 'var(--text-muted)' }}>{s.putVolume.toLocaleString()}</td>
                    <td style={tdStyle}>{s.putOi.toLocaleString()}</td>
                    <td
                      style={{
                        ...tdStyle,
                        color: s.putOiChange >= 0 ? 'var(--color-green)' : 'var(--color-red)',
                      }}
                    >
                      {s.putOiChange >= 0 ? '+' : ''}
                      {s.putOiChange.toLocaleString()}
                    </td>
                    <td style={tdStyle}>{getBuildupBadge(s.putBuildup)}</td>
                  </tr>
                );
              })}
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
  padding: '18px',
};

const labelStyle: React.CSSProperties = {
  fontSize: '0.75rem',
  color: 'var(--text-muted)',
  fontWeight: 600,
  marginBottom: '6px',
};

const thStyle: React.CSSProperties = {
  padding: '8px 10px',
  fontWeight: 600,
};

const tdStyle: React.CSSProperties = {
  padding: '8px 10px',
  fontFamily: 'JetBrains Mono',
};
