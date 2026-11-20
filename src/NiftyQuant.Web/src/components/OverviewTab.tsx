import React, { useState } from 'react';
import {
  AlertTriangle,
  ArrowDownRight,
  ArrowUpRight,
  CheckCircle2,
  Clock,
  Compass,
  DollarSign,
  Layers,
  Play,
  RefreshCw,
  ShieldAlert,
  Target,
  TrendingDown,
  TrendingUp,
  Zap,
} from 'lucide-react';
import { api } from '../services/api';
import { formatToIST } from '../utils/dateUtils';
import type {
  DailyRiskStatusDto,
  MarketRegimeResultDto,
  OptionsReportDto,
  PaperPortfolioSummaryDto,
  SelectedOptionContractDto,
  StrategySignalDto,
} from '../types';

interface OverviewTabProps {
  latestSignal: StrategySignalDto | null;
  signalHistory: StrategySignalDto[];
  regime: MarketRegimeResultDto | null;
  riskStatus: DailyRiskStatusDto | null;
  portfolio: PaperPortfolioSummaryDto | null;
  optionsReport: OptionsReportDto | null;
  onRefresh: () => void;
}

export const OverviewTab: React.FC<OverviewTabProps> = ({
  latestSignal,
  signalHistory,
  riskStatus,
  portfolio,
  optionsReport,
  onRefresh,
}) => {
  const [isExecuting, setIsExecuting] = useState(false);
  const [isEvaluating, setIsEvaluating] = useState(false);
  const [executionMessage, setExecutionMessage] = useState<{ text: string; isError: boolean } | null>(null);

  const handleExecuteTrade = async (signalToExecute?: StrategySignalDto) => {
    const target = signalToExecute || latestSignal;
    if (!target) return;
    setIsExecuting(true);
    setExecutionMessage(null);

    try {
      const res = await api.executeSignal(target);
      setExecutionMessage({
        text: `Order Executed! Opened ${res.quantity} shares of ${res.instrumentKey} @ ₹${res.executedPrice.toFixed(2)} (Target: ₹${target.target1Premium.toFixed(2)}, SL: ₹${target.stopLossPremium.toFixed(2)})`,
        isError: false,
      });
      await onRefresh();
    } catch (err: any) {
      setExecutionMessage({
        text: err.message || 'Trade execution rejected by risk engine',
        isError: true,
      });
    } finally {
      setIsExecuting(false);
    }
  };

  const handleEvaluateNow = async () => {
    setIsEvaluating(true);
    setExecutionMessage(null);
    try {
      await api.evaluateStrategy();
      await onRefresh();
    } catch (err: any) {
      setExecutionMessage({
        text: err.message || 'Strategy evaluation failed',
        isError: true,
      });
    } finally {
      setIsEvaluating(false);
    }
  };

  const handleBuySpecificContract = async (contract: SelectedOptionContractDto) => {
    setIsExecuting(true);
    setExecutionMessage(null);

    try {
      const slDist = Math.max(5, contract.ltp * 0.1);
      const tgtDist = slDist * 2;

      const syntheticSignal: StrategySignalDto = {
        id: '00000000-0000-0000-0000-000000000000',
        signalTimeUtc: new Date().toISOString(),
        underlyingKey: optionsReport?.underlyingKey || 'NSE_INDEX|Nifty 50',
        underlyingPrice: optionsReport?.underlyingPrice || 25500,
        direction: contract.optionType === 'CE' ? 'BuyCe' : 'BuyPe',
        strategyFamily: 'TrendFollowing',
        marketRegime: 'Transitional',
        bullishScore: 80,
        bearishScore: 20,
        confidence: 80,
        selectedInstrumentKey: contract.instrumentKey,
        selectedStrike: contract.strikePrice,
        selectedOptionType: contract.optionType,
        selectedExpiry: contract.expiryDate,
        entryPremium: contract.ltp,
        stopLossPremium: Math.max(1, Math.round((contract.ltp - slDist) * 100) / 100),
        target1Premium: Math.round((contract.ltp + tgtDist) * 100) / 100,
        target2Premium: Math.round((contract.ltp + tgtDist * 1.5) * 100) / 100,
        recommendedQuantity: 65,
        lotSize: 65,
        riskAmount: Math.round(slDist * 65),
        rewardToRiskRatio: 2.0,
        isExecuted: false,
        reasons: [{ category: 'OptionSelection', scoreContribution: 30, description: contract.selectionReason }],
      };

      const res = await api.executeSignal(syntheticSignal);
      setExecutionMessage({
        text: `Paper Order Filled: 1 Lot (65 Qty) of ${contract.strikePrice} ${contract.optionType} @ ₹${res.executedPrice.toFixed(2)}`,
        isError: false,
      });
      await onRefresh();
    } catch (err: any) {
      setExecutionMessage({
        text: err.message || 'Order rejected by risk engine',
        isError: true,
      });
    } finally {
      setIsExecuting(false);
    }
  };

  // Determine signal bias
  const isBullish = (latestSignal?.bullishScore ?? 0) >= (latestSignal?.bearishScore ?? 0) && (latestSignal?.bullishScore ?? 0) > 0;
  const isBearish = (latestSignal?.bearishScore ?? 0) > (latestSignal?.bullishScore ?? 0);
  const isTimeVetoed = latestSignal?.reasons?.some((r) => r.category === 'RiskVeto');
  const hasSelectedContract = Boolean(latestSignal?.selectedStrike && latestSignal.selectedStrike > 0);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Top Capital & Status Bar */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
          gap: '14px',
        }}
      >
        <div className="card">
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>CURRENT CAPITAL</div>
          <div style={{ fontSize: '1.4rem', fontWeight: 800, fontFamily: 'JetBrains Mono', marginTop: '4px' }}>
            ₹{portfolio ? portfolio.currentCapital.toFixed(2) : '50,000.00'}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
            Available: ₹{riskStatus ? riskStatus.availableBalance.toFixed(2) : '50,000.00'}
          </div>
        </div>

        <div className="card">
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>TODAY'S NET P&L</div>
          <div
            style={{
              fontSize: '1.4rem',
              fontWeight: 800,
              fontFamily: 'JetBrains Mono',
              marginTop: '4px',
              color: (riskStatus?.totalPnLToday ?? 0) >= 0 ? 'var(--color-green)' : 'var(--color-red)',
            }}
          >
            {(riskStatus?.totalPnLToday ?? 0) >= 0 ? '+' : ''}₹{riskStatus ? riskStatus.totalPnLToday.toFixed(2) : '0.00'}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
            Loss Buffer: ₹{riskStatus ? riskStatus.dailyLossRemaining.toFixed(2) : '750.00'}
          </div>
        </div>

        <div className="card">
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>WIN RATE & TRADES</div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px', marginTop: '4px' }}>
            <span style={{ fontSize: '1.4rem', fontWeight: 800, fontFamily: 'JetBrains Mono', color: 'var(--color-green)' }}>
              {portfolio ? `${portfolio.winRatePercent.toFixed(1)}%` : '0.0%'}
            </span>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
              ({portfolio?.winningTrades ?? 0}W / {portfolio?.losingTrades ?? 0}L)
            </span>
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '4px' }}>
            Expectancy: ₹{portfolio ? portfolio.expectancy.toFixed(2) : '0.00'}
          </div>
        </div>

        <div className="card" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>STRATEGY SCANNER</div>
          <button
            onClick={handleEvaluateNow}
            disabled={isEvaluating}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '8px',
              padding: '8px 14px',
              borderRadius: '6px',
              backgroundColor: 'rgba(59, 130, 246, 0.2)',
              border: '1px solid var(--color-blue)',
              color: 'var(--color-blue)',
              fontWeight: 700,
              fontSize: '0.85rem',
              cursor: 'pointer',
              marginTop: '6px',
            }}
          >
            <RefreshCw size={14} className={isEvaluating ? 'spin' : ''} />
            {isEvaluating ? 'Evaluating...' : 'Evaluate Strategy Now'}
          </button>
        </div>
      </div>

      {/* Main Signal Action Banner (Spotlight Card) */}
      <div
        className="card"
        style={{
          border: isBullish
            ? '2px solid rgba(14, 203, 129, 0.6)'
            : isBearish
            ? '2px solid rgba(246, 70, 93, 0.6)'
            : '1px solid var(--border-color)',
          boxShadow: isBullish
            ? '0 0 25px rgba(14, 203, 129, 0.15)'
            : isBearish
            ? '0 0 25px rgba(246, 70, 93, 0.15)'
            : 'none',
          padding: '24px',
        }}
      >
        {/* Signal Direction Header */}
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: '14px',
            marginBottom: '20px',
            borderBottom: '1px solid var(--border-color)',
            paddingBottom: '16px',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '14px', flexWrap: 'wrap' }}>
            {/* Direction Pill */}
            <div
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '10px',
                padding: '10px 20px',
                borderRadius: '8px',
                fontWeight: 900,
                fontSize: '1.25rem',
                letterSpacing: '0.5px',
                backgroundColor: isBullish
                  ? 'rgba(14, 203, 129, 0.2)'
                  : isBearish
                  ? 'rgba(246, 70, 93, 0.2)'
                  : 'rgba(154, 165, 190, 0.15)',
                color: isBullish ? 'var(--color-green)' : isBearish ? 'var(--color-red)' : 'var(--text-secondary)',
                border: `2px solid ${isBullish ? 'var(--color-green)' : isBearish ? 'var(--color-red)' : 'var(--border-color)'}`,
              }}
            >
              {isBullish ? <TrendingUp size={22} /> : isBearish ? <TrendingDown size={22} /> : <Clock size={22} />}
              <span>
                {hasSelectedContract
                  ? `BUY ${latestSignal?.selectedStrike} ${latestSignal?.selectedOptionType}`
                  : latestSignal?.direction === 'Wait'
                  ? 'MONITORING FOR SETUP (WAIT)'
                  : 'NEUTRAL STANDBY'}
              </span>
            </div>

            {/* Strategy & Confidence Tags */}
            <div style={{ display: 'flex', gap: '8px' }}>
              <span
                style={{
                  padding: '6px 12px',
                  borderRadius: '6px',
                  backgroundColor: 'rgba(59, 130, 246, 0.15)',
                  color: 'var(--color-blue)',
                  fontWeight: 700,
                  fontSize: '0.85rem',
                }}
              >
                Strategy: {latestSignal?.strategyFamily || 'Breakout'}
              </span>
              <span
                style={{
                  padding: '6px 12px',
                  borderRadius: '6px',
                  backgroundColor: 'rgba(168, 85, 247, 0.15)',
                  color: 'var(--color-purple)',
                  fontWeight: 700,
                  fontSize: '0.85rem',
                }}
              >
                Confidence: {latestSignal?.confidence || 85}%
              </span>
              {isTimeVetoed && (
                <span
                  style={{
                    padding: '6px 12px',
                    borderRadius: '6px',
                    backgroundColor: 'rgba(245, 158, 11, 0.15)',
                    color: 'var(--color-amber)',
                    fontWeight: 700,
                    fontSize: '0.85rem',
                  }}
                >
                  Market Closed (Pre-Market Setup)
                </span>
              )}
            </div>
          </div>

          {/* Execution Button */}
          <button
            onClick={() => handleExecuteTrade()}
            disabled={isExecuting || !hasSelectedContract}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
              padding: '12px 24px',
              borderRadius: '8px',
              backgroundColor: hasSelectedContract
                ? isBullish
                  ? 'var(--color-green)'
                  : 'var(--color-red)'
                : 'var(--border-color)',
              color: '#fff',
              border: 'none',
              fontWeight: 800,
              fontSize: '0.95rem',
              cursor: hasSelectedContract ? 'pointer' : 'not-allowed',
              opacity: isExecuting ? 0.7 : 1,
            }}
          >
            <Play size={18} />
            {isExecuting ? 'Submitting Order...' : `Execute Paper Trade (${latestSignal?.selectedStrike || 25500} ${latestSignal?.selectedOptionType || 'CE'})`}
          </button>
        </div>

        {/* Feedback Message */}
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

        {/* Trade Levels & Execution Blueprint */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))',
            gap: '14px',
            padding: '18px',
            backgroundColor: 'var(--bg-secondary)',
            borderRadius: '8px',
            marginBottom: '20px',
          }}
        >
          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>RECOMMENDED CONTRACT</div>
            <div style={{ fontWeight: 800, fontSize: '1.15rem', color: 'var(--color-blue)', fontFamily: 'JetBrains Mono', marginTop: '2px' }}>
              {latestSignal?.selectedStrike || 25500} {latestSignal?.selectedOptionType || 'CE'}
            </div>
            <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)', marginTop: '2px' }}>
              {latestSignal?.selectedInstrumentKey || 'NSE_FO|64473'}
            </div>
          </div>

          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>BUY ENTRY PREMIUM</div>
            <div style={{ fontWeight: 800, fontSize: '1.25rem', color: 'var(--text-primary)', fontFamily: 'JetBrains Mono', marginTop: '2px' }}>
              ₹{(latestSignal?.entryPremium || 162.0).toFixed(2)}
            </div>
            <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)', marginTop: '2px' }}>
              Market Order (LTP)
            </div>
          </div>

          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>STOP LOSS PREMIUM</div>
            <div style={{ fontWeight: 800, fontSize: '1.25rem', color: 'var(--color-red)', fontFamily: 'JetBrains Mono', marginTop: '2px' }}>
              ₹{(latestSignal?.stopLossPremium || 146.07).toFixed(2)}
            </div>
            <div style={{ fontSize: '0.7rem', color: 'var(--color-red)', marginTop: '2px' }}>
              Risk: -₹{((latestSignal?.entryPremium || 162.0) - (latestSignal?.stopLossPremium || 146.07)).toFixed(2)} / share
            </div>
          </div>

          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>TARGET 1 (1:2 R:R)</div>
            <div style={{ fontWeight: 800, fontSize: '1.25rem', color: 'var(--color-green)', fontFamily: 'JetBrains Mono', marginTop: '2px' }}>
              ₹{(latestSignal?.target1Premium || 193.8).toFixed(2)}
            </div>
            <div style={{ fontSize: '0.7rem', color: 'var(--color-green)', marginTop: '2px' }}>
              Gain: +₹{((latestSignal?.target1Premium || 193.8) - (latestSignal?.entryPremium || 162.0)).toFixed(2)} / share
            </div>
          </div>

          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>TARGET 2 EXTENDED</div>
            <div style={{ fontWeight: 800, fontSize: '1.25rem', color: 'var(--color-blue)', fontFamily: 'JetBrains Mono', marginTop: '2px' }}>
              ₹{(latestSignal?.target2Premium || 209.67).toFixed(2)}
            </div>
            <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)', marginTop: '2px' }}>
              Gain: +₹{((latestSignal?.target2Premium || 209.67) - (latestSignal?.entryPremium || 162.0)).toFixed(2)} / share
            </div>
          </div>

          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>LOT SIZING (1 LOT = 65)</div>
            <div style={{ fontWeight: 800, fontSize: '1.15rem', color: 'var(--text-primary)', fontFamily: 'JetBrains Mono', marginTop: '2px' }}>
              ₹{(latestSignal?.riskAmount || 1035.45).toFixed(2)}
            </div>
            <div style={{ fontSize: '0.7rem', color: 'var(--color-amber)', marginTop: '2px' }}>
              R:R Ratio = {latestSignal?.rewardToRiskRatio ? `1:${latestSignal.rewardToRiskRatio.toFixed(1)}` : '1:2.0'}
            </div>
          </div>
        </div>

        {/* Scoring Pillars & Rationale */}
        <div>
          <div style={{ fontSize: '0.85rem', fontWeight: 700, marginBottom: '10px', color: 'var(--text-secondary)' }}>
            QUANTITATIVE SIGNAL REASONS & MULTI-FACTOR WEIGHTS
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
            {latestSignal?.reasons && latestSignal.reasons.length > 0 ? (
              latestSignal.reasons.map((r, idx) => (
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
                        backgroundColor: r.category === 'RiskVeto' ? 'rgba(245, 158, 11, 0.2)' : 'rgba(59, 130, 246, 0.2)',
                        color: r.category === 'RiskVeto' ? 'var(--color-amber)' : 'var(--color-blue)',
                        fontSize: '0.75rem',
                        fontWeight: 700,
                      }}
                    >
                      {r.category}
                    </span>
                    <span>{r.description}</span>
                  </div>
                  {r.scoreContribution > 0 && (
                    <span style={{ color: 'var(--color-green)', fontWeight: 700, fontFamily: 'JetBrains Mono' }}>
                      +{r.scoreContribution}
                    </span>
                  )}
                </div>
              ))
            ) : (
              <div style={{ color: 'var(--text-muted)', fontSize: '0.85rem' }}>
                Breakout above Previous Day High 25501. Options flow confirms breakout (PCR 1.48). Strong ADX momentum (28.7).
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Side-by-Side Quick Selection: Recommended CE vs Recommended PE */}
      {optionsReport && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '16px' }}>
          {/* Best CE to Buy Card */}
          {optionsReport.recommendedCe && (
            <div
              className="card"
              style={{
                border: '1px solid rgba(14, 203, 129, 0.4)',
                backgroundColor: 'rgba(14, 203, 129, 0.03)',
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between',
                gap: '14px',
              }}
            >
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--color-green)', fontWeight: 800 }}>
                    <TrendingUp size={18} />
                    BEST CALL OPTION TO BUY (CE)
                  </div>
                  <span
                    style={{
                      padding: '2px 8px',
                      borderRadius: '4px',
                      fontSize: '0.75rem',
                      fontWeight: 700,
                      backgroundColor: 'rgba(14, 203, 129, 0.2)',
                      color: 'var(--color-green)',
                    }}
                  >
                    Score: {optionsReport.recommendedCe.liquidityScore}/100
                  </span>
                </div>

                <div style={{ fontSize: '1.6rem', fontWeight: 800, fontFamily: 'JetBrains Mono', marginTop: '10px' }}>
                  {optionsReport.recommendedCe.strikePrice} CE @ ₹{optionsReport.recommendedCe.ltp.toFixed(2)}
                </div>

                <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginTop: '4px' }}>
                  Delta: <strong>{optionsReport.recommendedCe.delta?.toFixed(2) ?? '0.57'}</strong> | Theta:{' '}
                  <strong>{optionsReport.recommendedCe.theta?.toFixed(1) ?? '-16.2'}</strong> | IV:{' '}
                  <strong>{optionsReport.recommendedCe.impliedVolatility?.toFixed(1) ?? '12.3'}%</strong>
                </div>

                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '8px', lineHeight: 1.4 }}>
                  {optionsReport.recommendedCe.selectionReason}
                </div>
              </div>

              <button
                onClick={() => handleBuySpecificContract(optionsReport.recommendedCe!)}
                disabled={isExecuting}
                style={{
                  width: '100%',
                  padding: '10px 16px',
                  borderRadius: '6px',
                  backgroundColor: 'rgba(14, 203, 129, 0.2)',
                  color: 'var(--color-green)',
                  border: '1px solid var(--color-green)',
                  fontWeight: 700,
                  fontSize: '0.85rem',
                  cursor: 'pointer',
                }}
              >
                Simulate Buy 1 Lot ({optionsReport.recommendedCe.strikePrice} CE)
              </button>
            </div>
          )}

          {/* Best PE to Buy Card */}
          {optionsReport.recommendedPe && (
            <div
              className="card"
              style={{
                border: '1px solid rgba(246, 70, 93, 0.4)',
                backgroundColor: 'rgba(246, 70, 93, 0.03)',
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between',
                gap: '14px',
              }}
            >
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--color-red)', fontWeight: 800 }}>
                    <TrendingDown size={18} />
                    BEST PUT OPTION TO BUY (PE)
                  </div>
                  <span
                    style={{
                      padding: '2px 8px',
                      borderRadius: '4px',
                      fontSize: '0.75rem',
                      fontWeight: 700,
                      backgroundColor: 'rgba(246, 70, 93, 0.2)',
                      color: 'var(--color-red)',
                    }}
                  >
                    Score: {optionsReport.recommendedPe.liquidityScore}/100
                  </span>
                </div>

                <div style={{ fontSize: '1.6rem', fontWeight: 800, fontFamily: 'JetBrains Mono', marginTop: '10px' }}>
                  {optionsReport.recommendedPe.strikePrice} PE @ ₹{optionsReport.recommendedPe.ltp.toFixed(2)}
                </div>

                <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginTop: '4px' }}>
                  Delta: <strong>{optionsReport.recommendedPe.delta?.toFixed(2) ?? '-0.55'}</strong> | Theta:{' '}
                  <strong>{optionsReport.recommendedPe.theta?.toFixed(1) ?? '-16.8'}</strong> | IV:{' '}
                  <strong>{optionsReport.recommendedPe.impliedVolatility?.toFixed(1) ?? '12.8'}%</strong>
                </div>

                <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '8px', lineHeight: 1.4 }}>
                  {optionsReport.recommendedPe.selectionReason}
                </div>
              </div>

              <button
                onClick={() => handleBuySpecificContract(optionsReport.recommendedPe!)}
                disabled={isExecuting}
                style={{
                  width: '100%',
                  padding: '10px 16px',
                  borderRadius: '6px',
                  backgroundColor: 'rgba(246, 70, 93, 0.2)',
                  color: 'var(--color-red)',
                  border: '1px solid var(--color-red)',
                  fontWeight: 700,
                  fontSize: '0.85rem',
                  cursor: 'pointer',
                }}
              >
                Simulate Buy 1 Lot ({optionsReport.recommendedPe.strikePrice} PE)
              </button>
            </div>
          )}
        </div>
      )}

      {/* Historical Signals Ledger */}
      <div className="card" style={{ overflowX: 'auto' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
          <div style={{ fontSize: '0.95rem', fontWeight: 700 }}>RECENT STRATEGY SIGNALS (AUDIT TRAIL)</div>
          <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Chronological quantitative evaluation log</span>
        </div>

        {signalHistory.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '24px', color: 'var(--text-muted)' }}>
            No historical signals recorded yet. Click "Evaluate Strategy Now" above to generate a signal.
          </div>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)' }}>
                <th style={{ padding: '8px' }}>Time</th>
                <th style={{ padding: '8px' }}>Action</th>
                <th style={{ padding: '8px' }}>Contract</th>
                <th style={{ padding: '8px' }}>Entry</th>
                <th style={{ padding: '8px' }}>SL</th>
                <th style={{ padding: '8px' }}>Target 1</th>
                <th style={{ padding: '8px' }}>R:R</th>
                <th style={{ padding: '8px' }}>Strategy</th>
                <th style={{ padding: '8px' }}>Score</th>
              </tr>
            </thead>
            <tbody>
              {signalHistory.map((s) => (
                <tr key={s.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                  <td style={{ padding: '8px', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                    {formatToIST(s.signalTimeUtc, { showDate: true, showSeconds: true })}
                  </td>
                  <td style={{ padding: '8px' }}>
                    <span
                      style={{
                        padding: '2px 8px',
                        borderRadius: '4px',
                        fontSize: '0.75rem',
                        fontWeight: 700,
                        backgroundColor:
                          s.selectedOptionType === 'CE'
                            ? 'rgba(14, 203, 129, 0.2)'
                            : 'rgba(246, 70, 93, 0.2)',
                        color: s.selectedOptionType === 'CE' ? 'var(--color-green)' : 'var(--color-red)',
                      }}
                    >
                      {s.selectedStrike > 0 ? `BUY ${s.selectedStrike} ${s.selectedOptionType}` : s.direction}
                    </span>
                  </td>
                  <td style={{ padding: '8px', fontFamily: 'JetBrains Mono', fontSize: '0.8rem' }}>
                    {s.selectedStrike > 0 ? `${s.selectedStrike} ${s.selectedOptionType}` : '—'}
                  </td>
                  <td style={{ padding: '8px', fontFamily: 'JetBrains Mono' }}>
                    {s.entryPremium > 0 ? `₹${s.entryPremium.toFixed(2)}` : '—'}
                  </td>
                  <td style={{ padding: '8px', fontFamily: 'JetBrains Mono', color: 'var(--color-red)' }}>
                    {s.stopLossPremium > 0 ? `₹${s.stopLossPremium.toFixed(2)}` : '—'}
                  </td>
                  <td style={{ padding: '8px', fontFamily: 'JetBrains Mono', color: 'var(--color-green)' }}>
                    {s.target1Premium > 0 ? `₹${s.target1Premium.toFixed(2)}` : '—'}
                  </td>
                  <td style={{ padding: '8px', fontFamily: 'JetBrains Mono', color: 'var(--color-amber)' }}>
                    {s.rewardToRiskRatio > 0 ? `1:${s.rewardToRiskRatio.toFixed(1)}` : '—'}
                  </td>
                  <td style={{ padding: '8px' }}>{s.strategyFamily}</td>
                  <td style={{ padding: '8px', fontWeight: 700 }}>{s.confidence}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
};
