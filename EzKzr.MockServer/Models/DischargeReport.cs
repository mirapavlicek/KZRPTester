namespace EzKzr.MockServer.Models;

public sealed class DischargeReport
{
    public DischargeHeader Header { get; set; } = new();
    public string? ReasonForAdmission { get; set; }
    public List<string>? Diagnoses { get; set; }
    public List<string>? Procedures { get; set; }
    public string? Course { get; set; }
    public List<MedEntry>? DischargeMedications { get; set; }
    public string? Recommendations { get; set; }
    public string? FollowUp { get; set; }
}

public sealed class DischargeHeader
{
    public string Rid { get; set; } = "";
    public DateTime Discharge { get; set; }
    public string AttendingDoctor { get; set; } = "";
    public string FacilityName { get; set; } = "";
}

public sealed class MedEntry
{
    public string Name { get; set; } = "";
    public string? Dosage { get; set; }
    public string? Instructions { get; set; }
}
