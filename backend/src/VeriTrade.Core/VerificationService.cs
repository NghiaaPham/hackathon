using VeriTrade.Core.Chain;
using System.Security.Cryptography;
using VeriTrade.Core.Identity;
using VeriTrade.Core.Storage;
using VeriTrade.Core.Xml;

namespace VeriTrade.Core;

public sealed record RegistrationRequest(Stream Xml, string SellerWallet, DateTimeOffset DueDate);

public sealed record RegistrationResult(
    string ReceivableId, string BuyerId, byte KeyVersion, InvoiceData Invoice,
    string RegisterTx, string VerifyTx);

public sealed class DuplicateReceivableException(string receivableId)
    : Exception($"Receivable {receivableId} is already registered in the network.")
{
    public string ReceivableId { get; } = receivableId;
}

/// <summary>XML in, verified receivable on-chain out. The only path by which the VERIFIER wallet signs.</summary>
public sealed class VerificationService(InvoiceVerifier verifier, ReceivableIdService ids, IRegistryClient registry, IReceivableStore store, TimeProvider clock)
{
    public async Task<RegistrationResult> RegisterAsync(RegistrationRequest req, CancellationToken ct = default)
    {
        if (!Nethereum.Util.AddressUtil.Current.IsValidEthereumAddressHexFormat(req.SellerWallet))
            throw new ArgumentException("Seller wallet is not a valid address.");
        if (req.DueDate <= clock.GetUtcNow())
            throw new ArgumentException("Due date must be in the future.");

        var xml = await ReadBoundedAsync(req.Xml, ct);
        var invoice = verifier.Verify(new MemoryStream(xml, writable: false)).Data;

        // Check every active key version so a rotation can never let a duplicate through.
        foreach (var (_, existingId) in ids.AllReceivableIds(invoice))
        {
            var onChain = await registry.GetAsync(existingId, ct);
            if (onChain.Status != ReceivableStatus.None)
                throw new DuplicateReceivableException(Hex(existingId));
        }

        var rid = ids.ReceivableId(invoice);
        var buyerId = ids.BuyerId(invoice.BuyerTaxCode);
        var registerTx = await registry.RegisterAsync(rid, buyerId, req.SellerWallet, invoice.TotalAmount,
            (ulong)req.DueDate.ToUnixTimeSeconds(), ids.CurrentVersion, ct);
        var verifyTx = await registry.MarkVerifiedAsync(rid, ct);

        await store.SaveAsync(new ReceivableRecord(rid, buyerId, ids.CurrentVersion, req.SellerWallet,
            invoice.SellerTaxCode, invoice.SellerName, invoice.BuyerTaxCode, invoice.BuyerName, invoice.TemplateCode,
            invoice.Series, invoice.Number, invoice.IssueDate, invoice.TotalAmount, invoice.TaxAuthorityCode,
            req.DueDate, SHA256.HashData(xml), xml, registerTx, verifyTx, clock.GetUtcNow()), ct);

        return new RegistrationResult(Hex(rid), Hex(buyerId), ids.CurrentVersion, invoice, registerTx, verifyTx);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream s, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buf = new byte[81920];
        int n;
        while ((n = await s.ReadAsync(buf, ct)) > 0)
        {
            if (ms.Length + n > SafeXml.MaxBytes)
                throw new InvoiceVerificationException(VerificationError.MalformedXml, "Invoice XML exceeds 2 MB.");
            ms.Write(buf, 0, n);
        }
        return ms.ToArray();
    }

    public static string Hex(byte[] b) => "0x" + Convert.ToHexString(b).ToLowerInvariant();
}
