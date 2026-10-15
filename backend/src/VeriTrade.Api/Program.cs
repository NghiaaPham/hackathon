using Microsoft.AspNetCore.Http.Features;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Security.Cryptography;
using System.Text;
using VeriTrade.Core;
using VeriTrade.Core.Buyers;
using VeriTrade.Core.Chain;
using VeriTrade.Core.Identity;
using VeriTrade.Core.Xml;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// Secrets come from environment / secret store only: VERIFIER_PRIVATE_KEY, HMAC_KEY_V1 (hex, >= 32 bytes).
string Secret(string name) => cfg[name] is { Length: > 0 } v ? v : throw new InvalidOperationException($"{name} is not set.");

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = SafeXml.MaxBytes);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins((cfg["Web:Origins"] ?? "http://localhost:3000").Split(',')).AllowAnyHeader().WithMethods("GET", "POST")));
builder.Services.AddProblemDetails();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new InvoiceVerifierOptions
{
    TrustedThumbprints = new HashSet<string>((cfg["Xml:TrustedThumbprints"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase),
});
builder.Services.AddSingleton<InvoiceVerifier>();
builder.Services.AddSingleton(_ => new ReceivableIdService(
    new Dictionary<byte, byte[]> { [1] = Convert.FromHexString(Secret("HMAC_KEY_V1").Replace("0x", "")) }, currentVersion: 1));
builder.Services.AddSingleton<IRegistryClient>(_ =>
{
    var account = new Account(Secret("VERIFIER_PRIVATE_KEY"), long.Parse(cfg["Chain:ChainId"] ?? "43113"));
    var web3 = new Web3(account, cfg["Chain:RpcUrl"] ?? "https://api.avax-test.network/ext/bc/C/rpc");
    return new RegistryClient(web3, Secret("Chain:RegistryAddress"));
});
builder.Services.AddSingleton<VerificationService>();
builder.Services.AddSingleton<IBuyerRequestStore, InMemoryBuyerRequestStore>();
builder.Services.AddSingleton<BuyerOnboardingService>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Seller uploads the signed XML e-invoice; backend verifies it, then registers + marks it verified on-chain.
app.MapPost("/api/receivables", async (IFormFile file, [Microsoft.AspNetCore.Mvc.FromForm] string sellerWallet,
    [Microsoft.AspNetCore.Mvc.FromForm] DateTimeOffset dueDate, VerificationService svc, CancellationToken ct) =>
{
    try
    {
        await using var stream = file.OpenReadStream();
        var r = await svc.RegisterAsync(new RegistrationRequest(stream, sellerWallet, dueDate), ct);
        return Results.Created($"/api/receivables/{r.ReceivableId}", new
        {
            r.ReceivableId, r.BuyerId, r.KeyVersion, r.RegisterTx, r.VerifyTx,
            invoice = new { r.Invoice.SellerName, r.Invoice.BuyerName, r.Invoice.TotalAmount, r.Invoice.IssueDate, r.Invoice.Series, r.Invoice.Number },
        });
    }
    catch (InvoiceVerificationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 422, title: ex.Error.ToString());
    }
    catch (DuplicateReceivableException ex)
    {
        return Results.Problem(ex.Message, statusCode: 409, title: "Duplicate");
    }
    catch (ArgumentException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400, title: "BadRequest");
    }
}).DisableAntiforgery();

// Status straight from the chain; the web timeline can also read the chain directly.
app.MapGet("/api/receivables/{id}", async (string id, IRegistryClient registry, CancellationToken ct) =>
{
    byte[] bytes;
    try { bytes = Convert.FromHexString(id.Replace("0x", "")); } catch (FormatException) { return Results.BadRequest(); }
    if (bytes.Length != 32) return Results.BadRequest();
    var r = await registry.GetAsync(bytes, ct);
    return r.Status == ReceivableStatus.None ? Results.NotFound() : Results.Ok(r);
});

// Buyer asks for its wallet to become a signer; proof = personal_sign of BuyerOnboardingService.ProofMessage.
app.MapPost("/api/buyers/requests", (BuyerRequestBody body, BuyerOnboardingService svc) =>
{
    try
    {
        var r = svc.Submit(body.TaxCode, body.CompanyName, body.Wallet, body.Signature);
        return Results.Accepted(value: new { requestId = r.Id, status = "pending" });
    }
    catch (BuyerOnboardingException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400, title: "BadRequest");
    }
});

// Operator review list. Disabled unless ADMIN_TOKEN is set; buyerId is only revealed here.
app.MapGet("/api/buyers/requests", (HttpRequest http, IBuyerRequestStore store) =>
{
    var token = cfg["ADMIN_TOKEN"];
    if (string.IsNullOrEmpty(token)) return Results.NotFound();
    var given = http.Headers["X-Admin-Token"].ToString();
    if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(token)))
        return Results.Unauthorized();
    return Results.Ok(store.List().Select(r => new
    {
        r.Id, r.TaxCode, r.CompanyName, r.Wallet, buyerId = "0x" + Convert.ToHexString(r.BuyerId).ToLowerInvariant(), r.CreatedAt,
    }));
});

app.Run();

public sealed record BuyerRequestBody(string TaxCode, string CompanyName, string Wallet, string Signature);

public partial class Program;

