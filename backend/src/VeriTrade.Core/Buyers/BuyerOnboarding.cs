using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Nethereum.Signer;
using Nethereum.Util;
using VeriTrade.Core.Identity;

namespace VeriTrade.Core.Buyers;

/// <summary>A buyer company asks for a wallet to be allowed to confirm receivables on its behalf.</summary>
public sealed record BuyerSignerRequest(Guid Id, string TaxCode, string CompanyName, string Wallet, byte[] BuyerId, DateTimeOffset CreatedAt);

public interface IBuyerRequestStore
{
    bool TryAdd(BuyerSignerRequest request);
    IReadOnlyList<BuyerSignerRequest> List();
}

/// <summary>Demo store; swap for Postgres. Keyed by (buyerId, wallet) so a repeat request is idempotent.</summary>
public sealed class InMemoryBuyerRequestStore : IBuyerRequestStore
{
    private readonly ConcurrentDictionary<string, BuyerSignerRequest> _byKey = new();
    public const int MaxRequests = 10_000;

    public bool TryAdd(BuyerSignerRequest r) =>
        _byKey.Count < MaxRequests && _byKey.TryAdd(Convert.ToHexString(r.BuyerId) + "|" + r.Wallet.ToLowerInvariant(), r);

    public IReadOnlyList<BuyerSignerRequest> List() => _byKey.Values.OrderBy(r => r.CreatedAt).ToList();
}

public sealed class BuyerOnboardingException(string message) : Exception(message);

/// <summary>
/// Collects signer requests. It never writes to the chain: an operator holding REGISTRAR_ROLE reviews the company
/// (KYB, off-chain) and calls BuyerRegistry.addSigner from their own wallet. Otherwise a seller could make its own
/// wallet a "buyer signer" and confirm its own invoices, which is exactly the fraud the registry exists to stop.
/// </summary>
public sealed class BuyerOnboardingService(ReceivableIdService ids, IBuyerRequestStore store, TimeProvider clock)
{
    // Vietnamese MST: 10 digits, or 10 digits + "-" + 3-digit branch suffix.
    private static readonly Regex TaxCodePattern = new(@"^\d{10}(-\d{3})?$", RegexOptions.Compiled);

    /// <summary>The exact text the wallet signs (EIP-191 personal_sign); the web builds the same string.</summary>
    public static string ProofMessage(string taxCode, string wallet) =>
        $"VeriTrade: yeu cau quyen ky xac nhan cho MST {taxCode}\nVi: {wallet.ToLowerInvariant()}";

    public BuyerSignerRequest Submit(string taxCode, string companyName, string wallet, string signature)
    {
        taxCode = (taxCode ?? "").Trim();
        companyName = (companyName ?? "").Trim();
        if (!TaxCodePattern.IsMatch(taxCode)) throw new BuyerOnboardingException("MST phải có 10 chữ số, hoặc 10 chữ số kèm -XXX cho chi nhánh.");
        if (companyName.Length is < 2 or > 200) throw new BuyerOnboardingException("Tên công ty không hợp lệ.");
        if (!AddressUtil.Current.IsValidEthereumAddressHexFormat(wallet ?? "")) throw new BuyerOnboardingException("Địa chỉ ví không hợp lệ.");

        string recovered;
        try { recovered = new EthereumMessageSigner().EncodeUTF8AndEcRecover(ProofMessage(taxCode, wallet!), signature); }
        catch (Exception) { throw new BuyerOnboardingException("Chữ ký không hợp lệ."); }
        if (!AddressUtil.Current.AreAddressesTheSame(recovered, wallet)) throw new BuyerOnboardingException("Chữ ký không khớp với ví.");

        var request = new BuyerSignerRequest(Guid.NewGuid(), taxCode, companyName, AddressUtil.Current.ConvertToChecksumAddress(wallet),
            ids.BuyerId(taxCode), clock.GetUtcNow());
        if (!store.TryAdd(request))
            return store.List().First(r => r.BuyerId.AsSpan().SequenceEqual(request.BuyerId) && AddressUtil.Current.AreAddressesTheSame(r.Wallet, wallet));
        return request;
    }
}
