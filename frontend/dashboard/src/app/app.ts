import { Component, OnInit, ChangeDetectorRef, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule, DecimalPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ArbitrageService, ArbitrageEvent, CoreSettings } from './arbitrage';
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

// This component renders the dashboard, loads history, and keeps the
// SignalR stream connected while updating the chart and metrics.
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, DecimalPipe, DatePipe, FormsModule],
  templateUrl: './app.html',
  styleUrls: ['./app.css']
})
export class App implements OnInit, AfterViewInit {
  events: ArbitrageEvent[] = [];
  chart: Chart | null = null;
  totalProfitUsdt: number = 0;
  isConnected: boolean = false;
  walletBalance: number = 0;
  
  showSettings: boolean = false;
  settings: CoreSettings = {
    tradeAmountUsdt: 100,
    minNetProfitUsdt: 0.1,
    minNetProfitPercent: 0.05,
    takerFeeRate: 0.001
  };

  @ViewChild('spreadChart') spreadChartRef!: ElementRef;

  constructor(
    private arbitrageService: ArbitrageService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.arbitrageService.fetchInitialWallet();

    this.arbitrageService.walletBalance$.subscribe(balance => {
      this.walletBalance = balance;
      this.cdr.detectChanges();
    });

    this.arbitrageService.getSettings().subscribe({
      next: (data) => {
        this.settings = data;
        this.cdr.detectChanges();
      },
      error: (err) => console.error('Failed to load settings:', err)
    });

    this.arbitrageService.getHistory().subscribe({
      next: (history) => {
        if (history && history.length > 0) {
          const normalized = history.map(e => this.normalizeEvent(e));
          this.events = normalized;
          this.arbitrageService.setInitialHistory(normalized);
          this.calculateMetrics();
          this.updateChart();
          this.cdr.detectChanges();
        }
      }
    });

    this.arbitrageService.liveEvents$.subscribe(liveData => {
      if (liveData && liveData.length > 0) {
        this.events = liveData.map(e => this.normalizeEvent(e));
        this.calculateMetrics();
        this.updateChart();
        this.cdr.detectChanges();
      }
    });

    this.arbitrageService.executionUpdates$.subscribe(update => {
      const ev = this.events.find(e => e.id === update.id);
      if (ev) {
        ev.executionStatus = update.status;
        this.cdr.detectChanges();
      }
    });

    this.arbitrageService.startSignalRConnection();

    this.arbitrageService.connectionStatus$.subscribe(status => {
      this.isConnected = status;
      this.cdr.detectChanges();
    });
  }

  ngAfterViewInit(): void {
    this.initChart();
  }

  toggleSettings() {
    this.showSettings = !this.showSettings;
  }

  saveSettings() {
    const payload: CoreSettings = {
      tradeAmountUsdt: Number(this.settings.tradeAmountUsdt),
      minNetProfitUsdt: Number(this.settings.minNetProfitUsdt),
      minNetProfitPercent: Number(this.settings.minNetProfitPercent),
      takerFeeRate: Number(this.settings.takerFeeRate)
    };

    this.arbitrageService.updateSettings(payload).subscribe({
      next: () => {
        alert('HFT Engine settings updated dynamically!');
        this.showSettings = false;
      },
      error: (err) => {
        console.error('API Error:', err);
        alert('Error updating settings. Check values or console.');
      }
    });
  }

  private calculateMetrics() {
    this.totalProfitUsdt = this.events.reduce((sum, e) => sum + e.netProfit, 0);
  }

  private initChart() {
    const ctx = this.spreadChartRef.nativeElement.getContext('2d');
    this.chart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: [],
        datasets: [{
          label: 'Net Profit (%)',
          data: [],
          borderColor: '#00ff88',
          backgroundColor: 'rgba(0, 255, 136, 0.1)',
          borderWidth: 2,
          pointRadius: 3,
          tension: 0.3,
          fill: true
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 0 },
        scales: {
          x: { ticks: { color: '#8892b0' }, grid: { color: '#23304a' } },
          y: { ticks: { color: '#8892b0' }, grid: { color: '#23304a' } }
        },
        plugins: { legend: { labels: { color: '#ccd6f6' } } }
      }
    });
  }

  private updateChart() {
    if (!this.chart || this.events.length === 0) return;
    const recentEvents = this.events.slice(0, 30).reverse();
    
    this.chart.data.labels = recentEvents.map(e => {
      const d = new Date(e.createdAtUtc);
      return `${d.getHours()}:${d.getMinutes()}:${d.getSeconds()}`;
    });
    this.chart.data.datasets[0].data = recentEvents.map(e => e.netProfitPercent);
    this.chart.update();
  }

  private normalizeEvent(raw: any): ArbitrageEvent {
    return {
      id: raw.id || raw.Id,
      symbol: raw.symbol || raw.Symbol,
      buyExchange: raw.buyExchange || raw.BuyExchange,
      sellExchange: raw.sellExchange || raw.SellExchange,
      avgBuyPrice: raw.avgBuyPrice || raw.AvgBuyPrice || raw.buyPrice || raw.BuyPrice,
      avgSellPrice: raw.avgSellPrice || raw.AvgSellPrice || raw.sellPrice || raw.SellPrice,
      tradeVolumeUsdt: raw.tradeVolumeUsdt || raw.TradeVolumeUsdt || raw.tradeAmountUsdt || raw.TradeAmountUsdt,
      netProfit: raw.netProfit || raw.NetProfit,
      netProfitPercent: raw.netProfitPercent || raw.NetProfitPercent,
      createdAtUtc: raw.createdAtUtc || raw.CreatedAtUtc || raw.timestamp || raw.Timestamp,
      executionStatus: raw.executionStatus || raw.ExecutionStatus || 'Archived'
    };
  }
}