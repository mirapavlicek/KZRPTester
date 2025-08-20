namespace EzKzr.MockServer.Models;

public sealed class ProviderDto
{
    public string Ico { get; set; } = "";
    public string Nazev { get; set; } = "";
}

public sealed class WorkerDto
{
    public long KrzpId { get; set; }
    public string Jmeno { get; set; } = "";
    public string Prijmeni { get; set; } = "";
    public DateOnly DatumNarozeni { get; set; }
    public string ZamestnavatelIco { get; set; } = "";
}
