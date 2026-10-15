#!/usr/bin/env bash
# Local end-to-end: anvil + Deploy.s.sol + API + sample invoice. Expect 201, 409 duplicate, 422 tampered.
# Keys below are the public Anvil test accounts. Never use them on Fuji.
set -u
export PATH=$HOME/.foundry/bin:$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
export NO_PROXY=127.0.0.1,localhost no_proxy=127.0.0.1,localhost
cd "$(dirname "$0")/../contracts"
[ -d lib/forge-std ] || forge install foundry-rs/forge-std OpenZeppelin/openzeppelin-contracts@v5.4.0 --no-git >/dev/null 2>&1
anvil --silent --chain-id 31337 > /tmp/anvil.log 2>&1 & ANVIL=$!
sleep 3
forge script script/Deploy.s.sol --rpc-url http://127.0.0.1:8545 --broadcast --private-key 0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80 --sig "run(address,address,address)" 0x70997970C51812dc3A010C7d01b50e0d17dc79C8 0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC 0x90F79bf6EB2c4f870365E785982E1f101E93b906 2>&1 | grep -E "ReceivableRegistry|Error"
rm -rf broadcast
cd ../backend
dotnet run --project tools/VeriTrade.SampleInvoice -- /tmp/samples 2
dotnet build src/VeriTrade.Api -c Release -o /tmp/apibin 2>&1 | grep -E "succeeded|error"
export HMAC_KEY_V1=$(openssl rand -hex 32) VERIFIER_PRIVATE_KEY=0x7c852118294e51e653712a81e05800f419141751be58f605c371e15141b007a6 Chain__RegistryAddress=0x9fE46736679d2D9a65F0992F2272dE9f3c7fa6e0 Chain__ChainId=31337 Chain__RpcUrl=http://127.0.0.1:8545 ASPNETCORE_URLS=http://127.0.0.1:5080
dotnet /tmp/apibin/VeriTrade.Api.dll > /tmp/api.log 2>&1 & API=$!
for i in $(seq 1 30); do curl -sf localhost:5080/health >/dev/null && break; sleep 1; done
echo "health: $(curl -s localhost:5080/health)"
S=0x15d34AAf54267DB7D7c367839AAf71A00a2C6A65
R=$(curl -s -F file=@/tmp/samples/invoice-001.xml -F sellerWallet=$S -F dueDate=2027-01-15T00:00:00Z localhost:5080/api/receivables); echo "register: $R"
ID=$(echo "$R" | jq -r .receivableId); echo "get: $(curl -s localhost:5080/api/receivables/$ID)"
echo "dup: $(curl -s -o /dev/null -w '%{http_code}' -F file=@/tmp/samples/invoice-001.xml -F sellerWallet=$S -F dueDate=2027-01-15T00:00:00Z localhost:5080/api/receivables)"
sed 's/>100000000</>900000000</' /tmp/samples/invoice-001.xml > /tmp/bad.xml
echo "tampered: $(curl -s -F file=@/tmp/bad.xml -F sellerWallet=$S -F dueDate=2027-01-15T00:00:00Z localhost:5080/api/receivables)"
echo "chain status: $(cast call 0x9fE46736679d2D9a65F0992F2272dE9f3c7fa6e0 'getReceivable(bytes32)((bytes32,address,uint256,uint64,uint8,uint8,bool,bool,bool,address,(uint16,uint256,uint16)))' $ID --rpc-url http://127.0.0.1:8545)"
kill $API $ANVIL
tail -5 /tmp/api.log
