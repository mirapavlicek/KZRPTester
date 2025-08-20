namespace EzKzr.MockServer.Models;

public sealed class EmsRun
{
    public Guid Id { get; set; }
    public string Rid { get; set; } = default!;
    public DateTime Started { get; set; }
    public string Reason { get; set; } = default!;
    public Vitals Vitals { get; set; } = new();
    public List<string>? Interventions { get; set; }
    public string Outcome { get; set; } = default!;
    public string Destination { get; set; } = default!;
}
public sealed class Vitals
{
    public int Systolic { get; set; }
    public int Diastolic { get; set; }
    public int HeartRate { get; set; }
    public int Spo2 { get; set; }
    public decimal Temperature { get; set; }
}
public sealed class CreateEmsRecord
{
    public KzrDotaz? ZadostInfo { get; set; }
    public EmsRun? ZadostData { get; set; }
}
