cat > EzKzr.MockServer/Infrastructure/Db.cs <<'CS'
using EzKzr.MockServer.Models;

namespace EzKzr.MockServer.Infrastructure;

public static class Db
{
    private static readonly JsonStorage Store = new(AppContext.BaseDirectory);

    // -------- Datové kolekce (lazy načítání + seedy) --------
    public static List<ProviderDto> Providers { get; } =
        Store.LoadList<ProviderDto>("providers", () => new List<ProviderDto>
        {
            new ProviderDto
            {
                Ico = "12345678",
                Nazev = "Nemocnice Alfa, a.s."
            },
            new ProviderDto
            {
                Ico = "87654321",
                Nazev = "Poliklinika Beta, s.r.o."
            }
        });

    public static List<WorkerDto> Workers { get; } =
        Store.LoadList<WorkerDto>("workers", () => new List<WorkerDto>
        {
            new WorkerDto { KrzpId = 100001, Jmeno = "Jana", Prijmeni = "Nováková", DatumNarozeni = new DateOnly(1987,5,12), ZamestnavatelIco = "12345678" },
            new WorkerDto { KrzpId = 100002, Jmeno = "Petr", Prijmeni = "Svoboda", DatumNarozeni = new DateOnly(1979,9,3), ZamestnavatelIco = "87654321" }
        });

    public static List<PatientSummary> PatientSummaries { get; } =
        Store.LoadList<PatientSummary>("patients", () => new List<PatientSummary>
        {
            new PatientSummary
            {
                Header = new EzKzr.MockServer.PatientSummaryHeader { Rid = "1234567891", GivenName = "Jan", FamilyName = "Novák", DateOfBirth = new DateOnly(1980,1,15), Gender = "M" },
                Body = new PatientSummaryBody
                {
                    Allergies   = new() { new Allergy    { Text = "Alergie na penicilin", CodeSystem = "SNOMED", Code = "91936005", Criticality = "high" } },
                    Vaccinations= new() { new Vaccination{ Text = "Tetanus", Date = new DateOnly(2020,5,20) } },
                    Problems    = new() { new Problem    { Text = "Diabetes mellitus 2. typu", CodeSystem = "ICD-10", Code = "E11" } },
                    Medications = new() { new Medication { Text = "Metformin 500 mg", Dosage = "1-0-1", Route = "per os" } },
                    Implants    = new() { new Implant    { Text = "Koronární stent" } }
                }
            },
            new PatientSummary
            {
                Header = new EzKzr.MockServer.PatientSummaryHeader { Rid = "2345678902", GivenName = "Eva", FamilyName = "Malá", DateOfBirth = new DateOnly(1992,3,22), Gender = "Z" },
                Body = new PatientSummaryBody
                {
                    Allergies   = new(),
                    Vaccinations= new() { new Vaccination { Text = "Covid‑19", Date = new DateOnly(2023,11,1) } },
                    Problems    = new() { new Problem     { Text = "Hypertenze", CodeSystem = "ICD-10", Code = "I10" } },
                    Medications = new() { new Medication  { Text = "Perindopril 5 mg", Dosage = "1-0-0" } },
                    Implants    = new()
                }
            }
        });

