export const STATUS = ["NONE", "REGISTERED", "VERIFIED", "BUYER_CONFIRMED", "FUNDED", "SETTLED", "REJECTED"] as const;
export const STATUS_VI: Record<(typeof STATUS)[number], string> = {
  NONE: "Chưa có",
  REGISTERED: "Đã đăng ký",
  VERIFIED: "Đã xác minh hóa đơn",
  BUYER_CONFIRMED: "Bên mua đã xác nhận",
  FUNDED: "Đã tài trợ",
  SETTLED: "Đã tất toán",
  REJECTED: "Bị từ chối",
};

export const vnd = (v: bigint | number) => `${new Intl.NumberFormat("vi-VN").format(v)} tVND`;
export const short = (a?: string) => (a ? `${a.slice(0, 6)}…${a.slice(-4)}` : "");
export const isBytes32 = (s: string) => /^0x[0-9a-fA-F]{64}$/.test(s);

/** Mirrors ReceivableRegistry._waterfall. */
export function waterfall(amount: bigint, advanceBps: number, financingFee: bigint, platformFeeBps: number) {
  const advance = (amount * BigInt(advanceBps)) / 10_000n;
  const toFunder = advance + financingFee;
  const toPlatform = (amount * BigInt(platformFeeBps)) / 10_000n;
  return { advance, toFunder, toPlatform, toSeller: amount - toFunder - toPlatform };
}
