// SPDX-License-Identifier: MIT
pragma solidity 0.8.28;

import {Script, console} from "forge-std/Script.sol";
import {MockTVND} from "../src/mocks/MockTVND.sol";
import {BuyerRegistry} from "../src/BuyerRegistry.sol";
import {ReceivableRegistry} from "../src/ReceivableRegistry.sol";

/// Deploys the full P0 stack. Same script for Fuji C-Chain and the VeriTrade L1.
///   forge script script/Deploy.s.sol --rpc-url fuji --broadcast --account deployer \
///     --sig "run(address,address,address)" <admin> <treasury> <verifier>
contract Deploy is Script {
    uint64 constant GRACE_PERIOD = 7 days;

    function run(address admin, address treasury, address verifier) external {
        vm.startBroadcast();
        (, address deployer,) = vm.readCallers();
        MockTVND token = new MockTVND();
        BuyerRegistry buyers = new BuyerRegistry(admin);
        ReceivableRegistry registry = new ReceivableRegistry(token, buyers, deployer, treasury, GRACE_PERIOD);
        registry.grantRole(registry.VERIFIER_ROLE(), verifier);
        if (admin != deployer) {
            registry.grantRole(registry.DEFAULT_ADMIN_ROLE(), admin);
            registry.renounceRole(registry.DEFAULT_ADMIN_ROLE(), deployer);
        }
        vm.stopBroadcast();

        console.log("chainId          :", block.chainid);
        console.log("tVND             :", address(token));
        console.log("BuyerRegistry    :", address(buyers));
        console.log("ReceivableRegistry:", address(registry));
    }
}
