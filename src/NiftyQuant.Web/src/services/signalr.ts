import * as signalR from '@microsoft/signalr';
import type { OptionsReportDto, StrategySignalDto } from '../types';

type MessageCallback<T = any> = (data: T) => void;

export interface LiveCandleUpdate {
  instrumentKey: string;
  timeframe: string;
  candleTimeUtc: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  isClosed: boolean;
}

class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private isConnecting = false;
  private listeners: Map<string, Set<MessageCallback>> = new Map();

  public async start(): Promise<boolean> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      return true;
    }

    if (this.isConnecting) return false;
    this.isConnecting = true;

    try {
      const hubUrl =
        (import.meta.env.VITE_HUB_URL as string | undefined) ||
        ((import.meta.env.VITE_API_URL as string | undefined)
          ? `${(import.meta.env.VITE_API_URL as string).replace(/\/api\/?$/, '')}/hubs/market`
          : '/hubs/market');

      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
          skipNegotiation: false,
          transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      const events = [
        'ReceiveCandle',
        'ReceiveSnapshot',
        'ReceiveSignal',
        'ReceiveOrderPlaced',
        'ReceiveOrderExecuted',
        'ReceivePositionOpened',
        'ReceivePositionUpdated',
        'ReceiveTradeClosed',
      ];

      events.forEach((evt) => {
        this.connection!.on(evt, (data) => {
          const callbacks = this.listeners.get(evt);
          if (callbacks) {
            callbacks.forEach((cb) => {
              try {
                cb(data);
              } catch (e) {
                console.error(`Error in SignalR callback for ${evt}:`, e);
              }
            });
          }
        });
      });

      await this.connection.start();
      console.log('SignalR connected successfully to MarketHub');
      this.isConnecting = false;
      return true;
    } catch (err) {
      console.warn('SignalR connection failed. Real-time updates will fallback to polling.', err);
      this.isConnecting = false;
      return false;
    }
  }

  public async connect(): Promise<boolean> {
    return this.start();
  }

  public async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
    }
  }

  public subscribe(event: string, callback: MessageCallback): () => void {
    if (!this.listeners.has(event)) {
      this.listeners.set(event, new Set());
    }
    this.listeners.get(event)!.add(callback);

    return () => {
      this.listeners.get(event)?.delete(callback);
    };
  }

  public onCandleUpdated(callback: (candle: LiveCandleUpdate) => void): () => void {
    return this.subscribe('ReceiveCandle', callback);
  }

  public onSignalGenerated(callback: (signal: StrategySignalDto) => void): () => void {
    return this.subscribe('ReceiveSignal', callback);
  }

  public onTradeExecuted(callback: (trade: any) => void): () => void {
    const unsub1 = this.subscribe('ReceivePositionOpened', callback);
    const unsub2 = this.subscribe('ReceiveTradeClosed', callback);
    return () => {
      unsub1();
      unsub2();
    };
  }

  public onOptionChainUpdated(callback: (snapshot: OptionsReportDto) => void): () => void {
    return this.subscribe('ReceiveSnapshot', callback);
  }

  public isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
}

export const signalRClient = new SignalRService();
export const signalRService = signalRClient;
