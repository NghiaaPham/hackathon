// SPDX-License-Identifier: MIT
pragma solidity 0.8.28;

import {AccessControl} from "@openzeppelin/contracts/access/AccessControl.sol";
import {ReentrancyGuard} from "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import {EIP712} from "@openzeppelin/contracts/utils/cryptography/EIP712.sol";
import {SignatureChecker} from "@openzeppelin/contracts/utils/cryptography/SignatureChecker.sol";
import {IERC20} from "@openzeppelin/contracts/token/ERC20/IERC20.sol";
import {IERC20Permit} from "@openzeppelin/contracts/token/ERC20/extensions/IERC20Permit.sol";
import {SafeERC20} from "@openzeppelin/contracts/token/ERC20/utils/SafeERC20.sol";

import {IReceivableRegistry} from "./interfaces/IReceivableRegistry.sol";
import {BuyerRegistry} from "./BuyerRegistry.sol";

/// @title ReceivableRegistry
/// @notice One shared registry for the whole network: every funder reads and writes the same state,
/// so a receivable can be financed at most once inside the network.
/// @dev All storage, calldata and events are public. The off-chain invoice identifiers never touch the
/// chain; only an HMAC-derived id does. HMAC is not encryption.
contract ReceivableRegistry is IReceivableRegistry, AccessControl, ReentrancyGuard, EIP712 {
    using SafeERC20 for IERC20;

    bytes32 public constant VERIFIER_ROLE = keccak256("VERIFIER_ROLE");
    bytes32 public constant FUNDER_ROLE = keccak256("FUNDER_ROLE");
    bytes32 public constant ARBITER_ROLE = keccak256("ARBITER_ROLE");

    bytes32 public constant CONFIRMATION_TYPEHASH = keccak256(
        "Confirmation(bytes32 receivableId,bytes32 buyerId,address seller,uint256 amount,uint64 dueDate,"
        "uint16 advanceBps,uint256 financingFee,uint16 platformFeeBps,uint256 nonce,uint256 deadline)"
    );

    uint16 public constant BPS = 10_000;

    IERC20 public immutable token;
    BuyerRegistry public immutable buyerRegistry;
    address public immutable platformTreasury;
    uint64 public immutable gracePeriod;

    mapping(bytes32 => Receivable) private _receivables;
    mapping(address => uint256) private _claimable;
    mapping(address => uint256) private _nonces;

    constructor(IERC20 token_, BuyerRegistry buyerRegistry_, address admin, address treasury, uint64 gracePeriod_)
        EIP712("VeriTrade", "1")
    {
        if (address(token_) == address(0) || address(buyerRegistry_) == address(0)) revert ZeroAddress();
        if (admin == address(0) || treasury == address(0)) revert ZeroAddress();
        token = token_;
        buyerRegistry = buyerRegistry_;
        platformTreasury = treasury;
        gracePeriod = gracePeriod_;
        _grantRole(DEFAULT_ADMIN_ROLE, admin);
    }

    // ------------------------------------------------------------------
    // Registration and verification (backend VERIFIER)
    // ------------------------------------------------------------------

    function register(
        bytes32 receivableId,
        bytes32 buyerId,
        address seller,
        uint256 amount,
        uint64 dueDate,
        uint8 keyVersion
    ) external onlyRole(VERIFIER_ROLE) {
        if (seller == address(0)) revert ZeroAddress();
        if (amount == 0) revert InvalidAmount();
        if (dueDate <= block.timestamp) revert InvalidDueDate(dueDate);
        Receivable storage r = _receivables[receivableId];
        if (r.status != Status.NONE) revert AlreadyRegistered(receivableId);

        r.buyerId = buyerId;
        r.seller = seller;
        r.amount = amount;
        r.dueDate = dueDate;
        r.keyVersion = keyVersion;
        r.status = Status.REGISTERED;

        emit Registered(receivableId, buyerId, seller, amount, dueDate, keyVersion);
    }

    function markVerified(bytes32 receivableId) external onlyRole(VERIFIER_ROLE) {
        Receivable storage r = _requireStatus(receivableId, Status.REGISTERED);
        r.status = Status.VERIFIED;
        emit Verified(receivableId, msg.sender);
    }

    /// @notice Reject before funding (invoice cancelled, data wrong). Funded receivables go through disputes.
    function reject(bytes32 receivableId, string calldata reason) external {
        if (!hasRole(VERIFIER_ROLE, msg.sender) && !hasRole(ARBITER_ROLE, msg.sender)) {
            revert NotAuthorized(msg.sender);
        }
        Receivable storage r = _receivables[receivableId];
        Status s = r.status;
        if (s != Status.REGISTERED && s != Status.VERIFIED && s != Status.BUYER_CONFIRMED) {
            revert InvalidStatus(receivableId, s);
        }
        r.status = Status.REJECTED;
        emit Rejected(receivableId, msg.sender, reason);
    }

    // ------------------------------------------------------------------
    // Buyer confirmation (EIP-712, EOA or ERC-1271 smart wallet)
    // ------------------------------------------------------------------

    /// @notice Anyone may relay the buyer's signed confirmation.
    function confirm(Confirmation calldata c, address signer, bytes calldata signature) external {
        Receivable storage r = _requireStatus(c.receivableId, Status.VERIFIED);
        if (r.disputed) revert ReceivableDisputed(c.receivableId);
        if (block.timestamp > c.deadline) revert SignatureExpired(c.deadline);
        if (c.buyerId != r.buyerId || c.seller != r.seller || c.amount != r.amount || c.dueDate != r.dueDate) {
            revert ConfirmationMismatch();
        }
        if (!buyerRegistry.isSigner(r.buyerId, signer)) revert InvalidSigner(signer);
        uint256 expectedNonce = _nonces[signer];
        if (c.nonce != expectedNonce) revert InvalidNonce(expectedNonce, c.nonce);

        Terms memory t = Terms(c.advanceBps, c.financingFee, c.platformFeeBps);
        _validateTerms(r.amount, t);

        if (!SignatureChecker.isValidSignatureNow(signer, _hashConfirmation(c), signature)) {
            revert InvalidSignature();
        }

        _nonces[signer] = expectedNonce + 1;
        r.terms = t;
        r.status = Status.BUYER_CONFIRMED;
        emit Confirmed(c.receivableId, signer, t);
    }

    // ------------------------------------------------------------------
    // Funding (atomic: the advance moves in the same transaction as FUNDED)
    // ------------------------------------------------------------------

    /// @param expected The terms the funder agreed to; must equal the buyer-signed terms.
    function fund(bytes32 receivableId, Terms calldata expected) external nonReentrant onlyRole(FUNDER_ROLE) {
        _fund(receivableId, expected);
    }

    function fundWithPermit(
        bytes32 receivableId,
        Terms calldata expected,
        uint256 permitDeadline,
        uint8 v,
        bytes32 r,
        bytes32 s
    ) external nonReentrant onlyRole(FUNDER_ROLE) {
        uint256 advance = advanceOf(receivableId);
        // A front-run permit would make this call revert; ignore failure and rely on the allowance check.
        // Token is the immutable tVND set at deploy and the function is nonReentrant.
        // slither-disable-next-line reentrancy-no-eth
        try IERC20Permit(address(token)).permit(msg.sender, address(this), advance, permitDeadline, v, r, s) {} catch {}
        _fund(receivableId, expected);
    }

    function _fund(bytes32 receivableId, Terms calldata expected) private {
        Receivable storage rec = _requireStatus(receivableId, Status.BUYER_CONFIRMED);
        if (rec.disputed) revert ReceivableDisputed(receivableId);
        Terms memory t = rec.terms;
        if (
            t.advanceBps != expected.advanceBps || t.financingFee != expected.financingFee
                || t.platformFeeBps != expected.platformFeeBps
        ) revert TermsMismatch();

        uint256 advance = (rec.amount * t.advanceBps) / BPS;
        // Effects before the external call; if the transfer fails the whole transaction reverts,
        // so FUNDED can never be recorded without the seller receiving the advance.
        rec.funder = msg.sender;
        rec.status = Status.FUNDED;
        emit Funded(receivableId, msg.sender, rec.seller, advance);

        token.safeTransferFrom(msg.sender, rec.seller, advance);
    }

    // ------------------------------------------------------------------
    // Settlement (buyer pays face value; pull-payment waterfall)
    // ------------------------------------------------------------------

    /// @notice Pull the full face value from the caller (normally the buyer).
    /// Distribution happens now, or after the arbiter clears a dispute.
    function pay(bytes32 receivableId) external nonReentrant {
        Receivable storage r = _requireStatus(receivableId, Status.FUNDED);
        if (r.paymentHeld) revert InvalidStatus(receivableId, r.status);
        bool held = r.disputed;
        if (held) {
            r.paymentHeld = true;
        } else {
            _settle(receivableId, r);
        }
        emit Paid(receivableId, msg.sender, r.amount, held);
        token.safeTransferFrom(msg.sender, address(this), r.amount);
    }

    function withdraw() external nonReentrant {
        uint256 amount = _claimable[msg.sender];
        if (amount == 0) revert NothingToWithdraw();
        _claimable[msg.sender] = 0;
        emit Withdrawn(msg.sender, amount);
        token.safeTransfer(msg.sender, amount);
    }

    function _settle(bytes32 receivableId, Receivable storage r) private {
        (uint256 toFunder, uint256 toPlatform, uint256 toSeller) = _waterfall(r.amount, r.terms);
        r.status = Status.SETTLED;
        r.paymentHeld = false;
        _claimable[r.funder] += toFunder;
        _claimable[platformTreasury] += toPlatform;
        _claimable[r.seller] += toSeller;
        emit Settled(receivableId, toFunder, toPlatform, toSeller);
    }

    // ------------------------------------------------------------------
    // Disputes and overdue flag
    // ------------------------------------------------------------------

    /// @notice Seller, funder, an authorised buyer signer, or an arbiter can freeze a receivable.
    function raiseDispute(bytes32 receivableId) external {
        Receivable storage r = _receivables[receivableId];
        Status s = r.status;
        if (s != Status.VERIFIED && s != Status.BUYER_CONFIRMED && s != Status.FUNDED) {
            revert InvalidStatus(receivableId, s);
        }
        if (r.disputed) revert AlreadyDisputed(receivableId);
        bool allowed = msg.sender == r.seller || msg.sender == r.funder || hasRole(ARBITER_ROLE, msg.sender)
            || buyerRegistry.isSigner(r.buyerId, msg.sender);
        if (!allowed) revert NotAuthorized(msg.sender);
        r.disputed = true;
        emit DisputeRaised(receivableId, msg.sender);
    }

    /// @notice upheld = the dispute is valid. Before funding this rejects the receivable.
    /// After funding the MVP only unfreezes; recovery between parties is off-chain (documented limit).
    function resolveDispute(bytes32 receivableId, bool upheld) external onlyRole(ARBITER_ROLE) {
        Receivable storage r = _receivables[receivableId];
        if (!r.disputed) revert NotDisputed(receivableId);
        r.disputed = false;
        emit DisputeResolved(receivableId, msg.sender, upheld);

        if (upheld && (r.status == Status.VERIFIED || r.status == Status.BUYER_CONFIRMED)) {
            r.status = Status.REJECTED;
            emit Rejected(receivableId, msg.sender, "dispute upheld");
        } else if (r.paymentHeld) {
            _settle(receivableId, r);
        }
    }

    /// @notice Anyone can flag a funded receivable as overdue after dueDate + gracePeriod.
    /// The contract never pays on its own; this is a visible risk flag only.
    function markOverdue(bytes32 receivableId) external {
        Receivable storage r = _requireStatus(receivableId, Status.FUNDED);
        if (r.overdue) revert AlreadyOverdue(receivableId);
        uint256 overdueAt = uint256(r.dueDate) + gracePeriod;
        if (block.timestamp <= overdueAt) revert NotOverdueYet(overdueAt);
        r.overdue = true;
        emit MarkedOverdue(receivableId, msg.sender);
    }

    // ------------------------------------------------------------------
    // Views and helpers
    // ------------------------------------------------------------------

    function getReceivable(bytes32 receivableId) external view returns (Receivable memory) {
        return _receivables[receivableId];
    }

    function claimable(address account) external view returns (uint256) {
        return _claimable[account];
    }

    function nonces(address signer) external view returns (uint256) {
        return _nonces[signer];
    }

    function confirmationDigest(Confirmation calldata c) external view returns (bytes32) {
        return _hashConfirmation(c);
    }

    function advanceOf(bytes32 receivableId) public view returns (uint256) {
        Receivable storage r = _receivables[receivableId];
        return (r.amount * r.terms.advanceBps) / BPS;
    }

    function domainSeparator() external view returns (bytes32) {
        return _domainSeparatorV4();
    }

    function _hashConfirmation(Confirmation calldata c) private view returns (bytes32) {
        return _hashTypedDataV4(
            keccak256(
                abi.encode(
                    CONFIRMATION_TYPEHASH,
                    c.receivableId,
                    c.buyerId,
                    c.seller,
                    c.amount,
                    c.dueDate,
                    c.advanceBps,
                    c.financingFee,
                    c.platformFeeBps,
                    c.nonce,
                    c.deadline
                )
            )
        );
    }

    function _requireStatus(bytes32 receivableId, Status expected) private view returns (Receivable storage r) {
        r = _receivables[receivableId];
        if (r.status != expected) revert InvalidStatus(receivableId, r.status);
    }

    function _validateTerms(uint256 amount, Terms memory t) private pure {
        if (t.advanceBps == 0 || t.advanceBps > BPS || t.platformFeeBps > BPS) revert InvalidTerms();
        (uint256 toFunder, uint256 toPlatform,) = _waterfallParts(amount, t);
        if (toFunder + toPlatform > amount) revert InvalidTerms();
    }

    function _waterfall(uint256 amount, Terms memory t)
        private
        pure
        returns (uint256 toFunder, uint256 toPlatform, uint256 toSeller)
    {
        (toFunder, toPlatform,) = _waterfallParts(amount, t);
        toSeller = amount - toFunder - toPlatform;
    }

    function _waterfallParts(uint256 amount, Terms memory t)
        private
        pure
        returns (uint256 toFunder, uint256 toPlatform, uint256 advance)
    {
        advance = (amount * t.advanceBps) / BPS;
        toFunder = advance + t.financingFee;
        toPlatform = (amount * t.platformFeeBps) / BPS;
    }
}
