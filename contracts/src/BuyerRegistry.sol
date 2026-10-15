// SPDX-License-Identifier: MIT
pragma solidity 0.8.28;

import {AccessControl} from "@openzeppelin/contracts/access/AccessControl.sol";

/// @title BuyerRegistry
/// @notice Maps each buyer (by an off-chain business id) to the wallets allowed to confirm its payables.
/// @dev A valid signature only proves key control. Authority to sign for the buyer comes from KYB
/// done off-chain by the network admin, recorded here. Admin should be a multisig.
contract BuyerRegistry is AccessControl {
    bytes32 public constant REGISTRAR_ROLE = keccak256("REGISTRAR_ROLE");

    mapping(bytes32 buyerId => mapping(address signer => bool)) private _isSigner;

    event SignerAdded(bytes32 indexed buyerId, address indexed signer, address indexed by);
    event SignerRemoved(bytes32 indexed buyerId, address indexed signer, address indexed by);

    error ZeroAddress();
    error AlreadySigner(bytes32 buyerId, address signer);
    error NotSigner(bytes32 buyerId, address signer);

    constructor(address admin) {
        if (admin == address(0)) revert ZeroAddress();
        _grantRole(DEFAULT_ADMIN_ROLE, admin);
        _grantRole(REGISTRAR_ROLE, admin);
    }

    function addSigner(bytes32 buyerId, address signer) external onlyRole(REGISTRAR_ROLE) {
        if (signer == address(0)) revert ZeroAddress();
        if (_isSigner[buyerId][signer]) revert AlreadySigner(buyerId, signer);
        _isSigner[buyerId][signer] = true;
        emit SignerAdded(buyerId, signer, msg.sender);
    }

    function removeSigner(bytes32 buyerId, address signer) external onlyRole(REGISTRAR_ROLE) {
        if (!_isSigner[buyerId][signer]) revert NotSigner(buyerId, signer);
        _isSigner[buyerId][signer] = false;
        emit SignerRemoved(buyerId, signer, msg.sender);
    }

    function isSigner(bytes32 buyerId, address signer) external view returns (bool) {
        return _isSigner[buyerId][signer];
    }
}
