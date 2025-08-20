// Program.cs
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EzKzr.MockServer;

public static class Program
{
    private const string FhirJson = "application/fhir+json";

    // --- In‑memory datové zdroje ---
    private static List<ProviderDto> Providers => Db.Providers;

    private static List<WorkerDto> Workers => Db.Workers;

    private static readonly ConcurrentDictionary<Guid, Notification> Notifications = new();

    // --- Patient Summary seed ---
    private static List<PatientSummary> PatientSummaries => Db.PatientSummaries;

    // --- HDR seed ---
    private static List<DischargeReport> DischargeReports => Db.DischargeReports;

    // --- LAB výsledky seed (sjednocený model) ---
    private static List<LabReport> LabReports => Db.LabReports;

    // --- MI zprávy seed (sjednocený model) ---
    private static List<ImagingReport> ImagingReports => Db.ImagingReports;

    // --- Žádanky seed (sjednocené List<...>) ---
    private static List<LabOrder> LabOrders => Db.LabOrders;

    private static List<ImagingOrder> ImagingOrders => Db.ImagingOrders;

    // --- EMS seed ---
    private static List<EmsRun> EmsRuns => Db.EmsRuns;

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

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

        // ----- ČÍSELNÍKY -----
        var ciselnik = api.MapGroup("/ciselnik/{zadostId:guid}");

        ciselnik.MapGet("/stat", (Guid zadostId, string? ucel, DateTime? datum) =>
        {
            var items = new[] { new CiselnikItem("CZ", "Česká republika"), new CiselnikItem("SK", "Slovensko"), new CiselnikItem("DE", "Německo") };
            return Ok(items, zadostId, "OK", popis: "CiselnikStat");
        });

        ciselnik.MapGet("/pohlavi", (Guid zadostId, string? ucel, DateTime? datum) =>
        {
            var items = new[] { new CiselnikItem("M", "Muž"), new CiselnikItem("Z", "Žena"), new CiselnikItem("X", "Neurčeno") };
            return Ok(items, zadostId, "OK", popis: "CiselnikPohlavi");
        });

        ciselnik.MapGet("/zdravotni_pojistovna", (Guid zadostId, string? ucel, DateTime? datum) =>
        {
            var items = new[] { new CiselnikItem("111", "VZP"), new CiselnikItem("201", "VoZP"), new CiselnikItem("205", "ČPZP"), new CiselnikItem("207", "OZP"), new CiselnikItem("209", "ZPŠ"), new CiselnikItem("211", "ZPMV") };
            return Ok(items, zadostId, "OK", popis: "CiselnikZdravotniPojistovna");
        });

        ciselnik.MapGet("/druh_dokladu", (Guid zadostId, string? ucel, DateTime? datum) =>
        {
            var items = new[] { new CiselnikItem("P", "Cestovní pas"), new CiselnikItem("OP", "Občanský průkaz"), new CiselnikItem("RP", "Povolení k pobytu") };
            return Ok(items, zadostId, "OK", popis: "CiselnikDruhDokladu");
        });

        // ----- KRPZS -----
        var krpzs = api.MapGroup("/krpzs");

