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
using System.Globalization;
using System.Text;


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

    private const string FhirJson = "application/fhir+json";

    private static readonly ConcurrentDictionary<Guid, Notification> Notifications = new();

    // --- Patient Summary seed ---
    private static readonly List<PatientSummary> PatientSummaries =
    [
        new PatientSummary
        {
            Header = new PatientHeader
            {
                Rid = "1234567891",
                GivenName = "Jan",
                FamilyName = "Novák",
                DateOfBirth = new DateOnly(1980, 1, 15),
                Gender = "M"
            },
            Body = new PatientSummaryBody
            {
                Allergies =
                [
                    new Allergy { Text = "Alergie na penicilin", CodeSystem = "SNOMED", Code = "91936005", Criticality = "high" }
                ],
                Vaccinations =
                [
                    new Vaccination { Text = "Tetanus", Date = new DateOnly(2020, 5, 20) }
                ],
                Problems =
                [
                    new Problem { Text = "Diabetes mellitus 2. typu", CodeSystem = "ICD-10", Code = "E11" }
                ],
                Medications =
                [
                    new Medication { Text = "Metformin 500 mg", Dosage = "1-0-1", Route = "per os" }
                ],
                Implants =
                [
                    new Implant { Text = "Koronární stent", Date = new DateOnly(2019, 9, 1) }
                ],
                AdvanceDirectives = "DNAR"
            }
        },
new PatientSummary
{
    Header = new PatientHeader
    {
        Rid = "2345678902",
        GivenName = "Eva",
        FamilyName = "Malá",
        DateOfBirth = new DateOnly(1992, 3, 22),
        Gender = "Z"
    },
    Body = new PatientSummaryBody
    {
        Allergies = [],
        Vaccinations = [ new Vaccination { Text = "Covid‑19", Date = new DateOnly(2023, 11, 1) } ],
        Problems = [ new Problem { Text = "Hypertenze", CodeSystem = "ICD-10", Code = "I10" } ],
        Medications = [ new Medication { Text = "Perindopril 5 mg", Dosage = "1-0-0" } ],
        Implants = [],
        AdvanceDirectives = null
    }
}
    ];

    // --- Hospital Discharge Report (HDR) seed (eHN/CZ) ---
    private static readonly List<DischargeReport> DischargeReports =
    [
        new DischargeReport
        {
            Header = new DischargeHeader
            {
                Rid = "1234567891",
                FacilityIco = "12345678",
                FacilityName = "Nemocnice Alfa, a.s.",
                Department = "Kardiologie",
                AttendingDoctor = "MUDr. Jana Nováková",
                Admission = new DateTime(2024, 10, 9, 10, 48, 0, DateTimeKind.Utc),
                Discharge = new DateTime(2024, 10, 17, 12, 0, 0, DateTimeKind.Utc),
                DischargeDestination = "Domů"
            },
            ReasonForAdmission = "Bolest na hrudi a dušnost.",
            Diagnoses = new List<string>
            {
                "I21.0 Akutní infarkt myokardu přední stěny",
                "E11 Diabetes mellitus 2. typu"
            },
            Procedures = new List<string>
            {
                "Koronární angiografie",
                "Perkutánní koronární intervence se zavedením stentu"
            },
            Course = "Po přijetí zahájena antitrombotická léčba. Provedena PCI s implantací stentu. Průběh bez komplikací.",
            DischargeMedications = new List<MedicationOnDischarge>
            {
                new MedicationOnDischarge { Name = "Acetylsalicylová kyselina 100 mg", Dosage = "1-0-0", Instructions = "ráno" },
                new MedicationOnDischarge { Name = "Atorvastatin 20 mg", Dosage = "0-0-1", Instructions = "večer" },
                new MedicationOnDischarge { Name = "Metformin 500 mg", Dosage = "1-0-1", Instructions = "s jídlem" }
            },
            FollowUp = "Kontrola na kardiologii za 6 týdnů.",
            Recommendations = "Nekuřit. Dietní a režimová opatření."
        },
new DischargeReport
{
    Header = new DischargeHeader
    {
        Rid = "2345678902",
        FacilityIco = "87654321",
        FacilityName = "Poliklinika Beta, s.r.o.",
        Department = "Interna",
        AttendingDoctor = "MUDr. Petr Svoboda",
        Admission = new DateTime(2024, 6, 3, 9, 0, 0, DateTimeKind.Utc),
        Discharge = new DateTime(2024, 6, 7, 14, 30, 0, DateTimeKind.Utc),
        DischargeDestination = "Domů"
    },
    ReasonForAdmission = "Synkopa.",
    Diagnoses = new List<string> { "I10 Esenciální hypertenze" },
    Procedures = new List<string> { "Monitorace TK, EKG" },
    Course = "Stabilizace, úprava medikace.",
    DischargeMedications = new List<MedicationOnDischarge>
    {
        new MedicationOnDischarge { Name = "Perindopril 5 mg", Dosage = "1-0-0", Instructions = "ráno" }
    },
    FollowUp = "Praktický lékař za 2 týdny.",
    Recommendations = "Dostatečný pitný režim."
}
    ];
    // --- Laboratory Results seed ---
    private static readonly List<LabReport> LabReports =
    [
        new LabReport
    {
        Header = new LabHeader
        {
            Rid = "1234567891",
            Issued = new DateTime(2024, 10, 17, 13, 0, 0, DateTimeKind.Utc),
            Laboratory = "Oddělení klinické biochemie Nemocnice Alfa",
            OrderId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
        },
        Results =
        [
            new LabResult { Code = "718-7",  Text = "Hemoglobin", Value = "145", Unit = "g/L",     ReferenceRange = "135-175", AbnormalFlag = "N" },
            new LabResult { Code = "6690-2", Text = "Leukocyty",  Value = "6.2", Unit = "10^9/L", ReferenceRange = "4.0-10.0", AbnormalFlag = "N" },
            new LabResult { Code = "2345-7", Text = "Glukóza",    Value = "7.8", Unit = "mmol/L", ReferenceRange = "3.9-5.5", AbnormalFlag = "H" }
        ]
    },
    new LabReport
    {
        Header = new LabHeader
        {
            Rid = "2345678902",
            Issued = new DateTime(2024, 6, 7, 11, 0, 0, DateTimeKind.Utc),
            Laboratory = "Laboratoř Poliklinika Beta",
            OrderId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
        },
        Results =
        [
            new LabResult { Code = "2339-0", Text = "Sodík",  Value = "139", Unit = "mmol/L", ReferenceRange = "136-145", AbnormalFlag = "N" },
            new LabResult { Code = "2823-3", Text = "Draslík", Value = "4.2", Unit = "mmol/L", ReferenceRange = "3.5-5.1", AbnormalFlag = "N" }
        ]
    }
    ];

    // --- Imaging Reports seed ---
    private static readonly List<ImagingReport> ImagingReports =
    [
        new ImagingReport
    {
        Header = new ImagingHeader
        {
            Rid = "1234567891",
            Performed = new DateTime(2024, 10, 10, 9, 30, 0, DateTimeKind.Utc),
            Modality = "CT",
            Performer = "MUDr. Alice Kovářová",
            FacilityName = "Nemocnice Alfa, a.s."
        },
        Indication = "Bolest na hrudi.",
        Findings  = "Subtotální stenóza LAD. Postisch. změny přední stěny.",
        Conclusion = "Nález odpovídá ICHS. Doporučena PCI."
    },
    new ImagingReport
    {
        Header = new ImagingHeader
        {
            Rid = "2345678902",
            Performed = new DateTime(2024, 6, 5, 10, 0, 0, DateTimeKind.Utc),
            Modality = "RTG",
            Performer = "MUDr. Pavel Hrubý",
            FacilityName = "Poliklinika Beta, s.r.o."
        },
        Indication = "Synkopa.",
        Findings  = "Bez čerstvých traumatických změn. Srdce nezvětšeno.",
        Conclusion = "Bez akutní patologie."
    }
    ];

    // --- Orders seed (Lab + Imaging) ---
    private static readonly List<LabOrder> LabOrders =
    [
        new LabOrder
    {
        Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Rid = "1234567891",
        Created = new DateTime(2024, 10, 9, 8, 0, 0, DateTimeKind.Utc),
        Tests = ["Glukóza", "Hemoglobin", "Leukocyty"],
        RequesterIco = "12345678",
        RequesterName = "Nemocnice Alfa, a.s.",
        Status = "received"
    },
    new LabOrder
    {
        Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        Rid = "2345678902",
        Created = new DateTime(2024, 6, 3, 8, 30, 0, DateTimeKind.Utc),
        Tests = ["Sodík", "Draslík"],
        RequesterIco = "87654321",
        RequesterName = "Poliklinika Beta, s.r.o.",
        Status = "received"
    }
    ];

    private static readonly List<ImagingOrder> ImagingOrders =
    [
        new ImagingOrder
    {
        Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
        Rid = "1234567891",
        Created = new DateTime(2024, 10, 9, 9, 0, 0, DateTimeKind.Utc),
        RequestedModality = "CT",
        RequestedProcedure = "CT koronarografie",
        ClinicalInfo = "Bolest na hrudi",
        RequesterIco = "12345678",
        RequesterName = "Nemocnice Alfa, a.s.",
        Status = "received"
    },
    new ImagingOrder
    {
        Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
        Rid = "2345678902",
        Created = new DateTime(2024, 6, 3, 10, 0, 0, DateTimeKind.Utc),
        RequestedModality = "RTG",
        RequestedProcedure = "RTG hrudníku PA",
        ClinicalInfo = "Synkopa",
        RequesterIco = "87654321",
        RequesterName = "Poliklinika Beta, s.r.o.",
        Status = "received"
    }
    ];

    // --- EMS (Záznam o výjezdu) seed ---
    private static readonly List<EmsRun> EmsRuns =
    [
        new EmsRun
    {
        Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
        Rid = "1234567891",
        Started = new DateTime(2024, 10, 9, 10, 0, 0, DateTimeKind.Utc),
        Reason = "Bolest na hrudi",
        Vitals = new Vitals { Systolic = 150, Diastolic = 90, HeartRate = 105, Spo2 = 94, Temperature = 36.9m },
        Interventions = ["ASA 500 mg p.o.", "Nitroglycerin spray"],
        Outcome = "Převoz do nemocnice",
        Destination = "Nemocnice Alfa, a.s."
    },
    new EmsRun
    {
        Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
        Rid = "2345678902",
        Started = new DateTime(2024, 6, 3, 8, 15, 0, DateTimeKind.Utc),
        Reason = "Synkopa",
        Vitals = new Vitals { Systolic = 125, Diastolic = 80, HeartRate = 78, Spo2 = 98, Temperature = 36.6m },
        Interventions = ["Monitoring, EKG"],
        Outcome = "Předán na interní ambulanci",
        Destination = "Poliklinika Beta, s.r.o."
    }
    ];
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

        // ----- SAMPLES (ukázková data a volání) -----
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
                    krzp_get_osoba =
                        $"/api/v1/krzp/hledat/{exZadostId}/jmeno_prijmeni_datum_narozeni?jmeno={Uri.EscapeDataString(Workers[0].Jmeno)}&prijmeni={Uri.EscapeDataString(Workers[0].Prijmeni)}&datumNarozeni={Workers[0].DatumNarozeni:yyyy-MM-dd}&ucel=Test&datum={exDate}",
                    krzp_get_zamestnavatel =
                        $"/api/v1/krzp/hledat/{exZadostId}/zamestnavatel?ico={Workers[0].ZamestnavatelIco}&ucel=Test&datum={exDate}",

                    ps_by_rid = $"/api/v1/ps/rid/{exZadostId}?rid={PatientSummaries[0].Header.Rid}&ucel=Test&datum={exDate}",
                    ps_by_osoba =
                        $"/api/v1/ps/osoba/{exZadostId}?jmeno={Uri.EscapeDataString(PatientSummaries[0].Header.GivenName)}&prijmeni={Uri.EscapeDataString(PatientSummaries[0].Header.FamilyName)}&datumNarozeni={PatientSummaries[0].Header.DateOfBirth:yyyy-MM-dd}&ucel=Test&datum={exDate}",

                    hdr_by_rid = $"/api/v1/hdr/rid/{exZadostId}?rid={DischargeReports[0].Header.Rid}&ucel=Test&datum={exDate}",

                    fhir_metadata = "/fhir/metadata",
                    fhir_patient = $"/fhir/Patient/{PatientSummaries[0].Header.Rid}",
                    fhir_summary = $"/fhir/Patient/{PatientSummaries[0].Header.Rid}/$summary",
                    fhir_document = $"/fhir/Bundle/{DischargeReports[0].Header.Rid}/$discharge"
                }
            });
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

        samples.MapGet("/providers", () => Results.Ok(Providers))
            .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

        samples.MapGet("/workers", () => Results.Ok(Workers))
            .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

        samples.MapGet("/ps", () => Results.Ok(PatientSummaries))
            .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

        samples.MapGet("/hdr", () => Results.Ok(DischargeReports))
            .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);
        samples.MapGet("/lab", () => Results.Ok(LabReports))
            .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

        samples.MapGet("/mi", () => Results.Ok(ImagingReports))
            .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

        samples.MapGet("/ems", () => Results.Ok(EmsRuns))
            .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

        // ukázkové request body (LAB/MI/EMS)
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
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

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
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

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
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

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
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

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
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);


        // ukázkové request body pro POSTy
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
                    Reklamujici = new Reklamujici
                    {
                        Ico = Providers[0].Ico,
                        Nazev = Providers[0].Nazev,
                        KontaktEmail = "it@example.org"
                    },
                    PolozkyReklamace = new List<UdajReklamace>
                    {
                new UdajReklamace { Klic = "Nazev", PuvodniHodnota = "Nemocnice Alfa, a.s.", PozadovanaHodnota = "Nemocnice ALFA a.s." }
                    },
                    Zduvodneni = "Oprava údajů v registru",
                    PopisReklamace = "Formální úprava názvu"
                }
            };
            return Results.Ok(body);
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

        samples.MapGet("/body/notifikace", () =>
        {
            var req = new CreateNotification
            {
                ZadostInfo = new KzrDotaz { Ucel = "Test", Datum = DateTime.UtcNow },
                ZadostData = new NotificationRequest
                {
                    System = "KRPZS",
                    Typ = "zmena-pzs",
                    Kriteria = $"ico={Providers[0].Ico}",
                    Kanal = "webhook"
                }
            };
            return Results.Ok(req);
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json);

        // ----- Patient Summary (EU PS/IPS mock) -----
        var ps = api.MapGroup("/ps");

        // GET /api/v1/ps/rid/{zadostId}?rid=...
        ps.MapGet("/rid/{zadostId:guid}", (Guid zadostId, string rid, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(rid)) errs.Add("rid je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var psu = PatientSummaries.FirstOrDefault(x => x.Header.Rid == rid);
            if (psu is null) return NotFound($"Pacient s RID {rid} nenalezen.", zadostId);

            return Ok(psu, zadostId, "OK", popis: "PatientSummary");
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status404NotFound, contentType: MediaTypeNames.Application.Json);

        // GET /api/v1/ps/osoba/{zadostId}?jmeno=...&prijmeni=...&datumNarozeni=YYYY-MM-DD
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
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status404NotFound, contentType: MediaTypeNames.Application.Json);

        // ----- HDR (Propouštěcí zpráva) -----
        var hdr = api.MapGroup("/hdr");

        // GET /api/v1/hdr/rid/{zadostId}?rid=...
        hdr.MapGet("/rid/{zadostId:guid}", (Guid zadostId, string rid, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(rid)) errs.Add("rid je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var rep = FindHdr(rid);
            if (rep is null) return NotFound($"Propouštěcí zpráva pro RID {rid} nenalezena.", zadostId);

            return Ok(rep, zadostId, "OK", popis: "HDR");
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status404NotFound, contentType: MediaTypeNames.Application.Json);

        // GET /api/v1/hdr/osoba/{zadostId}?jmeno=...&prijmeni=...&datumNarozeni=YYYY-MM-DD
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
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status404NotFound, contentType: MediaTypeNames.Application.Json);

        // ----- LAB (laboratorní výsledky + žádanky) -----
        var lab = api.MapGroup("/lab");

        // GET /api/v1/lab/rid/{zadostId}?rid=...
        lab.MapGet("/rid/{zadostId:guid}", (Guid zadostId, string rid, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(rid)) errs.Add("rid je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var list = LabReports.Where(x => x.Header.Rid == rid).ToList();
            if (list.Count == 0) return NotFound($"Laboratorní zpráva pro RID {rid} nenalezena.", zadostId);
            return Ok(list, zadostId, "OK", popis: "LAB");
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status404NotFound, contentType: MediaTypeNames.Application.Json);

        // POST /api/v1/lab/report/{zadostId}
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
            return Created(new { prijato = true, registr = "LAB", orderId = body.ZadostData!.Header.OrderId }, zadostId, popis: "LabReport přijata");
        })
        .Produces(StatusCodes.Status201Created, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json);

        // POST /api/v1/lab/order/{zadostId}
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
            return Created(body.ZadostData, zadostId, popis: "LabOrder přijata");
        })
        .Produces(StatusCodes.Status201Created, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json);

        // GET /api/v1/lab/order/{zadostId}?id=...
        lab.MapGet("/order/{zadostId:guid}", (Guid zadostId, Guid id, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);
            var o = LabOrders.FirstOrDefault(x => x.Id == id);
            if (o is null) return NotFound("Laboratorní žádanka nenalezena.", zadostId);
            return Ok(o, zadostId, "OK", popis: "LabOrder");
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status404NotFound, contentType: MediaTypeNames.Application.Json);

        // GET /api/v1/lab/result_by_order/{zadostId}?id=...
        lab.MapGet("/result_by_order/{zadostId:guid}", (Guid zadostId, Guid id, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);
            var list = LabReports.Where(x => string.Equals(x.Header.OrderId, id.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (list.Count == 0) return NotFound("Žádné výsledky pro zadanou žádanku.", zadostId);
            return Ok(list, zadostId, "OK", popis: "LAB");
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status404NotFound, contentType: MediaTypeNames.Application.Json);


        // ----- MI (zpráva z obrazového vyšetření + žádanky) -----
        var mi = api.MapGroup("/mi");

        // GET /api/v1/mi/rid/{zadostId}?rid=...
        mi.MapGet("/rid/{zadostId:guid}", (Guid zadostId, string rid, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(rid)) errs.Add("rid je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var list = ImagingReports.Where(x => x.Header.Rid == rid).ToList();
            if (list.Count == 0) return NotFound($"Zpráva z obrazového vyšetření pro RID {rid} nenalezena.", zadostId);
            return Ok(list, zadostId, "OK", popis: "MI");
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status404NotFound, contentType: MediaTypeNames.Application.Json);

        // POST /api/v1/mi/report/{zadostId}
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
            return Created(new { prijato = true, registr = "MI" }, zadostId, popis: "ImagingReport přijata");
        })
        .Produces(StatusCodes.Status201Created, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json);

        // POST /api/v1/mi/order/{zadostId}
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
            return Created(body.ZadostData, zadostId, popis: "ImagingOrder přijata");
        })
        .Produces(StatusCodes.Status201Created, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json);

        // GET /api/v1/mi/order/{zadostId}?id=...
        mi.MapGet("/order/{zadostId:guid}", (Guid zadostId, Guid id, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);
            var o = ImagingOrders.FirstOrDefault(x => x.Id == id);
            if (o is null) return NotFound("Obrazová žádanka nenalezena.", zadostId);
            return Ok(o, zadostId, "OK", popis: "ImagingOrder");
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status404NotFound, contentType: MediaTypeNames.Application.Json);


        // ----- EMS (Záznam o výjezdu) -----
        var ems = api.MapGroup("/ems");

        // GET /api/v1/ems/rid/{zadostId}?rid=...
        ems.MapGet("/rid/{zadostId:guid}", (Guid zadostId, string rid, string? ucel, DateTime? datum) =>
        {
            var errs = ValidateCommon(zadostId, ucel, datum).ToList();
            if (string.IsNullOrWhiteSpace(rid)) errs.Add("rid je povinné.");
            if (errs.Count > 0) return Bad(errs, zadostId, subStav: "Validace", http: 400);

            var list = EmsRuns.Where(x => x.Rid == rid).ToList();
            if (list.Count == 0) return NotFound($"Záznam o výjezdu pro RID {rid} nenalezen.", zadostId);
            return Ok(list, zadostId, "OK", popis: "EMS");
        })
        .Produces(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status404NotFound, contentType: MediaTypeNames.Application.Json);

        // POST /api/v1/ems/record/{zadostId}
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
            return Created(body.ZadostData, zadostId, popis: "EMS Record přijat");
        })
        .Produces(StatusCodes.Status201Created, contentType: MediaTypeNames.Application.Json)
        .Produces(StatusCodes.Status400BadRequest, contentType: MediaTypeNames.Application.Json);


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

        // FHIR
        MapFhirRoutes(app);
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

    private static void MapFhirRoutes(WebApplication app)
    {
        var fhir = app.MapGroup("/fhir");

        // CapabilityStatement (metadata)
        fhir.MapGet("/metadata", () =>
        {
            var meta = new
            {
                resourceType = "CapabilityStatement",
                status = "active",
                date = DateTime.UtcNow.ToString("o"),
                kind = "instance",
                format = new[] { "json", FhirJson },
                rest = new[]
                {
                    new {
                        mode = "server",
                        resource = new object[]
                        {
                            new { type = "Patient",               interaction = new[] { new { code = "read" }, new { code = "search-type" } } },
                            new { type = "AllergyIntolerance",    interaction = new[] { new { code = "search-type" } } },
                            new { type = "Condition",             interaction = new[] { new { code = "search-type" } } },
                            new { type = "MedicationStatement",   interaction = new[] { new { code = "search-type" } } },
                            new { type = "Immunization",          interaction = new[] { new { code = "search-type" } } },
                            new { type = "Device",                interaction = new[] { new { code = "search-type" } } },
                            new { type = "Composition",           interaction = new[] { new { code = "search-type" } } },
                            new { type = "DocumentReference",     interaction = new[] { new { code = "search-type" } } },
                            new { type = "Observation",       interaction = new[] { new { code = "search-type" } } },
new { type = "DiagnosticReport",  interaction = new[] { new { code = "search-type" } } }
                        }
                    }
                }
            };
            return Fhir(meta);
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson);

        // Patient by RID
        fhir.MapGet("/Patient/{rid}", (string rid) =>
        {
            var ps = PatientSummaries.FirstOrDefault(x => x.Header.Rid == rid);
            if (ps is null) return FhirNotFound($"Patient {rid} not found.");
            return Fhir(FhirPatient(ps));
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson)
        .Produces(StatusCodes.Status404NotFound, contentType: FhirJson);

        // Search endpoints (Bundle type=searchset)
        fhir.MapGet("/AllergyIntolerance", (string patient) =>
        {
            var ps = FindPs(patient);
            if (ps is null) return FhirNotFound($"Patient {patient} not found.");
            var list = (ps.Body.Allergies ?? new()).Select((a, i) => FhirAllergy(a, ps.Header.Rid, i + 1)).ToList();
            return Fhir(ToSearchBundle(list));
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson)
        .Produces(StatusCodes.Status404NotFound, contentType: FhirJson);

        fhir.MapGet("/Condition", (string patient) =>
        {
            var ps = FindPs(patient);
            if (ps is null) return FhirNotFound($"Patient {patient} not found.");
            var list = (ps.Body.Problems ?? new()).Select((p, i) => FhirCondition(p, ps.Header.Rid, i + 1)).ToList();
            return Fhir(ToSearchBundle(list));
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson)
        .Produces(StatusCodes.Status404NotFound, contentType: FhirJson);

        fhir.MapGet("/MedicationStatement", (string patient) =>
        {
            var ps = FindPs(patient);
            if (ps is null) return FhirNotFound($"Patient {patient} not found.");
            var list = (ps.Body.Medications ?? new()).Select((m, i) => FhirMedicationStatement(m, ps.Header.Rid, i + 1)).ToList();
            return Fhir(ToSearchBundle(list));
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson)
        .Produces(StatusCodes.Status404NotFound, contentType: FhirJson);

        fhir.MapGet("/Immunization", (string patient) =>
        {
            var ps = FindPs(patient);
            if (ps is null) return FhirNotFound($"Patient {patient} not found.");
            var list = (ps.Body.Vaccinations ?? new()).Select((v, i) => FhirImmunization(v, ps.Header.Rid, i + 1)).ToList();
            return Fhir(ToSearchBundle(list));
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson)
        .Produces(StatusCodes.Status404NotFound, contentType: FhirJson);

        fhir.MapGet("/Device", (string patient) =>
        {
            var ps = FindPs(patient);
            if (ps is null) return FhirNotFound($"Patient {patient} not found.");
            var list = (ps.Body.Implants ?? new()).Select((d, i) => FhirDevice(d, ps.Header.Rid, i + 1)).ToList();
            return Fhir(ToSearchBundle(list));
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson)
        .Produces(StatusCodes.Status404NotFound, contentType: FhirJson);
        // FHIR Observation (LAB)
        fhir.MapGet("/Observation", (string patient, string? category) =>
        {
            if (!string.Equals(category, "laboratory", StringComparison.OrdinalIgnoreCase))
                return Fhir(ToSearchBundle(Array.Empty<object>()));
            var list = LabReports.Where(x => x.Header.Rid == patient)
                                 .SelectMany(r => r.Results ?? new())
                                 .Select((res, i) => FhirLabObservation(res, patient, i + 1))
                                 .ToList();
            return list.Count == 0 ? FhirNotFound($"No LAB observations for patient {patient}.") : Fhir(ToSearchBundle(list));
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson)
        .Produces(StatusCodes.Status404NotFound, contentType: FhirJson);

        // FHIR DiagnosticReport (LAB/IMAGING)
        fhir.MapGet("/DiagnosticReport", (string patient, string? category) =>
        {
            if (string.Equals(category, "laboratory", StringComparison.OrdinalIgnoreCase))
            {
                var list = LabReports.Where(x => x.Header.Rid == patient)
                                     .Select(r => FhirLabDiagnosticReport(r))
                                     .ToList();
                return list.Count == 0 ? FhirNotFound($"No LAB report for patient {patient}.") : Fhir(ToSearchBundle(list));
            }
            if (string.Equals(category, "imaging", StringComparison.OrdinalIgnoreCase))
            {
                var list = ImagingReports.Where(x => x.Header.Rid == patient)
                                         .Select(r => FhirImagingDiagnosticReport(r))
                                         .ToList();
                return list.Count == 0 ? FhirNotFound($"No Imaging report for patient {patient}.") : Fhir(ToSearchBundle(list));
            }
            return Fhir(ToSearchBundle(Array.Empty<object>()));
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson)
        .Produces(StatusCodes.Status404NotFound, contentType: FhirJson);
        // Patient $summary => Bundle type=collection
        fhir.MapGet("/Patient/{rid}/$summary", (string rid) =>
        {
            var ps = PatientSummaries.FirstOrDefault(x => x.Header.Rid == rid);
            if (ps is null) return FhirNotFound($"Patient {rid} not found.");

            var resources = new List<object> { FhirPatient(ps) };
            resources.AddRange((ps.Body.Allergies ?? new()).Select((a, i) => FhirAllergy(a, rid, i + 1)));
            resources.AddRange((ps.Body.Problems ?? new()).Select((p, i) => FhirCondition(p, rid, i + 1)));
            resources.AddRange((ps.Body.Medications ?? new()).Select((m, i) => FhirMedicationStatement(m, rid, i + 1)));
            resources.AddRange((ps.Body.Vaccinations ?? new()).Select((v, i) => FhirImmunization(v, rid, i + 1)));
            resources.AddRange((ps.Body.Implants ?? new()).Select((d, i) => FhirDevice(d, rid, i + 1)));

            return Fhir(ToCollectionBundle(resources));
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson)
        .Produces(StatusCodes.Status404NotFound, contentType: FhirJson);

        // --- FHIR search: Composition (HDR) ---
        fhir.MapGet("/Composition", (string patient, string? type) =>
        {
            var dr = FindHdr(patient);
            if (dr is null) return FhirNotFound($"No HDR for patient {patient}.");
            var comp = FhirDischargeComposition(dr);
            return Fhir(ToSearchBundle(new[] { comp }));
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson)
        .Produces(StatusCodes.Status404NotFound, contentType: FhirJson);

        // --- FHIR search: DocumentReference (points to a document Bundle) ---
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
                content = new[]
                {
                    new
                    {
                        attachment = new
                        {
                            contentType = FhirJson,
                            url = $"/fhir/Bundle/{patient}/$discharge"
                        }
                    }
                }
            };
            return Fhir(ToSearchBundle(new[] { doc }));
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson)
        .Produces(StatusCodes.Status404NotFound, contentType: FhirJson);

        // --- FHIR document: Bundle type=document with Composition ---
        fhir.MapGet("/Bundle/{rid}/$discharge", (string rid) =>
        {
            var dr = FindHdr(rid);
            if (dr is null) return FhirNotFound($"No HDR for patient {rid}.");
            var composition = FhirDischargeComposition(dr);
            var ps = PatientSummaries.FirstOrDefault(x => x.Header.Rid == rid);
            var patientRes = ps is null ? new { resourceType = "Patient", id = rid } : FhirPatient(ps);
            var doc = ToDocumentBundle(new[] { composition, patientRes });
            return Fhir(doc);
        })
        .Produces(StatusCodes.Status200OK, contentType: FhirJson)
        .Produces(StatusCodes.Status404NotFound, contentType: FhirJson);
    }

    private static PatientSummary? FindPs(string rid)
        => PatientSummaries.FirstOrDefault(x => x.Header.Rid == rid);

    private static DischargeReport? FindHdr(string rid)
        => DischargeReports.FirstOrDefault(x => x.Header.Rid == rid);

    // ---------- FHIR helpers ----------

    private static object FhirLabDiagnosticReport(LabReport r)
        => new
        {
            resourceType = "DiagnosticReport",
            id = $"labdr-{r.Header.Rid}-{r.Header.Issued:yyyyMMddHHmmss}",
            status = "final",
            category = new[]
            {
            new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/v2-0074", code = "LAB", display = "Laboratory" } } }
            },
            code = new
            {
                coding = new[] { new { system = "http://loinc.org", code = "11502-2", display = "Laboratory report" } },
                text = "Laboratorní zpráva"
            },
            subject = new { reference = $"Patient/{r.Header.Rid}" },
            effectiveDateTime = r.Header.Issued.ToString("o"),
            issued = r.Header.Issued.ToString("o"),
            performer = new[] { new { display = r.Header.Laboratory } },
            result = (r.Results ?? new()).Select((res, i) => new { reference = $"Observation/labobs-{i + 1}-{r.Header.Rid}" })
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
            category = new[]
            {
            new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "laboratory", display = "Laboratory" } } }
        },
            code = new
            {
                text = res.Text,
                coding = string.IsNullOrWhiteSpace(res.Code) ? null : new[] { new { system = "http://loinc.org", code = res.Code!, display = res.Text } }
            },
            subject = new { reference = $"Patient/{rid}" },
            effectiveDateTime = DateTime.UtcNow.ToString("o"),
            valueQuantity = numeric ? new { value = (double)num, unit = res.Unit } : null,
            valueString = numeric ? null : res.Value,
            referenceRange = string.IsNullOrWhiteSpace(res.ReferenceRange) ? null : new[] { new { text = res.ReferenceRange } },
            interpretation = string.IsNullOrWhiteSpace(res.AbnormalFlag) ? null : new[] { new { text = res.AbnormalFlag } }
        };
    }

    private static object FhirImagingDiagnosticReport(ImagingReport r)
        => new
        {
            resourceType = "DiagnosticReport",
            id = $"imgdr-{r.Header.Rid}-{r.Header.Performed:yyyyMMddHHmmss}",
            status = "final",
            category = new[]
            {
            new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/v2-0074", code = "RAD", display = "Radiology" } } }
            },
            code = new
            {
                coding = new[] { new { system = "http://loinc.org", code = "18748-4", display = "Diagnostic imaging report" } },
                text = "Zpráva z obrazového vyšetření"
            },
            subject = new { reference = $"Patient/{r.Header.Rid}" },
            effectiveDateTime = r.Header.Performed.ToString("o"),
            issued = r.Header.Performed.ToString("o"),
            performer = new[] { new { display = r.Header.FacilityName } },
            conclusion = r.Conclusion,
            presentedForm = new[]
            {
            new
            {
                contentType = "text/plain",
                data = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{r.Findings}\n\nZávěr: {r.Conclusion}"))
            }
            }
        };
    private static IResult Fhir(object payload, int statusCode = StatusCodes.Status200OK)
        => Results.Json(payload, contentType: FhirJson, statusCode: statusCode);

    private static IResult FhirNotFound(string msg)
        => Results.Json(new
        {
            resourceType = "OperationOutcome",
            issue = new[] { new { severity = "error", code = "not-found", diagnostics = msg } }
        }, contentType: FhirJson, statusCode: StatusCodes.Status404NotFound);

    private static object ToSearchBundle(IEnumerable<object> resources)
        => new
        {
            resourceType = "Bundle",
            type = "searchset",
            total = resources.Count(),
            entry = resources.Select(r => new { resource = r })
        };

    private static object ToCollectionBundle(IEnumerable<object> resources)
        => new
        {
            resourceType = "Bundle",
            type = "collection",
            total = resources.Count(),
            entry = resources.Select(r => new { resource = r })
        };

    private static object ToDocumentBundle(IEnumerable<object> resources)
        => new
        {
            resourceType = "Bundle",
            type = "document",
            timestamp = DateTime.UtcNow.ToString("o"),
            entry = resources.Select(r => new { resource = r })
        };

    private static object FhirPatient(PatientSummary ps)
        => new
        {
            resourceType = "Patient",
            id = ps.Header.Rid,
            identifier = new[]
            {
                new { system = "urn:oid:1.2.203.0.0.1.1", value = ps.Header.Rid }
            },
            name = new[]
            {
                new { family = ps.Header.FamilyName, given = new[] { ps.Header.GivenName } }
            },
            gender = ToFhirGender(ps.Header.Gender),
            birthDate = ps.Header.DateOfBirth.ToString("yyyy-MM-dd")
        };

    private static object FhirAllergy(Allergy a, string rid, int ix)
        => new
        {
            resourceType = "AllergyIntolerance",
            id = $"alg-{ix}-{rid}",
            clinicalStatus = new
            {
                coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical", code = "active" } }
            },
            verificationStatus = new
            {
                coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/allergyintolerance-verification", code = "confirmed" } }
            },
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

    private static object FhirCondition(Problem p, string rid, int ix)
        => new
        {
            resourceType = "Condition",
            id = $"cond-{ix}-{rid}",
            clinicalStatus = new
            {
                coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/condition-clinical", code = "active" } }
            },
            code = new
            {
                text = p.Text,
                coding = (p.Code is not null && p.CodeSystem is not null)
                    ? new[] { new { system = MapCodeSystem(p.CodeSystem), code = p.Code, display = p.Text } }
                    : null
            },
            subject = new { reference = $"Patient/{rid}" }
        };

    private static object FhirMedicationStatement(Medication m, string rid, int ix)
        => new
        {
            resourceType = "MedicationStatement",
            id = $"meds-{ix}-{rid}",
            status = "active",
            medicationCodeableConcept = new { text = m.Text },
            subject = new { reference = $"Patient/{rid}" },
            dosage = string.IsNullOrWhiteSpace(m.Dosage)
                ? null
                : new[]
                {
                    new
                    {
                        text = m.Dosage,
                        route = string.IsNullOrWhiteSpace(m.Route) ? null : new { text = m.Route }
                    }
                }
        };

    private static object FhirImmunization(Vaccination v, string rid, int ix)
        => new
        {
            resourceType = "Immunization",
            id = $"imm-{ix}-{rid}",
            status = "completed",
            vaccineCode = new { text = v.Text },
            patient = new { reference = $"Patient/{rid}" },
            occurrenceDateTime = v.Date.HasValue ? v.Date.Value.ToString("yyyy-MM-dd") : null
        };

    private static object FhirDevice(Implant d, string rid, int ix)
        => new
        {
            resourceType = "Device",
            id = $"dev-{ix}-{rid}",
            type = new { text = d.Text },
            patient = new { reference = $"Patient/{rid}" }
        };

    private static object FhirDischargeComposition(DischargeReport dr)
        => new
        {
            resourceType = "Composition",
            id = $"comp-hdr-{dr.Header.Rid}",
            status = "final",
            type = new
            {
                coding = new[] { new { system = "http://loinc.org", code = "18842-5", display = "Discharge summary" } },
                text = "Propouštěcí zpráva"
            },
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
                new { title = "Medikace při propuštění", text = new { status = "generated", div = $"<div xmlns=\"http://www.w3.org/1999/xhtml\"><ul>{string.Join("", (dr.DischargeMedications ?? new()).Select(m => $"<li>{m.Name} {(string.IsNullOrWhiteSpace(m.Dosage) ? "" : m.Dosage)} {(string.IsNullOrWhiteSpace(m.Instructions) ? "" : m.Instructions)}</li>"))}</ul></div>" } },
                new { title = "Doporučení a kontroly", text = new { status = "generated", div = $"<div xmlns=\"http://www.w3.org/1999/xhtml\"><p>{dr.Recommendations}</p><p>{dr.FollowUp}</p></div>" } }
            }
        };

    private static string ToFhirGender(string? g) => g?.ToUpperInvariant() switch
    {
        "M" => "male",
        "Z" => "female",
        _ => "unknown"
    };

    private static string? MapCodeSystem(string? cs) => cs?.ToUpperInvariant() switch
    {
        "SNOMED" => "http://snomed.info/sct",
        "ICD-10" => "http://hl7.org/fhir/sid/icd-10",
        _ => null
    };

    private static string? MapCriticality(string? c) => c?.ToLowerInvariant() switch
    {
        "low" => "low",
        "high" => "high",
        "unable-to-assess" => "unable-to-assess",
        _ => null
    };

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

    // Patient Summary DTOs
    public sealed class PatientHeader
    {
        public string Rid { get; set; } = default!;
        public string GivenName { get; set; } = default!;
        public string FamilyName { get; set; } = default!;
        public DateOnly DateOfBirth { get; set; }
        public string Gender { get; set; } = default!;
    }

    public sealed class PatientSummary
    {
        public PatientHeader Header { get; set; } = default!;
        public PatientSummaryBody Body { get; set; } = default!;
    }

    public sealed class PatientSummaryBody
    {
        public List<Allergy>? Allergies { get; set; }
        public List<Vaccination>? Vaccinations { get; set; }
        public List<Problem>? Problems { get; set; }
        public List<Medication>? Medications { get; set; }
        public List<Implant>? Implants { get; set; }
        public string? AdvanceDirectives { get; set; }
    }

    public sealed class Allergy
    {
        public string Text { get; set; } = default!;
        public string? CodeSystem { get; set; }
        public string? Code { get; set; }
        public string? Criticality { get; set; } // low|high|unable-to-assess
    }

    public sealed class Vaccination
    {
        public string Text { get; set; } = default!;
        public DateOnly? Date { get; set; }
    }

    public sealed class Problem
    {
        public string Text { get; set; } = default!;
        public string? CodeSystem { get; set; }
        public string? Code { get; set; }
    }

    public sealed class Medication
    {
        public string Text { get; set; } = default!;
        public string? Dosage { get; set; }
        public string? Route { get; set; }
    }

    public sealed class Implant
    {
        public string Text { get; set; } = default!;
        public DateOnly? Date { get; set; }
    }

    // Discharge Report DTOs (eHN HDR / CZ)
    public sealed class DischargeHeader
    {
        public string Rid { get; set; } = default!;
        public string FacilityIco { get; set; } = default!;
        public string FacilityName { get; set; } = default!;
        public string Department { get; set; } = default!;
        public string AttendingDoctor { get; set; } = default!;
        public DateTime Admission { get; set; }
        public DateTime Discharge { get; set; }
        public string? DischargeDestination { get; set; }
    }

    public sealed class DischargeReport
    {
        public DischargeHeader Header { get; set; } = default!;
        public string ReasonForAdmission { get; set; } = default!;
        public List<string>? Diagnoses { get; set; }
        public List<string>? Procedures { get; set; }
        public List<MedicationOnDischarge>? DischargeMedications { get; set; }
        public string Course { get; set; } = default!;
        public string FollowUp { get; set; } = default!;
        public string Recommendations { get; set; } = default!;
    }

    public sealed class MedicationOnDischarge
    {
        public string Name { get; set; } = default!;
        public string? Dosage { get; set; }
        public string? Instructions { get; set; }
    }
    // --- Laboratory DTOs ---
public sealed class LabHeader
{
    public string Rid { get; set; } = default!;
    public DateTime Issued { get; set; }
    public string Laboratory { get; set; } = default!;
    public string? OrderId { get; set; }
}
public sealed class LabResult
{
    public string Text { get; set; } = default!;
    public string? Code { get; set; }
    public string? Value { get; set; }
    public string? Unit { get; set; }
    public string? ReferenceRange { get; set; }
    public string? AbnormalFlag { get; set; } // H|L|N
}
public sealed class LabReport
{
    public LabHeader Header { get; set; } = default!;
    public List<LabResult> Results { get; set; } = [];
}

// --- Imaging DTOs ---
public sealed class ImagingHeader
{
    public string Rid { get; set; } = default!;
    public DateTime Performed { get; set; }
    public string Modality { get; set; } = default!;
    public string Performer { get; set; } = default!;
    public string FacilityName { get; set; } = default!;
}
public sealed class ImagingReport
{
    public ImagingHeader Header { get; set; } = default!;
    public string? Indication { get; set; }
    public string Findings { get; set; } = default!;
    public string Conclusion { get; set; } = default!;
}

// --- Orders DTOs ---
public sealed class LabOrder
{
    public Guid Id { get; set; }
    public string Rid { get; set; } = default!;
    public DateTime Created { get; set; }
    public List<string> Tests { get; set; } = [];
    public string RequesterIco { get; set; } = default!;
    public string RequesterName { get; set; } = default!;
    public string Status { get; set; } = "new"; // new|received|in-progress|done|cancelled
}
public sealed class ImagingOrder
{
    public Guid Id { get; set; }
    public string Rid { get; set; } = default!;
    public DateTime Created { get; set; }
    public string RequestedModality { get; set; } = default!;
    public string RequestedProcedure { get; set; } = default!;
    public string? ClinicalInfo { get; set; }
    public string RequesterIco { get; set; } = default!;
    public string RequesterName { get; set; } = default!;
    public string Status { get; set; } = "new";
}

// --- EMS DTOs ---
public sealed class EmsRun
{
    public Guid Id { get; set; }
    public string Rid { get; set; } = default!;
    public DateTime Started { get; set; }
    public string Reason { get; set; } = default!;
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

// --- Wrappers for POST bodies ---
public sealed class CreateLabReport : KzrRequest<LabReport> { }
public sealed class CreateImagingReport : KzrRequest<ImagingReport> { }
public sealed class CreateLabOrder : KzrRequest<LabOrder> { }
public sealed class CreateImagingOrder : KzrRequest<ImagingOrder> { }
public sealed class CreateEmsRecord : KzrRequest<EmsRun> { }
}