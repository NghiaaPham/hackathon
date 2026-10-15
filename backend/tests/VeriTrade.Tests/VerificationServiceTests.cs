using VeriTrade.Core;
using VeriTrade.Core.Chain;
using System.Security.Cryptography;
using System.Text;
using VeriTrade.Core.Identity;
using VeriTrade.Core.Storage;
using VeriTrade.Core.Xml;

namespace VeriTrade.Tests;

public class VerificationServiceTests
{
    private const string Seller = "0x1111111111111111111111111111111111111111";

    private sealed class FakeRegistry : IRegistryClient
    {
        public readonly Dictionary<string, ReceivableStatus> State = new();
        public int Writes;

        public Task<OnChainReceivable> GetAsync(byte[] id, CancellationToken ct = default) =>
            Task.FromResult(new OnChainReceivable("", "", 0, 0, 0,
                State.GetValueOrDefault(Convert.ToHexString(id)), false, false, false, "", 0, 0, 0));

        public Task<string> RegisterAsync(byte[] id, byte[] buyerId, string seller, long amount, ulong due, byte v, CancellationToken ct = default)
        {
            Writes++;
            State[Convert.ToHexString(id)] = ReceivableStatus.Registered;
            return Task.FromResult("0xreg");
        }

        public Task<string> MarkVerifiedAsync(byte[] id, CancellationToken ct = default)
        {
            Writes++;
            State[Convert.ToHexString(id)] = ReceivableStatus.Verified;
            return Task.FromResult("0xver");
        }
    }

    private static (VerificationService svc, FakeRegistry reg) Make(IReceivableStore? store = null)
    {
        var reg = new FakeRegistry();
        var ids = new ReceivableIdService(new Dictionary<byte, byte[]> { [1] = new byte[32] }, 1);
        return (new VerificationService(new InvoiceVerifier(new()), ids, reg, store ?? new InMemoryReceivableStore(), TimeProvider.System), reg);
    }

    [Fact]
    public async Task ValidInvoice_OffChainRecordKeepsXmlAndParties()
    {
        var store = new InMemoryReceivableStore();
        var (svc, _) = Make(store);
        var xml = InvoiceFactory.Signed();
        var r = await svc.RegisterAsync(Req(xml));
        var saved = await store.GetAsync(Convert.FromHexString(r.ReceivableId[2..]));
        Assert.NotNull(saved);
        Assert.Equal(r.Invoice.BuyerTaxCode, saved!.BuyerTaxCode);
        Assert.Equal(SHA256.HashData(saved.Xml), saved.XmlSha256);
        Assert.Equal(r.VerifyTx, saved.VerifyTx);
    }

    [Fact]
    public async Task OversizedXml_RejectedBeforeParsing()
    {
        var (svc, reg) = Make();
        var big = new MemoryStream(Encoding.UTF8.GetBytes("<a>" + new string('x', SafeXml.MaxBytes) + "</a>"));
        await Assert.ThrowsAsync<InvoiceVerificationException>(() =>
            svc.RegisterAsync(new RegistrationRequest(big, Seller, DateTimeOffset.UtcNow.AddDays(9))));
        Assert.Equal(0, reg.Writes);
    }

    private static RegistrationRequest Req(string xml) =>
        new(InvoiceFactory.ToStream(xml), Seller, DateTimeOffset.UtcNow.AddDays(90));

    [Fact]
    public async Task ValidInvoice_RegistersAndVerifies()
    {
        var (svc, reg) = Make();
        var r = await svc.RegisterAsync(Req(InvoiceFactory.Signed()));
        Assert.StartsWith("0x", r.ReceivableId);
        Assert.Equal(66, r.ReceivableId.Length);
        Assert.Equal(2, reg.Writes);
        Assert.Equal(100_000_000, r.Invoice.TotalAmount);
    }

    [Fact]
    public async Task SameInvoiceTwice_IsDuplicate_AndSendsNoTransaction()
    {
        var (svc, reg) = Make();
        await svc.RegisterAsync(Req(InvoiceFactory.Signed()));
        // Same invoice re-exported with a different number format still maps to the same id.
        await Assert.ThrowsAsync<DuplicateReceivableException>(() => svc.RegisterAsync(Req(InvoiceFactory.Signed(number: "123"))));
        Assert.Equal(2, reg.Writes);
    }

    [Fact]
    public async Task InvalidXml_SendsNoTransaction()
    {
        var (svc, reg) = Make();
        await Assert.ThrowsAsync<InvoiceVerificationException>(() => svc.RegisterAsync(Req(InvoiceFactory.UnsignedXml())));
        Assert.Equal(0, reg.Writes);
    }

    [Fact]
    public async Task PastDueDate_Rejected()
    {
        var (svc, _) = Make();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.RegisterAsync(
            new RegistrationRequest(InvoiceFactory.ToStream(InvoiceFactory.Signed()), Seller, DateTimeOffset.UtcNow.AddDays(-1))));
    }

    [Fact]
    public async Task BadSellerWallet_Rejected()
    {
        var (svc, _) = Make();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.RegisterAsync(
            new RegistrationRequest(InvoiceFactory.ToStream(InvoiceFactory.Signed()), "not-an-address", DateTimeOffset.UtcNow.AddDays(9))));
    }
}
