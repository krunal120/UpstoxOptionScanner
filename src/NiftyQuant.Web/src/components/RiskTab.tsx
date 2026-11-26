import React, { useState } from 'react';
import {
  AlertOctagon,
  AlertTriangle,
  CheckCircle2,
  Lock,
  RefreshCw,
  RotateCcw,
  Shield,
  ShieldAlert,
  ShieldCheck,
  TrendingDown,
  XCircle,
} from 'lucide-react';
import { api } from '../services/api';
import type { DailyRiskStatusDto, PaperAccountDto } from '../types';

interface RiskTabProps {
  riskStatus: DailyRiskStatusDto | null;
  account: PaperAccountDto | null;
  onRefresh: () => void;
}

export const RiskTab: React.FC<RiskTabProps> = ({ riskStatus, account, onRefresh }) => {
  const [isResetting, setIsResetting] = useState(false);
  const [feedback, setFeedback] = useState<{ message: string; isError: boolean } | null>(null);

  const handleResetLimits = async () => {
    if (
      !window.confirm(
        'WARNING: Are you sure you want to reset the daily risk circuit breakers? This should only be done for simulation or administrative testing.'
      )
    ) {
      return;
    }

    setIsResetting(true);
    setFeedback(null);

    try {
      const res = await api.resetDailyLimits();
      setFeedback({
        message: res.message || 'Daily risk limits and circuit breakers reset successfully.',
        isError: false,
      });
      await onRefresh();
    } catch (err: any) {
      setFeedback({
        message: err.message || 'Failed to reset risk limits',
        isError: true,
      });
    } finally {
      setIsResetting(false);
    }
  };

  const isTripped = riskStatus?.isCircuitBreakerTripped ?? false;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Circuit Breaker Status Banner */}
      <div
        className="card"
        style={{
          backgroundColor: isTripped ? 'rgba(246, 70, 93, 0.15)' : 'rgba(14, 203, 129, 0.1)',
          border: `1px solid ${isTripped ? 'var(--color-red)' : 'var(--color-green)'}`,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '16px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
          {isTripped ? (
            <ShieldAlert size={36} color="var(--color-red)" />
          ) : (
            <ShieldCheck size={36} color="var(--color-green)" />
          )}
          <div>
            <div
              style={{
                fontSize: '1.1rem',
                fontWeight: 800,
                color: isTripped ? 'var(--color-red)' : 'var(--color-green)',
              }}
            >
              {isTripped ? 'CIRCUIT BREAKER ACTIVE - TRADING HALTED' : 'ALL CIRCUIT BREAKERS HEALTHY - TRADING ENABLED'}
            </div>
            <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)', marginTop: '2px' }}>
              {isTripped
                ? `Reason: ${riskStatus?.circuitBreakerReason || 'Risk threshold exceeded'}`
                : 'Account is within all intraday risk thresholds. New positions may be opened.'}
            </div>
          </div>
        </div>

        <button
          onClick={handleResetLimits}
          disabled={isResetting}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            padding: '8px 16px',
            borderRadius: '6px',
            backgroundColor: 'var(--bg-secondary)',
            border: '1px solid var(--border-color)',
            color: 'var(--text-primary)',
            fontWeight: 600,
            fontSize: '0.8rem',
            cursor: 'pointer',
          }}
        >
          <RotateCcw size={14} className={isResetting ? 'spin' : ''} /> Reset Daily Limits
        </button>
      </div>

      {/* Feedback Banner */}
      {feedback && (
        <div
          style={{
            padding: '12px 16px',
            borderRadius: '8px',
            display: 'flex',
            alignItems: 'center',
            gap: '10px',
            backgroundColor: feedback.isError ? 'rgba(246, 70, 93, 0.15)' : 'rgba(14, 203, 129, 0.15)',
            border: `1px solid ${feedback.isError ? 'var(--color-red)' : 'var(--color-green)'}`,
            color: feedback.isError ? 'var(--color-red)' : 'var(--color-green)',
            fontSize: '0.85rem',
          }}
        >
          {feedback.isError ? <AlertTriangle size={18} /> : <CheckCircle2 size={18} />}
          <span>{feedback.message}</span>
        </div>
      )}

      {/* Risk Metrics Cards */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
          gap: '16px',
        }}
      >
        {/* Daily Loss Limit Card */}
        <div className="card">
          <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginBottom: '8px' }}>
            DAILY LOSS LIMIT (1.5% MAX)
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px' }}>
            <span style={{ fontSize: '1.4rem', fontWeight: 800, fontFamily: 'JetBrains Mono', color: 'var(--color-red)' }}>
              ₹{riskStatus ? riskStatus.dailyLossLimit.toFixed(2) : '750.00'}
            </span>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Cap Threshold</span>
          </div>
          <div style={{ marginTop: '12px', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
            Remaining Buffer:{' '}
            <strong
              style={{
                fontFamily: 'JetBrains Mono',
                color: (riskStatus?.dailyLossRemaining ?? 750) > 200 ? 'var(--color-green)' : 'var(--color-red)',
              }}
            >
              ₹{riskStatus ? riskStatus.dailyLossRemaining.toFixed(2) : '750.00'}
            </strong>
          </div>
          <div style={{ marginTop: '6px', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
            Total PnL Today: ₹{riskStatus ? riskStatus.totalPnLToday.toFixed(2) : '0.00'}
          </div>
        </div>

        {/* Max Daily Trades Card */}
        <div className="card">
          <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginBottom: '8px' }}>
            DAILY TRADES BUDGET
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px' }}>
            <span style={{ fontSize: '1.4rem', fontWeight: 800, fontFamily: 'JetBrains Mono' }}>
              {riskStatus?.tradesCountToday ?? 0} / {riskStatus?.maxDailyTradesLimit ?? 6}
            </span>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Trades Executed</span>
          </div>
          {/* Progress Bar */}
          <div
            style={{
              height: '6px',
              borderRadius: '3px',
              backgroundColor: 'var(--bg-secondary)',
              marginTop: '14px',
              overflow: 'hidden',
            }}
          >
            <div
              style={{
                height: '100%',
                width: `${Math.min(100, ((riskStatus?.tradesCountToday ?? 0) / (riskStatus?.maxDailyTradesLimit ?? 6)) * 100)}%`,
                backgroundColor:
                  (riskStatus?.tradesCountToday ?? 0) >= (riskStatus?.maxDailyTradesLimit ?? 6)
                    ? 'var(--color-red)'
                    : 'var(--color-blue)',
              }}
            />
          </div>
          <div style={{ marginTop: '6px', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
            Max 6 trades / day to prevent overtrading.
          </div>
        </div>

        {/* Consecutive Losses Card */}
        <div className="card">
          <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginBottom: '8px' }}>
            CONSECUTIVE LOSSES (MAX 2)
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px' }}>
            <span
              style={{
                fontSize: '1.4rem',
                fontWeight: 800,
                fontFamily: 'JetBrains Mono',
                color: (riskStatus?.consecutiveLossesToday ?? 0) >= 2 ? 'var(--color-red)' : 'var(--text-primary)',
              }}
            >
              {riskStatus?.consecutiveLossesToday ?? 0} / {riskStatus?.maxConsecutiveLossesLimit ?? 2}
            </span>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Loss Streak</span>
          </div>
          <div style={{ marginTop: '14px', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
            2 back-to-back losses lock trading for the rest of the day to protect capital.
          </div>
        </div>

        {/* Concurrent Positions Card */}
        <div className="card">
          <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginBottom: '8px' }}>
            CONCURRENT POSITIONS (MAX 1)
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px' }}>
            <span style={{ fontSize: '1.4rem', fontWeight: 800, fontFamily: 'JetBrains Mono' }}>
              {riskStatus?.activePositionsCount ?? 0} / {riskStatus?.maxConcurrentPositionsLimit ?? 1}
            </span>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Active</span>
          </div>
          <div style={{ marginTop: '14px', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
            Strict single-position rule: one active contract at any given time.
          </div>
        </div>
      </div>

      {/* Safety Rules & Quantitative Policies Accordion/List */}
      <div className="card">
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 700, marginBottom: '16px' }}>
          <Shield size={18} color="var(--color-blue)" />
          System Risk Safeguards & Non-Negotiable Constraints
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: '16px' }}>
          <div
            style={{
              padding: '12px',
              borderRadius: '6px',
              backgroundColor: 'var(--bg-secondary)',
              border: '1px solid var(--border-color)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontWeight: 600, color: 'var(--color-green)' }}>
              <CheckCircle2 size={15} /> Long Options Buying Only
            </div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '6px', lineHeight: 1.5 }}>
              Strictly buys NIFTY 50 Call (CE) or Put (PE) options. Zero option writing/shorting permitted in Version 1. Risk is strictly capped at premium paid.
            </div>
          </div>

          <div
            style={{
              padding: '12px',
              borderRadius: '6px',
              backgroundColor: 'var(--bg-secondary)',
              border: '1px solid var(--border-color)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontWeight: 600, color: 'var(--color-green)' }}>
              <CheckCircle2 size={15} /> No Martingale / Averaging Down
            </div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '6px', lineHeight: 1.5 }}>
              The risk engine algorithmically rejects any order for an instrument already held. Scaling down into losing trades is hard-blocked.
            </div>
          </div>

          <div
            style={{
              padding: '12px',
              borderRadius: '6px',
              backgroundColor: 'var(--bg-secondary)',
              border: '1px solid var(--border-color)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontWeight: 600, color: 'var(--color-green)' }}>
              <CheckCircle2 size={15} /> Fixed Risk Per Trade (0.5% / ₹250)
            </div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '6px', lineHeight: 1.5 }}>
              Position sizing allocates exactly 1 lot (65 shares) sizing based on stop-loss distance, never risking more than ₹250 per entry.
            </div>
          </div>

          <div
            style={{
              padding: '12px',
              borderRadius: '6px',
              backgroundColor: 'var(--bg-secondary)',
              border: '1px solid var(--border-color)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontWeight: 600, color: 'var(--color-green)' }}>
              <CheckCircle2 size={15} /> Mandatory 15:15 IST Square-Off
            </div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '6px', lineHeight: 1.5 }}>
              Zero overnight risk. All intraday positions are automatically liquidated at 15:15 IST. Entries vetoed before 09:30 and after 15:10 IST.
            </div>
          </div>

          <div
            style={{
              padding: '12px',
              borderRadius: '6px',
              backgroundColor: 'var(--bg-secondary)',
              border: '1px solid var(--border-color)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontWeight: 600, color: 'var(--color-green)' }}>
              <CheckCircle2 size={15} /> Minimum 1:2 Reward-to-Risk
            </div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '6px', lineHeight: 1.5 }}>
              No signal is ever executed if the projected delta-weighted Target 1 to Stop-Loss ratio is below 2.0. Positive mathematical expectancy is enforced.
            </div>
          </div>

          <div
            style={{
              padding: '12px',
              borderRadius: '6px',
              backgroundColor: 'rgba(246, 70, 93, 0.1)',
              border: '1px solid var(--color-red)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontWeight: 600, color: 'var(--color-red)' }}>
              <Lock size={15} /> Real-Money Orders Hard-Disabled
            </div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginTop: '6px', lineHeight: 1.5 }}>
              `UpstoxOrderExecutionService` throws an `InvalidOperationException` by design. Only simulated paper trading with realistic slippage and charges is active.
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
