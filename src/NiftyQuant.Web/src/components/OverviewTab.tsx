import React, { useState } from 'react';
import {
  AlertTriangle,
  ArrowDownRight,
  ArrowUpRight,
  CheckCircle2,
  Clock,
  Compass,
  Play,
  TrendingDown,
  TrendingUp,
  Zap,
} from 'lucide-react';
import { api } from '../services/api';
import type {
  DailyRiskStatusDto,
  MarketRegimeResultDto,
  PaperPortfolioSummaryDto,
  StrategySignalDto,
} from '../types';

interface OverviewTabProps {
  latestSignal: StrategySignalDto | null;
  signalHistory: StrategySignalDto[];
  regime: MarketRegimeResultDto | null;
  riskStatus: DailyRiskStatusDto | null;
  portfolio: PaperPortfolioSummaryDto | null;
  onRefresh: () => void;
}

export const OverviewTab: React.FC<OverviewTabProps> = ({
  latestSignal,
  signalHistory,
  regime,
  riskStatus,
  portfolio,
  onRefresh,
}) => {
  const [isExecuting, setIsExecuting] = useState(false);
  const [executionMessage, setExecutionMessage] = useState<{ text: string; isError: boolean } | null>(null);

  const handleExecuteTrade = async () => {
    if (!latestSignal) return;
    setIsExecuting(true);
    setExecutionMessage(null);

    try {
      const res = await api.executeSignal(latestSignal);
      setExecutionMessage({
        text: `Order executed! Position opened for ${res.instrumentKey} (${res.quantity} shares) @ ₹${res.executedPrice}`,
        isError: false,
      });
      onRefresh();
    } catch (err: any) {
      setExecutionMessage({
        text: err.message || 'Trade execution rejected by risk engine',
        isError: true,
      });
    } finally {
      setIsExecuting(false);
    }
  };

  const getDirectionBadge = (dir: string) => {
    if (dir.includes('BuyCe')) {
      return {
        bg: 'rgba(14, 203, 129, 0.2)',
        text: '#0ecb81',
        border: '#0ecb81',
        icon: <TrendingUp size={18} />,
      };
    }
    if (dir.includes('BuyPe')) {
      return {
        bg: 'rgba(246, 70, 93, 0.2)',
        text: '#f6465d',
        border: '#f6465d',
        icon: <TrendingDown size={18} />,
      };
    }
    return {
      bg: 'rgba(154, 165, 190, 0.1)',
      text: '#9aa5be',
      border: '#5e6b88',
      icon: <Clock size={18} />,
    };
  };

  const dirBadge = getDirectionBadge(latestSignal?.direction ?? 'Wait');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Top Metric Cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px' }}>
        <div style={cardStyle}>
          <div style={metricLabelStyle}>VIRTUAL CAPITAL</div>
          <div style={metricValueStyle}>
            ₹{portfolio ? portfolio.currentCapital.toLocaleString('en-IN', { minimumFractionDigits: 2 }) : '50,000.00'}
          </div>
          <div style={metricSubStyle}>Initial: ₹50,000.00</div>
        </div>

        <div style={cardStyle}>
          <div style={metricLabelStyle}>AVAILABLE BALANCE</div>
          <div style={metricValueStyle}>
            ₹{riskStatus ? riskStatus.availableBalance.toLocaleString('en-IN', { minimumFractionDigits: 2 }) : '50,000.00'}
          </div>
          <div style={metricSubStyle}>Margin: ₹{riskStatus?.utilizedMargin.toFixed(2) ?? '0.00'}</div>
        </div>

        <div style={cardStyle}>
          <div style={metricLabelStyle}>TODAY'S NET P&L</div>
          <div
            style={{
              ...metricValueStyle,
              color: (riskStatus?.totalPnLToday ?? 0) >= 0 ? 'var(--color-green)' : 'var(--color-red)',
            }}
          >
            {(riskStatus?.totalPnLToday ?? 0) >= 0 ? '+' : ''}₹{riskStatus?.totalPnLToday.toFixed(2) ?? '0.00'}
          </div>
          <div style={metricSubStyle}>Remaining Loss Limit: ₹{riskStatus?.dailyLossRemaining.toFixed(2) ?? '750.00'}</div>
        </div>

        <div style={cardStyle}>
          <div style={metricLabelStyle}>WIN RATE</div>
          <div style={metricValueStyle}>
            {portfolio ? `${portfolio.winRatePercent.toFixed(1)}%` : '0.0%'}
          </div>
          <div style={metricSubStyle}>
            {portfolio ? `${portfolio.winningTrades}W / ${portfolio.losingTrades}L (${portfolio.totalTrades} Trades)` : 'No trades yet'}
          </div>
        </div>
      </div>

      {/* Main Signal Spotlight Card */}
      <div
        style={{
          ...cardStyle,
          border: `1px solid ${dirBadge.border}`,
          boxShadow: `0 0 20px rgba(0,0,0,0.5)`,
          padding: '24px',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '14px', marginBottom: '20px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                padding: '8px 16px',
                borderRadius: '8px',
                backgroundColor: dirBadge.bg,
                color: dirBadge.text,
                border: `1px solid ${dirBadge.border}`,
                fontWeight: 800,
                fontSize: '1.1rem',
                letterSpacing: '0.5px',
              }}
            >
              {dirBadge.icon}
              <span>{latestSignal?.direction ?? 'WAIT'}</span>
            </div>

            <div
              style={{
                padding: '6px 12px',
                borderRadius: '6px',
                backgroundColor: 'rgba(59, 130, 246, 0.15)',
                color: 'var(--color-blue)',
                border: '1px solid rgba(59, 130, 246, 0.3)',
                fontSize: '0.85rem',
                fontWeight: 600,
              }}
            >
              Strategy: {latestSignal?.strategyFamily ?? 'None'}
            </div>

            <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
              Confidence: <strong style={{ color: 'var(--text-primary)' }}>{latestSignal?.confidence ?? 0}%</strong>
            </div>
          </div>

          {/* Action Trigger */}
          <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
            <button
              onClick={handleExecuteTrade}
              disabled={isExecuting || !latestSignal || latestSignal.direction === 'Wait' || latestSignal.direction === 'NoTrade'}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                padding: '10px 20px',
                borderRadius: '8px',
                backgroundColor:
                  latestSignal && latestSignal.direction !== 'Wait' && latestSignal.direction !== 'NoTrade'
                    ? 'var(--color-green)'
                    : 'var(--border-color)',
                color: '#fff',
                border: 'none',
                fontWeight: 700,
                cursor: latestSignal && latestSignal.direction !== 'Wait' && latestSignal.direction !== 'NoTrade' ? 'pointer' : 'not-allowed',
                opacity: isExecuting ? 0.6 : 1,
                transition: 'all 0.2s',
              }}
            >
              <Play size={16} />
              {isExecuting ? 'Executing...' : 'Execute Paper Trade'}
            </button>
          </div>
        </div>

        {/* Execution Feedback Notification */}
        {executionMessage && (
          <div
            style={{
              padding: '12px 16px',
              marginBottom: '16px',
              borderRadius: '6px',
              backgroundColor: executionMessage.isError ? 'rgba(246, 70, 93, 0.15)' : 'rgba(14, 203, 129, 0.15)',
              border: `1px solid ${executionMessage.isError ? 'var(--color-red)' : 'var(--color-green)'}`,
              color: executionMessage.isError ? 'var(--color-red)' : 'var(--color-green)',
              fontSize: '0.9rem',
              display: 'flex',
              alignItems: 'center',
              gap: '10px',
            }}
          >
            {executionMessage.isError ? <AlertTriangle size={18} /> : <CheckCircle2 size={18} />}
            <span>{executionMessage.text}</span>
          </div>
        )}

        {/* Trade Parameters Grid */}
        {latestSignal && latestSignal.selectedInstrumentKey && (
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
              gap: '14px',
              padding: '16px',
              backgroundColor: 'var(--bg-secondary)',
              borderRadius: '8px',
              marginBottom: '20px',
            }}
          >
            <div>
              <div style={smallLabelStyle}>SELECTED OPTION</div>
              <div style={{ fontWeight: 700, fontSize: '0.95rem', color: 'var(--color-blue)', fontFamily: 'JetBrains Mono' }}>
                {latestSignal.selectedInstrumentKey}
              </div>
            </div>

            <div>
              <div style={smallLabelStyle}>ENTRY PREMIUM</div>
              <div style={{ fontWeight: 700, fontSize: '1.1rem', color: 'var(--text-primary)', fontFamily: 'JetBrains Mono' }}>
                ₹{latestSignal.entryPremium.toFixed(2)}
              </div>
            </div>

            <div>
              <div style={smallLabelStyle}>STOP LOSS</div>
              <div style={{ fontWeight: 700, fontSize: '1.1rem', color: 'var(--color-red)', fontFamily: 'JetBrains Mono' }}>
                ₹{latestSignal.stopLossPremium.toFixed(2)}
              </div>
            </div>

            <div>
              <div style={smallLabelStyle}>TARGET 1 (1:2 R:R)</div>
              <div style={{ fontWeight: 700, fontSize: '1.1rem', color: 'var(--color-green)', fontFamily: 'JetBrains Mono' }}>
                ₹{latestSignal.target1Premium.toFixed(2)}
              </div>
            </div>

            <div>
              <div style={smallLabelStyle}>RISK / REWARD</div>
              <div style={{ fontWeight: 700, fontSize: '1.1rem', color: 'var(--color-amber)', fontFamily: 'JetBrains Mono' }}>
                1:{latestSignal.rewardToRiskRatio.toFixed(2)}
              </div>
            </div>

            <div>
              <div style={smallLabelStyle}>LOT RISK (65 QTY)</div>
              <div style={{ fontWeight: 700, fontSize: '1.1rem', color: 'var(--text-secondary)', fontFamily: 'JetBrains Mono' }}>
                ₹{latestSignal.riskAmount.toFixed(2)}
              </div>
            </div>
          </div>
        )}

        {/* Scoring Breakdown & Reasons */}
        <div>
          <div style={{ fontSize: '0.9rem', fontWeight: 700, marginBottom: '10px', color: 'var(--text-secondary)' }}>
            SIGNAL RATIONALE & SCORING PILLARS
          </div>
          {latestSignal?.reasons && latestSignal.reasons.length > 0 ? (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              {latestSignal.reasons.map((r, idx) => (
                <div
                  key={idx}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '8px 12px',
                    borderRadius: '6px',
                    backgroundColor: 'var(--bg-secondary)',
                    border: '1px solid var(--border-color)',
                    fontSize: '0.85rem',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <span
                      style={{
                        padding: '2px 8px',
                        borderRadius: '4px',
                        backgroundColor: 'var(--border-color)',
                        fontSize: '0.75rem',
                        fontWeight: 600,
                        color: 'var(--text-secondary)',
                      }}
                    >
                      {r.category}
                    </span>
                    <span style={{ color: 'var(--text-primary)' }}>{r.description}</span>
                  </div>
                  {r.scoreContribution > 0 && (
                    <span style={{ color: 'var(--color-green)', fontWeight: 700, fontFamily: 'JetBrains Mono' }}>
                      +{r.scoreContribution}
                    </span>
                  )}
                </div>
              ))}
            </div>
          ) : (
            <div style={{ color: 'var(--text-muted)', fontSize: '0.85rem' }}>
              No active signal factors or awaiting next market bar evaluation.
            </div>
          )}
        </div>
      </div>

      {/* Signal History Table */}
      <div style={cardStyle}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <div style={{ fontWeight: 700, fontSize: '1rem' }}>RECENT STRATEGY SIGNALS</div>
          <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>Last 10 generated signals</span>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)' }}>
                <th style={thStyle}>TIME</th>
                <th style={thStyle}>DIRECTION</th>
                <th style={thStyle}>STRATEGY</th>
                <th style={thStyle}>SCORE</th>
                <th style={thStyle}>CONTRACT</th>
                <th style={thStyle}>ENTRY</th>
                <th style={thStyle}>SL</th>
                <th style={thStyle}>TARGET</th>
                <th style={thStyle}>STATUS</th>
              </tr>
            </thead>
            <tbody>
              {signalHistory.length > 0 ? (
                signalHistory.map((s) => (
                  <tr key={s.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                    <td style={tdStyle}>{new Date(s.signalTimeUtc).toLocaleTimeString()}</td>
                    <td style={tdStyle}>
                      <span
                        style={{
                          fontWeight: 700,
                          color: s.direction.includes('Ce') ? 'var(--color-green)' : s.direction.includes('Pe') ? 'var(--color-red)' : 'var(--text-muted)',
                        }}
                      >
                        {s.direction}
                      </span>
                    </td>
                    <td style={tdStyle}>{s.strategyFamily}</td>
                    <td style={{ ...tdStyle, fontWeight: 700 }}>{s.confidence}%</td>
                    <td style={{ ...tdStyle, fontFamily: 'JetBrains Mono', fontSize: '0.8rem' }}>{s.selectedInstrumentKey || '—'}</td>
                    <td style={tdStyle}>₹{s.entryPremium.toFixed(1)}</td>
                    <td style={{ ...tdStyle, color: 'var(--color-red)' }}>₹{s.stopLossPremium.toFixed(1)}</td>
                    <td style={{ ...tdStyle, color: 'var(--color-green)' }}>₹{s.target1Premium.toFixed(1)}</td>
                    <td style={tdStyle}>
                      <span
                        style={{
                          padding: '2px 6px',
                          borderRadius: '4px',
                          fontSize: '0.75rem',
                          backgroundColor: s.isExecuted ? 'rgba(14, 203, 129, 0.15)' : 'rgba(255, 255, 255, 0.05)',
                          color: s.isExecuted ? 'var(--color-green)' : 'var(--text-muted)',
                        }}
                      >
                        {s.isExecuted ? 'EXECUTED' : 'LOGGED'}
                      </span>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={9} style={{ ...tdStyle, textAlign: 'center', color: 'var(--text-muted)' }}>
                    No signal history recorded yet.
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
  padding: '18px',
};

const metricLabelStyle: React.CSSProperties = {
  fontSize: '0.75rem',
  color: 'var(--text-muted)',
  fontWeight: 600,
  letterSpacing: '0.5px',
  marginBottom: '6px',
};

const metricValueStyle: React.CSSProperties = {
  fontSize: '1.4rem',
  fontWeight: 700,
  fontFamily: 'JetBrains Mono',
  color: 'var(--text-primary)',
};

const metricSubStyle: React.CSSProperties = {
  fontSize: '0.75rem',
  color: 'var(--text-secondary)',
  marginTop: '4px',
};

const smallLabelStyle: React.CSSProperties = {
  fontSize: '0.7rem',
  color: 'var(--text-muted)',
  fontWeight: 600,
  marginBottom: '4px',
};

const thStyle: React.CSSProperties = {
  padding: '10px 12px',
  fontWeight: 600,
  fontSize: '0.75rem',
};

const tdStyle: React.CSSProperties = {
  padding: '10px 12px',
};
