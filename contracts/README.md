# Contracts (Foundry)

```bash
make deps   # from repo root
forge test -vv
```

| File | Role |
|---|---|
| `src/interfaces/IReceivableRegistry.sol` | Shared interface for contracts, backend, web |
| `src/ReceivableRegistry.sol` | Shared registry: register, verify, EIP-712 confirm, atomic fund, pull-payment settle, disputes |
| `src/BuyerRegistry.sol` | buyerId to authorised signers (EOA or ERC-1271) |
| `src/mocks/MockTVND.sol` | Test token, permit + faucet, 0 decimals |
| `test/` | Unit, fuzz and invariant tests |
| `script/Deploy.s.sol` | Deploy to Fuji or the L1 |

Demo numbers: F = 100M tVND, advance 80%, funder fee 4M, platform 0.5%, seller remainder 15.5M.
