// SPDX-License-Identifier: MIT
pragma solidity 0.8.28;

import {Test} from "forge-std/Test.sol";
import {MockTVND} from "../src/mocks/MockTVND.sol";

contract MockTVNDTest is Test {
    MockTVND token;
    address alice = makeAddr("alice");

    function setUp() public {
        token = new MockTVND();
    }

    function test_DecimalsIsZero() public view {
        assertEq(token.decimals(), 0);
    }

    function test_FaucetMints() public {
        vm.prank(alice);
        token.faucet();
        assertEq(token.balanceOf(alice), token.FAUCET_AMOUNT());
    }

    function test_FaucetCooldown() public {
        vm.warp(1_000);
        vm.startPrank(alice);
        token.faucet();
        vm.expectRevert();
        token.faucet();
        vm.warp(1_000 + 1 hours);
        token.faucet();
        vm.stopPrank();
        assertEq(token.balanceOf(alice), 2 * token.FAUCET_AMOUNT());
    }

    function test_PermitSetsAllowance() public {
        (address owner, uint256 pk) = makeAddrAndKey("owner");
        address spender = makeAddr("registry");
        uint256 deadline = block.timestamp + 1 hours;
        bytes32 PERMIT_TYPEHASH =
            keccak256("Permit(address owner,address spender,uint256 value,uint256 nonce,uint256 deadline)");
        bytes32 structHash = keccak256(abi.encode(PERMIT_TYPEHASH, owner, spender, 80_000_000, 0, deadline));
        bytes32 digest = keccak256(abi.encodePacked("\x19\x01", token.DOMAIN_SEPARATOR(), structHash));
        (uint8 v, bytes32 r, bytes32 s) = vm.sign(pk, digest);
        token.permit(owner, spender, 80_000_000, deadline, v, r, s);
        assertEq(token.allowance(owner, spender), 80_000_000);
    }
}
