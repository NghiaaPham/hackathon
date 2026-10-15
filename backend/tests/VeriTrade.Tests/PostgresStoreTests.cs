using Npgsql;
using VeriTrade.Core.Buyers;
using VeriTrade.Core.Storage;

namespace VeriTrade.Tests;

/// <summary>
/// Runs against a real Postgres when VERITRADE_TEST_PG is set (CI starts a service container);
/// otherwise each test returns immediately.
/// </summary>
public class PostgresStoreTests
{
    private static readonly string? Conn = Environment.GetEnvironmentVariable("VERITRADE_TEST_PG");

    private static async Task<NpgsqlDataSource> Db()
    {
        var db = NpgsqlDataSource.Create(Conn!);
        await PostgresSchema.EnsureAsync(db);
        await PostgresSchema.EnsureAsync(db); // idempotent
        return db;
    }

    private static byte[] Rand32() => System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

    [Fact]
    public async Task Receivable_RoundTrip_AndUpsertIsIdempotent()
    {
        if (Conn is null) return;
        await using var db = await Db();
        var store = new PostgresReceivableStore(db);
        var id = Rand32();
        var rec = new ReceivableRecord(id, Rand32(), 1, "0x1111111111111111111111111111111111111111",
            "0312345678", "Cong ty A", "0109876543", "Cong ty B", "1", "C25TAA", "0000123", new DateOnly(2026, 10, 1),
            100_000_000, null, new DateTimeOffset(2027, 1, 15, 0, 0, 0, TimeSpan.Zero), Rand32(), [1, 2, 3], "0xaa", "0xbb",
            DateTimeOffset.UtcNow);
        await store.SaveAsync(rec);
        await store.SaveAsync(rec with { VerifyTx = "0xcc" });
        var got = await store.GetAsync(id);
        Assert.NotNull(got);
        Assert.Equal(rec.BuyerTaxCode, got!.BuyerTaxCode);
        Assert.Equal(rec.IssueDate, got.IssueDate);
        Assert.Equal(rec.DueDate, got.DueDate);
        Assert.Equal(rec.Xml, got.Xml);
        Assert.Null(got.TaxAuthorityCode);
        Assert.Equal("0xcc", got.VerifyTx);
        Assert.Null(await store.GetAsync(Rand32()));
    }

    [Fact]
    public async Task BuyerRequest_SameBuyerAndWallet_ReturnsExisting_CaseInsensitive()
    {
        if (Conn is null) return;
        await using var db = await Db();
        var store = new PostgresBuyerRequestStore(db);
        var buyerId = Rand32();
        var a = await store.AddOrGetAsync(new BuyerSignerRequest(Guid.NewGuid(), "0109876543", "B", "0xAbCd000000000000000000000000000000000001", buyerId, DateTimeOffset.UtcNow));
        var b = await store.AddOrGetAsync(new BuyerSignerRequest(Guid.NewGuid(), "0109876543", "B", "0xabcd000000000000000000000000000000000001", buyerId, DateTimeOffset.UtcNow));
        Assert.Equal(a.Id, b.Id);
        Assert.Contains(await store.ListAsync(), r => r.Id == a.Id);
    }
}
