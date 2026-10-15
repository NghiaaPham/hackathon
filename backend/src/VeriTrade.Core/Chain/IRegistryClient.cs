namespace VeriTrade.Core.Chain;

public enum ReceivableStatus : byte { None, Registered, Verified, BuyerConfirmed, Funded, Settled, Rejected }

public sealed record OnChainReceivable(
    string BuyerId, string Seller, long Amount, ulong DueDate, byte KeyVersion,
    ReceivableStatus Status, bool Disputed, bool Overdue, bool PaymentHeld, string Funder,
    ushort AdvanceBps, long FinancingFee, ushort PlatformFeeBps);

public interface IRegistryClient
{
    Task<OnChainReceivable> GetAsync(byte[] receivableId, CancellationToken ct = default);
    Task<string> RegisterAsync(byte[] receivableId, byte[] buyerId, string seller, long amount, ulong dueDate, byte keyVersion, CancellationToken ct = default);
    Task<string> MarkVerifiedAsync(byte[] receivableId, CancellationToken ct = default);
}
