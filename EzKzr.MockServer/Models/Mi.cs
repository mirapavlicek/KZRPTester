namespace EzKzr.MockServer.Models;

public sealed class ImagingReport
{
    public ImagingHeader Header { get; set; } = new();
    public string Indication { get; set; } = default!;
    public string Findings { get; set; } = default!;
    public string Conclusion { get; set; } = default!;
    public string? Recommendations { get; set; }
}
public sealed class ImagingHeader
{
    public string Rid { get; set; } = default!;
    public DateTime Performed { get; set; }
    public string Modality { get; set; } = default!;
    public string Performer { get; set; } = default!;
    public string FacilityName { get; set; } = default!;
}
public sealed class CreateImagingReport
{
    public KzrDotaz? ZadostInfo { get; set; }
    public ImagingReport? ZadostData { get; set; }
}
public sealed class ImagingOrder
{
    public Guid Id { get; set; }
    public string Rid { get; set; } = default!;
    public DateTime Created { get; set; }
    public string RequestedModality { get; set; } = default!;
    public string? RequestedProcedure { get; set; }
    public string? ClinicalInfo { get; set; }
    public string RequesterIco { get; set; } = default!;
    public string RequesterName { get; set; } = default!;
    public string Status { get; set; } = "new";
}
public sealed class CreateImagingOrder
{
    public KzrDotaz? ZadostInfo { get; set; }
    public ImagingOrder? ZadostData { get; set; }
}