    public static List<DischargeReport> DischargeReports { get; } =
        Store.LoadList<DischargeReport>("hdr", () => new List<DischargeReport>
        {
            new DischargeReport
            {
                Header = new DischargeHeader
                {
                    Rid = "1234567891",
                    FacilityName = "Nemocnice Alfa, a.s.",
                    AttendingDoctor = "MUDr. Jana Nováková",
                    Discharge = new DateTime(2024,10,17,12,0,0,DateTimeKind.Utc)
                },
                ReasonForAdmission = "Bolest na hrudi a dušnost.",
                Diagnoses = new() { "I21.0 Akutní infarkt myokardu přední stěny", "E11 Diabetes mellitus 2. typu" },
                Procedures = new() { "Koronární angiografie", "PCI se zavedením stentu" },
                Course = "Po přijetí antitrombotická léčba. PCI se stentem. Bez komplikací.",
                DischargeMedications = new(),
                FollowUp = "Kontrola na kardiologii za 6 týdnů.",
                Recommendations = "Nekuřit. Dietní a režimová opatření."
            },
            new DischargeReport
            {
                Header = new DischargeHeader
                {
                    Rid = "2345678902",
                    FacilityName = "Poliklinika Beta, s.r.o.",
                    AttendingDoctor = "MUDr. Petr Svoboda",
                    Discharge = new DateTime(2024,6,7,14,30,0,DateTimeKind.Utc)
                },
                ReasonForAdmission = "Synkopa.",
                Diagnoses = new() { "I10 Esenciální hypertenze" },
                Procedures = new() { "Monitorace TK, EKG" },
                Course = "Stabilizace, úprava medikace.",
                DischargeMedications = new(),
                FollowUp = "Praktický lékař za 2 týdny.",
                Recommendations = "Dostatečný pitný režim."
            }
        });

    public static List<LabReport> LabReports { get; } =
        Store.LoadList<LabReport>("labReports", () => new List<LabReport>
        {
            new LabReport
            {
                Header = new LabHeader
                {
                    Rid = "1234567891",
                    Issued = new DateTime(2024,10,17,13,0,0,DateTimeKind.Utc),
                    Laboratory = "Oddělení klinické biochemie Nemocnice Alfa",
                    OrderId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
                },
                Results = new()
                {
                    new LabResult { Code="718-7",  Text="Hemoglobin", Value="145", Unit="g/L",     ReferenceRange="135-175", AbnormalFlag="N" },
                    new LabResult { Code="6690-2", Text="Leukocyty",  Value="6.2", Unit="10^9/L", ReferenceRange="4.0-10.0", AbnormalFlag="N" },
                    new LabResult { Code="2345-7", Text="Glukóza",    Value="7.8", Unit="mmol/L", ReferenceRange="3.9-5.5", AbnormalFlag="H" }
                }
            },
            new LabReport
            {
                Header = new LabHeader
                {
                    Rid = "2345678902",
                    Issued = new DateTime(2024,6,7,11,0,0,DateTimeKind.Utc),
                    Laboratory = "Laboratoř Poliklinika Beta",
                    OrderId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
                },
                Results = new()
                {
                    new LabResult { Code="2339-0", Text="Sodík",  Value="139", Unit="mmol/L", ReferenceRange="136-145", AbnormalFlag="N" },
                    new LabResult { Code="2823-3", Text="Draslík", Value="4.2", Unit="mmol/L", ReferenceRange="3.5-5.1", AbnormalFlag="N" }
                }
            }
        });

    public static List<ImagingReport> ImagingReports { get; } =
        Store.LoadList<ImagingReport>("imagingReports", () => new List<ImagingReport>
        {
            new ImagingReport
            {
                Header = new ImagingHeader { Rid = "1234567891", Performed = new DateTime(2024,10,10,9,30,0,DateTimeKind.Utc), Modality = "CT", Performer = "MUDr. Alice Kovářová", FacilityName = "Nemocnice Alfa, a.s." },
                Indication = "Bolest na hrudi.", Findings = "Subtotální stenóza LAD. Postisch. změny přední stěny.", Conclusion = "Nález odpovídá ICHS. Doporučena PCI."
            },
            new ImagingReport
            {
                Header = new ImagingHeader { Rid = "2345678902", Performed = new DateTime(2024,6,5,10,0,0,DateTimeKind.Utc), Modality = "RTG", Performer = "MUDr. Pavel Hrubý", FacilityName = "Poliklinika Beta, s.r.o." },
                Indication = "Synkopa.", Findings = "Bez čerstvých traumatických změn. Srdce nezvětšeno.", Conclusion = "Bez akutní patologie."
            }
        });

