// Program.cs
using System.Collections.Concurrent;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;

namespace EzKzr.MockServer;

public static class Program
{
    // In‑memory store
    private static readonly List<ProviderDto> Providers =
    [
        new ProviderDto
        {
            Ico = "12345678",
            Nazev = "Nemocnice Alfa, a.s.",
            MistaPoskytovani =
            [
                new Adresa { Ulice = "U nemocnice", CisloPopisne = "1", Obec = "Praha", Psc = "12000", Stat = "CZ" }
            ]
        },
        new ProviderDto
        {
            Ico = "87654321",
            Nazev = "Poliklinika Beta, s.r.o.",
            MistaPoskytovani =
            [
                new Adresa { Ulice = "Zdravotní", CisloPopisne = "22", Obec = "Brno", Psc = "60200", Stat = "CZ" }
            ]
        }
    ];

    private static readonly List<WorkerDto> Workers =
    [
        new WorkerDto { KrzpId = 100001, Jmeno = "Jana", Prijmeni = "Nováková", DatumNarozeni = new DateOnly(1987, 5, 12), ZamestnavatelIco = "12345678", Odbornost = "001" },
        new WorkerDto { KrzpId = 100002, Jmeno = "Petr", Prijmeni = "Svoboda", DatumNarozeni = new DateOnly(1979, 9, 3), ZamestnavatelIco = "87654321", Odbornost = "913" }
    ];

    private static readonly ConcurrentDictionary<Guid, Notification> Notifications = new();

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(); // no WithOpenApi used

        builder.Services.Configure<JsonOptions>(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        MapRoutes(app);

        app.Run();
    }

    private static void MapRoutes(WebApplication app)
    {
        // Health
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
           .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

        var api = app.MapGroup("/api/v1");

        // ----- ČÍSELNÍKY (společné) -----
        var ciselnik = api.MapGroup("/ciselnik/{zadostId:guid}");

        ciselnik.MapGet("/stat", (Guid zadostId, string? ucel, DateTime? datum) =>
        {
            var items = new[]
            {
                new CiselnikItem("CZ", "Česká republika"),
                new CiselnikItem("SK", "Slovensko"),
                new CiselnikItem("DE", "Německo"),
            };
            return Ok(items, zadostId, "OK", popis: "CiselnikStat");
        });

        ciselnik.MapGet("/pohlavi", (Guid zadostId, string? ucel, DateTime? datum) =>
        {
            var items = new[]
            {
                new CiselnikItem("M", "Muž"),
                new CiselnikItem("Z", "Žena"),
                new CiselnikItem("X", "Neurčeno")
            };
            return Ok(items, zadostId, "OK", popis: "CiselnikPohlavi");
        });

        ciselnik.MapGet("/zdravotni_pojistovna", (Guid zadostId, string? ucel, DateTime? datum) =>
        {
            var items = new[]
            {
                new CiselnikItem("111", "VZP"),
                new CiselnikItem("201", "VoZP"),
                new CiselnikItem("205", "ČPZP"),
                new CiselnikItem("207", "OZP"),
                new CiselnikItem("209", "ZPŠ"),
                new CiselnikItem("211", "ZPMV")
            };
            return Ok(items, zadostId, "OK", popis: "CiselnikZdravotniPojistovna");
        });

        ciselnik.MapGet("/druh_dokladu", (Guid zadostId, string? ucel, DateTime? datum) =>
        {
            var items = new[]
            {
                new CiselnikItem("P", "Cestovní pas"),
                new CiselnikItem("OP", "Občanský průkaz"),
                new CiselnikItem("RP", "Povolení k pobytu"),
            };
            return Ok(items, zadostId, "OK", popis: "CiselnikDruhDokladu");
        });

        // ----- KRPZS (poskytovatelé) -----
        var krpzs = api.MapGroup("/krpzs");

        // GET /api/v1/krpzs/hledat/{zadostId}/ico?ico=12345678
        krpzs.MapGet("/hledat/{zadostId:guid}/ico", (Guid zadostId, string ico, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).Concat(ValidateIco(ico)).ToList();
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var p = Providers.FirstOrDefault(x => x.Ico == ico);
            if (p == null) return NotFound($"Poskytovatel s IČO {ico} nenalezen.", zadostId);

            return Ok(p, zadostId, "OK", popis: "KRPZS GET ICO");
        });

