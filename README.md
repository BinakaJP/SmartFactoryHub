# SmartFactory Hub

A cloud-native **industrial IoT monitoring platform** built with .NET 8 microservices. Real-time equipment telemetry flows through a RabbitMQ event bus, is persisted in SQL Server, and is visualised in Grafana dashboards powered by Prometheus metrics. An embedded analytics engine performs continuous anomaly detection and predictive maintenance estimation using Z-Score, EWMA, and Rate-of-Change algorithms.

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────────┐
│                      API Gateway (YARP)  :5100                           │
│  /api/equipment  /api/metrics  /api/alerts  /api/notifications           │
│  /api/analytics  /api/auth  /api/users  /hubs (SignalR)                  │
└──────┬──────────┬──────────┬──────────┬──────────┬──────────┬────────────┘
       │          │          │          │          │          │
  Equipment   Metrics    Alert     Notification  Identity  Analytics
    API         API       API         API          API       API
   :5001       :5002     :5003       :5004        :5005     :5006
       │          │          │          │                      │
       └──────────┴──────────┴──────────┴──────────────────────┘
                                  │
                         RabbitMQ (topic exchange)
                         smartfactory_events
                                  │
          ┌───────────────────────┼───────────────────────┐
          │                       │                       │
   SQL Server (per-service DB)  Simulator           Analytics
   SmartFactory_Equipment        (pushes batch       Engine
   SmartFactory_Metrics           metrics every   (in-memory
   SmartFactory_Alerts            10 s)           rolling windows,
   SmartFactory_Notifications                     Z-Score / EWMA /
   SmartFactory_Identity                          RateOfChange)
   SmartFactory_Analytics
                  │
       Prometheus  ←  /metrics (all services)
                  │
              Grafana  :3000
```

### Services

| Service | Port | Responsibility |
|---|---|---|
| **API Gateway** | 5100 | YARP reverse proxy — single entry point for all clients |
| **Equipment.API** | 5001 | Equipment registry, status tracking, `EquipmentStatusChangedEvent` |
| **Metrics.API** | 5002 | Metric ingestion & query, Prometheus gauges, threshold detection |
| **Alert.API** | 5003 | Alert lifecycle (Open → Acknowledged → Resolved), `AlertTriggeredEvent` |
| **Notification.API** | 5004 | SignalR real-time push to browser clients |
| **Identity.API** | 5005 | JWT authentication, RBAC (Admin / Engineer / Operator / Viewer) |
| **Analytics.API** | 5006 | Anomaly detection (Z-Score + EWMA + Rate-of-Change), health scoring, predictive maintenance RUL |
| **Simulator** | — | Generates realistic equipment telemetry every 10 s |

### Infrastructure

| Service | Port | Purpose |
|---|---|---|
| SQL Server 2022 | 1434 | Persistent storage (one database per microservice) |
| RabbitMQ | 5672 / 15672 | Async event bus (AMQP + Management UI) |
| Prometheus | 9090 | Metrics scraping (15 s interval) |
| Grafana | 3000 | Dashboards — Service Health & Equipment Metrics |

---

## Quick Start

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) ≥ 4.x
- Git

### 1 — Clone & configure secrets

```bash
git clone https://github.com/BinakaJP/SmartFactoryHub.git
cd SmartFactoryHub

