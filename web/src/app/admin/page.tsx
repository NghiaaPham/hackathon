"use client";
import { useEffect, useState } from "react";
import { useAccount, useReadContract, useReadContracts, useWriteContract } from "wagmi";
import { keccak256, toHex, type Address, type Hex } from "viem";
import { addresses, backendUrl } from "@/lib/chain";
import { buyerRegistryAbi } from "@/lib/abi";
import { short } from "@/lib/format";
import { TxResult } from "@/components/Receivable";

type Req = { id: string; taxCode: string; companyName: string; wallet: Address; buyerId: Hex; createdAt: string };
const REGISTRAR_ROLE = keccak256(toHex("REGISTRAR_ROLE"));

/** Operator console: review buyer signer requests and approve them on-chain from the registrar wallet. */
export default function AdminPage() {
  const { address } = useAccount();
  const [token, setToken] = useState("");
  const [reqs, setReqs] = useState<Req[]>([]);
  const [error, setError] = useState<string | null>(null);
  const add = useWriteContract();

  useEffect(() => setToken(sessionStorage.getItem("vt-admin") ?? ""), []);

  const { data: isRegistrar } = useReadContract({
    address: addresses.buyerRegistry, abi: buyerRegistryAbi, functionName: "hasRole",
    args: address ? [REGISTRAR_ROLE, address] : undefined, query: { enabled: !!address },
  });
  const { data: signerFlags, refetch } = useReadContracts({
    contracts: reqs.map((r) => ({ address: addresses.buyerRegistry, abi: buyerRegistryAbi, functionName: "isSigner", args: [r.buyerId, r.wallet] }) as const),
    query: { enabled: reqs.length > 0, refetchInterval: 4000 },
  });

  async function load() {
    setError(null);
    sessionStorage.setItem("vt-admin", token);
    const res = await fetch(`${backendUrl}/api/buyers/requests`, { headers: { "X-Admin-Token": token } });
    if (!res.ok) { setError(res.status === 401 ? "Sai admin token." : "Backend chưa bật trang duyệt (ADMIN_TOKEN)."); return; }
    setReqs(await res.json());
  }

  return (
    <div className="space-y-4">
      <div className="card flex flex-wrap items-end gap-3">
        <div className="grow"><label className="label">Admin token</label>
          <input className="input" type="password" value={token} onChange={(e) => setToken(e.target.value)} /></div>
        <button className="btn" onClick={load} disabled={!token}>Tải yêu cầu</button>
        <p className="w-full text-sm text-slate-500">
          Ví đang kết nối {address ? (isRegistrar ? "có" : "không có") : "—"} quyền REGISTRAR trên BuyerRegistry.
        </p>
        {error && <p className="w-full text-sm text-red-600">{error}</p>}
      </div>
      {reqs.length > 0 && (
        <div className="card overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="text-slate-500"><tr><th className="py-2">Công ty</th><th>MST</th><th>Ví</th><th>Trạng thái</th><th /></tr></thead>
            <tbody>
              {reqs.map((r, i) => {
                const approved = signerFlags?.[i]?.result === true;
                return (
                  <tr key={r.id} className="border-t border-slate-100">
                    <td className="py-2">{r.companyName}</td>
                    <td>{r.taxCode}</td>
                    <td className="font-mono">{short(r.wallet)}</td>
                    <td>{approved ? <span className="text-emerald-700">Đã duyệt</span> : "Chờ duyệt"}</td>
                    <td className="text-right">
                      {!approved && (
                        <button className="btn" disabled={!isRegistrar || add.isPending}
                          onClick={async () => { await add.writeContractAsync({ address: addresses.buyerRegistry, abi: buyerRegistryAbi, functionName: "addSigner", args: [r.buyerId, r.wallet] }); setTimeout(() => refetch(), 2500); }}>
                          Duyệt
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <TxResult hash={add.data} error={add.error} />
        </div>
      )}
    </div>
  );
}
