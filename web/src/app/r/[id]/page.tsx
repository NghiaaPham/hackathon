"use client";
import { use } from "react";
import type { Hex } from "viem";
import { isBytes32 } from "@/lib/format";
import { ReceivableSummary } from "@/components/Receivable";
import { Timeline } from "@/components/Timeline";

/** Public status page: reads straight from the chain, works even when the backend is down. */
export default function ReceivablePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  if (!isBytes32(id)) return <div className="card">Mã không hợp lệ.</div>;
  return (
    <div className="grid gap-6 md:grid-cols-2">
      <ReceivableSummary id={id as Hex} />
      <div>
        <h2 className="mb-3 font-semibold">Dòng thời gian on-chain</h2>
        <Timeline id={id as Hex} />
      </div>
    </div>
  );
}
