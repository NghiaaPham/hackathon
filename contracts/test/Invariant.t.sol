// SPDX-License-Identifier: MIT
pragma solidity 0.8.28;

import {Test} from "forge-std/Test.sol";
import {Base} from "./utils/Base.t.sol";
import {IReceivableRegistry} from "../src/interfaces/IReceivableRegistry.sol";
import {ReceivableRegistry} from "../src/ReceivableRegistry.sol";
import {MockTVND} from "../src/mocks/MockTVND.sol";

/// @dev Drives random lifecycles. Tracks what the registry should hold.
contract Handler is Test {
    ReceivableRegistry registry;
    MockTVND token;
    address verifier;
    address arbiter;
    address seller;
    address[2] funders;
    address payer;
    address buyerSigner;
    uint256 buyerKey;
    bytes32 buyerId;

    bytes32[] public ids;
    mapping(bytes32 => uint256) public fundCount;
    address[] public accounts;
    uint256 public heldTotal;

    struct Config {
        ReceivableRegistry registry;
        MockTVND token;
        address verifier;
        address arbiter;
        address seller;
        address funderA;
        address funderB;
        address payer;
        address buyerSigner;
        uint256 buyerKey;
        bytes32 buyerId;
        address treasury;
    }

    constructor(Config memory c) {
        registry = c.registry;
        token = c.token;
        verifier = c.verifier;
        arbiter = c.arbiter;
        seller = c.seller;
        payer = c.payer;
        buyerSigner = c.buyerSigner;
        buyerKey = c.buyerKey;
        buyerId = c.buyerId;
        funders = [c.funderA, c.funderB];
        accounts = [c.seller, c.funderA, c.funderB, c.treasury];
    }

    function idsLength() external view returns (uint256) {
        return ids.length;
    }

    function accountsLength() external view returns (uint256) {
        return accounts.length;
    }

    function createConfirmed(uint256 amount, uint16 advanceBps, uint256 fee) external {
        amount = bound(amount, 1_000, 1e12);
        advanceBps = uint16(bound(advanceBps, 1, 9_000));
        uint256 advance = (amount * advanceBps) / 10_000;
        uint256 platform = (amount * 50) / 10_000;
        fee = bound(fee, 0, amount - advance - platform);
        bytes32 id = keccak256(abi.encode(ids.length, amount));
        uint64 due = uint64(block.timestamp + 30 days);

        vm.startPrank(verifier);
        registry.register(id, buyerId, seller, amount, due, 1);
        registry.markVerified(id);
        vm.stopPrank();

        IReceivableRegistry.Confirmation memory c = IReceivableRegistry.Confirmation(
            id, buyerId, seller, amount, due, advanceBps, fee, 50, registry.nonces(buyerSigner), block.timestamp + 1
        );
        (uint8 v, bytes32 r, bytes32 s) = vm.sign(buyerKey, registry.confirmationDigest(c));
        registry.confirm(c, buyerSigner, abi.encodePacked(r, s, v));
        ids.push(id);
    }

    function fund(uint256 idSeed, uint256 funderSeed) external {
        if (ids.length == 0) return;
        bytes32 id = ids[idSeed % ids.length];
        address funder = funders[funderSeed % 2];
        IReceivableRegistry.Receivable memory rec = registry.getReceivable(id);
        uint256 advance = registry.advanceOf(id);
        deal(address(token), funder, token.balanceOf(funder) + advance, true);
        vm.startPrank(funder);
        token.approve(address(registry), advance);
        try registry.fund(id, rec.terms) {
            fundCount[id]++;
        } catch {}
        vm.stopPrank();
    }

    function dispute(uint256 idSeed) external {
        if (ids.length == 0) return;
        vm.prank(seller);
        try registry.raiseDispute(ids[idSeed % ids.length]) {} catch {}
    }

    function resolve(uint256 idSeed) external {
        if (ids.length == 0) return;
        bytes32 id = ids[idSeed % ids.length];
        bool wasHeld = registry.getReceivable(id).paymentHeld;
        vm.prank(arbiter);
        try registry.resolveDispute(id, false) {
            if (wasHeld) heldTotal -= registry.getReceivable(id).amount;
        } catch {}
    }

    function pay(uint256 idSeed) external {
        if (ids.length == 0) return;
        bytes32 id = ids[idSeed % ids.length];
        uint256 amount = registry.getReceivable(id).amount;
        deal(address(token), payer, token.balanceOf(payer) + amount, true);
        vm.startPrank(payer);
        token.approve(address(registry), amount);
        try registry.pay(id) {
            if (registry.getReceivable(id).paymentHeld) heldTotal += amount;
        } catch {}
        vm.stopPrank();
    }

    function withdraw(uint256 accountSeed) external {
        vm.prank(accounts[accountSeed % accounts.length]);
        try registry.withdraw() {} catch {}
    }
}

contract RegistryInvariantTest is Base {
    Handler handler;

    function setUp() public override {
        super.setUp();
        handler = new Handler(
            Handler.Config(
                registry,
                token,
                verifier,
                arbiter,
                seller,
                funderA,
                funderB,
                payer,
                buyerSigner,
                buyerKey,
                BUYER_ID,
                treasury
            )
        );
        targetContract(address(handler));
    }

    /// The registry holds exactly what it owes: outstanding claimable plus payments held by disputes.
    function invariant_BalanceEqualsLiabilities() public view {
        uint256 owed = handler.heldTotal();
        for (uint256 i; i < handler.accountsLength(); i++) {
            owed += registry.claimable(handler.accounts(i));
        }
        assertEq(token.balanceOf(address(registry)), owed);
    }

    /// No receivable is ever financed twice.
    function invariant_FundedAtMostOnce() public view {
        for (uint256 i; i < handler.idsLength(); i++) {
            assertLe(handler.fundCount(handler.ids(i)), 1);
        }
    }
}
