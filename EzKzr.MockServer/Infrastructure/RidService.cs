using System.Security.Cryptography;
using System.Text;
using EzKzr.MockServer.Models;

namespace EzKzr.MockServer.Infrastructure;

public static class RidService
{
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    public static string Generate(IEnumerable<RidRecord> existing)
    {
        while (true)
        {
            var rid = TenDigits();
            if (rid[0] == '0') continue;
            if (!long.TryParse(rid, out var n)) continue;
            if (n % 13 != 0) continue;       // musí být dělitelné 13
            if (n % 11 == 0) continue;       // nesmí být dělitelné 11
            if (existing.Any(r => r.Rid == rid)) continue;
            return rid;
        }
    }

    private static string TenDigits()
    {
        var b = new byte[10];
        Rng.GetBytes(b);
        var sb = new StringBuilder(10);
        for (int i = 0; i < 10; i++) sb.Append((b[i] % 10).ToString());
        return sb.ToString();
    }
}
