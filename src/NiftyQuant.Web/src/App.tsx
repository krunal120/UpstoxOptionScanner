import React, { useCallback, useEffect, useState } from 'react';
import {
  Activity,
  BarChart2,
  Calendar,
  Layers,
  ListFilter,
  ShieldAlert,
  Wallet,
  Zap,
} from 'lucide-react';
import { BacktestingTab } from './components/BacktestingTab';
import { Header } from './components/Header';
import { IndicatorsTab } from './components/IndicatorsTab';
import { OptionChainTab } from './components/OptionChainTab';
import { OverviewTab } from './components/OverviewTab';
import { PaperTradingTab } from './components/PaperTradingTab';
import { RiskTab } from './components/RiskTab';
import { api } from './services/api';
import { signalRClient } from './services/signalr';
import type {
  DailyRiskStatusDto,
  MarketOverview,
  MarketRegimeResultDto,
  MultiTimeframeStateDto,
  OptionsReportDto,
  PaperAccountDto,
  PaperOrderDto,
  PaperPortfolioSummaryDto,
  PaperPositionDto,
  PaperTradeDto,
  StrategySignalDto,
} from './types';

export const App: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'overview' | 'chain' | 'indicators' | 'papertrading' | 'backtest' | 'risk'>(
    'overview'
  );

  // Core state
  const [overview, setOverview] = useState<MarketOverview | null>(null);
  const [regime, setRegime] = useState<MarketRegimeResultDto | null>(null);
  const [latestSignal, setLatestSignal] = useState<StrategySignalDto | null>(null);
  const [signalHistory, setSignalHistory] = useState<StrategySignalDto[]>([]);
  const [optionsReport, setOptionsReport] = useState<OptionsReportDto | null>(null);
  const [mtfState, setMtfState] = useState<MultiTimeframeStateDto | null>(null);
  const [riskStatus, setRiskStatus] = useState<DailyRiskStatusDto | null>(null);
  const [account, setAccount] = useState<PaperAccountDto | null>(null);
  const [portfolio, setPortfolio] = useState<PaperPortfolioSummaryDto | null>(null);
  const [openPositions, setOpenPositions] = useState<PaperPositionDto[]>([]);
  const [trades, setTrades] = useState<PaperTradeDto[]>([]);
  const [orders, setOrders] = useState<PaperOrderDto[]>([]);
  const [isSignalRConnected, setIsSignalRConnected] = useState<boolean>(false);

  // Central refresh dispatcher
  const refreshAllData = useCallback(async () => {
    try {
      const [ov, reg, sig, hist, rpt, mtf, rsk, acc, port, pos, trd, ord] = await Promise.allSettled([
        api.getMarketOverview(),
        api.getMarketRegime(),
        api.getLatestSignal(),
        api.getSignalHistory('NSE_INDEX|Nifty 50', 20),
        api.getOptionsIntelligence(),
        api.getMultiTimeframeState(),
        api.getDailyRiskStatus(),
        api.getPaperAccount(),
        api.getPortfolioSummary(),
        api.getOpenPositions(),
        api.getTradeHistory(30),
        api.getOrderHistory(30),
      ]);

      if (ov.status === 'fulfilled') setOverview(ov.value);
      if (reg.status === 'fulfilled') setRegime(reg.value);
      if (sig.status === 'fulfilled') setLatestSignal(sig.value);
      if (hist.status === 'fulfilled') setSignalHistory(hist.value);
      if (rpt.status === 'fulfilled') setOptionsReport(rpt.value);
      if (mtf.status === 'fulfilled') setMtfState(mtf.value);
      if (rsk.status === 'fulfilled') setRiskStatus(rsk.value);
      if (acc.status === 'fulfilled') setAccount(acc.value);
      if (port.status === 'fulfilled') setPortfolio(port.value);
      if (pos.status === 'fulfilled') setOpenPositions(pos.value);
      if (trd.status === 'fulfilled') setTrades(trd.value);
      if (ord.status === 'fulfilled') setOrders(ord.value);
    } catch (err) {
      console.warn('Refresh data failed:', err);
    }
  }, []);

  // Initial load and SignalR event bindings
  useEffect(() => {
    refreshAllData();

    // Connect SignalR
    signalRClient
      .start()
      .then(() => setIsSignalRConnected(true))
      .catch((err) => {
        console.warn('SignalR initial connection failed, will retry in background:', err);
        setIsSignalRConnected(false);
      });

    // Listen for live candle updates
    const unsubCandle = signalRClient.onCandleUpdated((candle) => {
      setOverview((prev) => {
        if (!prev) {
          return {
            niftySpot: {
              lastPrice: candle.close,
              open: candle.open,
              high: candle.high,
              low: candle.low,
              close: candle.close,
              change: 0,
              changePercent: 0,
            },
            timestampUtc: candle.candleTimeUtc,
          };
        }
        const change = candle.close - (prev.niftySpot.close - prev.niftySpot.change);
        const changePercent = prev.niftySpot.close !== 0 ? (change / prev.niftySpot.close) * 100 : 0;
        return {
          ...prev,
          niftySpot: {
            ...prev.niftySpot,
            lastPrice: candle.close,
            high: Math.max(prev.niftySpot.high, candle.high),
            low: Math.min(prev.niftySpot.low, candle.low),
            close: candle.close,
            change,
            changePercent,
          },
          timestampUtc: candle.candleTimeUtc,
        };
      });
    });

    // Listen for live strategy signals
    const unsubSignal = signalRClient.onSignalGenerated((signal) => {
      setLatestSignal(signal);
      setSignalHistory((prev) => [signal, ...prev.slice(0, 29)]);
    });

    // Listen for paper trade executions
    const unsubTrade = signalRClient.onTradeExecuted((trade) => {
      setTrades((prev) => [trade, ...prev]);
      // Refetch positions and portfolio
      api.getOpenPositions().then(setOpenPositions).catch(() => {});
      api.getPortfolioSummary().then(setPortfolio).catch(() => {});
      api.getPaperAccount().then(setAccount).catch(() => {});
    });

    // Listen for option chain updates
    const unsubChain = signalRClient.onOptionChainUpdated((report) => {
      setOptionsReport(report);
    });

    // Fallback polling interval every 5 seconds for robust synchronization
    const pollInterval = setInterval(() => {
      refreshAllData();
      setIsSignalRConnected(signalRClient.isConnected());
    }, 5000);

    return () => {
      unsubCandle();
      unsubSignal();
      unsubTrade();
      unsubChain();
      clearInterval(pollInterval);
      signalRClient.stop();
    };
  }, [refreshAllData]);

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', backgroundColor: 'var(--bg-primary)' }}>
      {/* Institutional Top Header */}
      <Header
        overview={overview}
        regime={regime}
        isSignalRConnected={isSignalRConnected}
        onRefresh={refreshAllData}
      />

      {/* Navigation Tab Bar */}
      <nav
        style={{
          backgroundColor: 'var(--bg-secondary)',
          borderBottom: '1px solid var(--border-color)',
          padding: '0 24px',
          display: 'flex',
          gap: '8px',
          overflowX: 'auto',
        }}
      >
        <button
          onClick={() => setActiveTab('overview')}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            padding: '12px 16px',
            border: 'none',
            borderBottom: activeTab === 'overview' ? '3px solid var(--color-blue)' : '3px solid transparent',
            backgroundColor: 'transparent',
            color: activeTab === 'overview' ? 'var(--color-blue)' : 'var(--text-secondary)',
            fontWeight: 700,
            fontSize: '0.85rem',
            cursor: 'pointer',
            whiteSpace: 'nowrap',
          }}
        >
          <Zap size={16} /> Overview & Live Signals
        </button>

        <button
          onClick={() => setActiveTab('chain')}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            padding: '12px 16px',
            border: 'none',
            borderBottom: activeTab === 'chain' ? '3px solid var(--color-blue)' : '3px solid transparent',
            backgroundColor: 'transparent',
            color: activeTab === 'chain' ? 'var(--color-blue)' : 'var(--text-secondary)',
            fontWeight: 700,
            fontSize: '0.85rem',
            cursor: 'pointer',
            whiteSpace: 'nowrap',
          }}
        >
          <ListFilter size={16} /> Option Chain & OI Buildup
        </button>

        <button
          onClick={() => setActiveTab('indicators')}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            padding: '12px 16px',
            border: 'none',
            borderBottom: activeTab === 'indicators' ? '3px solid var(--color-blue)' : '3px solid transparent',
            backgroundColor: 'transparent',
            color: activeTab === 'indicators' ? 'var(--color-blue)' : 'var(--text-secondary)',
            fontWeight: 700,
            fontSize: '0.85rem',
            cursor: 'pointer',
            whiteSpace: 'nowrap',
          }}
        >
          <BarChart2 size={16} /> Multi-Timeframe Indicators
        </button>

        <button
          onClick={() => setActiveTab('papertrading')}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            padding: '12px 16px',
            border: 'none',
            borderBottom: activeTab === 'papertrading' ? '3px solid var(--color-blue)' : '3px solid transparent',
            backgroundColor: 'transparent',
            color: activeTab === 'papertrading' ? 'var(--color-blue)' : 'var(--text-secondary)',
            fontWeight: 700,
            fontSize: '0.85rem',
            cursor: 'pointer',
            whiteSpace: 'nowrap',
          }}
        >
          <Wallet size={16} /> Paper Trading ({openPositions.length})
        </button>

        <button
          onClick={() => setActiveTab('backtest')}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            padding: '12px 16px',
            border: 'none',
            borderBottom: activeTab === 'backtest' ? '3px solid var(--color-blue)' : '3px solid transparent',
            backgroundColor: 'transparent',
            color: activeTab === 'backtest' ? 'var(--color-blue)' : 'var(--text-secondary)',
            fontWeight: 700,
            fontSize: '0.85rem',
            cursor: 'pointer',
            whiteSpace: 'nowrap',
          }}
        >
          <Calendar size={16} /> Backtesting Simulator
        </button>

        <button
          onClick={() => setActiveTab('risk')}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            padding: '12px 16px',
            border: 'none',
            borderBottom: activeTab === 'risk' ? '3px solid var(--color-blue)' : '3px solid transparent',
            backgroundColor: 'transparent',
            color: activeTab === 'risk' ? 'var(--color-blue)' : 'var(--text-secondary)',
            fontWeight: 700,
            fontSize: '0.85rem',
            cursor: 'pointer',
            whiteSpace: 'nowrap',
          }}
        >
          <ShieldAlert size={16} /> Risk Safeguards
        </button>
      </nav>

      {/* Main Content Viewport */}
      <main style={{ flex: 1, padding: '24px', maxWidth: '1600px', margin: '0 auto', width: '100%' }}>
        {activeTab === 'overview' && (
          <OverviewTab
            latestSignal={latestSignal}
            signalHistory={signalHistory}
            regime={regime}
            riskStatus={riskStatus}
            portfolio={portfolio}
            onRefresh={refreshAllData}
          />
        )}

        {activeTab === 'chain' && (
          <OptionChainTab optionsReport={optionsReport} onRefresh={refreshAllData} />
        )}

        {activeTab === 'indicators' && (
          <IndicatorsTab mtfState={mtfState} onRefresh={refreshAllData} />
        )}

        {activeTab === 'papertrading' && (
          <PaperTradingTab
            account={account}
            portfolio={portfolio}
            openPositions={openPositions}
            trades={trades}
            orders={orders}
            onRefresh={refreshAllData}
          />
        )}

        {activeTab === 'backtest' && <BacktestingTab />}

        {activeTab === 'risk' && (
          <RiskTab riskStatus={riskStatus} account={account} onRefresh={refreshAllData} />
        )}
      </main>
    </div>
  );
};

export default App;