        krpzs.MapGet("/hledat/{zadostId:guid}/ico", (Guid zadostId, string ico, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).Concat(ValidateIco(ico)).ToList();
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var p = Providers.FirstOrDefault(x => x.Ico == ico);
            if (p == null) return NotFound($"Poskytovatel s IČO {ico} nenalezen.", zadostId);
            return Ok(p, zadostId, "OK", popis: "KRPZS GET ICO");
        });

        krpzs.MapPost("/reklamace/{zadostId:guid}", (Guid zadostId, ReklamaceBody body) =>
        {
            var errs = ValidateCommon(zadostId, body.ZadostInfo?.Ucel, body.ZadostInfo?.Datum).ToList();
            if (body.ZadostData is null) errs.Add("ZadostData je povinné.");
            if (body.ZadostData is { PolozkyReklamace: null or { Count: 0 } }) errs.Add("PolozkyReklamace musí obsahovat alespoň jednu položku.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);
            return Created(new { prijato = true, registr = "KRPZS" }, zadostId, popis: "Reklamace přijata");
        });

        MapNotificationEndpoints(krpzs);

        // ----- SAMPLES -----
        var samples = api.MapGroup("/samples");
        samples.MapGet("/", () =>
        {
            var exZadostId = "11111111-1111-1111-1111-111111111111";
            var exDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            return Results.Ok(new
            {
                note = "Ukázková data a příklady volání.",
                providers = Providers.Select(p => new { p.Ico, p.Nazev }).ToArray(),
                workers = Workers.Select(w => new { w.KrzpId, w.Jmeno, w.Prijmeni, narozeni = w.DatumNarozeni }).ToArray(),
                patientRids = PatientSummaries.Select(p => new { p.Header.Rid, p.Header.GivenName, p.Header.FamilyName }).ToArray(),
                examples = new
                {
                    ciselnik_stat = $"/api/v1/ciselnik/{exZadostId}/stat?ucel=Test&datum={exDate}",
                    ciselnik_pohlavi = $"/api/v1/ciselnik/{exZadostId}/pohlavi?ucel=Test&datum={exDate}",
                    ciselnik_pojistovna = $"/api/v1/ciselnik/{exZadostId}/zdravotni_pojistovna?ucel=Test&datum={exDate}",

                    krpzs_get_ico = $"/api/v1/krpzs/hledat/{exZadostId}/ico?ico={Providers[0].Ico}&ucel=Test&datum={exDate}",

                    krzp_get_id = $"/api/v1/krzp/hledat/{exZadostId}/krzpid?id={Workers[0].KrzpId}&ucel=Test&datum={exDate}",
                    krzp_get_osoba = $"/api/v1/krzp/hledat/{exZadostId}/jmeno_prijmeni_datum_narozeni?jmeno={Uri.EscapeDataString(Workers[0].Jmeno)}&prijmeni={Uri.EscapeDataString(Workers[0].Prijmeni)}&datumNarozeni={Workers[0].DatumNarozeni:yyyy-MM-dd}&ucel=Test&datum={exDate}",
                    krzp_get_zamestnavatel = $"/api/v1/krzp/hledat/{exZadostId}/zamestnavatel?ico={Workers[0].ZamestnavatelIco}&ucel=Test&datum={exDate}",

                    ps_by_rid = $"/api/v1/ps/rid/{exZadostId}?rid={PatientSummaries[0].Header.Rid}&ucel=Test&datum={exDate}",
                    ps_by_osoba = $"/api/v1/ps/osoba/{exZadostId}?jmeno={Uri.EscapeDataString(PatientSummaries[0].Header.GivenName)}&prijmeni={Uri.EscapeDataString(PatientSummaries[0].Header.FamilyName)}&datumNarozeni={PatientSummaries[0].Header.DateOfBirth:yyyy-MM-dd}&ucel=Test&datum={exDate}",

                    hdr_by_rid = $"/api/v1/hdr/rid/{exZadostId}?rid={DischargeReports[0].Header.Rid}&ucel=Test&datum={exDate}",

                    fhir_metadata = "/fhir/metadata",
                    fhir_patient = $"/fhir/Patient/{PatientSummaries[0].Header.Rid}",
                    fhir_summary = $"/fhir/Patient/{PatientSummaries[0].Header.Rid}/$summary",
                    fhir_document = $"/fhir/Bundle/{DischargeReports[0].Header.Rid}/$discharge",

                    lab_by_rid = $"/api/v1/lab/rid/{exZadostId}?rid={PatientSummaries[0].Header.Rid}&ucel=Test&datum={exDate}",
                    mi_by_rid  = $"/api/v1/mi/rid/{exZadostId}?rid={PatientSummaries[0].Header.Rid}&ucel=Test&datum={exDate}",
                    ezadanka_lab_post = $"/api/v1/lab/order/{exZadostId}",
                    ezadanka_mi_post  = $"/api/v1/mi/order/{exZadostId}"
                }
            });
        });

        samples.MapGet("/providers", () => Results.Ok(Providers));
        samples.MapGet("/workers", () => Results.Ok(Workers));
        samples.MapGet("/ps", () => Results.Ok(PatientSummaries));
        samples.MapGet("/hdr", () => Results.Ok(DischargeReports));
        samples.MapGet("/lab", () => Results.Ok(LabReports));
        samples.MapGet("/mi",  () => Results.Ok(ImagingReports));
        samples.MapGet("/ems", () => Results.Ok(EmsRuns));

        samples.MapGet("/body/reklamace", () =>
        {
            var body = new ReklamaceBody
            {
                ZadostInfo = new KzrDotaz { Ucel = "Test", Datum = DateTime.UtcNow },
                ZadostData = new UdajReklamaceBulk
                {
                    Krpzsid = 123456789,
                    UlozkaId = "ORG001",
                    UlozkaRef = 42,
                    DatumReklamace = DateTime.UtcNow.Date,
                    Reklamujici = new Reklamujici { Ico = Providers[0].Ico, Nazev = Providers[0].Nazev, KontaktEmail = "it@example.org" },
                    PolozkyReklamace = [ new UdajReklamace { Klic = "Nazev", PuvodniHodnota = "Nemocnice Alfa, a.s.", PozadovanaHodnota = "Nemocnice ALFA a.s." } ],
                    Zduvodneni = "Oprava údajů v registru",
                    PopisReklamace = "Formální úprava názvu"
                }
            };
            return Results.Ok(body);
        });

        samples.MapGet("/body/notifikace", () =>
        {
            var req = new CreateNotification
            {
                ZadostInfo = new KzrDotaz { Ucel = "Test", Datum = DateTime.UtcNow },
                ZadostData = new NotificationRequest { System = "KRPZS", Typ = "zmena-pzs", Kriteria = $"ico={Providers[0].Ico}", Kanal = "webhook" }
            };
            return Results.Ok(req);
        });

        samples.MapGet("/body/lab-report", () =>
        {
            var sample = new CreateLabReport
            {
                ZadostInfo = new KzrDotaz { Ucel = "Test", Datum = DateTime.UtcNow },
                ZadostData = new LabReport
                {
                    Header = new LabHeader
                    {
                        Rid = PatientSummaries[0].Header.Rid,
                        Issued = DateTime.UtcNow,
                        Laboratory = "Ukázková laboratoř",
                        OrderId = LabOrders[0].Id.ToString()
                    },
                    Results = [new LabResult { Code = "718-7", Text = "Hemoglobin", Value = "140", Unit = "g/L", ReferenceRange = "135-175", AbnormalFlag = "N" }]
                }
            };
            return Results.Ok(sample);
        });

        samples.MapGet("/body/lab-order", () =>
        {
            var sample = new CreateLabOrder
            {
                ZadostInfo = new KzrDotaz { Ucel = "Test", Datum = DateTime.UtcNow },
                ZadostData = new LabOrder
                {
                    Id = Guid.NewGuid(),
                    Rid = PatientSummaries[0].Header.Rid,
                    Created = DateTime.UtcNow,
                    Tests = ["Glukóza", "Hemoglobin"],
                    RequesterIco = Providers[0].Ico,
                    RequesterName = Providers[0].Nazev,
                    Status = "new"
                }
            };
            return Results.Ok(sample);
        });

        samples.MapGet("/body/imaging-report", () =>
        {
            var sample = new CreateImagingReport
            {
                ZadostInfo = new KzrDotaz { Ucel = "Test", Datum = DateTime.UtcNow },
                ZadostData = new ImagingReport
                {
                    Header = new ImagingHeader
                    {
                        Rid = PatientSummaries[0].Header.Rid,
                        Performed = DateTime.UtcNow,
                        Modality = "US",
                        Performer = "MUDr. Tester",
                        FacilityName = Providers[0].Nazev
                    },
                    Indication = "Kontrolní vyšetření",
                    Findings = "Bez patrné patologie.",
                    Conclusion = "Nález v normě."
                }
            };
            return Results.Ok(sample);
        });

        samples.MapGet("/body/imaging-order", () =>
        {
            var sample = new CreateImagingOrder
            {
                ZadostInfo = new KzrDotaz { Ucel = "Test", Datum = DateTime.UtcNow },
                ZadostData = new ImagingOrder
                {
                    Id = Guid.NewGuid(),
                    Rid = PatientSummaries[0].Header.Rid,
                    Created = DateTime.UtcNow,
                    RequestedModality = "CT",
                    RequestedProcedure = "CT hrudníku",
                    ClinicalInfo = "Kontrola po terapii",
                    RequesterIco = Providers[0].Ico,
                    RequesterName = Providers[0].Nazev,
                    Status = "new"
                }
            };
            return Results.Ok(sample);
        });

        samples.MapGet("/body/ems-record", () =>
        {
            var sample = new CreateEmsRecord
            {
                ZadostInfo = new KzrDotaz { Ucel = "Test", Datum = DateTime.UtcNow },
                ZadostData = new EmsRun
                {
                    Id = Guid.NewGuid(),
                    Rid = PatientSummaries[0].Header.Rid,
                    Started = DateTime.UtcNow,
                    Reason = "Bolest na hrudi",
                    Vitals = new Vitals { Systolic = 150, Diastolic = 90, HeartRate = 100, Spo2 = 95, Temperature = 36.8m },
                    Interventions = ["ASA 500 mg", "Monitoring"],
                    Outcome = "Převoz",
                    Destination = Providers[0].Nazev
                }
            };
            return Results.Ok(sample);
        });

        // ----- Patient Summary -----
        var ps = api.MapGroup("/ps");
        ps.MapGet("/rid/{zadostId:guid}", (Guid zadostId, string rid, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(rid)) errs.Add("rid je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);
            var psu = PatientSummaries.FirstOrDefault(x => x.Header.Rid == rid);
            if (psu is null) return NotFound($"Pacient s RID {rid} nenalezen.", zadostId);
            return Ok(psu, zadostId, "OK", popis: "PatientSummary");
        });

        ps.MapGet("/osoba/{zadostId:guid}", (Guid zadostId, string jmeno, string prijmeni, DateOnly datumNarozeni, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(jmeno)) errs.Add("jmeno je povinné.");
            if (string.IsNullOrWhiteSpace(prijmeni)) errs.Add("prijmeni je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var psu = PatientSummaries.FirstOrDefault(x =>
                string.Equals(x.Header.GivenName, jmeno, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Header.FamilyName, prijmeni, StringComparison.OrdinalIgnoreCase) &&
                x.Header.DateOfBirth == datumNarozeni);

            if (psu is null) return NotFound("Pacient nenalezen.", zadostId);
            return Ok(psu, zadostId, "OK", popis: "PatientSummary");
        });

        // ----- HDR -----
        var hdr = api.MapGroup("/hdr");
        hdr.MapGet("/rid/{zadostId:guid}", (Guid zadostId, string rid, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(rid)) errs.Add("rid je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var rep = FindHdr(rid);
            if (rep is null) return NotFound($"Propouštěcí zpráva pro RID {rid} nenalezena.", zadostId);
            return Ok(rep, zadostId, "OK", popis: "HDR");
        });

        hdr.MapGet("/osoba/{zadostId:guid}", (Guid zadostId, string jmeno, string prijmeni, DateOnly datumNarozeni, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(jmeno)) errs.Add("jmeno je povinné.");
            if (string.IsNullOrWhiteSpace(prijmeni)) errs.Add("prijmeni je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var psu = PatientSummaries.FirstOrDefault(x =>
                string.Equals(x.Header.GivenName, jmeno, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Header.FamilyName, prijmeni, StringComparison.OrdinalIgnoreCase) &&
                x.Header.DateOfBirth == datumNarozeni);

            if (psu is null) return NotFound("Pacient nenalezen.", zadostId);

            var rep = FindHdr(psu.Header.Rid);
            if (rep is null) return NotFound("Propouštěcí zpráva nenalezena.", zadostId);
            return Ok(rep, zadostId, "OK", popis: "HDR");
        });

        // ----- LAB -----
        var lab = api.MapGroup("/lab");

        lab.MapGet("/rid/{zadostId:guid}", (Guid zadostId, string rid, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(rid)) errs.Add("rid je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var reps = LabReports.Where(r => r.Header.Rid == rid).ToList();
            if (reps.Count == 0) return NotFound($"Lab výsledky pro RID {rid} nenalezeny.", zadostId);
            return Ok(reps, zadostId, "OK", popis: "LAB Report");
        });

        lab.MapPost("/report/{zadostId:guid}", (Guid zadostId, CreateLabReport body) =>
        {
            var errs = ValidateCommon(zadostId, body.ZadostInfo?.Ucel, body.ZadostInfo?.Datum).ToList();
            if (body.ZadostData is null) errs.Add("ZadostData je povinné.");
            else
            {
                if (body.ZadostData.Header is null || string.IsNullOrWhiteSpace(body.ZadostData.Header.Rid)) errs.Add("Header.Rid je povinné.");
                if (body.ZadostData.Results is null || body.ZadostData.Results.Count == 0) errs.Add("Results musí obsahovat alespoň jednu položku.");
            }
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            LabReports.Add(body.ZadostData!);
                        Db.SaveLabReports(LabReports);
return Created(new { prijato = true, registr = "LAB", orderId = body.ZadostData!.Header.OrderId }, zadostId, popis: "LabReport přijata");
        });

        lab.MapPost("/order/{zadostId:guid}", (Guid zadostId, CreateLabOrder body) =>
        {
            var errs = ValidateCommon(zadostId, body.ZadostInfo?.Ucel, body.ZadostInfo?.Datum).ToList();
            if (body.ZadostData is null) errs.Add("ZadostData je povinné.");
            else
            {
                if (string.IsNullOrWhiteSpace(body.ZadostData.Rid)) errs.Add("Rid je povinné.");
                if (body.ZadostData.Tests is null || body.ZadostData.Tests.Count == 0) errs.Add("Tests musí obsahovat alespoň jednu položku.");
            }
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            if (body.ZadostData!.Id == Guid.Empty) body.ZadostData.Id = Guid.NewGuid();
            body.ZadostData.Status = "received";
            LabOrders.Add(body.ZadostData);
                        Db.SaveLabOrders(LabOrders);
return Created(body.ZadostData, zadostId, popis: "LabOrder přijata");
        });

        lab.MapGet("/order/{zadostId:guid}", (Guid zadostId, Guid id, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);
            var o = LabOrders.FirstOrDefault(x => x.Id == id);
            if (o is null) return NotFound("Laboratorní žádanka nenalezena.", zadostId);
            return Ok(o, zadostId, "OK", popis: "LabOrder");
        });

        lab.MapGet("/result_by_order/{zadostId:guid}", (Guid zadostId, Guid id, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);
            var list = LabReports.Where(x => string.Equals(x.Header.OrderId, id.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (list.Count == 0) return NotFound("Žádné výsledky pro zadanou žádanku.", zadostId);
            return Ok(list, zadostId, "OK", popis: "LAB");
        });

        // ----- MI -----
        var mi = api.MapGroup("/mi");

        mi.MapGet("/rid/{zadostId:guid}", (Guid zadostId, string rid, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(rid)) errs.Add("rid je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var reps = ImagingReports.Where(r => r.Header.Rid == rid).ToList();
            if (reps.Count == 0) return NotFound($"Zpráva z obrazového vyšetření pro RID {rid} nenalezena.", zadostId);
            return Ok(reps, zadostId, "OK", popis: "MI Report");
        });

        mi.MapPost("/report/{zadostId:guid}", (Guid zadostId, CreateImagingReport body) =>
        {
            var errs = ValidateCommon(zadostId, body.ZadostInfo?.Ucel, body.ZadostInfo?.Datum).ToList();
            if (body.ZadostData is null) errs.Add("ZadostData je povinné.");
            else
            {
                if (body.ZadostData.Header is null || string.IsNullOrWhiteSpace(body.ZadostData.Header.Rid)) errs.Add("Header.Rid je povinné.");
                if (string.IsNullOrWhiteSpace(body.ZadostData.Findings)) errs.Add("Findings je povinné.");
                if (string.IsNullOrWhiteSpace(body.ZadostData.Conclusion)) errs.Add("Conclusion je povinné.");
            }
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            ImagingReports.Add(body.ZadostData!);
                        Db.SaveImagingReports(ImagingReports);
return Created(new { prijato = true, registr = "MI" }, zadostId, popis: "ImagingReport přijata");
        });

        mi.MapPost("/order/{zadostId:guid}", (Guid zadostId, CreateImagingOrder body) =>
        {
            var errs = ValidateCommon(zadostId, body.ZadostInfo?.Ucel, body.ZadostInfo?.Datum).ToList();
            if (body.ZadostData is null) errs.Add("ZadostData je povinné.");
            else
            {
                if (string.IsNullOrWhiteSpace(body.ZadostData.Rid)) errs.Add("Rid je povinné.");
                if (string.IsNullOrWhiteSpace(body.ZadostData.RequestedModality)) errs.Add("RequestedModality je povinné.");
            }
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            if (body.ZadostData!.Id == Guid.Empty) body.ZadostData.Id = Guid.NewGuid();
            body.ZadostData.Status = "received";
            ImagingOrders.Add(body.ZadostData);
                        Db.SaveImagingOrders(ImagingOrders);
return Created(body.ZadostData, zadostId, popis: "ImagingOrder přijata");
        });

        mi.MapGet("/order/{zadostId:guid}", (Guid zadostId, Guid id, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);
            var o = ImagingOrders.FirstOrDefault(x => x.Id == id);
            if (o is null) return NotFound("Obrazová žádanka nenalezena.", zadostId);
            return Ok(o, zadostId, "OK", popis: "ImagingOrder");
        });

        // ----- EMS -----
        var ems = api.MapGroup("/ems");

        ems.MapGet("/rid/{zadostId:guid}", (Guid zadostId, string rid, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(rid)) errs.Add("rid je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var list = EmsRuns.Where(x => x.Rid == rid).ToList();
            if (list.Count == 0) return NotFound($"Záznam o výjezdu pro RID {rid} nenalezen.", zadostId);
            return Ok(list, zadostId, "OK", popis: "EMS");
        });

        ems.MapPost("/record/{zadostId:guid}", (Guid zadostId, CreateEmsRecord body) =>
        {
            var errs = ValidateCommon(zadostId, body.ZadostInfo?.Ucel, body.ZadostInfo?.Datum).ToList();
            if (body.ZadostData is null) errs.Add("ZadostData je povinné.");
            else
            {
                if (string.IsNullOrWhiteSpace(body.ZadostData.Rid)) errs.Add("Rid je povinné.");
                if (body.ZadostData.Started == default) errs.Add("Started je povinné.");
            }
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            if (body.ZadostData!.Id == Guid.Empty) body.ZadostData.Id = Guid.NewGuid();
            EmsRuns.Add(body.ZadostData);
                        Db.SaveEmsRuns(EmsRuns);
return Created(body.ZadostData, zadostId, popis: "EMS Record přijat");
        });

        // ----- KRZP -----
        var krzp = api.MapGroup("/krzp");

        krzp.MapGet("/hledat/{zadostId:guid}/krzpid", (Guid zadostId, long id, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (id <= 0) errs.Add("Parametr id musí být kladné číslo.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var w = Workers.FirstOrDefault(x => x.KrzpId == id);
            if (w == null) return NotFound($"Pracovník s KRZP ID {id} nenalezen.", zadostId);
            return Ok(w, zadostId, "OK", popis: "KRZP VyhledejPodleKRZPID");
        });

        krzp.MapGet("/hledat/{zadostId:guid}/jmeno_prijmeni_datum_narozeni", (Guid zadostId, string jmeno, string prijmeni, DateOnly datumNarozeni, string? ucel, DateTime? datum) =>
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

        krzp.MapGet("/hledat/{zadostId:guid}/zamestnavatel", (Guid zadostId, string ico, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).Concat(ValidateIco(ico)).ToList();
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var res = Workers.Where(x => x.ZamestnavatelIco == ico).ToList();
            if (res.Count == 0) return NotFound($"Žádní pracovníci pro IČO {ico}.", zadostId);
            return Ok(res, zadostId, "OK", popis: "KRZP VyhledejPodleZamestnavatele");
        });

        krzp.MapPost("/reklamace/{zadostId:guid}", (Guid zadostId, ReklamaceBody body) =>
        {
            var errs = ValidateCommon(zadostId, body.ZadostInfo?.Ucel, body.ZadostInfo?.Datum).ToList();
            if (body.ZadostData is null) errs.Add("ZadostData je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);
            return Created(new { prijato = true, registr = "KRZP" }, zadostId, popis: "Reklamace přijata");
        });

        MapNotificationEndpoints(krzp);

        
        // ----- KRP -----
        var krp = api.MapGroup("/krp");

        krp.MapPost("/rid/generate/{zadostId:guid}", (Guid zadostId, string givenName, string familyName, DateOnly dateOfBirth, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(givenName)) errs.Add("givenName je povinné.");
            if (string.IsNullOrWhiteSpace(familyName)) errs.Add("familyName je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);
            var rid = RidService.Generate(Db.Rids);
            var rec = new RidRecord { Rid = rid, GivenName = givenName, FamilyName = familyName, DateOfBirth = dateOfBirth, Created = DateTime.UtcNow };
            Db.Rids.Add(rec);
            Db.SaveRids(Db.Rids);
            return Ok(new { rid }, zadostId, "OK", popis: "RID vygenerován");
        });

        krp.MapGet("/rid/{zadostId:guid}", (Guid zadostId, string rid, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(rid)) errs.Add("rid je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);
            var rec = Db.Rids.FirstOrDefault(x => x.Rid == rid);
            if (rec is null) return NotFound("RID nenalezen.", zadostId);
            return Ok(rec, zadostId, "OK", popis: "KRP RID");
        });

// ----- FHIR -----
        MapFhirRoutes(app);
    }

    private static void MapNotificationEndpoints(RouteGroupBuilder group)
    {
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
                        Db.SaveNotifications(Notifications.Values);
return Created(n, zadostId, popis: "ZalozOdberNotifikaci");
        });

        group.MapPut("/notifikace/{zadostId:guid}/{id:guid}", (Guid zadostId, Guid id) =>
        {
            if (!Notifications.TryGetValue(id, out var n))
                return NotFound("Registrace nenalezena.", zadostId);

            n.Stav = "zruseno";
                        Db.SaveNotifications(Notifications.Values);
return Ok(n, zadostId, "OK", popis: "ZrusOdberNotifikaci");
        });

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

    // ---------- FHIR ----------
    private static void MapFhirRoutes(WebApplication app)
    {
        var fhir = app.MapGroup("/fhir");

        fhir.MapGet("/metadata", () =>
        {
            var meta = new
            {
                resourceType = "CapabilityStatement",
                status = "active",
                date = DateTime.UtcNow.ToString("o"),
                kind = "instance",
                format = new[] { "json", FhirJson },
                rest = new[] { new { mode = "server", resource = new object[]
                {
                    new { type = "Patient", interaction = new[] { new { code = "read" }, new { code = "search-type" } } },
                    new { type = "AllergyIntolerance", interaction = new[] { new { code = "search-type" } } },
                    new { type = "Condition", interaction = new[] { new { code = "search-type" } } },
                    new { type = "MedicationStatement", interaction = new[] { new { code = "search-type" } } },
                    new { type = "Immunization", interaction = new[] { new { code = "search-type" } } },
                    new { type = "Device", interaction = new[] { new { code = "search-type" } } },
                    new { type = "Composition", interaction = new[] { new { code = "search-type" } } },
                    new { type = "DocumentReference", interaction = new[] { new { code = "search-type" } } },
                    new { type = "Observation", interaction = new[] { new { code = "search-type" } } },
                    new { type = "DiagnosticReport", interaction = new[] { new { code = "search-type" } } }
                } } }
            };
            return Fhir(meta);
        }).Produces(StatusCodes.Status200OK, contentType: FhirJson);

        fhir.MapGet("/Patient/{rid}", (string rid) =>
        {
            var ps = FindPs(rid);
            return ps is null ? FhirNotFound($"Patient {rid} not found.") : Fhir(FhirPatient(ps));
        });

        fhir.MapGet("/AllergyIntolerance", (string patient) =>
        {
            var ps = FindPs(patient);
            if (ps is null) return FhirNotFound($"Patient {patient} not found.");
            var list = (ps.Body.Allergies ?? new()).Select((a, i) => FhirAllergy(a, ps.Header.Rid, i + 1)).ToList();
            return Fhir(ToSearchBundle(list));
        });

        fhir.MapGet("/Condition", (string patient) =>
        {
            var ps = FindPs(patient);
            if (ps is null) return FhirNotFound($"Patient {patient} not found.");
            var list = (ps.Body.Problems ?? new()).Select((p, i) => FhirCondition(p, ps.Header.Rid, i + 1)).ToList();
            return Fhir(ToSearchBundle(list));
        });

        fhir.MapGet("/MedicationStatement", (string patient) =>
        {
            var ps = FindPs(patient);
            if (ps is null) return FhirNotFound($"Patient {patient} not found.");
            var list = (ps.Body.Medications ?? new()).Select((m, i) => FhirMedicationStatement(m, ps.Header.Rid, i + 1)).ToList();
            return Fhir(ToSearchBundle(list));
        });

        fhir.MapGet("/Immunization", (string patient) =>
        {
            var ps = FindPs(patient);
            if (ps is null) return FhirNotFound($"Patient {patient} not found.");
            var list = (ps.Body.Vaccinations ?? new()).Select((v, i) => FhirImmunization(v, ps.Header.Rid, i + 1)).ToList();
            return Fhir(ToSearchBundle(list));
        });

        fhir.MapGet("/Device", (string patient) =>
        {
            var ps = FindPs(patient);
            if (ps is null) return FhirNotFound($"Patient {patient} not found.");
            var list = (ps.Body.Implants ?? new()).Select((d, i) => FhirDevice(d, ps.Header.Rid, i + 1)).ToList();
            return Fhir(ToSearchBundle(list));
        });

        fhir.MapGet("/Observation", (string patient, string? category) =>
        {
            if (!string.Equals(category, "laboratory", StringComparison.OrdinalIgnoreCase))
                return Fhir(ToSearchBundle(Array.Empty<object>()));
            var list = LabReports.Where(x => x.Header.Rid == patient)
                                 .SelectMany(r => r.Results ?? new())
                                 .Select((res, i) => FhirLabObservation(res, patient, i + 1))
                                 .ToList();
            return list.Count == 0 ? FhirNotFound($"No LAB observations for patient {patient}.") : Fhir(ToSearchBundle(list));
        });

        fhir.MapGet("/DiagnosticReport", (string patient, string? category) =>
        {
            if (string.Equals(category, "laboratory", StringComparison.OrdinalIgnoreCase))
            {
                var list = LabReports.Where(x => x.Header.Rid == patient).Select(r => FhirLabDiagnosticReport(r)).ToList();
                return list.Count == 0 ? FhirNotFound($"No LAB report for patient {patient}.") : Fhir(ToSearchBundle(list));
            }
            if (string.Equals(category, "imaging", StringComparison.OrdinalIgnoreCase))
            {
                var list = ImagingReports.Where(x => x.Header.Rid == patient).Select(r => FhirImagingDiagnosticReport(r)).ToList();
                return list.Count == 0 ? FhirNotFound($"No Imaging report for patient {patient}.") : Fhir(ToSearchBundle(list));
            }
            return Fhir(ToSearchBundle(Array.Empty<object>()));
        });

        fhir.MapGet("/Patient/{rid}/$summary", (string rid) =>
        {
            var ps = FindPs(rid);
            if (ps is null) return FhirNotFound($"Patient {rid} not found.");

            var resources = new List<object> { FhirPatient(ps) };
            resources.AddRange((ps.Body.Allergies ?? new()).Select((a, i) => FhirAllergy(a, rid, i + 1)));
            resources.AddRange((ps.Body.Problems ?? new()).Select((p, i) => FhirCondition(p, rid, i + 1)));
            resources.AddRange((ps.Body.Medications ?? new()).Select((m, i) => FhirMedicationStatement(m, rid, i + 1)));
            resources.AddRange((ps.Body.Vaccinations ?? new()).Select((v, i) => FhirImmunization(v, rid, i + 1)));
            resources.AddRange((ps.Body.Implants ?? new()).Select((d, i) => FhirDevice(d, rid, i + 1)));

            return Fhir(ToCollectionBundle(resources));
        });

        fhir.MapGet("/Composition", (string patient, string? type) =>
        {
            var dr = FindHdr(patient);
            if (dr is null) return FhirNotFound($"No HDR for patient {patient}.");
            var comp = FhirDischargeComposition(dr);
            return Fhir(ToSearchBundle(new[] { comp }));
        });

        fhir.MapGet("/DocumentReference", (string patient, string? type) =>
        {
            var dr = FindHdr(patient);
            if (dr is null) return FhirNotFound($"No HDR for patient {patient}.");
            var doc = new
            {
                resourceType = "DocumentReference",
                id = $"docref-hdr-{patient}",
                status = "current",
                type = new { coding = new[] { new { system = "http://loinc.org", code = "18842-5", display = "Discharge summary" } }, text = "Propouštěcí zpráva" },
                subject = new { reference = $"Patient/{patient}" },
                date = dr.Header.Discharge.ToString("o"),
                content = new[] { new { attachment = new { contentType = FhirJson, url = $"/fhir/Bundle/{patient}/$discharge" } } }
            };
            return Fhir(ToSearchBundle(new[] { doc }));
        });

        fhir.MapGet("/Bundle/{rid}/$discharge", (string rid) =>
        {
            var dr = FindHdr(rid);
            if (dr is null) return FhirNotFound($"No HDR for patient {rid}.");
            var composition = FhirDischargeComposition(dr);
            var ps = FindPs(rid);
            var patientRes = ps is null ? new { resourceType = "Patient", id = rid } : FhirPatient(ps);
            var doc = ToDocumentBundle(new[] { composition, patientRes });
            return Fhir(doc);
        });
    }

    private static PatientSummary? FindPs(string rid) => PatientSummaries.FirstOrDefault(x => x.Header.Rid == rid);
    private static DischargeReport? FindHdr(string rid) => DischargeReports.FirstOrDefault(x => x.Header.Rid == rid);

    // ---------- FHIR helpers ----------
    private static object FhirLabDiagnosticReport(LabReport r) => new
    {
        resourceType = "DiagnosticReport",
        id = $"labdr-{r.Header.Rid}-{r.Header.Issued:yyyyMMddHHmmss}",
        status = "final",
        category = new[] { new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/v2-0074", code = "LAB", display = "Laboratory" } } } },
        code = new { coding = new[] { new { system = "http://loinc.org", code = "11502-2", display = "Laboratory report" } }, text = "Laboratorní zpráva" },
        subject = new { reference = $"Patient/{r.Header.Rid}" },
        effectiveDateTime = r.Header.Issued.ToString("o"),
        issued = r.Header.Issued.ToString("o"),
        performer = new[] { new { display = r.Header.Laboratory } },
        result = (r.Results ?? new()).Select((_, i) => new { reference = $"Observation/labobs-{i + 1}-{r.Header.Rid}" })
    };

    private static object FhirLabObservation(LabResult res, string rid, int ix)
    {
        var s = (res.Value ?? "").Replace(",", ".");
        var numeric = decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var num);
        return new
        {
            resourceType = "Observation",
            id = $"labobs-{ix}-{rid}",
            status = "final",
            category = new[] { new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "laboratory", display = "Laboratory" } } } },
            code = new { text = res.Text, coding = string.IsNullOrWhiteSpace(res.Code) ? null : new[] { new { system = "http://loinc.org", code = res.Code!, display = res.Text } } },
            subject = new { reference = $"Patient/{rid}" },
            effectiveDateTime = DateTime.UtcNow.ToString("o"),
            valueQuantity = numeric ? new { value = (double)num, unit = res.Unit } : null,
            valueString = numeric ? null : res.Value,
            referenceRange = string.IsNullOrWhiteSpace(res.ReferenceRange) ? null : new[] { new { text = res.ReferenceRange } },
            interpretation = string.IsNullOrWhiteSpace(res.AbnormalFlag) ? null : new[] { new { text = res.AbnormalFlag } }
        };
    }

    private static object FhirImagingDiagnosticReport(ImagingReport r) => new
    {
        resourceType = "DiagnosticReport",
        id = $"imgdr-{r.Header.Rid}-{r.Header.Performed:yyyyMMddHHmmss}",
        status = "final",
        category = new[] { new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/v2-0074", code = "RAD", display = "Radiology" } } } },
        code = new { coding = new[] { new { system = "http://loinc.org", code = "18748-4", display = "Diagnostic imaging report" } }, text = "Zpráva z obrazového vyšetření" },
        subject = new { reference = $"Patient/{r.Header.Rid}" },
        effectiveDateTime = r.Header.Performed.ToString("o"),
        issued = r.Header.Performed.ToString("o"),
        performer = new[] { new { display = r.Header.FacilityName } },
        conclusion = r.Conclusion,
        presentedForm = new[] { new { contentType = "text/plain", data = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{r.Findings}\n\nZávěr: {r.Conclusion}")) } }
    };

    private static IResult Fhir(object payload, int statusCode = StatusCodes.Status200OK)
        => Results.Json(payload, contentType: FhirJson, statusCode: statusCode);

    private static IResult FhirNotFound(string msg) => Results.Json(new
    {
        resourceType = "OperationOutcome",
        issue = new[] { new { severity = "error", code = "not-found", diagnostics = msg } }
    }, contentType: FhirJson, statusCode: StatusCodes.Status404NotFound);

    private static object ToSearchBundle(IEnumerable<object> resources) => new
    {
        resourceType = "Bundle",
        type = "searchset",
        total = resources.Count(),
        entry = resources.Select(r => new { resource = r })
    };

    private static object ToCollectionBundle(IEnumerable<object> resources) => new
    {
        resourceType = "Bundle",
        type = "collection",
        total = resources.Count(),
        entry = resources.Select(r => new { resource = r })
    };

    private static object ToDocumentBundle(IEnumerable<object> resources) => new
    {
        resourceType = "Bundle",
        type = "document",
        timestamp = DateTime.UtcNow.ToString("o"),
        entry = resources.Select(r => new { resource = r })
    };

    private static object FhirPatient(PatientSummary ps) => new
    {
        resourceType = "Patient",
        id = ps.Header.Rid,
        identifier = new[] { new { system = "urn:oid:1.2.203.0.0.1.1", value = ps.Header.Rid } },
        name = new[] { new { family = ps.Header.FamilyName, given = new[] { ps.Header.GivenName } } },
        gender = ToFhirGender(ps.Header.Gender),
        birthDate = ps.Header.DateOfBirth.ToString("yyyy-MM-dd")
    };

    private static object FhirAllergy(Allergy a, string rid, int ix) => new
    {
        resourceType = "AllergyIntolerance",
        id = $"alg-{ix}-{rid}",
        clinicalStatus = new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical", code = "active" } } },
        verificationStatus = new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/allergyintolerance-verification", code = "confirmed" } } },
        code = new
        {
            text = a.Text,
            coding = (a.Code is not null && a.CodeSystem is not null)
                ? new[] { new { system = MapCodeSystem(a.CodeSystem), code = a.Code, display = a.Text } }
                : null
        },
        patient = new { reference = $"Patient/{rid}" },
        criticality = MapCriticality(a.Criticality)
    };

    private static object FhirCondition(Problem p, string rid, int ix) => new
    {
        resourceType = "Condition",
        id = $"cond-{ix}-{rid}",
        clinicalStatus = new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/condition-clinical", code = "active" } } },
        code = new
        {
            text = p.Text,
            coding = (p.Code is not null && p.CodeSystem is not null)
                ? new[] { new { system = MapCodeSystem(p.CodeSystem), code = p.Code, display = p.Text } }
                : null
        },
        subject = new { reference = $"Patient/{rid}" }
    };

    private static object FhirMedicationStatement(Medication m, string rid, int ix) => new
    {
        resourceType = "MedicationStatement",
        id = $"meds-{ix}-{rid}",
        status = "active",
        medicationCodeableConcept = new { text = m.Text },
        subject = new { reference = $"Patient/{rid}" },
        dosage = string.IsNullOrWhiteSpace(m.Dosage)
            ? null
            : new[] { new { text = m.Dosage, route = string.IsNullOrWhiteSpace(m.Route) ? null : new { text = m.Route } } }
    };

    private static object FhirImmunization(Vaccination v, string rid, int ix) => new
    {
        resourceType = "Immunization",
        id = $"imm-{ix}-{rid}",
        status = "completed",
        vaccineCode = new { text = v.Text },
        patient = new { reference = $"Patient/{rid}" },
        occurrenceDateTime = v.Date.HasValue ? v.Date.Value.ToString("yyyy-MM-dd") : null
    };

    private static object FhirDevice(Implant d, string rid, int ix) => new
    {
        resourceType = "Device",
        id = $"dev-{ix}-{rid}",
        type = new { text = d.Text },
        patient = new { reference = $"Patient/{rid}" }
    };

    private static object FhirDischargeComposition(DischargeReport dr) => new
    {
        resourceType = "Composition",
        id = $"comp-hdr-{dr.Header.Rid}",
        status = "final",
        type = new { coding = new[] { new { system = "http://loinc.org", code = "18842-5", display = "Discharge summary" } }, text = "Propouštěcí zpráva" },
        subject = new { reference = $"Patient/{dr.Header.Rid}" },
        date = dr.Header.Discharge.ToString("o"),
        title = "Propouštěcí zpráva",
        author = new[] { new { display = dr.Header.AttendingDoctor } },
        custodian = new { display = dr.Header.FacilityName },
        section = new object[]
        {
            new { title = "Důvod přijetí", text = new { status = "generated", div = $"<div xmlns=\"http://www.w3.org/1999/xhtml\"><p>{dr.ReasonForAdmission}</p></div>" } },
            new { title = "Diagnózy", text = new { status = "generated", div = $"<div xmlns=\"http://www.w3.org/1999/xhtml\"><ul>{string.Join("", (dr.Diagnoses ?? new()).Select(d => $"<li>{d}</li>"))}</ul></div>" } },
            new { title = "Výkony", text = new { status = "generated", div = $"<div xmlns=\"http://www.w3.org/1999/xhtml\"><ul>{string.Join("", (dr.Procedures ?? new()).Select(p => $"<li>{p}</li>"))}</ul></div>" } },
            new { title = "Průběh hospitalizace", text = new { status = "generated", div = $"<div xmlns=\"http://www.w3.org/1999/xhtml\"><p>{dr.Course}</p></div>" } },
            new { title = "Medikace při propuštění", text = new { status = "generated", div = $"<div xmlns=\"http://www.w3.org/1999/xhtml\"><ul>{string.Join("", (dr.DischargeMedications ?? new()).Select(m => $"<li>{m.Name} {m.Dosage} {m.Instructions}</li>"))}</ul></div>" } },
            new { title = "Doporučení", text = new { status = "generated", div = $"<div xmlns=\"http://www.w3.org/1999/xhtml\"><p>{dr.Recommendations}</p></div>" } },
            new { title = "Následná péče", text = new { status = "generated", div = $"<div xmlns=\"http://www.w3.org/1999/xhtml\"><p>{dr.FollowUp}</p></div>" } }
        }
    };

    private static string? MapCodeSystem(string? codeSystem)
        => codeSystem?.ToUpperInvariant() switch
        {
            "ICD-10" => "http://hl7.org/fhir/sid/icd-10",
            "SNOMED" => "http://snomed.info/sct",
            _ => null
        };

    private static string? MapCriticality(string? c)
        => c?.ToLowerInvariant() switch
        {
            "high" => "high",
            "low" => "low",
            "unable-to-assess" => "unable-to-assess",
            _ => null
        };

    private static string ToFhirGender(string g)
        => g.ToUpperInvariant() switch
        {
            "M" => "male",
            "Z" => "female",
            _ => "unknown"
        };

    // ---------- Helpery odpovědí ----------
    private static IResult Ok(object data, Guid zadostId, string stav, string? popis = null)
        => Results.Json(new { zadostId, stav, popis, data });

    private static IResult Created(object data, Guid zadostId, string? popis = null)
        => Results.Json(new { zadostId, stav = "CREATED", popis, data }, statusCode: StatusCodes.Status201Created);

    private static IResult NotFound(string message, Guid zadostId)
        => Results.Json(new { zadostId, stav = "NOT_FOUND", zprava = message }, statusCode: StatusCodes.Status404NotFound);

    private static IResult Bad(IEnumerable<string> chyby, Guid zadostId, string? subStav = null, int http = StatusCodes.Status400BadRequest)
        => Results.Json(new { zadostId, stav = "CHYBA", subStav, chyby = chyby.ToArray() }, statusCode: http);

    private static IEnumerable<string> ValidateCommon(Guid zadostId, string? ucel, DateTime? datum)
    {
        var e = new List<string>();
        if (zadostId == Guid.Empty) e.Add("zadostId je povinné.");
        if (string.IsNullOrWhiteSpace(ucel)) e.Add("ucel je povinné.");
        if (datum is null) e.Add("datum je povinné.");
        return e;
    }

    private static IEnumerable<string> ValidateIco(string ico)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(ico)) e.Add("ico je povinné.");
        else if (ico.Length != 8 || !ico.All(char.IsDigit)) e.Add("ico musí mít přesně 8 číslic.");
        return e;
    }
}
// ==== MODELS, REQUESTY A JEDNODUCHÉ ÚLOŽIŠTĚ DB ====
// Pozn.: vše je v namespacu EzKzr.MockServer díky file-scoped namespace nahoře.

