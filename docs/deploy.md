# Deploy

## Local (anvil)
```bash
make anvil                 # terminal 1
ADMIN_ADDRESS=0x7099...79C8 TREASURY_ADDRESS=0x3C44...93BC VERIFIER_ADDRESS=0x90F7...b906 make deploy-local
```

## Fuji C-Chain (judged build)
1. Create a dedicated testnet deployer: `cast wallet import deployer --interactive` (key stays in the encrypted keystore).
2. Get test AVAX: Builder Hub faucet, https://build.avax.network/console/primary-network/faucet
3. Fill `ADMIN_ADDRESS`, `TREASURY_ADDRESS`, `VERIFIER_ADDRESS` in `.env`.
4. `make deploy-fuji` (deploys tVND, BuyerRegistry, ReceivableRegistry and verifies on Routescan/Snowtrace).
5. Record the addresses below and run `make abi`.

| Contract | Fuji address | Explorer |
|---|---|---|
| tVND | _pending_ | |
| BuyerRegistry | _pending_ | |
| ReceivableRegistry | _pending_ | |

After deploy: grant `FUNDER_ROLE` to demo funders and `ARBITER_ROLE` to the arbiter; add buyer signers in BuyerRegistry.
