export type MarketRegimeType =
  | 'Transitional'
  | 'StrongBullish'
  | 'StrongBearish'
  | 'Sideways'
  | 'HighVolatility';

export type SignalDirection =
  | 'Wait'
  | 'NoTrade'
  | 'BuyCe'
  | 'StrongBuyCe'
  | 'BuyPe'
  | 'StrongBuyPe';

export type StrategyFamily = 'None' | 'TrendFollowing' | 'Breakout' | 'Reversal';

export type OptionType = 'CE' | 'PE';

export type OiBuildupType =
  | 'Neutral'
  | 'LongBuildup'
  | 'ShortBuildup'
  | 'ShortCovering'
  | 'LongUnwinding';

export interface MarketOverview {
  niftySpot: {
    lastPrice: number;
    change: number;
    changePercent: number;
    open: number;
    high: number;
    low: number;
    close: number;
    timestamp?: string;
  };
  indiaVix?: {
    lastPrice: number;
    change: number;
    changePercent: number;
  };
  timestampUtc: string;
}

export interface OptionQuoteDto {
  instrumentKey: string;
  strikePrice: number;
  optionType: OptionType;
  expiryDate: string;
  ltp: number;
  bidPrice: number;
  askPrice: number;
  volume: number;
  openInterest: number;
  previousOpenInterest: number;
  intradayOiChange: number;
  intradayOiChangePercent: number;
  impliedVolatility?: number;
  delta?: number;
  gamma?: number;
  theta?: number;
  vega?: number;
}

export interface StrikeAnalysisDto {
  strikePrice: number;
  isAtm: boolean;
  isMajorCallResistance: boolean;
  isMajorPutSupport: boolean;
  callQuote?: OptionQuoteDto;
  putQuote?: OptionQuoteDto;
  callBuildup: OiBuildupType;
  putBuildup: OiBuildupType;
  callOi: number;
  putOi: number;
  callOiChange: number;
  putOiChange: number;
  netOiChange: number;
  callDelta?: number;
  putDelta?: number;
  callIv?: number;
  putIv?: number;
  callVolume: number;
  putVolume: number;
  callBidAskSpreadPercent: number;
  putBidAskSpreadPercent: number;
}

export interface PcrAnalysisDto {
  pcrOi: number;
  pcrVolume: number;
  pcrTrend: string;
  sentimentZone: string;
}

export interface KeyLevelsDto {
  spotPrice: number;
  maxPainStrike: number;
  majorCallResistance: number;
  majorPutSupport: number;
  expectedTradingRangeLow: number;
  expectedTradingRangeHigh: number;
}

export interface SelectedOptionContractDto {
  instrumentKey: string;
  optionType: OptionType;
  strikePrice: number;
  expiryDate: string;
  ltp: number;
  delta?: number;
  theta?: number;
  impliedVolatility?: number;
  liquidityScore: number;
  isRecommended: boolean;
  selectionReason: string;
}

export interface OptionsReportDto {
  underlyingKey: string;
  underlyingPrice: number;
  expiryDate: string;
  totalStrikesAnalyzed: number;
  pcrAnalysis: PcrAnalysisDto;
  keyLevels: KeyLevelsDto;
  optionsSentimentScore: number;
  sentimentSummary: string;
  recommendedCe?: SelectedOptionContractDto;
  recommendedPe?: SelectedOptionContractDto;
  strikes: StrikeAnalysisDto[];
}

export interface IndicatorSnapshotDto {
  instrumentKey: string;
  timeframe: string;
  candleTimeUtc: string;
  ema9?: number;
  ema21?: number;
  ema50?: number;
  ema200?: number;
  vwap?: number;
  supertrend?: number;
  supertrendIsBullish?: boolean;
  rsi14?: number;
  macd?: number;
  macdSignal?: number;
  macdHistogram?: number;
  adx14?: number;
  plusDi?: number;
  minusDi?: number;
  atr14?: number;
  bollingerUpper?: number;
  bollingerMiddle?: number;
  bollingerLower?: number;
  relativeVolume?: number;
}

export interface CprResultDto {
  pivot: number;
  bottomCentralPivot: number;
  topCentralPivot: number;
  cprWidthPercent: number;
  isNarrowCpr: boolean;
  r1: number;
  r2: number;
  r3: number;
  s1: number;
  s2: number;
  s3: number;
}

export interface MultiTimeframeStateDto {
  instrumentKey: string;
  timeframe1m?: IndicatorSnapshotDto;
  timeframe5m?: IndicatorSnapshotDto;
  timeframe15m?: IndicatorSnapshotDto;
  dailyCpr?: CprResultDto;
  isTrendAlignedBullish: boolean;
  isTrendAlignedBearish: boolean;
}

export interface SignalReasonDto {
  category: string;
  scoreContribution: number;
  description: string;
}

export interface StrategySignalDto {
  id: string;
  signalTimeUtc: string;
  underlyingKey: string;
  underlyingPrice: number;
  direction: SignalDirection;
  strategyFamily: StrategyFamily;
  marketRegime: MarketRegimeType;
  bullishScore: number;
  bearishScore: number;
  confidence: number;
  selectedInstrumentKey: string;
  selectedStrike: number;
  selectedOptionType: OptionType;
  selectedExpiry: string;
  entryPremium: number;
  stopLossPremium: number;
  target1Premium: number;
  target2Premium: number;
  recommendedQuantity: number;
  lotSize: number;
  riskAmount: number;
  rewardToRiskRatio: number;
  isExecuted: boolean;
  reasons: SignalReasonDto[];
}

