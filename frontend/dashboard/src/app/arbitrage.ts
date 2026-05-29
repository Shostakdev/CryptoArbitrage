import { Injectable, NgZone, isDevMode } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable, Subject } from 'rxjs';

// This service talks to the API and SignalR hub, then exposes live
// events and wallet updates to the Angular dashboard.
export interface ArbitrageEvent {
  id?: string;
  symbol: string;
  buyExchange: string;
  sellExchange: string;
  avgBuyPrice: number;
  avgSellPrice: number;
  tradeVolumeUsdt: number;
  netProfit: number;
  netProfitPercent: number;
  createdAtUtc: string;
  executionStatus?: string;
}

export interface CoreSettings {
  tradeAmountUsdt: number;
  minNetProfitUsdt: number;
  minNetProfitPercent: number;
  takerFeeRate: number;
}

@Injectable({
  providedIn: 'root'
})
export class ArbitrageService {
  private baseUrl = isDevMode() ? 'http://localhost:5124' : '';
  
  private apiUrl = `${this.baseUrl}/api/History`;
  private walletUrl = `${this.baseUrl}/api/Wallet`;
  private settingsUrl = `${this.baseUrl}/api/Settings`;
  private hubUrl = `${this.baseUrl}/hubs/arbitrage`;

  private hubConnection: signalR.HubConnection;
  
  private liveEventsSubject = new BehaviorSubject<ArbitrageEvent[]>([]);
  public liveEvents$ = this.liveEventsSubject.asObservable();

  private connectionStatusSubject = new BehaviorSubject<boolean>(false);
  public connectionStatus$ = this.connectionStatusSubject.asObservable();

  private walletBalanceSubject = new BehaviorSubject<number>(1000);
  public walletBalance$ = this.walletBalanceSubject.asObservable();

  private executionUpdatesSubject = new Subject<{id: string, status: string}>();
  public executionUpdates$ = this.executionUpdatesSubject.asObservable();

  constructor(private http: HttpClient, private zone: NgZone) {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect()
      .build();
  }

  getHistory(): Observable<ArbitrageEvent[]> {
    return this.http.get<ArbitrageEvent[]>(this.apiUrl);
  }

  fetchInitialWallet() {
    this.http.get<{balance: number}>(this.walletUrl).subscribe({
      next: (res) => this.walletBalanceSubject.next(res.balance),
      error: (err) => console.error('Failed to fetch wallet:', err)
    });
  }

  getSettings(): Observable<CoreSettings> {
    return this.http.get<CoreSettings>(this.settingsUrl);
  }

  updateSettings(settings: CoreSettings): Observable<any> {
    return this.http.post(this.settingsUrl, settings);
  }

  setInitialHistory(history: ArbitrageEvent[]) {
    this.liveEventsSubject.next(history);
  }

  startSignalRConnection() {
    this.hubConnection.start()
      .then(() => this.zone.run(() => this.connectionStatusSubject.next(true)))
      .catch(err => console.error('Error starting SignalR: ', err));

    this.hubConnection.onclose(() => this.zone.run(() => this.connectionStatusSubject.next(false)));
    this.hubConnection.onreconnecting(() => this.zone.run(() => this.connectionStatusSubject.next(false)));
    this.hubConnection.onreconnected(() => this.zone.run(() => this.connectionStatusSubject.next(true)));

    this.hubConnection.on('ReceiveOpportunity', (event: ArbitrageEvent) => {
      this.zone.run(() => {
        event.executionStatus = 'Pending';
        const currentEvents = this.liveEventsSubject.value;
        this.liveEventsSubject.next([event, ...currentEvents].slice(0, 100));
      });
    });

    this.hubConnection.on('WalletUpdated', (balance: number) => {
      this.zone.run(() => this.walletBalanceSubject.next(balance));
    });

    this.hubConnection.on('ExecutionUpdated', (update: {id: string, status: string}) => {
      this.zone.run(() => this.executionUpdatesSubject.next(update));
    });
  }
}