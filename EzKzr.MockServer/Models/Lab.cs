namespace EzKzr.MockServer.Models;

public sealed class LabReport
{
    public LabHeader Header { get; set; } = new();
    public List<LabResult>? Results { get; set; }
    public string? Comments { get; set; }
}
public sealed class LabHeader
{
    public string Rid { get; set; } = default!;
    public DateTime Issued { get; set; }
    public string Laboratory { get; set; } = default!;
    public string? OrderId { get; set; }
}
public sealed class LabResult
{
    public string? Code { get; set; }
    public string Text { get; set; } = default!;
    public string? Value { get; set; }
    public string? Unit { get; set; }
    public string? ReferenceRange { get; set; }
    public string? AbnormalFlag { get; set; }
}
public sealed class CreateLabReport
{
    public KzrDotaz? ZadostInfo { get; set; }
    public LabReport? ZadostData { get; set; }
}
public sealed class LabOrder
{
    public Guid Id { get; set; }
    public string Rid { get; set; } = default!;
    public DateTime Created { get; set; }
    public List<string> Tests { get; set; } = new();
    public string RequesterIco { get; set; } = default!;
    public string RequesterName { get; set; } = default!;
    public string Status { get; set; } = "new";
    public string? Priority { get; set; }
    public string? ClinicalInfo { get; set; }
}
public sealed class CreateLabOrder
{
    public KzrDotaz? ZadostInfo { get; set; }
    public LabOrder? ZadostData { get; set; }
}
