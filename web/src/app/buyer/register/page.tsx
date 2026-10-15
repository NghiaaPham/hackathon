"use client";
import { useState } from "react";
import { useAccount, useSignMessage } from "wagmi";
import { backendUrl } from "@/lib/chain";

// Must match BuyerOnboardingService.ProofMessage on the backend byte for byte.
const proofMessage = (taxCode: string, wallet: string) =>
  `VeriTrade: yeu cau quyen ky xac nhan cho MST ${taxCode}\nVi: ${wallet.toLowerCase()}`;

export default function BuyerRegisterPage() {
  const { address } = useAccount();
  const { signMessageAsync } = useSignMessage();
  const [taxCode, setTaxCode] = useState("");
  const [company, setCompany] = useState("");
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const validMst = /^\d{10}(-\d{3})?$/.test(taxCode.trim());

  async function submit() {
    if (!address) return;
    setBusy(true); setError(null);
    try {
      const mst = taxCode.trim();
      const signature = await signMessageAsync({ message: proofMessage(mst, address) });
      const res = await fetch(`${backendUrl}/api/buyers/requests`, {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ taxCode: mst, companyName: company, wallet: address, signature }),
      });
      if (!res.ok) throw new Error((await res.json()).detail ?? "Gửi yêu cầu thất bại.");
      setDone(true);
    } catch (e) {
      setError((e as { shortMessage?: string }).shortMessage ?? (e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto max-w-lg space-y-4">
      <div className="card space-y-4">
        <h1 className="text-xl font-semibold">Đăng ký ví ký xác nhận cho bên mua</h1>
        <p className="text-sm text-slate-600">
          Chỉ ví đã được duyệt cho MST của bạn mới ký xác nhận được khoản phải thu. Việc duyệt do đơn vị vận hành làm sau khi
          kiểm tra doanh nghiệp, để nhà cung cấp không thể tự xác nhận hóa đơn của chính mình.
        </p>
        <div><label className="label">Mã số thuế bên mua</label>
          <input className="input" placeholder="0109876543" value={taxCode} onChange={(e) => setTaxCode(e.target.value)} /></div>
        <div><label className="label">Tên công ty</label>
          <input className="input" value={company} onChange={(e) => setCompany(e.target.value)} /></div>
        <button className="btn w-full" disabled={!address || !validMst || company.trim().length < 2 || busy || done} onClick={submit}>
          {!address ? "Kết nối ví trước" : busy ? "Đang ký và gửi…" : done ? "Đã gửi" : "Ký chứng minh sở hữu ví và gửi"}
        </button>
        {error && <p className="text-sm text-red-600">{error}</p>}
        {done && <p className="text-sm text-emerald-700">Đã gửi yêu cầu. Khi được duyệt, ví này ký được mọi khoản phải thu gửi tới MST {taxCode.trim()}.</p>}
      </div>
    </div>
  );
}