public record CiselnikItem(string Kod, string Nazev);

// --- KRP/KRZP/KRPZS reklamace a notifikace ---
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

// --- Notifikace ---
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

public sealed class Notification
{
    public Guid Id { get; set; }
    public string System { get; set; } = "";
    public string Typ { get; set; } = "";
    public string? Kriteria { get; set; }
    public string Kanal { get; set; } = "internal";
    public DateTime Vytvoreno { get; set; }
    public string Stav { get; set; } = "aktivni";
}

// --- Číselníky / referenční entity ---
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

// --- Patient Summary ---
public sealed class PatientSummary
{
    public PatientSummaryHeader Header { get; set; } = new();
    public PatientSummaryBody Body { get; set; } = new();
}

public sealed class PatientSummaryHeader
{
    public string Rid { get; set; } = "";
    public string GivenName { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    /// <summary>M/Z/X</summary>
    public string Gender { get; set; } = "X";
}

public sealed class PatientSummaryBody
{
    public List<Allergy>? Allergies { get; set; }
    public List<Problem>? Problems { get; set; }
    public List<Medication>? Medications { get; set; }
    public List<Vaccination>? Vaccinations { get; set; }
    public List<Implant>? Implants { get; set; }
}

public sealed class Allergy
{
    public string Text { get; set; } = "";
    public string? CodeSystem { get; set; }
    public string? Code { get; set; }
    public string? Criticality { get; set; }
}

public sealed class Problem
{
    public string Text { get; set; } = "";
    public string? CodeSystem { get; set; }
    public string? Code { get; set; }
}

public sealed class Medication
{
    public string Text { get; set; } = "";
    public string? Dosage { get; set; }
    public string? Route { get; set; }
}

public sealed class Vaccination
{
    public string Text { get; set; } = "";
    public DateOnly? Date { get; set; }
}

public sealed class Implant
{
    public string Text { get; set; } = "";
}

// --- HDR (propouštěcí zpráva) ---
public sealed class DischargeReport
{
    public DischargeHeader Header { get; set; } = new();
    public string? ReasonForAdmission { get; set; }
    public List<string>? Diagnoses { get; set; }
    public List<string>? Procedures { get; set; }
    public string? Course { get; set; }
    public List<MedEntry>? DischargeMedications { get; set; }
    public string? Recommendations { get; set; }
    public string? FollowUp { get; set; }
}

public sealed class DischargeHeader
{
    public string Rid { get; set; } = "";
    public DateTime Discharge { get; set; }
    public string AttendingDoctor { get; set; } = "";
    public string FacilityName { get; set; } = "";
}

public sealed class MedEntry
{
    public string Name { get; set; } = "";
    public string? Dosage { get; set; }
    public string? Instructions { get; set; }
}

// --- LAB report/order ---
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

// --- MI report/order ---
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

// --- EMS ---
public sealed class EmsRun
{
    public Guid Id { get; set; }
    public string Rid { get; set; } = "";
    public DateTime Started { get; set; }
    public string Reason { get; set; } = "";
    public Vitals? Vitals { get; set; }
    public List<string>? Interventions { get; set; }
    public string? Outcome { get; set; }
    public string? Destination { get; set; }
}

public sealed class Vitals
{
    public int? Systolic { get; set; }
    public int? Diastolic { get; set; }
    public int? HeartRate { get; set; }
    public int? Spo2 { get; set; }
    public decimal? Temperature { get; set; }
}

public sealed class CreateEmsRecord
{
    public KzrDotaz? ZadostInfo { get; set; }
    public EmsRun? ZadostData { get; set; }
}

// --- RID servis + evidence RID ---
public sealed class RidRecord
{
    public string Rid { get; set; } = "";
    public string GivenName { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public DateTime Created { get; set; }
}

public static class RidService
{
    // Jednoduchý generátor 10místného RID: dělitelné 13, ne 11, nezačíná nulou a není duplicitní.
    public static string Generate(IEnumerable<RidRecord> existing)
    {
        var used = new HashSet<string>(existing.Select(r => r.Rid));
        var rnd = Random.Shared;

        while (true)
        {
            long baseNum = 1_000_000_000L + rnd.NextInt64(0, 9_000_000_000L); // 10 číslic, nezačíná 0
            // posuň na nejbližší násobek 13
            var rem13 = baseNum % 13;
            if (rem13 != 0) baseNum += (13 - rem13);
            if (baseNum % 11 == 0) { baseNum += 13; } // vyhnout se dělitelnosti 11
            var rid = baseNum.ToString(CultureInfo.InvariantCulture);
            if (rid.Length == 10 && !used.Contains(rid)) return rid;
        }
    }
}

// --- Jednoduchá "DB" perzistence do ./data/*.json ---
public static class Db
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static List<ProviderDto> Providers { get; } =
        LoadList<ProviderDto>("providers.json", () => new()
        {
            new ProviderDto { Ico = "12345678", Nazev = "Nemocnice Alfa a.s." },
            new ProviderDto { Ico = "87654321", Nazev = "Poliklinika Beta s.r.o." }
        });

