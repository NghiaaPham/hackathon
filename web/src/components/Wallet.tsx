"use client";
import { useAccount, useConnect, useDisconnect, useSwitchChain, useWriteContract, useReadContract } from "wagmi";
import { targetChain, addresses } from "@/lib/chain";
import { tvndAbi } from "@/lib/abi";
import { short, vnd } from "@/lib/format";

export function ConnectButton() {
  const { address, chainId, isConnected } = useAccount();
  const { connect, connectors } = useConnect();
  const { disconnect } = useDisconnect();
  const { switchChain } = useSwitchChain();

  if (!isConnected)
    return (
      <button className="btn" onClick={() => connect({ connector: connectors[0] })}>
        Kết nối ví
      </button>
    );
  if (chainId !== targetChain.id)
    return (
      <button className="btn btn-warn" onClick={() => switchChain({ chainId: targetChain.id })}>
        Chuyển sang {targetChain.name}
      </button>
    );
  return (
    <button className="btn btn-ghost" onClick={() => disconnect()} title="Ngắt kết nối">
      {short(address)}
    </button>
  );
}

/** Demo mode for judges: 200,000,000 tVND per hour from the token faucet. */
export function FaucetButton() {
  const { address, chainId } = useAccount();
  const { data: balance, refetch } = useReadContract({
    address: addresses.tvnd, abi: tvndAbi, functionName: "balanceOf", args: address ? [address] : undefined,
    query: { enabled: !!address },
  });
  const { writeContractAsync, isPending, error } = useWriteContract();
  if (!address || chainId !== targetChain.id) return null;
  return (
    <div className="flex items-center gap-2 text-sm">
      <span className="text-slate-500">{balance !== undefined ? vnd(balance) : "…"}</span>
      <button
        className="btn btn-ghost"
        disabled={isPending}
        onClick={async () => {
          await writeContractAsync({ address: addresses.tvnd, abi: tvndAbi, functionName: "faucet" });
          setTimeout(() => refetch(), 2500);
        }}
        title={error ? error.message.split("\n")[0] : "Nhận 200.000.000 tVND (1 lần/giờ)"}
      >
        {isPending ? "Đang nhận…" : "Nhận tVND"}
      </button>
    </div>
  );
}
