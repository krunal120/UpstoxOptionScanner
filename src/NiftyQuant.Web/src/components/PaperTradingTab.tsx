import React, { useState } from 'react';
import {
  AlertOctagon,
  AlertTriangle,
  ArrowDownRight,
  ArrowUpRight,
  CheckCircle2,
  Clock,
  DollarSign,
  HelpCircle,
  Percent,
  RefreshCw,
  ShieldCheck,
  TrendingDown,
  TrendingUp,
  XCircle,
} from 'lucide-react';
import { api } from '../services/api';
import type {
  PaperAccountDto,
  PaperOrderDto,
  PaperPortfolioSummaryDto,
  PaperPositionDto,
  PaperTradeDto,
} from '../types';

interface PaperTradingTabProps {
  account: PaperAccountDto | null;
  portfolio: PaperPortfolioSummaryDto | null;
  openPositions: PaperPositionDto[];
  trades: PaperTradeDto[];
  orders: PaperOrderDto[];
  onRefresh: () => void;
}

export const PaperTradingTab: React.FC<PaperTradingTabProps> = ({
  account,
  portfolio,
  openPositions,
  trades,
  orders,
  onRefresh,
}) => {
  const [activeSubTab, setActiveSubTab] = useState<'positions' | 'trades' | 'orders'>('positions');
  const [closingPositionId, setClosingPositionId] = useState<string | null>(null);
  const [isSquaringOffAll, setIsSquaringOffAll] = useState(false);
  const [actionFeedback, setActionFeedback] = useState<{ message: string; isError: boolean } | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await onRefresh();
    setIsRefreshing(false);
  };

  const handleClosePosition = async (positionId: string) => {
    if (!window.confirm('Are you sure you want to manually close this position at market price?')) return;
    setClosingPositionId(positionId);
    setActionFeedback(null);

    try {
      const res = await api.closePosition(positionId, 'ManualClose');
      setActionFeedback({
        message: `Position closed: ${res.instrumentKey} exited @ ₹${res.exitPrice.toFixed(2)} (Net PnL: ₹${res.netPnL.toFixed(2)})`,
        isError: false,
      });
      await onRefresh();
    } catch (err: any) {
      setActionFeedback({
        message: err.message || 'Failed to close position',
        isError: true,
      });
    } finally {
      setClosingPositionId(null);
    }
  };

  const handleSquareOffAll = async () => {
    if (!window.confirm('EMERGENCY SQUARE-OFF: Are you sure you want to close ALL active open positions immediately?')) {
      return;
    }
    setIsSquaringOffAll(true);
    setActionFeedback(null);

    try {
      const res = await api.squareOffAll();
      setActionFeedback({
        message: res.message || 'All positions successfully squared off.',
        isError: false,
      });
      await onRefresh();
    } catch (err: any) {
      setActionFeedback({
        message: err.message || 'Failed to square off positions',
        isError: true,
      });
    } finally {
      setIsSquaringOffAll(false);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Portfolio & Account Top Cards */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
          gap: '14px',
        }}
      >
        {/* Available Capital */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
            <span>AVAILABLE BALANCE</span>
            <DollarSign size={16} color="var(--color-blue)" />
          </div>
          <div style={{ fontSize: '1.4rem', fontWeight: 800, fontFamily: 'JetBrains Mono', marginTop: '6px' }}>
            ₹{account ? account.availableBalance.toFixed(2) : '-'}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
            Capital: ₹{account ? account.initialCapital.toFixed(2) : '50,000.00'}
          </div>
        </div>

        {/* Realized PnL */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
            <span>REALIZED PNL</span>
            {(account?.realizedPnL ?? 0) >= 0 ? (
              <TrendingUp size={16} color="var(--color-green)" />
            ) : (
              <TrendingDown size={16} color="var(--color-red)" />
            )}
          </div>
          <div
            style={{
              fontSize: '1.4rem',
              fontWeight: 800,
              fontFamily: 'JetBrains Mono',
              marginTop: '6px',
              color: (account?.realizedPnL ?? 0) >= 0 ? 'var(--color-green)' : 'var(--color-red)',
            }}
          >
            {(account?.realizedPnL ?? 0) >= 0 ? '+' : ''}₹{account ? account.realizedPnL.toFixed(2) : '0.00'}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
            Charges Paid: ₹{portfolio ? portfolio.totalChargesPaid.toFixed(2) : '0.00'}
          </div>
        </div>

        {/* Unrealized PnL */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
            <span>OPEN UNREALIZED PNL</span>
            <Clock size={16} color="var(--color-amber)" />
          </div>
          <div
            style={{
              fontSize: '1.4rem',
              fontWeight: 800,
              fontFamily: 'JetBrains Mono',
              marginTop: '6px',
              color: (portfolio?.activeUnrealizedPnL ?? 0) >= 0 ? 'var(--color-green)' : 'var(--color-red)',
            }}
          >
            {(portfolio?.activeUnrealizedPnL ?? 0) >= 0 ? '+' : ''}₹
            {portfolio ? portfolio.activeUnrealizedPnL.toFixed(2) : '0.00'}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
            Active Positions: {openPositions.length}
          </div>
        </div>

        {/* Win Rate & Profit Factor */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
            <span>WIN RATE & FACTOR</span>
            <Percent size={16} color="var(--color-purple)" />
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px', marginTop: '6px' }}>
            <span
              style={{
                fontSize: '1.4rem',
                fontWeight: 800,
                fontFamily: 'JetBrains Mono',
                color: (portfolio?.winRatePercent ?? 0) >= 50 ? 'var(--color-green)' : 'var(--color-amber)',
              }}
            >
              {portfolio ? `${portfolio.winRatePercent.toFixed(1)}%` : '0.0%'}
            </span>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
              PF: <strong style={{ color: 'var(--text-primary)' }}>{portfolio ? portfolio.profitFactor.toFixed(2) : '0.00'}</strong>
            </span>
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
            W: {portfolio?.winningTrades ?? 0} | L: {portfolio?.losingTrades ?? 0} (Total: {portfolio?.totalTrades ?? 0})
          </div>
        </div>

        {/* Expectancy & Max Drawdown */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
            <span>EXPECTANCY / DRAWDOWN</span>
            <ShieldCheck size={16} color="var(--color-blue)" />
          </div>
          <div style={{ fontSize: '1.4rem', fontWeight: 800, fontFamily: 'JetBrains Mono', marginTop: '6px' }}>
            ₹{portfolio ? portfolio.expectancy.toFixed(2) : '0.00'}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--color-red)', marginTop: '4px' }}>
            Max DD: ₹{portfolio ? portfolio.maxDrawdownAmount.toFixed(2) : '0.00'} (
            {portfolio ? portfolio.maxDrawdownPercent.toFixed(1) : '0.0'}%)
          </div>
        </div>
      </div>

      {/* Action Feedback Banner */}
      {actionFeedback && (
        <div
          style={{
            padding: '12px 16px',
            borderRadius: '8px',
            display: 'flex',
            alignItems: 'center',
            gap: '10px',
            backgroundColor: actionFeedback.isError ? 'rgba(246, 70, 93, 0.15)' : 'rgba(14, 203, 129, 0.15)',
            border: `1px solid ${actionFeedback.isError ? 'var(--color-red)' : 'var(--color-green)'}`,
            color: actionFeedback.isError ? 'var(--color-red)' : 'var(--color-green)',
            fontSize: '0.85rem',
          }}
        >
          {actionFeedback.isError ? <AlertTriangle size={18} /> : <CheckCircle2 size={18} />}
          <span>{actionFeedback.message}</span>
        </div>
      )}

      {/* Main SubTab Header & Emergency Square Off */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '12px',
          borderBottom: '1px solid var(--border-color)',
          paddingBottom: '10px',
        }}
      >
        <div style={{ display: 'flex', gap: '8px' }}>
          <button
            onClick={() => setActiveSubTab('positions')}
            style={{
              padding: '8px 16px',
              borderRadius: '6px',
              fontWeight: 600,
              fontSize: '0.85rem',
              cursor: 'pointer',
              border: activeSubTab === 'positions' ? '1px solid var(--color-blue)' : '1px solid var(--border-color)',
              backgroundColor: activeSubTab === 'positions' ? 'rgba(59, 130, 246, 0.15)' : 'transparent',
              color: activeSubTab === 'positions' ? 'var(--color-blue)' : 'var(--text-secondary)',
            }}
          >
            Open Positions ({openPositions.length})
          </button>
          <button
            onClick={() => setActiveSubTab('trades')}
            style={{
              padding: '8px 16px',
              borderRadius: '6px',
              fontWeight: 600,
              fontSize: '0.85rem',
              cursor: 'pointer',
              border: activeSubTab === 'trades' ? '1px solid var(--color-blue)' : '1px solid var(--border-color)',
              backgroundColor: activeSubTab === 'trades' ? 'rgba(59, 130, 246, 0.15)' : 'transparent',
              color: activeSubTab === 'trades' ? 'var(--color-blue)' : 'var(--text-secondary)',
            }}
          >
            Trade Ledger ({trades.length})
          </button>
          <button
            onClick={() => setActiveSubTab('orders')}
            style={{
              padding: '8px 16px',
              borderRadius: '6px',
              fontWeight: 600,
              fontSize: '0.85rem',
              cursor: 'pointer',
              border: activeSubTab === 'orders' ? '1px solid var(--color-blue)' : '1px solid var(--border-color)',
              backgroundColor: activeSubTab === 'orders' ? 'rgba(59, 130, 246, 0.15)' : 'transparent',
              color: activeSubTab === 'orders' ? 'var(--color-blue)' : 'var(--text-secondary)',
            }}
          >
            Order Log ({orders.length})
          </button>
        </div>

        <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
          <button
            onClick={handleRefresh}
            disabled={isRefreshing}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
              padding: '6px 12px',
              borderRadius: '6px',
              backgroundColor: 'var(--bg-secondary)',
              border: '1px solid var(--border-color)',
              color: 'var(--text-secondary)',
              fontSize: '0.8rem',
              cursor: 'pointer',
            }}
          >
            <RefreshCw size={14} className={isRefreshing ? 'spin' : ''} /> Refresh
          </button>

          {openPositions.length > 0 && (
            <button
              onClick={handleSquareOffAll}
              disabled={isSquaringOffAll}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '6px',
                padding: '6px 14px',
                borderRadius: '6px',
                backgroundColor: 'rgba(246, 70, 93, 0.2)',
                border: '1px solid var(--color-red)',
                color: 'var(--color-red)',
                fontWeight: 700,
                fontSize: '0.8rem',
                cursor: 'pointer',
              }}
            >
              <AlertOctagon size={15} /> Square Off All
            </button>
          )}
        </div>
      </div>

      {/* SubTab 1: Open Positions */}
      {activeSubTab === 'positions' && (
        <div>
          {openPositions.length === 0 ? (
            <div className="card" style={{ textAlign: 'center', padding: '48px', color: 'var(--text-secondary)' }}>
              No open paper positions currently active. Ready for new signal entries.
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {openPositions.map((pos) => {
                const pnl = pos.unrealizedPnL;
                const pnlPercent = ((pos.currentLtp - pos.averageBuyPrice) / pos.averageBuyPrice) * 100;
                const isProfitable = pnl >= 0;

                return (
                  <div
                    key={pos.id}
                    className="card"
                    style={{
                      display: 'flex',
                      flexWrap: 'wrap',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      gap: '16px',
                      borderLeft: `4px solid ${isProfitable ? 'var(--color-green)' : 'var(--color-red)'}`,
                    }}
                  >
                    <div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '4px',
                            fontWeight: 800,
                            fontSize: '0.75rem',
                            backgroundColor: pos.optionType === 'CE' ? 'rgba(14, 203, 129, 0.2)' : 'rgba(246, 70, 93, 0.2)',
                            color: pos.optionType === 'CE' ? 'var(--color-green)' : 'var(--color-red)',
                          }}
                        >
                          {pos.optionType}
                        </span>
                        <span style={{ fontWeight: 700, fontSize: '1rem', fontFamily: 'JetBrains Mono' }}>
                          ₹{pos.strikePrice}
                        </span>
                        <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                          Qty: {pos.quantity} ({pos.quantity / 65} Lots)
                        </span>
                        <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Exp: {pos.expiryDate}</span>
                      </div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginTop: '4px' }}>
                        Instrument: {pos.instrumentKey}
                      </div>
                    </div>

                    {/* Price Metrics */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '24px' }}>
                      <div>
                        <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Avg Buy</div>
                        <div style={{ fontSize: '1rem', fontWeight: 700, fontFamily: 'JetBrains Mono' }}>
                          ₹{pos.averageBuyPrice.toFixed(2)}
                        </div>
                      </div>
                      <div>
                        <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Current LTP</div>
                        <div
                          style={{
                            fontSize: '1rem',
                            fontWeight: 700,
                            fontFamily: 'JetBrains Mono',
                            color: isProfitable ? 'var(--color-green)' : 'var(--color-red)',
                          }}
                        >
                          ₹{pos.currentLtp.toFixed(2)}
                        </div>
                      </div>
                      <div>
                        <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Stop Loss</div>
                        <div style={{ fontSize: '1rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-red)' }}>
                          ₹{pos.stopLossPrice.toFixed(2)}
                        </div>
                      </div>
                      <div>
                        <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Target</div>
                        <div style={{ fontSize: '1rem', fontWeight: 700, fontFamily: 'JetBrains Mono', color: 'var(--color-green)' }}>
                          ₹{pos.targetPrice.toFixed(2)}
                        </div>
                      </div>
                      <div>
                        <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Unrealized PnL</div>
                        <div
                          style={{
                            fontSize: '1.2rem',
                            fontWeight: 800,
                            fontFamily: 'JetBrains Mono',
                            color: isProfitable ? 'var(--color-green)' : 'var(--color-red)',
                          }}
                        >
                          {isProfitable ? '+' : ''}₹{pnl.toFixed(2)} ({isProfitable ? '+' : ''}
                          {pnlPercent.toFixed(2)}%)
                        </div>
                      </div>
                    </div>

                    {/* Action Button */}
                    <button
                      onClick={() => handleClosePosition(pos.id)}
                      disabled={closingPositionId === pos.id}
                      style={{
                        padding: '8px 16px',
                        borderRadius: '6px',
                        backgroundColor: 'rgba(246, 70, 93, 0.15)',
                        border: '1px solid var(--color-red)',
                        color: 'var(--color-red)',
                        fontWeight: 700,
                        fontSize: '0.8rem',
                        cursor: 'pointer',
                      }}
                    >
                      {closingPositionId === pos.id ? 'Closing...' : 'Close Position'}
                    </button>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* SubTab 2: Trade Ledger */}
      {activeSubTab === 'trades' && (
        <div className="card" style={{ overflowX: 'auto' }}>
          {trades.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '36px', color: 'var(--text-secondary)' }}>
              No completed trades in ledger yet.
            </div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)' }}>
                  <th style={{ padding: '10px' }}>Instrument</th>
                  <th style={{ padding: '10px' }}>Qty</th>
                  <th style={{ padding: '10px' }}>Entry</th>
                  <th style={{ padding: '10px' }}>Exit</th>
                  <th style={{ padding: '10px' }}>Charges</th>
                  <th style={{ padding: '10px' }}>Gross PnL</th>
                  <th style={{ padding: '10px' }}>Net PnL</th>
                  <th style={{ padding: '10px' }}>Exit Reason</th>
                  <th style={{ padding: '10px' }}>Exit Time</th>
                </tr>
              </thead>
              <tbody>
                {trades.map((t) => {
                  const isWin = t.netPnL >= 0;
                  return (
                    <tr key={t.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono', fontSize: '0.8rem' }}>{t.instrumentKey}</td>
                      <td style={{ padding: '10px' }}>{t.quantity}</td>
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono' }}>₹{t.entryPrice.toFixed(2)}</td>
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono' }}>₹{t.exitPrice.toFixed(2)}</td>
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono', color: 'var(--text-muted)' }}>
                        ₹{t.totalCharges.toFixed(2)}
                      </td>
                      <td
                        style={{
                          padding: '10px',
                          fontFamily: 'JetBrains Mono',
                          fontWeight: 600,
                          color: t.grossPnL >= 0 ? 'var(--color-green)' : 'var(--color-red)',
                        }}
                      >
                        {t.grossPnL >= 0 ? '+' : ''}₹{t.grossPnL.toFixed(2)}
                      </td>
                      <td
                        style={{
                          padding: '10px',
                          fontFamily: 'JetBrains Mono',
                          fontWeight: 700,
                          color: isWin ? 'var(--color-green)' : 'var(--color-red)',
                        }}
                      >
                        {isWin ? '+' : ''}₹{t.netPnL.toFixed(2)}
                      </td>
                      <td style={{ padding: '10px' }}>
                        <span
                          style={{
                            padding: '2px 8px',
                            borderRadius: '4px',
                            fontSize: '0.75rem',
                            fontWeight: 600,
                            backgroundColor:
                              t.exitReason === 'TargetHit'
                                ? 'rgba(14, 203, 129, 0.15)'
                                : t.exitReason === 'StopLossHit'
                                ? 'rgba(246, 70, 93, 0.15)'
                                : 'rgba(59, 130, 246, 0.15)',
                            color:
                              t.exitReason === 'TargetHit'
                                ? 'var(--color-green)'
                                : t.exitReason === 'StopLossHit'
                                ? 'var(--color-red)'
                                : 'var(--color-blue)',
                          }}
                        >
                          {t.exitReason}
                        </span>
                      </td>
                      <td style={{ padding: '10px', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                        {new Date(t.exitTimeUtc).toLocaleTimeString()}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* SubTab 3: Order Log */}
      {activeSubTab === 'orders' && (
        <div className="card" style={{ overflowX: 'auto' }}>
          {orders.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '36px', color: 'var(--text-secondary)' }}>
              No order executions logged yet.
            </div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)' }}>
                  <th style={{ padding: '10px' }}>Instrument</th>
                  <th style={{ padding: '10px' }}>Side</th>
                  <th style={{ padding: '10px' }}>Qty</th>
                  <th style={{ padding: '10px' }}>Ordered Price</th>
                  <th style={{ padding: '10px' }}>Executed Price</th>
                  <th style={{ padding: '10px' }}>Slippage</th>
                  <th style={{ padding: '10px' }}>Status</th>
                  <th style={{ padding: '10px' }}>Time</th>
                </tr>
              </thead>
              <tbody>
                {orders.map((o) => (
                  <tr key={o.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                    <td style={{ padding: '10px', fontFamily: 'JetBrains Mono', fontSize: '0.8rem' }}>{o.instrumentKey}</td>
                    <td style={{ padding: '10px', fontWeight: 700, color: o.side === 1 ? 'var(--color-green)' : 'var(--color-red)' }}>
                      {o.side === 1 ? 'BUY' : 'SELL'}
                    </td>
                    <td style={{ padding: '10px' }}>{o.quantity}</td>
                    <td style={{ padding: '10px', fontFamily: 'JetBrains Mono' }}>₹{o.orderedPrice.toFixed(2)}</td>
                    <td style={{ padding: '10px', fontFamily: 'JetBrains Mono' }}>₹{o.executedPrice.toFixed(2)}</td>
                    <td style={{ padding: '10px', fontFamily: 'JetBrains Mono', color: 'var(--text-muted)' }}>
                      ₹{o.slippage.toFixed(2)}
                    </td>
                    <td style={{ padding: '10px' }}>
                      <span
                        style={{
                          padding: '2px 8px',
                          borderRadius: '4px',
                          fontSize: '0.75rem',
                          fontWeight: 600,
                          backgroundColor: o.status === 2 ? 'rgba(14, 203, 129, 0.15)' : 'rgba(246, 70, 93, 0.15)',
                          color: o.status === 2 ? 'var(--color-green)' : 'var(--color-red)',
                        }}
                      >
                        {o.status === 2 ? 'FILLED' : o.status === 3 ? 'REJECTED' : 'PENDING'}
                      </span>
                    </td>
                    <td style={{ padding: '10px', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                      {new Date(o.orderTimeUtc).toLocaleTimeString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
};
