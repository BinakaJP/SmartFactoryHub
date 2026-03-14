namespace Analytics.API.Core;

/// <summary>
/// Singleton in-memory analytics engine.
///
/// Maintains per-(equipment, metricType) circular buffers of recent values and
/// runs three anomaly detection algorithms on every incoming metric:
///
///   1. Z-Score         — how many std-devs is this value from the rolling mean?
///   2. EWMA            — has the exponentially weighted average drifted past control limits?
///   3. Rate-of-Change  — did the value jump more than 25% in a single step?
///
/// Also maintains per-equipment health score history for Remaining Useful Life (RUL)
/// estimation via linear regression.
///
/// Thread-safe via a single lock — acceptable since RabbitMQ consumers process
/// events serially per queue and API reads are fast.
/// </summary>
public sealed class AnalyticsEngine
{
    // ── Configuration constants ───────────────────────────────────────────────

    private const int    WindowSize            = 100;   // rolling value buffer per metric
    private const int    MinWindowForAnalysis  = 15;    // need this many readings before flagging
    private const double LambdaEwma            = 0.2;   // EWMA smoothing factor (lower = smoother)
    private const double ZScoreSuspicious      = 2.0;
    private const double ZScoreAnomalous       = 3.0;
    private const double ZScoreCritical        = 4.5;
    private const double EwmaControlK          = 3.0;   // k-sigma EWMA control limits
    private const double RateOfChangePct       = 25.0;  // % single-step change = anomaly
    private const int    HealthHistoryCapacity = 576;   // ~4 days at one reading per minute
    private const double MaintenanceTrigger    = 50.0;  // health score below this → maintenance
    private const int    MinHealthForRul       = 5;     // need this many health snapshots for RUL
    private const int    MaintenanceEventCooldownMinutes = 60;

    // ── Per-metric state (key = "equipmentId:metricType") ────────────────────

    private readonly Dictionary<string, CircularBuffer<double>> _windows   = new();
    private readonly Dictionary<string, double>                 _ewma      = new();
    private readonly Dictionary<string, double>                 _ewmaTarget= new(); // long-run baseline
    private readonly Dictionary<string, double>                 _prevValue = new();
    private readonly Dictionary<string, double>                 _latestNormalcy = new(); // 0-1, for health

    // ── Per-equipment state ───────────────────────────────────────────────────

    private readonly HashSet<string>                                       _knownEquipment    = new();
    private readonly Dictionary<string, string>                            _equipmentNames    = new();
    private readonly Dictionary<string, Queue<(DateTime ts, double score)>> _healthHistory    = new();
    private readonly Dictionary<string, string>                            _lastMaintSeverity = new();
    private readonly Dictionary<string, DateTime>                          _lastMaintEventAt  = new();

    private readonly ILogger<AnalyticsEngine> _logger;
    private readonly object _lock = new();

    public AnalyticsEngine(ILogger<AnalyticsEngine> logger) => _logger = logger;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Record a new metric value and run all anomaly detection algorithms.
    /// Returns an <see cref="AnalysisResult"/> describing the outcome.
    /// Call <see cref="ComputeHealth"/> afterward to refresh the equipment health score.
    /// </summary>
    public AnalysisResult RecordAndAnalyze(
        string equipmentId, string equipmentName, string metricType, double value)
    {
        lock (_lock)
        {
            _knownEquipment.Add(equipmentId);
            _equipmentNames[equipmentId] = equipmentName;

            var key = MetricKey(equipmentId, metricType);

            if (!_windows.TryGetValue(key, out var buffer))
            {
                buffer = new CircularBuffer<double>(WindowSize);
                _windows[key] = buffer;
            }

            var result = Analyze(key, buffer, value);

            // Update per-metric normalcy score (used by health calculator)
            _latestNormalcy[key] = result.MetricNormalcyScore;

            // Advance state
            _prevValue[key] = value;
            buffer.Push(value);

            return result;
        }
    }

