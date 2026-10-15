// SPDX-License-Identifier: MIT
pragma solidity 0.8.28;

import {ERC20} from "@openzeppelin/contracts/token/ERC20/ERC20.sol";
import {ERC20Permit} from "@openzeppelin/contracts/token/ERC20/extensions/ERC20Permit.sol";

/// @title MockTVND
/// @notice Test-only stand-in for VND on testnet. Not a stablecoin, no real value.
/// @dev 0 decimals so 1 unit = 1 VND, which keeps demo numbers readable.
contract MockTVND is ERC20, ERC20Permit {
    uint256 public constant FAUCET_AMOUNT = 200_000_000; // 200 million tVND
    uint256 public constant FAUCET_COOLDOWN = 1 hours;

    mapping(address => uint256) public lastFaucetAt;

    error FaucetCooldown(uint256 availableAt);

    constructor() ERC20("Test VND", "tVND") ERC20Permit("Test VND") {}

    function decimals() public pure override returns (uint8) {
        return 0;
    }

    /// @notice Anyone can claim test tokens so judges can try the demo.
    function faucet() external {
        uint256 next = lastFaucetAt[msg.sender] + FAUCET_COOLDOWN;
        if (lastFaucetAt[msg.sender] != 0 && block.timestamp < next) revert FaucetCooldown(next);
        lastFaucetAt[msg.sender] = block.timestamp;
        _mint(msg.sender, FAUCET_AMOUNT);
    }
}
