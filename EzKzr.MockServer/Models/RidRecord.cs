namespace EzKzr.MockServer.Models;

public sealed class RidRecord
{
    public string Rid { get; set; } = "";
    public string GivenName { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public DateTime Created { get; set; }
    public bool IsTemporary { get; set; } = false;
    public string? PromotedToRid { get; set; }
}
