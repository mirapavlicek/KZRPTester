namespace EzKzr.MockServer.Models;

public sealed class ProviderDto
{
    public string Ico { get; set; } = default!;
    public string Nazev { get; set; } = default!;
    public List<Adresa> MistaPoskytovani { get; set; } = new();
}
public sealed class Adresa
{
    public string? Ulice { get; set; }
    public string? CisloPopisne { get; set; }
    public string? Obec { get; set; }
    public string? Psc { get; set; }
    public string? Stat { get; set; }
}
public sealed class WorkerDto
{
    public long KrzpId { get; set; }
    public string Jmeno { get; set; } = default!;
    public string Prijmeni { get; set; } = default!;
    public DateOnly DatumNarozeni { get; set; }
    public string ZamestnavatelIco { get; set; } = default!;
    public string Odbornost { get; set; } = default!;
}

public sealed record CiselnikItem(string Kod, string Popis);

public sealed class Notification
{
    public Guid Id { get; set; }
    public string System { get; set; } = default!;
    public string Typ { get; set; } = default!;
    public string? Kriteria { get; set; }
    public string Kanal { get; set; } = "internal";
    public DateTime Vytvoreno { get; set; }
    public string Stav { get; set; } = "aktivni";
}

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
    public int UlozkaRef { get; set; }
    public DateTime DatumReklamace { get; set; }
    public Reklamujici Reklamujici { get; set; } = new();
    public List<UdajReklamace> PolozkyReklamace { get; set; } = new();
    public string? Zduvodneni { get; set; }
    public string? PopisReklamace { get; set; }
}
public sealed class Reklamujici
{
    public string? Ico { get; set; }
    public string? Nazev { get; set; }
    public string? KontaktEmail { get; set; }
}
public sealed class UdajReklamace
{
    public string Klic { get; set; } = default!;
    public string? PuvodniHodnota { get; set; }
    public string? PozadovanaHodnota { get; set; }
}

public sealed class CreateNotification
{
    public KzrDotaz? ZadostInfo { get; set; }
    public NotificationRequest? ZadostData { get; set; }
}
public sealed class NotificationRequest
{
    public string? System { get; set; }
    public string? Typ { get; set; }
    public string? Kriteria { get; set; }
    public string? Kanal { get; set; }
}

public sealed class RidRecord
{
    public string Rid { get; set; } = default!;
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public DateTime Created { get; set; }
}
