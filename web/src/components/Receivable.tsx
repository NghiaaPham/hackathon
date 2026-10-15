"use client";
import { useReadContract } from "wagmi";
import { addresses } from "@/lib/chain";
import { registryAbi } from "@/lib/abi";
import { STATUS, STATUS_VI, short, vnd, waterfall } from "@/lib/format";
import type { Hex } from "viem";

export function useReceivable(id?: Hex) {
  return useReadContract({
    address: addresses.registry, abi: registryAbi, functionName: "getReceivable",
    args: id ? [id] : undefined, query: { enabled: !!id, refetchInterval: 4000 },
  });
}

export function StatusBadge({ status }: { status: number }) {
  const key = STATUS[status] ?? "NONE";
  const color =
    key === "SETTLED" ? "bg-emerald-100 text-emerald-800"
    : key === "FUNDED" ? "bg-blue-100 text-blue-800"
    : key === "REJECTED" ? "bg-red-100 text-red-800"
    : "bg-slate-100 text-slate-700";
  return <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${color}`}>{STATUS_VI[key]}</span>;
}

export function ReceivableSummary({ id }: { id: Hex }) {
  const { data: r, isLoading } = useReceivable(id);
  if (isLoading) return <div className="card">Đang đọc từ chain…</div>;
  if (!r || r.status === 0) return <div className="card">Không tìm thấy khoản phải thu này trên chain.</div>;
  const t = r.terms;
  const w = t.advanceBps ? waterfall(r.amount, t.advanceBps, t.financingFee, t.platformFeeBps) : undefined;
  return (
    <div className="card space-y-3">
      <div className="flex items-center justify-between">
        <span className="font-mono text-xs text-slate-500">{short(id)}</span>
        <div className="flex gap-2">
          {r.disputed && <span className="rounded-full bg-amber-100 px-2.5 py-0.5 text-xs text-amber-800">Đang tranh chấp</span>}
          {r.overdue && <span className="rounded-full bg-red-100 px-2.5 py-0.5 text-xs text-red-800">Quá hạn</span>}
          <StatusBadge status={r.status} />
        </div>
      </div>
      <dl className="grid grid-cols-2 gap-y-1 text-sm">
        <dt className="text-slate-500">Giá trị hóa đơn</dt><dd className="text-right font-semibold">{vnd(r.amount)}</dd>
        <dt className="text-slate-500">Đến hạn</dt><dd className="text-right">{new Date(Number(r.dueDate) * 1000).toLocaleDateString("vi-VN")}</dd>
        <dt className="text-slate-500">Nhà cung cấp</dt><dd className="text-right font-mono">{short(r.seller)}</dd>
        {r.funder !== "0x0000000000000000000000000000000000000000" && (<><dt className="text-slate-500">Bên tài trợ</dt><dd className="text-right font-mono">{short(r.funder)}</dd></>)}
      </dl>
      {w && (
        <div className="rounded-lg bg-slate-50 p-3 text-sm">
          <p className="mb-1 font-medium">Điều khoản bên mua đã ký</p>
          <div className="grid grid-cols-2 gap-y-1">
            <span className="text-slate-500">Ứng trước ({t.advanceBps / 100}%)</span><span className="text-right">{vnd(w.advance)}</span>
            <span className="text-slate-500">Phí tài trợ</span><span className="text-right">{vnd(t.financingFee)}</span>
            <span className="text-slate-500">Phí nền tảng ({t.platformFeeBps / 100}%)</span><span className="text-right">{vnd(w.toPlatform)}</span>
            <span className="text-slate-500">Nhà cung cấp nhận thêm khi tất toán</span><span className="text-right">{vnd(w.toSeller)}</span>
          </div>
        </div>
      )}
    </div>
  );
}

export function TxResult({ hash, error }: { hash?: string; error?: Error | null }) {
  if (error) return <p className="text-sm text-red-600">{(error as { shortMessage?: string }).shortMessage ?? error.message.split("\n")[0]}</p>;
  if (hash) return <p className="break-all text-sm text-emerald-700">Đã gửi giao dịch: <span className="font-mono">{hash}</span></p>;
  return null;
}
