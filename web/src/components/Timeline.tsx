"use client";
import { usePublicClient } from "wagmi";
import { useQuery } from "@tanstack/react-query";
import { parseAbiItem, type Hex } from "viem";
import { addresses, deployBlock, explorerTx } from "@/lib/chain";
import { short, vnd } from "@/lib/format";

// Every state transition emits one event indexed by receivableId (topic 1).
const EVENTS = [
  { label: "Đăng ký hóa đơn", item: parseAbiItem("event Registered(bytes32 indexed receivableId, bytes32 indexed buyerId, address indexed seller, uint256 amount, uint64 dueDate, uint8 keyVersion)") },
  { label: "Backend xác minh XML", item: parseAbiItem("event Verified(bytes32 indexed receivableId, address indexed verifier)") },
  { label: "Bên mua ký xác nhận", item: parseAbiItem("event Confirmed(bytes32 indexed receivableId, address indexed signer, (uint16 advanceBps, uint256 financingFee, uint16 platformFeeBps) terms)") },
  { label: "Tài trợ, tiền ứng đã chuyển", item: parseAbiItem("event Funded(bytes32 indexed receivableId, address indexed funder, address indexed seller, uint256 advance)") },
  { label: "Bên mua thanh toán", item: parseAbiItem("event Paid(bytes32 indexed receivableId, address indexed payer, uint256 amount, bool held)") },
  { label: "Phân chia tất toán", item: parseAbiItem("event Settled(bytes32 indexed receivableId, uint256 toFunder, uint256 toPlatform, uint256 toSeller)") },
  { label: "Mở tranh chấp", item: parseAbiItem("event DisputeRaised(bytes32 indexed receivableId, address indexed by)") },
  { label: "Giải quyết tranh chấp", item: parseAbiItem("event DisputeResolved(bytes32 indexed receivableId, address indexed arbiter, bool upheld)") },
  { label: "Đánh dấu quá hạn", item: parseAbiItem("event MarkedOverdue(bytes32 indexed receivableId, address indexed by)") },
  { label: "Từ chối", item: parseAbiItem("event Rejected(bytes32 indexed receivableId, address indexed by, string reason)") },
] as const;

function detail(name: string, args: Record<string, unknown>) {
  switch (name) {
    case "Registered": return vnd(args.amount as bigint);
    case "Funded": return `${vnd(args.advance as bigint)} → ${short(args.seller as string)}`;
    case "Paid": return `${vnd(args.amount as bigint)}${args.held ? " (giữ lại do tranh chấp)" : ""}`;
    case "Settled": return `Bên tài trợ ${vnd(args.toFunder as bigint)} · Nền tảng ${vnd(args.toPlatform as bigint)} · Nhà cung cấp ${vnd(args.toSeller as bigint)}`;
    case "Confirmed": return `Ký bởi ${short(args.signer as string)}`;
    case "Rejected": return String(args.reason);
    default: return "";
  }
}

export function Timeline({ id }: { id: Hex }) {
  const client = usePublicClient();
  const { data, isLoading } = useQuery({
    queryKey: ["timeline", id],
    enabled: !!client,
    refetchInterval: 5000,
    queryFn: async () => {
      const logs = (
        await Promise.all(
          EVENTS.map(async (e) =>
            (await client!.getLogs({ address: addresses.registry, event: e.item, args: { receivableId: id } as never, fromBlock: deployBlock }))
              .map((l) => ({ label: e.label, name: e.item.name, args: l.args as Record<string, unknown>, block: l.blockNumber, index: l.logIndex, tx: l.transactionHash })),
          ),
        )
      ).flat();
      return logs.sort((a, b) => (a.block === b.block ? a.index - b.index : Number(a.block - b.block)));
    },
  });
  if (isLoading) return <div className="card">Đang đọc event…</div>;
  return (
    <ol className="card relative space-y-4 border-l-0">
      {data?.length ? data.map((e) => (
        <li key={`${e.tx}-${e.index}`} className="flex gap-3">
          <span className="mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full bg-blue-600" />
          <div className="min-w-0">
            <p className="font-medium">{e.label}</p>
            <p className="text-sm text-slate-600">{detail(e.name, e.args)}</p>
            {explorerTx(e.tx) ? (
              <a className="font-mono text-xs text-blue-600 underline" href={explorerTx(e.tx)} target="_blank" rel="noreferrer">{short(e.tx)}</a>
            ) : <span className="font-mono text-xs text-slate-400">{short(e.tx)} · block {e.block.toString()}</span>}
          </div>
        </li>
      )) : <li className="text-sm text-slate-500">Chưa có event.</li>}
    </ol>
  );
}
