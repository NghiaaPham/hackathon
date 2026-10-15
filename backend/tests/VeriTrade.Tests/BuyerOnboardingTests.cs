using Nethereum.Signer;
using VeriTrade.Core.Buyers;
using VeriTrade.Core.Identity;

namespace VeriTrade.Tests;

public class BuyerOnboardingTests
{
    private static readonly ReceivableIdService Ids = new(new Dictionary<byte, byte[]> { [1] = Enumerable.Repeat((byte)7, 32).ToArray() }, 1);
    private readonly InMemoryBuyerRequestStore _store = new();
    private readonly BuyerOnboardingService _svc;
    private readonly EthECKey _key = EthECKey.GenerateKey();
    private string Wallet => _key.GetPublicAddress();

    public BuyerOnboardingTests() => _svc = new BuyerOnboardingService(Ids, _store, TimeProvider.System);

    private string Sign(string taxCode, string wallet, EthECKey? key = null) =>
        new EthereumMessageSigner().EncodeUTF8AndSign(BuyerOnboardingService.ProofMessage(taxCode, wallet), key ?? _key);

    [Fact]
    public void ValidRequest_StoredWithSameBuyerIdAsInvoices()
    {
        var r = _svc.Submit("0109876543", "Cong ty B", Wallet, Sign("0109876543", Wallet));
        Assert.Equal(Ids.BuyerId("0109876543"), r.BuyerId);
        Assert.Single(_store.List());
    }

    [Fact]
    public void BranchTaxCode_Accepted() =>
        Assert.NotNull(_svc.Submit("0109876543-001", "Chi nhanh", Wallet, Sign("0109876543-001", Wallet)));

    [Theory]
    [InlineData("123")]
    [InlineData("01098765431")]
    [InlineData("0109876543-1")]
    [InlineData("abcdefghij")]
    public void BadTaxCode_Rejected(string mst) =>
        Assert.Throws<BuyerOnboardingException>(() => _svc.Submit(mst, "Cong ty B", Wallet, Sign(mst, Wallet)));

    [Fact]
    public void SignatureFromAnotherWallet_Rejected()
    {
        var other = EthECKey.GenerateKey();
        Assert.Throws<BuyerOnboardingException>(() => _svc.Submit("0109876543", "Cong ty B", Wallet, Sign("0109876543", Wallet, other)));
    }

    [Fact]
    public void SignatureForAnotherTaxCode_Rejected() =>
        Assert.Throws<BuyerOnboardingException>(() => _svc.Submit("0109876543", "Cong ty B", Wallet, Sign("0312345678", Wallet)));

    [Fact]
    public void GarbageSignature_Rejected() =>
        Assert.Throws<BuyerOnboardingException>(() => _svc.Submit("0109876543", "Cong ty B", Wallet, "0x1234"));

    [Fact]
    public void RepeatRequest_Idempotent()
    {
        var a = _svc.Submit("0109876543", "Cong ty B", Wallet, Sign("0109876543", Wallet));
        var b = _svc.Submit("0109876543", "Cong ty B", Wallet.ToLowerInvariant(), Sign("0109876543", Wallet.ToLowerInvariant()));
        Assert.Equal(a.Id, b.Id);
        Assert.Single(_store.List());
    }
}
