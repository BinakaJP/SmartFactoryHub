using Prometheus;

namespace Metrics.API.Instrumentation;

/// <summary>
/// Custom Prometheus metrics for the Metrics.API service.
/// All metrics are exposed at /metrics and scraped by Prometheus every 15s.
/// </summary>
public static class FactoryMetrics
{
    /// <summary>
    /// Total individual data points ingested, by equipment and metric type.
    /// Query: metrics_ingest_total{equipment_id="CNC-001", metric_type="Temperature"}
    /// Rate:  rate(metrics_ingest_total[5m])
    /// </summary>
    public static readonly Counter MetricsIngestTotal = Prometheus.Metrics.CreateCounter(
        "metrics_ingest_total",
        "Total number of individual metric data points ingested.",
        new CounterConfiguration
        {
            LabelNames = new[] { "equipment_id", "metric_type" }
        });

    /// <summary>
    /// Total batch ingest requests received from the Simulator.
    /// Query: metrics_ingest_batch_total
    /// Rate:  rate(metrics_ingest_batch_total[5m])
    /// </summary>
    public static readonly Counter MetricsIngestBatchTotal = Prometheus.Metrics.CreateCounter(
        "metrics_ingest_batch_total",
        "Total number of batch ingest requests received.");

    /// <summary>
    /// Total threshold breaches detected during ingestion, by equipment, metric type and severity.
    /// Query: metrics_threshold_breaches_total{severity="Critical"}
    /// </summary>
    public static readonly Counter ThresholdBreachesTotal = Prometheus.Metrics.CreateCounter(
        "metrics_threshold_breaches_total",
        "Total number of metric threshold breaches detected.",
        new CounterConfiguration
        {
            LabelNames = new[] { "equipment_id", "metric_type", "severity" }
        });

    /// <summary>
    /// Total data points saved to SQL Server (tracks DB write success).
    /// Query: metrics_db_writes_total
    /// </summary>
    public static readonly Counter DbWritesTotal = Prometheus.Metrics.CreateCounter(
        "metrics_db_writes_total",
        "Total number of metric data points successfully written to SQL Server.");

    /// <summary>
    /// Latest observed value of each equipment metric — the core gauge for Grafana dashboards.
    /// Labels: equipment_id (GUID), equipment_name (friendly), metric_type, unit.
    /// Query: equipment_metric_value{equipment_name="CNC-001", metric_type="Temperature"}
    /// NOTE: equipment_name is resolved from KnownEquipmentNames below.
    ///       TODO Phase 5 — resolve dynamically from Equipment.API via service discovery.
    /// </summary>
    public static readonly Gauge EquipmentMetricValue = Prometheus.Metrics.CreateGauge(
        "equipment_metric_value",
        "Latest observed value of an equipment metric.",
        new GaugeConfiguration
        {
            LabelNames = new[] { "equipment_id", "equipment_name", "metric_type", "unit" }
        });

    /// <summary>
    /// Maps equipment GUIDs (seeded by Equipment.API) to friendly display names.
    /// Used to populate the equipment_name Prometheus label without a cross-service call.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownEquipmentNames =
        new Dictionary<string, string>
        {
            ["a1b2c3d4-e5f6-7890-abcd-ef1234567890"] = "CNC-001",
            ["b2c3d4e5-f6a7-8901-bcde-f12345678901"] = "CONV-001",
            ["c3d4e5f6-a7b8-9012-cdef-123456789012"] = "TEMP-001",
            ["d4e5f6a7-b8c9-0123-defa-234567890123"] = "ROB-001",
            ["e5f6a7b8-c9d0-1234-efab-345678901234"] = "QC-001"
        };
}