    /// <summary>
    /// Compute the current health score for an equipment from all its tracked metrics,
    /// record it in the health history, and estimate Remaining Useful Life.
    /// Returns null if the equipment has no tracked metrics yet.
    /// </summary>
    public EquipmentHealth? ComputeHealth(string equipmentId)
    {
        lock (_lock)
        {
            if (!_knownEquipment.Contains(equipmentId)) return null;

            // Collect normalcy scores for all metrics belonging to this equipment
            var metricNormalcy = _latestNormalcy
                .Where(kv => kv.Key.StartsWith(equipmentId + ":"))
                .Select(kv => (MetricType: kv.Key[(equipmentId.Length + 1)..], Normalcy: kv.Value))
                .ToList();

            if (metricNormalcy.Count == 0) return null;

            // Weighted average → health score 0-100
            // Weights mirror the design doc (OEE and Vibration matter most)
            double health = 100.0 * WeightedNormalcy(metricNormalcy);

            string severity = health switch
            {
                >= 80 => "Healthy",
                >= 60 => "Advisory",
                >= 40 => "Warning",
                _     => "Urgent"
            };

            // Triggering metrics: those with normalcy < 0.75 (|z| > 1)
            var triggering = metricNormalcy
                .Where(m => m.Normalcy < 0.75)
                .OrderBy(m => m.Normalcy)
                .Select(m => m.MetricType)
                .ToArray();

            // Record in health history for trend/RUL
            if (!_healthHistory.TryGetValue(equipmentId, out var history))
            {
                history = new Queue<(DateTime, double)>(HealthHistoryCapacity);
                _healthHistory[equipmentId] = history;
            }
            if (history.Count >= HealthHistoryCapacity) history.Dequeue();
            history.Enqueue((DateTime.UtcNow, health));

            string trend = ComputeTrend(history);
            int? rulDays = EstimateRulDays(health, history);
            bool shouldPublish = ShouldPublishMaintenanceEvent(equipmentId, severity, rulDays);

            return new EquipmentHealth(
                EquipmentId:                 equipmentId,
                EquipmentName:               _equipmentNames.GetValueOrDefault(equipmentId, equipmentId),
                HealthScore:                 Math.Round(health, 1),
                Severity:                    severity,
                Trend:                       trend,
                EstimatedDaysToMaintenance:  rulDays,
                TriggeringMetrics:           triggering,
                ComputedAt:                  DateTime.UtcNow,
                ShouldPublishMaintenanceEvent: shouldPublish
            );
        }
    }

    /// <summary>Returns health for all known equipment.</summary>
    public IEnumerable<EquipmentHealth> GetAllHealth()
    {
        IEnumerable<string> ids;
        lock (_lock) { ids = _knownEquipment.ToList(); }

        foreach (var id in ids)
        {
            var h = ComputeHealth(id);
            if (h is not null) yield return h;
        }
    }

    /// <summary>Seeds the rolling window for a metric from historical data (called on startup).</summary>
    public void SeedMetric(string equipmentId, string equipmentName, string metricType,
        IEnumerable<double> historicalValues)
    {
        lock (_lock)
        {
            _knownEquipment.Add(equipmentId);
            _equipmentNames[equipmentId] = equipmentName;

            var key = MetricKey(equipmentId, metricType);
            if (!_windows.TryGetValue(key, out var buffer))
            {
                buffer = new CircularBuffer<double>(WindowSize);
                _windows[key] = buffer;
            }

            double? last = null;
            foreach (var v in historicalValues)
            {
                buffer.Push(v);
                last = v;
            }

            // Initialise EWMA from the seeded mean
            var vals = buffer.Values.ToList();
            if (vals.Count > 0)
            {
                double mean = vals.Average();
                _ewma[key]       = mean;
                _ewmaTarget[key] = mean;
            }
            if (last.HasValue) _prevValue[key] = last.Value;

            _logger.LogDebug("Seeded {Count} values for {EquipmentId}:{MetricType}",
                buffer.Count, equipmentId, metricType);
        }
    }

    public IReadOnlyCollection<string> KnownEquipment
    {
        get { lock (_lock) { return _knownEquipment.ToList(); } }
    }

    // ── Core detection logic ──────────────────────────────────────────────────

