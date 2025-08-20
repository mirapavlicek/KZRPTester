namespace EzKzr.MockServer.Models;

public sealed class PatientSummary
{
    public PatientSummaryHeader Header { get; set; } = new();
    public PatientSummaryBody Body { get; set; } = new();
}

public sealed class PatientSummaryHeader
{
    public string Rid { get; set; } = "";
    public string GivenName { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    /// <summary>M/Z/X</summary>
    public string Gender { get; set; } = "X";
}

public sealed class PatientSummaryBody
{
    public List<Allergy>? Allergies { get; set; }
    public List<Problem>? Problems { get; set; }
    public List<Medication>? Medications { get; set; }
    public List<Vaccination>? Vaccinations { get; set; }
    public List<Implant>? Implants { get; set; }
}

public sealed class Allergy
{
    public string Text { get; set; } = "";
    public string? CodeSystem { get; set; }
    public string? Code { get; set; }
    public string? Criticality { get; set; }
}

public sealed class Problem
{
    public string Text { get; set; } = "";
    public string? CodeSystem { get; set; }
    public string? Code { get; set; }
}

public sealed class Medication
{
    public string Text { get; set; } = "";
    public string? Dosage { get; set; }
    public string? Route { get; set; }
}

public sealed class Vaccination
{
    public string Text { get; set; } = "";
    public DateOnly? Date { get; set; }
}

public sealed class Implant
{
    public string Text { get; set; } = "";
}