    public static List<WorkerDto> Workers { get; } =
        LoadList<WorkerDto>("workers.json", () => new()
        {
            new WorkerDto { KrzpId = 1001, Jmeno = "Jan",  Prijmeni = "Novák",  DatumNarozeni = new DateOnly(1980,5,1), ZamestnavatelIco = "12345678" },
            new WorkerDto { KrzpId = 1002, Jmeno = "Eva",  Prijmeni = "Svobodová", DatumNarozeni = new DateOnly(1985,7,10), ZamestnavatelIco = "87654321" },
            new WorkerDto { KrzpId = 1003, Jmeno = "Petr", Prijmeni = "Dvořák", DatumNarozeni = new DateOnly(1990,1,15), ZamestnavatelIco = "12345678" }
        });

    public static List<RidRecord> Rids { get; } =
        LoadList<RidRecord>("rids.json", () => new()
        {
            new RidRecord
            {
                Rid = "1000000014", // dělitelné 13, ne 11
                GivenName = "Karel", FamilyName = "Test", DateOfBirth = new DateOnly(1975, 3, 14), Created = DateTime.UtcNow
            }
        });

    public static List<PatientSummary> PatientSummaries { get; } =
        LoadList<PatientSummary>("ps.json", () => {
            var rid = Rids[0].Rid;
            return new()
            {
                new PatientSummary
                {
                    Header = new PatientSummaryHeader
                    {
                        Rid = rid, GivenName = "Karel", FamilyName = "Test",
                        DateOfBirth = new DateOnly(1975,3,14), Gender = "M"
                    },
                    Body = new PatientSummaryBody
                    {
                        Allergies = new() { new Allergy { Text = "Penicilin", CodeSystem = "SNOMED", Code = "294513009", Criticality = "low" } },
                        Problems  = new() { new Problem  { Text = "Hypertenze", CodeSystem = "ICD-10", Code = "I10" } },
                        Medications = new() { new Medication { Text = "Atorvastatin 20 mg", Dosage = "1-0-0", Route = "per os" } },
                        Vaccinations = new() { new Vaccination { Text = "COVID-19", Date = new DateOnly(2023,10,1) } },
                        Implants = new() { new Implant { Text = "Stent koronární" } }
                    }
                }
            };
        });

