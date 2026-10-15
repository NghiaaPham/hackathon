// SPDX-License-Identifier: MIT
pragma solidity 0.8.28;

import {Base} from "./utils/Base.t.sol";
import {IReceivableRegistry} from "../src/interfaces/IReceivableRegistry.sol";
import {ReceivableRegistry} from "../src/ReceivableRegistry.sol";
import {BuyerRegistry} from "../src/BuyerRegistry.sol";
import {MockERC1271Wallet} from "./utils/MockERC1271Wallet.sol";
import {IAccessControl} from "@openzeppelin/contracts/access/IAccessControl.sol";

contract ReceivableRegistryTest is Base {
    // ------------------------------------------------------------ happy path

    function test_FullFlow_DemoNumbers() public {
        _funded(RID, funderA);
        assertEq(token.balanceOf(seller), 80_000_000, "seller gets 80% at funding");
        assertEq(uint8(_status(RID)), uint8(IReceivableRegistry.Status.FUNDED));

        _pay(RID);
        assertEq(uint8(_status(RID)), uint8(IReceivableRegistry.Status.SETTLED));
        assertEq(registry.claimable(funderA), 84_000_000);
        assertEq(registry.claimable(treasury), 500_000);
        assertEq(registry.claimable(seller), 15_500_000);
        assertEq(token.balanceOf(address(registry)), F);

        vm.prank(funderA);
        registry.withdraw();
        vm.prank(seller);
        registry.withdraw();
        vm.prank(treasury);
        registry.withdraw();
        assertEq(token.balanceOf(address(registry)), 0);
        assertEq(token.balanceOf(seller), 95_500_000);
        assertEq(token.balanceOf(funderA), 500_000_000 + 4_000_000);
    }

    function test_EmitsEventPerTransition() public {
        vm.expectEmit(true, true, true, true);
        emit IReceivableRegistry.Registered(RID, BUYER_ID, seller, F, dueDate, 1);
        _register(RID);

        vm.expectEmit(true, true, false, false);
        emit IReceivableRegistry.Verified(RID, verifier);
        vm.prank(verifier);
        registry.markVerified(RID);
    }

    // ------------------------------------------------------------ registration

    function test_Register_OnlyVerifier() public {
        vm.expectRevert(
            abi.encodeWithSelector(
                IAccessControl.AccessControlUnauthorizedAccount.selector, seller, registry.VERIFIER_ROLE()
            )
        );
        vm.prank(seller);
        registry.register(RID, BUYER_ID, seller, F, dueDate, 1);
    }

    function test_Register_DuplicateReverts() public {
        _register(RID);
        vm.expectRevert(abi.encodeWithSelector(IReceivableRegistry.AlreadyRegistered.selector, RID));
        _register(RID);
    }

    function test_Register_PastDueDateReverts() public {
        vm.prank(verifier);
        vm.expectRevert();
        registry.register(RID, BUYER_ID, seller, F, uint64(block.timestamp), 1);
    }

    function test_Register_ZeroAmountReverts() public {
        vm.prank(verifier);
        vm.expectRevert(IReceivableRegistry.InvalidAmount.selector);
        registry.register(RID, BUYER_ID, seller, 0, dueDate, 1);
    }

    function test_Reject_BeforeFunding() public {
        _verified(RID);
        vm.prank(verifier);
        registry.reject(RID, "invoice cancelled");
        assertEq(uint8(_status(RID)), uint8(IReceivableRegistry.Status.REJECTED));
        // rejected id stays taken: cannot be re-registered
        vm.expectRevert(abi.encodeWithSelector(IReceivableRegistry.AlreadyRegistered.selector, RID));
        _register(RID);
    }

    function test_Reject_AfterFundingReverts() public {
        _funded(RID, funderA);
        vm.prank(verifier);
        vm.expectRevert();
        registry.reject(RID, "too late");
    }

    // ------------------------------------------------------------ buyer confirmation

    function test_Confirm_WrongSignerKeyReverts() public {
        _verified(RID);
        (, uint256 otherKey) = makeAddrAndKey("attacker");
        IReceivableRegistry.Confirmation memory c = _conf(RID, 0);
        bytes memory sig = _sign(otherKey, c);
        vm.expectRevert(IReceivableRegistry.InvalidSignature.selector);
        registry.confirm(c, buyerSigner, sig);
    }

    function test_Confirm_UnregisteredSignerReverts() public {
        _verified(RID);
        (address attacker, uint256 attackerKey) = makeAddrAndKey("attacker");
        IReceivableRegistry.Confirmation memory c = _conf(RID, 0);
        bytes memory sig = _sign(attackerKey, c);
        vm.expectRevert(abi.encodeWithSelector(IReceivableRegistry.InvalidSigner.selector, attacker));
        registry.confirm(c, attacker, sig);
    }

    function test_Confirm_RemovedSignerReverts() public {
        _verified(RID);
        vm.prank(admin);
        buyers.removeSigner(BUYER_ID, buyerSigner);
        IReceivableRegistry.Confirmation memory c = _conf(RID, 0);
        bytes memory sig = _sign(buyerKey, c);
        vm.expectRevert(abi.encodeWithSelector(IReceivableRegistry.InvalidSigner.selector, buyerSigner));
        registry.confirm(c, buyerSigner, sig);
    }

    function test_Confirm_ExpiredDeadlineReverts() public {
        _verified(RID);
        IReceivableRegistry.Confirmation memory c = _conf(RID, 0);
        bytes memory sig = _sign(buyerKey, c);
        vm.warp(c.deadline + 1);
        vm.expectRevert(abi.encodeWithSelector(IReceivableRegistry.SignatureExpired.selector, c.deadline));
        registry.confirm(c, buyerSigner, sig);
    }

    function test_Confirm_ReusedNonceReverts() public {
        bytes32 rid2 = keccak256("receivable:demo-2");
        _confirmed(RID); // uses nonce 0
        _verified(rid2);
        IReceivableRegistry.Confirmation memory c = _conf(rid2, 0);
        bytes memory sig = _sign(buyerKey, c);
        vm.expectRevert(abi.encodeWithSelector(IReceivableRegistry.InvalidNonce.selector, 1, 0));
        registry.confirm(c, buyerSigner, sig);
    }

    function test_Confirm_SignatureForOtherReceivableReverts() public {
        bytes32 rid2 = keccak256("receivable:demo-2");
        _verified(RID);
        _verified(rid2);
        IReceivableRegistry.Confirmation memory signed = _conf(RID, 0);
        bytes memory sig = _sign(buyerKey, signed);
        IReceivableRegistry.Confirmation memory submitted = signed;
        submitted.receivableId = rid2;
        vm.expectRevert(IReceivableRegistry.InvalidSignature.selector);
        registry.confirm(submitted, buyerSigner, sig);
    }

    function test_Confirm_FieldMismatchReverts() public {
        _verified(RID);
        IReceivableRegistry.Confirmation memory c = _conf(RID, 0);
        c.amount = F + 1;
        bytes memory sig = _sign(buyerKey, c);
        vm.expectRevert(IReceivableRegistry.ConfirmationMismatch.selector);
        registry.confirm(c, buyerSigner, sig);
    }

    function test_Confirm_SignatureFromOtherChainReverts() public {
        // Buyer signs for the Fuji C-Chain deployment...
        _verified(RID);
        IReceivableRegistry.Confirmation memory c = _conf(RID, 0);
        bytes memory sig = _sign(buyerKey, c);
        // ...and someone replays it on a chain with a different id (e.g. the VeriTrade L1).
        vm.chainId(99_999);
        vm.expectRevert(IReceivableRegistry.InvalidSignature.selector);
        registry.confirm(c, buyerSigner, sig);
    }

    function test_Confirm_SignatureForOtherDeploymentReverts() public {
        ReceivableRegistry other = new ReceivableRegistry(token, buyers, admin, treasury, GRACE);
        vm.startPrank(admin);
        other.grantRole(other.VERIFIER_ROLE(), verifier);
        vm.stopPrank();
        vm.startPrank(verifier);
        other.register(RID, BUYER_ID, seller, F, dueDate, 1);
        other.markVerified(RID);
        vm.stopPrank();
        _verified(RID);

        IReceivableRegistry.Confirmation memory c = _conf(RID, 0);
        bytes memory sigForThis = _sign(buyerKey, c); // bound to `registry`
        vm.expectRevert(IReceivableRegistry.InvalidSignature.selector);
        other.confirm(c, buyerSigner, sigForThis);
    }

    function test_Confirm_InvalidTermsReverts() public {
        _verified(RID);
        IReceivableRegistry.Confirmation memory c = _conf(RID, 0);
        c.advanceBps = 9_900;
        c.financingFee = 2_000_000; // 99M + 2M > 100M
        bytes memory sig = _sign(buyerKey, c);
        vm.expectRevert(IReceivableRegistry.InvalidTerms.selector);
        registry.confirm(c, buyerSigner, sig);
    }

    function test_Confirm_ERC1271MultisigBuyer() public {
        (address owner, uint256 ownerKey) = makeAddrAndKey("multisigOwner");
        MockERC1271Wallet wallet = new MockERC1271Wallet(owner);
        vm.prank(admin);
        buyers.addSigner(BUYER_ID, address(wallet));

        _verified(RID);
        IReceivableRegistry.Confirmation memory c = _conf(RID, 0);
        registry.confirm(c, address(wallet), _sign(ownerKey, c));
        assertEq(uint8(_status(RID)), uint8(IReceivableRegistry.Status.BUYER_CONFIRMED));
        assertEq(registry.nonces(address(wallet)), 1);
    }

    // ------------------------------------------------------------ funding

    function test_Fund_SecondTimeRevertsForAnyFunder() public {
        _funded(RID, funderA);
        vm.startPrank(funderB);
        token.approve(address(registry), type(uint256).max);
        vm.expectRevert(
            abi.encodeWithSelector(IReceivableRegistry.InvalidStatus.selector, RID, IReceivableRegistry.Status.FUNDED)
        );
        registry.fund(RID, _terms());
        vm.stopPrank();
    }

    function test_Fund_NotFunderReverts() public {
        _confirmed(RID);
        vm.prank(seller);
        vm.expectRevert();
        registry.fund(RID, _terms());
    }

    function test_Fund_BeforeBuyerConfirmationReverts() public {
        _verified(RID);
        vm.prank(funderA);
        vm.expectRevert();
        registry.fund(RID, _terms());
    }

    function test_Fund_InsufficientAllowanceRevertsAndKeepsState() public {
        _confirmed(RID);
        vm.startPrank(funderA);
        token.approve(address(registry), 1);
        vm.expectRevert();
        registry.fund(RID, _terms());
        vm.stopPrank();
        assertEq(uint8(_status(RID)), uint8(IReceivableRegistry.Status.BUYER_CONFIRMED));
        assertEq(token.balanceOf(seller), 0);
    }

    function test_Fund_InsufficientBalanceReverts() public {
        _confirmed(RID);
        _mint(funderA, 1);
        vm.startPrank(funderA);
        token.approve(address(registry), type(uint256).max);
        vm.expectRevert();
        registry.fund(RID, _terms());
        vm.stopPrank();
        assertEq(uint8(_status(RID)), uint8(IReceivableRegistry.Status.BUYER_CONFIRMED));
    }

    function test_Fund_TermsDifferentFromSignedReverts() public {
        _confirmed(RID);
        IReceivableRegistry.Terms memory t = _terms();
        t.financingFee = 9_000_000;
        vm.startPrank(funderA);
        token.approve(address(registry), type(uint256).max);
        vm.expectRevert(IReceivableRegistry.TermsMismatch.selector);
        registry.fund(RID, t);
        vm.stopPrank();
    }

    function test_Fund_WhileDisputedReverts() public {
        _confirmed(RID);
        vm.prank(seller);
        registry.raiseDispute(RID);
        vm.startPrank(funderA);
        token.approve(address(registry), type(uint256).max);
        vm.expectRevert(abi.encodeWithSelector(IReceivableRegistry.ReceivableDisputed.selector, RID));
        registry.fund(RID, _terms());
        vm.stopPrank();
    }

    function test_FundWithPermit_OneTransaction() public {
        (address permitFunder, uint256 pk) = makeAddrAndKey("permitFunder");
        _mint(permitFunder, 100_000_000);
        bytes32 role = registry.FUNDER_ROLE();
        vm.prank(admin);
        registry.grantRole(role, permitFunder);
        _confirmed(RID);

        uint256 advance = registry.advanceOf(RID);
        uint256 deadline = block.timestamp + 1 hours;
        bytes32 structHash = keccak256(
            abi.encode(
                keccak256("Permit(address owner,address spender,uint256 value,uint256 nonce,uint256 deadline)"),
                permitFunder,
                address(registry),
                advance,
                0,
                deadline
            )
        );
        bytes32 digest = keccak256(abi.encodePacked("\x19\x01", token.DOMAIN_SEPARATOR(), structHash));
        (uint8 v, bytes32 r, bytes32 s) = vm.sign(pk, digest);

        vm.prank(permitFunder);
        registry.fundWithPermit(RID, _terms(), deadline, v, r, s);
        assertEq(token.balanceOf(seller), advance);
        assertEq(registry.getReceivable(RID).funder, permitFunder);
    }

    // ------------------------------------------------------------ settlement

    function test_Pay_BeforeFundingReverts() public {
        _confirmed(RID);
        vm.startPrank(payer);
        token.approve(address(registry), F);
        vm.expectRevert();
        registry.pay(RID);
        vm.stopPrank();
    }

    function test_Pay_TwiceReverts() public {
        _funded(RID, funderA);
        _pay(RID);
        vm.startPrank(payer);
        token.approve(address(registry), F);
        vm.expectRevert();
        registry.pay(RID);
        vm.stopPrank();
    }

    function test_Pay_WhileDisputedHoldsFunds() public {
        _funded(RID, funderA);
        vm.prank(seller);
        registry.raiseDispute(RID);
        _pay(RID);

        IReceivableRegistry.Receivable memory r = registry.getReceivable(RID);
        assertTrue(r.paymentHeld);
        assertEq(uint8(r.status), uint8(IReceivableRegistry.Status.FUNDED));
        assertEq(registry.claimable(funderA), 0);
        assertEq(token.balanceOf(address(registry)), F);

        vm.prank(arbiter);
        registry.resolveDispute(RID, false);
        assertEq(uint8(_status(RID)), uint8(IReceivableRegistry.Status.SETTLED));
        assertEq(registry.claimable(funderA), 84_000_000);
        assertEq(registry.claimable(seller), 15_500_000);
    }

    function test_Withdraw_TwiceReverts() public {
        _funded(RID, funderA);
        _pay(RID);
        vm.startPrank(seller);
        registry.withdraw();
        vm.expectRevert(IReceivableRegistry.NothingToWithdraw.selector);
        registry.withdraw();
        vm.stopPrank();
    }

    // ------------------------------------------------------------ disputes

    function test_Dispute_OnlyPartiesCanRaise() public {
        _confirmed(RID);
        address stranger = makeAddr("stranger");
        vm.prank(stranger);
        vm.expectRevert(abi.encodeWithSelector(IReceivableRegistry.NotAuthorized.selector, stranger));
        registry.raiseDispute(RID);

        vm.prank(buyerSigner);
        registry.raiseDispute(RID);
        assertTrue(registry.getReceivable(RID).disputed);
    }

    function test_Dispute_UpheldBeforeFundingRejects() public {
        _confirmed(RID);
        vm.prank(buyerSigner);
        registry.raiseDispute(RID);
        vm.prank(arbiter);
        registry.resolveDispute(RID, true);
        assertEq(uint8(_status(RID)), uint8(IReceivableRegistry.Status.REJECTED));
    }

    function test_Dispute_OnlyArbiterResolves() public {
        _confirmed(RID);
        vm.prank(seller);
        registry.raiseDispute(RID);
        vm.prank(seller);
        vm.expectRevert();
        registry.resolveDispute(RID, false);
    }

    // ------------------------------------------------------------ overdue

    function test_MarkOverdue_BeforeGraceReverts() public {
        _funded(RID, funderA);
        vm.warp(dueDate + GRACE);
        vm.expectRevert();
        registry.markOverdue(RID);
    }

    function test_MarkOverdue_AfterGraceByAnyone() public {
        _funded(RID, funderA);
        vm.warp(dueDate + GRACE + 1);
        vm.prank(makeAddr("anyone"));
        registry.markOverdue(RID);
        assertTrue(registry.getReceivable(RID).overdue);
        // late payment still settles
        _pay(RID);
        assertEq(uint8(_status(RID)), uint8(IReceivableRegistry.Status.SETTLED));
    }

    // ------------------------------------------------------------ fuzz

    /// Waterfall conserves value for any valid terms: funder + platform + seller == F.
    function testFuzz_WaterfallConservesValue(uint256 amount, uint16 advanceBps, uint256 fee, uint16 platformBps)
        public
    {
        amount = bound(amount, 1_000, 1e15);
        advanceBps = uint16(bound(advanceBps, 1, 10_000));
        platformBps = uint16(bound(platformBps, 0, 1_000));
        uint256 advance = (amount * advanceBps) / 10_000;
        uint256 platform = (amount * platformBps) / 10_000;
        vm.assume(advance + platform <= amount);
        fee = bound(fee, 0, amount - advance - platform);

        bytes32 rid = keccak256(abi.encode(amount, advanceBps, fee, platformBps));
        vm.prank(verifier);
        registry.register(rid, BUYER_ID, seller, amount, dueDate, 1);
        vm.prank(verifier);
        registry.markVerified(rid);
        IReceivableRegistry.Confirmation memory c = IReceivableRegistry.Confirmation(
            rid, BUYER_ID, seller, amount, dueDate, advanceBps, fee, platformBps, 0, block.timestamp + 1 days
        );
        registry.confirm(c, buyerSigner, _sign(buyerKey, c));

        _mint(funderA, amount);
        vm.startPrank(funderA);
        token.approve(address(registry), advance);
        registry.fund(rid, IReceivableRegistry.Terms(advanceBps, fee, platformBps));
        vm.stopPrank();

        _mint(payer, amount);
        vm.startPrank(payer);
        token.approve(address(registry), amount);
        registry.pay(rid);
        vm.stopPrank();

        uint256 total = registry.claimable(funderA) + registry.claimable(treasury) + registry.claimable(seller);
        assertEq(total, amount);
        assertEq(registry.claimable(funderA), advance + fee);
    }
}

contract BuyerRegistryTest is Base {
    function test_AddRemoveSigner() public {
        address s = makeAddr("s");
        vm.startPrank(admin);
        buyers.addSigner(BUYER_ID, s);
        assertTrue(buyers.isSigner(BUYER_ID, s));
        buyers.removeSigner(BUYER_ID, s);
        assertFalse(buyers.isSigner(BUYER_ID, s));
        vm.stopPrank();
    }

    function test_OnlyRegistrar() public {
        vm.prank(seller);
        vm.expectRevert();
        buyers.addSigner(BUYER_ID, seller);
    }

    function test_DuplicateSignerReverts() public {
        vm.prank(admin);
        vm.expectRevert(abi.encodeWithSelector(BuyerRegistry.AlreadySigner.selector, BUYER_ID, buyerSigner));
        buyers.addSigner(BUYER_ID, buyerSigner);
    }
}
