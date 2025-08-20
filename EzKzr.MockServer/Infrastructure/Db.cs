namespace EzKzr.MockServer.Infrastructure;

using EzKzr.MockServer.Models;
using static EzKzr.MockServer.Infrastructure.JsonStorage;

public static class Db
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    public static List<ProviderDto> Providers { get; } =
        LoadList<ProviderDto>(DataDir, "providers.json", () => new()
        {
            new ProviderDto { Ico = "12345678", Nazev = "Nemocnice Alfa a.s." },
            new ProviderDto { Ico = "87654321", Nazev = "Poliklinika Beta s.r.o." }
        });

    public static List<WorkerDto> Workers { get; } =
        LoadList<WorkerDto>(DataDir, "workers.json", () => new()
        {
            new WorkerDto { KrzpId = 1001, Jmeno = "Jan",  Prijmeni = "Novák",      DatumNarozeni = new DateOnly(1980,5,1),  ZamestnavatelIco = "12345678" },
            new WorkerDto { KrzpId = 1002, Jmeno = "Eva",  Prijmeni = "Svobodová",  DatumNarozeni = new DateOnly(1985,7,10), ZamestnavatelIco = "87654321" }
        });

    public static List<RidRecord> Rids { get; } =
        LoadList<RidRecord>(DataDir, "rids.json", () => new()
        {
            new RidRecord { Rid = "1000000014", GivenName="Karel", FamilyName="Test", DateOfBirth=new DateOnly(1975,3,14), Created = DateTime.UtcNow }
        });

    public static List<PatientSummary> PatientSummaries { get; } =
        LoadList<PatientSummary>(DataDir, "ps.json", () =>
        {
            var rid = Rids[0].Rid;
            return new()
            {
                new PatientSummary
                {
                    Header = new PatientSummaryHeader { Rid = rid, GivenName="Karel", FamilyName="Test", DateOfBirth=new DateOnly(1975,3,14), Gender="M" },
                    Body = new PatientSummaryBody
                    {
                        Allergies   = new() { new Allergy   { Text="Penicilin", CodeSystem="SNOMED", Code="294513009", Criticality="low" } },
                        Problems    = new() { new Problem   { Text="Hypertenze", CodeSystem="ICD-10", Code="I10" } },
                        Medications = new() { new Medication{ Text="Atorvastatin 20 mg", Dosage="1-0-0", Route="per os" } },
                        Vaccinations= new() { new Vaccination{ Text="COVID-19", Date=new DateOnly(2023,10,1) } },
                        Implants    = new() { new Implant   { Text="Stent koronární" } }
                    }
                }
            };
        });

    public static List<DischargeReport> DischargeReports { get; } =
        LoadList<DischargeReport>(DataDir, "hdr.json", () =>
        {
            var rid = PatientSummaries[0].Header.Rid;
            return new()
            {
                new DischargeReport
                {
                    Header = new DischargeHeader { Rid = rid, Discharge = DateTime.UtcNow.AddDays(-7), AttendingDoctor="MUDr. Alfa", FacilityName="Nemocnice Alfa a.s." },
                    ReasonForAdmission = "Bolesti na hrudi",
                    Diagnoses = new() { "I21.9 Akutní infarkt myokardu", "I10 Hypertenze" },
                    Procedures = new() { "Koronarografie", "PCI" },
                    Course = "Nezkomplikovaný průběh",
                    DischargeMedications = new() { new MedEntry { Name="ASA", Dosage="100 mg 1-0-0" } },
                    Recommendations = "Kontrola Kardiologie 6 týdnů",
                    FollowUp = "PL do 3 dnů"
                }
            };
        });

    public static List<LabReport> LabReports { get; } =
        LoadList<LabReport>(DataDir, "lab_reports.json", () =>
        {
            var rid = PatientSummaries[0].Header.Rid;
            return new()
            {
                new LabReport
                {
                    Header = new LabHeader { Rid = rid, Issued = DateTime.UtcNow.AddDays(-2), Laboratory = "Lab Alfa", OrderId = Guid.NewGuid().ToString() },
                    Results = new() { new LabResult { Code="718-7", Text="Hemoglobin", Value="140", Unit="g/L", ReferenceRange="135-175", AbnormalFlag="N" } }
                }
            };
        });

    public static List<ImagingReport> ImagingReports { get; } =
        LoadList<ImagingReport>(DataDir, "mi_reports.json", () =>
        {
            var rid = PatientSummaries[0].Header.Rid;
            return new()
            {
                new ImagingReport
                {
                    Header = new ImagingHeader { Rid = rid, Performed = DateTime.UtcNow.AddDays(-10), Modality="US", Performer="MUDr. Beta", FacilityName="Poliklinika Beta s.r.o." },
                    Indication="Kontrolní vyšetření",
                    Findings="Bez patrné patologie.",
                    Conclusion="Nález v normě."
                }
            };
        });

    public static List<LabOrder> LabOrders { get; } =
        LoadList<LabOrder>(DataDir, "lab_orders.json", () =>
        {
            var rid = PatientSummaries[0].Header.Rid;
            return new()
            {
                new LabOrder { Id = Guid.NewGuid(), Rid = rid, Created = DateTime.UtcNow.AddDays(-3), Tests = new(){ "Glukóza", "Hb" }, RequesterIco = Providers[0].Ico, RequesterName = Providers[0].Nazev, Status = "received" }
            };
        });

    public static List<ImagingOrder> ImagingOrders { get; } =
        LoadList<ImagingOrder>(DataDir, "mi_orders.json", () =>
        {
            var rid = PatientSummaries[0].Header.Rid;
            return new()
            {
                new ImagingOrder { Id = Guid.NewGuid(), Rid = rid, Created = DateTime.UtcNow.AddDays(-4), RequestedModality="CT", RequestedProcedure="CT hrudníku", ClinicalInfo="Kontrola", RequesterIco=Providers[0].Ico, RequesterName=Providers[0].Nazev, Status="received" }
            };
        });

    public static List<EmsRun> EmsRuns { get; } =
        LoadList<EmsRun>(DataDir, "ems_runs.json", () =>
        {
            var rid = PatientSummaries[0].Header.Rid;
            return new()
            {
                new EmsRun { Id = Guid.NewGuid(), Rid = rid, Started = DateTime.UtcNow.AddDays(-15), Reason="Bolest na hrudi", Vitals = new Vitals{ Systolic=150, Diastolic=90, HeartRate=100, Spo2=95, Temperature=36.8m }, Interventions = new(){ "ASA 500 mg", "Monitoring" }, Outcome = "Převoz", Destination = Providers[0].Nazev }
            };
        });

    // Save helpers
    public static void SavePatientSummaries(IEnumerable<PatientSummary> items) => SaveList(DataDir, "ps.json", items);
    public static void SaveRids(IEnumerable<RidRecord> items) => SaveList(DataDir, "rids.json", items);
    public static void SaveLabReports(IEnumerable<LabReport> items) => SaveList(DataDir, "lab_reports.json", items);
    public static void SaveImagingReports(IEnumerable<ImagingReport> items) => SaveList(DataDir, "mi_reports.json", items);
    public static void SaveLabOrders(IEnumerable<LabOrder> items) => SaveList(DataDir, "lab_orders.json", items);
    public static void SaveImagingOrders(IEnumerable<ImagingOrder> items) => SaveList(DataDir, "mi_orders.json", items);
    public static void SaveEmsRuns(IEnumerable<EmsRun> items) => SaveList(DataDir, "ems_runs.json", items);
    public static void SaveNotifications(IEnumerable<Notification> items) => SaveList(DataDir, "notifications.json", items);
}