    public static List<DischargeReport> DischargeReports { get; } =
        LoadList<DischargeReport> ("hdr.json", () => {
            var rid = PatientSummaries[0].Header.Rid;
            return new()
            {
                new DischargeReport
                {
                    Header = new DischargeHeader
                    {
                        Rid = rid, Discharge = DateTime.UtcNow.AddDays(-7),
                        AttendingDoctor = "MUDr. Alfa", FacilityName = "Nemocnice Alfa a.s."
                    },
                    ReasonForAdmission = "Bolesti na hrudi",
                    Diagnoses = new() { "I21.9 Akutní infarkt myokardu", "I10 Hypertenze" },
                    Procedures = new() { "Koronarografie", "PCI" },
                    Course = "Nezkomplikovaný průběh",
                    DischargeMedications = new() { new MedEntry { Name = "ASA", Dosage = "100 mg 1-0-0" } },
                    Recommendations = "Kontrola Kardiologie 6 týdnů",
                    FollowUp = "PL do 3 dnů"
                }
            };
        });

    public static List<LabReport> LabReports { get; } =
        LoadList<LabReport>("lab_reports.json", () => {
            var rid = PatientSummaries[0].Header.Rid;
            return new()
            {
                new LabReport
                {
                    Header = new LabHeader { Rid = rid, Issued = DateTime.UtcNow.AddDays(-2), Laboratory = "Lab Alfa", OrderId = Guid.NewGuid().ToString() },
                    Results = new() { new LabResult { Code = "718-7", Text = "Hemoglobin", Value = "140", Unit = "g/L", ReferenceRange = "135-175", AbnormalFlag = "N" } }
                }
            };
        });

