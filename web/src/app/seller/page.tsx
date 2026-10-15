"use client";
import { useState } from "react";
import Link from "next/link";
import { useAccount } from "wagmi";
import { backendUrl } from "@/lib/chain";
import { vnd } from "@/lib/format";

type Result = { receivableId: string; registerTx: string; verifyTx: string; invoice: { sellerName: string; buyerName: string; totalAmount: number; series: string; number: string } };

export default function SellerPage() {
  const { address } = useAccount();
  const [file, setFile] = useState<File | null>(null);
  const [due, setDue] = useState(() => new Date(Date.now() + 90 * 864e5).toISOString().slice(0, 10));
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<Result | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (!file || !address) return;
    setBusy(true); setError(null); setResult(null);
    const form = new FormData();
    form.append("file", file);
    form.append("sellerWallet", address);
    form.append("dueDate", new Date(due + "T00:00:00Z").toISOString());
    try {
      const res = await fetch(`${backendUrl}/api/receivables`, { method: "POST", body: form });
      const body = await res.json();
      if (!res.ok) throw new Error(res.status === 409 ? "Hóa đơn này đã được đăng ký trong mạng lưới." : body.detail ?? body.title);
      setResult(body);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="grid gap-6 md:grid-cols-2">
      <div className="card space-y-4">
        <h1 className="text-xl font-semibold">Đăng ký hóa đơn</h1>
        <div>
          <label className="label">Hóa đơn điện tử (XML đã ký số)</label>
          <input className="input" type="file" accept=".xml,text/xml" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
        </div>
        <div>
          <label className="label">Ngày đến hạn thanh toán</label>
          <input className="input" type="date" value={due} onChange={(e) => setDue(e.target.value)} />
        </div>
        <p className="text-xs text-slate-500">Tiền ứng trước sẽ chuyển vào ví đang kết nối. Số hóa đơn và MST không lên chain, chỉ có mã HMAC.</p>
        <button className="btn w-full" disabled={!file || !address || busy} onClick={submit}>
          {!address ? "Kết nối ví trước" : busy ? "Đang kiểm chữ ký và đăng ký…" : "Kiểm tra và đăng ký"}
        </button>
        {error && <p className="text-sm text-red-600">{error}</p>}
      </div>
      {result && (
        <div className="card space-y-3">
          <p className="font-semibold text-emerald-700">Hóa đơn hợp lệ, đã đăng ký</p>
          <p className="text-sm">{result.invoice.sellerName} → {result.invoice.buyerName}</p>
          <p className="text-sm">Ký hiệu {result.invoice.series}, số {result.invoice.number}: <b>{vnd(result.invoice.totalAmount)}</b></p>
          <p className="break-all font-mono text-xs text-slate-500">{result.receivableId}</p>
          <p className="text-sm text-slate-600">Gửi mã này cho bên mua để ký xác nhận.</p>
          <Link className="btn btn-ghost" href={`/r/${result.receivableId}`}>Xem dòng thời gian</Link>
        </div>
      )}
    </div>
  );
}
