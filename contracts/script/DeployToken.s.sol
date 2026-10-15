// SPDX-License-Identifier: MIT
pragma solidity 0.8.28;

import {Script, console} from "forge-std/Script.sol";
import {MockTVND} from "../src/mocks/MockTVND.sol";

/// forge script script/DeployToken.s.sol --rpc-url fuji --broadcast --account deployer
contract DeployToken is Script {
    function run() external {
        vm.startBroadcast();
        MockTVND token = new MockTVND();
        vm.stopBroadcast();
        console.log("tVND:", address(token));
    }
}
