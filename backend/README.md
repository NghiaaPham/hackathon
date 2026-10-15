# Backend (ASP.NET Core 8)

Verifies signed Vietnamese e-invoice XML, derives HMAC ids, and is the only `VERIFIER` wallet that calls
`register` + `markVerified` on `ReceivableRegistry`.

```bash
cd backend
dotnet test                                                        # 27 tests
dotnet run --project tools/VeriTrade.SampleInvoice -- samples 3    # signed demo invoices
HMAC_KEY_V1=<64 hex> VERIFIER_PRIVATE_KEY=<key> Chain__RegistryAddress=<addr> dotnet run --project src/VeriTrade.Api
```

| Endpoint | |
|---|---|
| `POST /api/receivables` | multipart: `file` (XML), `sellerWallet`, `dueDate` (ISO 8601). 201, 409 duplicate, 422 invalid XML/signature |
| `GET /api/receivables/{id}` | status read from chain |
| `GET /health` | |

## XML defences
- DTD and external entities prohibited (XXE, billion laughs), 2 MB limit
- Exactly one `DLHDon` directly under `HDon`, carrying an `Id`; exactly one seller signature in `DSCKS/NBan`
- Signature reference must be `#<Id of DLHDon>`; ids must be unique (signature wrapping)
- Only enveloped/C14N transforms; data is read only from the signed element
- Certificate subject must contain the seller MST; optional trusted thumbprint/CA allowlist (`Xml:TrustedThumbprints`)

## Ids
`receivableId = HMAC-SHA256(k_v, "INV|" + MST|KHMSHDon|KHHDon|SHDon)` (whitespace removed, upper case, leading zeros of the number dropped).
`buyerId = HMAC-SHA256(k_v, "BUYER|" + buyer MST)`. Before registering, the id under every active key version is checked on-chain.

Secrets (`HMAC_KEY_V1`, `VERIFIER_PRIVATE_KEY`) come from environment/secret store only.