# Copy the template and fill in your own passwords
cp .env.example .env
# Edit .env — set SA_PASSWORD, JWT_SECRET_KEY, and optionally RABBITMQ_PASS / GRAFANA_ADMIN_PASSWORD
```

> **Never commit `.env`** — it is listed in `.gitignore`.

### 2 — Start the full stack

```bash
docker compose up --build -d
```

All 12 containers start in dependency order. SQL Server takes ~60 s on first run.

### 3 — Verify everything is healthy

```bash
curl http://localhost:5001/health   # Equipment.API    → Healthy
curl http://localhost:5002/health   # Metrics.API      → Healthy
curl http://localhost:5003/health   # Alert.API        → Healthy
curl http://localhost:5004/health   # Notification.API → Healthy
curl http://localhost:5005/health   # Identity.API     → Healthy
curl http://localhost:5006/health   # Analytics.API    → Healthy
curl http://localhost:5100/health   # API Gateway      → Healthy
```

### 4 — Open the web UIs

| UI | URL | Credentials |
|---|---|---|
| Grafana | http://localhost:3000 | `GRAFANA_ADMIN_USER` / `GRAFANA_ADMIN_PASSWORD` from `.env` |
| Prometheus | http://localhost:9090 | — |
| RabbitMQ Management | http://localhost:15672 | `RABBITMQ_USER` / `RABBITMQ_PASS` from `.env` |
| API Gateway Swagger | http://localhost:5100/swagger | — |
| Equipment API Swagger | http://localhost:5001/swagger | — |
| Metrics API Swagger | http://localhost:5002/swagger | — |
| Alert API Swagger | http://localhost:5003/swagger | — |
| Notification API Swagger | http://localhost:5004/swagger | — |
| Identity API Swagger | http://localhost:5005/swagger | — |
| Analytics API Swagger | http://localhost:5006/swagger | — |

---

## Authentication

Identity.API issues JWT Bearer tokens. Seed accounts:

| Email | Password | Role |
|---|---|---|
| admin@smartfactory.com | Admin123! | Admin |
| engineer@smartfactory.com | Engineer123! | Engineer |
| operator@smartfactory.com | Operator123! | Operator |

**Get a token:**

```bash
curl -s -X POST http://localhost:5005/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@smartfactory.com","password":"Admin123!"}'
```

Use the returned `token` as `Authorization: Bearer <token>` on protected endpoints.

---

## Analytics API

Analytics.API subscribes to every `MetricRecordedEvent` from RabbitMQ and runs three real-time anomaly detection algorithms on each incoming sensor reading.

### Detection Algorithms

| Algorithm | How it works | Fires when |
|---|---|---|
| **Z-Score** | Measures deviation from the rolling mean in standard deviations: `z = (x − μ) / σ` | `\|z\| ≥ 2.0` (Suspicious), `≥ 3.0` (Anomalous), `≥ 4.5` (Critical) |
| **EWMA** | Exponentially Weighted Moving Average control chart: `EWMA(t) = λx(t) + (1−λ)EWMA(t−1)`, λ=0.2 | EWMA drifts beyond `mean ± 3σ_ewma` |
| **Rate-of-Change** | Single-step percentage jump between consecutive readings | `\|Δ\| > 25%` in one step |

A rolling window of the last 100 readings per (equipment, metricType) pair is maintained in memory by the singleton `AnalyticsEngine`. At least 15 readings are required before any algorithm fires (warm-up protection). On startup, `MetricsSeedWorker` pre-populates these windows from `Metrics.API` history.

### Health Scoring & Predictive Maintenance

Each equipment unit is assigned a **health score** (0–100) computed as a weighted normalcy average across all its metric types:

| Metric | Weight |
|---|---|
| OEE | 30% |
| Vibration | 25% |
| Temperature | 20% |
| YieldRate | 15% |
| PowerConsumption / Throughput | 10% each |

**Remaining Useful Life (RUL)** is estimated via linear regression on a rolling 4-day health score history, projecting days until the score drops below 50 (the maintenance trigger). A `MaintenancePredictedEvent` is published when severity worsens and a 60-minute cooldown has elapsed.

### Analytics Endpoints

```
GET   /api/analytics/anomalies                   # List anomalies (filter by equipment, severity, hours, count)
GET   /api/analytics/anomalies/{id}              # Single anomaly by ID
PATCH /api/analytics/anomalies/{id}/acknowledge  # Mark anomaly as reviewed
GET   /api/analytics/health                      # Fleet health scores (all equipment)
GET   /api/analytics/health/{equipmentId}        # Single equipment health + RUL
GET   /api/analytics/maintenance/schedule        # Maintenance schedule ordered by urgency
GET   /api/analytics/dashboard                   # Fleet overview: anomaly counts + avg health
```

---

## Grafana Dashboards

Navigate to **http://localhost:3000** → SmartFactory folder.

### Equipment Metrics Dashboard

Real-time telemetry for all 5 simulated machines (CNC-001, CONV-001, TEMP-001, ROB-001, QC-001). Auto-refreshes every 10 s.

| Row | Panels |
|---|---|
| **Fleet KPIs** | Avg OEE · Avg Yield Rate · Total Throughput · Threshold Breaches (1h) |
| **Production Performance** | OEE % per equipment · Yield Rate % per equipment |
| **Throughput** | Units/hr per equipment over time |
| **Equipment Health** | Temperature (°C) with warning/critical threshold lines · Vibration (mm/s) |
| **Energy** | Power consumption (kWh) per equipment |
| **Alert Activity** | Threshold breaches by severity · Data ingest rate |
| **Current State** | Horizontal bar gauges: OEE · Temperature · Vibration |

### Service Health Dashboard

HTTP request rates, P95 latency, error rates, memory usage, and up/down status for every service.

---

## Prometheus Metrics

Custom business metrics from Metrics.API (exposed at `:8080/metrics`):

| Metric | Type | Labels | Description |
|---|---|---|---|
| `equipment_metric_value` | Gauge | `equipment_id`, `equipment_name`, `metric_type`, `unit` | Latest sensor reading — primary Grafana source |
| `metrics_ingest_total` | Counter | `equipment_id`, `metric_type` | Individual data points ingested |
| `metrics_ingest_batch_total` | Counter | — | Batch requests from Simulator |
| `metrics_threshold_breaches_total` | Counter | `equipment_id`, `metric_type`, `severity` | Warning / Critical threshold crossings |
| `metrics_db_writes_total` | Counter | — | Successful SQL Server writes |

**Useful PromQL queries:**

```promql
# Current fleet OEE average
avg(equipment_metric_value{metric_type="OEE"})

