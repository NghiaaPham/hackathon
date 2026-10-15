"use client";
import { useState } from "react";
import Link from "next/link";
import { useAccount, useReadContract, useSignTypedData, useWriteContract } from "wagmi";
import type { Hex } from "viem";
import { addresses, targetChain } from "@/lib/chain";
import { buyerRegistryAbi, registryAbi, tvndAbi } from "@/lib/abi";
import { isBytes32, vnd, waterfall } from "@/lib/format";
import { ReceivableSummary, TxResult, useReceivable } from "@/components/Receivable";

const types = {
  Confirmation: [
    { name: "receivableId", type: "bytes32" }, { name: "buyerId", type: "bytes32" }, { name: "seller", type: "address" },
    { name: "amount", type: "uint256" }, { name: "dueDate", type: "uint64" }, { name: "advanceBps", type: "uint16" },
    { name: "financingFee", type: "uint256" }, { name: "platformFeeBps", type: "uint16" }, { name: "nonce", type: "uint256" },
    { name: "deadline", type: "uint256" },
  ],
} as const;

export default function BuyerPage() {
  const { address } = useAccount();
  const [idInput, setIdInput] = useState("");
  const id = isBytes32(idInput) ? (idInput as Hex) : undefined;
  const { data: r } = useReceivable(id);
  const [advancePct, setAdvancePct] = useState(80);
  const [fee, setFee] = useState("4000000");
  const [platformBps, setPlatformBps] = useState(50);
  const { data: nonce } = useReadContract({
    address: addresses.registry, abi: registryAbi, functionName: "nonces", args: address ? [address] : undefined, query: { enabled: !!address },
  });
  const { data: isSigner } = useReadContract({
    address: addresses.buyerRegistry, abi: buyerRegistryAbi, functionName: "isSigner",
    args: r && address ? [r.buyerId, address] : undefined, query: { enabled: !!r && !!address },
  });
  const { signTypedDataAsync } = useSignTypedData();
  const confirm = useWriteContract();
  const approve = useWriteContract();
  const pay = useWriteContract();

  const terms = { advanceBps: Math.round(advancePct * 100), financingFee: BigInt(fee || "0"), platformFeeBps: platformBps };
  const w = r ? waterfall(r.amount, terms.advanceBps, terms.financingFee, terms.platformFeeBps) : undefined;

  async function signAndConfirm() {
    if (!r || !id || nonce === undefined || !address) return;
    const message = {
      receivableId: id, buyerId: r.buyerId, seller: r.seller, amount: r.amount, dueDate: r.dueDate,
      advanceBps: terms.advanceBps, financingFee: terms.financingFee, platformFeeBps: terms.platformFeeBps,
      nonce, deadline: BigInt(Math.floor(Date.now() / 1000) + 3600),
    };
    const signature = await signTypedDataAsync({
      domain: { name: "VeriTrade", version: "1", chainId: targetChain.id, verifyingContract: addresses.registry },
      types, primaryType: "Confirmation", message,
    });
    await confirm.writeContractAsync({ address: addresses.registry, abi: registryAbi, functionName: "confirm", args: [message, address, signature] });
  }

  async function payNow() {
    if (!r || !id) return;
    await approve.writeContractAsync({ address: addresses.tvnd, abi: tvndAbi, functionName: "approve", args: [addresses.registry, r.amount] });
    await pay.writeContractAsync({ address: addresses.registry, abi: registryAbi, functionName: "pay", args: [id] });
  }

  return (
    <div className="grid gap-6 md:grid-cols-2">
      <div className="space-y-4">
        <div className="card space-y-2">
          <h1 className="text-xl font-semibold">Bên mua</h1>
          <label className="label">Mã khoản phải thu</label>
          <input className="input font-mono" placeholder="0x…" value={idInput} onChange={(e) => setIdInput(e.target.value.trim())} />
        </div>
        {id && <ReceivableSummary id={id} />}
        {id && <Link className="text-sm text-blue-600 underline" href={`/r/${id}`}>Dòng thời gian</Link>}
      </div>

      {r?.status === 2 && address && isSigner === false && (
        <div className="card space-y-3">
          <h2 className="font-semibold">Ví này chưa được duyệt cho bên mua của hóa đơn</h2>
          <p className="text-sm text-slate-600">Chỉ ví đã đăng ký cho đúng MST bên mua mới ký xác nhận được.</p>
          <Link className="btn" href="/buyer/register">Đăng ký ví cho doanh nghiệp</Link>
        </div>
      )}

      {r?.status === 2 && isSigner && (
        <div className="card space-y-4">
          <h2 className="font-semibold">Xác nhận khoản nợ và điều khoản tài trợ</h2>
          <div className="grid grid-cols-3 gap-3">
            <div><label className="label">Ứng trước %</label><input className="input" type="number" min={1} max={100} value={advancePct} onChange={(e) => setAdvancePct(Number(e.target.value))} /></div>
            <div><label className="label">Phí tài trợ</label><input className="input" value={fee} onChange={(e) => setFee(e.target.value.replace(/\D/g, ""))} /></div>
            <div><label className="label">Phí nền tảng (bps)</label><input className="input" type="number" value={platformBps} onChange={(e) => setPlatformBps(Number(e.target.value))} /></div>
          </div>
          {w && (
            <p className="text-sm text-slate-600">
              Nhà cung cấp nhận ngay {vnd(w.advance)}. Khi bạn trả {vnd(r.amount)}: bên tài trợ {vnd(w.toFunder)}, nền tảng {vnd(w.toPlatform)}, nhà cung cấp {vnd(w.toSeller)}.
            </p>
          )}
          <p className="text-xs text-slate-500">Chữ ký chỉ hợp lệ trên {targetChain.name}, cho contract này, trong 1 giờ.</p>
          <button className="btn w-full" disabled={!address || confirm.isPending || (w && w.toSeller < 0n)} onClick={signAndConfirm}>
            {confirm.isPending ? "Đang gửi…" : "Ký EIP-712 và xác nhận"}
          </button>
          <TxResult hash={confirm.data} error={confirm.error} />
        </div>
      )}

      {r?.status === 4 && !r.paymentHeld && (
        <div className="card space-y-4">
          <h2 className="font-semibold">Thanh toán đến hạn</h2>
          <p className="text-sm text-slate-600">Trả {vnd(r.amount)} vào contract. Contract chia tiền theo điều khoản bạn đã ký.</p>
          <button className="btn w-full" disabled={approve.isPending || pay.isPending} onClick={payNow}>
            {approve.isPending ? "Đang duyệt tVND…" : pay.isPending ? "Đang thanh toán…" : "Duyệt và thanh toán"}
          </button>
          <TxResult hash={pay.data} error={approve.error ?? pay.error} />
        </div>
      )}
    </div>
  );
}
