using System.Collections.Concurrent;
using VeriTrade.Core.Buyers;

namespace VeriTrade.Core.Storage;

/// <summary>
/// Off-chain record of a registered invoice. The chain stays the source of truth for status and money;
/// this keeps what must not go on-chain (tax codes, names, the signed XML) for audit and dispute evidence.
/// </summary>
public sealed record ReceivableRecord(
    byte[] ReceivableId, byte[] BuyerId, byte KeyVersion, string SellerWallet,
    string SellerTaxCode, string SellerName, string BuyerTaxCode, string BuyerName,
    string TemplateCode, string Series, string Number, DateOnly IssueDate, long TotalAmount, string? TaxAuthorityCode,
    DateTimeOffset DueDate, byte[] XmlSha256, byte[] Xml, string RegisterTx, string VerifyTx, DateTimeOffset CreatedAt);

public interface IReceivableStore
{
    Task SaveAsync(ReceivableRecord record, CancellationToken ct = default);
    Task<ReceivableRecord?> GetAsync(byte[] receivableId, CancellationToken ct = default);
}

public interface IBuyerRequestStore
{
    /// <summary>Adds the request, or returns the existing one for the same (buyerId, wallet).</summary>
    Task<BuyerSignerRequest> AddOrGetAsync(BuyerSignerRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<BuyerSignerRequest>> ListAsync(CancellationToken ct = default);
}

/// <summary>Demo/test store, used when no Postgres connection string is configured.</summary>
public sealed class InMemoryReceivableStore : IReceivableStore
{
    private readonly ConcurrentDictionary<string, ReceivableRecord> _items = new();
    public Task SaveAsync(ReceivableRecord r, CancellationToken ct = default)
    {
        _items[Convert.ToHexString(r.ReceivableId)] = r;
        return Task.CompletedTask;
    }
    public Task<ReceivableRecord?> GetAsync(byte[] id, CancellationToken ct = default) =>
        Task.FromResult(_items.GetValueOrDefault(Convert.ToHexString(id)));
}

public sealed class InMemoryBuyerRequestStore : IBuyerRequestStore
{
    public const int MaxRequests = 10_000;
    private readonly ConcurrentDictionary<string, BuyerSignerRequest> _byKey = new();

    public Task<BuyerSignerRequest> AddOrGetAsync(BuyerSignerRequest r, CancellationToken ct = default)
    {
        var key = Convert.ToHexString(r.BuyerId) + "|" + r.Wallet.ToLowerInvariant();
        if (_byKey.TryGetValue(key, out var existing)) return Task.FromResult(existing);
        if (_byKey.Count >= MaxRequests) throw new BuyerOnboardingException("Hệ thống đang quá tải yêu cầu, thử lại sau.");
        return Task.FromResult(_byKey.GetOrAdd(key, r));
    }

    public Task<IReadOnlyList<BuyerSignerRequest>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BuyerSignerRequest>>(_byKey.Values.OrderBy(r => r.CreatedAt).ToList());
}
