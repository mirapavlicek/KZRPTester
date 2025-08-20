namespace EzKzr.MockServer.Models;

public sealed class PatientSummary
{
    public PatientHeader Header { get; set; } = new();
    public PatientSummaryBody Body { get; set; } = new();
}
public sealed class PatientHeader
{
    public string Rid { get; set; } = default!;
    public string GivenName { get; set; } = default!;
    public string FamilyName { get; set; } = default!;
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = "X";
}
public sealed class PatientSummaryBody
{
    public List<Allergy>? Allergies { get; set; }
    public List<Vaccination>? Vaccinations { get; set; }
    public List<Problem>? Problems { get; set; }
    public List<Medication>? Medications { get; set; }
    public List<Implant>? Implants { get; set; }
    public string? AdvanceDirectives { get; set; }
}
public sealed class Allergy
{
    public string Text { get; set; } = default!;
    public string? CodeSystem { get; set; }
    public string? Code { get; set; }
    public string? Criticality { get; set; }
}
public sealed class Vaccination
{
    public string Text { get; set; } = default!;
    public DateOnly? Date { get; set; }
}
public sealed class Problem
{
    public string Text { get; set; } = default!;
    public string? CodeSystem { get; set; }
    public string? Code { get; set; }
}
public sealed class Medication
{
    public string Text { get; set; } = default!;
    public string? Dosage { get; set; }
    public string? Route { get; set; }
}
public sealed class Implant
{
    public string Text { get; set; } = default!;
    public DateOnly? Date { get; set; }
}
