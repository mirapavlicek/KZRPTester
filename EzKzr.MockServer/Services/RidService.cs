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
