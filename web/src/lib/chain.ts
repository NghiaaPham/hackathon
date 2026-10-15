import { avalancheFuji, anvil } from "viem/chains";
import type { Address } from "viem";

export const chains = [avalancheFuji, anvil] as const;
export const targetChain = process.env.NEXT_PUBLIC_CHAIN === "anvil" ? anvil : avalancheFuji;

export const addresses = {
  registry: (process.env.NEXT_PUBLIC_REGISTRY_ADDRESS ?? "0x9fE46736679d2D9a65F0992F2272dE9f3c7fa6e0") as Address,
  buyerRegistry: (process.env.NEXT_PUBLIC_BUYER_REGISTRY_ADDRESS ?? "0xe7f1725E7734CE288F8367e1Bb143E90bb3F0512") as Address,
  tvnd: (process.env.NEXT_PUBLIC_TVND_ADDRESS ?? "0x5FbDB2315678afecb367f032d93F642f64180aa3") as Address,
};

export const deployBlock = BigInt(process.env.NEXT_PUBLIC_DEPLOY_BLOCK ?? "0");
export const backendUrl = process.env.NEXT_PUBLIC_BACKEND_URL ?? "http://localhost:5080";
export const explorerTx = (hash: string) =>
  targetChain.id === avalancheFuji.id ? `https://testnet.snowtrace.io/tx/${hash}` : undefined;