    public static List<ImagingReport> ImagingReports { get; } =
        LoadList<ImagingReport>("mi_reports.json", () => {
            var rid = PatientSummaries[0].Header.Rid;
            return new()
            {
                new ImagingReport
                {
                    Header = new ImagingHeader { Rid = rid, Performed = DateTime.UtcNow.AddDays(-10), Modality = "US", Performer = "MUDr. Beta", FacilityName = "Poliklinika Beta s.r.o." },
                    Indication = "Kontrolní vyšetření",
                    Findings = "Bez patrné patologie.",
                    Conclusion = "Nález v normě."
                }
            };
        });

    public static List<LabOrder> LabOrders { get; } =
        LoadList<LabOrder>("lab_orders.json", () => {
            var rid = PatientSummaries[0].Header.Rid;
            return new()
            {
                new LabOrder { Id = Guid.NewGuid(), Rid = rid, Created = DateTime.UtcNow.AddDays(-3), Tests = new(){ "Glukóza", "Hb" }, RequesterIco = Providers[0].Ico, RequesterName = Providers[0].Nazev, Status = "received" }
            };
        });

    public static List<ImagingOrder> ImagingOrders { get; } =
        LoadList<ImagingOrder>("mi_orders.json", () => {
            var rid = PatientSummaries[0].Header.Rid;
            return new()
            {
                new ImagingOrder { Id = Guid.NewGuid(), Rid = rid, Created = DateTime.UtcNow.AddDays(-4), RequestedModality = "CT", RequestedProcedure = "CT hrudníku", ClinicalInfo = "Kontrola", RequesterIco = Providers[0].Ico, RequesterName = Providers[0].Nazev, Status = "received" }
            };
        });

