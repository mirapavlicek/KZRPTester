namespace EzKzr.MockServer.Models;

public sealed class ImagingReport
{
    public ImagingHeader Header { get; set; } = new();
    public string? Indication { get; set; }
    public string Findings { get; set; } = "";
    public string Conclusion { get; set; } = "";
}

public sealed class ImagingHeader
{
    public string Rid { get; set; } = "";
    public DateTime Performed { get; set; }
    public string Modality { get; set; } = "";
    public string Performer { get; set; } = "";
    public string FacilityName { get; set; } = "";
}

public sealed class CreateImagingReport
{
    public KzrDotaz? ZadostInfo { get; set; }
    public ImagingReport? ZadostData { get; set; }
}

public sealed class ImagingOrder
{
    public Guid Id { get; set; }
    public string Rid { get; set; } = "";
    public DateTime Created { get; set; }
    public string RequestedModality { get; set; } = "";
    public string? RequestedProcedure { get; set; }
    public string? ClinicalInfo { get; set; }
    public string RequesterIco { get; set; } = "";
    public string RequesterName { get; set; } = "";
    public string? Status { get; set; }
}

public sealed class CreateImagingOrder
{
    public KzrDotaz? ZadostInfo { get; set; }
    public ImagingOrder? ZadostData { get; set; }
}
