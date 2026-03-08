# SmartFactory Hub

A cloud-native **industrial IoT monitoring platform** built with .NET 8 microservices. Real-time equipment telemetry flows through a RabbitMQ event bus, is persisted in SQL Server, and is visualised in Grafana dashboards powered by Prometheus metrics.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         API Gateway (YARP)  :5100               │
│    /api/equipment  /api/metrics  /api/alerts  /api/auth         │
│    /api/notifications  /hubs (SignalR)  /api/users              │
└──────┬──────────┬──────────┬──────────┬──────────┬─────────────┘
       │          │          │          │          │
  Equipment   Metrics    Alert     Notification  Identity
    API         API       API         API          API
   :5001       :5002     :5003       :5004        :5005
       │          │          │          │
       └──────────┴──────────┴──────────┘
                        │
               RabbitMQ (topic exchange)
               smartfactory_events
                        │
       ┌────────────────┴────────────────┐
       │                                 │
  SQL Server (per-service DB)       Simulator
  SmartFactory_Equipment             (pushes batch
  SmartFactory_Metrics                metrics every
  SmartFactory_Alerts                 10 s)
  SmartFactory_Notifications
  SmartFactory_Identity
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

All 11 containers start in dependency order. SQL Server takes ~60 s on first run.

### 3 — Verify everything is healthy

```bash
curl http://localhost:5001/health   # Equipment.API   → Healthy
curl http://localhost:5002/health   # Metrics.API     → Healthy
curl http://localhost:5003/health   # Alert.API       → Healthy
curl http://localhost:5004/health   # Notification.API → Healthy
curl http://localhost:5005/health   # Identity.API    → Healthy
curl http://localhost:5100/health   # API Gateway     → Healthy
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

| Event | Publisher | Consumer(s) | Queue |
|---|---|---|---|
| `MetricRecordedEvent` | Metrics.API | — | — |
| `MetricThresholdBreachedEvent` | Metrics.API | Alert.API | `MetricThresholdBreachedEvent_queue` |
| `AlertTriggeredEvent` | Alert.API | Notification.API | `AlertTriggeredEvent_queue` |
| `EquipmentStatusChangedEvent` | Equipment.API | Notification.API | `EquipmentStatusChangedEvent_queue` |

---

## Project Structure

```
SmartFactoryHub/
├── src/
│   ├── BuildingBlocks/
│   │   └── BuildingBlocks.Common/          # Shared: IEventBus, RabbitMqEventBus, events
│   ├── ApiGateway/ApiGateway/              # YARP reverse proxy
│   └── Services/
│       ├── Equipment/Equipment.API/
│       ├── Metrics/Metrics.API/
│       ├── Alert/Alert.API/
│       ├── Notification/Notification.API/
│       ├── Identity/Identity.API/
│       └── Simulator/Simulator.Service/
├── deploy/
│   ├── prometheus/prometheus.yml           # 7 scrape targets, 15 s interval
│   └── grafana/
│       ├── provisioning/                   # Auto-provisioned datasource & folders
│       └── dashboards/
│           ├── service-health.json
│           └── equipment-metrics.json
├── docker-compose.yml                      # Full stack definition
├── .env.example                            # Secret template (committed)
└── .env                                    # Local secrets (GITIGNORED)
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
| Phase 3 | 🔜 Planned | Angular 17+ SPA (real-time dashboard) |
| Phase 4 | 🔜 Planned | GitHub Actions CI/CD · AKS deployment |
| Phase 5 | 🔜 Planned | EF Migrations · OpenTelemetry · Azure Key Vault · Kubernetes HPA |

---

## Development Notes

- **Database**: `EnsureCreated()` on startup (EF Migrations planned for Phase 5).
- **Enums**: stored as strings via `HasConversion<string>()`.
- **RabbitMQ consumers**: `BackgroundService` + `IEventBus.SubscribeAsync<T>()` + `Task.Delay(Infinite, ct)`.
- **Prometheus namespace**: inside `Metrics.API`, always use `Prometheus.Metrics.CreateCounter/Gauge(...)` — the project namespace `Metrics.API` would otherwise shadow the `Prometheus.Metrics` static class.
- **SignalR CORS**: requires `.AllowCredentials()` — different from REST CORS config.
- **Swashbuckle v10**: `Microsoft.OpenApi.Models` is not exported — do not reference `OpenApiSecurityScheme` in `Program.cs`.
