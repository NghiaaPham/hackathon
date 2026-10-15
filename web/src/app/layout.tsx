import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";
import { Providers } from "./providers";
import { ConnectButton, FaucetButton } from "@/components/Wallet";

export const metadata: Metadata = {
  title: "VeriTrade",
  description: "Verify once. Finance once. Settle transparently.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="vi">
      <body>
        <Providers>
          <header className="border-b border-slate-200 bg-white">
            <nav className="mx-auto flex max-w-5xl items-center gap-6 px-4 py-3">
              <Link href="/" className="font-bold text-blue-700">VeriTrade</Link>
              <Link href="/seller" className="text-sm text-slate-600 hover:text-slate-900">Nhà cung cấp</Link>
              <Link href="/buyer" className="text-sm text-slate-600 hover:text-slate-900">Bên mua</Link>
              <Link href="/funder" className="text-sm text-slate-600 hover:text-slate-900">Bên tài trợ</Link>
              <Link href="/admin" className="text-sm text-slate-400 hover:text-slate-900">Vận hành</Link>
              <div className="ml-auto flex items-center gap-3"><FaucetButton /><ConnectButton /></div>
            </nav>
          </header>
          <main className="mx-auto max-w-5xl px-4 py-8">{children}</main>
        </Providers>
      </body>
    </html>
  );
}
