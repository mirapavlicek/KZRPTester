namespace EzKzr.MockServer.Models;

public sealed class CreatePatient
{
    public KzrDotaz? ZadostInfo { get; set; }
    public NewPatient? ZadostData { get; set; }
}

public sealed class NewPatient
{
    public string GivenName { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = "X";
    public string? Drid { get; set; }
    public string? Rid { get; set; }
}
