namespace EzKzr.MockServer.Models;

public sealed class DischargeReport
{
    public DischargeHeader Header { get; set; } = new();
    public string ReasonForAdmission { get; set; } = default!;
    public List<string>? Diagnoses { get; set; }
    public List<string>? Procedures { get; set; }
    public string Course { get; set; } = default!;
    public List<MedicationOnDischarge>? DischargeMedications { get; set; }
    public string? FollowUp { get; set; }
    public string? Recommendations { get; set; }
}
public sealed class DischargeHeader
{
    public string Rid { get; set; } = default!;
    public string FacilityIco { get; set; } = default!;
    public string FacilityName { get; set; } = default!;
    public string Department { get; set; } = default!;
    public string AttendingDoctor { get; set; } = default!;
    public DateTime Admission { get; set; }
    public DateTime Discharge { get; set; }
    public string DischargeDestination { get; set; } = default!;
}
public sealed class MedicationOnDischarge
{
    public string Name { get; set; } = default!;
    public string? Dosage { get; set; }
    public string? Instructions { get; set; }
}
