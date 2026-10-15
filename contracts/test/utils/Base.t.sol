// SPDX-License-Identifier: MIT
pragma solidity 0.8.28;

import {Test} from "forge-std/Test.sol";
import {MockTVND} from "../../src/mocks/MockTVND.sol";
import {BuyerRegistry} from "../../src/BuyerRegistry.sol";
import {ReceivableRegistry} from "../../src/ReceivableRegistry.sol";
import {IReceivableRegistry} from "../../src/interfaces/IReceivableRegistry.sol";

/// @dev Shared fixture. Demo numbers: F = 100M, advance 80%, funder fee 4M, platform 0.5%.
abstract contract Base is Test {
    uint256 constant FUJI_CHAIN_ID = 43113;
    uint256 constant F = 100_000_000;
    uint16 constant ADVANCE_BPS = 8000;
    uint256 constant FIN_FEE = 4_000_000;
    uint16 constant PLATFORM_BPS = 50;
    uint64 constant GRACE = 7 days;

    bytes32 constant BUYER_ID = keccak256("buyer:0312345678");
    bytes32 constant RID = keccak256("receivable:demo-1");

    MockTVND token;
    BuyerRegistry buyers;
    ReceivableRegistry registry;

    address admin = makeAddr("admin");
    address treasury = makeAddr("treasury");
    address verifier = makeAddr("verifier");
    address arbiter = makeAddr("arbiter");
    address seller = makeAddr("seller");
    address funderA = makeAddr("funderA");
    address funderB = makeAddr("funderB");
    address payer = makeAddr("buyerTreasury");
    address buyerSigner;
    uint256 buyerKey;

    uint64 dueDate;

    function setUp() public virtual {
        vm.chainId(FUJI_CHAIN_ID);
        vm.warp(1_800_000_000);
        (buyerSigner, buyerKey) = makeAddrAndKey("buyerSigner");
        dueDate = uint64(block.timestamp + 90 days);

        token = new MockTVND();
        buyers = new BuyerRegistry(admin);
        registry = new ReceivableRegistry(token, buyers, admin, treasury, GRACE);

        vm.startPrank(admin);
        registry.grantRole(registry.VERIFIER_ROLE(), verifier);
        registry.grantRole(registry.ARBITER_ROLE(), arbiter);
        registry.grantRole(registry.FUNDER_ROLE(), funderA);
        registry.grantRole(registry.FUNDER_ROLE(), funderB);
        buyers.addSigner(BUYER_ID, buyerSigner);
        vm.stopPrank();

        _mint(funderA, 500_000_000);
        _mint(funderB, 500_000_000);
        _mint(payer, 500_000_000);
    }

    function _mint(address to, uint256 amount) internal {
        deal(address(token), to, amount, true);
    }

    function _terms() internal pure returns (IReceivableRegistry.Terms memory) {
        return IReceivableRegistry.Terms(ADVANCE_BPS, FIN_FEE, PLATFORM_BPS);
    }

    function _conf(bytes32 rid, uint256 nonce) internal view returns (IReceivableRegistry.Confirmation memory) {
        return IReceivableRegistry.Confirmation({
            receivableId: rid,
            buyerId: BUYER_ID,
            seller: seller,
            amount: F,
            dueDate: dueDate,
            advanceBps: ADVANCE_BPS,
            financingFee: FIN_FEE,
            platformFeeBps: PLATFORM_BPS,
            nonce: nonce,
            deadline: block.timestamp + 1 days
        });
    }

    function _sign(uint256 key, IReceivableRegistry.Confirmation memory c) internal view returns (bytes memory) {
        (uint8 v, bytes32 r, bytes32 s) = vm.sign(key, registry.confirmationDigest(c));
        return abi.encodePacked(r, s, v);
    }

    function _register(bytes32 rid) internal {
        vm.prank(verifier);
        registry.register(rid, BUYER_ID, seller, F, dueDate, 1);
    }

    function _verified(bytes32 rid) internal {
        _register(rid);
        vm.prank(verifier);
        registry.markVerified(rid);
    }

    function _confirmed(bytes32 rid) internal {
        _verified(rid);
        IReceivableRegistry.Confirmation memory c = _conf(rid, registry.nonces(buyerSigner));
        registry.confirm(c, buyerSigner, _sign(buyerKey, c));
    }

    function _funded(bytes32 rid, address funder) internal {
        _confirmed(rid);
        vm.startPrank(funder);
        token.approve(address(registry), registry.advanceOf(rid));
        registry.fund(rid, _terms());
        vm.stopPrank();
    }

    function _pay(bytes32 rid) internal {
        vm.startPrank(payer);
        token.approve(address(registry), F);
        registry.pay(rid);
        vm.stopPrank();
    }

    function _status(bytes32 rid) internal view returns (IReceivableRegistry.Status) {
        return registry.getReceivable(rid).status;
    }
}
