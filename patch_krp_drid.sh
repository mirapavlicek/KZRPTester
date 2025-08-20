set -euo pipefail

# 1) Vypnout kolizní Infrastructure/Db.cs
applypatch <<'PATCH'
*** Begin Patch
*** Update File: EzKzr.MockServer/Infrastructure/Db.cs
@@
+#if false
 /* původní obsah dočasně vypnut kvůli kolizi s Db v Program.cs.
    Refaktor proběhne v další iteraci. */
+#endif
*** End Patch
PATCH

# 2) Přidání SavePatientSummaries do Program.cs (pokud tam ještě není)
grep -q 'SavePatientSummaries' EzKzr.MockServer/Program.cs || \
  gsed -i '/SaveNotifications/a\\n    public static void SavePatientSummaries(IEnumerable<PatientSummary> items) => SaveList("ps.json", items);' EzKzr.MockServer/Program.cs || \
  sed -i '' '/SaveNotifications/a\
\
    public static void SavePatientSummaries(IEnumerable<PatientSummary> items) => SaveList("ps.json", items);' EzKzr.MockServer/Program.cs

echo "Hotovo. Teď: dotnet build"