        // POST /api/v1/krpzs/reklamace/{zadostId}
        krpzs.MapPost("/reklamace/{zadostId:guid}", (Guid zadostId, ReklamaceBody body) =>
        {
            var errs = ValidateCommon(zadostId, body.ZadostInfo?.Ucel, body.ZadostInfo?.Datum).ToList();
            if (body.ZadostData is null) errs.Add("ZadostData je povinné.");
            if (body.ZadostData is { PolozkyReklamace: null or { Count: 0 } }) errs.Add("PolozkyReklamace musí obsahovat alespoň jednu položku.");

            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            return Created(new { prijato = true, registr = "KRPZS" }, zadostId, popis: "Reklamace přijata");
        });

        // Notifikace KRPZS
        MapNotificationEndpoints(krpzs);

        // ----- KRZP (zdravotničtí pracovníci) -----
        var krzp = api.MapGroup("/krzp");

        // GET /api/v1/krzp/hledat/{zadostId}/krzpid?id=100001
        krzp.MapGet("/hledat/{zadostId:guid}/krzpid", (Guid zadostId, long id, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (id <= 0) errs.Add("Parametr id musí být kladné číslo.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var w = Workers.FirstOrDefault(x => x.KrzpId == id);
            if (w == null) return NotFound($"Pracovník s KRZP ID {id} nenalezen.", zadostId);

            return Ok(w, zadostId, "OK", popis: "KRZP VyhledejPodleKRZPID");
        });

        // GET /api/v1/krzp/hledat/{zadostId}/jmeno_prijmeni_datum_narozeni
        krzp.MapGet("/hledat/{zadostId:guid}/jmeno_prijmeni_datum_narozeni",
            (Guid zadostId, string jmeno, string prijmeni, DateOnly datumNarozeni, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(jmeno)) errs.Add("jmeno je povinné.");
            if (string.IsNullOrWhiteSpace(prijmeni)) errs.Add("prijmeni je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var res = Workers.Where(x =>
                x.Jmeno.Equals(jmeno, StringComparison.OrdinalIgnoreCase) &&
                x.Prijmeni.Equals(prijmeni, StringComparison.OrdinalIgnoreCase) &&
                x.DatumNarozeni == datumNarozeni).ToList();

            if (res.Count == 0) return NotFound("Pracovník nenalezen.", zadostId);
            return Ok(res, zadostId, "OK", popis: "KRZP VyhledejPodleJmenoPrijmeniDatumNarozeni");
        });

