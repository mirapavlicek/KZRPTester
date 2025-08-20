# tools/split_full.sh
#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.."; pwd)"
APP="$ROOT/EzKzr.MockServer"

echo "==> Vytvářím adresáře"
mkdir -p "$APP/Models" "$APP/Infrastructure" "$APP/Services" "$APP/data"

echo "==> Úprava Program.cs: přidání using a smazání starých modelů"
# Přidej usingy před 'namespace EzKzr.MockServer;'
awk 'BEGIN{done=0}
{
  if($0 ~ /^namespace EzKzr\.MockServer;$/ && done==0){
    print "using EzKzr.MockServer.Infrastructure;";
    print "using EzKzr.MockServer.Models;";
    print "using EzKzr.MockServer.Services;";
    done=1
  }
  print
}' "$APP/Program.cs" > "$APP/Program.cs.tmp" && mv "$APP/Program.cs.tmp" "$APP/Program.cs"

# Smaž vše od markeru MODELS ... do konce souboru
# (bez diakritiky v patternu kvůli přenositelnosti)
sed -i '' '/^\/\/ ==== MODELS/,$d' "$APP/Program.cs" || true

echo "==> Models"
# Models/CiselnikItem.cs
cat > "$APP/Models/CiselnikItem.cs" <<'EOF'
namespace EzKzr.MockServer.Models;
public record CiselnikItem(string Kod, string Nazev);
EOF

# Models/Reference.cs
cat > "$APP/Models/Reference.cs" <<'EOF'
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
EOF

# Models/KzrCommon.cs
cat > "$APP/Models/KzrCommon.cs" <<'EOF'
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
EOF

# Models/Notifications.cs
cat > "$APP/Models/Notifications.cs" <<'EOF'
namespace EzKzr.MockServer.Models;

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
EOF

# Models/PatientSummary.cs
cat > "$APP/Models/PatientSummary.cs" <<'EOF'
namespace EzKzr.MockServer.Models;

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
EOF

# Models/DischargeReport.cs
cat > "$APP/Models/DischargeReport.cs" <<'EOF'
namespace EzKzr.MockServer.Models;

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
EOF

# Models/Lab.cs
cat > "$APP/Models/Lab.cs" <<'EOF'
namespace EzKzr.MockServer.Models;

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
EOF

# Models/Imaging.cs
cat > "$APP/Models/Imaging.cs" <<'EOF'
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
EOF

# Models/Ems.cs
cat > "$APP/Models/Ems.cs" <<'EOF'
namespace EzKzr.MockServer.Models;

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
EOF

# Models/Patients.cs
cat > "$APP/Models/Patients.cs" <<'EOF'
namespace EzKzr.MockServer.Models;

public sealed class CreatePatient
{
    public KzrDotaz? ZadostInfo { get; set; }
    public NewPatient? ZadostData { get; set; }
}

public sealed class NewPatient
{
    public string GivenName { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = "X";
    public string? Drid { get; set; }
    public string? Rid { get; set; }
}
EOF

# Models/RidRecord.cs
cat > "$APP/Models/RidRecord.cs" <<'EOF'
namespace EzKzr.MockServer.Models;

public sealed class RidRecord
{
    public string Rid { get; set; } = "";
    public string GivenName { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public DateTime Created { get; set; }
    public bool IsTemporary { get; set; } = false;
    public string? PromotedToRid { get; set; }
}
EOF

echo "==> Services"
# Services/RidService.cs
cat > "$APP/Services/RidService.cs" <<'EOF'
namespace EzKzr.MockServer.Services;

using EzKzr.MockServer.Models;
using System.Globalization;

public static class RidService
{
    public static string Generate(IEnumerable<RidRecord> existing)
    {
        var used = new HashSet<string>(existing.Select(r => r.Rid));
        var rnd = Random.Shared;
        while (true)
        {
            long baseNum = 1_000_000_000L + rnd.NextInt64(0, 9_000_000_000L);
            var rem13 = baseNum % 13;
            if (rem13 != 0) baseNum += (13 - rem13);
            if (baseNum % 11 == 0) baseNum += 13;
            var rid = baseNum.ToString(CultureInfo.InvariantCulture);
            if (rid.Length == 10 && !used.Contains(rid)) return rid;
        }
    }

    public static string GenerateDrid(IEnumerable<RidRecord> existing)
    {
        var used = new HashSet<string>(existing.Select(r => r.Rid));
        var rnd = Random.Shared;
        while (true)
        {
            var nine = rnd.NextInt64(100_000_000L, 1_000_000_000L - 1);
            var drid = $"D{nine}";
            if (!used.Contains(drid)) return drid;
        }
    }
}
EOF

echo "==> Infrastructure"
# Infrastructure/JsonStorage.cs
cat > "$APP/Infrastructure/JsonStorage.cs" <<'EOF'
namespace EzKzr.MockServer.Infrastructure;

using System.Text.Json;

internal static class JsonStorage
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    internal static List<T> LoadList<T>(string dir, string fileName, Func<List<T>> seed)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var res = JsonSerializer.Deserialize<List<T>>(json, JsonOpts);
                if (res is not null) return res;
            }
            catch { }
        }
        var seeded = seed();
        File.WriteAllText(path, JsonSerializer.Serialize(seeded, JsonOpts));
        return seeded;
    }

    internal static void SaveList<T>(string dir, string fileName, IEnumerable<T> list)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(list, JsonOpts));
    }
}
EOF

# Infrastructure/Db.cs
cat > "$APP/Infrastructure/Db.cs" <<'EOF'
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
EOF

echo "==> NuGet (Swagger) – pokud ještě není"
dotnet add "$APP/EzKzr.MockServer.csproj" package Swashbuckle.AspNetCore >/dev/null 2>&1 || true

echo "==> Hotovo. Nyní build:"
dotnet build "$ROOT"
EOF