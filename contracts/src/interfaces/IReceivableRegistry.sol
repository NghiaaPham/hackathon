// SPDX-License-Identifier: MIT
pragma solidity 0.8.28;

/// @title IReceivableRegistry
/// @notice Shared interface (the "ABI contract") between contracts, backend and web.
/// Change it only through a PR that updates all three.
interface IReceivableRegistry {
    enum Status {
        NONE,
        REGISTERED,
        VERIFIED,
        BUYER_CONFIRMED,
        FUNDED,
        SETTLED,
        REJECTED
    }

    struct Terms {
        uint16 advanceBps; // e.g. 8000 = 80%
        uint256 financingFee; // absolute tVND, e.g. 4_000_000
        uint16 platformFeeBps; // e.g. 50 = 0.5%
    }

    struct Receivable {
        bytes32 buyerId;
        address seller;
        uint256 amount; // face value F in tVND
        uint64 dueDate;
        uint8 keyVersion; // HMAC key version used for the id
        Status status;
        bool disputed;
        bool overdue;
        address funder;
        Terms terms;
    }

    /// @dev EIP-712 typed data signed by an authorised buyer signer.
    /// Confirmation(bytes32 receivableId,bytes32 buyerId,address seller,uint256 amount,uint64 dueDate,
    ///   uint16 advanceBps,uint256 financingFee,uint16 platformFeeBps,uint256 nonce,uint256 deadline)
    struct Confirmation {
        bytes32 receivableId;
        bytes32 buyerId;
        address seller;
        uint256 amount;
        uint64 dueDate;
        uint16 advanceBps;
        uint256 financingFee;
        uint16 platformFeeBps;
        uint256 nonce;
        uint256 deadline;
    }

    // ---- events (one per state transition, id indexed for timelines) ----
    event Registered(
        bytes32 indexed receivableId,
        bytes32 indexed buyerId,
        address indexed seller,
        uint256 amount,
        uint64 dueDate,
        uint8 keyVersion
    );
    event Verified(bytes32 indexed receivableId, address indexed verifier);
    event Rejected(bytes32 indexed receivableId, address indexed by, string reason);
    event Confirmed(bytes32 indexed receivableId, address indexed signer, Terms terms);
    event Funded(bytes32 indexed receivableId, address indexed funder, address indexed seller, uint256 advance);
    event Paid(bytes32 indexed receivableId, address indexed payer, uint256 amount);
    event Credited(bytes32 indexed receivableId, address indexed account, uint256 amount);
    event Withdrawn(address indexed account, uint256 amount);
    event DisputeRaised(bytes32 indexed receivableId, address indexed by);
    event DisputeResolved(bytes32 indexed receivableId, address indexed arbiter, bool upheld);
    event MarkedOverdue(bytes32 indexed receivableId, address indexed by);

    // ---- errors ----
    error AlreadyRegistered(bytes32 receivableId);
    error InvalidStatus(bytes32 receivableId, Status current);
    error Disputed(bytes32 receivableId);
    error InvalidSigner(address signer);
    error SignatureExpired(uint256 deadline);
    error TermsMismatch();
    error NotDueYet(uint64 dueDate);
    error NothingToWithdraw();

    // ---- flow ----
    function register(
        bytes32 receivableId,
        bytes32 buyerId,
        address seller,
        uint256 amount,
        uint64 dueDate,
        uint8 keyVersion
    ) external;
    function markVerified(bytes32 receivableId) external;
    function reject(bytes32 receivableId, string calldata reason) external;
    function confirm(Confirmation calldata c, address signer, bytes calldata signature) external;
    function fund(bytes32 receivableId, Terms calldata expected) external;
    function fundWithPermit(
        bytes32 receivableId,
        Terms calldata expected,
        uint256 permitDeadline,
        uint8 v,
        bytes32 r,
        bytes32 s
    ) external;
    function pay(bytes32 receivableId) external;
    function withdraw() external;

    // ---- risk flags ----
    function raiseDispute(bytes32 receivableId) external;
    function resolveDispute(bytes32 receivableId, bool upheld) external;
    function markOverdue(bytes32 receivableId) external;

    // ---- views ----
    function getReceivable(bytes32 receivableId) external view returns (Receivable memory);
    function claimable(address account) external view returns (uint256);
    function nonces(address signer) external view returns (uint256);
}
