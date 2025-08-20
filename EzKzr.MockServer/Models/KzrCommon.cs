namespace EzKzr.MockServer.Models;

public sealed class KzrDotaz
{
    public string? Ucel { get; set; }
    public DateTime? Datum { get; set; }
}

public sealed class ReklamaceBody
{
    public KzrDotaz? ZadostInfo { get; set; }
    public UdajReklamaceBulk? ZadostData { get; set; }
}

public sealed class UdajReklamaceBulk
{
    public long Krpzsid { get; set; }
    public string? UlozkaId { get; set; }
    public int? UlozkaRef { get; set; }
    public DateTime DatumReklamace { get; set; }
    public Reklamujici? Reklamujici { get; set; }
    public List<UdajReklamace>? PolozkyReklamace { get; set; }
    public string? Zduvodneni { get; set; }
    public string? PopisReklamace { get; set; }
}

public sealed class UdajReklamace
{
    public string? Klic { get; set; }
    public string? PuvodniHodnota { get; set; }
    public string? PozadovanaHodnota { get; set; }
}

public sealed class Reklamujici
{
    public string? Ico { get; set; }
    public string? Nazev { get; set; }
    public string? KontaktEmail { get; set; }
}