export interface MarketRegimeResultDto {
  regime: MarketRegimeType;
  confidence: number;
  description: string;
  adxValue: number;
  indiaVix: number;
  trendAllowedBullish: boolean;
  trendAllowedBearish: boolean;
  reversalAllowed: boolean;
}

export interface DailyRiskStatusDto {
  date: string;
  accountName: string;
  initialCapital: number;
  availableBalance: number;
  utilizedMargin: number;
  realizedPnLToday: number;
  unrealizedPnLToday: number;
  totalPnLToday: number;
  dailyLossLimit: number;
  dailyLossRemaining: number;
  tradesCountToday: number;
  maxDailyTradesLimit: number;
  consecutiveLossesToday: number;
  maxConsecutiveLossesLimit: number;
  activePositionsCount: number;
  maxConcurrentPositionsLimit: number;
  isCircuitBreakerTripped: boolean;
  circuitBreakerReason: string;
  canOpenNewPositions: boolean;
}

export interface PaperAccountDto {
  id: number;
  accountName: string;
  initialCapital: number;
  availableBalance: number;
  utilizedMargin: number;
  realizedPnL: number;
  unrealizedPnL: number;
  dailyLossLimit: number;
  maxRiskPerTrade: number;
  isTradingLockedForDay: boolean;
}

export interface PaperPositionDto {
  id: string;
  instrumentKey: string;
  tradingSymbol?: string;
  optionType: OptionType;
  strikePrice: number;
  expiryDate: string;
  quantity: number;
  averageBuyPrice: number;
  currentLtp: number;
  stopLossPrice: number;
  targetPrice: number;
  trailingStopLossPrice?: number;
  maxLtpReached?: number;
  unrealizedPnL: number;
  isOpen: boolean;
  openedAtUtc: string;
}

export interface PaperTradeDto {
  id: string;
  instrumentKey: string;
  tradingSymbol?: string;
  optionType?: OptionType;
  strikePrice?: number;
  expiryDate?: string;
  quantity: number;
  entryPrice: number;
  exitPrice: number;
  slippagePaid: number;
  totalCharges: number;
  grossPnL: number;
  netPnL: number;
  returnPercent?: number;
  durationMinutes?: number;
  entryTimeUtc: string;
  exitTimeUtc: string;
  exitReason: string;
}

export interface PaperOrderDto {
  id: string;
  instrumentKey: string;
  tradingSymbol?: string;
  side: number | string;
  optionType?: OptionType;
  strikePrice?: number;
  quantity: number;
  orderedPrice: number;
  executedPrice: number;
  slippage: number;
  estimatedCharges: number;
  status: number | string;
  rejectReason?: string;
  orderTimeUtc: string;
}

export interface AutonomousTradingStatusDto {
  isEnabled: boolean;
  executionMode: string;
  minConfidenceThreshold: number;
  isMarketHoursOnly: boolean;
  lastExecutionTime?: string;
  totalOrdersPunchedToday: number;
  totalOrdersExitedToday: number;
  activePositionsCount: number;
  statusMessage: string;
}

export interface PaperPortfolioSummaryDto {
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  winRatePercent: number;
  totalGrossPnL: number;
  totalChargesPaid: number;
  totalNetPnL: number;
  profitFactor: number;
  averageProfitPerTrade: number;
  averageLossPerTrade: number;
  expectancy: number;
  maxDrawdownAmount: number;
  maxDrawdownPercent: number;
  currentCapital: number;
  availableBalance: number;
  activeOpenPositionsCount: number;
  activeUnrealizedPnL: number;
}

export interface BacktestRequestDto {
  underlyingKey: string;
  fromDate: string;
  toDate: string;
  initialCapital: number;
  riskPerTradePercent: number;
  maxDailyLossPercent: number;
  maxDailyTrades: number;
  maxConsecutiveLosses: number;
  minRewardRiskRatio: number;
  simulateSlippage: boolean;
  slippagePercent: number;
  enableTrailingStop: boolean;
  targetStrategy?: number;
}

export interface BacktestTradeDto {
  tradeNumber: number;
  entryTimeUtc: string;
  exitTimeUtc: string;
  instrumentKey: string;
  direction: SignalDirection;
  strategyFamily: StrategyFamily;
  marketRegime: MarketRegimeType;
  quantity: number;
  entryPrice: number;
  exitPrice: number;
  stopLossPrice: number;
  targetPrice: number;
  grossPnL: number;
  totalCharges: number;
  netPnL: number;
  returnPercent: number;
  exitReason: string;
  runningCapital: number;
}

export interface EquityPointDto {
  timestampUtc: string;
  capital: number;
  drawdownAmount: number;
  drawdownPercent: number;
  tradeNumber: number;
}

export interface BacktestResultDto {
  runId: string;
  underlyingKey: string;
  fromDate: string;
  toDate: string;
  totalTradingDays: number;
  initialCapital: number;
  finalCapital: number;
  totalGrossPnL: number;
  totalCharges: number;
  totalNetPnL: number;
  totalReturnPercent: number;
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  winRatePercent: number;
  profitFactor: number;
  averageTradePnL: number;
  averageWin: number;
  averageLoss: number;
  winLossRatio: number;
  mathematicalExpectancy: number;
  maxDrawdownAmount: number;
  maxDrawdownPercent: number;
  sharpeRatio: number;
  sortinoRatio: number;
  dailyLossLimitBreaches: number;
  trades: BacktestTradeDto[];
  equityCurve: EquityPointDto[];
}