    private AnalysisResult Analyze(string key, CircularBuffer<double> buffer, double value)
    {
        var existing = buffer.Values.ToList();

        // Not enough history — return a healthy no-op result
        if (existing.Count < MinWindowForAnalysis)
        {
            return new AnalysisResult(
                ZScore: 0, EwmaValue: value, RateOfChangePercent: 0,
                Severity: AnomalySeverity.None, TriggeringMethod: null,
                ExpectedValue: value, MetricNormalcyScore: 1.0,
                HasSufficientHistory: false);
        }

        double mean = existing.Average();
        double variance = existing.Select(v => (v - mean) * (v - mean)).Average();
        double std = Math.Sqrt(variance);

        double zScore = 0;
        double ewmaValue = value;
        double roc = 0;
        AnomalySeverity severity = AnomalySeverity.None;
        string? method = null;
        double expectedValue = mean;

        // ── Z-Score ───────────────────────────────────────────────────────────
        if (std > 1e-9)
        {
            zScore = (value - mean) / std;
            double absZ = Math.Abs(zScore);
            (severity, method) = absZ switch
            {
                >= ZScoreCritical   => (AnomalySeverity.Critical,   "ZScore"),
                >= ZScoreAnomalous  => (AnomalySeverity.Anomalous,  "ZScore"),
                >= ZScoreSuspicious => (AnomalySeverity.Suspicious, "ZScore"),
                _                   => (AnomalySeverity.None, null)
            };
        }

        // ── EWMA control chart ────────────────────────────────────────────────
        double prevEwma = _ewma.GetValueOrDefault(key, mean);
        ewmaValue = LambdaEwma * value + (1 - LambdaEwma) * prevEwma;
        _ewma[key] = ewmaValue;

        if (std > 1e-9 && severity < AnomalySeverity.Anomalous)
        {
            // σ_EWMA = σ × √(λ / (2 − λ))
            double sigmaEwma = std * Math.Sqrt(LambdaEwma / (2.0 - LambdaEwma));
            double ewmaTarget = _ewmaTarget.GetValueOrDefault(key, mean);
            if (sigmaEwma > 1e-9 && Math.Abs(ewmaValue - ewmaTarget) > EwmaControlK * sigmaEwma)
            {
                severity = AnomalySeverity.Anomalous;
                method = "EWMA";
                expectedValue = ewmaTarget;
            }
        }

        // Slowly adapt EWMA target toward long-run mean (10% weight per update)
        _ewmaTarget[key] = 0.9 * _ewmaTarget.GetValueOrDefault(key, mean) + 0.1 * mean;

        // ── Rate-of-Change ────────────────────────────────────────────────────
        if (_prevValue.TryGetValue(key, out double prev) && Math.Abs(prev) > 1e-9)
        {
            roc = (value - prev) / Math.Abs(prev) * 100.0;
            if (Math.Abs(roc) > RateOfChangePct && severity < AnomalySeverity.Anomalous)
            {
                severity = AnomalySeverity.Anomalous;
                method = "RateOfChange";
                expectedValue = prev;
            }
        }

        // ── Normalcy score (0–1) for health aggregation ───────────────────────
        // 0 sigma → 1.0 (perfect), 4 sigma → 0.0 (critical)
        double normalcy = Math.Max(0.0, 1.0 - Math.Abs(zScore) / 4.0);
        double deviationPct = mean > 1e-9 ? Math.Abs(value - mean) / mean * 100.0 : 0;

        return new AnalysisResult(
            ZScore:               Math.Round(zScore, 3),
            EwmaValue:            Math.Round(ewmaValue, 3),
            RateOfChangePercent:  Math.Round(roc, 2),
            Severity:             severity,
            TriggeringMethod:     method,
            ExpectedValue:        Math.Round(expectedValue, 3),
            MetricNormalcyScore:  normalcy,
            HasSufficientHistory: true);
    }

    // ── Health helpers ────────────────────────────────────────────────────────

