using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Web3;

namespace VeriTrade.Core.Chain;

/// <summary>Nethereum client for ReceivableRegistry. The VERIFIER key comes from a secret store, never config files.</summary>
public sealed class RegistryClient(IWeb3 web3, string registryAddress) : IRegistryClient
{
    public async Task<OnChainReceivable> GetAsync(byte[] receivableId, CancellationToken ct = default)
    {
        var handler = web3.Eth.GetContractQueryHandler<GetReceivableFunction>();
        var r = (await handler.QueryDeserializingToObjectAsync<GetReceivableOutput>(
            new GetReceivableFunction { ReceivableId = receivableId }, registryAddress)).Value;
        return new OnChainReceivable(
            Convert.ToHexString(r.BuyerId).ToLowerInvariant(), r.Seller, (long)r.Amount, r.DueDate, r.KeyVersion,
            (ReceivableStatus)r.Status, r.Disputed, r.Overdue, r.PaymentHeld, r.Funder,
            r.Terms.AdvanceBps, (long)r.Terms.FinancingFee, r.Terms.PlatformFeeBps);
    }

    public async Task<string> RegisterAsync(byte[] receivableId, byte[] buyerId, string seller, long amount, ulong dueDate, byte keyVersion, CancellationToken ct = default)
    {
        var handler = web3.Eth.GetContractTransactionHandler<RegisterFunction>();
        var receipt = await handler.SendRequestAndWaitForReceiptAsync(registryAddress, new RegisterFunction
        {
            ReceivableId = receivableId, BuyerId = buyerId, Seller = seller,
            Amount = amount, DueDate = dueDate, KeyVersion = keyVersion,
        }, ct);
        return EnsureSuccess(receipt);
    }

    public async Task<string> MarkVerifiedAsync(byte[] receivableId, CancellationToken ct = default)
    {
        var handler = web3.Eth.GetContractTransactionHandler<MarkVerifiedFunction>();
        var receipt = await handler.SendRequestAndWaitForReceiptAsync(registryAddress,
            new MarkVerifiedFunction { ReceivableId = receivableId }, ct);
        return EnsureSuccess(receipt);
    }

    private static string EnsureSuccess(Nethereum.RPC.Eth.DTOs.TransactionReceipt receipt) =>
        receipt.Status?.Value == 1 ? receipt.TransactionHash
            : throw new InvalidOperationException($"Transaction {receipt.TransactionHash} reverted.");

    // ---- ABI messages (mirror IReceivableRegistry.sol) ----

    [Function("register")]
    public sealed class RegisterFunction : FunctionMessage
    {
        [Parameter("bytes32", "receivableId", 1)] public byte[] ReceivableId { get; set; } = [];
        [Parameter("bytes32", "buyerId", 2)] public byte[] BuyerId { get; set; } = [];
        [Parameter("address", "seller", 3)] public string Seller { get; set; } = "";
        [Parameter("uint256", "amount", 4)] public BigInteger Amount { get; set; }
        [Parameter("uint64", "dueDate", 5)] public ulong DueDate { get; set; }
        [Parameter("uint8", "keyVersion", 6)] public byte KeyVersion { get; set; }
    }

    [Function("markVerified")]
    public sealed class MarkVerifiedFunction : FunctionMessage
    {
        [Parameter("bytes32", "receivableId", 1)] public byte[] ReceivableId { get; set; } = [];
    }

    [Function("getReceivable", typeof(GetReceivableOutput))]
    public sealed class GetReceivableFunction : FunctionMessage
    {
        [Parameter("bytes32", "receivableId", 1)] public byte[] ReceivableId { get; set; } = [];
    }

    [FunctionOutput]
    public sealed class GetReceivableOutput : IFunctionOutputDTO
    {
        [Parameter("tuple", "", 1)] public ReceivableDto Value { get; set; } = new();
    }

    public sealed class ReceivableDto
    {
        [Parameter("bytes32", "buyerId", 1)] public byte[] BuyerId { get; set; } = [];
        [Parameter("address", "seller", 2)] public string Seller { get; set; } = "";
        [Parameter("uint256", "amount", 3)] public BigInteger Amount { get; set; }
        [Parameter("uint64", "dueDate", 4)] public ulong DueDate { get; set; }
        [Parameter("uint8", "keyVersion", 5)] public byte KeyVersion { get; set; }
        [Parameter("uint8", "status", 6)] public byte Status { get; set; }
        [Parameter("bool", "disputed", 7)] public bool Disputed { get; set; }
        [Parameter("bool", "overdue", 8)] public bool Overdue { get; set; }
        [Parameter("bool", "paymentHeld", 9)] public bool PaymentHeld { get; set; }
        [Parameter("address", "funder", 10)] public string Funder { get; set; } = "";
        [Parameter("tuple", "terms", 11)] public TermsDto Terms { get; set; } = new();
    }

    public sealed class TermsDto
    {
        [Parameter("uint16", "advanceBps", 1)] public ushort AdvanceBps { get; set; }
        [Parameter("uint256", "financingFee", 2)] public BigInteger FinancingFee { get; set; }
        [Parameter("uint16", "platformFeeBps", 3)] public ushort PlatformFeeBps { get; set; }
    }
}
