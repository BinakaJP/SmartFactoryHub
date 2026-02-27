namespace Simulator.Service.Configuration;

public class SimulatorConfig
{
    public int IntervalSeconds { get; set; } = 10;
    public string MetricsApiUrl { get; set; } = "http://localhost:5002";
    public string EquipmentApiUrl { get; set; } = "http://localhost:5001";
    public List<SimulatedEquipment> Equipment { get; set; } = new();
}

public class SimulatedEquipment
{
    public string EquipmentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, MetricSimulation> Metrics { get; set; } = new();
}

public class MetricSimulation
{
    public double BaseValue { get; set; }
    public double Variance { get; set; }
    public double DriftRate { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double? FailureThreshold { get; set; }
}
