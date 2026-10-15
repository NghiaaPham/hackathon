.PHONY: deps build test fmt slither
deps:
	cd contracts && forge install foundry-rs/forge-std OpenZeppelin/openzeppelin-contracts@v5.4.0 --no-git
build:
	cd contracts && forge build
test:
	cd contracts && forge test -vv
fmt:
	cd contracts && forge fmt
slither:
	cd contracts && slither . --filter-paths "lib/"
