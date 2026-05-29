<!-- This document describes the full project: the backend engine, the Angular dashboard, and the Docker deployment setup. -->
# HFT Crypto Arbitrage Engine & Paper Trading Simulator

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Angular](https://img.shields.io/badge/Angular-17-DD0031?logo=angular&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker&logoColor=white)

A high-frequency trading (HFT) cryptocurrency arbitrage scanner with a built-in event-driven paper trading execution engine. Developed as a Bachelor's Degree thesis project for the **121 Software Engineering** program (State University "Kyiv Aviation Institute").

## 🚀 Architecture & Features

This system consists of a `.NET 8` microservice back-end and an `Angular` real-time dashboard, entirely containerized via Docker.

* **Real-Time Data Ingestion:** Establishes resilient WebSocket connections to major exchanges (Binance, Bybit, OKX, Bitget, Gate.io) to stream Level 2 Order Book data.
* **Concurrent Event-Driven Radar:** Uses `System.Threading.Channels` for lock-free, high-throughput spread calculation across multiple exchanges in milliseconds.
* **Paper Trading Simulator:** A fully integrated simulation module that accounts for network latency (ping simulation), market slippage, and taker fees to validate HFT strategies without financial risk.
* **Dynamic Control Panel:** Modify trading volumes, minimum profit thresholds, and taker fees on the fly without restarting the engine.
* **Live Dashboard:** Real-time UI connected via `SignalR` displaying live spreads, theoretical session profits, and virtual wallet balances.

## 🛠 Tech Stack

* **Backend:** C#, .NET 8, Entity Framework Core, SignalR, xUnit, Moq
* **Frontend:** TypeScript, Angular, Chart.js
* **Infrastructure:** Docker, Docker Compose, Nginx, PostgreSQL, Redis

## ⚙️ Quick Start (Docker)

To deploy the entire system locally or on a VPS:

```bash
# Clone the repository
git clone https://github.com/Shostakdev/CryptoArbitrage.git
cd cryptoarbitrage/infrastructure

# Build and start all containers in detached mode
docker compose up -d --build
