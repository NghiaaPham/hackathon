.PHONY: deps build test fmt slither abi anvil deploy-local deploy-fuji
-include .env
export

ROUTESCAN_FUJI := https://api.routescan.io/v2/network/testnet/evm/43113/etherscan
DEPLOY_SIG := "run(address,address,address)" $(ADMIN_ADDRESS) $(TREASURY_ADDRESS) $(VERIFIER_ADDRESS)

deps:
	cd contracts && forge install foundry-rs/forge-std OpenZeppelin/openzeppelin-contracts@v5.4.0 --no-git
build:
	cd contracts && forge build
test:
	cd contracts && forge test -vv
fmt:
	cd contracts && forge fmt
slither:
	cd contracts && slither . --filter-paths "lib/|test/"

# ABI JSON for backend (Nethereum) and web (viem) -> shared/abi/
abi: build
	mkdir -p shared/abi
	for c in ReceivableRegistry BuyerRegistry MockTVND; do \
	  (cd contracts && forge inspect $$c abi --json) > shared/abi/$$c.json; done

anvil:
	anvil --chain-id 31337

# Anvil default key #0, local only
deploy-local:
	cd contracts && forge script script/Deploy.s.sol --rpc-url http://127.0.0.1:8545 --broadcast \
	  --private-key 0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80 --sig $(DEPLOY_SIG)

# Requires: cast wallet import deployer --interactive ; ADMIN/TREASURY/VERIFIER_ADDRESS in .env
deploy-fuji:
	cd contracts && forge script script/Deploy.s.sol --rpc-url fuji --broadcast --account deployer \
	  --verify --verifier-url $(ROUTESCAN_FUJI) --etherscan-api-key verifyContract --sig $(DEPLOY_SIG)
