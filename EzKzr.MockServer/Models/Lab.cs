namespace EzKzr.MockServer.Models;

public sealed class LabReport
{
    public LabHeader Header { get; set; } = new();
    public List<LabResult>? Results { get; set; }
}

public sealed class LabHeader
{
    public string Rid { get; set; } = "";
    public DateTime Issued { get; set; }
    public string Laboratory { get; set; } = "";
    public string? OrderId { get; set; }
}

public sealed class LabResult
{
    public string? Code { get; set; }
    public string Text { get; set; } = "";
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
    public string Rid { get; set; } = "";
    public DateTime Created { get; set; }
    public List<string> Tests { get; set; } = new();
    public string RequesterIco { get; set; } = "";
    public string RequesterName { get; set; } = "";
    public string? Status { get; set; }
}

public sealed class CreateLabOrder
{
    public KzrDotaz? ZadostInfo { get; set; }
    public LabOrder? ZadostData { get; set; }
}