    private static readonly Dictionary<string, double> MetricWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OEE"]              = 0.30,
        ["Vibration"]        = 0.25,
        ["Temperature"]      = 0.20,
        ["YieldRate"]        = 0.15,
        ["PowerConsumption"] = 0.10,
        ["Throughput"]       = 0.10,
    };

    private static double WeightedNormalcy(List<(string MetricType, double Normalcy)> metrics)
    {
        double weightSum = 0, weightedSum = 0;
        foreach (var (mt, n) in metrics)
        {
            double w = MetricWeights.GetValueOrDefault(mt, 0.10);
            weightSum   += w;
            weightedSum += w * n;
        }
        return weightSum > 0 ? weightedSum / weightSum : metrics.Average(m => m.Normalcy);
    }

    private static string ComputeTrend(Queue<(DateTime ts, double score)> history)
    {
        if (history.Count < 5) return "Unknown";

        var recent = history.TakeLast(5).Select(h => h.score).ToList();
        double delta = recent[^1] - recent[0];

        return delta switch
        {
            > 2  => "Improving",
            < -2 => "Degrading",
            _    => "Stable"
        };
    }

    private static int? EstimateRulDays(double currentHealth,
        Queue<(DateTime ts, double score)> history)
    {
        if (history.Count < MinHealthForRul) return null;
        if (currentHealth <= MaintenanceTrigger) return 0;

        // Build (hours_ago, score) pairs for linear regression
        var now = DateTime.UtcNow;
        var points = history
            .Select(h => ((now - h.ts).TotalHours, h.score))
            .OrderBy(p => p.Item1)   // oldest first (most negative hours_ago)
            .ToList();

        // x = hours elapsed since oldest point, y = health score
        double x0 = points[0].Item1;
        var reg = points.Select(p => (x: p.Item1 - x0, y: p.score)).ToList();

        int n = reg.Count;
        double sumX  = reg.Sum(p => p.x);
        double sumY  = reg.Sum(p => p.y);
        double sumXY = reg.Sum(p => p.x * p.y);
        double sumX2 = reg.Sum(p => p.x * p.x);
        double denom = n * sumX2 - sumX * sumX;

        if (Math.Abs(denom) < 1e-9) return null;

        double slope = (n * sumXY - sumX * sumY) / denom; // health change per hour

        // Only predict if health is degrading
        if (slope >= 0) return null;

        // Hours until health reaches MaintenanceTrigger
        double rulHours = (currentHealth - MaintenanceTrigger) / Math.Abs(slope);
        int rulDays = (int)Math.Ceiling(rulHours / 24.0);
        return Math.Max(0, Math.Min(rulDays, 365)); // cap at 1 year
    }

    private bool ShouldPublishMaintenanceEvent(string equipmentId, string severity, int? rulDays)
    {
        // Only publish for Warning/Urgent severity with an estimated RUL
        if (severity is "Healthy" or "Advisory") return false;
        if (!rulDays.HasValue) return false;

        // Cooldown: don't re-publish within the cooldown window
        if (_lastMaintEventAt.TryGetValue(equipmentId, out var lastAt) &&
            (DateTime.UtcNow - lastAt).TotalMinutes < MaintenanceEventCooldownMinutes)
            return false;

        // Publish if severity just changed or if this is the first event
        string prevSeverity = _lastMaintSeverity.GetValueOrDefault(equipmentId, "Healthy");
        bool severityChanged = prevSeverity != severity;

        if (severityChanged || !_lastMaintEventAt.ContainsKey(equipmentId))
        {
            _lastMaintSeverity[equipmentId] = severity;
            _lastMaintEventAt[equipmentId]  = DateTime.UtcNow;
            return true;
        }

        return false;
    }

    private static string MetricKey(string equipmentId, string metricType)
        => $"{equipmentId}:{metricType}";
}

// ── Value objects ─────────────────────────────────────────────────────────────

public enum AnomalySeverity { None, Suspicious, Anomalous, Critical }

public record AnalysisResult(
    double          ZScore,
    double          EwmaValue,
    double          RateOfChangePercent,
    AnomalySeverity Severity,
    string?         TriggeringMethod,
    double          ExpectedValue,
    double          MetricNormalcyScore,
    bool            HasSufficientHistory);

public record EquipmentHealth(
    string   EquipmentId,
    string   EquipmentName,
    double   HealthScore,
    string   Severity,
    string   Trend,
    int?     EstimatedDaysToMaintenance,
    string[] TriggeringMetrics,
    DateTime ComputedAt,
    bool     ShouldPublishMaintenanceEvent);
