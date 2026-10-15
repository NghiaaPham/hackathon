"use client";
import { useState } from "react";
import Link from "next/link";
import { useAccount, useReadContract, useSignTypedData, useWriteContract } from "wagmi";
import { parseSignature, type Hex } from "viem";
import { addresses, targetChain } from "@/lib/chain";
import { registryAbi, tvndAbi } from "@/lib/abi";
import { isBytes32, vnd } from "@/lib/format";
import { ReceivableSummary, TxResult, useReceivable } from "@/components/Receivable";

const permitTypes = {
  Permit: [
    { name: "owner", type: "address" }, { name: "spender", type: "address" }, { name: "value", type: "uint256" },
    { name: "nonce", type: "uint256" }, { name: "deadline", type: "uint256" },
  ],
} as const;

export default function FunderPage() {
  const { address } = useAccount();
  const [idInput, setIdInput] = useState("");
  const id = isBytes32(idInput) ? (idInput as Hex) : undefined;
  const { data: r } = useReceivable(id);
  const { data: advance } = useReadContract({ address: addresses.registry, abi: registryAbi, functionName: "advanceOf", args: id ? [id] : undefined, query: { enabled: !!id } });
  const { data: permitNonce } = useReadContract({ address: addresses.tvnd, abi: tvndAbi, functionName: "nonces", args: address ? [address] : undefined, query: { enabled: !!address } });
  const { data: claimable } = useReadContract({ address: addresses.registry, abi: registryAbi, functionName: "claimable", args: address ? [address] : undefined, query: { enabled: !!address, refetchInterval: 4000 } });
  const { signTypedDataAsync } = useSignTypedData();
  const fund = useWriteContract();
  const withdraw = useWriteContract();

  async function fundWithPermit() {
    if (!r || !id || !address || advance === undefined || permitNonce === undefined) return;
    const deadline = BigInt(Math.floor(Date.now() / 1000) + 1800);
    const sig = await signTypedDataAsync({
      domain: { name: "Test VND", version: "1", chainId: targetChain.id, verifyingContract: addresses.tvnd },
      types: permitTypes, primaryType: "Permit",
      message: { owner: address, spender: addresses.registry, value: advance, nonce: permitNonce, deadline },
    });
    const { v, r: sr, s } = parseSignature(sig);
    await fund.writeContractAsync({
      address: addresses.registry, abi: registryAbi, functionName: "fundWithPermit",
      args: [id, r.terms, deadline, Number(v ?? 27n), sr, s],
    });
  }

  return (
    <div className="grid gap-6 md:grid-cols-2">
      <div className="space-y-4">
        <div className="card space-y-2">
          <h1 className="text-xl font-semibold">Bên tài trợ</h1>
          <label className="label">Mã khoản phải thu</label>
          <input className="input font-mono" placeholder="0x…" value={idInput} onChange={(e) => setIdInput(e.target.value.trim())} />
        </div>
        {id && <ReceivableSummary id={id} />}
        {id && <Link className="text-sm text-blue-600 underline" href={`/r/${id}`}>Dòng thời gian</Link>}
      </div>
      <div className="space-y-4">
        {r?.status === 3 && !r.disputed && advance !== undefined && (
          <div className="card space-y-3">
            <h2 className="font-semibold">Tài trợ {vnd(advance)}</h2>
            <p className="text-sm text-slate-600">Một giao dịch: ký permit, contract chuyển {vnd(advance)} cho nhà cung cấp và ghi FUNDED cùng lúc. Nếu chuyển tiền thất bại, cả giao dịch hoàn lại.</p>
            <button className="btn w-full" disabled={fund.isPending} onClick={fundWithPermit}>{fund.isPending ? "Đang tài trợ…" : "Ký permit và tài trợ"}</button>
            <TxResult hash={fund.data} error={fund.error} />
          </div>
        )}
        <div className="card space-y-3">
          <h2 className="font-semibold">Số dư có thể rút</h2>
          <p className="text-2xl font-semibold">{claimable !== undefined ? vnd(claimable) : "…"}</p>
          <button className="btn btn-ghost w-full" disabled={!claimable || withdraw.isPending}
            onClick={() => withdraw.writeContract({ address: addresses.registry, abi: registryAbi, functionName: "withdraw" })}>
            Rút về ví
          </button>
          <TxResult hash={withdraw.data} error={withdraw.error} />
        </div>
      </div>
    </div>
  );
}
