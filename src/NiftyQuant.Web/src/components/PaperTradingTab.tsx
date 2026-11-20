import React, { useState } from 'react';
import {
  Activity,
  AlertOctagon,
  AlertTriangle,
  ArrowDownRight,
  ArrowUpRight,
  CheckCircle2,
  Clock,
  DollarSign,
  Flame,
  HelpCircle,
  Percent,
  Play,
  RefreshCw,
  ShieldCheck,
  StopCircle,
  TrendingDown,
  TrendingUp,
  XCircle,
  Zap,
} from 'lucide-react';
import { api } from '../services/api';
import type {
  AutonomousTradingStatusDto,
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
  autoTradingStatus?: AutonomousTradingStatusDto | null;
  onRefresh: () => void;
}

export const PaperTradingTab: React.FC<PaperTradingTabProps> = ({
  account,
  portfolio,
  openPositions,
  trades,
  orders,
  autoTradingStatus,
  onRefresh,
}) => {
  const [activeSubTab, setActiveSubTab] = useState<'positions' | 'trades' | 'orders'>('positions');
  const [closingPositionId, setClosingPositionId] = useState<string | null>(null);
  const [isSquaringOffAll, setIsSquaringOffAll] = useState(false);
  const [actionFeedback, setActionFeedback] = useState<{ message: string; isError: boolean } | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isTogglingAuto, setIsTogglingAuto] = useState(false);
  const [isTriggeringTestOrder, setIsTriggeringTestOrder] = useState<string | null>(null);

  const isAutoEnabled = autoTradingStatus?.isEnabled ?? true;
  const currentConfidence = autoTradingStatus?.minConfidenceThreshold ?? 65;

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await onRefresh();
    setIsRefreshing(false);
  };

  const handleToggleAutoTrading = async () => {
    setIsTogglingAuto(true);
    setActionFeedback(null);
    try {
      const nextState = !isAutoEnabled;
      const res = await api.toggleAutoTrading({ enabled: nextState });
      setActionFeedback({
        message: nextState
          ? 'Autonomous Trading Engine ACTIVATED: Actively scanning, punching CE/PE buys and managing exits.'
          : 'Autonomous Trading Engine PAUSED: New auto-entries halted. Open positions remain monitored.',
        isError: false,
      });
      await onRefresh();
    } catch (err: any) {
      setActionFeedback({
        message: err.message || 'Failed to toggle auto trading status',
        isError: true,
      });
    } finally {
      setIsTogglingAuto(false);
    }
  };

  const handleChangeConfidence = async (newVal: number) => {
    try {
      await api.toggleAutoTrading({ minConfidence: newVal });
      setActionFeedback({
        message: `Auto-trading minimum confidence threshold updated to ${newVal}%.`,
        isError: false,
      });
      await onRefresh();
    } catch (err: any) {
      setActionFeedback({
        message: err.message || 'Failed to update confidence threshold',
        isError: true,
      });
    }
  };

  const handleTriggerTestOrder = async (optionType: 'CE' | 'PE') => {
    setIsTriggeringTestOrder(optionType);
    setActionFeedback(null);
    try {
      const res = await api.testExecuteOrder(optionType, 25500, 75);
      setActionFeedback({
        message: `[TEST ORDER PUNCHED] BUY ${res.tradingSymbol || (optionType === 'CE' ? 'NIFTY 25500 CE' : 'NIFTY 25500 PE')} @ ₹${res.executedPrice?.toFixed(2) || '125.50'} (SL: ₹${res.stopLossPrice?.toFixed(2) || '100.50'}, Target: ₹${res.targetPrice?.toFixed(2) || '175.50'}). Monitoring active!`,
        isError: false,
      });
      await onRefresh();
    } catch (err: any) {
      setActionFeedback({
        message: err.message || 'Failed to trigger test order execution',
        isError: true,
      });
    } finally {
      setIsTriggeringTestOrder(null);
    }
  };

  const handleClosePosition = async (positionId: string) => {
    if (!window.confirm('Are you sure you want to manually sell/close this position at market price?')) return;
    setClosingPositionId(positionId);
    setActionFeedback(null);

    try {
      const res = await api.closePosition(positionId, 'ManualClose');
      setActionFeedback({
        message: `Sold position: ${res.tradingSymbol || res.instrumentKey} exited @ ₹${res.exitPrice.toFixed(2)} (Net PnL: ₹${res.netPnL.toFixed(2)})`,
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
    if (!window.confirm('EMERGENCY SQUARE-OFF: Are you sure you want to sell/close ALL active open positions immediately?')) {
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

  const formatIST = (dateStr?: string) => {
    if (!dateStr) return '-';
    try {
      const d = new Date(dateStr);
      return d.toLocaleTimeString('en-IN', { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' }) + ' IST';
    } catch {
      return dateStr;
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Autonomous Trading Engine Control Center */}
      <div
        className="card"
        style={{
          background: 'linear-gradient(135deg, rgba(20, 26, 40, 0.95), rgba(15, 23, 42, 0.95))',
          border: isAutoEnabled ? '1px solid rgba(14, 203, 129, 0.4)' : '1px solid rgba(246, 70, 93, 0.4)',
          borderRadius: '10px',
          padding: '16px 20px',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '14px' }}>
          {/* Status & Indicator */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <div
              style={{
                width: '12px',
                height: '12px',
                borderRadius: '50%',
                backgroundColor: isAutoEnabled ? 'var(--color-green)' : 'var(--color-red)',
                boxShadow: isAutoEnabled ? '0 0 10px var(--color-green)' : '0 0 10px var(--color-red)',
                animation: isAutoEnabled ? 'pulse 2s infinite' : 'none',
              }}
            />
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span style={{ fontSize: '1.05rem', fontWeight: 800, color: 'var(--text-primary)' }}>
                  AUTONOMOUS TRADING ENGINE
                </span>
                <span
                  style={{
                    fontSize: '0.7rem',
                    fontWeight: 700,
                    padding: '2px 8px',
                    borderRadius: '4px',
                    backgroundColor: isAutoEnabled ? 'rgba(14, 203, 129, 0.2)' : 'rgba(246, 70, 93, 0.2)',
                    color: isAutoEnabled ? 'var(--color-green)' : 'var(--color-red)',
                  }}
                >
                  {isAutoEnabled ? 'ACTIVE & SCANNING' : 'PAUSED'}
                </span>
                <span
                  style={{
                    fontSize: '0.7rem',
                    fontWeight: 700,
                    padding: '2px 8px',
                    borderRadius: '4px',
                    backgroundColor: 'rgba(59, 130, 246, 0.15)',
                    color: 'var(--color-blue)',
                  }}
                >
                  SIMULATED PAPER MODE
                </span>
              </div>
              <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginTop: '2px' }}>
                Auto-punches BUY orders for identified CE/PE strikes &bull; Trails SL to Breakeven &bull; Auto-sells on Target / SL / 15:15 IST
              </div>
            </div>
          </div>

          {/* Controls & Quick Test Triggers */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
            {/* Confidence Threshold Picker */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
              <span>Min Conf:</span>
              <select
                value={currentConfidence}
                onChange={(e) => handleChangeConfidence(Number(e.target.value))}
                style={{
                  backgroundColor: 'var(--bg-secondary)',
                  color: 'var(--text-primary)',
                  border: '1px solid var(--border-color)',
                  borderRadius: '4px',
                  padding: '4px 8px',
                  fontSize: '0.8rem',
                  cursor: 'pointer',
                }}
              >
                <option value={60}>60%</option>
                <option value={65}>65% (Recommended)</option>
                <option value={70}>70% (Strict)</option>
                <option value={75}>75% (Ultra High)</option>
              </select>
            </div>

            {/* Test Auto BUY CE */}
            <button
              onClick={() => handleTriggerTestOrder('CE')}
              disabled={isTriggeringTestOrder !== null}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '5px',
                padding: '6px 12px',
                borderRadius: '6px',
                backgroundColor: 'rgba(14, 203, 129, 0.15)',
                border: '1px solid var(--color-green)',
                color: 'var(--color-green)',
                fontWeight: 700,
                fontSize: '0.8rem',
                cursor: 'pointer',
              }}
              title="Test the complete automated Buy -> Monitor -> Sell pipeline immediately"
            >
              <Zap size={14} /> {isTriggeringTestOrder === 'CE' ? 'Punching CE...' : 'Test Auto BUY CE'}
            </button>

            {/* Test Auto BUY PE */}
            <button
              onClick={() => handleTriggerTestOrder('PE')}
              disabled={isTriggeringTestOrder !== null}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '5px',
                padding: '6px 12px',
                borderRadius: '6px',
                backgroundColor: 'rgba(246, 70, 93, 0.15)',
                border: '1px solid var(--color-red)',
                color: 'var(--color-red)',
                fontWeight: 700,
                fontSize: '0.8rem',
                cursor: 'pointer',
              }}
              title="Test the complete automated Buy -> Monitor -> Sell pipeline immediately"
            >
              <Zap size={14} /> {isTriggeringTestOrder === 'PE' ? 'Punching PE...' : 'Test Auto BUY PE'}
            </button>

            {/* Master Toggle Button */}
            <button
              onClick={handleToggleAutoTrading}
              disabled={isTogglingAuto}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '6px',
                padding: '7px 14px',
                borderRadius: '6px',
                backgroundColor: isAutoEnabled ? 'rgba(246, 70, 93, 0.2)' : 'rgba(14, 203, 129, 0.2)',
                border: isAutoEnabled ? '1px solid var(--color-red)' : '1px solid var(--color-green)',
                color: isAutoEnabled ? 'var(--color-red)' : 'var(--color-green)',
                fontWeight: 700,
                fontSize: '0.82rem',
                cursor: 'pointer',
              }}
            >
              {isAutoEnabled ? <StopCircle size={15} /> : <Play size={15} />}
              {isAutoEnabled ? 'Pause Auto-Trader' : 'Start Auto-Trader'}
            </button>
          </div>
        </div>
      </div>

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
            Initial Capital: ₹{account ? account.initialCapital.toFixed(2) : '50,000.00'}
          </div>
        </div>

        {/* Realized PnL */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
            <span>REALIZED PNL (NET)</span>
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
            Charges & Taxes: ₹{portfolio ? portfolio.totalChargesPaid.toFixed(2) : '0.00'}
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
            Wins: {portfolio?.winningTrades ?? 0} | Losses: {portfolio?.losingTrades ?? 0} (Total: {portfolio?.totalTrades ?? 0})
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

      {/* Main SubTab Header & Controls */}
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
            Active Positions ({openPositions.length})
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
            Trade Ledger & Exits ({trades.length})
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
              <div style={{ fontSize: '1rem', fontWeight: 600, marginBottom: '6px' }}>
                No active open positions
              </div>
              <div style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>
                The autonomous trading engine is armed. When a qualified CE or PE signal appears, the buy order will be punched automatically.
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {openPositions.map((pos) => {
                const isCe = String(pos.optionType).toUpperCase() === 'CE';
                const pnl = pos.unrealizedPnL;
                const pnlPercent = pos.averageBuyPrice > 0 ? (pnl / (pos.averageBuyPrice * pos.quantity)) * 100 : 0;
                const isProfitable = pnl >= 0;
                const displayName = pos.tradingSymbol || `NIFTY ${pos.strikePrice || ''} ${pos.optionType || 'CE'}`;

                return (
                  <div
                    key={pos.id}
                    className="card"
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      flexWrap: 'wrap',
                      gap: '16px',
                      borderLeft: `4px solid ${isCe ? 'var(--color-green)' : 'var(--color-red)'}`,
                    }}
                  >
                    {/* Contract Details */}
                    <div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '4px',
                            fontWeight: 800,
                            fontSize: '0.75rem',
                            backgroundColor: isCe ? 'rgba(14, 203, 129, 0.2)' : 'rgba(246, 70, 93, 0.2)',
                            color: isCe ? 'var(--color-green)' : 'var(--color-red)',
                          }}
                        >
                          BUY {pos.optionType}
                        </span>
                        <span style={{ fontSize: '1.1rem', fontWeight: 800, fontFamily: 'JetBrains Mono' }}>
                          {displayName}
                        </span>
                        <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                          Qty: {pos.quantity} ({Math.round(pos.quantity / 75)} Lots)
                        </span>
                      </div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                        Entry Time: {formatIST(pos.openedAtUtc)} &bull; Instrument: {pos.instrumentKey}
                      </div>
                    </div>

                    {/* Pricing Metrics */}
                    <div style={{ display: 'flex', gap: '24px', alignItems: 'center', flexWrap: 'wrap' }}>
                      <div>
                        <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>Entry Price</div>
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
                      {closingPositionId === pos.id ? 'Selling...' : 'Sell / Square Off'}
                    </button>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* SubTab 2: Trade Ledger & Exits */}
      {activeSubTab === 'trades' && (
        <div className="card" style={{ overflowX: 'auto' }}>
          {trades.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '36px', color: 'var(--text-secondary)' }}>
              No completed trades in ledger yet. Orders will appear here upon automatic or manual exit.
            </div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)' }}>
                  <th style={{ padding: '10px' }}>Contract</th>
                  <th style={{ padding: '10px' }}>Side / Qty</th>
                  <th style={{ padding: '10px' }}>Entry Time</th>
                  <th style={{ padding: '10px' }}>Sell / Exit Time</th>
                  <th style={{ padding: '10px' }}>Entry (₹)</th>
                  <th style={{ padding: '10px' }}>Exit (₹)</th>
                  <th style={{ padding: '10px' }}>Gross PnL</th>
                  <th style={{ padding: '10px' }}>Charges</th>
                  <th style={{ padding: '10px' }}>Net Profit/Loss</th>
                  <th style={{ padding: '10px' }}>Exit Reason</th>
                </tr>
              </thead>
              <tbody>
                {trades.map((t) => {
                  const isWin = t.netPnL >= 0;
                  const isCe = t.optionType === 'CE' || t.instrumentKey.includes('CE');
                  const returnPct = t.returnPercent ?? (t.entryPrice > 0 ? ((t.exitPrice - t.entryPrice) / t.entryPrice) * 100 : 0);
                  const displayName = t.tradingSymbol || t.instrumentKey;

                  return (
                    <tr key={t.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono', fontWeight: 700 }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                          <span
                            style={{
                              padding: '2px 6px',
                              borderRadius: '4px',
                              fontSize: '0.7rem',
                              fontWeight: 800,
                              backgroundColor: isCe ? 'rgba(14, 203, 129, 0.2)' : 'rgba(246, 70, 93, 0.2)',
                              color: isCe ? 'var(--color-green)' : 'var(--color-red)',
                            }}
                          >
                            {isCe ? 'CE' : 'PE'}
                          </span>
                          <span>{displayName}</span>
                        </div>
                      </td>
                      <td style={{ padding: '10px' }}>
                        BUY / {t.quantity} shares
                      </td>
                      <td style={{ padding: '10px', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                        {formatIST(t.entryTimeUtc)}
                      </td>
                      <td style={{ padding: '10px', fontSize: '0.8rem', color: 'var(--text-secondary)', fontWeight: 600 }}>
                        {formatIST(t.exitTimeUtc)}
                        {t.durationMinutes ? (
                          <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)' }}>
                            ({t.durationMinutes.toFixed(1)} mins)
                          </div>
                        ) : null}
                      </td>
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono' }}>₹{t.entryPrice.toFixed(2)}</td>
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono', fontWeight: 600 }}>₹{t.exitPrice.toFixed(2)}</td>
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
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono', color: 'var(--text-muted)' }}>
                        ₹{t.totalCharges.toFixed(2)}
                      </td>
                      <td
                        style={{
                          padding: '10px',
                          fontFamily: 'JetBrains Mono',
                          fontWeight: 800,
                        }}
                      >
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '4px',
                            backgroundColor: isWin ? 'rgba(14, 203, 129, 0.15)' : 'rgba(246, 70, 93, 0.15)',
                            color: isWin ? 'var(--color-green)' : 'var(--color-red)',
                          }}
                        >
                          {isWin ? '+' : ''}₹{t.netPnL.toFixed(2)} ({isWin ? '+' : ''}{returnPct.toFixed(1)}%)
                        </span>
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
                                : t.exitReason.includes('SquareOff')
                                ? 'rgba(245, 158, 11, 0.15)'
                                : 'rgba(59, 130, 246, 0.15)',
                            color:
                              t.exitReason === 'TargetHit'
                                ? 'var(--color-green)'
                                : t.exitReason === 'StopLossHit'
                                ? 'var(--color-red)'
                                : t.exitReason.includes('SquareOff')
                                ? 'var(--color-amber)'
                                : 'var(--color-blue)',
                          }}
                        >
                          {t.exitReason}
                        </span>
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
              No orders logged in history yet.
            </div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)' }}>
                  <th style={{ padding: '10px' }}>Order Time</th>
                  <th style={{ padding: '10px' }}>Contract</th>
                  <th style={{ padding: '10px' }}>Side</th>
                  <th style={{ padding: '10px' }}>Qty</th>
                  <th style={{ padding: '10px' }}>Ordered</th>
                  <th style={{ padding: '10px' }}>Executed</th>
                  <th style={{ padding: '10px' }}>Slippage</th>
                  <th style={{ padding: '10px' }}>Charges</th>
                  <th style={{ padding: '10px' }}>Status</th>
                  <th style={{ padding: '10px' }}>Details / Reason</th>
                </tr>
              </thead>
              <tbody>
                {orders.map((o) => {
                  const sideStr = typeof o.side === 'string' ? o.side : (o.side === 1 ? 'BUY' : 'SELL');
                  const statusStr = typeof o.status === 'string' ? o.status : (o.status === 2 ? 'Filled' : o.status === 3 ? 'Rejected' : 'Submitted');
                  const isBuy = sideStr.toUpperCase() === 'BUY';
                  const isFilled = statusStr.toLowerCase() === 'filled';
                  const displayName = o.tradingSymbol || o.instrumentKey;

                  return (
                    <tr key={o.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                      <td style={{ padding: '10px', fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                        {formatIST(o.orderTimeUtc)}
                      </td>
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono', fontWeight: 600 }}>
                        {displayName}
                      </td>
                      <td style={{ padding: '10px' }}>
                        <span
                          style={{
                            padding: '2px 8px',
                            borderRadius: '4px',
                            fontWeight: 700,
                            fontSize: '0.75rem',
                            backgroundColor: isBuy ? 'rgba(14, 203, 129, 0.15)' : 'rgba(246, 70, 93, 0.15)',
                            color: isBuy ? 'var(--color-green)' : 'var(--color-red)',
                          }}
                        >
                          {sideStr}
                        </span>
                      </td>
                      <td style={{ padding: '10px' }}>{o.quantity}</td>
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono' }}>₹{o.orderedPrice.toFixed(2)}</td>
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono', fontWeight: 700 }}>₹{o.executedPrice.toFixed(2)}</td>
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono', color: 'var(--text-muted)' }}>₹{o.slippage.toFixed(2)}</td>
                      <td style={{ padding: '10px', fontFamily: 'JetBrains Mono', color: 'var(--text-muted)' }}>₹{o.estimatedCharges.toFixed(2)}</td>
                      <td style={{ padding: '10px' }}>
                        <span
                          style={{
                            padding: '2px 8px',
                            borderRadius: '4px',
                            fontSize: '0.75rem',
                            fontWeight: 700,
                            backgroundColor: isFilled ? 'rgba(14, 203, 129, 0.2)' : 'rgba(246, 70, 93, 0.2)',
                            color: isFilled ? 'var(--color-green)' : 'var(--color-red)',
                          }}
                        >
                          {statusStr}
                        </span>
                      </td>
                      <td style={{ padding: '10px', fontSize: '0.75rem', color: o.rejectReason ? 'var(--color-red)' : 'var(--text-muted)' }}>
                        {o.rejectReason || 'Executed successfully'}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
};
