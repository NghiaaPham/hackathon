using VeriTrade.Core.Identity;
using VeriTrade.Core.Xml;

namespace VeriTrade.Tests;

public class ReceivableIdServiceTests
{
    private static readonly byte[] K1 = Enumerable.Repeat((byte)1, 32).ToArray();
    private static readonly byte[] K2 = Enumerable.Repeat((byte)2, 32).ToArray();
    private readonly ReceivableIdService _ids = new(new Dictionary<byte, byte[]> { [1] = K1 }, 1);

    private static InvoiceData Inv(string seller = "0312345678", string series = "C25TAA", string number = "0000123") =>
        new(seller, "A", "0109876543", "B", "1", series, number, new DateOnly(2025, 10, 1), 100_000_000, null);

    [Fact]
    public void SameInvoice_SameId() => Assert.Equal(_ids.ReceivableId(Inv()), _ids.ReceivableId(Inv()));

    [Fact]
    public void WhitespaceCaseAndLeadingZeros_Normalized() =>
        Assert.Equal(_ids.ReceivableId(Inv()), _ids.ReceivableId(Inv(series: " c25taa ", number: "123")));

    [Fact]
    public void DifferentNumber_DifferentId() =>
        Assert.NotEqual(_ids.ReceivableId(Inv()), _ids.ReceivableId(Inv(number: "124")));

    [Fact]
    public void Canonical_Format() => Assert.Equal("0312345678|1|C25TAA|123", ReceivableIdService.Canonical(Inv()));

    [Fact]
    public void BuyerIdAndReceivableId_AreDomainSeparated() =>
        Assert.NotEqual(_ids.BuyerId("0312345678|1|C25TAA|123"), _ids.ReceivableId(Inv()));

    [Fact]
    public void DifferentKeyVersion_DifferentId()
    {
        var both = new ReceivableIdService(new Dictionary<byte, byte[]> { [1] = K1, [2] = K2 }, 2);
        var all = both.AllReceivableIds(Inv()).ToList();
        Assert.Equal(2, all.Count);
        Assert.NotEqual(all[0].Id, all[1].Id);
        Assert.Equal(_ids.ReceivableId(Inv()), all.Single(x => x.Version == 1).Id);
    }

    [Fact]
    public void ShortKey_Throws() =>
        Assert.Throws<ArgumentException>(() => new ReceivableIdService(new Dictionary<byte, byte[]> { [1] = new byte[16] }, 1));
}