        // GET /api/v1/krzp/hledat/{zadostId}/zamestnavatel?ico=12345678
        krzp.MapGet("/hledat/{zadostId:guid}/zamestnavatel",
            (Guid zadostId, string ico, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).Concat(ValidateIco(ico)).ToList();
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var res = Workers.Where(x => x.ZamestnavatelIco == ico).ToList();
            if (res.Count == 0) return NotFound($"Žádní pracovníci pro IČO {ico}.", zadostId);
            return Ok(res, zadostId, "OK", popis: "KRZP VyhledejPodleZamestnavatele");
        });

        // POST /api/v1/krzp/reklamace/{zadostId}
        krzp.MapPost("/reklamace/{zadostId:guid}", (Guid zadostId, ReklamaceBody body) =>
        {
            var errs = ValidateCommon(zadostId, body.ZadostInfo?.Ucel, body.ZadostInfo?.Datum).ToList();
            if (body.ZadostData is null) errs.Add("ZadostData je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            return Created(new { prijato = true, registr = "KRZP" }, zadostId, popis: "Reklamace přijata");
        });

        // Notifikace KRZP
        MapNotificationEndpoints(krzp);
    }

    private static void MapNotificationEndpoints(RouteGroupBuilder group)
    {
        // POST /notifikace/{zadostId}
        group.MapPost("/notifikace/{zadostId:guid}", (Guid zadostId, CreateNotification req) =>
        {
            var errs = ValidateCommon(zadostId, req.ZadostInfo?.Ucel, req.ZadostInfo?.Datum).ToList();
            if (req.ZadostData is null) errs.Add("ZadostData je povinné.");
            if (req.ZadostData is { System: null or "", Typ: null or "" }) errs.Add("ZadostData.System a ZadostData.Typ jsou povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var id = Guid.NewGuid();
            var n = new Notification
            {
                Id = id,
                System = req.ZadostData!.System!,
                Typ = req.ZadostData!.Typ!,
                Kriteria = req.ZadostData!.Kriteria,
                Kanal = req.ZadostData!.Kanal ?? "internal",
                Vytvoreno = DateTime.UtcNow,
                Stav = "aktivni"
            };
            Notifications[id] = n;
            return Created(n, zadostId, popis: "ZalozOdberNotifikaci");
        });

        // PUT /notifikace/{zadostId}/{id}
        group.MapPut("/notifikace/{zadostId:guid}/{id:guid}", (Guid zadostId, Guid id) =>
        {
            if (!Notifications.TryGetValue(id, out var n))
                return NotFound("Registrace nenalezena.", zadostId);

            n.Stav = "zruseno";
            return Ok(n, zadostId, "OK", popis: "ZrusOdberNotifikaci");
        });

        // GET /notifikace/{zadostId}?id=...&system=KRZP
        group.MapGet("/notifikace/{zadostId:guid}", (Guid zadostId, Guid? id, string? system) =>
        {
            if (id.HasValue)
            {
                if (!Notifications.TryGetValue(id.Value, out var n))
                    return NotFound("Registrace nenalezena.", zadostId);
                return Ok(new[] { n }, zadostId, "OK", popis: "VyhledejOdberNotifikaciPZS");
            }

            var q = Notifications.Values.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(system))
                q = q.Where(x => string.Equals(x.System, system, StringComparison.OrdinalIgnoreCase));

            return Ok(q.ToArray(), zadostId, "OK", popis: "VyhledejOdberNotifikaciPZS");
        });
    }

    // ---------- Helpers for uniform responses ----------
    private static IResult Ok<T>(T data, Guid zadostId, string stav = "OK", string? subStav = null, string? popis = null)
        => Results.Ok(new KzrResponse<T>
        {
            OdpovedInfo = new KzrOdpoved
            {
                ZadostId = zadostId,
                OdpovedId = Guid.NewGuid(),
                Stav = stav,
                SubStav = subStav,
                Popis = popis
            },
            OdpovedData = data
        });

    private static IResult Created<T>(T data, Guid zadostId, string? popis = null)
        => Results.Json(new KzrResponse<T>
        {
            OdpovedInfo = new KzrOdpoved
            {
                ZadostId = zadostId,
                OdpovedId = Guid.NewGuid(),
                Stav = "OK",
                Popis = popis
            },
            OdpovedData = data
        }, statusCode: StatusCodes.Status201Created);

    private static IResult NotFound(string msg, Guid zadostId)
        => Results.Json(new KzrResponse<object?>
        {
            OdpovedInfo = new KzrOdpoved
            {
                ZadostId = zadostId,
                OdpovedId = Guid.NewGuid(),
                Stav = "Chyba",
                SubStav = "NotFound",
                Popis = msg,
                ChybyZpracovani = [msg]
            },
            OdpovedData = null
        }, statusCode: StatusCodes.Status404NotFound);

    private static IResult Bad(IEnumerable<string> errs, Guid zadostId, string? subStav, int http)
        => Results.Json(new KzrResponse<object?>
        {
            OdpovedInfo = new KzrOdpoved
            {
                ZadostId = zadostId,
                OdpovedId = Guid.NewGuid(),
                Stav = "Chyba",
                SubStav = subStav,
                Popis = "Vstupní data neprošla validací.",
                ChybyZpracovani = errs.ToList()
            },
            OdpovedData = null
        }, statusCode: http);

    private static IEnumerable<string> ValidateCommon(Guid zadostId, string? ucel, DateTime? datum)
    {
        if (zadostId == Guid.Empty) yield return "ZadostId nesmí být prázdné.";
        if (string.IsNullOrWhiteSpace(ucel)) yield return "Ucel je povinný.";
        if (datum is null) yield return "Datum je povinné.";
    }

    private static IEnumerable<string> ValidateIco(string ico)
    {
        if (string.IsNullOrWhiteSpace(ico)) yield return "IČO je povinné.";
        else if (ico.Length != 8 || !ico.All(char.IsDigit)) yield return "IČO musí mít 8 číslic.";
    }

    // ---------- DTOs ----------
    public sealed class KzrDotaz
    {
        public Guid ZadostId { get; set; }
        public string Ucel { get; set; } = default!;
        public DateTime Datum { get; set; }
    }

    public sealed class KzrOdpoved
    {
        public Guid ZadostId { get; set; }
        public Guid OdpovedId { get; set; }
        public string Stav { get; set; } = default!; // OK | Varovani | Chyba
        public string? SubStav { get; set; }
        public string? Popis { get; set; }
        public List<string>? ChybyZpracovani { get; set; }
    }

    public class KzrRequest<T>
    {
        public KzrDotaz? ZadostInfo { get; set; }
        public T? ZadostData { get; set; }
    }

    public sealed class KzrResponse<T>
    {
        public KzrOdpoved OdpovedInfo { get; set; } = default!;
        public T? OdpovedData { get; set; }
    }

    public sealed class ProviderDto
    {
        public string Ico { get; set; } = default!;
        public string Nazev { get; set; } = default!;
        public List<Adresa> MistaPoskytovani { get; set; } = [];
    }

    public sealed class WorkerDto
    {
        public long KrzpId { get; set; }
        public string Jmeno { get; set; } = default!;
        public string Prijmeni { get; set; } = default!;
        public DateOnly DatumNarozeni { get; set; }
        public string ZamestnavatelIco { get; set; } = default!;
        public string? Odbornost { get; set; }
    }

    public sealed class Adresa
    {
        public string? Ulice { get; set; }
        public string? CisloPopisne { get; set; }
        public string? Obec { get; set; }
        public string? Psc { get; set; }
        public string? Stat { get; set; }
    }

    public sealed record CiselnikItem(string Kod, string Popis);

    // Reklamace
    public sealed class ReklamaceBody : KzrRequest<UdajReklamaceBulk> { }
    public sealed class UdajReklamaceBulk
    {
        public long? Krpzsid { get; set; } // nebo KRZPId; flexibilně pro mock
        public string? UlozkaId { get; set; }
        public int? UlozkaRef { get; set; }
        public DateTime? DatumReklamace { get; set; }
        public Reklamujici? Reklamujici { get; set; }
        public List<UdajReklamace>? PolozkyReklamace { get; set; }
        public string? Zduvodneni { get; set; }
        public string PopisReklamace { get; set; } = default!;
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

    // Notifikace
    public sealed class CreateNotification : KzrRequest<NotificationRequest> { }
    public sealed class NotificationRequest
    {
        public string? System { get; set; } // např. "KRZP" / "KRPZS"
        public string? Typ { get; set; }    // identifikátor tématu
        public string? Kriteria { get; set; } // např. "ico=12345678"
        public string? Kanal { get; set; }  // např. "webhook"
    }
    public sealed class Notification
    {
        public Guid Id { get; set; }
        public string System { get; set; } = default!;
        public string Typ { get; set; } = default!;
        public string? Kriteria { get; set; }
        public string Kanal { get; set; } = default!;
        public DateTime Vytvoreno { get; set; }
        public string Stav { get; set; } = "aktivni";
    }
}