    public static List<LabOrder> LabOrders { get; } =
        Store.LoadList<LabOrder>("labOrders", () => new List<LabOrder>
        {
            new LabOrder
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Rid = "1234567891",
                Created = new DateTime(2024,10,9,8,0,0,DateTimeKind.Utc),
                Tests = new() {"Glukóza","Hemoglobin","Leukocyty"},
                RequesterIco = "12345678",
                RequesterName = "Nemocnice Alfa, a.s.",
                Status = "received"
            },
            new LabOrder
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Rid = "2345678902",
                Created = new DateTime(2024,6,3,8,30,0,DateTimeKind.Utc),
                Tests = new() {"Sodík","Draslík"},
                RequesterIco = "87654321",
                RequesterName = "Poliklinika Beta, s.r.o.",
                Status = "received"
            }
        });

    public static List<ImagingOrder> ImagingOrders { get; } =
        Store.LoadList<ImagingOrder>("imagingOrders", () => new List<ImagingOrder>
        {
            new ImagingOrder
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Rid = "1234567891",
                Created = new DateTime(2024,10,9,9,0,0,DateTimeKind.Utc),
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
                Created = new DateTime(2024,6,3,10,0,0,DateTimeKind.Utc),
                RequestedModality = "RTG",
                RequestedProcedure = "RTG hrudníku PA",
                ClinicalInfo = "Synkopa",
                RequesterIco = "87654321",
                RequesterName = "Poliklinika Beta, s.r.o.",
                Status = "received"
            }
        });

    public static List<EmsRun> EmsRuns { get; } =
        Store.LoadList<EmsRun>("emsRuns", () => new List<EmsRun>
        {
            new EmsRun
            {
                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                Rid = "1234567891",
                Started = new DateTime(2024,10,9,10,0,0,DateTimeKind.Utc),
                Reason = "Bolest na hrudi",
                Vitals = new Vitals { Systolic = 150, Diastolic = 90, HeartRate = 105, Spo2 = 94, Temperature = 36.9m },
                Interventions = new() {"ASA 500 mg p.o.","Nitroglycerin spray"},
                Outcome = "Převoz do nemocnice",
                Destination = "Nemocnice Alfa, a.s."
            },
            new EmsRun
            {
                Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                Rid = "2345678902",
                Started = new DateTime(2024,6,3,8,15,0,DateTimeKind.Utc),
                Reason = "Synkopa",
                Vitals = new Vitals { Systolic = 125, Diastolic = 80, HeartRate = 78, Spo2 = 98, Temperature = 36.6m },
                Interventions = new() {"Monitoring, EKG"},
                Outcome = "Předán na interní ambulanci",
                Destination = "Poliklinika Beta, s.r.o."
            }
        });

    public static List<RidRecord> Rids { get; } =
        Store.LoadList<RidRecord>("rids", () => new List<RidRecord>
        {
            new RidRecord{ Rid="1234567891", GivenName="Jan", FamilyName="Novák", DateOfBirth=new DateOnly(1980,1,15), Created=DateTime.UtcNow},
            new RidRecord{ Rid="2345678902", GivenName="Eva", FamilyName="Malá",  DateOfBirth=new DateOnly(1992,3,22), Created=DateTime.UtcNow}
        });

    // -------- Save helpers --------
    public static void SaveLabReports(IEnumerable<LabReport> x)         => Store.SaveList<LabReport>("labReports", x);
    public static void SaveImagingReports(IEnumerable<ImagingReport> x) => Store.SaveList<ImagingReport>("imagingReports", x);
    public static void SaveLabOrders(IEnumerable<LabOrder> x)           => Store.SaveList<LabOrder>("labOrders", x);
    public static void SaveImagingOrders(IEnumerable<ImagingOrder> x)   => Store.SaveList<ImagingOrder>("imagingOrders", x);
    public static void SaveEmsRuns(IEnumerable<EmsRun> x)               => Store.SaveList<EmsRun>("emsRuns", x);
    public static void SaveNotifications(IEnumerable<Notification> x)   => Store.SaveList<Notification>("notifications", x);
    public static void SaveRids(IEnumerable<RidRecord> x)               => Store.SaveList<RidRecord>("rids", x);
}
CS