# Ingestion throughput (points per second, 5-min window)
rate(metrics_ingest_total[5m])

# Critical breaches in last hour
sum(increase(metrics_threshold_breaches_total{severity="Critical"}[1h]))

# Equipment running above temperature warning threshold
equipment_metric_value{metric_type="Temperature"} > 350
```

> **Note:** prometheus-net uses `http_requests_received_total` (not `http_requests_total`).

---

## Event Bus

All events use RabbitMQ exchange `smartfactory_events` (topic type, durable):

| Event | Publisher | Consumer(s) | Trigger |
|---|---|---|---|
| `MetricRecordedEvent` | Metrics.API | Analytics.API | Every metric data point persisted |
| `MetricThresholdBreachedEvent` | Metrics.API | Alert.API | Static warning/critical threshold crossed |
| `AlertTriggeredEvent` | Alert.API | Notification.API | New alert created |
| `EquipmentStatusChangedEvent` | Equipment.API | Notification.API | Equipment changes status |
| `AnomalyDetectedEvent` | Analytics.API | Notification.API | Z-Score/EWMA/RoC anomaly (Anomalous or Critical) |
| `MaintenancePredictedEvent` | Analytics.API | Notification.API | Health degrading, RUL estimated (60 min cooldown) |

---

## Project Structure

```
SmartFactoryHub/
├── src/
│   ├── BuildingBlocks/
│   │   └── BuildingBlocks.Common/          # Shared: IEventBus, RabbitMqEventBus, all integration events
│   ├── ApiGateway/ApiGateway/              # YARP reverse proxy
│   └── Services/
│       ├── Equipment/Equipment.API/
│       ├── Metrics/Metrics.API/
│       ├── Alert/Alert.API/
│       ├── Notification/Notification.API/
│       ├── Identity/Identity.API/
│       ├── Analytics/Analytics.API/        # Phase 3: anomaly detection + predictive maintenance
│       │   ├── Core/                       #   AnalyticsEngine (singleton), CircularBuffer<T>
│       │   ├── Consumers/                  #   MetricRecordedConsumer
│       │   ├── Controllers/                #   AnalyticsController
│       │   ├── Data/                       #   AnalyticsDbContext
│       │   ├── Dtos/
│       │   ├── Models/                     #   AnomalyRecord
│       │   ├── Services/                   #   IAnalyticsService, AnalyticsService
│       │   └── Workers/                    #   MetricsSeedWorker (startup warm-up)
│       └── Simulator/Simulator.Service/
├── tests/
│   ├── Unit/
│   │   ├── Identity.API.Tests/             # 23 tests — PasswordHasher + AuthService
│   │   ├── Alert.API.Tests/                # 19 tests — AlertService state machine
│   │   └── Metrics.API.Tests/             # 17 tests — MetricsService ingestion + aggregation
│   └── Integration/
│       └── Integration.Tests/              # 52 tests — all 5 controllers via WebApplicationFactory
├── deploy/
│   ├── prometheus/prometheus.yml           # 7 scrape targets, 15 s interval
│   └── grafana/
│       ├── provisioning/                   # Auto-provisioned datasource & folders
│       └── dashboards/
│           ├── service-health.json
│           └── equipment-metrics.json
├── docker-compose.yml                      # Full stack definition (12 containers)
├── .env.example                            # Secret template (committed)
└── .env                                    # Local secrets (GITIGNORED)
```

---

## Test Suite

109 tests across 4 projects (107 passing, 2 skipped):

| Project | Tests | Focus |
|---|---|---|
| `Identity.API.Tests` | 23 | PasswordHasher (pure unit), AuthService (JWT generation, login flows) |
| `Alert.API.Tests` | 19 | AlertService state machine (Open → Acknowledged → Resolved) |
| `Metrics.API.Tests` | 17 | Metric ingestion, threshold evaluation, aggregation, dashboard |
| `Integration.Tests` | 52 | All 5 REST controllers via `WebApplicationFactory` + InMemory EF Core |

```bash
dotnet test --verbosity normal
```

---

## Security

All secrets are injected at runtime via environment variables. **Nothing sensitive is committed.**

| Secret | `.env` variable |
|---|---|
| SQL Server SA password | `SA_PASSWORD` |
| JWT signing key (≥ 32 chars) | `JWT_SECRET_KEY` |
| RabbitMQ credentials | `RABBITMQ_USER`, `RABBITMQ_PASS` |
| Grafana admin password | `GRAFANA_ADMIN_USER`, `GRAFANA_ADMIN_PASSWORD` |

For production deployments use a dedicated secrets manager (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault) and rotate all credentials before going live.

---

## Roadmap

| Phase | Status | Description |
|---|---|---|
| Phase 1 | ✅ Complete | Equipment.API · Metrics.API · Simulator · API Gateway · RabbitMQ · Prometheus/Grafana |
| Phase 2 | ✅ Complete | Alert.API · Notification.API (SignalR) · Identity.API (JWT + RBAC) |
| Phase 2b | ✅ Complete | Full test suite — 109 tests (unit + integration via WebApplicationFactory) |
| Phase 3a | ✅ Complete | Analytics.API — Z-Score, EWMA, Rate-of-Change anomaly detection + predictive maintenance RUL |
| Phase 3b | 🔜 Planned | AI Factory Chatbot (Semantic Kernel + Azure OpenAI function-calling) |
| Phase 4 | 🔜 Planned | Angular 17+ SPA (real-time dashboard consuming SignalR + REST) |
| Phase 5 | 🔜 Planned | GitHub Actions CI/CD · AKS deployment |
| Phase 6 | 🔜 Planned | EF Migrations · OpenTelemetry · Azure Key Vault · Kubernetes HPA |

---

## Development Notes

- **Database**: `EnsureCreated()` on startup (EF Migrations planned for Phase 6).
- **Enums**: stored as strings via `HasConversion<string>()`.
- **RabbitMQ consumers**: `BackgroundService` + `IEventBus.SubscribeAsync<T>()` + `Task.Delay(Infinite, ct)`.
- **Prometheus namespace**: inside `Metrics.API`, always use `Prometheus.Metrics.CreateCounter/Gauge(...)` — the project namespace `Metrics.API` would otherwise shadow the `Prometheus.Metrics` static class.
- **SignalR CORS**: requires `.AllowCredentials()` — different from REST CORS config.
- **Swashbuckle v10**: `Microsoft.OpenApi.Models` is not exported — do not reference `OpenApiSecurityScheme` in `Program.cs`.
- **AnalyticsEngine singleton**: the engine holds all rolling windows and health history in memory. It is registered as `AddSingleton<AnalyticsEngine>()`. `AnalyticsService` is scoped and receives the singleton via DI — this is safe because the engine is thread-safe via an internal `lock`.
- **MetricsSeedWorker**: on startup, calls `GET /api/Metrics/equipment/{id}/latest?count=200` for each configured equipment ID and feeds the results into `AnalyticsEngine.SeedMetric()`. This pre-warms rolling windows so anomaly detection starts working immediately rather than waiting for 15+ live readings.
- **Integration tests with InMemory EF Core**: `Guid.NewGuid()` for the database name must be captured *outside* the `AddDbContext` lambda. If computed inside the lambda, each DI scope creates a different database instance — HTTP requests see an empty database even after the test has seeded data.
- **`ExecuteUpdateAsync` / `ExecuteDeleteAsync`**: EF Core bulk operations require a relational provider. Two `NotificationService.MarkAllAsReadAsync()` tests are skipped with `[Fact(Skip = "...")]` when running against the InMemory provider.
