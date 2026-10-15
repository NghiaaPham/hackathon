using System.Security.Cryptography;
using System.Text;
using VeriTrade.Core.Xml;

namespace VeriTrade.Core.Identity;

/// <summary>
/// Deterministic, keyed ids. Same invoice + same key version => same id, so the shared registry
/// rejects a second registration. HMAC is not encryption; the key is a trusted secret kept off-chain.
/// </summary>
public sealed class ReceivableIdService
{
    private readonly IReadOnlyDictionary<byte, byte[]> _keys;
    public byte CurrentVersion { get; }

    public ReceivableIdService(IReadOnlyDictionary<byte, byte[]> keys, byte currentVersion)
    {
        if (!keys.TryGetValue(currentVersion, out var k) || k.Length < 32)
            throw new ArgumentException("Current HMAC key must exist and be at least 32 bytes.");
        _keys = keys;
        CurrentVersion = currentVersion;
    }

    /// <summary>MST người bán | ký hiệu mẫu số | ký hiệu hóa đơn | số hóa đơn, no whitespace, upper case.</summary>
    public static string Canonical(InvoiceData d) =>
        string.Join('|', new[] { d.SellerTaxCode, d.TemplateCode, d.Series, NormalizeNumber(d.Number) }.Select(Clean));

    public byte[] ReceivableId(InvoiceData d, byte? version = null) => Mac(version ?? CurrentVersion, "INV|" + Canonical(d));

    /// <summary>Ids under every active key version, for the rotation check before registering (P1).</summary>
    public IEnumerable<(byte Version, byte[] Id)> AllReceivableIds(InvoiceData d) =>
        _keys.Keys.Select(v => (v, ReceivableId(d, v)));

    public byte[] BuyerId(string buyerTaxCode) => Mac(CurrentVersion, "BUYER|" + Clean(buyerTaxCode));

    private byte[] Mac(byte version, string message)
    {
        if (!_keys.TryGetValue(version, out var key)) throw new ArgumentException($"Unknown key version {version}.");
        return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(message));
    }

    private static string Clean(string s) =>
        new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    // "0000123" and "123" are the same invoice number.
    private static string NormalizeNumber(string n)
    {
        var trimmed = Clean(n).TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }
}