    public static List<EmsRun> EmsRuns { get; } =
        LoadList<EmsRun>("ems_runs.json", () => {
            var rid = PatientSummaries[0].Header.Rid;
            return new()
            {
                new EmsRun { Id = Guid.NewGuid(), Rid = rid, Started = DateTime.UtcNow.AddDays(-15), Reason = "Bolest na hrudi", Vitals = new Vitals{ Systolic=150, Diastolic=90, HeartRate=100, Spo2=95, Temperature=36.8m }, Interventions = new(){ "ASA 500 mg", "Monitoring" }, Outcome = "Převoz", Destination = Providers[0].Nazev }
            };
        });

    // --- Save helpers ---
    public static void SaveRids(IEnumerable<RidRecord> items) => SaveList("rids.json", items);
    public static void SaveLabReports(IEnumerable<LabReport> items) => SaveList("lab_reports.json", items);
    public static void SaveImagingReports(IEnumerable<ImagingReport> items) => SaveList("mi_reports.json", items);
    public static void SaveLabOrders(IEnumerable<LabOrder> items) => SaveList("lab_orders.json", items);
    public static void SaveImagingOrders(IEnumerable<ImagingOrder> items) => SaveList("mi_orders.json", items);
    public static void SaveEmsRuns(IEnumerable<EmsRun> items) => SaveList("ems_runs.json", items);
    public static void SaveNotifications(IEnumerable<Notification> items) => SaveList("notifications.json", items);

    // --- I/O ---
    private static List<T> LoadList<T>(string fileName, Func<List<T>> seed)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            var path = Path.Combine(DataDir, fileName);
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<T>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return list ?? seed();
            }
            var seeded = seed();
            File.WriteAllText(path, JsonSerializer.Serialize(seeded, JsonOpts));
            return seeded;
        }
        catch
        {
            return seed();
        }
    }

    private static void SaveList<T>(string fileName, IEnumerable<T> items)
    {
        Directory.CreateDirectory(DataDir);
        var path = Path.Combine(DataDir, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(items, JsonOpts));
    }
}
