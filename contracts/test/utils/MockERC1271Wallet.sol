// SPDX-License-Identifier: MIT
pragma solidity 0.8.28;

import {IERC1271} from "@openzeppelin/contracts/interfaces/IERC1271.sol";
import {ECDSA} from "@openzeppelin/contracts/utils/cryptography/ECDSA.sol";

/// @dev Minimal smart wallet that approves signatures from a single owner key.
/// Stands in for a buyer's multisig in tests.
contract MockERC1271Wallet is IERC1271 {
    address public immutable owner;

    constructor(address owner_) {
        owner = owner_;
    }

    function isValidSignature(bytes32 hash, bytes memory signature) external view returns (bytes4) {
        (address rec, ECDSA.RecoverError err,) = ECDSA.tryRecover(hash, signature);
        if (err == ECDSA.RecoverError.NoError && rec == owner) return IERC1271.isValidSignature.selector;
        return 0xffffffff;
    }
}
